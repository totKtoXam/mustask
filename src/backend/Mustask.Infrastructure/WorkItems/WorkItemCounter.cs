namespace Mustask.Infrastructure.WorkItems;

/// <summary>
/// The per-project number sequence of BR-M3-5, as a row: the last number the
/// project issued, shared by all five types. It is storage only — written by
/// <see cref="WorkItemRepository"/> with raw SQL under a row lock, never through
/// change tracking — so it lives here and not in the domain. A project gets its
/// row with its first creation.
/// </summary>
internal sealed class WorkItemCounter
{
    public const string Table = "work_item_counters";

    private WorkItemCounter()
    {
        // EF materialization.
    }

    public Guid ProjectId { get; private set; }

    public int LastNumber { get; private set; }
}
