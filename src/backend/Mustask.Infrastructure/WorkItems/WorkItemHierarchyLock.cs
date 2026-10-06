namespace Mustask.Infrastructure.WorkItems;

/// <summary>
/// The queue of a project's parent writes (feat-001, ADR-0011 echelon 3): one
/// row per project, locked <c>FOR UPDATE</c> by every parent write before it
/// reads what it decides on. So two writes for one child, or two that would
/// close a cycle together, never decide on the same snapshot (BR-H-4, spec
/// § NFR). It holds no data — written by <see cref="WorkItemRepository"/> with
/// raw SQL only, so it lives here and not in the domain. A project gets its row
/// with its first parent write.
/// </summary>
internal sealed class WorkItemHierarchyLock
{
    public const string Table = "work_item_hierarchy_locks";

    private WorkItemHierarchyLock()
    {
        // EF materialization.
    }

    public Guid ProjectId { get; private set; }
}
