using Mustask.Application.Abstractions.Process;
using Mustask.Application.Abstractions.WorkItems;
using Mustask.Application.Features.Organizations;
using Mustask.Domain.Boards;
using Mustask.Domain.Organizations;
using Mustask.Domain.Process;
using Mustask.Domain.WorkItems;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Abstractions.Boards;

/// <summary>
/// A row of an aggregate view (feat-048 api-contract § 2): the backlog row's fields without the tree, the work-item list's
/// project and team, and the time of the last change. The same shape serves a backlog group, a work-item group and the flat list.
/// </summary>
public sealed record AggregateItemView(
    Guid Id,
    string Identifier,
    int Number,
    WorkItemTypeView Type,
    string Title,
    WorkItemAssigneeView? Assignee,
    WorkItemStateView State,
    int Priority,
    WorkItemTeamView? Team,
    WorkItemProjectView Project,
    decimal? Effort,
    int? BusinessValue,
    string? ValueArea,
    WorkItemRefView? Parent,
    DateTimeOffset ChangedAt)
{
    public static AggregateItemView Of(GroupRow item, IReadOnlyDictionary<Guid, WorkItemRefView> parents)
    {
        var row = item.Row;

        return new AggregateItemView(
            row.Id, row.Identifier, row.Number, row.Type, row.Title, row.Assignee, row.State, row.Priority, row.Team, row.Project,
            row.Effort, row.BusinessValue, row.ValueArea, row.ParentId is { } parentId ? parents.GetValueOrDefault(parentId) : null, item.ChangedAt);
    }
}

/// <summary>A card of a board cell: an item's fields and its checklist (feat-032).</summary>
public sealed record AggregateCardView(
    Guid Id,
    string Identifier,
    int Number,
    WorkItemTypeView Type,
    string Title,
    WorkItemAssigneeView? Assignee,
    WorkItemStateView State,
    int Priority,
    WorkItemTeamView? Team,
    WorkItemProjectView Project,
    DateTimeOffset ChangedAt,
    IReadOnlyList<ChecklistChildView> Checklist);

/// <summary>A group header: <c>kind</c> names the grouping; <c>id</c> and <c>name</c> are absent for «Без департамента», «Без команды» and the stream.</summary>
public sealed record GroupHeaderView(
    string Kind, Guid? Id, string? Key, string? Name, int Count, int Total, IReadOnlyList<ColumnCountView>? Columns);

/// <summary>A board column's count inside one group, by the column's key (<c>InProgress-2</c>).</summary>
public sealed record ColumnCountView(string Key, int Count);

/// <summary>What the aggregate slices share of resolving the address, the level and the scope.</summary>
public static class AggregateSupport
{
    public static Guid OrganizationOf(string? organizationId) =>
        AddressedId.TryParse(organizationId, out var id) ? id : throw new NotFoundException(OrganizationErrorCodes.OrganizationNotFound);

    /// <summary>The level of the address in the organization's process; the board's layout is derived from its types.</summary>
    public static ResolvedLevel LevelOf(ProcessDefinitionRow? process, string? segment)
    {
        if (!BoardLevelNames.TryParse(segment, out var level)
            || process is null
            || level.Resolve(process.BacklogLevels.Select(row => new ProcessLevel(row.Id, row.Rank, row.Kind))) is not { } resolved)
        {
            throw new NotFoundException(BoardErrorCodes.LevelNotFound);
        }

        var types = process.WorkItemTypes.Where(type => type.BacklogLevelId == resolved.Id).ToList();
        var layout = BoardLayout.Derive(types
            .Select(type => new LayoutType(
                type.Id,
                type.States.Select(state => new LayoutState(state.Id, state.Name, state.Category, state.Position)).ToList()))
            .ToList());

        return new ResolvedLevel(resolved.Id, types, layout);
    }

    public static AggregateScope ScopeOf(AggregateView view, ResolvedLevel? level, ParsedAggregate parsed, IReadOnlyCollection<Guid>? stateIds = null) =>
        new(
            view,
            level?.Id,
            level?.Types.Select(type => type.Id).ToList(),
            parsed.Filter,
            parsed.DepartmentsSet,
            parsed.DepartmentIds,
            stateIds);

    /// <summary>The rows, each with its live parent (one statement for all of them).</summary>
    public static async Task<IReadOnlyList<AggregateItemView>> ItemsOfAsync(
        IReadOnlyList<GroupRow> rows, Guid organizationId, IBacklogReadModel backlogs, CancellationToken cancellationToken)
    {
        var parentIds = rows.Select(row => row.Row.ParentId).OfType<Guid>().Distinct().ToList();
        var found = await backlogs.FindLiveParentsAsync(organizationId, parentIds, cancellationToken);

        return rows.Select(row => AggregateItemView.Of(row, found)).ToList();
    }
}

/// <summary>A level of the process, resolved: its id, its types in display order and the board's columns derived from them.</summary>
public sealed record ResolvedLevel(Guid Id, IReadOnlyList<WorkItemTypeRow> Types, BoardLayout Layout);
