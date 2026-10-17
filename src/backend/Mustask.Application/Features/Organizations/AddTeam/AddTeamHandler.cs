using Mustask.Application.Abstractions;
using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Organizations;
using Mustask.Shared.Errors;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Features.Organizations.AddTeam;

/// <summary>
/// <c>POST /api/organizations/{organizationId}/projects/{projectId}/teams</c>
/// (mod-017 api-contract § 2.7; US-4 "A team's name is unique among the project's active teams", "Two teams of one name at
/// the same moment"; US-7 "The deleted team's name is free"). A new team is an ordinary one: a name, a status and no people
/// (BR-OT-11). It comes with no area and owns nothing (BR-OT-10) — the area of the same name that mod-002 created is gone.
/// </summary>
/// <remarks>
/// <para>
/// <b>The order of answers</b> (plan-backend § 5.3, api-contract § 0.4), one numbered step each below: the address from the
/// outside in; rules about the addressed object alone (none: the address ends at the project); the values in the body; the
/// references in the body (none: the team is new); the uniqueness pre-check; the save.
/// </para>
/// <para>
/// <b>Locks</b> (plan-backend § 7): none. The new row binds to nothing it must hold: the project's reachability is read, not
/// locked (ADR-0026 rule 8), and there is no root to hold <c>FOR SHARE</c> any more. Two teams of one name are closed by
/// <c>ix_teams_project_id_normalized_name</c>, and the loser gets <see cref="OrganizationErrorCodes.TeamNameTaken"/> from the
/// repository's constraint map (plan-backend § 6.3). A team added while a deletion holds the project's teams is not among
/// the rows the deletion locked: the deletion's guard counts the teams it locked, so a project that gains a team meanwhile
/// is refused its deletion of the then last team, never emptied (BR-OT-13).
/// </para>
/// </remarks>
public sealed class AddTeamHandler(
    IProjectRepository projects,
    ITeamRepository teams,
    IUnitOfWork unitOfWork)
{
    public async Task<AddTeamResult> Handle(AddTeamCommand command, CancellationToken cancellationToken) =>
        await unitOfWork.ExecuteInTransactionAsync(
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

                // 2. Rules about the addressed object alone: none, the address ends at the project.

                // 3. Values in the body: one ValidationException with every field reason.
                if (!EntityName.TryCreate(command.Name, out var name))
                {
                    throw new ValidationException(
                        OrganizationErrorCodes.ValidationFailed,
                        new Dictionary<string, IReadOnlyList<FieldError>> { ["name"] = [EntityName.LengthReason] });
                }

                // 4. References in the body: none, the team is new.

                // 5. The uniqueness pre-check. The team's name is unique among the project's active teams, so a deleted
                // team's name is free (BR-M1-1, ADR-0026 rule 6). A name an area holds is not in the way: a team has no area.
                if (await teams.IsActiveNameTakenAsync(projectId, name.Normalized, exceptId: null, ct))
                {
                    throw new ConflictException(OrganizationErrorCodes.TeamNameTaken);
                }

                var created = Team.Create(projectId, name);
                teams.Add(created);

                // 6. Save. A concurrent addition of the same name that committed first fails the insert on the team index,
                // and the repository answers it with the pre-check's code (plan-backend § 6.3).
                await teams.SaveAsync(ct);

                // The project had a team already (BR-OT-11), so a team just added is never its only one, and the settings
                // cannot name a team that did not exist: it carries no mark (api-contract § 1.3).
                return new AddTeamResult(new TeamResponse(created.Id, created.ProjectId, created.Name, DefaultMark: null));
            },
            cancellationToken);
}
