using Mustask.Domain.Boards;
using Mustask.Shared.Exceptions;

namespace Mustask.Domain.Tests.Boards;

/// <summary>The stack rank of one backlog (spec § The stack rank; BR-M4-13, -24, -25, -26).</summary>
public sealed class StackRankTests
{
    private static readonly Guid Us3 = Guid.NewGuid();
    private static readonly Guid Bug5 = Guid.NewGuid();
    private static readonly Guid Us9 = Guid.NewGuid();
    private static readonly Guid Us11 = Guid.NewGuid();

    private static readonly Dictionary<Guid, string> Names = new()
    {
        [Us3] = "MSK-US-3",
        [Bug5] = "MSK-BUG-5",
        [Us9] = "MSK-US-9",
        [Us11] = "MSK-US-11",
    };

    /// <summary>US-2's backlog: MSK-US-3, MSK-BUG-5, MSK-US-9, MSK-US-11, never reordered.</summary>
    private static IReadOnlyList<Guid> Backlog() =>
        StackRank.Order([new(Us9, 9, null), new(Us11, 11, null), new(Bug5, 5, null), new(Us3, 3, null)]);

    private static string[] Read(IEnumerable<Guid> order) => order.Select(id => Names[id]).ToArray();

    // US-1 · "Items nobody has ordered yet are listed by number" (BR-M4-13): unplaced items by number, ascending.
    [Fact]
    public void Order_NothingPlaced_ByNumber()
    {
        var order = StackRank.Order([new(Us9, 9, null), new(Bug5, 5, null), new(Us3, 3, null)]);

        Assert.Equal(["MSK-US-3", "MSK-BUG-5", "MSK-US-9"], Read(order));
    }

    // BR-M4-24, OQ-5: the placed items by their places, then the unplaced ones below every placed item, by number.
    [Fact]
    public void Order_PlacedThenUnplaced()
    {
        var order = StackRank.Order([new(Us3, 3, null), new(Us11, 11, 2), new(Bug5, 5, null), new(Us9, 9, 1)]);

        Assert.Equal(["MSK-US-9", "MSK-US-11", "MSK-US-3", "MSK-BUG-5"], Read(order));
    }

    // Places with gaps (an item that left keeps no row in the read) order the same as dense ones.
    [Fact]
    public void Order_PlacesWithGaps_KeepTheirRelativeOrder()
    {
        var order = StackRank.Order([new(Us3, 3, 7), new(Us9, 9, 2)]);

        Assert.Equal(["MSK-US-9", "MSK-US-3"], Read(order));
    }

    // US-2 · "The handle's menu moves a row by keyboard": «В начало» and «Выше» on the second row are the top, «Ниже» is
    // directly after the row below, «В конец» the bottom.
    [Theory]
    [InlineData("top", null, "MSK-BUG-5,MSK-US-3,MSK-US-9,MSK-US-11")]
    [InlineData("after", "MSK-US-9", "MSK-US-3,MSK-US-9,MSK-BUG-5,MSK-US-11")]
    [InlineData("bottom", null, "MSK-US-3,MSK-US-9,MSK-US-11,MSK-BUG-5")]
    public void Move_SecondRow_ToEachPosition(string kind, string? after, string expected)
    {
        var anchor = after is null ? (Guid?)null : Names.Single(pair => pair.Value == after).Key;
        Assert.True(StackRankPosition.TryCreate(kind, anchor, out var position));

        var moved = StackRank.Move(Backlog(), Bug5, position);

        Assert.Equal(expected.Split(','), Read(moved));
    }

    // US-2 · "A drag between two rows persists": MSK-US-11 dropped between MSK-US-3 and MSK-BUG-5 is "after MSK-US-3".
    [Fact]
    public void Move_AfterAnItem_GoesDirectlyBelowIt()
    {
        var moved = StackRank.Move(Backlog(), Us11, StackRankPosition.After(Us3));

        Assert.Equal(["MSK-US-3", "MSK-US-11", "MSK-BUG-5", "MSK-US-9"], Read(moved));
    }

    // US-2 · "Two users reorder different items, both moves count": each applies to the order the previous one left.
    [Fact]
    public void Move_TwoDifferentItemsInTurn_BothCount()
    {
        var afterAnna = StackRank.Move(Backlog(), Us11, StackRankPosition.Top);
        var afterBoris = StackRank.Move(afterAnna, Us3, StackRankPosition.After(Us9));

        Assert.Equal(["MSK-US-11", "MSK-BUG-5", "MSK-US-9", "MSK-US-3"], Read(afterBoris));
    }

    // US-2 · "Two users move the same item, the later decides".
    [Fact]
    public void Move_SameItemTwice_TheLaterDecides()
    {
        var afterAnna = StackRank.Move(Backlog(), Us9, StackRankPosition.Top);
        var afterBoris = StackRank.Move(afterAnna, Us9, StackRankPosition.Bottom);

        Assert.Equal(["MSK-US-3", "MSK-BUG-5", "MSK-US-11", "MSK-US-9"], Read(afterBoris));
    }

    // US-2 · "A drop next to an item that has left is refused" (BR-M4-26), and the same for the moved item itself; an
    // anchor that names nothing at all is not on the backlog either.
    [Fact]
    public void Move_ItemOrAnchorNotOnTheBacklog_IsStale()
    {
        var gone = Guid.NewGuid();

        var anchorGone = Assert.Throws<ConflictException>(() => StackRank.Move(Backlog(), Us11, StackRankPosition.After(gone)));
        var itemGone = Assert.Throws<ConflictException>(() => StackRank.Move(Backlog(), gone, StackRankPosition.Top));
        Assert.True(StackRankPosition.TryCreate("after", null, out var noAnchor));
        var noAnchorAtAll = Assert.Throws<ConflictException>(() => StackRank.Move(Backlog(), Us11, noAnchor));

        Assert.All([anchorGone, itemGone, noAnchorAtAll], refusal => Assert.Equal(BoardErrorCodes.ReorderStale, refusal.Code));
    }

    // api-contract § 3.2 step 4: directly after itself, the item stays where it stands.
    [Fact]
    public void Move_AfterItself_LeavesTheOrder()
    {
        Assert.Equal(Read(Backlog()), Read(StackRank.Move(Backlog(), Bug5, StackRankPosition.After(Bug5))));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Top")]
    [InlineData("before")]
    public void TryCreate_UnknownKind_IsInvalid(string? kind)
    {
        Assert.False(StackRankPosition.TryCreate(kind, Guid.NewGuid(), out _));
    }
}
