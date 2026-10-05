namespace Mustask.Application.Features.Organizations.GetOrganizations;

/// <summary>
/// <c>GET /api/organizations</c> (US-1, US-8: the active organizations in display order). Ids are the untrusted strings of the
/// address and the body: the handler parses them with <c>AddressedId</c>, so a
/// malformed id gets its level's not-found code, never a 400 (BR-M1-20).
/// </summary>
public sealed record GetOrganizationsQuery();
