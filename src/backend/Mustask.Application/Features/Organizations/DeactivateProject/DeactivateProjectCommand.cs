namespace Mustask.Application.Features.Organizations.DeactivateProject;

/// <summary>
/// <c>POST /api/organizations/{organizationId}/projects/{projectId}/deactivate</c> (US-3: deactivate a project (204)). Ids are the untrusted strings of the
/// address and the body: the handler parses them with <c>AddressedId</c>, so a
/// malformed id gets its level's not-found code, never a 400 (BR-M1-20).
/// </summary>
/// <param name="OrganizationId">The organization's id as it arrived in the address.</param>
/// <param name="ProjectId">The project's id as it arrived in the address.</param>
public sealed record DeactivateProjectCommand(string? OrganizationId, string? ProjectId);
