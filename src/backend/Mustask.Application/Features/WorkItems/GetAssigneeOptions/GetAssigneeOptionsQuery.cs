namespace Mustask.Application.Features.WorkItems.GetAssigneeOptions;

/// <summary>
/// <c>GET /api/organizations/{organizationId}/projects/{projectId}/workitems/assignee-options?state=</c> (feat-003 US-2,
/// api-contract § 2.2). Ids are the untrusted strings of the address: a malformed one gets its level's not-found code.
/// </summary>
/// <param name="States">
/// The repeated <c>state</c> values as they arrived — the states the list's «Статус» has chosen, which decide whether the people of
/// deleted items are offered. A blank value is left out, one that names no state adds nobody, and none is an error.
/// </param>
public sealed record GetAssigneeOptionsQuery(string? OrganizationId, string? ProjectId, IReadOnlyList<string?>? States = null);
