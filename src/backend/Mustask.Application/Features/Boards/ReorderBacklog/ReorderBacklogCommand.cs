namespace Mustask.Application.Features.Boards.ReorderBacklog;

/// <summary>
/// <c>POST /api/organizations/{organizationId}/projects/{projectId}/teams/{teamId}/backlogs/{level}/reorder</c>
/// (US-2, api-contract § 3.2). Every value is the untrusted string it arrived as.
/// </summary>
/// <param name="ItemId">The id of the moved work item.</param>
/// <param name="Position"><c>top</c>, <c>bottom</c> or <c>after</c> (BR-M4-25).</param>
/// <param name="AfterItemId">With <c>after</c>: the id of the item the moved one goes directly after.</param>
public sealed record ReorderBacklogCommand(
    string? OrganizationId,
    string? ProjectId,
    string? TeamId,
    string? Level,
    string? ItemId,
    string? Position,
    string? AfterItemId);
