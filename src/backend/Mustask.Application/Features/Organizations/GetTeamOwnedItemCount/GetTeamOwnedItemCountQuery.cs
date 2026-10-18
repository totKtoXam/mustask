namespace Mustask.Application.Features.Organizations.GetTeamOwnedItemCount;

/// <summary>
/// <c>GET /api/organizations/{organizationId}/projects/{projectId}/teams/{teamId}/unlink-count</c> (feat-028 api-contract § 2.4,
/// BR-OL-8; the earlier <c>…/owned-items/count</c> of mod-017 US-7 is retired): what unlinking the team from this project would
/// reassign — the items it owns in this project. Ids are the untrusted strings of the address: a malformed one gets its level's
/// not-found code, never a 400 (BR-M1-20).
/// </summary>
/// <param name="OrganizationId">The organization's id as it arrived in the address.</param>
/// <param name="ProjectId">The project's id as it arrived in the address.</param>
/// <param name="TeamId">The team's id as it arrived in the address.</param>
public sealed record GetTeamOwnedItemCountQuery(string? OrganizationId, string? ProjectId, string? TeamId);
