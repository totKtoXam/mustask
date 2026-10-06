using Mustask.Application.Abstractions.WorkItems;

namespace Mustask.Application.Features.WorkItems.SaveWorkItem;

/// <summary>
/// <c>PATCH /api/organizations/{organizationId}/projects/{projectId}/workitems/{identifier}</c>
/// (US-3, US-4, US-5, api-contract § 2.4; feat-001 api-contract § 3.3). Every
/// value is the untrusted string it arrived as; a field that is <c>null</c> was
/// not sent and is not changed (BR-M3-25).
/// </summary>
/// <param name="Assignee"><c>null</c>: not sent; otherwise the user to set, or <c>null</c> inside it for «Не назначен».</param>
/// <param name="Parent"><c>null</c>: not sent, the parent stays whoever changed it; otherwise feat-001's parent write.</param>
public sealed record SaveWorkItemCommand(
    string? OrganizationId,
    string? ProjectId,
    string? Identifier,
    string? Title = null,
    string? Description = null,
    string? StateId = null,
    AssigneeChange? Assignee = null,
    string? AreaId = null,
    ParentChange? Parent = null);

/// <summary>The sent assignee: a user id, or <c>null</c> to clear it (BR-M3-22).</summary>
public sealed record AssigneeChange(string? UserId);
