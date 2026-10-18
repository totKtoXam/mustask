namespace Mustask.Infrastructure.WorkItems;

/// <summary>
/// The queue of a project's parent writes (feat-001, ADR-0011 echelon 3): one
/// row per project, locked <c>FOR UPDATE</c> by every parent write before it
/// reads what it decides on. So two writes for one child, or two that would
/// close a cycle together, never decide on the same snapshot (BR-H-4, spec
/// § NFR). The purge and the team clean-ups (an unlink, a team's deletion) take
/// it too, before the work items they lock, so none of them meets a parent write
/// in a lock-order cycle (mod-005 QD-1). It holds no data — written by
/// <see cref="WorkItemHierarchyLocks"/> with raw SQL only, so it lives here and
/// not in the domain. A project gets its row with the first one of those.
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
