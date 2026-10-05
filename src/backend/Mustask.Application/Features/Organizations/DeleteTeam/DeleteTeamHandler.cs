using Mustask.Application.Abstractions;
using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Organizations;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Features.Organizations.DeleteTeam;

/// <summary>
/// <c>DELETE /api/organizations/{organizationId}/projects/{projectId}/teams/{teamId}</c>
/// (US-7: a team is deleted and its areas pass to the first team, BR-M1-18).
/// </summary>
/// <remarks>
/// <para>
/// <b>The order of answers</b> (plan-backend § 5.3, api-contract § 0.4): the
/// address from the outside in (organization, project, team); then the rule about
/// the addressed team alone (the first team is never deleted); the request has no
/// body, so there are no values and no references to answer; then the cascade and
/// the save.
/// </para>
/// <para>
/// <b>Locks</b> (plan-backend § 8.2): the team <c>FOR UPDATE</c>, then the
/// set-based re-own takes the row locks of the areas it changes. Every write that
/// binds to a team (a re-own, a new area with that owner, a default change) locks
/// it first and re-checks that it is <c>Active</c>, so the two serialize on this
/// one row (ADR-0026 rule 8): the deletion first, the binder answers
/// <see cref="OrganizationErrorCodes.TeamNotFound"/>; the binder first, the
/// deletion's re-own below passes the binder's area on to the first team, and
/// in no order does an area end up owned by a deleted team (US-7 "a re-own to a
/// team and that team's deletion at the same moment", NFR Concurrency). The
/// project is not locked: a write beneath only reads its containers' status
/// (ADR-0026 rule 8). The first team is not locked either, because it is never
/// deleted (BR-M1-18).
/// </para>
/// <para>
/// <b>The cascade</b> is one set-based statement, not a load of the team's areas:
/// it waits on any area another transaction holds and re-evaluates its condition
/// on the committed row, so an area re-owned, cleared or deleted meanwhile is
/// skipped and nothing this handler loaded can go stale (plan-backend § 8.2).
/// </para>
/// </remarks>
public sealed class DeleteTeamHandler(
    IProjectRepository projects,
    ITeamRepository teams,
    IUnitOfWork unitOfWork)
{
    public async Task Handle(DeleteTeamCommand command, CancellationToken cancellationToken)
    {
        // The unit of work returns a result; this use case has none (204), so it returns a flag nobody reads.
        _ = await unitOfWork.ExecuteInTransactionAsync(
            async ct =>
            {
                // 1. The address, from the outermost level in. The organization-context filter has answered for the
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

                // The team is locked FOR UPDATE and re-checked after the lock: a team another request deleted meanwhile
                // answers not found, whoever got the lock first (US-7 "deletes it again").
                if (!AddressedId.TryParse(command.TeamId, out var teamId))
                {
                    throw new NotFoundException(OrganizationErrorCodes.TeamNotFound);
                }

                var team = await teams.LockForUpdateAsync(projectId, teamId, ct);

                if (team is not { IsActive: true })
                {
                    throw new NotFoundException(OrganizationErrorCodes.TeamNotFound);
                }

                // 2. The rule about the addressed team alone: the first team, owner of the root, is refused (BR-M1-18).
                // The status change is only tracked here, so a refusal has written nothing.
                team.Delete();

                // 3. and 4. Values and references in the body: there is no body.

                // 5. The cascade (BR-M1-18): every area the team owns explicitly passes to the first team, which loses
                // nothing by it, and the area that was the team's default loses the mark (the first team's default stays
                // the root for good). Areas that only inherit follow their nearest explicit owner by themselves.
                var firstTeam = await teams.GetFirstTeamAsync(projectId, ct);
                await teams.ReassignExplicitAreasAsync(team.Id, firstTeam.Id, ct);

                // 6. Save. The name is freed by the status alone (the unique index covers active teams only), and no
                // other unique index is involved, so there is no race to map here.
                await teams.SaveAsync(ct);

                return true;
            },
            cancellationToken);
    }
}
