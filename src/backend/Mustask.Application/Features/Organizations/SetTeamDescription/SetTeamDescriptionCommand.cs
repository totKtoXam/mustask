namespace Mustask.Application.Features.Organizations.SetTeamDescription;

/// <summary>
/// <c>PUT /api/organizations/{organizationId}/teams/{teamId}/description</c> (feat-028 api-contract § 2.1: describe a team). Ids
/// are the untrusted strings of the address: a malformed one gets its level's not-found code, never a 400 (BR-M1-20).
/// </summary>
/// <param name="OrganizationId">The organization's id as it arrived in the address.</param>
/// <param name="TeamId">The team's id as it arrived in the address.</param>
/// <param name="Description">
/// The description as sent, never trimmed. <c>null</c> (also <c>{}</c> and no body) and the empty string clear it (BR-OL-3).
/// </param>
public sealed record SetTeamDescriptionCommand(string? OrganizationId, string? TeamId, string? Description);
