using Mustask.Application.Abstractions;
using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Organizations;
using Mustask.Shared.Errors;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Features.Organizations.RenameTeam;

/// <summary>
/// <c>PUT /api/organizations/{organizationId}/projects/{projectId}/teams/{teamId}/name</c>
/// (US-4 "A team is renamed", and "A team's name is unique among the project's
/// active teams"). Follows the reference use case <c>CreateArea</c>
/// (plan-backend § 14.2).
/// </summary>
/// <remarks>
/// <para>
/// <b>The order of answers</b> (plan-backend § 5.3, api-contract § 0.4), one
/// numbered step each below: the address from the outside in, the team included;
/// rules about the addressed object alone (none: the first team may be renamed);
/// the name, every reason at once; references in the body (none); the
/// uniqueness pre-check; the save.
/// </para>
/// <para>
/// <b>Locks</b> (plan-backend § 8.2): the team <c>FOR NO KEY UPDATE</c>, taken
/// with the address, because the team is the object being written. A deletion of
/// the same team holds it <c>FOR UPDATE</c>, so the two serialize, and the check
/// of <c>Active</c> after the lock sees the outcome: a rename that comes second
/// answers <see cref="OrganizationErrorCodes.TeamNotFound"/> (ADR-0026 rule 8,
/// US-7). The project is not locked: a write beneath only reads its containers'
/// status. The team's areas are not touched, so none is locked (US-4: they keep
/// their names, ownership and default).
/// </para>
/// <para>
/// A rename is last-writer-wins (api-contract § 0.6): two renames of one team to
/// different free names both succeed. Two renames, or a rename and an addition,
/// to one name are closed by <c>ix_teams_project_id_normalized_name</c>, and the
/// loser's save answers with the pre-check's code (plan-backend § 6.3).
/// </para>
/// </remarks>
public sealed class RenameTeamHandler(
    IProjectRepository projects,
    ITeamRepository teams,
    IAreaRepository areas,
    IUnitOfWork unitOfWork)
{
    public async Task<RenameTeamResult> Handle(RenameTeamCommand command, CancellationToken cancellationToken) =>
        await unitOfWork.ExecuteInTransactionAsync(
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

                // The team: locked as the object being written, then re-checked as active. A team of another project, an
                // unknown one and a deleted one (a deletion that committed first included) answer alike (BR-M1-22, US-7).
                var team = AddressedId.TryParse(command.TeamId, out var teamId)
                    ? await teams.LockForNoKeyUpdateAsync(projectId, teamId, ct)
                    : null;

                if (team is not { IsActive: true })
                {
                    throw new NotFoundException(OrganizationErrorCodes.TeamNotFound);
                }

                // 2. Rules about the addressed object alone: none. The first team is renamed like any other.

                // 3. Values in the body: one ValidationException with every field reason.
                if (!EntityName.TryCreate(command.Name, out var name))
                {
                    throw new ValidationException(
                        OrganizationErrorCodes.ValidationFailed,
                        new Dictionary<string, IReadOnlyList<FieldError>> { ["name"] = [EntityName.LengthReason] });
                }

                // 4. References in the body: none.

                // 5. The uniqueness pre-check (BR-M1-1): among the project's active teams, ignoring case and surrounding
                // spaces, and never against the team's own current name, so «платформа» renames «Платформа».
                if (await teams.IsActiveNameTakenAsync(projectId, name.Normalized, exceptId: team.Id, ct))
                {
                    throw new ConflictException(OrganizationErrorCodes.TeamNameTaken);
                }

                team.Rename(name);

                // 6. Save. A rename or an addition to the same name that committed first fails this update on the team
                // index, and the repository answers it with the pre-check's code (plan-backend § 6.3).
                await teams.SaveAsync(ct);

                return new RenameTeamResult(await ToResponseAsync(team, ct));
            },
            cancellationToken);

    /// <summary>
    /// The team as every team response shows it, with its default area's path
    /// resolved through <see cref="AreaTree"/>, the one definition of a path
    /// (DC-9). The areas are read, never changed: the rename leaves them alone.
    /// </summary>
    private async Task<TeamResponse> ToResponseAsync(Team team, CancellationToken cancellationToken)
    {
        var tree = new AreaTree(await areas.GetNodesAsync(team.ProjectId, cancellationToken));

        // Every active team has exactly one default area (BR-M1-14); a missing one is a defect, not a client's fault.
        var defaultArea = tree.PreOrder().FirstOrDefault(node => node.DefaultOfTeamId == team.Id)
            ?? throw new InvalidOperationException($"The active team {team.Id} has no default area.");

        return new TeamResponse(
            team.Id,
            team.ProjectId,
            team.Name,
            team.IsFirstTeam,
            new TeamDefaultAreaResponse(defaultArea.Id, tree.PathOf(defaultArea.Id)));
    }
}
