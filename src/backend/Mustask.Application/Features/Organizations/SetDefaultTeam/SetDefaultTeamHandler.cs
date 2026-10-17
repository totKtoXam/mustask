using Mustask.Application.Abstractions;
using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Organizations;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Features.Organizations.SetDefaultTeam;

/// <summary>
/// <c>PUT /api/organizations/{organizationId}/projects/{projectId}/default-team</c> (mod-017 US-3, BR-OT-4; feat-028 BR-OL-7,
/// US-3 "the default must be linked"): an administrator names the team new items start on, or clears the setting. The setting
/// decides a creation's starting value and nothing else — no item moves, and no view, filter or order reads it. It survives a
/// deactivated project and is cleared by the unlink and the deletion of the team it names.
/// </summary>
/// <remarks>
/// <para>
/// <b>The order of answers</b> (feat-028 api-contract § 0.5): the address from the outside in (organization, project); then the team
/// (<see cref="ProjectTeams.EnsureBindable"/>, api-contract § 0.3): an unknown, malformed, foreign or deleted team is
/// <see cref="OrganizationErrorCodes.TeamNotFound"/>, an active team of the organization that is not linked to the project
/// <see cref="OrganizationErrorCodes.TeamNotInProject"/>; clearing names no team and is never refused. The scoped check has
/// answered 401/404/403 before the handler runs (<c>organizations.project.write</c>: only an administrator; mod-017 declared
/// <c>team.write</c>, feat-028 moved the setting to the project's own code).
/// </para>
/// <para>
/// <b>Locks</b> (plan-backend § 7, ADR-0028 rule 8): the <b>link</b> <c>FOR SHARE</c> — an item binder — then the project row
/// <c>FOR NO KEY UPDATE</c> to write the setting, never the other way round, and the project's status is read again under that
/// lock (a deactivation holds it too). An unlink or a deletion of the team either committed before the lock (the team reads not
/// linked, 409, or deleted, 404) or waits for this naming and clears the setting it wrote with the rest. Naming the current default
/// again changes nothing.
/// </para>
/// </remarks>
public sealed class SetDefaultTeamHandler(
    IOrganizationStructure structure,
    IProjectRepository projects,
    IProjectTeamRepository links,
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

                // 2. The team, if one is named: its link locked as a binder, then judged on a fresh read.
                Guid? teamId = null;

                if (command.TeamId is not null)
                {
                    if (!AddressedId.TryParse(command.TeamId, out var parsed))
                    {
                        throw new NotFoundException(OrganizationErrorCodes.TeamNotFound);
                    }

                    _ = await links.LockLinkForShareAsync(organizationId, projectId, parsed, ct);

                    var found = await structure.FindProjectTeamAsync(organizationId, projectId, parsed, ct);

                    ProjectTeams.EnsureBindable(
                        found is null ? null : new TeamFacts(found.Team.Id, found.Team.OrganizationId, found.Team.Status, found.IsLinked),
                        organizationId);

                    teamId = parsed;
                }

                // 3. The setting: the project row, after the link's, and its status again under the lock.
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
