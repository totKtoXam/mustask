namespace Mustask.Application.Features.Organizations.DeleteArea;

/// <summary>
/// <c>DELETE /api/organizations/{organizationId}/projects/{projectId}/areas/{areaId}</c> (US-5, US-6: delete an empty leaf (204)). Ids are the untrusted strings of the
/// address and the body: the handler parses them with <c>AddressedId</c>, so a
/// malformed id gets its level's not-found code, never a 400 (BR-M1-20).
/// </summary>
/// <param name="OrganizationId">The organization's id as it arrived in the address.</param>
/// <param name="ProjectId">The project's id as it arrived in the address.</param>
/// <param name="AreaId">The area's id as it arrived in the address.</param>
public sealed record DeleteAreaCommand(string? OrganizationId, string? ProjectId, string? AreaId);
