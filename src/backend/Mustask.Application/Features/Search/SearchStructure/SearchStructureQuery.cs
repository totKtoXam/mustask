namespace Mustask.Application.Features.Search.SearchStructure;

/// <summary>
/// <c>POST /api/organizations/{organizationId}/search</c> (feat-071 api-contract). The text travels in the body so it is never in
/// the request line (BR-GS-10); nothing here is logged.
/// </summary>
/// <param name="OrganizationId">The address's organization, as the route carries it.</param>
/// <param name="Query">The typed text.</param>
/// <param name="Expand">The group to expand to up to 20 rows, or <c>null</c> for the first read.</param>
public sealed record SearchStructureQuery(string? OrganizationId, string? Query, string? Expand);
