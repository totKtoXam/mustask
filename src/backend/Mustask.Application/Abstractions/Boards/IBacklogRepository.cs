using Mustask.Domain.Boards;

namespace Mustask.Application.Abstractions.Boards;

/// <summary>
/// The write side of a team backlog's order (mod-006 BR-M4-24, -25; mod-017 BR-OT-6, BR-OT-13; feat-040 BR-TS-6, BR-TS-7).
/// Locks follow ADR-0028 rule 8's order: the team, then the backlog's <b>member items</b> (rule 8 step 5), then the backlog's
/// order row (step 6) — no project row and no project–team link is locked (feat-040 plan-backend PD-8). Each is taken before
/// what it guards is read, so the read sees what the previous holder committed. Every statement carries the organization of
/// the address as its tenant predicate (BR-TS-13): the team is found by (organization, id), the members join their project on
/// it.
/// </summary>
public interface IBacklogRepository
{
    /// <summary>
    /// Locks the team <c>FOR SHARE</c> (found by <paramref name="organizationId"/> and id) and answers <c>false</c> when it is
    /// not an active team of the organization (deleted meanwhile: a deletion holds it <c>FOR NO KEY UPDATE</c>, so the two
    /// serialize). Otherwise locks the backlog's member items <c>FOR KEY SHARE</c> in id order — a save of an item locks it
    /// <c>FOR UPDATE</c>, so a team change of a member waits for this reorder, or this reorder for it, and the place write's
    /// own foreign-key check (a key share too) never fails; an unlink reassigns the owned items <c>FOR UPDATE</c> in id order
    /// too, so it and a reorder end in one order with no deadlock — and then the backlog's order row <c>FOR UPDATE</c>,
    /// creating it on the backlog's first reorder: from here to the commit no other reorder of this backlog proceeds, and no
    /// member leaves it. The members are the items the team owns in the active projects of the organization (a deactivation
    /// or a restore of a project is not serialized with the reorder: BR-TS-7). The scope carries no filter; a filtered one is
    /// refused as a programming error.
    /// </summary>
    Task<bool> LockAsync(Guid organizationId, TeamBacklogScope scope, CancellationToken cancellationToken);

    /// <summary>
    /// The backlog's items with their places on it, their creation time and their project key, through the same membership as
    /// the reads, read <b>in a locking statement</b> (<c>FOR KEY SHARE</c> of the items): an item that changed owner before this
    /// read is no member, and one read here stays a member until the commit. Needs <see cref="LockAsync"/> first.
    /// </summary>
    Task<IReadOnlyList<StackRankEntry>> ReadEntriesAsync(Guid organizationId, TeamBacklogScope scope, CancellationToken cancellationToken);

    /// <summary>
    /// Gives every item of <paramref name="order"/> the place it stands at (1-based), taking it from any other backlog, and
    /// drops the places this backlog holds for items that have <b>left it</b> — no longer owned by the team, of another type,
    /// or in the Removed category — <b>in the organization's active projects</b> (feat-040 BR-TS-7: the team's order is one per
    /// level across its projects). The places of a <b>deactivated project's</b> items are left exactly as they are
    /// (ADR-0028 rule 9). A place key that refuses a write (an item gone, or owned by another team) is a stale reorder
    /// (<c>boards.backlog.reorder_stale</c>), never a server error. Needs <see cref="LockAsync"/> first.
    /// </summary>
    Task SaveOrderAsync(Guid organizationId, TeamBacklogScope scope, IReadOnlyList<Guid> order, CancellationToken cancellationToken);
}
