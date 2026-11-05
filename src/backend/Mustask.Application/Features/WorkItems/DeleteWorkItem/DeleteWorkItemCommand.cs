namespace Mustask.Application.Features.WorkItems.DeleteWorkItem;

/// <summary>
/// <c>POST /api/organizations/{organizationId}/projects/{projectId}/workitems/{identifier}/delete</c> (mod-005 US-1,
/// api-contract § 2.1). Every value is the untrusted string of the address; the route has no body.
/// </summary>
/// <param name="Identifier">The work item identifier, e.g. <c>MSK-US-12</c>.</param>
public sealed record DeleteWorkItemCommand(string? OrganizationId, string? ProjectId, string? Identifier);
