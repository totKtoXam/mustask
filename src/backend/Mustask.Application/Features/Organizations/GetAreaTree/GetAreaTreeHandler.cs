using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Organizations;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Features.Organizations.GetAreaTree;

/// <summary>The query of <c>GET /api/organizations/{organizationId}/areas</c>.</summary>
public sealed record GetAreaTreeQuery(string? OrganizationId);

/// <summary>One area of the tree (mod-018 api-contract § 1): <paramref name="Depth"/> is 1 for a top-level area.</summary>
public sealed record AreaNodeResponse(Guid Id, Guid? ParentId, string Name, string? Description, string Path, int Depth);

/// <summary>The handler's outcome: the whole tree, pre-order.</summary>
public sealed record GetAreaTreeResult(IReadOnlyList<AreaNodeResponse> Items);

/// <summary>
/// <c>GET /api/organizations/{organizationId}/areas</c> (mod-018 api-contract § 2, US-1): the organization's whole area tree,
/// pre-order, siblings by name in Russian collation. One statement for the rows; the pre-order, the levels and the paths are
/// assembled from them by <see cref="AreaTree"/>, so the number of statements does not grow with the tree (NFR Query shape).
/// </summary>
public sealed class GetAreaTreeHandler(IAreaTreeReadModel readModel)
{
    public async Task<GetAreaTreeResult> Handle(GetAreaTreeQuery query, CancellationToken cancellationToken)
    {
        if (!AddressedId.TryParse(query.OrganizationId, out var organizationId))
        {
            throw new NotFoundException(OrganizationErrorCodes.OrganizationNotFound);
        }

        var rows = await readModel.GetRowsAsync(organizationId, cancellationToken);
        var byId = rows.ToDictionary(row => row.Id);
        var tree = AreaRequests.TreeOf(rows);

        var items = tree.PreOrder()
            .Select(node => new AreaNodeResponse(
                node.Id, node.ParentId, node.Name, byId[node.Id].Description, tree.PathOf(node.Id), tree.LevelOf(node.Id)))
            .ToList();

        return new GetAreaTreeResult(items);
    }
}
