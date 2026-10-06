using Mustask.Application.Abstractions.WorkItems;

namespace Mustask.Application.Features.WorkItems.SetWorkItemParent;

/// <summary>
/// <c>PUT /api/organizations/{organizationId}/projects/{projectId}/workitems/{identifier}/parent</c>
/// (feat-001 US-1, US-2, US-5, US-7; api-contract § 3.2). Every value is the
/// untrusted string it arrived as.
/// </summary>
public sealed record SetWorkItemParentCommand(string? OrganizationId, string? ProjectId, string? Identifier, ParentChange Change);
