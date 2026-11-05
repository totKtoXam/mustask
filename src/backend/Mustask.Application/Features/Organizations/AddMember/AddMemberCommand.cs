namespace Mustask.Application.Features.Organizations.AddMember;

/// <summary>
/// <c>POST /api/organizations/{organizationId}/members</c> (api-contract § 1.4). The id of the address and the two fields of
/// the body are the untrusted strings that arrived; the handler judges every one of them.
/// </summary>
/// <param name="OrganizationId">The organization's id as it arrived in the address.</param>
/// <param name="UserId">The user to add, as sent: 1 to 255 characters, not blank. Never trimmed.</param>
/// <param name="Role">Exactly <c>admin</c>, <c>member</c> or <c>viewer</c>.</param>
public sealed record AddMemberCommand(string? OrganizationId, string? UserId, string? Role);
