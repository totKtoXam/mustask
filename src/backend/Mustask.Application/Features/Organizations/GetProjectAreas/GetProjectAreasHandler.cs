using Mustask.Domain.Organizations;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Features.Organizations.GetProjectAreas;

/// <summary>The query of <c>GET /api/organizations/{organizationId}/projects/{projectId}/areas</c>.</summary>
public sealed record GetProjectAreasQuery(string? OrganizationId, string? ProjectId);

/// <summary>An area linked to the project: its name and its path (mod-018 api-contract § 1, <c>AreaRef</c>).</summary>
public sealed record ProjectAreaResponse(Guid Id, string Name, string Path);

/// <summary>The handler's outcome: the project's areas.</summary>
public sealed record GetProjectAreasResult(IReadOnlyList<ProjectAreaResponse> Items);

/// <summary>
/// <c>GET /api/organizations/{organizationId}/projects/{projectId}/areas</c> (mod-018 api-contract § 3, US-4): the areas the project
/// is linked to, by path in tree order. It replaces mod-002's project-nested tree read at this address. A deactivated project is a
/// 404, as everywhere (ADR-0026 rule 5); its links stay stored.
/// </summary>
public sealed class GetProjectAreasHandler(IOrganizationStructure structure)
{
    public async Task<GetProjectAreasResult> Handle(GetProjectAreasQuery query, CancellationToken cancellationToken)
    {
        if (!AddressedId.TryParse(query.OrganizationId, out var organizationId))
        {
            throw new NotFoundException(OrganizationErrorCodes.OrganizationNotFound);
        }

        if (!AddressedId.TryParse(query.ProjectId, out var projectId)
            || await structure.FindProjectAsync(organizationId, projectId, cancellationToken) is not { IsReachable: true })
        {
            throw new NotFoundException(OrganizationErrorCodes.ProjectNotFound);
        }

        var areas = await structure.ListAreasOfProjectAsync(organizationId, projectId, cancellationToken);

        return new GetProjectAreasResult(areas.Select(area => new ProjectAreaResponse(area.Id, area.Name, area.Path)).ToList());
    }
}
