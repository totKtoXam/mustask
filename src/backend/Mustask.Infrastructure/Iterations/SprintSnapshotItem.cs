namespace Mustask.Infrastructure.Iterations;

/// <summary>
/// One item of a Closed sprint's close record (BR-14): what the sprint held at the close, except items in «Удалена». Written once,
/// by a set-based statement in the close's transaction, and never changed. <see cref="WorkItemId"/> has <b>no key</b> onto
/// <c>work_items</c>: a purge of the item keeps the row (OQ-15), and the list shows it by its identifier, built from the project,
/// the type and the number recorded here.
/// </summary>
internal sealed class SprintSnapshotItem
{
    public const string Table = "sprint_snapshot_items";

    private SprintSnapshotItem()
    {
        // EF materialization.
    }

    public Guid SprintId { get; private set; }

    public Guid WorkItemId { get; private set; }

    public Guid ProjectId { get; private set; }

    public Guid TypeId { get; private set; }

    public int Number { get; private set; }

    /// <summary>The backlog level of the item's type at the close (mod-006), the figures' level.</summary>
    public Guid LevelId { get; private set; }

    public string? AssigneeId { get; private set; }

    /// <summary>The item's story points then; 0 when none was recorded.</summary>
    public decimal StoryPoints { get; private set; }

    /// <summary>The Completed category at the close.</summary>
    public bool IsDone { get; private set; }
}
