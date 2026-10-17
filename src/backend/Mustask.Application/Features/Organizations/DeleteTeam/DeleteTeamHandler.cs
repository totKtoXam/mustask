using Mustask.Application.Abstractions;
using Mustask.Application.Abstractions.Organizations;
using Mustask.Application.Features.Boards;
using Mustask.Application.Features.WorkItems;
using Mustask.Domain.Organizations;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Features.Organizations.DeleteTeam;

/// <summary>
/// <c>DELETE /api/organizations/{organizationId}/projects/{projectId}/teams/{teamId}</c>
/// (mod-017 US-7, BR-OT-11, BR-OT-12, BR-OT-13): a team is deleted, in one operation over what it owns, and the items it owns go
/// to «Без команды». The last team of a project is refused until feat-028.
/// </summary>
/// <remarks>
/// <para>
/// <b>The order of answers</b> (api-contract § 0.4): the address from the outside in (organization, project, team); then the
/// rule about the addressed team and its project (the last team, 409); the request has no body, so there are no values and no
/// references to answer; then the operation and the save.
/// </para>
/// <para>
/// <b>Locks</b> (plan-backend § 7, D-3, D-4; ADR-0028 rule 8): <b>every active team of the project</b> <c>FOR NO KEY UPDATE</c>,
/// in id order, first of all — every write that binds to a team (a save, a creation that names it or may default to it, a
/// default naming, a reorder) holds it <c>FOR SHARE</c>, so the two serialize: the binder first, the deletion waits and takes
/// what the binder wrote; the deletion first, the binder reads the team deleted and is refused <c>team_not_found</c>. Locking all
/// the teams in one order serializes two deletions that would empty the project: the second waits, re-reads, and is refused
/// <see cref="OrganizationErrorCodes.TeamLastTeamCannotBeDeleted"/>. <c>NO KEY UPDATE</c> and not <c>UPDATE</c> because a child
/// created beneath an item of this team inserts with the owner key — a key share of the team, which <c>NO KEY UPDATE</c> does not
/// wait for — while the deletion waits for that child's parent; <c>FOR UPDATE</c> would deadlock them. Then the owned items
/// <c>FOR UPDATE</c> by id, the places, the release, the setting, the memberships, the status. The project row is written after
/// every team row, never before (the default naming takes a team, then the project, in the same order).
/// </para>
/// <para>
/// <b>The operation</b> (api-contract § 2.7, BR-OT-12): the team's items get no owning team and show the operation's time as
/// «Изменён»; the team's backlog places are deleted (before the release: the place key onto the item's owner is checked at the
/// end of its <c>UPDATE</c>); a default setting that named the team is cleared; its memberships end (mod-016 BR-M1-36, a person
/// who is also in another team stays there); then the team is deleted — the name is free, and the address answers
/// <c>team_not_found</c>. Areas, projects and other teams' items are untouched. The release is one statement over the committed
/// rows, so an item given to the team, or a child created beneath one of its items, after the confirmation's count is reassigned
/// all the same: every write that can give an item this team holds a team of the project <c>FOR SHARE</c> (a creation with no
/// default holds them all), which the locks above wait for — it has committed when the owned items are locked, or it waits for
/// this deletion's commit and reads the team deleted. All or nothing, within the transaction.
/// </para>
/// </remarks>
public sealed class DeleteTeamHandler(
    IProjectRepository projects,
    ITeamRepository teams,
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
                // 1. The address, from the outermost level in. The scoped check has answered for the
                // organization already; the handler parses it again only to name its container in the next read.
                if (!AddressedId.TryParse(command.OrganizationId, out var organizationId))
                {
                    throw new NotFoundException(OrganizationErrorCodes.OrganizationNotFound);
                }

                if (!AddressedId.TryParse(command.ProjectId, out var projectId)
                    || !await projects.IsReachableAsync(organizationId, projectId, ct))
                {
                    throw new NotFoundException(OrganizationErrorCodes.ProjectNotFound);
                }

                // The team: every active team of the project is locked, and the addressed one must be among them — a team another
                // request deleted meanwhile answers not found, whoever got the lock first (US-7 "deletes it again").
                if (!AddressedId.TryParse(command.TeamId, out var teamId))
                {
                    throw new NotFoundException(OrganizationErrorCodes.TeamNotFound);
                }

                var activeTeams = await teams.LockProjectTeamsAsync(projectId, ct);
                var team = activeTeams.SingleOrDefault(candidate => candidate.Id == teamId)
                    ?? throw new NotFoundException(OrganizationErrorCodes.TeamNotFound);

                // 2. The rule about the team and its project: the last team is refused (BR-OT-11). The status change is only
                // tracked here, so a refusal has written nothing.
                team.Delete(activeTeams.Count);

                // 3. and 4. Values and references in the body: there is no body.

                // 5. The operation (BR-OT-12), in rule 8's order: the owned items locked, the places dropped, the items released,
                // the setting cleared, the memberships ended (under the team lock that every add to the team also takes).
                await ownership.LockOwnedAsync(projectId, team.Id, ct);
                await places.DropForTeamAsync(team.Id, ct);
                await ownership.ReleaseOwnedAsync(projectId, team.Id, clock.UtcNow, ct);
                await projects.ClearDefaultTeamAsync(projectId, team.Id, ct);
                await teamMemberships.EndAllAsync(team.Id, ct);

                // 6. Save. The name is freed by the status alone (the unique index covers active teams only), and no
                // other unique index is involved, so there is no race to map here.
                await teams.SaveAsync(ct);

                return true;
            },
            cancellationToken);
    }
}
