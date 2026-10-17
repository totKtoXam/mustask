using Mustask.Domain.Organizations;

namespace Mustask.Domain.Tests.Organizations;

/// <summary>
/// The single definition of pre-order, path, depth and "self or descendant" (US-5, DC-9). The tree used below:
/// <code>
/// Пример проекта
///   Платформа
///     API
///       Шлюз
///   Отчёты
///     Квартальные
/// </code>
/// No node owns anything and no rule resolves a team through the tree (mod-017 BR-OT-10).
/// </summary>
public sealed class AreaTreeTests
{
    private static readonly AreaNode Root = new(Guid.CreateVersion7(), null, "Пример проекта");
    private static readonly AreaNode PlatformArea = new(Guid.CreateVersion7(), Root.Id, "Платформа");
    private static readonly AreaNode Api = new(Guid.CreateVersion7(), PlatformArea.Id, "API");
    private static readonly AreaNode Gateway = new(Guid.CreateVersion7(), Api.Id, "Шлюз");
    private static readonly AreaNode Reports = new(Guid.CreateVersion7(), Root.Id, "Отчёты");
    private static readonly AreaNode Quarterly = new(Guid.CreateVersion7(), Reports.Id, "Квартальные");

    // Siblings in the order given (display order is the reader's), children after their parent in any input order.
    private static AreaTree Tree() => new([Quarterly, Gateway, Root, PlatformArea, Reports, Api]);

    // US-5 · "The area tree is shown": each node before its children, siblings in the order the reader gave.
    [Fact]
    public void PreOrder_PutsEachNodeBeforeItsChildren_SiblingsInTheGivenOrder()
    {
        var order = Tree().PreOrder().Select(node => node.Name);

        Assert.Equal(["Пример проекта", "Платформа", "API", "Шлюз", "Отчёты", "Квартальные"], order);
    }

    // DC-9: the root's path is its own name; any other path is the names below the root.
    [Fact]
    public void PathOf_IsTheNamesBelowTheRoot()
    {
        var tree = Tree();

        Assert.Equal("Пример проекта", tree.PathOf(Root.Id));
        Assert.Equal("Платформа", tree.PathOf(PlatformArea.Id));
        Assert.Equal("Платформа / API / Шлюз", tree.PathOf(Gateway.Id));
        Assert.Equal("Отчёты / Квартальные", tree.PathOf(Quarterly.Id));
    }

    [Fact]
    public void DepthOf_IsZeroForTheRoot()
    {
        var tree = Tree();

        Assert.Equal(0, tree.DepthOf(Root.Id));
        Assert.Equal(1, tree.DepthOf(Reports.Id));
        Assert.Equal(3, tree.DepthOf(Gateway.Id));
    }

    // US-5 · "A node is never moved under itself or its descendant" (BR-M1-12).
    [Fact]
    public void IsSelfOrDescendant_IsTrueForTheNodeAndItsSubtreeOnly()
    {
        var tree = Tree();

        Assert.True(tree.IsSelfOrDescendant(PlatformArea.Id, PlatformArea.Id));
        Assert.True(tree.IsSelfOrDescendant(PlatformArea.Id, Api.Id));
        Assert.True(tree.IsSelfOrDescendant(PlatformArea.Id, Gateway.Id));
        Assert.False(tree.IsSelfOrDescendant(PlatformArea.Id, Root.Id));
        Assert.False(tree.IsSelfOrDescendant(PlatformArea.Id, Reports.Id));
        Assert.False(tree.IsSelfOrDescendant(Api.Id, PlatformArea.Id));
    }

    [Fact]
    public void ChildrenOf_AreTheDirectChildrenInOrder()
    {
        var tree = Tree();

        Assert.Equal([PlatformArea.Id, Reports.Id], tree.ChildrenOf(Root.Id).Select(node => node.Id));
        Assert.Empty(tree.ChildrenOf(Gateway.Id));
        Assert.True(tree.Contains(Api.Id));
        Assert.False(tree.Contains(Guid.CreateVersion7()));
        Assert.Equal(6, tree.Count);
        Assert.Equal(Root, tree.Root);
    }

    [Fact]
    public void Construction_RefusesNodesThatAreNotOneTree()
    {
        var orphan = new AreaNode(Guid.CreateVersion7(), Guid.CreateVersion7(), "Сирота");
        var secondRoot = new AreaNode(Guid.CreateVersion7(), null, "Второй");
        var a = Guid.CreateVersion7();
        var b = Guid.CreateVersion7();
        var cycleA = new AreaNode(a, b, "А");
        var cycleB = new AreaNode(b, a, "Б");

        Assert.Throws<ArgumentException>(() => new AreaTree([]));
        Assert.Throws<ArgumentException>(() => new AreaTree([Root, orphan]));
        Assert.Throws<ArgumentException>(() => new AreaTree([Root, secondRoot]));
        Assert.Throws<ArgumentException>(() => new AreaTree([Root, Root]));
        Assert.Throws<ArgumentException>(() => new AreaTree([Root, cycleA, cycleB]));
    }

    [Fact]
    public void Queries_UnknownId_AreRefused()
    {
        var tree = Tree();
        var unknown = Guid.CreateVersion7();

        Assert.Throws<KeyNotFoundException>(() => tree.PathOf(unknown));
        Assert.Throws<KeyNotFoundException>(() => tree.IsSelfOrDescendant(Root.Id, unknown));
    }

    // NFR Volumes: a deep tree is walked without recursion.
    [Fact]
    public void PreOrder_TenThousandLevels_DoesNotOverflow()
    {
        var nodes = new List<AreaNode> { Root };

        for (var level = 0; level < 10_000; level++)
        {
            nodes.Add(new AreaNode(Guid.CreateVersion7(), nodes[^1].Id, $"Уровень {level}"));
        }

        var tree = new AreaTree(nodes);

        Assert.Equal(nodes.Count, tree.PreOrder().Count);
        Assert.Equal(10_000, tree.DepthOf(nodes[^1].Id));
    }
}
