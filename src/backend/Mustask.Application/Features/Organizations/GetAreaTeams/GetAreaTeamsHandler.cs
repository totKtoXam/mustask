using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Organizations;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Features.Organizations.GetAreaTeams;

/// <summary>The query of <c>GET /api/organizations/{organizationId}/areas/{areaId}/teams</c>.</summary>
public sealed record GetAreaTeamsQuery(string? OrganizationId, string? AreaId);

/// <summary>A team linked to the area.</summary>
public sealed record AreaTeamResponse(Guid Id, string Name);

/// <summary>The handler's outcome: the area's teams.</summary>
public sealed record GetAreaTeamsResult(IReadOnlyList<AreaTeamResponse> Items);

/// <summary>
/// <c>GET /api/organizations/{organizationId}/areas/{areaId}/teams</c> (mod-018 api-contract § 3, US-4): the active teams linked
/// to this area itself, by name. A deleted team's link ended with it.
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

        return new GetAreaTeamsResult(rows.Select(row => new AreaTeamResponse(row.Id, row.Name)).ToList());
    }
}
