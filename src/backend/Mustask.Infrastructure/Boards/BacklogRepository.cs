using Mustask.Application.Abstractions.Boards;
using Mustask.Domain.Boards;
using Mustask.Domain.Organizations;
using Mustask.Infrastructure.Dictionaries;
using Mustask.Shared.Errors;
using Mustask.Shared.Exceptions;
using Microsoft.EntityFrameworkCore;
using Npgsql;

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
/// <para>
/// mod-017 (BR-OT-6, BR-OT-13 (3), plan-backend D-10). A team place names the team that owns its item (ADR-0028 rule 5), and a
/// reorder writes a place for <i>every</i> member, not only for the one it moves, so the members must not change owner
/// while the reorder runs. The order of the locks is ADR-0028 rule 8's: the team <c>FOR SHARE</c> (step 2), the member items
/// <c>FOR KEY SHARE</c> in id order (step 5), the order row <c>FOR UPDATE</c> (step 6), and then the members <b>read again in
/// a locking statement</b>. A save of an item locks it <c>FOR UPDATE</c> first, so it conflicts with the key share: the
/// reorder first, the save waits and then drops the place the reorder wrote; the save first, the reorder's locking read
/// no longer finds the item a member. The place write's own foreign-key check onto the item is a key share too and never
/// waits on this reorder's.
/// </para>
/// <para>
/// feat-040 (BR-TS-6, BR-TS-7, plan-backend PD-8, PD-9, PD-11): the backlog spans the team's projects, so the members are the
/// items the team owns in the active projects of the organization, found by a join on <c>projects</c> that states the
/// organization (the tenant predicate) and the project's status in the same snapshot as the items. <b>No project row and no
/// project–team link is locked</b>: a deactivation or a restore of a project is not serialized with a reorder (the reorder works
/// on the members its locking statement reads, and a deactivated project's places are kept); an unlink, a team deletion and a
/// save that changes a member's team lock the item rows (or the team) the key shares order them with, and every party takes the
/// item locks in id order, so there is no cycle.
/// </para>
/// </remarks>
internal sealed class BacklogRepository(MustaskDbContext context) : IBacklogRepository
{
    /// <summary>The place's key onto its item: an item gone between the locking read and the write.</summary>
    private const string PlaceItemKey = "fk_backlog_places_work_items_work_item_id";

    /// <summary>The place's key onto the item's current owner: the team must own the item the place names (ADR-0028 rule 5).</summary>
    private const string PlaceOwnerKey = "fk_backlog_places_work_items_owner";

    public async Task<bool> LockAsync(Guid organizationId, TeamBacklogScope scope, CancellationToken cancellationToken)
    {
        // The reorder scope carries no project filter (the team backlog's reorder is of the whole backlog): the locking
        // statements below state the membership without one, and a test pins them to BacklogMembership.
        if (scope.Filter.ProjectsSet || scope.Filter.TeamsSet)
        {
            throw new InvalidOperationException("A team backlog's reorder is of the whole backlog; its scope carries no filter.");
        }

        var (teamId, levelId) = (scope.TeamId, scope.LevelId);
        var typeIds = scope.TypeIds.ToArray();
        var stateIds = scope.ShownStateIds.ToArray();

        // Step 2: the team row, found by the organization of the address and its id.
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM teams WHERE id = {teamId} AND organization_id = {organizationId} FOR SHARE",
            cancellationToken);

        // A new statement under READ COMMITTED: a team deleted by the lock's previous holder reads deleted.
        var active = await context.Teams.AnyAsync(
            team => team.Id == teamId && team.OrganizationId == organizationId && team.Status == TeamStatus.Active,
            cancellationToken);

        if (!active)
        {
            return false;
        }

        // Step 5: the members, in id order: ORDER BY is applied before the locks are taken, one fixed order for every caller.
        // FOR KEY SHARE OF item locks the items and nothing else — no project row is touched (PD-8).
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"""
             SELECT 1
             FROM work_items AS item
             JOIN projects AS project ON project.id = item.project_id
             WHERE item.owning_team_id = {teamId}
               AND project.organization_id = {organizationId}
               AND project.status = 'Active'
               AND item.type_id = ANY({typeIds})
               AND item.state_id = ANY({stateIds})
             ORDER BY item.id
             FOR KEY SHARE OF item
             """,
            cancellationToken);

        // Step 6: a concurrent first reorder of the backlog waits here on the other's uncommitted key, then does nothing.
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO backlog_orders (team_id, backlog_level_id) VALUES ({teamId}, {levelId}) ON CONFLICT (team_id, backlog_level_id) DO NOTHING",
            cancellationToken);

        await context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM backlog_orders WHERE team_id = {teamId} AND backlog_level_id = {levelId} FOR UPDATE",
            cancellationToken);

        return true;
    }

    public async Task<IReadOnlyList<StackRankEntry>> ReadEntriesAsync(Guid organizationId, TeamBacklogScope scope, CancellationToken cancellationToken)
    {
        var (teamId, levelId) = (scope.TeamId, scope.LevelId);
        var typeIds = scope.TypeIds.ToArray();
        var stateIds = scope.ShownStateIds.ToArray();

        // The membership of BacklogMembership (the items the team owns in the organization's active projects, of the level's types,
        // in a shown state) as a locking statement: raw SQL, parameterized, for row locking (backend.md "Raw SQL"). A member
        // read here stays a member until the commit; one that changed owner before this read is not read. A test pins it to the
        // membership.
        var rows = await context.Database
            .SqlQuery<MemberRow>(
                $"""
                 SELECT item.id AS "ItemId",
                        item.number AS "Number",
                        CASE WHEN place.team_id = {teamId} AND place.backlog_level_id = {levelId} THEN place.position END AS "Place",
                        item.created_at AS "CreatedAt",
                        project.key AS "ProjectKey"
                 FROM work_items AS item
                 JOIN projects AS project ON project.id = item.project_id
                 LEFT JOIN backlog_places AS place ON place.work_item_id = item.id
                 WHERE item.owning_team_id = {teamId}
                   AND project.organization_id = {organizationId}
                   AND project.status = 'Active'
                   AND item.type_id = ANY({typeIds})
                   AND item.state_id = ANY({stateIds})
                 ORDER BY item.id
                 FOR KEY SHARE OF item
                 """)
            .ToListAsync(cancellationToken);

        return rows.Select(row => new StackRankEntry(row.ItemId, row.Number, row.Place, row.CreatedAt, row.ProjectKey)).ToList();
    }

    public async Task SaveOrderAsync(Guid organizationId, TeamBacklogScope scope, IReadOnlyList<Guid> order, CancellationToken cancellationToken)
    {
        var ids = order.ToArray();
        var typeIds = scope.TypeIds.ToArray();
        var stateIds = scope.ShownStateIds.ToArray();

        // The places this backlog holds for items that have left it — no longer owned by the team, of another type, or in the
        // Removed category — in the active projects of the organization (feat-040 BR-TS-7: the team's order is one per level across
        // its projects). Decided by a predicate over facts the reorder holds locked or that cannot change under it, in the one
        // statement (PD-9), not by "held minus the order" in memory: that would drop the kept place of a restored project's items if
        // the project were restored between the locking read (inactive, so not read) and the read of the held places (active). A
        // deactivated project's places are left exactly as they are (ADR-0028 rule 9), and so is the place of any item that is
        // still a member by owner, type and state, whatever the project's status at the statement's snapshot.
        // mod-005 (BR-DR-10): in any state — a deleted item's place is a place this backlog still holds, and the reorder drops it
        // (a restore then returns the item below every placed item, not to a stale place).
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"""
             DELETE FROM backlog_places AS place
             USING work_items AS item, projects AS project
             WHERE place.work_item_id = item.id
               AND item.project_id = project.id
               AND place.team_id = {scope.TeamId}
               AND place.backlog_level_id = {scope.LevelId}
               AND project.organization_id = {organizationId}
               AND project.status = 'Active'
               AND NOT (COALESCE(item.owning_team_id = {scope.TeamId}, false) AND item.type_id = ANY({typeIds}) AND item.state_id = ANY({stateIds}))
             """,
            cancellationToken);

        // Every item at the place it stands at; an item placed on another backlog moves its one row here. A row already
        // holding its value is not rewritten. Every item written is a member the reorder holds locked, so the place's key
        // onto the item's owner cannot fail; if a race ever reaches it, it is a stale reorder (BR-M4-26), never a 500.
        try
        {
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
        catch (PostgresException exception)
            when (PostgresConflictDetection.IsForeignKeyViolationOnConstraint(exception, PlaceItemKey)
                || PostgresConflictDetection.IsForeignKeyViolationOnConstraint(exception, PlaceOwnerKey))
        {
            throw new ConflictException(BoardErrorCodes.ReorderStale, ErrorParams.None, exception);
        }
    }

    /// <summary>A member of the backlog as the locking statement reads it.</summary>
    private sealed class MemberRow
    {
        public Guid ItemId { get; set; }

        public int Number { get; set; }

        public int? Place { get; set; }

        public DateTimeOffset CreatedAt { get; set; }

        public string ProjectKey { get; set; } = null!;
    }
}
