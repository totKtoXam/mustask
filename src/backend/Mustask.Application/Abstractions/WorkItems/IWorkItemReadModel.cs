using Mustask.Domain.WorkItems;

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
    /// <summary>
    /// The project's item with that number, or <c>null</c> — in every state, a deleted item too: it is found by its address
    /// and read in full (mod-005 BR-DR-6, BR-DR-7).
    /// </summary>
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
    /// One page of the project's items, most recently changed first, then by number descending, and the count of the items
    /// that match <paramref name="filter"/> (mod-017 BR-OT-8, feat-003 BR-WS-2): the count and the page follow the filter, in
    /// the same statements — a filter adds none. The count counts the same set as the page, whatever page it is. The items are
    /// the ones <b>outside «Удалена»</b> (mod-005 BR-DR-6) unless the filter sets <see cref="WorkItemFilter.StatesSet"/>; then
    /// the deleted ones are reachable too, and an item is listed when its state's name is among the chosen (feat-003 BR-WS-4).
    /// </summary>
    Task<WorkItemPage> GetPageAsync(Guid projectId, int page, int pageSize, WorkItemFilter filter, CancellationToken cancellationToken);

    /// <summary>
    /// The options of the list's «Исполнитель» filter (feat-003 api-contract § 2.2): the distinct assignees of the project's items
    /// outside «Удалена», and of its deleted items whose state's name is among <paramref name="stateNames"/>; each as the directory
    /// and the membership know them now, known users by display name, then the users the directory does not know, by id.
    /// </summary>
    Task<IReadOnlyList<AssigneeFilterOptionView>> GetAssigneeOptionsAsync(
        Guid projectId, IReadOnlyCollection<string> stateNames, CancellationToken cancellationToken);
}

/// <summary>A page of the list: <paramref name="Count"/> is the number of the project's items that match the filter, not the page's.</summary>
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
    WorkItemTeamView? Team,
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

    /// <summary>
    /// mod-005 (api-contract § 1.1): the record of the item's latest deletion — the state it held, who deleted it and when.
    /// <c>null</c> for a live item, and for an item in «Удалена» that has no marks (one put there outside Delete,
    /// BR-DR-10). Whether the item is deleted is its <see cref="State"/>'s category, never this.
    /// </summary>
    public required WorkItemDeletionView? Deletion { get; init; }

    /// <summary>
    /// feat-040 (api-contract § 4.2, BR-TS-3): the item's project <b>as it stands now</b>, beside <see cref="ProjectId"/>,
    /// which stays. The card dialog's «Проект» reads it, so a card opened over a team view shows the item's own project.
    /// </summary>
    public required WorkItemProjectView Project { get; init; }
}

/// <summary>
/// mod-005 (api-contract § 1.1) <c>deletion</c>: <paramref name="FromState"/> is the state held when the item was
/// deleted, shaped as <c>state</c> is; <paramref name="At"/> is the deletion's own time, not <c>changedAt</c>.
/// </summary>
public sealed record WorkItemDeletionView(WorkItemStateView FromState, WorkItemUserView By, DateTimeOffset At);

/// <summary>
/// mod-005 (api-contract § 1.1) <c>deletion.by</c>, a user reference with the marks of BR-13: <paramref name="Id"/> is the
/// Keycloak subject; <paramref name="DisplayName"/> is <c>null</c> when the user directory does not know the id;
/// <paramref name="IsActive"/> is the directory's flag (<c>false</c> for an unknown, disabled or deleted user);
/// <paramref name="IsMember"/> is whether the user is <b>now</b> a member of the item's organization, in any role, from the
/// membership alone. No e-mail is served.
/// </summary>
public sealed record WorkItemUserView(string Id, string? DisplayName, bool IsActive, bool IsMember);

/// <summary>A single scenario of a user story's acceptance criteria (R1).</summary>
public sealed record AcceptanceCriterionView(string Given, string When, string Then);

/// <summary>
/// An item as an address resolves it: its id, its type and — mod-005 — its state, which the purge's early read decides
/// "not deleted" on, before its pre-flights run.
/// </summary>
public sealed record WorkItemHead(Guid Id, Guid TypeId, Guid StateId);

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

/// <summary><c>WorkItemListItem</c> of api-contract § 1.2, with feat-003's <c>assignee.isActive</c> (its api-contract § 1.1).</summary>
public sealed record WorkItemListItemView(
    Guid Id,
    string Identifier,
    int Number,
    WorkItemTypeView Type,
    string Title,
    WorkItemListAssigneeView? Assignee,
    WorkItemStateView State,
    WorkItemAreaView Area,
    WorkItemTeamView? Team,
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

/// <summary>
/// feat-003 (OQ-2 A, api-contract § 1.1): the list row's assignee — the card's <see cref="WorkItemAssigneeView"/> plus
/// <paramref name="IsActive"/>, the user directory's flag (mod-014 BR-UP-11): <c>false</c> for a disabled or deleted account and
/// for an id the directory does not know, the way <see cref="WorkItemUserView"/> reads it. The card keeps its shape until feat-017.
/// </summary>
public sealed record WorkItemListAssigneeView(string Id, string? DisplayName, string? Email, bool IsActive, bool IsMember);

/// <summary>
/// feat-003 (api-contract § 1.2): one option of the «Исполнитель» filter, with the same five fields as the list row's assignee.
/// <paramref name="DisplayName"/> and <paramref name="Email"/> are <c>null</c> and <paramref name="IsActive"/> is <c>false</c> for an
/// id the directory does not know; <paramref name="IsMember"/> is the membership alone.
/// </summary>
public sealed record AssigneeFilterOptionView(string Id, string? DisplayName, string? Email, bool IsActive, bool IsMember);

/// <param name="Path">mod-002 DC-9: «Платформа / API», the root by its own name.</param>
public sealed record WorkItemAreaView(Guid Id, string Path);

/// <summary>
/// mod-017 api-contract § 1.1 <c>WorkItemTeam</c>: the owning team <b>as it stands now</b> (a rename shows at once). The
/// item's <c>team</c> is <c>null</c> for «Без команды»; a deleted team never appears, its items have none (BR-OT-12).
/// </summary>
public sealed record WorkItemTeamView(Guid Id, string Name);

/// <summary>
/// feat-040 api-contract § 2.1 <c>WorkItemProject</c>: the item's project <b>as it stands now</b> (a rename shows at once).
/// Every item has one; a row, a child and a card of every backlog and board carry it (BR-TS-3).
/// </summary>
public sealed record WorkItemProjectView(Guid Id, string Name);
