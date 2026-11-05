namespace Mustask.Application.Features.Organizations.GetMembers;

/// <summary>
/// <c>GET /api/organizations/{organizationId}/members</c> (api-contract § 1.2). The id is the untrusted string of the
/// address: the handler parses it with <c>AddressedId</c>, so a malformed id gets the not-found code, never a 400.
/// </summary>
public sealed record GetMembersQuery(string? OrganizationId);
