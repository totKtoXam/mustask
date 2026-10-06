namespace Mustask.Infrastructure.Boards;

/// <summary>
/// One backlog (team, level) that has been reordered at least once: the row a
/// reorder locks <c>FOR UPDATE</c>, so the reorders of one backlog run one after
/// another (BR-M4-25, ADR-0011 echelon 3), and the parent of the backlog's
/// places. Storage only — written by <see cref="BacklogRepository"/> with raw
/// SQL — so it lives here and not in the domain; the order itself is the
/// domain's <see cref="Mustask.Domain.Boards.StackRank"/>. A backlog gets its row
/// with its first reorder.
/// </summary>
internal sealed class BacklogOrder
{
    public const string Table = "backlog_orders";

    private BacklogOrder()
    {
        // EF materialization.
    }

    public Guid TeamId { get; private set; }

    /// <summary>The process level the backlog is of (mod-001's <c>backlog_levels</c>).</summary>
    public Guid BacklogLevelId { get; private set; }
}
