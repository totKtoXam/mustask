using Mustask.Domain.Organizations;
using Mustask.Shared.Exceptions;

namespace Mustask.Domain.Tests.Organizations;

/// <summary>
/// The single definition of pre-order, path, level, height and "self or descendant" for the organization's area forest (mod-018,
/// BR-AW-3, BR-AW-13). The forest used below:
/// <code>
/// Платформа
///   API
///     Шлюз
/// Отчёты
///   Квартальные
/// </code>
/// </summary>
public sealed class AreaTreeTests
{
    private static readonly AreaNode PlatformArea = new(Guid.CreateVersion7(), null, "Платформа");
    private static readonly AreaNode Api = new(Guid.CreateVersion7(), PlatformArea.Id, "API");
    private static readonly AreaNode Gateway = new(Guid.CreateVersion7(), Api.Id, "Шлюз");
    private static readonly AreaNode Reports = new(Guid.CreateVersion7(), null, "Отчёты");
    private static readonly AreaNode Quarterly = new(Guid.CreateVersion7(), Reports.Id, "Квартальные");

    // Siblings in the order given (display order is the reader's), children after their parent in any input order.
    private static AreaTree Tree() => new([Quarterly, Gateway, PlatformArea, Reports, Api]);

    // US-1 · "Every member reads the whole tree": each node before its children, siblings in the order the reader gave.
    [Fact]
    public void PreOrder_PutsEachNodeBeforeItsChildren_SiblingsInTheGivenOrder()
    {
        var order = Tree().PreOrder().Select(node => node.Name);

        Assert.Equal(["Платформа", "API", "Шлюз", "Отчёты", "Квартальные"], order);
    }

    // BR-AW-13: the path is the names from the top level down, joined by " / ".
    [Fact]
    public void PathOf_IsTheNamesFromTheTopLevel()
    {
        var tree = Tree();

        Assert.Equal("Платформа", tree.PathOf(PlatformArea.Id));
        Assert.Equal("Платформа / API / Шлюз", tree.PathOf(Gateway.Id));
        Assert.Equal("Отчёты / Квартальные", tree.PathOf(Quarterly.Id));
    }

    // BR-AW-3: level 1 is the top level.
    [Fact]
    public void LevelOf_IsOneForTheTopLevel_HeightOf_IsZeroForALeaf()
    {
        var tree = Tree();

        Assert.Equal(1, tree.LevelOf(PlatformArea.Id));
        Assert.Equal(3, tree.LevelOf(Gateway.Id));
        Assert.Equal(2, tree.HeightOf(PlatformArea.Id));
        Assert.Equal(0, tree.HeightOf(Gateway.Id));
    }

    // US-2 · "never under itself or a descendant" (BR-AW-3).
    [Fact]
    public void IsSelfOrDescendant_IsTrueForTheNodeAndItsSubtreeOnly()
    {
        var tree = Tree();

        Assert.True(tree.IsSelfOrDescendant(PlatformArea.Id, PlatformArea.Id));
        Assert.True(tree.IsSelfOrDescendant(PlatformArea.Id, Api.Id));
        Assert.True(tree.IsSelfOrDescendant(PlatformArea.Id, Gateway.Id));
        Assert.False(tree.IsSelfOrDescendant(PlatformArea.Id, Reports.Id));
        Assert.False(tree.IsSelfOrDescendant(Api.Id, PlatformArea.Id));
    }

    [Fact]
    public void ChildrenOf_AreTheDirectChildrenInOrder_TopLevelAreTheRoots()
    {
        var tree = Tree();

        Assert.Equal([PlatformArea.Id, Reports.Id], tree.TopLevel().Select(node => node.Id));
        Assert.Equal([Api.Id], tree.ChildrenOf(PlatformArea.Id).Select(node => node.Id));
        Assert.Empty(tree.ChildrenOf(Gateway.Id));
        Assert.True(tree.Contains(Api.Id));
        Assert.False(tree.Contains(Guid.CreateVersion7()));
        Assert.Equal(5, tree.Count);
    }

    // An organization with no area works fully (US-1): the empty forest is a tree.
    [Fact]
    public void Construction_AcceptsTheEmptyForest_RefusesNodesThatAreNotAForest()
    {
        var orphan = new AreaNode(Guid.CreateVersion7(), Guid.CreateVersion7(), "Сирота");
        var a = Guid.CreateVersion7();
        var b = Guid.CreateVersion7();
        var cycleA = new AreaNode(a, b, "А");
        var cycleB = new AreaNode(b, a, "Б");

        Assert.Empty(new AreaTree([]).PreOrder());
        Assert.Throws<ArgumentException>(() => new AreaTree([PlatformArea, orphan]));
        Assert.Throws<ArgumentException>(() => new AreaTree([PlatformArea, PlatformArea]));
        Assert.Throws<ArgumentException>(() => new AreaTree([PlatformArea, cycleA, cycleB]));
    }

    [Fact]
    public void Queries_UnknownId_AreRefused()
    {
        var tree = Tree();
        var unknown = Guid.CreateVersion7();

        Assert.Throws<KeyNotFoundException>(() => tree.PathOf(unknown));
        Assert.Throws<KeyNotFoundException>(() => tree.IsSelfOrDescendant(PlatformArea.Id, unknown));
    }

    // US-1 · "The tree is at most 10 levels deep at creation": a new area (height 0) fits under level 9, not under level 10.
    [Theory]
    [InlineData(9, true)]
    [InlineData(10, false)]
    public void EnsureCanHold_ANewAreaFitsUntilLevelTen(int parentLevel, bool fits)
    {
        var nodes = new List<AreaNode> { new(Guid.CreateVersion7(), null, "Уровень 1") };

        for (var level = 2; level <= parentLevel; level++)
        {
            nodes.Add(new AreaNode(Guid.CreateVersion7(), nodes[^1].Id, $"Уровень {level}"));
        }

        var tree = new AreaTree(nodes);

        if (fits)
        {
            tree.EnsureCanHold(nodes[^1].Id, subtreeHeight: 0);
        }
        else
        {
            Assert.Equal(
                OrganizationErrorCodes.AreaDepthExceeded,
                Assert.Throws<ConflictException>(() => tree.EnsureCanHold(nodes[^1].Id, subtreeHeight: 0)).Code);
        }
    }

    [Fact]
    public void EnsureCanHold_TheTopLevelHoldsATenLevelSubtree_NotAnElevenLevelOne()
    {
        var tree = Tree();

        tree.EnsureCanHold(null, subtreeHeight: 9);

        Assert.Equal(
            OrganizationErrorCodes.AreaDepthExceeded,
            Assert.Throws<ConflictException>(() => tree.EnsureCanHold(null, subtreeHeight: 10)).Code);
    }

    // NFR Volumes: a deep forest is walked without recursion.
    [Fact]
    public void PreOrder_TenThousandLevels_DoesNotOverflow()
    {
        var nodes = new List<AreaNode> { PlatformArea };

        for (var level = 0; level < 10_000; level++)
        {
            nodes.Add(new AreaNode(Guid.CreateVersion7(), nodes[^1].Id, $"Уровень {level}"));
        }

        var tree = new AreaTree(nodes);

        Assert.Equal(nodes.Count, tree.PreOrder().Count);
        Assert.Equal(10_001, tree.LevelOf(nodes[^1].Id));
    }
}
