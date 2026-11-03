using Mustask.Application.Abstractions.Organizations;
using Mustask.Application.Abstractions.WorkItems;
using Mustask.Domain.Boards;
using Mustask.Domain.WorkItems;

namespace Mustask.Application.Abstractions.Boards;

/// <summary>
/// The read side of the aggregate views (feat-048): the organization's, a department's and a team's «Бэклог», «Канбан» and
/// «Рабочие элементы», cut into groups by the user's grouping (BR-AV-1). Every method takes the <see cref="CallerAccess"/> first
/// and required (BR-NV-15): its organization is the tenant predicate, and feat-041 adds the visible set inside it. A
/// signature test pins that. Each method is a fixed number of statements whatever the volume, the groups or the projects.
/// </summary>
public interface IAggregateViewReadModel
{
    /// <summary>
    /// The headers' counts: one statement of the items in scope grouped by project, owning team, type and state, which the
    /// handler folds into the chosen grouping and the board's columns; <paramref name="narrowed"/> <c>false</c> leaves out the
    /// selectors and the filters (the «из 40» total).
    /// </summary>
    Task<IReadOnlyList<ViewCountRow>> ReadCountsAsync(CallerAccess caller, AggregateScope scope, bool narrowed, CancellationToken cancellationToken);

    /// <summary>The names the headers show: the organization's active projects with their department, its departments and its teams.</summary>
    Task<ViewDirectory> ReadDirectoryAsync(CallerAccess caller, CancellationToken cancellationToken);

    /// <summary>
    /// One page of a group (ADR-0036): up to <paramref name="limit"/> items after <paramref name="after"/>, in the group's order —
    /// the project's rank for the backlog's and board's by-project group, change time descending, number descending, project key
    /// for everything else — and the position to continue from, <c>null</c> at the end. An absent <c>group.ProjectFound</c>
    /// means the project of a by-project group is not an active project of the organization.
    /// </summary>
    Task<GroupPage> ReadGroupPageAsync(
        CallerAccess caller, AggregateScope scope, GroupSelector group, CursorPosition? after, int limit, CancellationToken cancellationToken);

    /// <summary>The flat list's page and its total (BR-NV-12): change time descending, number descending, project key.</summary>
    Task<FlatPage> ReadFlatPageAsync(CallerAccess caller, AggregateScope scope, int page, int pageSize, CancellationToken cancellationToken);

    /// <summary>The options of the organization-wide list's «Исполнитель» filter: the people assigned to the items in scope.</summary>
    Task<IReadOnlyList<AssigneeFilterOptionView>> ReadAssigneeOptionsAsync(CallerAccess caller, AggregateScope scope, CancellationToken cancellationToken);

    /// <summary>The checklists of a board's cards (feat-032), as the other boards read them.</summary>
    Task<IReadOnlyDictionary<Guid, IReadOnlyList<ChecklistChildView>>> ReadChecklistsAsync(
        CallerAccess caller, IReadOnlyCollection<Guid> parentIds, CancellationToken cancellationToken);
}

/// <summary>
/// What an aggregate read covers. <paramref name="TypeIds"/> are the level's types for a backlog or board, <c>null</c> for the
/// work-item list (every type); <paramref name="StateIds"/> restrict a board cell to a column's states, <c>null</c> for no
/// restriction. <paramref name="Filter"/> carries the team, project, state, assignee and keyword dimensions; the department
/// selector is the one dimension the filter has no member for.
/// </summary>
public sealed record AggregateScope(
    AggregateView View,
    Guid? LevelId,
    IReadOnlyCollection<Guid>? TypeIds,
    WorkItemFilter Filter,
    bool DepartmentsSet,
    IReadOnlyCollection<Guid> DepartmentIds,
    IReadOnlyCollection<Guid>? StateIds = null);

/// <summary>The group a page is read for: the id of the department, project or team, or <c>null</c> for «Без департамента» / «Без команды»; for <see cref="AggregateGrouping.None"/> the id is unused.</summary>
public sealed record GroupSelector(AggregateGrouping Grouping, Guid? Id);

/// <summary>A count of items in scope for one (project, owning team, type, state).</summary>
public sealed record ViewCountRow(Guid ProjectId, Guid? TeamId, Guid TypeId, Guid StateId, int Count);

/// <summary>The names of the headers: <paramref name="Projects"/> carry their department (<c>null</c> at the top level).</summary>
public sealed record ViewDirectory(
    IReadOnlyList<DirectoryProject> Projects,
    IReadOnlyList<DirectoryName> Departments,
    IReadOnlyList<DirectoryName> Teams);

public sealed record DirectoryProject(Guid Id, string Name, string Key, Guid? DepartmentId);

public sealed record DirectoryName(Guid Id, string Name);

/// <summary>An item of a page with the time it was last changed, the first key of the change-time order.</summary>
public sealed record GroupRow(BacklogItemRow Row, DateTimeOffset ChangedAt);

/// <summary>A group page: its rows in order and the position after the last of them when more follow.</summary>
public sealed record GroupPage(bool ProjectFound, IReadOnlyList<GroupRow> Rows, CursorPosition? Next);

/// <summary>The flat list's page of rows and the total of the filtered list.</summary>
public sealed record FlatPage(int Count, IReadOnlyList<GroupRow> Rows);

/// <summary>A position in a group's order (ADR-0036): where the next page starts, never a row.</summary>
public abstract record CursorPosition;

/// <summary>Change-time order: the last row's change time (UTC ticks), number and project key.</summary>
public sealed record ChangedPosition(long ChangedTicks, int Number, string ProjectKey) : CursorPosition;

/// <summary>The project's rank order: unplaced (1) after placed (0), the place, the creation time (UTC ticks) and the number.</summary>
public sealed record RankPosition(int Unplaced, int Place, long CreatedTicks, int Number) : CursorPosition;
