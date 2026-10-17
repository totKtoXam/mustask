using Mustask.Domain.Boards;

namespace Mustask.Application.Abstractions.Boards;

/// <summary>
/// The write side of a project backlog's order (feat-039 BR-PB-4, ADR-0028 rule 9): the same shape as the team's
/// <see cref="IBacklogRepository"/>, over the project's own places and lock rows, so a reorder of a project's backlog
/// and one of a team's never touch each other. Locks follow one order: the project, then the backlog. Each is taken
/// before what it guards is read, so the read sees what the previous holder committed.
/// </summary>
public interface IProjectBacklogRepository
{
    /// <summary>
    /// Locks the project <c>FOR SHARE</c> and answers <c>false</c> when it is not a reachable project of the
    /// organization (deactivated meanwhile: a deactivation holds it <c>FOR NO KEY UPDATE</c>, so the two serialize).
    /// Otherwise locks the backlog's order row <c>FOR UPDATE</c>, creating it on the backlog's first reorder: from
    /// here to the commit no other reorder of this project backlog proceeds.
    /// </summary>
    Task<bool> LockAsync(Guid organizationId, Guid projectId, Guid levelId, CancellationToken cancellationToken);

    /// <summary>The project backlog's items with their places on it, through the same membership query as the reads.</summary>
    Task<IReadOnlyList<StackRankEntry>> ReadEntriesAsync(ProjectBacklogScope scope, CancellationToken cancellationToken);

    /// <summary>
    /// Gives every item of <paramref name="order"/> the place it stands at (1-based) and drops the places this
    /// project backlog holds for items not in <paramref name="order"/>. Writes no team place. Needs
    /// <see cref="LockAsync"/> first.
    /// </summary>
    Task SaveOrderAsync(ProjectBacklogScope scope, IReadOnlyList<Guid> order, CancellationToken cancellationToken);
}
