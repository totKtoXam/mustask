namespace Mustask.Application.Features.Organizations.GetDeactivatedOrganizations;

/// <summary>
/// <c>GET /api/organizations/deactivated</c> (US-2: the deactivated organizations in display order). Ids are the untrusted strings of the
/// address and the body: the handler parses them with <c>AddressedId</c>, so a
/// malformed id gets its level's not-found code, never a 400 (BR-M1-20).
/// </summary>
public sealed record GetDeactivatedOrganizationsQuery();
