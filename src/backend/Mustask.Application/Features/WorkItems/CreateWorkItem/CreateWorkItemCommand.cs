namespace Mustask.Application.Features.WorkItems.CreateWorkItem;

/// <summary>
/// <c>POST /api/organizations/{organizationId}/projects/{projectId}/workitems</c>
/// (US-1, api-contract § 2.2). Every value is the untrusted string it arrived as.
/// </summary>
/// <param name="RequestId">The client's key of this submission of the dialog (BR-M3-26).</param>
/// <param name="TypeId">A type of the organization's process (BR-M3-1).</param>
/// <param name="Title">Заголовок, untrimmed (BR-M3-3).</param>
public sealed record CreateWorkItemCommand(
    string? OrganizationId,
    string? ProjectId,
    string? RequestId,
    string? TypeId,
    string? Title);
