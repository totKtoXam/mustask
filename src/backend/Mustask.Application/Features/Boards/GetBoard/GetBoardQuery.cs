namespace Mustask.Application.Features.Boards.GetBoard;

/// <summary>
/// <c>GET /api/organizations/{organizationId}/teams/{teamId}/boards/{level}</c> (feat-040 US-1, US-2, US-4; api-contract § 3.3).
/// Every value is the untrusted string of the address.
/// </summary>
/// <param name="Level">The level segment: <c>epics</c>, <c>features</c> or <c>stories</c>.</param>
/// <param name="ProjectIds">As <see cref="Mustask.Application.Features.Boards.GetBacklog.GetBacklogQuery.ProjectIds"/>.</param>
public sealed record GetBoardQuery(string? OrganizationId, string? TeamId, string? Level, IReadOnlyList<string?>? ProjectIds = null);
