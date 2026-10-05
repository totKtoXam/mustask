namespace Mustask.Application.Features.Organizations.RenameTeam;

/// <summary>
/// <c>PUT /api/organizations/{organizationId}/projects/{projectId}/teams/{teamId}/name</c> (US-4: rename a team). Ids are the untrusted strings of the
/// address and the body: the handler parses them with <c>AddressedId</c>, so a
/// malformed id gets its level's not-found code, never a 400 (BR-M1-20).
/// </summary>
/// <param name="OrganizationId">The organization's id as it arrived in the address.</param>
/// <param name="ProjectId">The project's id as it arrived in the address.</param>
/// <param name="TeamId">The team's id as it arrived in the address.</param>
/// <param name="Name">The name as sent, untrimmed.</param>
public sealed record RenameTeamCommand(string? OrganizationId, string? ProjectId, string? TeamId, string? Name);
