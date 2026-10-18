using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Organizations;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Features.Organizations.GetTeamMembers;

/// <summary>
/// <c>GET /api/organizations/{organizationId}/teams/{teamId}/members</c> (feat-028 api-contract § 2.3, mod-016 api-contract § 2.1;
/// US-3 "A team with no members says so", "An inactive or unknown team member stays and is marked", "A deleted team's memberships
/// end"): the team's members as people. The project left the address with the team's move to the organization (BR-OL-1).
/// </summary>
/// <remarks>
/// <b>The order of answers</b> (api-contract § 0.5): the address from the outermost level in, each level through the reachability
/// specification (ADR-0026 rules 3 and 5): the organization, then the team. A deleted team, and a team of another organization,
/// answer <see cref="OrganizationErrorCodes.TeamNotFound"/> (BR-M1-36). Nothing else can refuse a read. <b>Reads, not locks:</b> the
/// memberships (one query), then the projection lookups in batches of 500; an <c>inactive</c> or <c>unknown</c> member stays listed
/// (BR-M1-17).
/// </remarks>
public sealed class GetTeamMembersHandler(ITeamReadModel teams, IMemberReadModel members)
{
    public async Task<GetTeamMembersResult> Handle(GetTeamMembersQuery query, CancellationToken cancellationToken)
    {
        // 1. The address, from the outermost level in.
        if (!AddressedId.TryParse(query.OrganizationId, out var organizationId))
        {
            throw new NotFoundException(OrganizationErrorCodes.OrganizationNotFound);
        }

        var team = AddressedId.TryParse(query.TeamId, out var teamId)
            ? await teams.FindAsync(organizationId, teamId, cancellationToken)
            : null;

        if (team is null)
        {
            throw new NotFoundException(OrganizationErrorCodes.TeamNotFound);
        }

        return new GetTeamMembersResult(await members.ListTeamMembersAsync(team.Id, cancellationToken));
    }
}
