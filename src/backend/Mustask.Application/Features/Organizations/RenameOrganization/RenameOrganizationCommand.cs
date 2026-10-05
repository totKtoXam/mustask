namespace Mustask.Application.Features.Organizations.RenameOrganization;

/// <summary>
/// <c>PUT /api/organizations/{organizationId}/name</c> (US-2: rename an organization). Ids are the untrusted strings of the
/// address and the body: the handler parses them with <c>AddressedId</c>, so a
/// malformed id gets its level's not-found code, never a 400 (BR-M1-20).
/// </summary>
/// <param name="OrganizationId">The organization's id as it arrived in the address.</param>
/// <param name="Name">The name as sent, untrimmed.</param>
public sealed record RenameOrganizationCommand(string? OrganizationId, string? Name);
