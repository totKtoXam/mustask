namespace Mustask.Application.Features.Organizations.GetProject;

/// <summary>
/// <c>GET /api/organizations/{organizationId}/projects/{projectId}</c> (US-3: one reachable project). Ids are the untrusted strings of the
/// address and the body: the handler parses them with <c>AddressedId</c>, so a
/// malformed id gets its level's not-found code, never a 400 (BR-M1-20).
/// </summary>
/// <param name="OrganizationId">The organization's id as it arrived in the address.</param>
/// <param name="ProjectId">The project's id as it arrived in the address.</param>
public sealed record GetProjectQuery(string? OrganizationId, string? ProjectId);
