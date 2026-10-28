using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Organizations;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Features.Organizations.GetArea;

/// <summary>The query of <c>GET /api/organizations/{organizationId}/areas/{areaId}</c>.</summary>
public sealed record GetAreaQuery(string? OrganizationId, string? AreaId);

/// <summary>The area, its parent and its direct children (mod-018 api-contract § 1, <c>AreaPage</c>).</summary>
public sealed record AreaPageResponse(
    Guid Id,
    Guid? ParentId,
    string Type,
    Guid? ProjectId,
    bool? ProjectActive,
    string Name,
    string? Description,
    string Path,
    AreaRefResponse? Parent,
    IReadOnlyList<AreaChildResponse> Children);

/// <summary>The area's parent: its name and its path.</summary>
public sealed record AreaRefResponse(Guid Id, string Name, string Path);

/// <summary>A direct child of the area.</summary>
public sealed record AreaChildResponse(Guid Id, string Type, string Name, string? Description);

/// <summary>The handler's outcome: the page.</summary>
public sealed record GetAreaResult(AreaPageResponse Response);

/// <summary>
/// <c>GET /api/organizations/{organizationId}/areas/{areaId}</c> (mod-018 api-contract § 2, US-3): one area with its path, its
/// parent and its direct children by name. A malformed, unknown or foreign id is one 404 (BR-AW-1). The organization's rows come in
/// one statement, the same as the tree's; the page is cut from them.
/// </summary>
public sealed class GetAreaHandler(IAreaTreeReadModel readModel)
{
    public async Task<GetAreaResult> Handle(GetAreaQuery query, CancellationToken cancellationToken)
    {
        if (!AddressedId.TryParse(query.OrganizationId, out var organizationId))
        {
            throw new NotFoundException(OrganizationErrorCodes.OrganizationNotFound);
        }

        var rows = await readModel.GetRowsAsync(organizationId, cancellationToken);
        var byId = rows.ToDictionary(row => row.Id);

        if (!AddressedId.TryParse(query.AreaId, out var areaId) || !byId.TryGetValue(areaId, out var row))
        {
            throw new NotFoundException(OrganizationErrorCodes.AreaNotFound);
        }

        var tree = AreaRequests.TreeOf(rows);
        var parent = row.ParentId is { } parentId
            ? new AreaRefResponse(parentId, byId[parentId].Name, tree.PathOf(parentId))
            : null;
        var children = tree.ChildrenOf(areaId)
            .Select(child => new AreaChildResponse(child.Id, AreaRequests.TypeName(child.Type), child.Name, byId[child.Id].Description))
            .ToList();

        return new GetAreaResult(new AreaPageResponse(
                row.Id,
                row.ParentId,
                AreaRequests.TypeName(row.Type),
                row.ProjectId,
                row.ProjectActive,
                row.Name,
                row.Description,
                tree.PathOf(areaId),
                parent,
                children));
    }
}
