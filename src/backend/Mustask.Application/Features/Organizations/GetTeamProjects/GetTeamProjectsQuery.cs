namespace Mustask.Application.Features.Organizations.GetTeamProjects;

/// <summary>
/// <c>GET /api/organizations/{organizationId}/teams/{teamId}/projects</c> (feat-028 api-contract § 2.2: a team's active projects).
/// Ids are the untrusted strings of the address: a malformed one gets its level's not-found code, never a 400 (BR-M1-20).
/// </summary>
/// <param name="OrganizationId">The organization's id as it arrived in the address.</param>
/// <param name="TeamId">The team's id as it arrived in the address.</param>
public sealed record GetTeamProjectsQuery(string? OrganizationId, string? TeamId);
