using Mustask.Domain.Iterations;

namespace Mustask.Application.Abstractions.Iterations;

/// <summary>
/// The write side of sprints (mod-007 plan-backend, "Locks"). A lock takes the row in the mode a use case needs and carries the
/// organization and the team, so a sprint of another team locks nothing and answers <c>null</c> (an id that names nothing). The
/// close, the removal and the team's numbering run in the caller's transaction (<see cref="IUnitOfWork"/>).
/// </summary>
public interface ISprintRepository
{
    /// <summary>The sprint of the team <c>FOR UPDATE</c>, tracked, or <c>null</c> (ADR-0028 rule 8: after the team's own lock).</summary>
    Task<Sprint?> LockForUpdateAsync(Guid organizationId, Guid teamId, Guid sprintId, CancellationToken cancellationToken);

    /// <summary>
    /// A sprint of the organization <c>FOR SHARE</c> by what the lock statement returned, whatever team it belongs to (a work item's
    /// save binds to it: a close or a removal of the sprint waits for the save, or the save sees the new state). <c>null</c> for an
    /// id that names nothing in the organization.
    /// </summary>
    Task<SprintFacts?> LockForShareAsync(Guid organizationId, Guid sprintId, CancellationToken cancellationToken);

    /// <summary>
    /// The team's next number (1, 2, 3, never reused, BR-7), issued under the counter's row lock: two creations at once get two
    /// numbers. A refusal after it rolls the issue back with the transaction.
    /// </summary>
    Task<int> TakeNextNumberAsync(Guid teamId, CancellationToken cancellationToken);

    /// <summary>Whether another sprint of the team holds the normalized name (BR-7).</summary>
    Task<bool> IsNameTakenAsync(Guid teamId, string normalizedName, Guid? exceptId, CancellationToken cancellationToken);

    /// <summary>Whether the team has an Active sprint other than <paramref name="exceptId"/>.</summary>
    Task<bool> HasActiveAsync(Guid teamId, Guid exceptId, CancellationToken cancellationToken);

    /// <summary>Adds a sprint of a team (<see cref="Sprint.Create"/>).</summary>
    void Add(Sprint sprint);

    /// <summary>Removes the sprint for good (BR-8): its number stays issued, its name is free.</summary>
    void Remove(Sprint sprint);

    /// <summary>
    /// Locks the sprint's items <c>FOR UPDATE</c> in id order, after the hierarchy locks of their projects (the order a team's
    /// unlink takes), and answers the number of items outside «Удалена», whatever the project's status (BR-8).
    /// </summary>
    Task<int> LockItemsAsync(Guid sprintId, CancellationToken cancellationToken);

    /// <summary>Clears the pointer of every item of the sprint (the removal's system write: only «Удалена» items remain).</summary>
    Task ClearItemsAsync(Guid sprintId, CancellationToken cancellationToken);

    /// <summary>
    /// Writes the close record (BR-14): every item of the sprint outside «Удалена», with its project, level, story points, assignee
    /// and done flag as they are now. One set-based statement; the items are locked.
    /// </summary>
    Task WriteSnapshotAsync(Guid sprintId, CancellationToken cancellationToken);

    /// <summary>
    /// Moves the items of the sprint in a Proposed, In Progress or Resolved category to <paramref name="destinationId"/> (or to no
    /// sprint) and gives them <paramref name="now"/> as «Изменён» (BR-6). Completed and «Удалена» items keep the sprint.
    /// </summary>
    Task MoveUnfinishedAsync(Guid sprintId, Guid? destinationId, DateTimeOffset now, CancellationToken cancellationToken);

    /// <summary>Flushes the sprint's changes; a unique index that closes a race answers as its pre-check does.</summary>
    Task SaveAsync(CancellationToken cancellationToken);
}

/// <summary>A sprint as a lock statement returned it: enough to judge an item's sprint (BR-2, BR-5).</summary>
public sealed record SprintFacts(Guid Id, Guid TeamId, SprintState State);
