namespace Mustask.Application.Features.WorkItems.PurgeWorkItem;

/// <summary>
/// <c>POST /api/organizations/{organizationId}/projects/{projectId}/workitems/{identifier}/purge</c> (mod-005 US-6,
/// api-contract § 2.3). Every value is the untrusted string of the address; the route has no body.
/// </summary>
/// <param name="Identifier">The work item identifier, e.g. <c>MSK-US-12</c>.</param>
public sealed record PurgeWorkItemCommand(string? OrganizationId, string? ProjectId, string? Identifier);
