using Mustask.Application.Abstractions;
using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Organizations;
using Mustask.Shared.Errors;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Features.Organizations.RenameTeam;

/// <summary>
/// <c>PUT /api/organizations/{organizationId}/teams/{teamId}/name</c> (feat-028 api-contract § 2.1; US-1 "rename and description",
/// BR-OL-3 "names are unique per organization"). Follows the reference use case <c>UnlinkTeam</c>'s shape: the address, the values,
/// the pre-check, the flush.
/// </summary>
/// <remarks>
/// <para>
/// <b>The order of answers</b> (api-contract § 0.5), one numbered step each below: the address from the outside in, the team
/// included; rules about the addressed object alone (none: any team may be renamed, BR-OT-11); the name, every reason at once;
/// references in the body (none); the uniqueness pre-check; the save.
/// </para>
/// <para>
/// <b>Locks</b> (plan-backend § 7.2): the team <c>FOR NO KEY UPDATE</c>, taken with the address, because the team is the object
/// being written. A deletion of the same team holds it <c>FOR NO KEY UPDATE</c> too, so the two serialize, and the check of
/// <c>Active</c> after the lock sees the outcome: a rename that comes second answers
/// <see cref="OrganizationErrorCodes.TeamNotFound"/> (ADR-0026 rule 8). No project is locked or read: a team belongs to the
/// organization, and what it owns, its people and its links do not change with its name (api-contract § 2.1). The projects show
/// the new name from their next read.
/// </para>
/// <para>
/// A rename is last-writer-wins: two renames of one team to different free names both succeed. Two renames, or a rename and a
/// creation, to one name are closed by <c>ix_teams_organization_id_normalized_name</c>, and the loser's save answers with the
/// pre-check's code (plan-backend § 6.3).
/// </para>
/// </remarks>
public sealed class RenameTeamHandler(ITeamRepository teams, IUnitOfWork unitOfWork)
{
    public async Task<RenameTeamResult> Handle(RenameTeamCommand command, CancellationToken cancellationToken) =>
        await unitOfWork.ExecuteInTransactionAsync(
            async ct =>
            {
                // 1. The address, from the outermost level in. The scoped check has answered for the
                // organization already; the handler parses it again only to name its container in the next read.
                if (!AddressedId.TryParse(command.OrganizationId, out var organizationId))
                {
                    throw new NotFoundException(OrganizationErrorCodes.OrganizationNotFound);
                }

                // The team: locked as the object being written, then re-checked as active. A team of another organization, an
                // unknown one and a deleted one (a deletion that committed first included) answer alike (BR-OL-1, US-7).
                var team = AddressedId.TryParse(command.TeamId, out var teamId)
                    ? await teams.LockForNoKeyUpdateAsync(organizationId, teamId, ct)
                    : null;

                if (team is not { IsActive: true })
                {
                    throw new NotFoundException(OrganizationErrorCodes.TeamNotFound);
                }

                // 2. Rules about the addressed object alone: none. Any team is renamed, no team is the first (BR-OT-11).

                // 3. Values in the body: one ValidationException with every field reason.
                if (!EntityName.TryCreate(command.Name, out var name))
                {
                    throw new ValidationException(
                        OrganizationErrorCodes.ValidationFailed,
                        new Dictionary<string, IReadOnlyList<FieldError>> { ["name"] = [EntityName.LengthReason] });
                }

                // 4. References in the body: none.

                // 5. The uniqueness pre-check (BR-OL-3): among the organization's active teams, ignoring case and surrounding
                // spaces, and never against the team's own current name, so «платформа» renames «Платформа».
                if (await teams.IsActiveNameTakenAsync(organizationId, name.Normalized, exceptId: team.Id, ct))
                {
                    throw new ConflictException(OrganizationErrorCodes.TeamNameTaken);
                }

                team.Rename(name);

                // 6. Save. A rename or a creation to the same name that committed first fails this update on the team index, and
                // the repository answers it with the pre-check's code (plan-backend § 6.3).
                await teams.SaveAsync(ct);

                return new RenameTeamResult(new TeamResponse(team.Id, team.Name, team.Description));
            },
            cancellationToken);
}
