namespace Mustask.Application.Features.WorkItems.GetWorkItems;

/// <summary>
/// <c>GET /api/organizations/{organizationId}/projects/{projectId}/workitems?page=</c>
/// (US-2, api-contract § 2.1). Ids are the untrusted strings of the address: a
/// malformed one gets its level's not-found code, never a 400.
/// </summary>
/// <param name="Page">1-based; <c>null</c> when the query string has none, which is the first page.</param>
/// <param name="TeamIds">
/// mod-017 (BR-OT-8, api-contract § 3): the repeated <c>teamId</c> values as they arrived — a team's id or <c>none</c>;
/// <c>null</c> when the parameter is absent. A value that names no team of the project selects nothing and is never an error.
/// </param>
public sealed record GetWorkItemsQuery(string? OrganizationId, string? ProjectId, int? Page, IReadOnlyList<string?>? TeamIds = null);
