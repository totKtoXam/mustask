namespace Mustask.Application.Features.Organizations.GetAreaTree;

/// <summary>
/// <c>GET /api/organizations/{organizationId}/projects/{projectId}/areas</c> (US-5, US-6: the project's area tree in pre-order with resolved owners). Ids are the untrusted strings of the
/// address and the body: the handler parses them with <c>AddressedId</c>, so a
/// malformed id gets its level's not-found code, never a 400 (BR-M1-20).
/// </summary>
/// <param name="OrganizationId">The organization's id as it arrived in the address.</param>
/// <param name="ProjectId">The project's id as it arrived in the address.</param>
public sealed record GetAreaTreeQuery(string? OrganizationId, string? ProjectId);
