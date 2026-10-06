using Mustask.Application.Abstractions.Process;
using Mustask.Application.Abstractions.WorkItems;
using Mustask.Domain.Boards;
using Mustask.Domain.Process;

namespace Mustask.Application.Abstractions.Boards;

/// <summary>The team of a backlog or a board, with its default area as it stands (api-contract § 2.1).</summary>
public sealed record BoardTeamView(Guid Id, string Name, WorkItemAreaView DefaultArea);

/// <summary>The level of a backlog or a board and its types in display order (api-contract § 2.2).</summary>
public sealed record BoardLevelView(string Code, string Name, IReadOnlyList<BoardTypeView> Types)
{
    public static BoardLevelView Of(BoardLevel level, BacklogLevelRow processLevel, IEnumerable<WorkItemTypeRow> types) =>
        new(
            level.ToWireName(),
            processLevel.Name,
            types
                .Select(type =>
                {
                    var initial = type.States.Single(state => state.Position == StateDefinition.InitialPosition);

                    return new BoardTypeView(
                        type.Id,
                        type.Key,
                        type.Name,
                        type.Appearance.ToWireName(),
                        new WorkItemStateView(
                            initial.Id,
                            initial.Name,
                            new WorkItemStateCategoryView(initial.Category.ToString(), StateCategoryLabels.Of(initial.Category))));
                })
                .ToList());
}

/// <summary>A type of the level: mod-003's type fields and the state a new item of it takes (BR-M3-20).</summary>
public sealed record BoardTypeView(Guid Id, string Key, string Name, string Appearance, WorkItemStateView InitialState);

/// <summary>A backlog row and a board card (api-contract § 2.3): mod-003's fields and «Порядок».</summary>
public sealed record BacklogItemView(
    int Order,
    Guid Id,
    string Identifier,
    int Number,
    WorkItemTypeView Type,
    string Title,
    WorkItemAssigneeView? Assignee,
    WorkItemStateView State)
{
    /// <summary><paramref name="rows"/> in the backlog's order (<see cref="StackRank.Order"/>), numbered from 1.</summary>
    public static IReadOnlyList<BacklogItemView> InOrder(IReadOnlyList<BacklogItemRow> rows)
    {
        var byId = rows.ToDictionary(row => row.Id);

        return StackRank.Order(rows.Select(row => new StackRankEntry(row.Id, row.Number, row.Place)))
            .Select((id, index) =>
            {
                var row = byId[id];

                return new BacklogItemView(index + 1, row.Id, row.Identifier, row.Number, row.Type, row.Title, row.Assignee, row.State);
            })
            .ToList();
    }
}

/// <summary>
/// A top-level row of a team's backlog (mod-006 api-contract § 2.3 with feat-001
/// api-contract § 4): mod-006's fields and «Порядок», the stored parent unless it
/// is in «Удалена», and the rows nested beneath it.
/// </summary>
public sealed record BacklogRowView(
    int Order,
    Guid Id,
    string Identifier,
    int Number,
    WorkItemTypeView Type,
    string Title,
    WorkItemAssigneeView? Assignee,
    WorkItemStateView State,
    WorkItemRefView? Parent,
    IReadOnlyList<BacklogChildView> Children)
{
    /// <summary>
    /// The backlog's rows with their tree (<see cref="BacklogTree.Nest"/>): <paramref name="levels"/>
    /// holds the backlog's own items first, then those of the same team's
    /// backlog one level below, and so on; <paramref name="liveParents"/> the
    /// rows' parents that are not in «Удалена». The top-level rows are numbered
    /// «Порядок» from 1; a nested row has none (BR-H-11).
    /// </summary>
    public static IReadOnlyList<BacklogRowView> Tree(
        IReadOnlyList<IReadOnlyList<BacklogItemRow>> levels, IReadOnlyDictionary<Guid, WorkItemRefView> liveParents)
    {
        var byId = levels.SelectMany(rows => rows).ToDictionary(row => row.Id);

        var tree = BacklogTree.Nest(levels
            .Select(rows => (IReadOnlyCollection<BacklogTreeEntry>)rows.Select(row => new BacklogTreeEntry(row.Id, row.Number, row.Place, row.ParentId)).ToList())
            .ToList());

        return tree
            .Select((node, index) =>
            {
                var row = byId[node.ItemId];

                return new BacklogRowView(
                    index + 1, row.Id, row.Identifier, row.Number, row.Type, row.Title, row.Assignee, row.State,
                    ParentOf(row, liveParents), BacklogChildView.Of(node.Children, byId, liveParents));
            })
            .ToList();
    }

    internal static WorkItemRefView? ParentOf(BacklogItemRow row, IReadOnlyDictionary<Guid, WorkItemRefView> liveParents) =>
        row.ParentId is { } parentId && liveParents.TryGetValue(parentId, out var parent) ? parent : null;
}

/// <summary>A nested row (feat-001 api-contract § 4): a top-level row's fields without «Порядок».</summary>
public sealed record BacklogChildView(
    Guid Id,
    string Identifier,
    int Number,
    WorkItemTypeView Type,
    string Title,
    WorkItemAssigneeView? Assignee,
    WorkItemStateView State,
    WorkItemRefView? Parent,
    IReadOnlyList<BacklogChildView> Children)
{
    internal static IReadOnlyList<BacklogChildView> Of(
        IReadOnlyList<BacklogTreeNode> nodes, IReadOnlyDictionary<Guid, BacklogItemRow> rows, IReadOnlyDictionary<Guid, WorkItemRefView> liveParents) =>
        nodes
            .Select(node =>
            {
                var row = rows[node.ItemId];

                return new BacklogChildView(
                    row.Id, row.Identifier, row.Number, row.Type, row.Title, row.Assignee, row.State,
                    BacklogRowView.ParentOf(row, liveParents), Of(node.Children, rows, liveParents));
            })
            .ToList();
}
