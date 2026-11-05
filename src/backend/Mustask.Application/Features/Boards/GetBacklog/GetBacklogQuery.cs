namespace Mustask.Application.Features.Boards.GetBacklog;

/// <summary>
/// <c>GET /api/organizations/{organizationId}/teams/{teamId}/backlogs/{level}</c> (feat-040 US-1, US-2; api-contract § 3.1).
/// Every value is the untrusted string of the address.
/// </summary>
/// <param name="Level">The level segment: <c>epics</c>, <c>features</c> or <c>stories</c>.</param>
/// <param name="ProjectIds">
/// feat-040 (BR-TS-8, api-contract § 3.1): the repeated <c>projectId</c> query values as they arrived; <c>null</c> when the
/// parameter is absent. A value that names no project the team's items are in selects nothing and is never an error.
/// </param>
public sealed record GetBacklogQuery(string? OrganizationId, string? TeamId, string? Level, IReadOnlyList<string?>? ProjectIds = null);
