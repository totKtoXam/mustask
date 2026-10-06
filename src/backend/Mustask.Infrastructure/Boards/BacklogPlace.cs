namespace Mustask.Infrastructure.Boards;

/// <summary>
/// A work item's place on one backlog (spec § The stack rank, BR-M4-24). Keyed
/// by the item, so an item holds at most one place: placing it on another
/// backlog moves this one row there, and the old place is gone. A place is given
/// only by a reorder of its backlog, which renumbers the whole backlog from 1
/// and drops the places of items that have left it; a place whose item has left
/// and that no reorder has dropped yet is simply not read (the item is not a
/// member of that backlog). Storage only, written with raw SQL by
/// <see cref="BacklogRepository"/>, read by <see cref="BacklogMembership"/>.
/// </summary>
internal sealed class BacklogPlace
{
    public const string Table = "backlog_places";

    private BacklogPlace()
    {
        // EF materialization.
    }

    public Guid WorkItemId { get; private set; }

    public Guid TeamId { get; private set; }

    public Guid BacklogLevelId { get; private set; }

    /// <summary>1-based; dense over the backlog as its last reorder left it.</summary>
    public int Position { get; private set; }
}
