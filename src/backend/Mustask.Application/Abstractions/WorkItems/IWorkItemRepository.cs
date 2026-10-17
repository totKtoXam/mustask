using Mustask.Domain.WorkItems;

namespace Mustask.Application.Abstractions.WorkItems;

/// <summary>
/// The write side of work items (mod-003). Locks follow one order: the team a
/// write binds to or may default to (mod-017, ADR-0028 rule 8 — first of all, so a
/// team's deletion and every binder serialize on the team row), then the
/// project's number counter (creation only), then the project's hierarchy (a
/// parent write, feat-001, or a creation with a parent, feat-027), then the item,
/// then the area it moves into, then its new and its stored parent (a parent
/// write, or a creation's parent).
/// Each lock is taken before the row it guards is read, so the read sees the row
/// as the previous lock holder left it. No holder of an area's lock waits on an
/// item, so the area may come before the parents.
/// </summary>
public interface IWorkItemRepository
{
    /// <summary>
    /// feat-028 (ADR-0028 rule 8 step 3): locks the <b>link</b> between the project and the team <c>FOR SHARE</c> — the item
    /// binder's lock, taken <b>first</b> of a write's locks — and decides nothing. A team of another organization locks nothing
    /// (no lock spans organizations, ADR-0028 § 4: the statement carries the organization); the caller judges the team afterwards
    /// with <c>ProjectTeams.EnsureBindable</c> on a fresh read. An unlink locks the link <c>FOR NO KEY UPDATE</c> and a deletion
    /// locks all of the team's links so, so a binder and an unlink or a deletion of the same team serialize: the unlink first, the
    /// binder reads the team not linked (409); the binder first, the unlink reassigns what the binder wrote.
    /// </summary>
    Task LockTeamLinkAsync(Guid organizationId, Guid projectId, Guid teamId, CancellationToken cancellationToken);

    /// <summary>
    /// feat-028 (BR-OT-3, BR-OL-8, plan-backend D-4): locks <b>every link of the project</b> <c>FOR SHARE</c> in one statement
    /// ordered by team id, then reads the project default team once under that lock — the team the settings name when it is
    /// still linked and active, else the project's only linked active team, else none (<c>ProjectDefaultTeam</c>). A creation that
    /// may take the default (no team sent, or a child, whatever team its parent has) takes this lock <b>before</b> the number
    /// lock, whether or not it ends up using the default: the fallback is known only after the parent is locked, and a lock not
    /// taken then would let the child land on a team a concurrent unlink or deletion has just committed. Every link lock a
    /// creation takes is therefore in the unlink's and the deletion's order, so the three-request cycle of mod-017 CR-7 cannot
    /// form. Answers the default team, or <c>null</c>.
    /// </summary>
    Task<Guid?> LockProjectLinksAsync(Guid organizationId, Guid projectId, CancellationToken cancellationToken);

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
    /// as stored, and — for the depth bound (feat-027 D-9) — the height of the child's own subtree, each in one
    /// recursive statement. <c>null</c> when <paramref name="newParentId"/> names no item of
    /// the child's project (BR-H-1).
    /// </summary>
    Task<ParentWrite?> LockParentWriteAsync(WorkItem child, Guid? newParentId, Guid? seenParentId, CancellationToken cancellationToken);

    /// <summary>
    /// feat-027 (BR-T-10): the parent a creation names, after <see cref="LockHierarchyAsync"/> — the project's item
    /// with that id, locked <c>FOR NO KEY UPDATE</c> as a parent write's link end and tracked, so its area and state
    /// stay as read until the commit; <c>null</c> when the project has no such item (BR-H-1).
    /// </summary>
    Task<WorkItem?> LockNewParentAsync(Guid projectId, Guid parentId, CancellationToken cancellationToken);

    /// <summary>
    /// feat-027 D-9: the item and every ancestor of it as stored, in one recursive statement (the creation's parent,
    /// for the depth bound). Read under the hierarchy lock, so no link of the project moves meanwhile.
    /// </summary>
    Task<IReadOnlyCollection<HierarchyLink>> AncestryAsync(Guid projectId, Guid workItemId, CancellationToken cancellationToken);

    void Add(WorkItem item);

    /// <summary>Flushes the tracked changes inside the caller's transaction.</summary>
    Task SaveAsync(CancellationToken cancellationToken);
}
