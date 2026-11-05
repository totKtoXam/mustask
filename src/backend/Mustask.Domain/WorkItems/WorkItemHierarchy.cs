using Mustask.Shared.Errors;
using Mustask.Shared.Exceptions;

namespace Mustask.Domain.WorkItems;

/// <summary>
/// A type of the organization's process as the hierarchy reads it (feat-001
/// BR-H-2, BR-H-8): its key, the rank of the level that hosts it (mod-001
/// BR-M2-4, a higher rank is a higher level), and its states of the Removed
/// category — its «Удалена».
/// </summary>
public sealed record HierarchyType(Guid Id, string Key, int LevelRank, IReadOnlyCollection<Guid> RemovedStateIds);

/// <summary>
/// The organization's process as the hierarchy reads it: every type by id. A
/// work item's type never changes (BR-M3-1) and is always one of its
/// organization's process, so a type missing here is a defect, not an answer.
/// </summary>
public sealed class HierarchyTypes
{
    private readonly Dictionary<Guid, HierarchyType> _types;

    public HierarchyTypes(IEnumerable<HierarchyType> types)
    {
        ArgumentNullException.ThrowIfNull(types);

        _types = types.ToDictionary(type => type.Id);
    }

    public HierarchyType Of(Guid typeId) =>
        _types.TryGetValue(typeId, out var type)
            ? type
            : throw new ArgumentException($"The type {typeId} is not a type of the organization's process.", nameof(typeId));

    /// <summary>Whether the item is in its type's «Удалена» (BR-H-8, D-8: the Removed category).</summary>
    public bool IsDeleted(WorkItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        return Of(item.TypeId).RemovedStateIds.Contains(item.StateId);
    }

    /// <summary>The types an item of <paramref name="childTypeId"/> may have as its parent: its own level's and every higher one's (BR-H-2).</summary>
    public IReadOnlyList<Guid> ParentTypeIds(Guid childTypeId)
    {
        var rank = Of(childTypeId).LevelRank;

        return _types.Values.Where(type => WorkItemHierarchy.AllowsParent(rank, type.LevelRank)).Select(type => type.Id).ToList();
    }

    /// <summary>
    /// The types of the level directly above <paramref name="childTypeId"/>'s —
    /// the lowest rank higher than its own (BR-H-14, the backlog's mapping); none
    /// for a type of the top level.
    /// </summary>
    public IReadOnlyList<Guid> LevelAboveTypeIds(Guid childTypeId)
    {
        var rank = Of(childTypeId).LevelRank;
        var higher = _types.Values.Where(type => type.LevelRank > rank).ToList();

        if (higher.Count == 0)
        {
            return [];
        }

        var above = higher.Min(type => type.LevelRank);

        return higher.Where(type => type.LevelRank == above).Select(type => type.Id).ToList();
    }

    /// <summary>The identifier of an item of <paramref name="typeId"/> numbered <paramref name="number"/> in the project keyed <paramref name="projectKey"/>.</summary>
    public string IdentifierOf(string projectKey, Guid typeId, int number) => WorkItemIdentifier.Format(projectKey, Of(typeId).Key, number);
}

/// <summary>A stored link as the cycle check walks it: an item, its stored parent, and what its identifier needs.</summary>
public sealed record HierarchyLink(Guid ItemId, Guid? ParentId, Guid TypeId, int Number);

/// <summary>
/// One parent write (BR-H-3), as the caller has locked and loaded it: every
/// work item here is tracked and locked, and belongs to one project.
/// </summary>
/// <param name="Child">The item whose parent is written.</param>
/// <param name="StoredParent">The child's stored parent; <c>null</c> exactly when it has none.</param>
/// <param name="NewParent">The new parent; <c>null</c> to clear («Нет родителя», «Убрать родителя»).</param>
/// <param name="SeenParentId">
/// The parent the user saw when they chose, or <c>null</c> when they saw none
/// (BR-H-4). <see cref="Guid.Empty"/> stands for a value that named nothing: it
/// never matches.
/// </param>
/// <param name="NewParentAncestry">
/// The new parent and every ancestor of it, as stored, in any order; empty
/// when clearing. The cycle check walks it from the new parent upwards.
/// </param>
/// <param name="ChildSubtreeHeight">
/// The number of items on the longest chain of stored links from the child down
/// to its deepest descendant, the child itself counted: 1 for an item with no
/// children (feat-027 D-9). It may be capped at <see cref="WorkItemHierarchy.MaxChainLength"/>
/// — any height that large refuses the write whatever the new parent's chain.
/// Irrelevant when clearing.
/// </param>
public sealed record ParentWrite(
    WorkItem Child,
    WorkItem? StoredParent,
    WorkItem? NewParent,
    Guid? SeenParentId,
    IReadOnlyCollection<HierarchyLink> NewParentAncestry,
    int ChildSubtreeHeight);

/// <summary>
/// The Parent/Child link's rules (feat-001 BR-H-1..-5, BR-H-8): one decision
/// for every parent write — the form's save and the backlog's mapping alike.
/// </summary>
public static class WorkItemHierarchy
{
    /// <summary>The separator of a cycle's path in <see cref="WorkItemErrorCodes.Cycle"/> (spec § Error codes).</summary>
    public const string PathSeparator = " → ";

    /// <summary>
    /// feat-027 D-9: the most items one chain of parents holds, from a top item down to its deepest descendant. A
    /// write that would make any chain longer is refused with <see cref="WorkItemErrorCodes.DepthExceeded"/>. BR-H-2
    /// sets no depth otherwise — a task may stand under a task — and the backlog's tree is written to the panel as
    /// nested rows, so without a bound one cheap call per link deepens the answer for the whole team without end. 64 is
    /// far beyond the process's levels plus any same-level nesting a team keeps. Also the bound the backlog answer's
    /// JSON depth is derived from.
    /// </summary>
    public const int MaxChainLength = 64;

    /// <summary>BR-H-2: a parent's level is the child's own or a higher one, by rank — never lower.</summary>
    public static bool AllowsParent(int childLevelRank, int parentLevelRank) => parentLevelRank >= childLevelRank;

    /// <summary>
    /// Decides and applies one parent write: set, replace — the old link removed
    /// and the new one added at once — or clear. The checks run in the spec's
    /// order, and a refusal throws before anything changes (BR-H-3):
    /// <list type="number">
    /// <item>the child, then the new parent, in «Удалена» → <see cref="WorkItemErrorCodes.ItemDeleted"/> (BR-H-8);</item>
    /// <item>the stored parent is not the one seen → <see cref="WorkItemErrorCodes.ParentChanged"/>; having seen none
    /// also matches a stored parent in «Удалена» (BR-H-4);</item>
    /// <item>the child itself → <see cref="WorkItemErrorCodes.SelfParent"/>;</item>
    /// <item>a lower level → <see cref="WorkItemErrorCodes.ParentLevelNotAllowed"/> (BR-H-2);</item>
    /// <item>the child an ancestor of the new parent → <see cref="WorkItemErrorCodes.Cycle"/>, with the path from the
    /// child down to the new parent;</item>
    /// <item>the new parent's chain of ancestors, itself included, plus the height of the child's own subtree over
    /// <see cref="MaxChainLength"/> → <see cref="WorkItemErrorCodes.DepthExceeded"/> (feat-027 D-9).</item>
    /// </list>
    /// The stored parent named again changes nothing, and is not checked for depth: the link is already there.
    /// Otherwise «Изменён» moves on the child, on the parent removed unless it is in «Удалена», and on the parent
    /// added (BR-H-5); nothing else changes.
    /// </summary>
    /// <returns>Whether anything changed.</returns>
    public static bool Write(ParentWrite write, HierarchyTypes types, string projectKey, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(write);
        ArgumentNullException.ThrowIfNull(types);
        ArgumentException.ThrowIfNullOrEmpty(projectKey);

        var (child, storedParent, newParent) = (write.Child, write.StoredParent, write.NewParent);
        EnsureConsistent(write);

        if (types.IsDeleted(child) || (newParent is not null && types.IsDeleted(newParent)))
        {
            throw new ConflictException(WorkItemErrorCodes.ItemDeleted);
        }

        var storedIsDeleted = storedParent is not null && types.IsDeleted(storedParent);

        if (write.SeenParentId != child.ParentId && !(write.SeenParentId is null && storedIsDeleted))
        {
            throw new ConflictException(WorkItemErrorCodes.ParentChanged);
        }

        if (newParent is not null)
        {
            if (newParent.Id == child.Id)
            {
                throw new ConflictException(WorkItemErrorCodes.SelfParent);
            }

            if (!AllowsParent(types.Of(child.TypeId).LevelRank, types.Of(newParent.TypeId).LevelRank))
            {
                throw new ConflictException(WorkItemErrorCodes.ParentLevelNotAllowed);
            }
        }

        if (newParent?.Id == child.ParentId)
        {
            return false;
        }

        if (newParent is not null)
        {
            var parentChain = RefuseCycle(child, newParent, write.NewParentAncestry, types, projectKey);

            RefuseTooDeep(parentChain.Count + write.ChildSubtreeHeight);
        }

        child.LinkTo(newParent?.Id, now);

        if (storedParent is not null && !storedIsDeleted)
        {
            storedParent.MarkLinkChanged(now);
        }

        newParent?.MarkLinkChanged(now);

        return true;
    }

    /// <summary>
    /// A new item created beneath <paramref name="parent"/> (feat-027 BR-T-10): mod-003's creation
    /// (<see cref="WorkItem.Create"/>) in the parent's area as stored (a classification path, mod-017 BR-OT-10), owned by
    /// <paramref name="owningTeamId"/> — the team the caller decided under the parent's lock, the parent's as stored else the
    /// project default else none (<c>ProjectTeams.ChildTeam</c>, mod-017 BR-OT-3) — linked
    /// beneath the parent in the same step, its «Изменён» its creation time; the parent's «Изменён» moves to the same
    /// time (BR-H-5) and nothing else of it changes. Of <see cref="Write"/>'s checks, three can refuse an item that
    /// does not exist yet, in that order:
    /// <list type="number">
    /// <item>the parent in «Удалена» → <see cref="WorkItemErrorCodes.ItemDeleted"/> (BR-H-8);</item>
    /// <item>a parent of a lower level → <see cref="WorkItemErrorCodes.ParentLevelNotAllowed"/> (BR-H-2);</item>
    /// <item>the parent's chain of ancestors, itself included, already <see cref="MaxChainLength"/> items long, so
    /// the new item would be the next → <see cref="WorkItemErrorCodes.DepthExceeded"/> (feat-027 D-9).</item>
    /// </list>
    /// The rest cannot: the new item is in its type's initial state, has no stored parent to have changed, is not the
    /// parent and has no descendants — its subtree is itself alone. The caller holds the project's hierarchy lock and
    /// the parent's (feat-001 api-contract § 3.2), has taken <paramref name="number"/> from the project's sequence and
    /// validated the title; <paramref name="parentAncestry"/> is the parent and every ancestor of it as stored, in any
    /// order (as <see cref="ParentWrite.NewParentAncestry"/>).
    /// </summary>
    public static WorkItem CreateChild(
        WorkItem parent,
        IReadOnlyCollection<HierarchyLink> parentAncestry,
        HierarchyTypes types,
        int number,
        WorkItemTypeStates type,
        string title,
        Guid creationRequestId,
        DateTimeOffset now,
        string createdBy,
        Guid? owningTeamId)
    {
        ArgumentNullException.ThrowIfNull(parent);
        ArgumentNullException.ThrowIfNull(parentAncestry);
        ArgumentNullException.ThrowIfNull(types);
        ArgumentNullException.ThrowIfNull(type);

        if (types.IsDeleted(parent))
        {
            throw new ConflictException(WorkItemErrorCodes.ItemDeleted);
        }

        if (!AllowsParent(types.Of(type.TypeId).LevelRank, types.Of(parent.TypeId).LevelRank))
        {
            throw new ConflictException(WorkItemErrorCodes.ParentLevelNotAllowed);
        }

        RefuseTooDeep(Chain(parent, parentAncestry).Count + 1);

        var child = WorkItem.Create(parent.ProjectId, number, type, parent.AreaId, title, creationRequestId, now, createdBy, owningTeamId);
        child.LinkTo(parent.Id, now);
        parent.MarkLinkChanged(now);

        return child;
    }

    /// <summary>
    /// The stored chain upwards from <paramref name="start"/>, itself first, in the order of the links: its length is
    /// the depth of <paramref name="start"/>. A cycle can only close inside one level (BR-H-2), and no stored link
    /// forms one, so the walk ends; the visited set only guards against a defect.
    /// </summary>
    private static List<HierarchyLink> Chain(WorkItem start, IReadOnlyCollection<HierarchyLink> ancestry)
    {
        var links = ancestry.ToDictionary(link => link.ItemId);

        if (!links.ContainsKey(start.Id))
        {
            throw new ArgumentException("The ancestry starts at the new parent.", nameof(ancestry));
        }

        var chain = new List<HierarchyLink>();
        var visited = new HashSet<Guid>();
        Guid? current = start.Id;

        while (current is { } id && visited.Add(id) && links.TryGetValue(id, out var link))
        {
            chain.Add(link);
            current = link.ParentId;
        }

        return chain;
    }

    /// <summary>
    /// Walks the new parent's stored ancestry upwards; reaching the child means
    /// the link would make the child its own ancestor.
    /// </summary>
    /// <returns>The new parent's chain upwards, itself first (<see cref="Chain"/>).</returns>
    private static List<HierarchyLink> RefuseCycle(
        WorkItem child, WorkItem newParent, IReadOnlyCollection<HierarchyLink> ancestry, HierarchyTypes types, string projectKey)
    {
        var chain = Chain(newParent, ancestry);
        var reached = chain.FindIndex(link => link.ItemId == child.Id);

        if (reached >= 0)
        {
            var path = chain.Take(reached + 1).Reverse().ToList();

            throw new ConflictException(
                WorkItemErrorCodes.Cycle,
                new ErrorParams
                {
                    ["parent"] = ErrorParam.Text(types.IdentifierOf(projectKey, newParent.TypeId, newParent.Number)),
                    ["item"] = ErrorParam.Text(types.IdentifierOf(projectKey, child.TypeId, child.Number)),
                    ["path"] = ErrorParam.Text(string.Join(PathSeparator, path.Select(step => types.IdentifierOf(projectKey, step.TypeId, step.Number)))),
                });
        }

        return chain;
    }

    /// <summary>
    /// feat-027 D-9: <paramref name="chainLength"/>, the items on the longest chain the write makes, over
    /// <see cref="MaxChainLength"/> refuses it. The limit goes out as the refusal's parameter, so the text and the
    /// bound cannot drift apart.
    /// </summary>
    private static void RefuseTooDeep(int chainLength)
    {
        if (chainLength > MaxChainLength)
        {
            throw new ConflictException(
                WorkItemErrorCodes.DepthExceeded,
                new ErrorParams { ["limit"] = ErrorParam.Number(MaxChainLength) });
        }
    }

    private static void EnsureConsistent(ParentWrite write)
    {
        if (write.StoredParent?.Id != write.Child.ParentId)
        {
            throw new ArgumentException("The stored parent is the child's own.", nameof(write));
        }

        if ((write.StoredParent is { } stored && stored.ProjectId != write.Child.ProjectId)
            || (write.NewParent is { } parent && parent.ProjectId != write.Child.ProjectId))
        {
            throw new ArgumentException("A link joins two items of one project (BR-H-1).", nameof(write));
        }

        if (write.ChildSubtreeHeight < 1)
        {
            throw new ArgumentException("A subtree holds at least its own root.", nameof(write));
        }
    }
}
