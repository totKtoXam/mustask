using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Organizations;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Features.Organizations.GetAreaTeams;

/// <summary>The query of <c>GET /api/organizations/{organizationId}/areas/{areaId}/teams</c>.</summary>
public sealed record GetAreaTeamsQuery(string? OrganizationId, string? AreaId);

/// <summary>A team standing in the department (its place, doc-016 BR-TD-1), with its colour (BR-TD-20).</summary>
public sealed record AreaTeamResponse(Guid Id, string Name, string Color);

/// <summary>The handler's outcome: the area's teams.</summary>
public sealed record GetAreaTeamsResult(IReadOnlyList<AreaTeamResponse> Items);

/// <summary>
/// <c>GET /api/organizations/{organizationId}/areas/{areaId}/teams</c> (mod-018 api-contract § 3, US-4): the active teams standing
/// in this department (the areaâteam links of mod-018 are retired), by name. A deleted team keeps no department.
/// </summary>
public sealed class GetAreaTeamsHandler(IAreaRepository areas, IAreaTreeReadModel readModel)
{
    public async Task<GetAreaTeamsResult> Handle(GetAreaTeamsQuery query, CancellationToken cancellationToken)
    {
        if (!AddressedId.TryParse(query.OrganizationId, out var organizationId))
        {
            throw new NotFoundException(OrganizationErrorCodes.OrganizationNotFound);
        }

        if (!AddressedId.TryParse(query.AreaId, out var areaId)
            || await areas.FindAsync(organizationId, areaId, cancellationToken) is null)
        {
            throw new NotFoundException(OrganizationErrorCodes.AreaNotFound);
        }

        var rows = await readModel.ListTeamsAsync(organizationId, areaId, cancellationToken);
        return new GetAreaTeamsResult(rows.Select(row => new AreaTeamResponse(row.Id, row.Name, row.Color)).ToList());
    }
}
