namespace Mustask.Application.Features.Organizations.ChangeMemberRole;

/// <summary>
/// <c>PUT /api/organizations/{organizationId}/members/{userId}/role</c> (api-contract § 1.5). The ids of the address and the
/// role of the body are the untrusted strings that arrived.
/// </summary>
/// <param name="OrganizationId">The organization's id as it arrived in the address.</param>
/// <param name="UserId">The member's user id as it arrived in the path: matched as given.</param>
/// <param name="Role">Exactly <c>admin</c>, <c>member</c> or <c>viewer</c>.</param>
public sealed record ChangeMemberRoleCommand(string? OrganizationId, string? UserId, string? Role);
