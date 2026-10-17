using Mustask.Application.Abstractions;
using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Organizations;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Features.Organizations.SetDefaultTeam;

/// <summary>
/// <c>PUT /api/organizations/{organizationId}/projects/{projectId}/default-team</c> (mod-017 US-3, BR-OT-4): an administrator
/// names the team new items start on, or clears the setting. The setting decides a creation's starting value and nothing else —
/// no item moves, and no view, filter or order reads it. It survives a deactivated project and is cleared by the deletion of the
/// team it names.
/// </summary>
/// <remarks>
/// <para>
/// <b>The order of answers</b> (api-contract § 0.4): the address from the outside in (organization, project); then the team
/// (<see cref="ProjectTeams.EnsureBindable"/>, api-contract § 0.2): an unknown, malformed, foreign or deleted team, or one of a
/// deactivated project, is <see cref="OrganizationErrorCodes.TeamNotFound"/>, an active team of another active project
/// <see cref="OrganizationErrorCodes.TeamNotInProject"/>; clearing names no team and is never refused. The scoped check has
/// answered 401/404/403 before the handler runs (<c>organizations.team.write</c>: only an administrator).
/// </para>
/// <para>
/// <b>Locks</b> (plan-backend § 7, ADR-0028 rule 8): the team <c>FOR SHARE</c> — a binder — then the project row
/// <c>FOR NO KEY UPDATE</c> to write the setting, never the other way round, and the project's status is read again under that
/// lock (a deactivation holds it too). A deletion of the team either committed before the lock (the team reads deleted, 404) or
/// waits for this naming and clears the setting it wrote with the rest. Naming the current default again changes nothing.
/// </para>
/// </remarks>
public sealed class SetDefaultTeamHandler(
    IOrganizationStructure structure,
    IProjectRepository projects,
    ITeamRepository teams,
    IUnitOfWork unitOfWork)
{
    public async Task Handle(SetDefaultTeamCommand command, CancellationToken cancellationToken)
    {
        // The unit of work returns a result; this use case has none (204), so it returns a flag nobody reads.
        _ = await unitOfWork.ExecuteInTransactionAsync(
            async ct =>
            {
                // 1. The address, from the outermost level in.
                if (!AddressedId.TryParse(command.OrganizationId, out var organizationId))
                {
                    throw new NotFoundException(OrganizationErrorCodes.OrganizationNotFound);
                }

                if (!AddressedId.TryParse(command.ProjectId, out var projectId)
                    || !await projects.IsReachableAsync(organizationId, projectId, ct))
                {
                    throw new NotFoundException(OrganizationErrorCodes.ProjectNotFound);
                }

                // 2. The team, if one is named: locked as a binder, then judged on a fresh read.
                Guid? teamId = null;

                if (command.TeamId is not null)
                {
                    if (!AddressedId.TryParse(command.TeamId, out var parsed))
                    {
                        throw new NotFoundException(OrganizationErrorCodes.TeamNotFound);
                    }

                    // Only a team of this project locks: another project's team is refused on the read below either way.
                    _ = await teams.LockForShareAsync(projectId, parsed, ct);

                    var found = await structure.FindTeamAsync(parsed, ct);

                    ProjectTeams.EnsureBindable(
                        found is null ? null : new TeamFacts(found.Id, found.OrganizationId, found.ProjectId, found.Status, found.IsReachable),
                        organizationId,
                        projectId);

                    teamId = parsed;
                }

                // 3. The setting: the project row, after the team's, and its status again under the lock.
                var project = await projects.LockAsync(organizationId, projectId, ct);

                if (project is not { IsActive: true })
                {
                    throw new NotFoundException(OrganizationErrorCodes.ProjectNotFound);
                }

                if (project.NameDefaultTeam(teamId))
                {
                    await projects.SaveAsync(ct);
                }

                return true;
            },
            cancellationToken);
    }
}
