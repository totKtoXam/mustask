using Mustask.Application.Abstractions.Boards;
using Mustask.Domain.Boards;
using Mustask.Infrastructure.Dictionaries;
using Mustask.Infrastructure.Organizations;
using Mustask.Shared.Errors;
using Mustask.Shared.Exceptions;
using Microsoft.EntityFrameworkCore;
using Npgsql;

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
    /// <summary>The place's key onto its item: a member that left between the read and the write fails it (the locks make that unreachable).</summary>
    private const string PlaceItemKey = "fk_project_backlog_places_work_items_work_item_id";

    public async Task<bool> LockAsync(Guid organizationId, ProjectBacklogScope scope, CancellationToken cancellationToken)
    {
        // The reorder scope carries no team filter (the project backlog's reorder is of the whole backlog): the locking
        // statements below state the membership without one, and a test pins them to ProjectBacklogMembership.
        if (scope.Filter.TeamsSet)
        {
            throw new InvalidOperationException("A project backlog's reorder is of the whole backlog; its scope carries no filter.");
        }

        var (projectId, levelId) = (scope.ProjectId, scope.LevelId);
        var typeIds = scope.TypeIds.ToArray();
        var stateIds = scope.ShownStateIds.ToArray();

        await context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM projects WHERE id = {projectId} AND organization_id = {organizationId} FOR SHARE",
            cancellationToken);

        // A new statement under READ COMMITTED: a project deactivated by the lock's previous holder reads deactivated.
        if (!await context.ReachableProjects(organizationId).AnyAsync(project => project.Id == projectId, cancellationToken))
        {
            return false;
        }

        // mod-005 (ADR-0030 rule 6, ADR-0028 rule 8 step 5): the members, in id order — ORDER BY is applied before the locks are
        // taken, one fixed order for every caller — FOR KEY SHARE: a Delete or a save of a member (FOR UPDATE) waits for this
        // reorder or is read as a non-member by the locking read below, and a purge needs a prior Delete.
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"""
             SELECT 1
             FROM work_items
             WHERE project_id = {projectId} AND type_id = ANY({typeIds}) AND state_id = ANY({stateIds})
             ORDER BY id
             FOR KEY SHARE
             """,
            cancellationToken);

        // A concurrent first reorder of the backlog waits here on the other's uncommitted key, then does nothing.
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO project_backlog_orders (project_id, backlog_level_id) VALUES ({projectId}, {levelId}) ON CONFLICT (project_id, backlog_level_id) DO NOTHING",
            cancellationToken);

        await context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM project_backlog_orders WHERE project_id = {projectId} AND backlog_level_id = {levelId} FOR UPDATE",
            cancellationToken);

        return true;
    }

    public async Task<IReadOnlyList<StackRankEntry>> ReadEntriesAsync(ProjectBacklogScope scope, CancellationToken cancellationToken)
    {
        var (projectId, levelId) = (scope.ProjectId, scope.LevelId);
        var typeIds = scope.TypeIds.ToArray();
        var stateIds = scope.ShownStateIds.ToArray();

        // The membership of ProjectBacklogMembership (the project's items of the level's types in a shown state) as a
        // locking statement: raw SQL, parameterized, for row locking (backend.md "Raw SQL"). A member read here stays a member
        // until the commit; one that was deleted before this read is not read. A test pins it to the membership.
        var rows = await context.Database
            .SqlQuery<MemberRow>(
                $"""
                 SELECT item.id AS "ItemId",
                        item.number AS "Number",
                        CASE WHEN place.project_id = {projectId} AND place.backlog_level_id = {levelId} THEN place.position END AS "Place",
                        item.created_at AS "CreatedAt"
                 FROM work_items AS item
                 LEFT JOIN project_backlog_places AS place ON place.work_item_id = item.id
                 WHERE item.project_id = {projectId}
                   AND item.type_id = ANY({typeIds})
                   AND item.state_id = ANY({stateIds})
                 ORDER BY item.id
                 FOR KEY SHARE OF item
                 """)
            .ToListAsync(cancellationToken);

        return rows.Select(row => new StackRankEntry(row.ItemId, row.Number, row.Place, row.CreatedAt)).ToList();
    }

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
        try
        {
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
        catch (PostgresException exception) when (PostgresConflictDetection.IsForeignKeyViolationOnConstraint(exception, PlaceItemKey))
        {
            // mod-005 (ADR-0030 rule 6): the backstop to the member lock — an item that left the work_items table between the
            // locking read and this write fails the place's key. The locks make this unreachable; if a race ever reaches it,
            // it answers as a stale reorder does (BR-M4-26), never as a 500.
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
    }
}
