namespace Mustask.Application.Features.WorkItems;

/// <summary>
/// The work items module's write contract for a team's deletion (mod-017, ADR-0028 rule 7): who owns the items a team owns is
/// the work items module's data, so the organizations module asks it to count, lock and release them in the caller's
/// transaction rather than writing <c>work_items</c> itself. It sits at the module's root, not inside a slice, so a slice of
/// another module can depend on it without referencing a slice of this one (docs/conventions/backend.md: modules talk through
/// explicit interfaces).
/// </summary>
/// <remarks>
/// Scoped; no access check and no event. The calls lock their own rows in ADR-0028 rule 8's order — the items after the teams,
/// by id — and join the caller's transaction. An item "owned by the team" is one the team owns in <b>any state</b>, Removed
/// included (BR-OT-12).
/// </remarks>
public interface IWorkItemTeamOwnership
{
    /// <summary>The number of items of the project the team owns now, in any state (the deletion's confirmation, api-contract § 2.9). One statement.</summary>
    Task<int> CountOwnedAsync(Guid projectId, Guid teamId, CancellationToken cancellationToken);

    /// <summary>
    /// Locks the items of the project the team owns <c>FOR UPDATE</c>, in id order. A save of an item locks it first, so the two
    /// serialize; a creation of a child beneath one of them holds it <c>FOR NO KEY UPDATE</c> and is waited for. Taken after the
    /// team's lock.
    /// </summary>
    Task LockOwnedAsync(Guid projectId, Guid teamId, CancellationToken cancellationToken);

    /// <summary>
    /// Releases what the team owns when the statement runs — items the lock found and items committed since, a child created
    /// beneath one of them included: the owning team becomes none and «Изменён» shows <paramref name="now"/>; nothing else of an
    /// item changes. Answers the number released. The caller drops the team's places first (the place key is checked at the end
    /// of this statement).
    /// </summary>
    Task<int> ReleaseOwnedAsync(Guid projectId, Guid teamId, DateTimeOffset now, CancellationToken cancellationToken);
}
