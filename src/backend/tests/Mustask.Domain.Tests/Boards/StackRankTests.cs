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

    // feat-039 US-2 · "New and unplaced items follow the placed ones" (BR-PB-4, BR-MX-18): on a project's backlog the entries give
    // their creation time; the placed ones lead by place whatever their time, the unplaced follow by creation time, and the
    // number breaks a tie — S6 (10:00), S7 and S8 (both 10:05, S7 the lower number), S9 numbered last but created earliest.
    [Fact]
    public void Order_ProjectEntries_PlacedThenUnplacedByCreationTimeThenNumber()
    {
        var (s6, s7, s8, s9, placed) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var ten = new DateTimeOffset(2026, 10, 10, 10, 0, 0, TimeSpan.Zero);

        var order = StackRank.Order(
        [
            new(s8, 8, null, ten.AddMinutes(5)),
            new(s7, 7, null, ten.AddMinutes(5)),
            new(s9, 9, null, ten.AddHours(-3)),
            new(s6, 6, null, ten),
            new(placed, 20, 1, ten.AddDays(1)),
        ]);

        Assert.Equal([placed, s9, s6, s7, s8], order);
    }

    // feat-039 BR-PB-4: entries that give no creation time — a team's, and a project's tasks — keep the number tie-break exactly as
    // before, so a team's order is not changed by the project's.
    [Fact]
    public void Order_EntriesWithoutACreationTime_StayByNumber()
    {
        var order = StackRank.Order([new(Us9, 9, null), new(Us3, 3, null, null), new(Us11, 11, 1)]);

        Assert.Equal(["MSK-US-11", "MSK-US-3", "MSK-US-9"], Read(order));
    }

    // feat-039 BR-PB-4: a reorder on a project's backlog moves one item in the order the creation time gave, and the result is the
    // order every item is placed at — an item created earlier but numbered later keeps its place relative to the others.
    [Fact]
    public void Move_OnProjectEntries_MovesInTheCreationTimeOrder()
    {
        var (early, late, middle) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var ten = new DateTimeOffset(2026, 10, 10, 10, 0, 0, TimeSpan.Zero);
        var order = StackRank.Order([new(late, 1, null, ten.AddHours(1)), new(early, 3, null, ten), new(middle, 2, null, ten.AddMinutes(30))]);

        Assert.Equal([early, middle, late], order);
        Assert.Equal([middle, early, late], StackRank.Move(order, middle, StackRankPosition.Top));
    }

    // feat-040 US-3 · "Unplaced items of several projects follow by creation time, then project key, then number" (BR-TS-6): the
    // spec's example — BET-US-3 and ALP-US-7 both created at 10:00 stand ALP before BET by key; ALP-US-2 at 10:15 follows both;
    // a placed item leads whatever its time.
    [Fact]
    public void Order_TeamEntries_PlacedFirst_ThenCreationTime_ThenProjectKey_ThenNumber()
    {
        var (alp2, alp7, bet3, placed) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var ten = new DateTimeOffset(2026, 10, 10, 10, 0, 0, TimeSpan.Zero);

        var order = StackRank.Order(
        [
            new(alp2, 2, null, ten.AddMinutes(15), "ALP"),
            new(bet3, 3, null, ten, "BET"),
            new(alp7, 7, null, ten, "ALP"),
            new(placed, 99, 1, ten.AddDays(1), "BET"),
        ]);

        Assert.Equal([placed, alp7, bet3, alp2], order);
    }

    // feat-040 US-3 · BR-TS-6: placed items of an equal position (feat-028's per-project reorders left them) sort by the same
    // rule, so two projects' "position 1" items stand by time, key, number — and the first reorder writes a dense 1..n.
    [Fact]
    public void Order_TeamEntries_EqualPositions_SortByTheTieBreak()
    {
        var (alp, bet, betLater) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var ten = new DateTimeOffset(2026, 10, 10, 10, 0, 0, TimeSpan.Zero);

        var order = StackRank.Order(
        [
            new(betLater, 1, 1, ten.AddMinutes(1), "ALP"),
            new(bet, 5, 1, ten, "BET"),
            new(alp, 9, 1, ten, "ALP"),
        ]);

        Assert.Equal([alp, bet, betLater], order);
    }

    // feat-040 BR-TS-6 · "project key by character code": ordinal, never culture — «ALP» < «BET» < «alp», and «BET» < «bet».
    [Fact]
    public void Order_TeamEntries_ProjectKeyIsByCharacterCode()
    {
        var (upperA, upperB, lowerA) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var ten = new DateTimeOffset(2026, 10, 10, 10, 0, 0, TimeSpan.Zero);

        var order = StackRank.Order([new(lowerA, 1, null, ten, "alp"), new(upperB, 1, null, ten, "BET"), new(upperA, 1, null, ten, "ALP")]);

        Assert.Equal([upperA, upperB, lowerA], order);
    }

    // feat-040 BR-TS-6 · "identifier number as a number": 10 follows 9, not 1.
    [Fact]
    public void Order_TeamEntries_NumberIsANumber()
    {
        var (nine, ten, two) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var time = new DateTimeOffset(2026, 10, 10, 10, 0, 0, TimeSpan.Zero);

        var order = StackRank.Order([new(ten, 10, null, time, "ALP"), new(nine, 9, null, time, "ALP"), new(two, 2, null, time, "ALP")]);

        Assert.Equal([two, nine, ten], order);
    }

    // feat-040 US-3 · a reorder moves one item in the cross-project order and the result is the order every item is placed at.
    [Fact]
    public void Move_OnTeamEntries_MovesInTheCrossProjectOrder()
    {
        var (alp, bet, gam) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var ten = new DateTimeOffset(2026, 10, 10, 10, 0, 0, TimeSpan.Zero);
        var order = StackRank.Order([new(gam, 1, null, ten, "GAM"), new(bet, 1, null, ten, "BET"), new(alp, 1, null, ten, "ALP")]);

        Assert.Equal([alp, bet, gam], order);
        Assert.Equal([gam, alp, bet], StackRank.Move(order, gam, StackRankPosition.Top));
        Assert.Equal([alp, gam, bet], StackRank.Move(order, gam, StackRankPosition.After(alp)));
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
