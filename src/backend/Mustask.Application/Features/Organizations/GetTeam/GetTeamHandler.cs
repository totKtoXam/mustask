using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Organizations;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Features.Organizations.GetTeam;

/// <summary>
/// <c>GET /api/organizations/{organizationId}/teams/{teamId}</c> (feat-028 api-contract § 2.1; US-1 "a team page does not depend
/// on a project", US-8 "a foreign id answers as one that names nothing"). One active team of the organization.
/// </summary>
/// <remarks>
/// <b>The order of answers</b> (api-contract § 0.5): the organization, then the team, each through the reachability
/// specification (ADR-0026 rules 3 and 5); a deleted team, a team of another organization and a malformed id all answer
/// <see cref="OrganizationErrorCodes.TeamNotFound"/>. There is nothing else to refuse. <b>No locks and no unit of work:</b> a read
/// takes the committed rows of one statement. A team whose only projects are deactivated is still read: it belongs to the
/// organization (BR-OL-1).
/// </remarks>
public sealed class GetTeamHandler(ITeamReadModel teams)
{
    public async Task<GetTeamResult> Handle(GetTeamQuery query, CancellationToken cancellationToken)
    {
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

        return new GetTeamResult(new TeamResponse(team.Id, team.Name, team.Description));
    }
}
