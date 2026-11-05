namespace Mustask.Application.Features.Organizations.SearchTeamMemberCandidates;

/// <summary>
/// <c>GET /api/organizations/{organizationId}/teams/member-candidates?text=</c> (feat-028 api-contract § 2.3a): the search of the
/// creation form's «Участники», where the team does not exist yet. The id is the untrusted string of the address.
/// </summary>
/// <param name="Text">The typed text; <c>null</c> when the query string has none.</param>
public sealed record SearchNewTeamMemberCandidatesQuery(string? OrganizationId, string? Text);
