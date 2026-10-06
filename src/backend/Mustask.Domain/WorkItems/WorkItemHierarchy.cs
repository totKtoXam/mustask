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
public sealed record ParentWrite(
    WorkItem Child,
    WorkItem? StoredParent,
    WorkItem? NewParent,
    Guid? SeenParentId,
    IReadOnlyCollection<HierarchyLink> NewParentAncestry);

/// <summary>
/// The Parent/Child link's rules (feat-001 BR-H-1..-5, BR-H-8): one decision
/// for every parent write — the form's save and the backlog's mapping alike.
/// </summary>
public static class WorkItemHierarchy
{
    /// <summary>The separator of a cycle's path in <see cref="WorkItemErrorCodes.Cycle"/> (spec § Error codes).</summary>
    public const string PathSeparator = " → ";

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
    /// child down to the new parent.</item>
    /// </list>
    /// The stored parent named again changes nothing. Otherwise «Изменён» moves
    /// on the child, on the parent removed unless it is in «Удалена», and on the
    /// parent added (BR-H-5); nothing else changes.
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
            RefuseCycle(child, newParent, write.NewParentAncestry, types, projectKey);
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
    /// Walks the new parent's stored ancestry upwards; reaching the child means
    /// the link would make the child its own ancestor. A cycle can only close
    /// inside one level (BR-H-2), and no stored link forms one, so the walk ends;
    /// the visited set only guards against a defect.
    /// </summary>
    private static void RefuseCycle(
        WorkItem child, WorkItem newParent, IReadOnlyCollection<HierarchyLink> ancestry, HierarchyTypes types, string projectKey)
    {
        var links = ancestry.ToDictionary(link => link.ItemId);

        if (!links.ContainsKey(newParent.Id))
        {
            throw new ArgumentException("The ancestry starts at the new parent.", nameof(ancestry));
        }

        var chain = new List<HierarchyLink>();
        var visited = new HashSet<Guid>();
        Guid? current = newParent.Id;

        while (current is { } id && visited.Add(id) && links.TryGetValue(id, out var link))
        {
            chain.Add(link);

            if (id == child.Id)
            {
                chain.Reverse();

                throw new ConflictException(
                    WorkItemErrorCodes.Cycle,
                    new ErrorParams
                    {
                        ["parent"] = ErrorParam.Text(types.IdentifierOf(projectKey, newParent.TypeId, newParent.Number)),
                        ["item"] = ErrorParam.Text(types.IdentifierOf(projectKey, child.TypeId, child.Number)),
                        ["path"] = ErrorParam.Text(string.Join(PathSeparator, chain.Select(step => types.IdentifierOf(projectKey, step.TypeId, step.Number)))),
                    });
            }

            current = link.ParentId;
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
    }
}
