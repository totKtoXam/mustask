namespace Mustask.Domain.Boards;

/// <summary>
/// An item of a backlog page's tree set (feat-027 api-contract § 1) as the tree
/// needs it: its stack rank entry on its own level's backlog of the team (the
/// place <c>null</c> when unplaced there — always, on the «Задачи» level), its
/// stored parent, and the rank of its level (mod-001 BR-M2-4, a higher rank is
/// a higher level).
/// </summary>
public sealed record BacklogTreeEntry(Guid ItemId, int Number, int? Place, Guid? ParentId, int LevelRank);

/// <summary>A row of the tree: the item and the rows nested beneath it, in display order.</summary>
public sealed record BacklogTreeNode(Guid ItemId, IReadOnlyList<BacklogTreeNode> Children);

/// <summary>
/// A team's backlog page with its tree at full depth (feat-027 BR-T-4..-6): the
/// tree is read from the links, the backlogs store none of it. The entries are
/// the tree set — the team's live items of the page's level and of every level
/// ranked below it.
/// </summary>
public static class BacklogTree
{
    /// <summary>
    /// Nests <paramref name="entries"/> for the page of the level ranked <paramref name="levelRank"/>:
    /// <list type="bullet">
    /// <item>an entry stands beneath its stored parent exactly when that parent is an entry too and its level ranks
    /// the same or higher (BR-H-2) — a child of another team or in «Удалена» is no entry, so it nests nowhere;</item>
    /// <item>the top-level rows are the entries of the page's level that stand beneath no entry (BR-T-5): a
    /// same-level child stands beneath its parent only;</item>
    /// <item>an entry of a lower level that stands beneath no entry is not on the page;</item>
    /// <item>rows stand by level, highest first, then in their level's stack rank (BR-T-6, <see cref="StackRank.Order"/>);
    /// the top-level rows likewise.</item>
    /// </list>
    /// Every entry stands at most once. Entries a stored cycle would join — no parent write makes one — stand beneath
    /// one another and none at the top, so they are not reached; the walk keeps a visited set besides, and is a loop,
    /// not a recursion, so no depth of the tree can exhaust the stack.
    /// </summary>
    public static IReadOnlyList<BacklogTreeNode> Nest(int levelRank, IReadOnlyCollection<BacklogTreeEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var byId = entries.ToDictionary(entry => entry.ItemId);

        // Display order once for the whole set: each level's stack rank, the levels from the highest down.
        var ordered = entries
            .GroupBy(entry => entry.LevelRank)
            .OrderByDescending(level => level.Key)
            .SelectMany(level => StackRank.Order(level.Select(entry => new StackRankEntry(entry.ItemId, entry.Number, entry.Place))));

        var beneath = new Dictionary<Guid, List<Guid>>();
        var top = new List<Guid>();

        foreach (var id in ordered)
        {
            var entry = byId[id];

            if (ParentOf(entry, byId) is { } parentId)
            {
                if (!beneath.TryGetValue(parentId, out var siblings))
                {
                    beneath[parentId] = siblings = [];
                }

                siblings.Add(id);
            }
            else if (entry.LevelRank == levelRank)
            {
                top.Add(id);
            }
        }

        return Build(top, beneath);
    }

    /// <summary>The entry's parent when it nests beneath it: an entry of its own level or a higher one, not itself.</summary>
    private static Guid? ParentOf(BacklogTreeEntry entry, IReadOnlyDictionary<Guid, BacklogTreeEntry> byId) =>
        entry.ParentId is { } parentId
        && parentId != entry.ItemId
        && byId.TryGetValue(parentId, out var parent)
        && parent.LevelRank >= entry.LevelRank
            ? parentId
            : null;

    /// <summary>The rows from <paramref name="top"/> down, depth first, each list in the order it was given.</summary>
    private static List<BacklogTreeNode> Build(List<Guid> top, Dictionary<Guid, List<Guid>> beneath)
    {
        var rows = new List<BacklogTreeNode>();
        var visited = new HashSet<Guid>();
        var pending = new Stack<(Guid ItemId, List<BacklogTreeNode> Into)>();

        for (var index = top.Count - 1; index >= 0; index--)
        {
            pending.Push((top[index], rows));
        }

        while (pending.TryPop(out var next))
        {
            if (!visited.Add(next.ItemId))
            {
                continue;
            }

            var children = new List<BacklogTreeNode>();
            next.Into.Add(new BacklogTreeNode(next.ItemId, children));

            if (beneath.TryGetValue(next.ItemId, out var nested))
            {
                for (var index = nested.Count - 1; index >= 0; index--)
                {
                    pending.Push((nested[index], children));
                }
            }
        }

        return rows;
    }
}
