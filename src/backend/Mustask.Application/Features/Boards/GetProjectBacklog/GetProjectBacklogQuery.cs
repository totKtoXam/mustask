namespace Mustask.Application.Features.Boards.GetProjectBacklog;

/// <summary>
/// <c>GET /api/organizations/{organizationId}/projects/{projectId}/backlogs/{level}</c>
/// (feat-039 US-1, api-contract § 3.1). Every value is the untrusted string of the address.
/// </summary>
/// <param name="Level">The level segment: <c>epics</c>, <c>features</c> or <c>stories</c>.</param>
/// <param name="TeamIds">
/// mod-017 (BR-OT-8, api-contract § 3): the repeated <c>teamId</c> query values as they arrived — a team's id or <c>none</c>;
/// <c>null</c> when the parameter is absent. A value that names no team of the project selects nothing and is never an error.
/// </param>
public sealed record GetProjectBacklogQuery(string? OrganizationId, string? ProjectId, string? Level, IReadOnlyList<string?>? TeamIds = null);
