using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Organizations;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Features.Organizations.GetAreaDeletion;

/// <summary>The query of <c>GET /api/organizations/{organizationId}/areas/{areaId}/deletion</c>.</summary>
public sealed record GetAreaDeletionQuery(string? OrganizationId, string? AreaId);

/// <summary>A project whose link a deletion ends; a deactivated project's link is listed too.</summary>
public sealed record AreaDeletionProjectResponse(Guid Id, string Name, string Key, bool Active);

/// <summary>A team whose link a deletion ends.</summary>
public sealed record AreaDeletionTeamResponse(Guid Id, string Name);

/// <summary>What the deletion of the area ends (mod-018 api-contract § 1, <c>AreaDeletion</c>).</summary>
public sealed record GetAreaDeletionResult(
    IReadOnlyList<AreaDeletionProjectResponse> Projects,
    IReadOnlyList<AreaDeletionTeamResponse> Teams);

/// <summary>
/// <c>GET /api/organizations/{organizationId}/areas/{areaId}/deletion</c> (mod-018 api-contract § 2, BR-AW-9): the links a deletion
/// would end, for the dialog's confirmation. Every project link counts, the deactivated projects' too (their links are stored,
/// BR-AW-10); a deleted team has no link left. The area must exist: one 404 for a malformed, unknown or foreign id.
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

        var projects = await readModel.ListAllProjectsAsync(organizationId, areaId, cancellationToken);
        var teams = await readModel.ListTeamsAsync(organizationId, areaId, cancellationToken);

        return new GetAreaDeletionResult(
            projects.Select(row => new AreaDeletionProjectResponse(row.Id, row.Name, row.Key, row.IsActive)).ToList(),
            teams.Select(row => new AreaDeletionTeamResponse(row.Id, row.Name)).ToList());
    }
}
