namespace Mustask.Application.Features.Organizations.AddTeam;

/// <summary>
/// <c>POST /api/organizations/{organizationId}/teams</c> (feat-028 api-contract § 2.1, US-1: create a team of the organization).
/// Ids are the untrusted strings of the address and the body: the handler parses them with <c>AddressedId</c>, so a malformed
/// organization gets its not-found code, never a 400 (BR-M1-20).
/// </summary>
/// <param name="OrganizationId">The organization's id as it arrived in the address.</param>
/// <param name="Name">The name as sent, untrimmed.</param>
/// <param name="Description">The description as sent, never trimmed; <c>null</c>, absent and the empty string are none (BR-OL-3).</param>
/// <param name="MemberIds">
/// The people to put in the team, as sent: each must be an active member of this organization, known to the user directory and
/// active there. Absent and <c>null</c> are empty; duplicates collapse.
/// </param>
/// <param name="ProjectIds">
/// The projects to link the team to, as sent: each must be an active project of this organization. Absent and <c>null</c> are
/// empty; duplicates collapse.
/// </param>
public sealed record AddTeamCommand(
    string? OrganizationId,
    string? Name,
    string? Description,
    IReadOnlyList<string?>? MemberIds,
    IReadOnlyList<string?>? ProjectIds);
