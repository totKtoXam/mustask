namespace Mustask.Application.Abstractions.WorkItems;

/// <summary>
/// The read side of work items (mod-003 api-contract § 1): straight from the
/// store into the wire shapes, with the type and the state as mod-001 holds
/// them, the area's current path as mod-002's tree gives it (DC-9) and the
/// assignee as mod-014's directory knows them. Every read runs a fixed number of
/// queries, whatever the page holds. None checks the project's reachability:
/// the handler has.
/// </summary>
public interface IWorkItemReadModel
{
    /// <summary>The project's item with that number, or <c>null</c>.</summary>
    Task<WorkItemView?> FindAsync(Guid projectId, int number, CancellationToken cancellationToken);

    /// <summary>The item, which the caller knows exists (it has just written it, in the same transaction).</summary>
    Task<WorkItemView> GetAsync(Guid workItemId, CancellationToken cancellationToken);

    /// <summary>
    /// One page of the project's items in every state, most recently changed
    /// first, then by number descending (BR-M3-29), and the project's item count.
    /// </summary>
    Task<WorkItemPage> GetPageAsync(Guid projectId, int page, int pageSize, CancellationToken cancellationToken);
}

/// <summary>A page of the list: <paramref name="Count"/> is the project's item count, not the page's.</summary>
public sealed record WorkItemPage(int Count, IReadOnlyList<WorkItemListItemView> Items);

/// <summary><c>WorkItem</c> of api-contract § 1.1.</summary>
public sealed record WorkItemView(
    Guid Id,
    string Identifier,
    int Number,
    Guid ProjectId,
    WorkItemTypeView Type,
    string Title,
    string Description,
    WorkItemStateView State,
    WorkItemAssigneeView? Assignee,
    WorkItemAreaView Area,
    string CreatedBy,
    DateTimeOffset CreatedAt,
    DateTimeOffset ChangedAt);

/// <summary><c>WorkItemListItem</c> of api-contract § 1.2.</summary>
public sealed record WorkItemListItemView(
    Guid Id,
    string Identifier,
    int Number,
    WorkItemTypeView Type,
    string Title,
    WorkItemAssigneeView? Assignee,
    WorkItemStateView State,
    WorkItemAreaView Area,
    DateTimeOffset ChangedAt);

/// <param name="Appearance"><c>epic</c>, <c>feature</c>, <c>story</c>, <c>task</c> or <c>bug</c>.</param>
public sealed record WorkItemTypeView(Guid Id, string Key, string Name, string Appearance);

public sealed record WorkItemStateView(Guid Id, string Name, WorkItemStateCategoryView Category);

/// <param name="Code"><c>Proposed</c>, <c>InProgress</c>, <c>Resolved</c>, <c>Completed</c> or <c>Removed</c>. Never shown.</param>
/// <param name="Label">The Russian label the panel shows.</param>
public sealed record WorkItemStateCategoryView(string Code, string Label);

/// <summary>
/// An assignee. <paramref name="DisplayName"/> and <paramref name="Email"/> are
/// both <c>null</c> when the directory does not know <paramref name="Id"/>
/// («Неизвестный пользователь»); <paramref name="Email"/> is empty for a known
/// user without one.
/// </summary>
public sealed record WorkItemAssigneeView(string Id, string? DisplayName, string? Email);

/// <param name="Path">mod-002 DC-9: «Платформа / API», the root by its own name.</param>
public sealed record WorkItemAreaView(Guid Id, string Path);
