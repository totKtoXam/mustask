using Mustask.Domain.Boards;

namespace Mustask.Application.Abstractions.Boards;

/// <summary>
/// The write side of a backlog's order (mod-006 BR-M4-24, -25). Locks follow
/// one order: the team, then the backlog. Each is taken before what it guards is
/// read, so the read sees what the previous holder committed.
/// </summary>
public interface IBacklogRepository
{
    /// <summary>
    /// Locks the team <c>FOR SHARE</c> and answers <c>false</c> when it is not
    /// an active team of the project (deleted meanwhile: a deletion holds it
    /// <c>FOR UPDATE</c>, so the two serialize). Otherwise locks the backlog's
    /// order row <c>FOR UPDATE</c>, creating it on the backlog's first reorder:
    /// from here to the commit no other reorder of this backlog proceeds.
    /// </summary>
    Task<bool> LockAsync(Guid projectId, Guid teamId, Guid levelId, CancellationToken cancellationToken);

    /// <summary>The backlog's items with their places on it, through the same membership query as the reads.</summary>
    Task<IReadOnlyList<StackRankEntry>> ReadEntriesAsync(BacklogScope scope, CancellationToken cancellationToken);

    /// <summary>
    /// Gives every item of <paramref name="order"/> the place it stands at
    /// (1-based), taking it from any other backlog, and drops the places this
    /// backlog holds for items not in <paramref name="order"/>. Needs
    /// <see cref="LockAsync"/> first.
    /// </summary>
    Task SaveOrderAsync(BacklogScope scope, IReadOnlyList<Guid> order, CancellationToken cancellationToken);
}
