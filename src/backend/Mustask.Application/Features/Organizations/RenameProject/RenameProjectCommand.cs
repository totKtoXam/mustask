namespace Mustask.Application.Features.Organizations.RenameProject;

/// <summary>
/// <c>PUT /api/organizations/{organizationId}/projects/{projectId}/name</c> (US-3: rename a project and its root). Ids are the untrusted strings of the
/// address and the body: the handler parses them with <c>AddressedId</c>, so a
/// malformed id gets its level's not-found code, never a 400 (BR-M1-20).
/// </summary>
/// <param name="OrganizationId">The organization's id as it arrived in the address.</param>
/// <param name="ProjectId">The project's id as it arrived in the address.</param>
/// <param name="Name">The name as sent, untrimmed.</param>
public sealed record RenameProjectCommand(string? OrganizationId, string? ProjectId, string? Name);
