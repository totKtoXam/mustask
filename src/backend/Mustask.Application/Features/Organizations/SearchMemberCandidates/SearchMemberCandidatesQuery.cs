namespace Mustask.Application.Features.Organizations.SearchMemberCandidates;

/// <summary>
/// <c>GET /api/organizations/{organizationId}/member-candidates?text=</c> (api-contract § 1.3).
/// </summary>
/// <param name="OrganizationId">The organization's id as it arrived in the address.</param>
/// <param name="Text">The typed text; <c>null</c> when the query string has none.</param>
public sealed record SearchMemberCandidatesQuery(string? OrganizationId, string? Text);
