using Mustask.Application.Abstractions;
using Mustask.Application.Abstractions.Organizations;
using Mustask.Application.Features.Boards;
using Mustask.Application.Features.WorkItems;
using Mustask.Domain.Organizations;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Features.Organizations.DeleteTeam;

/// <summary>
/// <c>DELETE /api/organizations/{organizationId}/teams/{teamId}</c> (feat-028 api-contract § 2.1; US-5, BR-OL-9, BR-OL-10; mod-017
/// US-7, BR-OT-12): a team is deleted, in one operation over what it owns in <b>every project it is linked to</b>, deactivated ones
/// included, and the items it owns go to «Без команды». There is no last-team guard any more: a project's last team may be deleted.
/// </summary>
/// <remarks>
/// <para>
/// <b>The order of answers</b> (api-contract § 0.5): the address from the outside in (organization, team); the request has no body,
/// so there are no values and no references to answer, and no rule refuses a deletion (no 409): then the operation and the save.
/// </para>
/// <para>
/// <b>Locks</b> (plan-backend § 7.2, D-3; ADR-0028 rule 8): the team <c>FOR NO KEY UPDATE</c> (step 2) and re-checked as active — every
/// team binder (a link, a member add, a rename, a reorder) holds it <c>FOR SHARE</c> —, then <b>all of the team's links</b>
/// <c>FOR NO KEY UPDATE</c> in project-id order (step 3) — every item binder (a save, a creation that names it or may default to it, a
/// default naming) holds the link <c>FOR SHARE</c>, and an unlink holds it <c>FOR NO KEY UPDATE</c> — so the two serialize: the writer
/// first, the deletion waits and takes what it wrote; the deletion first, the writer reads the team deleted and is refused
/// <c>team.not_found</c>. <c>NO KEY UPDATE</c> and not <c>UPDATE</c> because a child created beneath an item of this team inserts with
/// the owner key — a key share of the link, which <c>NO KEY UPDATE</c> does not wait for — while the deletion waits for that child's
/// parent (D-3). Then the owned items <c>FOR UPDATE</c> by id (step 5), the places, the reassignment, the settings, the links, the
/// memberships and the status. The project rows are written after every team and link row, never before.
/// </para>
/// <para>
/// <b>The operation</b> (api-contract § 2.1): the team's items get no owning team and show the operation's time as «Изменён»; the
/// team's backlog places are deleted (before the reassignment: the place key onto the item's owner is checked at the end of its
/// <c>UPDATE</c>); every default setting that named the team is cleared; every link of the team is removed; its memberships end
/// (mod-016 BR-M1-36, a person who is also in another team stays there); then the team is deleted — the name is free, and the address
/// answers <c>team.not_found</c>. Projects, areas and other teams' items are untouched. The reassignment is one statement over the
/// committed rows, so an item given to the team, or a child created beneath one of its items, after the confirmation's count is
/// reassigned all the same. Constant statements, whatever the number of items or of projects; all or nothing, within the transaction.
/// </para>
/// </remarks>
public sealed class DeleteTeamHandler(
    IProjectRepository projects,
    ITeamRepository teams,
    IProjectTeamRepository links,
    ITeamMembershipRepository teamMemberships,
    IWorkItemTeamOwnership ownership,
    ITeamBacklogPlaces places,
    IClock clock,
    IUnitOfWork unitOfWork)
{
    public async Task Handle(DeleteTeamCommand command, CancellationToken cancellationToken)
    {
        // The unit of work returns a result; this use case has none (204), so it returns a flag nobody reads.
        _ = await unitOfWork.ExecuteInTransactionAsync(
            async ct =>
            {
                // 1. The address, from the outermost level in. The scoped check has answered for the organization already; the
                // handler parses it again only to name its container in the next read.
                if (!AddressedId.TryParse(command.OrganizationId, out var organizationId))
                {
                    throw new NotFoundException(OrganizationErrorCodes.OrganizationNotFound);
                }

                // The team: locked as the object being deleted, then re-checked as active — a team another request deleted meanwhile
                // answers not found, whoever got the lock first (US-7 "deletes it again").
                var team = AddressedId.TryParse(command.TeamId, out var teamId)
                    ? await teams.LockForNoKeyUpdateAsync(organizationId, teamId, ct)
                    : null;

                if (team is not { IsActive: true })
                {
                    throw new NotFoundException(OrganizationErrorCodes.TeamNotFound);
                }

                // 2. The rule about the team alone: none. The last-team guard is retired (BR-OL-10).

                // 3. and 4. Values and references in the body: there is no body.

                // 5. The operation (BR-OL-9), in rule 8's order: the team's links locked (their project ids are the projects the
                // operation reaches, deactivated ones included), the owned items locked, the places dropped, the items reassigned to
                // nobody, the settings cleared, the links removed, the memberships ended.
                var linkedProjectIds = await links.LockTeamLinksAsync(organizationId, team.Id, ct);

                await ownership.LockOwnedAsync(linkedProjectIds, team.Id, ct);
                await places.DropForTeamAsync(team.Id, ct);
                await ownership.ReassignOwnedAsync(linkedProjectIds, team.Id, destinationTeamId: null, clock.UtcNow, ct);
                await projects.ClearDefaultTeamAsync(organizationId, team.Id, ct);
                await links.RemoveTeamLinksAsync(organizationId, team.Id, ct);
                await teamMemberships.EndAllAsync(team.Id, ct);

                team.Delete();

                // 6. Save. The name is freed by the status alone (the unique index covers active teams only), and no other unique
                // index is involved, so there is no race to map here.
                await teams.SaveAsync(ct);

                return true;
            },
            cancellationToken);
    }
}
