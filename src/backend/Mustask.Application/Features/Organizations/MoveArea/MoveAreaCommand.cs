namespace Mustask.Application.Features.Organizations.MoveArea;

/// <summary>
/// <c>PUT /api/organizations/{organizationId}/projects/{projectId}/areas/{areaId}/parent</c> (US-5, US-6: move an area with its subtree). Ids are the untrusted strings of the
/// address and the body: the handler parses them with <c>AddressedId</c>, so a
/// malformed id gets its level's not-found code, never a 400 (BR-M1-20).
/// </summary>
/// <param name="OrganizationId">The organization's id as it arrived in the address.</param>
/// <param name="ProjectId">The project's id as it arrived in the address.</param>
/// <param name="AreaId">The area's id as it arrived in the address.</param>
/// <param name="ParentId">The new parent's id as sent in the body.</param>
public sealed record MoveAreaCommand(string? OrganizationId, string? ProjectId, string? AreaId, string? ParentId);
