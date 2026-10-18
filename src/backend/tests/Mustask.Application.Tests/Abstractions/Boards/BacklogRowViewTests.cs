using Mustask.Application.Abstractions.Boards;
using Mustask.Application.Abstractions.WorkItems;

namespace Mustask.Application.Tests.Abstractions.Boards;

/// <summary>
/// The backlog page's view mapping (<see cref="BacklogRowView.Tree"/>), without the database or the endpoint:
/// feat-027's review B-1 and D-9 — building the nested views must not recurse once per tree level, because a stack
/// overflow cannot be caught and ends the whole process, for every organization.
/// </summary>
public sealed class BacklogRowViewTests
{
    private const int StoryRank = 2;

    private static readonly WorkItemTypeView StoryType = new(Guid.CreateVersion7(), "US", "Пользовательская история", "story");

    private static readonly WorkItemStateView NewState =
        new(Guid.CreateVersion7(), "Новая", new WorkItemStateCategoryView("Proposed", "Новые"));

    private static readonly WorkItemProjectView Msk = new(Guid.CreateVersion7(), "Мастаск");

    private static BacklogItemRow Row(int number, Guid id, Guid? parentId) =>
        new(id, $"MSK-US-{number}", number, null, StoryType, $"Элемент {number}", null, NewState, 2, parentId, StoryRank, null, null, "Business", null, Msk, "MSK");

    private static IReadOnlyList<BacklogRowView> Map(IReadOnlyList<BacklogItemRow> rows) =>
        BacklogRowView.Tree(StoryRank, rows, new Dictionary<Guid, WorkItemRefView>());

    // feat-027 D-9 / review B-1 · a chain of 10 000 stories, each beneath the one before — a depth the write side no
    // longer allows (64), but a chain stored before the bound could hold — is mapped without exhausting the stack. The
    // mapping runs on a thread with a 256 KB stack: a recursion of even a few hundred levels (about five frames per
    // level) would overflow it, which ends the test host — the loud failure this guards against. The depth is counted
    // by a loop, and the views are never compared (a record's equality recurses too).
    [Fact]
    public void Tree_AChainOfTenThousandLevels_IsMappedWithoutRecursing()
    {
        const int depth = 10_000;
        var ids = Enumerable.Range(0, depth).Select(_ => Guid.CreateVersion7()).ToArray();
        var rows = Enumerable.Range(0, depth).Select(index => Row(index + 1, ids[index], index == 0 ? null : ids[index - 1])).ToList();
        IReadOnlyList<BacklogRowView>? mapped = null;
        Exception? failure = null;

        var thread = new Thread(
            () =>
            {
                try
                {
                    mapped = Map(rows);
                }
                catch (Exception exception)
                {
                    failure = exception;
                }
            },
            maxStackSize: 256 * 1024);
        thread.Start();
        thread.Join();

        Assert.Null(failure);
        var top = Assert.Single(mapped!);
        Assert.Equal((1, "MSK-US-1"), (top.Order, top.Identifier));

        var levels = 1;
        var last = default(BacklogChildView);

        for (var children = top.Children; children.Count > 0; children = children[0].Children)
        {
            Assert.Single(children);
            last = children[0];
            levels++;
        }

        Assert.Equal(depth, levels);
        Assert.Equal($"MSK-US-{depth}", last!.Identifier);
        Assert.Equal($"MSK-US-{depth - 1}", last.Parent!.Identifier);
    }

    // The iterative mapping keeps what the recursive one gave: the rows in the tree's order at every level, and each
    // nested row's «Родитель» the row it stands beneath.
    [Fact]
    public void Tree_ABranchingTree_KeepsTheOrderOfEachLevelAndTheParentOfEachRow()
    {
        var (root, first, second, firstChild, secondChild, grandchild) = (Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7());
        var rows = new List<BacklogItemRow>
        {
            Row(1, root, null),
            Row(2, first, root),
            Row(3, second, root),
            Row(4, firstChild, first),
            Row(5, secondChild, second),
            Row(6, grandchild, firstChild),
        };

        var tree = Map(rows);

        var top = Assert.Single(tree);
        Assert.Equal(["MSK-US-2", "MSK-US-3"], top.Children.Select(child => child.Identifier));
        Assert.Equal(["MSK-US-4"], top.Children[0].Children.Select(child => child.Identifier));
        Assert.Equal(["MSK-US-5"], top.Children[1].Children.Select(child => child.Identifier));
        Assert.Equal(["MSK-US-6"], top.Children[0].Children[0].Children.Select(child => child.Identifier));
        Assert.Empty(top.Children[0].Children[0].Children[0].Children);
        Assert.Null(top.Parent);
        Assert.Equal("MSK-US-1", top.Children[0].Parent!.Identifier);
        Assert.Equal("MSK-US-1", top.Children[1].Parent!.Identifier);
        Assert.Equal("MSK-US-2", top.Children[0].Children[0].Parent!.Identifier);
        Assert.Equal("MSK-US-3", top.Children[1].Children[0].Parent!.Identifier);
        Assert.Equal("MSK-US-4", top.Children[0].Children[0].Children[0].Parent!.Identifier);
    }

    // feat-039 BR-PB-4: the rows of a project's read carry their creation time, and the page's rows, numbered «Порядок» 1..n,
    // follow the unplaced ones by it before the number; a team's rows carry none and keep the number.
    [Fact]
    public void Tree_RowsWithACreationTime_StandByCreationTimeThenNumber_RowsWithoutKeepTheNumber()
    {
        var (first, second, third) = (Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7());
        var ten = new DateTimeOffset(2026, 10, 10, 10, 0, 0, TimeSpan.Zero);
        var project = new List<BacklogItemRow>
        {
            Row(1, first, null) with { CreatedAt = ten.AddMinutes(2) },
            Row(2, second, null) with { CreatedAt = ten.AddMinutes(1) },
            Row(3, third, null) with { CreatedAt = ten },
        };
        var team = project.Select(row => row with { CreatedAt = null }).ToList();

        Assert.Equal(["MSK-US-3", "MSK-US-2", "MSK-US-1"], Map(project).Select(row => row.Identifier));
        Assert.Equal([1, 2, 3], Map(project).Select(row => row.Order));
        Assert.Equal(["MSK-US-1", "MSK-US-2", "MSK-US-3"], Map(team).Select(row => row.Identifier));
    }

    // feat-040 US-1 / BR-TS-3 · "«Проект» on every row and card": a top-level row, a nested child at any depth, a board card and a
    // backlog item each carry the project of their own item (a team's tree set holds items of several projects), by its current name.
    [Fact]
    public void Tree_AndInOrder_EveryRowChildAndCardCarriesItsOwnProject()
    {
        var (root, child, grandchild) = (Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7());
        var beta = new WorkItemProjectView(Guid.CreateVersion7(), "Бета");
        var rows = new List<BacklogItemRow>
        {
            Row(1, root, null),
            Row(2, child, root) with { Project = beta, ProjectKey = "BET" },
            Row(3, grandchild, child),
        };

        var top = Assert.Single(Map(rows));

        Assert.Equal(Msk, top.Project);
        Assert.Equal(beta, top.Children[0].Project);
        Assert.Equal(Msk, top.Children[0].Children[0].Project);

        var cards = BacklogItemView.InOrder(rows).Select(card => BoardCardView.Of(card, new Dictionary<Guid, IReadOnlyList<ChecklistChildView>>())).ToList();

        // With no creation time the project key (BET before MSK) comes before the number.
        Assert.Equal([beta, Msk, Msk], cards.Select(card => card.Project));
    }

    // feat-040 US-3 / BR-TS-6 · a team's rows give their creation time and project key: the rows of two projects created at the
    // same moment stand by key, then number — whatever order they were read in. The key is never a field of a view.
    [Fact]
    public void Tree_AndInOrder_RowsOfTwoProjects_StandByCreationTimeThenProjectKeyThenNumber()
    {
        var (bet, alp, alpLater) = (Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7());
        var ten = new DateTimeOffset(2026, 10, 10, 10, 0, 0, TimeSpan.Zero);
        var rows = new List<BacklogItemRow>
        {
            Row(3, bet, null) with { CreatedAt = ten, ProjectKey = "BET" },
            Row(7, alp, null) with { CreatedAt = ten, ProjectKey = "ALP" },
            Row(2, alpLater, null) with { CreatedAt = ten.AddMinutes(15), ProjectKey = "ALP" },
        };

        Assert.Equal([alp, bet, alpLater], Map(rows).Select(row => row.Id));
        Assert.Equal([alp, bet, alpLater], BacklogItemView.InOrder(rows).Select(card => card.Id));
    }

    // feat-039 BR-PB-4 on the board: the cards of a project's read stand in the same order, «Порядок» their position.
    [Fact]
    public void InOrder_RowsWithACreationTime_StandByCreationTimeThenNumber()
    {
        var ten = new DateTimeOffset(2026, 10, 10, 10, 0, 0, TimeSpan.Zero);
        var placed = Row(9, Guid.CreateVersion7(), null) with { Place = 1, CreatedAt = ten.AddDays(1) };
        var rows = new List<BacklogItemRow>
        {
            Row(1, Guid.CreateVersion7(), null) with { CreatedAt = ten.AddMinutes(5) },
            Row(2, Guid.CreateVersion7(), null) with { CreatedAt = ten },
            placed,
        };

        var cards = BacklogItemView.InOrder(rows);

        Assert.Equal(["MSK-US-9", "MSK-US-2", "MSK-US-1"], cards.Select(card => card.Identifier));
        Assert.Equal([1, 2, 3], cards.Select(card => card.Order));
    }
}
