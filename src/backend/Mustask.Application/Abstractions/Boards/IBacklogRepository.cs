using Mustask.Domain.Boards;

namespace Mustask.Application.Abstractions.Boards;

/// <summary>
/// The write side of a backlog's order (mod-006 BR-M4-24, -25; mod-017 BR-OT-6, BR-OT-13). Locks follow ADR-0028 rule 8's
/// order: the team, then the backlog's <b>member items</b> (rule 8 step 5), then the backlog's order row (step 6). Each is taken
/// before what it guards is read, so the read sees what the previous holder committed.
/// </summary>
public interface IBacklogRepository
{
    /// <summary>
    /// Locks the team <c>FOR SHARE</c> and answers <c>false</c> when it is not
    /// an active team (deleted meanwhile: a deletion holds it
    /// <c>FOR NO KEY UPDATE</c>, so the two serialize); then locks the link between the project and the team <c>FOR SHARE</c> and
    /// answers <c>false</c> when it is gone (feat-028: an unlink or a deletion committed first and holds it <c>FOR NO KEY
    /// UPDATE</c>). The team row is taken <b>before</b> the link (ADR-0028 rule 8: step 2, then step 3). Otherwise locks the backlog's member items
    /// <c>FOR KEY SHARE</c>, in id order — a save of an item locks it <c>FOR UPDATE</c>, so a team change of a member waits
    /// for this reorder, or this reorder for it, and the place write's own foreign-key check (a key share too) never fails —
    /// and then the backlog's order row <c>FOR UPDATE</c>, creating it on the backlog's first reorder: from here to the
    /// commit no other reorder of this backlog proceeds, and no member leaves it.
    /// </summary>
    Task<bool> LockAsync(BacklogScope scope, CancellationToken cancellationToken);

    /// <summary>
    /// The backlog's items with their places on it, through the same membership as the reads, read <b>in a locking
    /// statement</b> (<c>FOR KEY SHARE</c> of the items): an item that changed owner before this read is no member, and one
    /// read here stays a member until the commit. Needs <see cref="LockAsync"/> first.
    /// </summary>
    Task<IReadOnlyList<StackRankEntry>> ReadEntriesAsync(BacklogScope scope, CancellationToken cancellationToken);

    /// <summary>
    /// Gives every item of <paramref name="order"/> the place it stands at
    /// (1-based), taking it from any other backlog, and drops the places this
    /// backlog holds for items not in <paramref name="order"/> (items that left it by state) — <b>of this project only</b>
    /// (feat-028 OQ-3 A, plan-backend D-13): the team's order is one per level, and its places on the items of another project
    /// are that project's view, which a reorder here never moves. Needs <see cref="LockAsync"/> first.
    /// </summary>
    Task SaveOrderAsync(BacklogScope scope, IReadOnlyList<Guid> order, CancellationToken cancellationToken);
}
