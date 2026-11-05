namespace Mustask.Application.Features.Organizations.SetDefaultTeam;

/// <summary>
/// <c>PUT /api/organizations/{organizationId}/projects/{projectId}/default-team</c> (mod-017 US-3, api-contract § 2.8): name the
/// project's default team, or clear the setting. Ids are the untrusted strings of the address and the body: the handler parses
/// them with <c>AddressedId</c>, so a malformed id gets its level's not-found code, never a 400 (BR-M1-20).
/// </summary>
/// <param name="OrganizationId">The organization's id as it arrived in the address.</param>
/// <param name="ProjectId">The project's id as it arrived in the address.</param>
/// <param name="TeamId">
/// The team to name, as it arrived in the body. <c>null</c> — <c>{ "teamId": null }</c>, <c>{}</c> or no body — clears the setting
/// («Не выбрана», D-7); a string that is no id names no team (404).
/// </param>
public sealed record SetDefaultTeamCommand(string? OrganizationId, string? ProjectId, string? TeamId);
