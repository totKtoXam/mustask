namespace Mustask.Application.Features.WorkItems.RestoreWorkItem;

/// <summary>
/// <c>POST /api/organizations/{organizationId}/projects/{projectId}/workitems/{identifier}/restore</c> (mod-005 US-4,
/// api-contract § 2.2). Every value is the untrusted string it arrived as.
/// </summary>
/// <param name="Identifier">The work item identifier, e.g. <c>MSK-US-12</c>.</param>
/// <param name="StateId">
/// <c>null</c> when the body carried none (no body, <c>{}</c> or <c>"stateId": null</c>): the state held at deletion, else the
/// type's first. Otherwise the state to restore into, in the canonical id form or any other (which names nothing).
/// </param>
public sealed record RestoreWorkItemCommand(string? OrganizationId, string? ProjectId, string? Identifier, string? StateId = null);
