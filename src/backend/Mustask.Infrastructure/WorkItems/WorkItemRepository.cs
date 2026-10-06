using Mustask.Application.Abstractions.WorkItems;
using Mustask.Domain.Organizations;
using Mustask.Domain.WorkItems;
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

    public async Task<Guid?> LockTeamDefaultAreaAsync(Guid projectId, Guid teamId, CancellationToken cancellationToken)
    {
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM teams WHERE id = {teamId} AND project_id = {projectId} FOR SHARE",
            cancellationToken);

        // A new statement under READ COMMITTED: a team deleted, or a default moved, by the lock's previous holder reads so.
        return await context.Teams
            .AsNoTracking()
            .Where(team => team.Id == teamId && team.ProjectId == projectId && team.Status == TeamStatus.Active)
            .Select(team => context.Areas.Where(area => area.DefaultOfTeamId == team.Id).Select(area => (Guid?)area.Id).FirstOrDefault())
            .FirstOrDefaultAsync(cancellationToken);
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

        return new ParentWrite(child, storedParent, newParent, seenParentId, ancestry);
    }

    public void Add(WorkItem item) => context.WorkItems.Add(item);

    public Task SaveAsync(CancellationToken cancellationToken) => context.SaveChangesAsync(cancellationToken);

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
    /// The item and every ancestor of it, as stored, in one recursive statement
    /// (LINQ cannot express it). <c>UNION</c> rather than <c>UNION ALL</c>: a row
    /// already found is not walked again, so the statement ends even if a defect
    /// ever stored a cycle. Read under the hierarchy lock, so no parent write of
    /// the project moves a link meanwhile. The outer <c>SELECT</c> names its
    /// columns after the properties: the snake_case convention names the table's
    /// columns, not those of a SQL source EF composes over.
    /// </summary>
    private async Task<IReadOnlyCollection<HierarchyLink>> AncestryAsync(Guid projectId, Guid workItemId, CancellationToken cancellationToken) =>
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
}
