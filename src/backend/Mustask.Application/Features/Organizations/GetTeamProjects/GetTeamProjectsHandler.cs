using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Organizations;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Features.Organizations.GetTeamProjects;

/// <summary>
/// <c>GET /api/organizations/{organizationId}/teams/{teamId}/projects</c> (feat-028 api-contract § 2.2; US-3 "a link from either
/// page", "a deactivated project keeps its links"): the active projects a team is linked to, by name. A deactivated project keeps
/// its link and is left out of the list until it is restored (BR-OL-4, ADR-0026 rule 3).
/// </summary>
/// <remarks>
/// <b>The order of answers</b> (api-contract § 0.5): the organization, then the team (404 for a deleted team, a foreign one and a
/// malformed id alike). <b>Reads, not locks:</b> one statement for the team, one for the projects.
/// </remarks>
public sealed class GetTeamProjectsHandler(ITeamReadModel teams)
{
    public async Task<GetTeamProjectsResult> Handle(GetTeamProjectsQuery query, CancellationToken cancellationToken)
    {
        if (!AddressedId.TryParse(query.OrganizationId, out var organizationId))
        {
            throw new NotFoundException(OrganizationErrorCodes.OrganizationNotFound);
        }

        if (!AddressedId.TryParse(query.TeamId, out var teamId)
            || await teams.FindAsync(organizationId, teamId, cancellationToken) is null)
        {
            throw new NotFoundException(OrganizationErrorCodes.TeamNotFound);
        }

        var rows = await teams.ListProjectsOfTeamAsync(organizationId, teamId, cancellationToken);

        return new GetTeamProjectsResult(rows.Select(row => new TeamProjectResponse(row.Id, row.Name, row.Key)).ToList());
    }
}
