namespace Mustask.Domain.Organizations;

/// <summary>One area as the tree sees it: its place, its name, its explicit owner and the team whose default it is.</summary>
public sealed record AreaNode(Guid Id, Guid? ParentId, string Name, Guid? OwnerTeamId, Guid? DefaultOfTeamId);

/// <summary>The team that owns an area, and whether that ownership is inherited from an ancestor.</summary>
public readonly record struct ResolvedOwner(Guid TeamId, bool IsInherited);

/// <summary>
/// A pure view of one project's area tree, built from its nodes. It is the
/// single definition of an area's path (DC-9), its depth, its resolved owner
/// (BR-M1-11) and of "the node or one of its descendants" (BR-M1-12): the
/// tree read, the team reads, the move and the reads for other modules all use
/// it, so other modules get the same answer as the tree (US-6).
/// </summary>
public sealed class AreaTree
{
    /// <summary>What joins the names of a path (DC-9).</summary>
    public const string PathSeparator = " / ";

    private readonly Dictionary<Guid, AreaNode> _byId;
    private readonly Dictionary<Guid, List<AreaNode>> _children;
    private readonly List<AreaNode> _preOrder;

    /// <summary>
    /// Builds the tree. Siblings keep the order <paramref name="nodes"/> gives
    /// them, so a caller that reads the nodes in display order gets the display
    /// order back from <see cref="PreOrder"/>. Exactly one node has no parent,
    /// every parent is a node of the tree, and every node is reachable from the root.
    /// </summary>
    public AreaTree(IEnumerable<AreaNode> nodes)
    {
        ArgumentNullException.ThrowIfNull(nodes);

        var list = nodes.ToList();
        _byId = [];
        _children = [];
        AreaNode? root = null;

        foreach (var node in list)
        {
            if (!_byId.TryAdd(node.Id, node))
            {
                throw new ArgumentException($"The area {node.Id} appears twice.", nameof(nodes));
            }

            if (node.ParentId is null)
            {
                if (root is not null)
                {
                    throw new ArgumentException("A project's tree has exactly one root.", nameof(nodes));
                }

                root = node;
            }
        }

        Root = root ?? throw new ArgumentException("A project's tree has a root.", nameof(nodes));

        foreach (var node in list)
        {
            if (node.ParentId is { } parentId)
            {
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
        }

        _preOrder = BuildPreOrder();

        if (_preOrder.Count != _byId.Count)
        {
            throw new ArgumentException("Every area is reachable from the root: the nodes hold a cycle.", nameof(nodes));
        }
    }

    public AreaNode Root { get; }

    public int Count => _byId.Count;

    public bool Contains(Guid id) => _byId.ContainsKey(id);

    public AreaNode this[Guid id] => Node(id);

    /// <summary>Every node, each before its children, siblings in the order the tree was built with.</summary>
    public IReadOnlyList<AreaNode> PreOrder() => _preOrder;

    /// <summary>The direct children of <paramref name="id"/>, in the order the tree was built with.</summary>
    public IReadOnlyList<AreaNode> ChildrenOf(Guid id)
    {
        Node(id);

        return _children.TryGetValue(id, out var children) ? children : [];
    }

    /// <summary>The root's own name for the root; otherwise the names below the root, joined by <see cref="PathSeparator"/> (DC-9).</summary>
    public string PathOf(Guid id)
    {
        var node = Node(id);

        if (node.ParentId is null)
        {
            return node.Name;
        }

        var names = new List<string>();

        for (var current = node; current.ParentId is { } parentId; current = _byId[parentId])
        {
            names.Add(current.Name);
        }

        names.Reverse();

        return string.Join(PathSeparator, names);
    }

    /// <summary>0 for the root.</summary>
    public int DepthOf(Guid id)
    {
        var depth = 0;

        for (var current = Node(id); current.ParentId is { } parentId; current = _byId[parentId])
        {
            depth++;
        }

        return depth;
    }

    /// <summary>
    /// The nearest explicit owner up the tree, at the latest the root's, which is
    /// the first team (BR-M1-11). Every node has exactly one.
    /// </summary>
    public ResolvedOwner ResolveOwner(Guid id)
    {
        var node = Node(id);

        for (var current = node; ; current = _byId[current.ParentId!.Value])
        {
            if (current.OwnerTeamId is { } owner)
            {
                return new ResolvedOwner(owner, IsInherited: current.Id != node.Id);
            }

            if (current.ParentId is null)
            {
                throw new InvalidOperationException("The root is always owned explicitly (BR-M1-11).");
            }
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

    private AreaNode Node(Guid id) =>
        _byId.TryGetValue(id, out var node)
            ? node
            : throw new KeyNotFoundException($"The area {id} is not in the tree.");

    private List<AreaNode> BuildPreOrder()
    {
        var order = new List<AreaNode>(_byId.Count);
        var stack = new Stack<AreaNode>();
        stack.Push(Root);

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
