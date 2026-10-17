namespace Mustask.Application.Features.Organizations.CreateProject;

/// <summary>
/// <c>POST /api/organizations/{organizationId}/projects</c> (US-3: create a project with its team and root). Ids are the untrusted strings of the
/// address and the body: the handler parses them with <c>AddressedId</c>, so a
/// malformed id gets its level's not-found code, never a 400 (BR-M1-20).
/// </summary>
/// <param name="OrganizationId">The organization's id as it arrived in the address.</param>
/// <param name="Name">The name as sent, untrimmed.</param>
/// <param name="Key">The key as sent: never trimmed or upper-cased.</param>
public sealed record CreateProjectCommand(string? OrganizationId, string? Name, string? Key);
