using Mustask.Shared.Exceptions;

namespace Mustask.Domain.Boards;

/// <summary>
/// An item on one backlog (team or project, level), as the order needs it: its
/// id, its number, and its place on this backlog, or <c>null</c> when it holds
/// none here (spec § The stack rank). <paramref name="CreatedAt"/> is given by a
/// project's backlog (feat-039 BR-PB-4): its unplaced items follow by creation time before the number. A team's
/// backlog spans projects (feat-040 BR-TS-6), so its entries give <paramref name="CreatedAt"/> and
/// <paramref name="ProjectKey"/> both: creation time, then the project key by character code, then the number. An
/// entry that gives neither (a task row of a project's page) sorts on its number alone.
/// </summary>
public sealed record StackRankEntry(Guid ItemId, int Number, int? Place, DateTimeOffset? CreatedAt = null, string? ProjectKey = null);

/// <summary>Where a reorder puts its item (BR-M4-25): the top, the bottom, or directly after one named item.</summary>
public abstract record StackRankPosition
{
    private StackRankPosition()
    {
    }

    public static StackRankPosition Top { get; } = new TopPosition();

    public static StackRankPosition Bottom { get; } = new BottomPosition();

    public static StackRankPosition After(Guid itemId) => new AfterPosition(itemId);

    /// <summary>
    /// The position of a request (api-contract § 3.2): <paramref name="kind"/> is
    /// exactly <c>top</c>, <c>bottom</c> or <c>after</c>; with <c>after</c>,
    /// <paramref name="afterItemId"/> is the anchor, already parsed — <c>null</c>
    /// when it is missing or malformed, which no backlog holds. <c>false</c> for
    /// any other kind.
    /// </summary>
    public static bool TryCreate(string? kind, Guid? afterItemId, out StackRankPosition position)
    {
        StackRankPosition? parsed = kind switch
        {
            "top" => Top,
            "bottom" => Bottom,
            "after" => new AfterPosition(afterItemId),
            _ => null,
        };

        position = parsed ?? Top;

        return parsed is not null;
    }

    public sealed record TopPosition : StackRankPosition;

    public sealed record BottomPosition : StackRankPosition;

    /// <summary>Directly after <see cref="ItemId"/>; <c>null</c> names no item at all.</summary>
    public sealed record AfterPosition(Guid? ItemId) : StackRankPosition;
}

/// <summary>
/// The stack rank of one backlog (spec § The stack rank, BR-M4-24, -25, -26):
/// one order over the items the backlog holds. It is computed at read time from
/// the items and their places; a reorder moves one item in it and then every
/// item takes the place it stands at.
/// </summary>
public static class StackRank
{
    /// <summary>
    /// The backlog's order (feat-040 BR-TS-6): the items placed here first, by their places; then, for the unplaced
    /// ones and for placed ones of an equal place (which one reorder never writes, but feat-028's per-project reorders
    /// and a deactivated project's kept places leave), by creation time, then the project key by character code
    /// (ordinal, never culture), then the number as a number. Numbers are one sequence per project, so within one
    /// project the unplaced ones stand in creation order and a new item is last; across projects the key breaks a tie
    /// of the time. An entry without a creation time or a key (a task row of a project's page, which has no project
    /// order) sorts on its number alone.
    /// </summary>
    public static IReadOnlyList<Guid> Order(IEnumerable<StackRankEntry> entries) =>
        entries
            .OrderBy(entry => entry.Place is null)
            .ThenBy(entry => entry.Place)
            .ThenBy(entry => entry.CreatedAt)
            .ThenBy(entry => entry.ProjectKey, StringComparer.Ordinal)
            .ThenBy(entry => entry.Number)
            .Select(entry => entry.ItemId)
            .ToList();

    /// <summary>
    /// <paramref name="order"/> with <paramref name="itemId"/> moved to
    /// <paramref name="position"/>, every other item keeping its relative order.
    /// The result is the order every item is placed at.
    /// </summary>
    /// <exception cref="ConflictException">
    /// <see cref="BoardErrorCodes.ReorderStale"/>: the moved item, or the item it
    /// was to go after, is not in <paramref name="order"/> (BR-M4-26).
    /// </exception>
    public static IReadOnlyList<Guid> Move(IReadOnlyList<Guid> order, Guid itemId, StackRankPosition position)
    {
        if (!order.Contains(itemId))
        {
            throw new ConflictException(BoardErrorCodes.ReorderStale);
        }

        if (position is StackRankPosition.AfterPosition { ItemId: var anchor } && (anchor is not { } id || !order.Contains(id)))
        {
            throw new ConflictException(BoardErrorCodes.ReorderStale);
        }

        // After itself: the item stays where it stands.
        if (position is StackRankPosition.AfterPosition { ItemId: var self } && self == itemId)
        {
            return order.ToList();
        }

        var rest = order.Where(candidate => candidate != itemId).ToList();

        var index = position switch
        {
            StackRankPosition.TopPosition => 0,
            StackRankPosition.BottomPosition => rest.Count,
            StackRankPosition.AfterPosition { ItemId: { } after } => rest.IndexOf(after) + 1,
            _ => throw new ArgumentOutOfRangeException(nameof(position), position, "Unknown position."),
        };

        rest.Insert(index, itemId);

        return rest;
    }
}
