namespace Mustask.Application.Features.Organizations.SetAreaOwner;

/// <summary>
/// <c>PUT /api/organizations/{organizationId}/projects/{projectId}/areas/{areaId}/owner</c> (US-6: set or clear an area's explicit owner). Ids are the untrusted strings of the
/// address and the body: the handler parses them with <c>AddressedId</c>, so a
/// malformed id gets its level's not-found code, never a 400 (BR-M1-20).
/// </summary>
/// <param name="OrganizationId">The organization's id as it arrived in the address.</param>
/// <param name="ProjectId">The project's id as it arrived in the address.</param>
/// <param name="AreaId">The area's id as it arrived in the address.</param>
/// <param name="OwnerTeamId">The owner's id as sent in the body; <c>null</c> clears it.</param>
public sealed record SetAreaOwnerCommand(string? OrganizationId, string? ProjectId, string? AreaId, string? OwnerTeamId);
