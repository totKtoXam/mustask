using Mustask.Domain.Organizations;
using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Identity;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Features.Organizations.GetWritableAreaIds;

/// <summary>The query of <c>GET /api/organizations/{organizationId}/areas/writable-ids</c>.</summary>
public sealed record GetWritableAreaIdsQuery(string? OrganizationId);

/// <summary>
/// <c>GET …/areas/writable-ids</c> (mod-019 api-contract § 5, US-4): the ids of the nodes where the caller holds
/// <c>organizations.area.write</c>, computed once from the set form over the one tree read — the panel shows the tree's «Добавить» and
/// move targets from it. A sibling read, because the tree is a bare array that the panel already consumes.
/// </summary>
public sealed class GetWritableAreaIdsHandler(IAreaTreeReadModel readModel, IOrganizationAccess access)
{
    public async Task<IReadOnlyList<Guid>> Handle(GetWritableAreaIdsQuery query, CancellationToken cancellationToken)
    {
        if (!AddressedId.TryParse(query.OrganizationId, out var organizationId))
        {
            throw new NotFoundException(OrganizationErrorCodes.OrganizationNotFound);
        }

        var rows = await readModel.GetRowsAsync(organizationId, cancellationToken);
        var set = await access.SetAsync(cancellationToken);

        return rows.Where(row => set.Holds(PermissionCatalog.OrganizationsAreaWrite, row.Id)).Select(row => row.Id).ToList();
    }
}
