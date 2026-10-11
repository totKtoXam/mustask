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

    private static BacklogItemRow Row(int number, Guid id, Guid? parentId) =>
        new(id, $"MSK-US-{number}", number, null, StoryType, $"Элемент {number}", null, NewState, 2, parentId, StoryRank, null, null, "Business");

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
}
