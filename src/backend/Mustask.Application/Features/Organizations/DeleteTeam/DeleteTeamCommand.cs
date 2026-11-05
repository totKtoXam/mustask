namespace Mustask.Application.Features.Organizations.DeleteTeam;

/// <summary>
/// <c>DELETE /api/organizations/{organizationId}/teams/{teamId}</c> (feat-028 api-contract § 2.1, US-5: delete a team, the items it
/// owns passing to «Без команды» in every project it is linked to (204); mod-017 BR-OT-12). Ids are the untrusted strings of the
/// address: the handler parses them with <c>AddressedId</c>, so a malformed id gets its level's not-found code, never a 400 (BR-M1-20).
/// </summary>
/// <param name="OrganizationId">The organization's id as it arrived in the address.</param>
/// <param name="TeamId">The team's id as it arrived in the address.</param>
public sealed record DeleteTeamCommand(string? OrganizationId, string? TeamId);
