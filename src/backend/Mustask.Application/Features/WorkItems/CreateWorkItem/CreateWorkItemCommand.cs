namespace Mustask.Application.Features.WorkItems.CreateWorkItem;

/// <summary>
/// <c>POST /api/organizations/{organizationId}/projects/{projectId}/workitems</c>
/// (US-1, api-contract § 2.2). Every value is the untrusted string it arrived as.
/// </summary>
/// <param name="RequestId">The client's key of this submission of the dialog (BR-M3-26).</param>
/// <param name="TypeId">A type of the organization's process (BR-M3-1).</param>
/// <param name="Title">Заголовок, untrimmed (BR-M3-3).</param>
/// <param name="TeamId">
/// mod-006 (BR-M4-21): the team whose backlog or board the item is created from; it then takes that team's default
/// area instead of the root. <c>null</c> when the request has none.
/// </param>
/// <param name="ParentId">
/// feat-027 (BR-T-10, its api-contract § 3): the item to create the new one beneath; it then takes that item's area
/// and is linked beneath it. <c>null</c> when the request has none.
/// </param>
public sealed record CreateWorkItemCommand(
    string? OrganizationId,
    string? ProjectId,
    string? RequestId,
    string? TypeId,
    string? Title,
    string? TeamId = null,
    string? ParentId = null);
