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
/// <param name="States">
/// feat-003 (BR-WS-3, api-contract § 3.1): the repeated <c>state</c> values as they arrived — state names as stored. A blank
/// value is left out, one that names no state selects nothing, and none is an error.
/// </param>
/// <param name="AssigneeIds">
/// feat-003 (BR-WS-8, api-contract § 3.2): the repeated <c>assigneeId</c> values as they arrived — a user id as stored or <c>none</c>.
/// </param>
/// <param name="Search">
/// feat-003 (BR-WS-9, api-contract § 3.3): the <c>search</c> values as they arrived; the first non-blank one is the keyword.
/// </param>
public sealed record GetWorkItemsQuery(
    string? OrganizationId,
    string? ProjectId,
    int? Page,
    IReadOnlyList<string?>? TeamIds = null,
    IReadOnlyList<string?>? States = null,
    IReadOnlyList<string?>? AssigneeIds = null,
    IReadOnlyList<string?>? Search = null);
