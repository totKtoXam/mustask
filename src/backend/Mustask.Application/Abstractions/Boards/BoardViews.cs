using Mustask.Application.Abstractions.Process;
using Mustask.Application.Abstractions.WorkItems;
using Mustask.Domain.Boards;
using Mustask.Domain.Process;

namespace Mustask.Application.Abstractions.Boards;

/// <summary>The team of a backlog or a board (mod-017 api-contract § 1.4): a team has no default area now (BR-OT-10).</summary>
public sealed record BoardTeamView(Guid Id, string Name);

/// <summary>The project of a project backlog or board, as it stands now (feat-039 api-contract § 2.1).</summary>
public sealed record BoardProjectView(Guid Id, string Name);

/// <summary>A board column: its key, header and category, what a drop writes per type, and its cards in the view's order, each with its checklist (feat-032).</summary>
public sealed record BoardColumnView(
    string Key,
    string Header,
    WorkItemStateCategoryView Category,
    IReadOnlyList<BoardColumnTargetView> Targets,
    IReadOnlyList<BoardCardView> Items);

/// <summary>The state a drop of a card of <see cref="TypeId"/> writes; <c>null</c>: no drop target (BR-M4-18).</summary>
public sealed record BoardColumnTargetView(Guid TypeId, Guid? StateId);

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

/// <summary>
/// A backlog row and a board card (api-contract § 2.3): mod-003's fields, «Порядок», feat-022's «Приоритет» and, mod-017
/// (api-contract § 1.5), the owning team by its current name or <c>null</c> for «Без команды».
/// </summary>
public sealed record BacklogItemView(
    int Order,
    Guid Id,
    string Identifier,
    int Number,
    WorkItemTypeView Type,
    string Title,
    WorkItemAssigneeView? Assignee,
    WorkItemStateView State,
    int Priority,
    WorkItemTeamView? Team)
{
    /// <summary><paramref name="rows"/> in the backlog's order (<see cref="StackRank.Order"/>), numbered from 1.</summary>
    public static IReadOnlyList<BacklogItemView> InOrder(IReadOnlyList<BacklogItemRow> rows)
    {
        var byId = rows.ToDictionary(row => row.Id);

        return StackRank.Order(rows.Select(row => new StackRankEntry(row.Id, row.Number, row.Place, row.CreatedAt)))
            .Select((id, index) =>
            {
                var row = byId[id];

                return new BacklogItemView(index + 1, row.Id, row.Identifier, row.Number, row.Type, row.Title, row.Assignee, row.State, row.Priority, row.Team);
            })
            .ToList();
    }
}

/// <summary>
/// A board card (feat-032 api-contract § 2.1): a <see cref="BacklogItemView"/>'s fields and the
/// <see cref="Checklist"/> of children. The backlog's rows do not carry it.
/// </summary>
public sealed record BoardCardView(
    int Order,
    Guid Id,
    string Identifier,
    int Number,
    WorkItemTypeView Type,
    string Title,
    WorkItemAssigneeView? Assignee,
    WorkItemStateView State,
    int Priority,
    WorkItemTeamView? Team,
    IReadOnlyList<ChecklistChildView> Checklist)
{
    /// <summary><paramref name="item"/> with its checklist from <paramref name="checklists"/>, by the item's id; none: an empty list.</summary>
    public static BoardCardView Of(BacklogItemView item, IReadOnlyDictionary<Guid, IReadOnlyList<ChecklistChildView>> checklists) =>
        new(
            item.Order, item.Id, item.Identifier, item.Number, item.Type, item.Title, item.Assignee, item.State, item.Priority, item.Team,
            checklists.TryGetValue(item.Id, out var children) ? children : []);
}

/// <summary>One entry of a card's checklist (feat-032 api-contract § 2.2): a task, a bug or a test case beneath the card's item.</summary>
public sealed record ChecklistChildView(
    Guid Id,
    string Identifier,
    int Number,
    WorkItemTypeView Type,
    string Title,
    WorkItemAssigneeView? Assignee,
    WorkItemStateView State);

/// <summary>
/// A top-level row of a team's backlog page (feat-027 api-contract § 2.1): mod-006's
/// fields, «Порядок» among the top-level rows, feat-022's «Приоритет» and the
/// three planning values, the stored parent unless it is in «Удалена», and the
/// whole tree beneath it.
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
    int Priority,
    WorkItemTeamView? Team,
    decimal? Effort,
    int? BusinessValue,
    string? ValueArea,
    WorkItemRefView? Parent,
    IReadOnlyList<BacklogChildView> Children)
{
    /// <summary>
    /// The page's rows with their tree (<see cref="BacklogTree.Nest"/>):
    /// <paramref name="rows"/> are the tree set read for the page of the level ranked
    /// <paramref name="levelRank"/>; <paramref name="liveParentsOutside"/> the rows'
    /// parents outside the set that are not in «Удалена» — a parent inside it is
    /// live by the set's own condition and is its own row. The top-level rows are
    /// numbered «Порядок» from 1; a nested row has none (BR-T-2).
    /// </summary>
    public static IReadOnlyList<BacklogRowView> Tree(
        int levelRank, IReadOnlyList<BacklogItemRow> rows, IReadOnlyDictionary<Guid, WorkItemRefView> liveParentsOutside)
    {
        var byId = rows.ToDictionary(row => row.Id);
        var parents = new ParentCells(byId, liveParentsOutside);

        return BacklogTree.Nest(levelRank, rows.Select(row => new BacklogTreeEntry(row.Id, row.Number, row.Place, row.ParentId, row.LevelRank, row.CreatedAt)).ToList())
            .Select((node, index) =>
            {
                var row = byId[node.ItemId];

                return new BacklogRowView(
                    index + 1, row.Id, row.Identifier, row.Number, row.Type, row.Title, row.Assignee, row.State, row.Priority, row.Team,
                    row.Effort, row.BusinessValue, row.ValueArea, parents.Of(row), BacklogChildView.Of(node.Children, byId, parents));
            })
            .ToList();
    }
}

/// <summary>A nested row (feat-027 api-contract § 2.2): a top-level row's fields without «Порядок».</summary>
public sealed record BacklogChildView(
    Guid Id,
    string Identifier,
    int Number,
    WorkItemTypeView Type,
    string Title,
    WorkItemAssigneeView? Assignee,
    WorkItemStateView State,
    int Priority,
    WorkItemTeamView? Team,
    decimal? Effort,
    int? BusinessValue,
    string? ValueArea,
    WorkItemRefView? Parent,
    IReadOnlyList<BacklogChildView> Children)
{
    /// <summary>
    /// The views of <paramref name="nodes"/> and of every row beneath them, built without recursion (feat-027 D-9,
    /// review B-1): a loop over an explicit stack of levels still to fill, so no depth of the tree — a chain stored
    /// before the depth bound included — can exhaust the stack of the thread that maps it, which would end the whole
    /// process. Each view takes its <c>Children</c> list at once, empty, and the loop fills it when it reaches that
    /// level; the lists are not read until the mapping returns.
    /// </summary>
    internal static IReadOnlyList<BacklogChildView> Of(
        IReadOnlyList<BacklogTreeNode> nodes, IReadOnlyDictionary<Guid, BacklogItemRow> rows, ParentCells parents)
    {
        var views = new List<BacklogChildView>(nodes.Count);
        var pending = new Stack<(IReadOnlyList<BacklogTreeNode> Nodes, List<BacklogChildView> Into)>();
        pending.Push((nodes, views));

        while (pending.TryPop(out var level))
        {
            foreach (var node in level.Nodes)
            {
                var row = rows[node.ItemId];
                var children = new List<BacklogChildView>(node.Children.Count);

                level.Into.Add(new BacklogChildView(
                    row.Id, row.Identifier, row.Number, row.Type, row.Title, row.Assignee, row.State, row.Priority, row.Team,
                    row.Effort, row.BusinessValue, row.ValueArea, parents.Of(row), children));

                if (node.Children.Count > 0)
                {
                    pending.Push((node.Children, children));
                }
            }
        }

        return views;
    }
}

/// <summary>
/// The «Родитель» cells of a page (feat-001 BR-H-13, BR-H-8): a parent in the tree
/// set is built from its own row; one outside it is the live parent read for it,
/// or none when it is in «Удалена».
/// </summary>
internal sealed class ParentCells(IReadOnlyDictionary<Guid, BacklogItemRow> rows, IReadOnlyDictionary<Guid, WorkItemRefView> liveOutside)
{
    public WorkItemRefView? Of(BacklogItemRow row) =>
        row.ParentId switch
        {
            null => null,
            { } id when rows.TryGetValue(id, out var parent) => new WorkItemRefView(parent.Id, parent.Identifier, parent.Type, parent.Title, parent.State),
            { } id => liveOutside.GetValueOrDefault(id),
        };
}
