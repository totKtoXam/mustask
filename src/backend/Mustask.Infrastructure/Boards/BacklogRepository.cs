using Mustask.Application.Abstractions.Boards;
using Mustask.Domain.Boards;
using Mustask.Domain.Organizations;
using Mustask.Infrastructure.WorkItems;
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
/// <remarks>
/// mod-017 (BR-OT-6, BR-OT-13 (3), plan-backend D-10). A team place names the team that owns its item (ADR-0028 rule 5), and a
/// reorder writes a place for <i>every</i> member, not only for the one it moves, so the members must not change owner
/// while the reorder runs. The order of the locks is ADR-0028 rule 8's: the team <c>FOR SHARE</c> (step 2), the member items
/// <c>FOR KEY SHARE</c> in id order (step 5), the order row <c>FOR UPDATE</c> (step 6), and then the members <b>read again in
/// a locking statement</b>. A save of an item locks it <c>FOR UPDATE</c> first, so it conflicts with the key share: the
/// reorder first, the save waits and then drops the place the reorder wrote; the save first, the reorder's locking read
/// no longer finds the item a member. The place write's own foreign-key check onto the item is a key share too and never
/// waits on this reorder's.
/// </remarks>
internal sealed class BacklogRepository(MustaskDbContext context) : IBacklogRepository
{
    public async Task<bool> LockAsync(BacklogScope scope, CancellationToken cancellationToken)
    {
        var (projectId, teamId, levelId) = (scope.ProjectId, scope.TeamId, scope.LevelId);
        var typeIds = scope.TypeIds.ToArray();
        var stateIds = scope.ShownStateIds.ToArray();

        // Step 2: the team row. A team belongs to the organization (feat-028), so the project's organization is read through the
        // project, which the handler has resolved under the organization of the address already.
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"""
             SELECT 1
             FROM teams
             WHERE id = {teamId}
               AND organization_id = (SELECT organization_id FROM projects WHERE id = {projectId})
             FOR SHARE
             """,
            cancellationToken);

        // A new statement under READ COMMITTED: a team deleted by the lock's previous holder reads deleted.
        var active = await context.Teams.AnyAsync(
            team => team.Id == teamId && team.Status == TeamStatus.Active,
            cancellationToken);

        if (!active)
        {
            return false;
        }

        // Step 3: the link between the project and the team (feat-028 BR-OL-11): an unlink or a deletion holds it FOR NO KEY UPDATE,
        // so the reorder either commits before the unlink reassigns the items (the unlink then drops the places it wrote) or reads the
        // link gone and writes nothing. The team row came first: no request holds a link and waits for a team row (plan-backend § 7.4).
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM project_teams WHERE project_id = {projectId} AND team_id = {teamId} FOR SHARE",
            cancellationToken);

        var linked = await context.ProjectTeams.AnyAsync(
            link => link.ProjectId == projectId && link.TeamId == teamId,
            cancellationToken);

        if (!linked)
        {
            return false;
        }

        // The members, in id order: ORDER BY is applied before the locks are taken, one fixed order for every caller.
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"""
             SELECT 1
             FROM work_items
             WHERE project_id = {projectId} AND owning_team_id = {teamId} AND type_id = ANY({typeIds}) AND state_id = ANY({stateIds})
             ORDER BY id
             FOR KEY SHARE
             """,
            cancellationToken);

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
        var (projectId, teamId, levelId) = (scope.ProjectId, scope.TeamId, scope.LevelId);
        var typeIds = scope.TypeIds.ToArray();
        var stateIds = scope.ShownStateIds.ToArray();

        // The membership of BacklogMembership (the project's items of the level's types in a shown state that the team owns)
        // as a locking statement: raw SQL, parameterized, for row locking (backend.md "Raw SQL"). A member read here stays a
        // member until the commit; one that changed owner before this read is not read.
        var rows = await context.Database
            .SqlQuery<MemberRow>(
                $"""
                 SELECT item.id AS "ItemId",
                        item.number AS "Number",
                        CASE WHEN place.team_id = {teamId} AND place.backlog_level_id = {levelId} THEN place.position END AS "Place"
                 FROM work_items AS item
                 LEFT JOIN backlog_places AS place ON place.work_item_id = item.id
                 WHERE item.project_id = {projectId}
                   AND item.owning_team_id = {teamId}
                   AND item.type_id = ANY({typeIds})
                   AND item.state_id = ANY({stateIds})
                 ORDER BY item.id
                 FOR KEY SHARE OF item
                 """)
            .ToListAsync(cancellationToken);

        return rows.Select(row => new StackRankEntry(row.ItemId, row.Number, row.Place)).ToList();
    }

    public async Task SaveOrderAsync(BacklogScope scope, IReadOnlyList<Guid> order, CancellationToken cancellationToken)
    {
        var ids = order.ToArray();

        // The places this backlog holds for items that have left it by state: found here, so the delete names its few rows by
        // key instead of testing every place against the whole order. (An item that changed owner left no place: the save
        // dropped it in its own write.) Under the backlog's lock no other reorder of it adds a place; one of another backlog
        // may take a row meanwhile, which the delete's own condition then skips.
        // feat-028 D-13 (OQ-3 A): only the places on THIS project's items are read — a team is linked to several projects and its
        // order is one per level, so another project's places are that project's view and are never "departed" from this one.
        // mod-005 (BR-DR-10): in any state — a deleted item's place is a place this backlog still holds, and the reorder drops it as
        // it dropped it before the project filter (a restore then returns the item below every placed item, not to a stale place).
        var held = await (
                from place in context.Set<BacklogPlace>().AsNoTracking()
                join item in WorkItemSets.InAnyState(context) on place.WorkItemId equals item.Id
                where place.TeamId == scope.TeamId && place.BacklogLevelId == scope.LevelId && item.ProjectId == scope.ProjectId
                select place.WorkItemId)
            .ToListAsync(cancellationToken);

        var departed = held.Except(ids).ToArray();

        if (departed.Length > 0)
        {
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"DELETE FROM backlog_places WHERE work_item_id = ANY({departed}) AND team_id = {scope.TeamId} AND backlog_level_id = {scope.LevelId}",
                cancellationToken);
        }

        // Every item at the place it stands at; an item placed on another backlog moves its one row here. A row already
        // holding its value is not rewritten. Every item written is a member the reorder holds locked, so the place's key
        // onto the item's owner cannot fail.
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

    /// <summary>A member of the backlog as the locking statement reads it.</summary>
    private sealed class MemberRow
    {
        public Guid ItemId { get; set; }

        public int Number { get; set; }

        public int? Place { get; set; }
    }
}
