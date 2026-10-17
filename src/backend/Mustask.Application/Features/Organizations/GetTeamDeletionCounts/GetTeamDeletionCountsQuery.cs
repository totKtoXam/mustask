namespace Mustask.Application.Features.Organizations.GetTeamDeletionCounts;

/// <summary>
/// <c>GET /api/organizations/{organizationId}/teams/{teamId}/deletion-counts</c> (feat-028 api-contract § 1.5, § 2.1, US-5): what a
/// team's deletion would reassign, per project. Ids are the untrusted strings of the address: a malformed one gets its level's
/// not-found code, never a 400 (BR-M1-20).
/// </summary>
/// <param name="OrganizationId">The organization's id as it arrived in the address.</param>
/// <param name="TeamId">The team's id as it arrived in the address.</param>
public sealed record GetTeamDeletionCountsQuery(string? OrganizationId, string? TeamId);
