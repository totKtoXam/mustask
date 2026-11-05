namespace Mustask.Application.Features.Organizations.LinkProjectsToTeam;

/// <summary>
/// <c>POST /api/organizations/{organizationId}/teams/{teamId}/projects</c> (feat-028 api-contract § 2.2, US-3: link projects to the
/// team, from the team's page). Ids are the untrusted strings of the address and the body: the handler parses them with
/// <c>AddressedId</c>, so a malformed id gets its level's not-found code, never a 400 (BR-M1-20).
/// </summary>
/// <param name="OrganizationId">The organization's id as it arrived in the address.</param>
/// <param name="TeamId">The team's id as it arrived in the address.</param>
/// <param name="ProjectIds">
/// The projects to link, as sent. Absent, <c>null</c> and empty are the same: nothing to link, a no-op 204 (D-14); duplicates
/// collapse (api-contract § 0.2).
/// </param>
public sealed record LinkProjectsToTeamCommand(string? OrganizationId, string? TeamId, IReadOnlyList<string?>? ProjectIds);
