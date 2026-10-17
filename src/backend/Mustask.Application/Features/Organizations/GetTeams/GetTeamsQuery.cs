namespace Mustask.Application.Features.Organizations.GetTeams;

/// <summary>
/// <c>GET /api/organizations/{organizationId}/projects/{projectId}/teams</c> (US-4, US-8: the project's active teams, by name; mod-017 BR-OT-11). Ids are the untrusted strings of the
/// address and the body: the handler parses them with <c>AddressedId</c>, so a
/// malformed id gets its level's not-found code, never a 400 (BR-M1-20).
/// </summary>
/// <param name="OrganizationId">The organization's id as it arrived in the address.</param>
/// <param name="ProjectId">The project's id as it arrived in the address.</param>
public sealed record GetTeamsQuery(string? OrganizationId, string? ProjectId);
