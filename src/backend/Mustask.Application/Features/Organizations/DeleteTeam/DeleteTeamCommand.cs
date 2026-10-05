namespace Mustask.Application.Features.Organizations.DeleteTeam;

/// <summary>
/// <c>DELETE /api/organizations/{organizationId}/projects/{projectId}/teams/{teamId}</c> (US-7: delete a team, its areas passing to the first team (204)). Ids are the untrusted strings of the
/// address and the body: the handler parses them with <c>AddressedId</c>, so a
/// malformed id gets its level's not-found code, never a 400 (BR-M1-20).
/// </summary>
/// <param name="OrganizationId">The organization's id as it arrived in the address.</param>
/// <param name="ProjectId">The project's id as it arrived in the address.</param>
/// <param name="TeamId">The team's id as it arrived in the address.</param>
public sealed record DeleteTeamCommand(string? OrganizationId, string? ProjectId, string? TeamId);
