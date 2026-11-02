using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Organizations;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Features.Organizations.GetProjectAreas;

/// <summary>The query of <c>GET /api/organizations/{organizationId}/projects/{projectId}/areas</c>.</summary>
public sealed record GetProjectAreasQuery(string? OrganizationId, string? ProjectId);

/// <summary>One node of the project's subtree (mod-018 api-contract § 1, <c>AreaNode</c>): <paramref name="Depth"/> is its level in the organization's tree.</summary>
public sealed record ProjectAreaResponse(
    Guid Id,
    Guid? ParentId,
    string Type,
    Guid? ProjectId,
    bool? ProjectActive,
    string Name,
    string? Description,
    string Path,
    int Depth);

/// <summary>The handler's outcome: the project's node and its sub-areas, pre-order.</summary>
public sealed record GetProjectAreasResult(IReadOnlyList<ProjectAreaResponse> Items);

/// <summary>
/// <c>GET /api/organizations/{organizationId}/projects/{projectId}/areas</c> (mod-018 api-contract § 3, BR-AW-12): the project's own
/// tree — its node and the sub-areas below it, as the organization's tree shows them (paths from the top level). The project's page
/// «Области» and the work item's «Область» offer read it. A deactivated project is a 404, as everywhere (ADR-0026 rule 5); its node
/// stays in the organization's tree, marked.
/// </summary>
public sealed class GetProjectAreasHandler(IOrganizationStructure structure, IAreaTreeReadModel readModel)
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

        var rows = await readModel.GetRowsAsync(organizationId, cancellationToken);
        var byId = rows.ToDictionary(row => row.Id);
        var tree = AreaRequests.TreeOf(rows);

        var items = tree.PreOrder()
            .Where(node => node.ProjectId == projectId)
            .Select(node => new ProjectAreaResponse(
                node.Id,
                node.ParentId,
                AreaRequests.TypeName(node.Type),
                node.ProjectId,
                byId[node.Id].ProjectActive,
                node.Name,
                byId[node.Id].Description,
                tree.PathOf(node.Id),
                tree.LevelOf(node.Id)))
            .ToList();

        return new GetProjectAreasResult(items);
    }
}
