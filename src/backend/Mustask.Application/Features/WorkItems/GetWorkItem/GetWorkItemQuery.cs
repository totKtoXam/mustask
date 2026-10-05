namespace Mustask.Application.Features.WorkItems.GetWorkItem;

/// <summary>
/// <c>GET /api/organizations/{organizationId}/projects/{projectId}/workitems/{identifier}</c>
/// (US-3, api-contract § 2.3). Every value is the untrusted string of the address.
/// </summary>
/// <param name="Identifier">The work item identifier, e.g. <c>MSK-BUG-42</c>.</param>
public sealed record GetWorkItemQuery(string? OrganizationId, string? ProjectId, string? Identifier);
