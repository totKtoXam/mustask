namespace Mustask.Application.Features.Organizations.RestoreOrganization;

/// <summary>
/// <c>POST /api/organizations/{organizationId}/restore</c> (US-2: restore an organization exactly as it was). Ids are the untrusted strings of the
/// address and the body: the handler parses them with <c>AddressedId</c>, so a
/// malformed id gets its level's not-found code, never a 400 (BR-M1-20).
/// </summary>
/// <param name="OrganizationId">The organization's id as it arrived in the address.</param>
public sealed record RestoreOrganizationCommand(string? OrganizationId);
