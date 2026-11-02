using Mustask.Domain.Organizations;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Features.Organizations.GetTeamAreas;

/// <summary>The query of <c>GET /api/organizations/{organizationId}/teams/{teamId}/areas</c>.</summary>
public sealed record GetTeamAreasQuery(string? OrganizationId, string? TeamId);

/// <summary>An area linked to the team: its name and its path (mod-018 api-contract § 1, <c>AreaRef</c>).</summary>
public sealed record TeamAreaResponse(Guid Id, string Name, string Path);

/// <summary>The handler's outcome: the team's areas.</summary>
public sealed record GetTeamAreasResult(IReadOnlyList<TeamAreaResponse> Items);

/// <summary>
/// <c>GET /api/organizations/{organizationId}/teams/{teamId}/areas</c> (mod-018 api-contract § 3, US-4): the areas the team is
/// linked to, by path in tree order. A deleted team is a 404.
/// </summary>
public sealed class GetTeamAreasHandler(IOrganizationStructure structure)
{
    public async Task<GetTeamAreasResult> Handle(GetTeamAreasQuery query, CancellationToken cancellationToken)
    {
        if (!AddressedId.TryParse(query.OrganizationId, out var organizationId))
        {
            throw new NotFoundException(OrganizationErrorCodes.OrganizationNotFound);
        }

        if (!AddressedId.TryParse(query.TeamId, out var teamId)
            || await structure.FindTeamAsync(organizationId, teamId, cancellationToken) is not { IsReachable: true })
        {
            throw new NotFoundException(OrganizationErrorCodes.TeamNotFound);
        }

        var areas = await structure.ListAreasOfTeamAsync(organizationId, teamId, cancellationToken);

        return new GetTeamAreasResult(areas.Select(area => new TeamAreaResponse(area.Id, area.Name, area.Path)).ToList());
    }
}
