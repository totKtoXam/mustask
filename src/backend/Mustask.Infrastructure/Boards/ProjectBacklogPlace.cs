namespace Mustask.Infrastructure.Boards;

/// <summary>
/// A work item's place on its project's backlog (feat-039 BR-PB-4, ADR-0028 rule 9). Keyed by the item, so an item
/// holds at most one: an item belongs to one project and one level for good (BR-MX-9, mod-003 BR-M3-1), so its one
/// row stays on one backlog. It is kept apart from the team's <see cref="BacklogPlace"/>: an item can hold both, a
/// team reorder never touches this row and a change of team never loses it. A place is given only by a reorder of
/// its backlog, which renumbers the whole backlog from 1 and drops the places of items that have left it; a place
/// whose item has left and that no reorder has dropped yet is simply not read. Storage only, written with raw SQL
/// by <see cref="ProjectBacklogRepository"/>, read by <see cref="ProjectBacklogMembership"/>.
/// </summary>
internal sealed class ProjectBacklogPlace
{
    public const string Table = "project_backlog_places";

    private ProjectBacklogPlace()
    {
        // EF materialization.
    }

    public Guid WorkItemId { get; private set; }

    public Guid ProjectId { get; private set; }

    public Guid BacklogLevelId { get; private set; }

    /// <summary>1-based; dense over the backlog as its last reorder left it.</summary>
    public int Position { get; private set; }
}
