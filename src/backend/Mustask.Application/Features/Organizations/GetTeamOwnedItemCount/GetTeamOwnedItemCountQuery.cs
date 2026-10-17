namespace Mustask.Application.Features.Organizations.GetTeamOwnedItemCount;

/// <summary>
/// <c>GET /api/organizations/{organizationId}/projects/{projectId}/teams/{teamId}/owned-items/count</c> (mod-017 US-7,
/// api-contract § 2.9): what a team's deletion would reassign. Ids are the untrusted strings of the address: a malformed one
/// gets its level's not-found code, never a 400 (BR-M1-20).
/// </summary>
/// <param name="OrganizationId">The organization's id as it arrived in the address.</param>
/// <param name="ProjectId">The project's id as it arrived in the address.</param>
/// <param name="TeamId">The team's id as it arrived in the address.</param>
public sealed record GetTeamOwnedItemCountQuery(string? OrganizationId, string? ProjectId, string? TeamId);
