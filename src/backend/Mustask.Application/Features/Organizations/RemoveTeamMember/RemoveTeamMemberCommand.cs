namespace Mustask.Application.Features.Organizations.RemoveTeamMember;

/// <summary>
/// <c>DELETE /api/organizations/{organizationId}/teams/{teamId}/members/{userId}</c> (feat-028 api-contract § 2.3).
/// The ids are the untrusted strings of the address.
/// </summary>
/// <param name="UserId">The member's user id as it arrived in the path: matched as given.</param>
public sealed record RemoveTeamMemberCommand(string? OrganizationId, string? TeamId, string? UserId);
