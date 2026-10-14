namespace Mustask.Application.Features.Organizations.AddTeamMember;

/// <summary>
/// <c>POST /api/organizations/{organizationId}/projects/{projectId}/teams/{teamId}/members</c> (api-contract § 2.3). The ids of
/// the address and the field of the body are the untrusted strings that arrived.
/// </summary>
/// <param name="UserId">The user to add, as sent: 1 to 255 characters, not blank. Never trimmed.</param>
public sealed record AddTeamMemberCommand(string? OrganizationId, string? ProjectId, string? TeamId, string? UserId);
