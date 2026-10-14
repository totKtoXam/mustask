using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Organizations;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Features.Organizations.RemoveTeamMember;

/// <summary>
/// <c>DELETE /api/organizations/{organizationId}/projects/{projectId}/teams/{teamId}/members/{userId}</c> (api-contract § 2.4;
/// US-3 "An administrator removes a team member", "Removing a team member who is already gone").
/// </summary>
/// <remarks>
/// <b>The order of answers</b> (api-contract § 0.1, § 2.4): the address from the outermost level in (the organization, the
/// project, the team), then the member in the path (<c>team.member_not_found</c>). The request has no body. <b>Locks:</b>
/// none — one <c>DELETE</c> of the membership row. The user stays in the organization with their role and still reads every
/// team (team membership grants no code). The team's deletion ends the memberships itself, so this delete against it
/// either removes the row first or finds it gone and answers not found — never a membership of a deleted team.
/// </remarks>
public sealed class RemoveTeamMemberHandler(
    IProjectReadModel projects,
    ITeamReadModel teams,
    ITeamMembershipRepository teamMemberships)
{
    public async Task Handle(RemoveTeamMemberCommand command, CancellationToken cancellationToken)
    {
        // 1. The address, from the outermost level in.
        if (!AddressedId.TryParse(command.OrganizationId, out var organizationId))
        {
            throw new NotFoundException(OrganizationErrorCodes.OrganizationNotFound);
        }

        if (!AddressedId.TryParse(command.ProjectId, out var projectId)
            || await projects.FindReachableAsync(organizationId, projectId, cancellationToken) is null)
        {
            throw new NotFoundException(OrganizationErrorCodes.ProjectNotFound);
        }

        var team = AddressedId.TryParse(command.TeamId, out var teamId)
            ? await teams.FindReachableAsync(organizationId, projectId, teamId, cancellationToken)
            : null;

        if (team is null)
        {
            throw new NotFoundException(OrganizationErrorCodes.TeamNotFound);
        }

        // The member in the path: a blank or over-long user id names no membership, like an unknown one (api-contract § 0.3).
        if (!OrganizationMembership.IsValidUserId(command.UserId)
            || !await teamMemberships.DeleteAsync(team.Id, command.UserId, cancellationToken))
        {
            throw new NotFoundException(OrganizationErrorCodes.TeamMemberNotFound);
        }
    }
}
