using Mustask.Application.Abstractions.WorkItems;
using Mustask.Domain.Organizations;
using Mustask.Domain.WorkItems;
using Mustask.Infrastructure.Dictionaries;
using Mustask.Infrastructure.Organizations;
using Mustask.Shared.Errors;
using Mustask.Shared.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Mustask.Infrastructure.WorkItems;

/// <summary>
/// <see cref="IWorkItemRepository"/> over <c>work_items</c> and
/// <c>work_item_counters</c>. Every lock is raw SQL taken before the regular
/// tracking load (ADR-0010 "lock by id, load with the regular query"), so the
/// loaded <c>xmin</c> is current and the flush never meets a concurrency
/// conflict: concurrent writers wait on the lock instead (BR-M3-25).
/// </summary>
internal sealed class WorkItemRepository(MustaskDbContext context) : IWorkItemRepository
{
    public async Task LockNumberSequenceAsync(Guid projectId, CancellationToken cancellationToken)
    {
        // A concurrent first creation of the project waits here on the other's uncommitted key, then does nothing.
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO work_item_counters (project_id, last_number) VALUES ({projectId}, 0) ON CONFLICT (project_id) DO NOTHING",
            cancellationToken);

        await context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM work_item_counters WHERE project_id = {projectId} FOR UPDATE",
            cancellationToken);
    }

    public async Task<int> TakeNextNumberAsync(Guid projectId, CancellationToken cancellationToken)
    {
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE work_item_counters SET last_number = last_number + 1 WHERE project_id = {projectId}",
            cancellationToken);

        // The row is locked by this transaction, so this reads the number it has just issued.
        return await context.Database
            .SqlQuery<int>($"SELECT last_number AS \"Value\" FROM work_item_counters WHERE project_id = {projectId}")
            .SingleAsync(cancellationToken);
    }

    public Task LockTeamLinkAsync(Guid organizationId, Guid projectId, Guid teamId, CancellationToken cancellationToken) =>
        // The link is named with the organization of the address, so a team or a project of another organization locks nothing
        // (ADR-0028 § 4): the later read finds the team unknown, exactly as it finds an id that never existed. Only the link row
        // is locked (step 3 of rule 8); a deletion also holds the team row, which a binder of the link does not need.
        context.Database.ExecuteSqlInterpolatedAsync(
            $"""
             SELECT 1
             FROM project_teams
             WHERE organization_id = {organizationId} AND project_id = {projectId} AND team_id = {teamId}
             FOR SHARE
             """,
            cancellationToken);

    public async Task<Guid?> LockProjectLinksAsync(Guid organizationId, Guid projectId, CancellationToken cancellationToken)
    {
        // Every link of the project FOR SHARE, in one statement ordered by team id — the order an unlink (the pair, by team id)
        // and a deletion (the team's links, by project id) take them in, so a creation never waits for a link while holding another
        // out of order (plan-backend D-4, § 7.4). Shares do not wait for each other or for a binder's share.
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"""
             SELECT 1
             FROM project_teams
             WHERE organization_id = {organizationId} AND project_id = {projectId}
             ORDER BY team_id
             FOR SHARE
             """,
            cancellationToken);

        // A new statement under READ COMMITTED: the default as the unlinks and deletions that committed before the lock left it.
        // Under the lock no unlink or deletion of this project's links can commit before this creation does.
        return await DefaultTeams.ResolveAsync(context, projectId, cancellationToken);
    }

    public Task<Guid?> FindCreatedByRequestAsync(Guid projectId, Guid requestId, CancellationToken cancellationToken) =>
        context.WorkItems
            .AsNoTracking()
            .Where(item => item.ProjectId == projectId && item.CreationRequestId == requestId)
            .Select(item => (Guid?)item.Id)
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<WorkItem?> LockAsync(Guid projectId, int number, CancellationToken cancellationToken)
    {
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM work_items WHERE project_id = {projectId} AND number = {number} FOR UPDATE",
            cancellationToken);

        return await context.WorkItems.SingleOrDefaultAsync(item => item.ProjectId == projectId && item.Number == number, cancellationToken);
    }

    public async Task<bool> LockAreaAsync(Guid projectId, Guid areaId, CancellationToken cancellationToken)
    {
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM areas WHERE id = {areaId} AND project_id = {projectId} FOR SHARE",
            cancellationToken);

        // A new statement under READ COMMITTED: an area deleted by the lock's previous holder is gone.
        return await context.Areas.AnyAsync(area => area.Id == areaId && area.ProjectId == projectId, cancellationToken);
    }

    public async Task LockHierarchyAsync(Guid projectId, CancellationToken cancellationToken)
    {
        // A concurrent first parent write of the project waits here on the other's uncommitted key, then does nothing.
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO work_item_hierarchy_locks (project_id) VALUES ({projectId}) ON CONFLICT (project_id) DO NOTHING",
            cancellationToken);

        await context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM work_item_hierarchy_locks WHERE project_id = {projectId} FOR UPDATE",
            cancellationToken);
    }

    public async Task<ParentWrite?> LockParentWriteAsync(WorkItem child, Guid? newParentId, Guid? seenParentId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(child);

        WorkItem? newParent = null;

        if (newParentId is { } parentId)
        {
            newParent = await LockLinkEndAsync(child.ProjectId, parentId, cancellationToken);

            if (newParent is null)
            {
                return null;
            }
        }

        // The stored parent is there: its key is Restrict, and nothing purges an item in this cut.
        var storedParent = child.ParentId is { } storedId
            ? await LockLinkEndAsync(child.ProjectId, storedId, cancellationToken)
                ?? throw new InvalidOperationException($"The stored parent {storedId} of the work item {child.Id} is missing.")
            : null;

        IReadOnlyCollection<HierarchyLink> ancestry = newParent is null ? [] : await AncestryAsync(child.ProjectId, newParent.Id, cancellationToken);
        var subtreeHeight = newParent is null ? 1 : await SubtreeHeightAsync(child.ProjectId, child.Id, cancellationToken);

        return new ParentWrite(child, storedParent, newParent, seenParentId, ancestry, subtreeHeight);
    }

    public Task<WorkItem?> LockNewParentAsync(Guid projectId, Guid parentId, CancellationToken cancellationToken) =>
        LockLinkEndAsync(projectId, parentId, cancellationToken);

    /// <summary>
    /// The item and every ancestor of it, as stored, in one recursive statement
    /// (LINQ cannot express it). <c>UNION</c> rather than <c>UNION ALL</c>: a row
    /// already found is not walked again, so the statement ends even if a defect
    /// ever stored a cycle. Read under the hierarchy lock, so no parent write of
    /// the project moves a link meanwhile. The outer <c>SELECT</c> names its
    /// columns after the properties: the snake_case convention names the table's
    /// columns, not those of a SQL source EF composes over.
    /// </summary>
    public async Task<IReadOnlyCollection<HierarchyLink>> AncestryAsync(Guid projectId, Guid workItemId, CancellationToken cancellationToken) =>
        await context.WorkItems
            .FromSqlInterpolated($"""
                WITH RECURSIVE ancestry (id, project_id, parent_id, type_id, number) AS (
                    SELECT item.id, item.project_id, item.parent_id, item.type_id, item.number
                    FROM work_items AS item
                    WHERE item.id = {workItemId} AND item.project_id = {projectId}
                    UNION
                    SELECT parent.id, parent.project_id, parent.parent_id, parent.type_id, parent.number
                    FROM work_items AS parent
                    JOIN ancestry ON parent.id = ancestry.parent_id
                )
                SELECT id AS "Id", project_id AS "ProjectId", parent_id AS "ParentId", type_id AS "TypeId", number AS "Number"
                FROM ancestry
                """)
            .AsNoTracking()
            .Select(item => new HierarchyLink(item.Id, item.ParentId, item.TypeId, item.Number))
            .ToListAsync(cancellationToken);

    public void Add(WorkItem item) => context.WorkItems.Add(item);

    public async Task SaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (PostgresConflictDetection.IsForeignKeyViolationOnConstraint(exception, WorkItemsConstraints.OwningTeamKey))
        {
            // ADR-0028 § 4, plan-backend D-15: the owner key refused a team that is not there. The locks above make this
            // unreachable; if a race ever reaches it, it answers as the team's absence does, never as a 500.
            throw new NotFoundException(OrganizationErrorCodes.TeamNotFound, ErrorParams.None, exception);
        }
    }

    /// <summary>
    /// A link end of a parent write: the project's item with that id, locked
    /// <c>FOR NO KEY UPDATE</c> — the write changes no key of it — then loaded
    /// tracked (an item already tracked, the child named as its own parent, comes
    /// back as itself). <c>null</c> when the project has no such item.
    /// </summary>
    private async Task<WorkItem?> LockLinkEndAsync(Guid projectId, Guid workItemId, CancellationToken cancellationToken)
    {
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM work_items WHERE id = {workItemId} AND project_id = {projectId} FOR NO KEY UPDATE",
            cancellationToken);

        return await context.WorkItems.SingleOrDefaultAsync(item => item.Id == workItemId && item.ProjectId == projectId, cancellationToken);
    }

    /// <summary>
    /// The height of the item's own subtree, the item counted (1 for an item with no children), in one recursive
    /// statement (LINQ cannot express it) over the stored links — the number of items on the longest chain from the
    /// item down (feat-027 D-9). The recursion stops at <see cref="WorkItemHierarchy.MaxChainLength"/> levels: a height
    /// that large refuses any write whatever the new parent's chain, so the exact figure past it is not needed, and the
    /// statement ends even if a defect ever stored a cycle (<c>UNION ALL</c> is safe, as an item has one parent column
    /// and so is reached by one path). Joined by project and parent, so the
    /// <c>(project_id, parent_id)</c> index serves it. Read under the hierarchy lock, so no parent write of the project
    /// moves a link meanwhile.
    /// </summary>
    private async Task<int> SubtreeHeightAsync(Guid projectId, Guid workItemId, CancellationToken cancellationToken)
    {
        const int cap = WorkItemHierarchy.MaxChainLength;

        return await context.Database
            .SqlQuery<int>($"""
                WITH RECURSIVE subtree (id, depth) AS (
                    SELECT item.id, 1
                    FROM work_items AS item
                    WHERE item.id = {workItemId} AND item.project_id = {projectId}
                    UNION ALL
                    SELECT child.id, subtree.depth + 1
                    FROM work_items AS child
                    JOIN subtree ON child.parent_id = subtree.id AND child.project_id = {projectId}
                    WHERE subtree.depth < {cap}
                )
                SELECT COALESCE(MAX(depth), 1) AS "Value" FROM subtree
                """)
            .SingleAsync(cancellationToken);
    }
}
