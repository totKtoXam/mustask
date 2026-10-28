using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Organizations;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Features.Organizations.GetAreaDeletion;

/// <summary>The query of <c>GET /api/organizations/{organizationId}/areas/{areaId}/deletion</c>.</summary>
public sealed record GetAreaDeletionQuery(string? OrganizationId, string? AreaId);

/// <summary>A team whose link a deletion ends.</summary>
public sealed record AreaDeletionTeamResponse(Guid Id, string Name);

/// <summary>What the deletion of the node ends (mod-018 api-contract § 1, <c>AreaDeletion</c>): its team links (a department's).</summary>
public sealed record GetAreaDeletionResult(IReadOnlyList<AreaDeletionTeamResponse> Teams);

/// <summary>
/// <c>GET /api/organizations/{organizationId}/areas/{areaId}/deletion</c> (mod-018 api-contract § 2, BR-AW-9): the team links a
/// deletion would end, for the dialog's confirmation. A deleted team has no link left. The node must exist: one 404 for a malformed,
/// unknown or foreign id. Any other type than a department has no link of this kind, so the list is empty.
/// </summary>
public sealed class GetAreaDeletionHandler(IAreaRepository areas, IAreaTreeReadModel readModel)
{
    public async Task<GetAreaDeletionResult> Handle(GetAreaDeletionQuery query, CancellationToken cancellationToken)
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

        var teams = await readModel.ListTeamsAsync(organizationId, areaId, cancellationToken);

        return new GetAreaDeletionResult(teams.Select(row => new AreaDeletionTeamResponse(row.Id, row.Name)).ToList());
    }
}
