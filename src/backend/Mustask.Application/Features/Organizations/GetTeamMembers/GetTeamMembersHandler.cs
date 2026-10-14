using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Organizations;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Features.Organizations.GetTeamMembers;

/// <summary>
/// <c>GET /api/organizations/{organizationId}/projects/{projectId}/teams/{teamId}/members</c> (api-contract § 2.1; US-3 "A
/// team with no members says so", "An inactive or unknown team member stays and is marked", "A deleted team's memberships
/// end"): the team's members as people.
/// </summary>
/// <remarks>
/// <b>The order of answers</b> (api-contract § 0.1, § 2): the address from the outermost level in, each level through the
/// reachability specification (ADR-0026 rules 3 and 5): the organization, the project, the team. A deleted team, and a team
/// of another project or organization, answer <see cref="OrganizationErrorCodes.TeamNotFound"/> (BR-M1-36). Nothing else
/// can refuse a read. <b>Reads, not locks:</b> the memberships (one query), then the projection lookups in batches of 500;
/// an <c>inactive</c> or <c>unknown</c> member stays listed (BR-M1-17).
/// </remarks>
public sealed class GetTeamMembersHandler(IProjectReadModel projects, ITeamReadModel teams, IMemberReadModel members)
{
    public async Task<GetTeamMembersResult> Handle(GetTeamMembersQuery query, CancellationToken cancellationToken)
    {
        // 1. The address, from the outermost level in.
        if (!AddressedId.TryParse(query.OrganizationId, out var organizationId))
        {
            throw new NotFoundException(OrganizationErrorCodes.OrganizationNotFound);
        }

        if (!AddressedId.TryParse(query.ProjectId, out var projectId)
            || await projects.FindReachableAsync(organizationId, projectId, cancellationToken) is null)
        {
            throw new NotFoundException(OrganizationErrorCodes.ProjectNotFound);
        }

        var team = AddressedId.TryParse(query.TeamId, out var teamId)
            ? await teams.FindReachableAsync(organizationId, projectId, teamId, cancellationToken)
            : null;

        if (team is null)
        {
            throw new NotFoundException(OrganizationErrorCodes.TeamNotFound);
        }

        return new GetTeamMembersResult(await members.ListTeamMembersAsync(team.Id, cancellationToken));
    }
}
