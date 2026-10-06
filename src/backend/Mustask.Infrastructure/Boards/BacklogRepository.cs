using Mustask.Application.Abstractions.Boards;
using Mustask.Domain.Boards;
using Mustask.Domain.Organizations;
using Microsoft.EntityFrameworkCore;

namespace Mustask.Infrastructure.Boards;

/// <summary>
/// <see cref="IBacklogRepository"/> over <c>backlog_orders</c> and
/// <c>backlog_places</c>. The locks are raw SQL (ADR-0011); the places are
/// written by two set-based statements, not through change tracking, because a
/// reorder rewrites up to the whole backlog (NFR: 5 000 items within 1 s), and a
/// set-based statement waits on any row another transaction holds and
/// re-evaluates its condition on the committed row — so a reorder of another
/// backlog that took or dropped the same item's place meanwhile is never undone
/// by a stale read.
/// </summary>
internal sealed class BacklogRepository(MustaskDbContext context) : IBacklogRepository
{
    public async Task<bool> LockAsync(Guid projectId, Guid teamId, Guid levelId, CancellationToken cancellationToken)
    {
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM teams WHERE id = {teamId} AND project_id = {projectId} FOR SHARE",
            cancellationToken);

        // A new statement under READ COMMITTED: a team deleted by the lock's previous holder reads deleted.
        var active = await context.Teams.AnyAsync(
            team => team.Id == teamId && team.ProjectId == projectId && team.Status == TeamStatus.Active,
            cancellationToken);

        if (!active)
        {
            return false;
        }

        // A concurrent first reorder of the backlog waits here on the other's uncommitted key, then does nothing.
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO backlog_orders (team_id, backlog_level_id) VALUES ({teamId}, {levelId}) ON CONFLICT (team_id, backlog_level_id) DO NOTHING",
            cancellationToken);

        await context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM backlog_orders WHERE team_id = {teamId} AND backlog_level_id = {levelId} FOR UPDATE",
            cancellationToken);

        return true;
    }

    public async Task<IReadOnlyList<StackRankEntry>> ReadEntriesAsync(BacklogScope scope, CancellationToken cancellationToken)
    {
        var tree = await BacklogMembership.AreaTreeAsync(context, scope.ProjectId, cancellationToken);

        return await BacklogMembership.Members(context, scope, BacklogMembership.TeamAreas(tree, scope.TeamId))
            .Select(member => new StackRankEntry(member.Item.Id, member.Item.Number, member.Place))
            .ToListAsync(cancellationToken);
    }

    public async Task SaveOrderAsync(BacklogScope scope, IReadOnlyList<Guid> order, CancellationToken cancellationToken)
    {
        var ids = order.ToArray();

        // The places this backlog holds for items that have left it: found here, so the delete names its few rows by key
        // instead of testing every place against the whole order. Under the backlog's lock no other reorder of it adds a
        // place; one of another backlog may take a row meanwhile, which the delete's own condition then skips.
        var held = await context.Set<BacklogPlace>()
            .AsNoTracking()
            .Where(place => place.TeamId == scope.TeamId && place.BacklogLevelId == scope.LevelId)
            .Select(place => place.WorkItemId)
            .ToListAsync(cancellationToken);

        var departed = held.Except(ids).ToArray();

        if (departed.Length > 0)
        {
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"DELETE FROM backlog_places WHERE work_item_id = ANY({departed}) AND team_id = {scope.TeamId} AND backlog_level_id = {scope.LevelId}",
                cancellationToken);
        }

        // Every item at the place it stands at; an item placed on another backlog moves its one row here. A row already
        // holding its value is not rewritten.
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"""
             INSERT INTO backlog_places (work_item_id, team_id, backlog_level_id, position)
             SELECT moved.id, {scope.TeamId}, {scope.LevelId}, moved.ordinal::integer
             FROM unnest({ids}) WITH ORDINALITY AS moved(id, ordinal)
             ON CONFLICT (work_item_id) DO UPDATE
             SET team_id = excluded.team_id, backlog_level_id = excluded.backlog_level_id, position = excluded.position
             WHERE (backlog_places.team_id, backlog_places.backlog_level_id, backlog_places.position)
                 IS DISTINCT FROM (excluded.team_id, excluded.backlog_level_id, excluded.position)
             """,
            cancellationToken);
    }
}
