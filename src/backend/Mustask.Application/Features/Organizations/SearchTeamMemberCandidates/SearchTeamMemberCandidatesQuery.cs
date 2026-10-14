namespace Mustask.Application.Features.Organizations.SearchTeamMemberCandidates;

/// <summary>
/// <c>GET /api/organizations/{organizationId}/projects/{projectId}/teams/{teamId}/member-candidates?text=</c> (api-contract
/// § 2.2). The ids are the untrusted strings of the address.
/// </summary>
/// <param name="Text">The typed text; <c>null</c> when the query string has none.</param>
public sealed record SearchTeamMemberCandidatesQuery(string? OrganizationId, string? ProjectId, string? TeamId, string? Text);
