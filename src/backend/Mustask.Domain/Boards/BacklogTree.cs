namespace Mustask.Domain.Boards;

/// <summary>An item of one backlog as the tree needs it: its stack rank entry and its stored parent.</summary>
public sealed record BacklogTreeEntry(Guid ItemId, int Number, int? Place, Guid? ParentId);

/// <summary>A row of the tree: the item and the rows nested beneath it, in their own backlog's order.</summary>
public sealed record BacklogTreeNode(Guid ItemId, IReadOnlyList<BacklogTreeNode> Children);

/// <summary>
/// A team's backlog with its tree (feat-001 BR-H-9..-11): the tree is read from
/// the links, the backlogs store none of it. The backlog's own items are the
/// top-level rows, in its stack rank (mod-006 BR-M4-24); beneath a row are the
/// items of the same team's backlog exactly one level below whose stored parent
/// is that row, in that backlog's own stack rank, and so on down.
/// </summary>
public static class BacklogTree
{
    /// <summary>
    /// Nests <paramref name="levels"/>: the backlog's own entries first, then the
    /// entries of the same team's backlog one level below, and so on. An item
    /// whose parent is not an entry of the level directly above — no parent, a
    /// parent on its own level or further up, on another team's backlog, or in
    /// «Удалена» (no backlog holds such an item) — nests under no row: it is a
    /// top-level row of its own level's backlog only (BR-H-10).
    /// </summary>
    public static IReadOnlyList<BacklogTreeNode> Nest(IReadOnlyList<IReadOnlyCollection<BacklogTreeEntry>> levels)
    {
        ArgumentNullException.ThrowIfNull(levels);

        if (levels.Count == 0)
        {
            return [];
        }

        // From the lowest level up: a level's rows take their children from the rows already built beneath it.
        IReadOnlyDictionary<Guid, List<BacklogTreeNode>> beneath = new Dictionary<Guid, List<BacklogTreeNode>>();
        IReadOnlyList<BacklogTreeNode> rows = [];

        for (var index = levels.Count - 1; index >= 0; index--)
        {
            var entries = levels[index];
            var byId = entries.ToDictionary(entry => entry.ItemId);
            var children = beneath;

            rows = StackRank.Order(entries.Select(entry => new StackRankEntry(entry.ItemId, entry.Number, entry.Place)))
                .Select(id => new BacklogTreeNode(id, children.TryGetValue(id, out var nested) ? nested : []))
                .ToList();

            var byParent = new Dictionary<Guid, List<BacklogTreeNode>>();

            foreach (var row in rows)
            {
                if (byId[row.ItemId].ParentId is { } parentId)
                {
                    if (!byParent.TryGetValue(parentId, out var siblings))
                    {
                        byParent[parentId] = siblings = [];
                    }

                    siblings.Add(row);
                }
            }

            beneath = byParent;
        }

        return rows;
    }
}
