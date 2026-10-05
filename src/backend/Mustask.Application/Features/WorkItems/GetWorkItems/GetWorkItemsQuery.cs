namespace Mustask.Application.Features.WorkItems.GetWorkItems;

/// <summary>
/// <c>GET /api/organizations/{organizationId}/projects/{projectId}/workitems?page=</c>
/// (US-2, api-contract § 2.1). Ids are the untrusted strings of the address: a
/// malformed one gets its level's not-found code, never a 400.
/// </summary>
/// <param name="Page">1-based; <c>null</c> when the query string has none, which is the first page.</param>
public sealed record GetWorkItemsQuery(string? OrganizationId, string? ProjectId, int? Page);
