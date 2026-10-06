using Mustask.Domain.WorkItems;

namespace Mustask.Application.Abstractions.WorkItems;

/// <summary>
/// The write side of work items (mod-003). Locks follow one order: the
/// project's number counter (creation only), then the team a creation comes from
/// (mod-006), then the project's hierarchy (a parent write only, feat-001), then
/// the item, then the area it moves into, then its new and its stored parent (a
/// parent write only). Each lock is taken before the row it guards is read, so
/// the read sees the row as the previous lock holder left it. No holder of an
/// area's lock waits on an item, so the area may come before the parents.
/// </summary>
public interface IWorkItemRepository
{
    /// <summary>
    /// mod-006 (BR-M4-21): locks the team <c>FOR SHARE</c> and answers its
    /// default area as it stands, or <c>null</c> when it is not an active team of
    /// the project. A default change locks the team <c>FOR NO KEY UPDATE</c> and a
    /// deletion <c>FOR UPDATE</c> (mod-002), so neither can move the answer before
    /// the commit, and a default area is never deleted while it is one.
    /// </summary>
    Task<Guid?> LockTeamDefaultAreaAsync(Guid projectId, Guid teamId, CancellationToken cancellationToken);

    /// <summary>
    /// Locks the project's number counter <c>FOR UPDATE</c>, creating it on the
    /// project's first creation. From here to the commit no other creation of
    /// the project proceeds (BR-M3-5).
    /// </summary>
    Task LockNumberSequenceAsync(Guid projectId, CancellationToken cancellationToken);

    /// <summary>
    /// The next number of the project: one more than every number it issued
    /// before. Needs <see cref="LockNumberSequenceAsync"/> first.
    /// </summary>
    Task<int> TakeNextNumberAsync(Guid projectId, CancellationToken cancellationToken);

    /// <summary>The item the submission <paramref name="requestId"/> created in the project, or <c>null</c> (BR-M3-26).</summary>
    Task<Guid?> FindCreatedByRequestAsync(Guid projectId, Guid requestId, CancellationToken cancellationToken);

    /// <summary>The project's item with that number, locked <c>FOR UPDATE</c> and tracked; <c>null</c> when there is none.</summary>
    Task<WorkItem?> LockAsync(Guid projectId, int number, CancellationToken cancellationToken);

    /// <summary>
    /// Locks the area of that project <c>FOR SHARE</c> and answers whether it
    /// exists (BR-M3-23, BR-M3-24). The area deletion locks it <c>FOR UPDATE</c>,
    /// so the two serialize: an area deleted meanwhile is gone when this lock is
    /// granted, and one that is still there cannot be deleted until the commit.
    /// </summary>
    Task<bool> LockAreaAsync(Guid projectId, Guid areaId, CancellationToken cancellationToken);

    /// <summary>
    /// feat-001: locks the project's hierarchy <c>FOR UPDATE</c>, creating its row
    /// on the project's first parent write. Every parent write takes it before
    /// it locks its item, so from here to the commit no other parent write of the
    /// project proceeds: the stored parent and the ancestry a write decides on
    /// stay as it read them (BR-H-4, spec § NFR "two writes that would together
    /// close a cycle never both succeed").
    /// </summary>
    Task LockHierarchyAsync(Guid projectId, CancellationToken cancellationToken);

    /// <summary>
    /// feat-001: the rest of a parent write of <paramref name="child"/>, which
    /// <see cref="LockAsync"/> has locked after <see cref="LockHierarchyAsync"/>
    /// (and, on a save that moves it, after its new area's lock):
    /// the new parent, then the child's stored parent, each locked
    /// <c>FOR NO KEY UPDATE</c> and tracked — a backlog reorder's foreign-key check
    /// (<c>FOR KEY SHARE</c>) is never waited on — and the new parent's ancestry
    /// as stored. <c>null</c> when <paramref name="newParentId"/> names no item of
    /// the child's project (BR-H-1).
    /// </summary>
    Task<ParentWrite?> LockParentWriteAsync(WorkItem child, Guid? newParentId, Guid? seenParentId, CancellationToken cancellationToken);

    void Add(WorkItem item);

    /// <summary>Flushes the tracked changes inside the caller's transaction.</summary>
    Task SaveAsync(CancellationToken cancellationToken);
}
