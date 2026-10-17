namespace Mustask.Application.Features.Boards.GetProjectBoard;

/// <summary>
/// <c>GET /api/organizations/{organizationId}/projects/{projectId}/boards/{level}</c>
/// (feat-039 US-3, api-contract § 3.3). Every value is the untrusted string of the address.
/// </summary>
/// <param name="Level">The level segment: <c>epics</c>, <c>features</c> or <c>stories</c>.</param>
public sealed record GetProjectBoardQuery(string? OrganizationId, string? ProjectId, string? Level);
