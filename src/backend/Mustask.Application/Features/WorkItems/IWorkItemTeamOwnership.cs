namespace Mustask.Application.Features.WorkItems;

/// <summary>
/// The work items module's write contract for a team's unlink and deletion (mod-017, feat-028, ADR-0028 rule 7): who owns the
/// items a team owns is the work items module's data, so the organizations module asks it to count, lock and reassign them in
/// the caller's transaction rather than writing <c>work_items</c> itself. It sits at the module's root, not inside a slice, so a
/// slice of another module can depend on it without referencing a slice of this one (docs/conventions/backend.md: modules talk
/// through explicit interfaces).
/// </summary>
/// <remarks>
/// Scoped; no access check and no event. The calls lock their own rows in ADR-0028 rule 8's order — the items after the teams
/// and the links, by id — and join the caller's transaction. An item "owned by the team" is one the team owns in <b>any
/// state</b>, Removed included (BR-OT-12). The statements take a <b>set of projects</b>: an unlink passes one, a deletion every
/// project the team is linked to, so the number of statements does not grow with the number of projects. The caller passes ids it
/// resolved under the organization; none of these statements names an organization of its own.
/// </remarks>
public interface IWorkItemTeamOwnership
{
    /// <summary>The number of items of the project the team owns now, in any state (the unlink's confirmation). One statement.</summary>
    Task<int> CountOwnedAsync(Guid projectId, Guid teamId, CancellationToken cancellationToken);

    /// <summary>
    /// The number of items the team owns in each of <paramref name="projectIds"/>, in any state, with one <c>GROUP BY</c>
    /// statement (the deletion's confirmation). A project in which the team owns nothing is simply absent.
    /// </summary>
    Task<IReadOnlyList<ProjectCount>> CountOwnedByProjectAsync(Guid teamId, IReadOnlyCollection<Guid> projectIds, CancellationToken cancellationToken);

    /// <summary>
    /// Locks the items of the projects the team owns <c>FOR UPDATE</c>, in id order. A save of an item locks it first, so the two
    /// serialize; a creation of a child beneath one of them holds it <c>FOR NO KEY UPDATE</c> and is waited for. Taken after the
    /// team's and the links' locks.
    /// </summary>
    Task LockOwnedAsync(IReadOnlyCollection<Guid> projectIds, Guid teamId, CancellationToken cancellationToken);

    /// <summary>
    /// Gives what the team owns in <paramref name="projectIds"/> when the statement runs — items the lock found and items
    /// committed since, a child created beneath one of them included — to <paramref name="destinationTeamId"/>, or to nobody for
    /// <c>null</c> («Без команды»): the owning team changes and «Изменён» shows <paramref name="now"/>; nothing else of an item
    /// changes. Answers the number reassigned. The caller drops the team's places in those projects first (the place key is
    /// checked at the end of this statement).
    /// </summary>
    Task<int> ReassignOwnedAsync(
        IReadOnlyCollection<Guid> projectIds, Guid teamId, Guid? destinationTeamId, DateTimeOffset now, CancellationToken cancellationToken);
}

/// <summary>The number of items a team owns in one project.</summary>
public sealed record ProjectCount(Guid ProjectId, int Count);
