using Mustask.Shared.Exceptions;

namespace Mustask.Domain.Organizations;

/// <summary>One node as the tree sees it: its place, name, type and (below and at a project's node) its project. A node owns nothing (BR-AW-2).</summary>
public sealed record AreaNode(Guid Id, Guid? ParentId, string Name, AreaType Type = AreaType.Area, Guid? ProjectId = null);

/// <summary>
/// A pure view of one organization's area tree — a forest: every area with no parent is a top-level area (BR-AW-3). It is the single
/// definition of an area's path (BR-AW-13), its level, its height, and of "the area or one of its descendants": the tree read, the
/// writes' cycle and depth checks and the reads for other modules all use it (ADR-0028 rule 5).
/// </summary>
public sealed class AreaTree
{
    /// <summary>What joins the names of a path.</summary>
    public const string PathSeparator = " / ";

    /// <summary>The tree is at most this many levels deep, at creation and after a move (BR-AW-3).</summary>
    public const int MaxDepth = 10;

    private readonly Dictionary<Guid, AreaNode> _byId;
    private readonly Dictionary<Guid, List<AreaNode>> _children;
    private readonly List<AreaNode> _roots;
    private readonly List<AreaNode> _preOrder;

    /// <summary>
    /// Builds the tree. Siblings keep the order <paramref name="nodes"/> gives them, so a caller that reads the nodes in display order
    /// gets the display order back from <see cref="PreOrder"/>. Every parent is a node of the tree and every node is reachable from a
    /// top-level area.
    /// </summary>
    public AreaTree(IEnumerable<AreaNode> nodes)
    {
        ArgumentNullException.ThrowIfNull(nodes);

        var list = nodes.ToList();
        _byId = [];
        _children = [];
        _roots = [];

        foreach (var node in list)
        {
            if (!_byId.TryAdd(node.Id, node))
            {
                throw new ArgumentException($"The area {node.Id} appears twice.", nameof(nodes));
            }
        }

        foreach (var node in list)
        {
            if (node.ParentId is not { } parentId)
            {
                _roots.Add(node);
                continue;
            }

            if (!_byId.ContainsKey(parentId))
            {
                throw new ArgumentException($"The parent of the area {node.Id} is not in the tree.", nameof(nodes));
            }

            if (!_children.TryGetValue(parentId, out var siblings))
            {
                siblings = [];
                _children[parentId] = siblings;
            }

            siblings.Add(node);
        }

        _preOrder = BuildPreOrder();

        if (_preOrder.Count != _byId.Count)
        {
            throw new ArgumentException("Every area is reachable from the top level: the nodes hold a cycle.", nameof(nodes));
        }
    }

    public int Count => _byId.Count;

    public bool Contains(Guid id) => _byId.ContainsKey(id);

    public AreaNode this[Guid id] => Node(id);

    /// <summary>Every node, each before its children, siblings in the order the tree was built with.</summary>
    public IReadOnlyList<AreaNode> PreOrder() => _preOrder;

    /// <summary>The top-level areas, in the order the tree was built with.</summary>
    public IReadOnlyList<AreaNode> TopLevel() => _roots;

    /// <summary>The direct children of <paramref name="id"/>, in the order the tree was built with.</summary>
    public IReadOnlyList<AreaNode> ChildrenOf(Guid id)
    {
        Node(id);

        return _children.TryGetValue(id, out var children) ? children : [];
    }

    /// <summary>The names from the top level down to the area, joined by <see cref="PathSeparator"/> (BR-AW-13).</summary>
    public string PathOf(Guid id)
    {
        var names = new List<string>();

        for (AreaNode? current = Node(id); current is not null; current = current.ParentId is { } parentId ? _byId[parentId] : null)
        {
            names.Add(current.Name);
        }

        names.Reverse();

        return string.Join(PathSeparator, names);
    }

    /// <summary>1 for a top-level area (the spec's levels).</summary>
    public int LevelOf(Guid id)
    {
        var level = 0;

        for (AreaNode? current = Node(id); current is not null; current = current.ParentId is { } parentId ? _byId[parentId] : null)
        {
            level++;
        }

        return level;
    }

    /// <summary>The number of levels below the area: 0 for a leaf.</summary>
    public int HeightOf(Guid id)
    {
        var height = 0;
        var frontier = new List<AreaNode> { Node(id) };

        while (true)
        {
            var next = frontier.SelectMany(node => _children.TryGetValue(node.Id, out var children) ? children : []).ToList();

            if (next.Count == 0)
            {
                return height;
            }

            height++;
            frontier = next;
        }
    }

    /// <summary>Whether <paramref name="candidateId"/> is <paramref name="nodeId"/> itself or one of its descendants.</summary>
    public bool IsSelfOrDescendant(Guid nodeId, Guid candidateId)
    {
        Node(nodeId);

        for (AreaNode? current = Node(candidateId); current is not null; current = current.ParentId is { } parentId ? _byId[parentId] : null)
        {
            if (current.Id == nodeId)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Whether a subtree of <paramref name="subtreeHeight"/> levels below its own top fits under <paramref name="parentId"/>
    /// (<c>null</c>: the top level, level 1) within <see cref="MaxDepth"/> levels; throws
    /// <see cref="OrganizationErrorCodes.AreaDepthExceeded"/> when it does not. A new area is a subtree of height 0.
    /// </summary>
    public void EnsureCanHold(Guid? parentId, int subtreeHeight)
    {
        var topLevel = (parentId is { } parent ? LevelOf(parent) : 0) + 1;

        if (topLevel + subtreeHeight > MaxDepth)
        {
            throw new ConflictException(OrganizationErrorCodes.AreaDepthExceeded);
        }
    }

    private AreaNode Node(Guid id) =>
        _byId.TryGetValue(id, out var node)
            ? node
            : throw new KeyNotFoundException($"The area {id} is not in the tree.");

    private List<AreaNode> BuildPreOrder()
    {
        var order = new List<AreaNode>(_byId.Count);
        var stack = new Stack<AreaNode>();

        for (var index = _roots.Count - 1; index >= 0; index--)
        {
            stack.Push(_roots[index]);
        }

        while (stack.Count > 0)
        {
            var node = stack.Pop();
            order.Add(node);

            if (_children.TryGetValue(node.Id, out var children))
            {
                for (var index = children.Count - 1; index >= 0; index--)
                {
                    stack.Push(children[index]);
                }
            }
        }

        return order;
    }
}
