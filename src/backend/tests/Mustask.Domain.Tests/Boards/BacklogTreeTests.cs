using Mustask.Domain.Boards;

namespace Mustask.Domain.Tests.Boards;

/// <summary>
/// The backlog's tree (feat-001 BR-H-9..-11), without the database: the
/// backlog's own items are the top-level rows in its stack rank, and beneath a
/// row are the items of the level directly below whose parent it is, in their
/// own backlog's stack rank. US-4's data: «Эпики» MSK-EPIC-1, MSK-EPIC-20;
/// «Фичи» MSK-FEAT-8 (placed first), MSK-FEAT-6 (placed second), MSK-FEAT-23
/// (unplaced); «Истории» MSK-US-2, MSK-BUG-4, MSK-US-22.
/// </summary>
public sealed class BacklogTreeTests
{
    private static readonly Guid Epic1 = Guid.CreateVersion7();
    private static readonly Guid Epic20 = Guid.CreateVersion7();
    private static readonly Guid Feature6 = Guid.CreateVersion7();
    private static readonly Guid Feature8 = Guid.CreateVersion7();
    private static readonly Guid Feature23 = Guid.CreateVersion7();
    private static readonly Guid Story2 = Guid.CreateVersion7();
    private static readonly Guid Bug4 = Guid.CreateVersion7();
    private static readonly Guid Story22 = Guid.CreateVersion7();
    private static readonly Guid Elsewhere = Guid.CreateVersion7();

    /// <summary>US-4's links: MSK-EPIC-1 holds MSK-FEAT-6 and MSK-FEAT-8, MSK-FEAT-6 MSK-US-2 and MSK-BUG-4, MSK-FEAT-8 MSK-FEAT-23, MSK-EPIC-1 MSK-US-22.</summary>
    private static readonly BacklogTreeEntry[] Epics = [new(Epic1, 1, null, null), new(Epic20, 20, null, null)];

    private static readonly BacklogTreeEntry[] Features = [new(Feature6, 6, 2, Epic1), new(Feature8, 8, 1, Epic1), new(Feature23, 23, null, Feature8)];

    private static readonly BacklogTreeEntry[] Stories = [new(Bug4, 4, null, Feature6), new(Story2, 2, null, Feature6), new(Story22, 22, null, Epic1)];

    private static string Shape(IReadOnlyList<BacklogTreeNode> rows) =>
        string.Join(' ', rows.Select(row => row.Children.Count == 0 ? Name(row.ItemId) : $"{Name(row.ItemId)}[{Shape(row.Children)}]"));

    private static string Name(Guid id) =>
        new Dictionary<Guid, string>
        {
            [Epic1] = "E1", [Epic20] = "E20", [Feature6] = "F6", [Feature8] = "F8", [Feature23] = "F23",
            [Story2] = "US2", [Bug4] = "BUG4", [Story22] = "US22", [Elsewhere] = "X",
        }[id];

    // US-4 · "Expanding shows the children of the level below in their order" and "A nested row expands one tier
    // further": MSK-FEAT-8 before MSK-FEAT-6, as «Фичи» orders them; the stories beneath MSK-FEAT-6 by number.
    [Fact]
    public void Nest_ThreeLevels_ChildrenInTheirOwnBacklogsOrder()
    {
        Assert.Equal("E1[F8 F6[US2 BUG4]] E20", Shape(BacklogTree.Nest([Epics, Features, Stories])));
    }

    // US-4 · "A link that does not nest leaves the child a top-level row elsewhere": MSK-FEAT-23 (its parent on its own
    // level) and MSK-US-22 (its parent two levels up) are beneath no row; each is a row of its own level's backlog.
    [Fact]
    public void Nest_AParentNotOnTheLevelDirectlyAbove_NestsUnderNoRow()
    {
        var epics = Shape(BacklogTree.Nest([Epics, Features, Stories]));
        var features = Shape(BacklogTree.Nest([Features, Stories]));

        Assert.DoesNotContain("F23", epics);
        Assert.DoesNotContain("US22", epics);
        Assert.Equal("F8 F6[US2 BUG4] F23", features);
    }

    // BR-H-10: a parent that is no entry of the level above — another team's, or one in «Удалена», which no backlog
    // holds — nests nothing.
    [Fact]
    public void Nest_AParentOutsideTheLevelAbove_NestsNothing()
    {
        BacklogTreeEntry[] features = [new(Feature6, 6, null, Elsewhere), new(Feature8, 8, null, null)];

        Assert.Equal("E1 E20", Shape(BacklogTree.Nest([Epics, features])));
    }

    // US-4 · "«Истории» rows do not expand in this cut": one level, no children.
    [Fact]
    public void Nest_OneLevel_RowsOnlyInTheStackRank()
    {
        Assert.Equal("US2 BUG4 US22", Shape(BacklogTree.Nest([Stories])));
        Assert.Empty(BacklogTree.Nest([]));
        Assert.Empty(BacklogTree.Nest([[], Features]));
    }
}
