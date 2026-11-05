namespace Mustask.Application.Features.Boards;

/// <summary>
/// The team backlog places that other modules have to drop (mod-017, feat-028, ADR-0028 rules 7 and 9). A team place is the boards
/// module's table and a place names the team that owns its item, so a write of another module that changes the owner — a
/// work item's save, a team's unlink or deletion — asks the boards module to drop the place, in the caller's transaction, rather than
/// writing <c>backlog_places</c> itself. It sits at the module's root, not inside a slice, so a slice of another module can
/// depend on it without referencing a slice of this one (docs/conventions/backend.md: modules talk through explicit
/// interfaces).
/// </summary>
/// <remarks>
/// Scoped; no access check and no event. Each call is one statement that waits on a place row another transaction holds and
/// re-evaluates on the committed row, so a reorder that wrote a place a moment ago is never missed.
/// </remarks>
public interface ITeamBacklogPlaces
{
    /// <summary>
    /// Deletes the item's team place, if it has one (BR-OT-6): a save that changes the owning team calls it <b>before</b> the
    /// item's <c>UPDATE</c>, because the place's key onto the item's current owner is checked at the end of that statement.
    /// </summary>
    Task DropForItemAsync(Guid workItemId, CancellationToken cancellationToken);

    /// <summary>Deletes every place of the team, in every project (a team's deletion, feat-028 BR-OL-9).</summary>
    Task DropForTeamAsync(Guid teamId, CancellationToken cancellationToken);

    /// <summary>
    /// Deletes the places of the team on the items of one project (an unlink, feat-028 BR-OL-8): the team's places in the other
    /// projects it is linked to stay. Called <b>before</b> the items are reassigned (the place key onto the item's owner).
    /// </summary>
    Task DropForTeamInProjectAsync(Guid teamId, Guid projectId, CancellationToken cancellationToken);
}
