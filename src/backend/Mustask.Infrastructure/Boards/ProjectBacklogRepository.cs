using Mustask.Application.Abstractions.Boards;
using Mustask.Domain.Boards;
using Mustask.Infrastructure.Organizations;
using Microsoft.EntityFrameworkCore;

namespace Mustask.Infrastructure.Boards;

/// <summary>
/// <see cref="IProjectBacklogRepository"/> over <c>project_backlog_orders</c> and <c>project_backlog_places</c>
/// (feat-039, ADR-0028 rule 9). As <see cref="BacklogRepository"/>: the locks are raw SQL (ADR-0011); the places are
/// written by two set-based statements, not through change tracking, because a reorder rewrites up to the whole
/// backlog (NFR: 5 000 items within 1 s). It reads and writes no team table: a reorder of a team's backlog neither
/// waits for nor undoes a project's.
/// </summary>
internal sealed class ProjectBacklogRepository(MustaskDbContext context) : IProjectBacklogRepository
{
    public async Task<bool> LockAsync(Guid organizationId, Guid projectId, Guid levelId, CancellationToken cancellationToken)
    {
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM projects WHERE id = {projectId} AND organization_id = {organizationId} FOR SHARE",
            cancellationToken);

        // A new statement under READ COMMITTED: a project deactivated by the lock's previous holder reads deactivated.
        if (!await context.ReachableProjects(organizationId).AnyAsync(project => project.Id == projectId, cancellationToken))
        {
            return false;
        }

        // A concurrent first reorder of the backlog waits here on the other's uncommitted key, then does nothing.
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO project_backlog_orders (project_id, backlog_level_id) VALUES ({projectId}, {levelId}) ON CONFLICT (project_id, backlog_level_id) DO NOTHING",
            cancellationToken);

        await context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM project_backlog_orders WHERE project_id = {projectId} AND backlog_level_id = {levelId} FOR UPDATE",
            cancellationToken);

        return true;
    }

    public async Task<IReadOnlyList<StackRankEntry>> ReadEntriesAsync(ProjectBacklogScope scope, CancellationToken cancellationToken) =>
        await ProjectBacklogMembership.Members(context, scope)
            .Select(member => new StackRankEntry(member.Item.Id, member.Item.Number, member.Place, member.Item.CreatedAt))
            .ToListAsync(cancellationToken);

    public async Task SaveOrderAsync(ProjectBacklogScope scope, IReadOnlyList<Guid> order, CancellationToken cancellationToken)
    {
        var ids = order.ToArray();

        // The places this backlog holds for items that have left it (they entered the Removed category): found here, so
        // the delete names its few rows by key instead of testing every place against the whole order. Under the
        // backlog's lock no other reorder of it adds a place.
        var held = await context.Set<ProjectBacklogPlace>()
            .AsNoTracking()
            .Where(place => place.ProjectId == scope.ProjectId && place.BacklogLevelId == scope.LevelId)
            .Select(place => place.WorkItemId)
            .ToListAsync(cancellationToken);

        var departed = held.Except(ids).ToArray();

        if (departed.Length > 0)
        {
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"DELETE FROM project_backlog_places WHERE work_item_id = ANY({departed}) AND project_id = {scope.ProjectId} AND backlog_level_id = {scope.LevelId}",
                cancellationToken);
        }

        // Every item at the place it stands at; a row already holding its value is not rewritten.
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"""
             INSERT INTO project_backlog_places (work_item_id, project_id, backlog_level_id, position)
             SELECT moved.id, {scope.ProjectId}, {scope.LevelId}, moved.ordinal::integer
             FROM unnest({ids}) WITH ORDINALITY AS moved(id, ordinal)
             ON CONFLICT (work_item_id) DO UPDATE
             SET project_id = excluded.project_id, backlog_level_id = excluded.backlog_level_id, position = excluded.position
             WHERE (project_backlog_places.project_id, project_backlog_places.backlog_level_id, project_backlog_places.position)
                 IS DISTINCT FROM (excluded.project_id, excluded.backlog_level_id, excluded.position)
             """,
            cancellationToken);
    }
}
