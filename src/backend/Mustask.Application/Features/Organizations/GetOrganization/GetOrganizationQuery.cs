namespace Mustask.Application.Features.Organizations.GetOrganization;

/// <summary>
/// <c>GET /api/organizations/{organizationId}</c> (US-2: one active organization). Ids are the untrusted strings of the
/// address and the body: the handler parses them with <c>AddressedId</c>, so a
/// malformed id gets its level's not-found code, never a 400 (BR-M1-20).
/// </summary>
/// <param name="OrganizationId">The organization's id as it arrived in the address.</param>
public sealed record GetOrganizationQuery(string? OrganizationId);
