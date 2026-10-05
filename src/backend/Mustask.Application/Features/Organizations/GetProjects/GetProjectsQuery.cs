namespace Mustask.Application.Features.Organizations.GetProjects;

/// <summary>
/// <c>GET /api/organizations/{organizationId}/projects</c> (US-3, US-8: the organization's active projects in display order). Ids are the untrusted strings of the
/// address and the body: the handler parses them with <c>AddressedId</c>, so a
/// malformed id gets its level's not-found code, never a 400 (BR-M1-20).
/// </summary>
/// <param name="OrganizationId">The organization's id as it arrived in the address.</param>
public sealed record GetProjectsQuery(string? OrganizationId);
