namespace Mustask.Application.Features.Organizations.GetTeam;

/// <summary>
/// <c>GET /api/organizations/{organizationId}/teams/{teamId}</c> (feat-028 api-contract § 2.1: one active team). Ids are the
/// untrusted strings of the address: the handler parses them with <c>AddressedId</c>, so a malformed id gets its level's
/// not-found code, never a 400 (BR-M1-20).
/// </summary>
/// <param name="OrganizationId">The organization's id as it arrived in the address.</param>
/// <param name="TeamId">The team's id as it arrived in the address.</param>
public sealed record GetTeamQuery(string? OrganizationId, string? TeamId);
