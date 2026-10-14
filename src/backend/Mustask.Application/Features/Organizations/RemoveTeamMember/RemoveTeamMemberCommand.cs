namespace Mustask.Application.Features.Organizations.RemoveTeamMember;

/// <summary>
/// <c>DELETE /api/organizations/{organizationId}/projects/{projectId}/teams/{teamId}/members/{userId}</c> (api-contract § 2.4).
/// The ids are the untrusted strings of the address.
/// </summary>
/// <param name="UserId">The member's user id as it arrived in the path: matched as given.</param>
public sealed record RemoveTeamMemberCommand(string? OrganizationId, string? ProjectId, string? TeamId, string? UserId);
