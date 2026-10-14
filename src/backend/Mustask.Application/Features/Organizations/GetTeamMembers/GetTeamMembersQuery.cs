namespace Mustask.Application.Features.Organizations.GetTeamMembers;

/// <summary>
/// <c>GET /api/organizations/{organizationId}/projects/{projectId}/teams/{teamId}/members</c> (api-contract § 2.1). The ids are
/// the untrusted strings of the address: the handler parses them with <c>AddressedId</c>, so a malformed id gets its level's
/// not-found code, never a 400.
/// </summary>
public sealed record GetTeamMembersQuery(string? OrganizationId, string? ProjectId, string? TeamId);
