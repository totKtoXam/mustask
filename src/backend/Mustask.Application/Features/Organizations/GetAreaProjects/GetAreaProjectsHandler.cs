using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Organizations;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Features.Organizations.GetAreaProjects;

/// <summary>The query of <c>GET /api/organizations/{organizationId}/areas/{areaId}/projects</c>.</summary>
public sealed record GetAreaProjectsQuery(string? OrganizationId, string? AreaId);

/// <summary>A project linked to the area.</summary>
public sealed record AreaProjectResponse(Guid Id, string Name, string Key);

/// <summary>The handler's outcome: the area's active projects.</summary>
public sealed record GetAreaProjectsResult(IReadOnlyList<AreaProjectResponse> Items);

/// <summary>
/// <c>GET /api/organizations/{organizationId}/areas/{areaId}/projects</c> (mod-018 api-contract § 3, US-4): the active projects
/// linked to this area itself, by name (the descendants' are not included). A deactivated project is left out, its link stays.
/// </summary>
public sealed class GetAreaProjectsHandler(IAreaRepository areas, IAreaTreeReadModel readModel)
{
    public async Task<GetAreaProjectsResult> Handle(GetAreaProjectsQuery query, CancellationToken cancellationToken)
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

        var rows = await readModel.ListProjectsAsync(organizationId, areaId, cancellationToken);

        return new GetAreaProjectsResult(rows.Select(row => new AreaProjectResponse(row.Id, row.Name, row.Key)).ToList());
    }
}
