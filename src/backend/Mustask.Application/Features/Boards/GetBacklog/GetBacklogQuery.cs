namespace Mustask.Application.Features.Boards.GetBacklog;

/// <summary>
/// <c>GET /api/organizations/{organizationId}/projects/{projectId}/teams/{teamId}/backlogs/{level}</c>
/// (US-1, api-contract § 3.1). Every value is the untrusted string of the address.
/// </summary>
/// <param name="Level">The level segment: <c>epics</c>, <c>features</c> or <c>stories</c>.</param>
public sealed record GetBacklogQuery(string? OrganizationId, string? ProjectId, string? TeamId, string? Level);
