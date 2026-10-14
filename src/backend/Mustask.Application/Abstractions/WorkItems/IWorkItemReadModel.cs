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

    /// <summary>The project's item with that number as an address needs it (its id and type), or <c>null</c>.</summary>
    Task<WorkItemHead?> FindHeadAsync(Guid projectId, int number, CancellationToken cancellationToken);

    /// <summary>The item as a link end (feat-001 api-contract § 2.1), which the caller knows exists.</summary>
    Task<WorkItemRefView> GetRefAsync(Guid workItemId, CancellationToken cancellationToken);

    /// <summary>
    /// The «Родитель» picker's candidates (feat-001 api-contract § 3.4): items of
    /// the project of the search's types, not the excluded item, not in «Удалена»,
    /// matching the text; at most <paramref name="search"/>'s limit, most recently
    /// changed first, then by number descending, and whether more matched.
    /// </summary>
    Task<ParentCandidates> FindParentCandidatesAsync(ParentCandidateSearch search, CancellationToken cancellationToken);

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

/// <summary>
/// <c>WorkItem</c> of api-contract § 1.1, with feat-001's <paramref name="Parent"/>
/// (the stored one, in «Удалена» too) and <paramref name="Children"/> (direct,
/// those in «Удалена» excepted, by level rank descending, then by number), and
/// feat-022's fields (its api-contract § 2), on the wire beside the others.
/// </summary>
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
    DateTimeOffset ChangedAt,
    WorkItemRefView? Parent,
    IReadOnlyList<WorkItemRefView> Children)
{
    /// <summary>feat-022: «Приоритет», 1..4, on every type.</summary>
    public required int Priority { get; init; }

    /// <summary>«Риск» 1..3; <c>null</c> when empty or not on the type, as every optional field below.</summary>
    public required int? Risk { get; init; }

    public required decimal? StoryPoints { get; init; }

    public required decimal? Effort { get; init; }

    public required int? BusinessValue { get; init; }

    public required decimal? TimeCriticality { get; init; }

    /// <summary>A calendar date, <c>YYYY-MM-DD</c> on the wire.</summary>
    public required DateOnly? StartDate { get; init; }

    public required DateOnly? TargetDate { get; init; }

    /// <summary>«Серьёзность» 1..4, always set on a bug, <c>null</c> on every other type.</summary>
    public required int? Severity { get; init; }

    /// <summary>A key of «Вид работ» (<c>Deployment</c> … <c>Testing</c>), or <c>null</c>.</summary>
    public required string? Activity { get; init; }

    public required decimal? OriginalEstimate { get; init; }

    public required decimal? RemainingWork { get; init; }

    public required decimal? CompletedWork { get; init; }

    /// <summary><c>Business</c> or <c>Architectural</c>, always set on an epic, a feature and a story, <c>null</c> on the others.</summary>
    public required string? ValueArea { get; init; }

    /// <summary>A story's scenarios, [] when empty; null on every other type.</summary>
    public required IReadOnlyList<AcceptanceCriterionView>? AcceptanceCriteria { get; init; }

    public required string? ReproSteps { get; init; }

    public required string? SystemInfo { get; init; }
}

/// <summary>A single scenario of a user story's acceptance criteria (R1).</summary>
public sealed record AcceptanceCriterionView(string Given, string When, string Then);

/// <summary>An item as an address resolves it: its id and its type.</summary>
public sealed record WorkItemHead(Guid Id, Guid TypeId);

/// <summary><c>WorkItemRef</c> of feat-001 api-contract § 2.1: a parent, a child, a picker option.</summary>
public sealed record WorkItemRefView(Guid Id, string Identifier, WorkItemTypeView Type, string Title, WorkItemStateView State);

/// <summary>
/// One search of the «Родитель» picker. <paramref name="Text"/> is trimmed and in
/// lower case, empty for every candidate; <paramref name="ProjectKey"/> builds the
/// identifiers the text is matched against.
/// </summary>
public sealed record ParentCandidateSearch(
    Guid ProjectId,
    string ProjectKey,
    Guid ExcludedItemId,
    IReadOnlyCollection<Guid> TypeIds,
    string Text,
    int Limit);

/// <summary>At most the search's limit of candidates, and whether more matched.</summary>
public sealed record ParentCandidates(IReadOnlyList<WorkItemRefView> Items, bool MoreMatched);

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

/// <param name="Appearance"><c>epic</c>, <c>feature</c>, <c>story</c>, <c>task</c>, <c>bug</c> or <c>testcase</c>.</param>
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
/// <paramref name="IsMember"/> (mod-016 BR-M1-26, api-contract § 3.3) is whether the assignee is
/// currently a member of the item's organization, in any role. It reads the membership alone (BR-M1-32):
/// <c>false</c> for an ex-member and for an id that never was a member, <c>true</c> for a member the directory
/// does not know yet (a creator the projection has not received, BR-M1-25). The «неактивен» and
/// «не в организации» marks are feat-017's.
/// </summary>
public sealed record WorkItemAssigneeView(string Id, string? DisplayName, string? Email, bool IsMember);

/// <param name="Path">mod-002 DC-9: «Платформа / API», the root by its own name.</param>
public sealed record WorkItemAreaView(Guid Id, string Path);
