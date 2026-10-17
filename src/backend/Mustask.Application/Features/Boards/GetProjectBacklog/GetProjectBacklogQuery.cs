namespace Mustask.Application.Features.Boards.GetProjectBacklog;

/// <summary>
/// <c>GET /api/organizations/{organizationId}/projects/{projectId}/backlogs/{level}</c>
/// (feat-039 US-1, api-contract § 3.1). Every value is the untrusted string of the address.
/// </summary>
/// <param name="Level">The level segment: <c>epics</c>, <c>features</c> or <c>stories</c>.</param>
public sealed record GetProjectBacklogQuery(string? OrganizationId, string? ProjectId, string? Level);
