namespace Mustask.Application.Features.Boards.GetBoard;

/// <summary>
/// <c>GET /api/organizations/{organizationId}/projects/{projectId}/teams/{teamId}/boards/{level}</c>
/// (US-4, api-contract § 3.3). Every value is the untrusted string of the address.
/// </summary>
/// <param name="Level">The level segment: <c>epics</c>, <c>features</c> or <c>stories</c>.</param>
public sealed record GetBoardQuery(string? OrganizationId, string? ProjectId, string? TeamId, string? Level);
