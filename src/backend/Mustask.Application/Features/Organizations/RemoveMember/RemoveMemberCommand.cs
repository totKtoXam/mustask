namespace Mustask.Application.Features.Organizations.RemoveMember;

/// <summary>
/// <c>DELETE /api/organizations/{organizationId}/members/{userId}</c> (api-contract § 1.6). The ids are the untrusted
/// strings of the address.
/// </summary>
/// <param name="OrganizationId">The organization's id as it arrived in the address.</param>
/// <param name="UserId">The member's user id as it arrived in the path: matched as given.</param>
public sealed record RemoveMemberCommand(string? OrganizationId, string? UserId);
