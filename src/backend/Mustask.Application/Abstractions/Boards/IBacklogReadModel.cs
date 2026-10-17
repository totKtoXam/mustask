using Mustask.Application.Abstractions.Process;
using Mustask.Application.Abstractions.WorkItems;
using Mustask.Domain.Boards;
using Mustask.Domain.Process;

namespace Mustask.Application.Abstractions.Boards;

/// <summary>
/// The read side of a team's (mod-006) and a project's (feat-039) backlog and
/// board. Both surfaces of a view read the same items through the one membership
/// query (spec § Model, conditions 1–4), so a board never holds what the backlog
/// does not.
/// </summary>
public interface IBacklogReadModel
{
    /// <summary>
    /// The backlog's items with what a row and a card show and their place on
    /// this backlog, in no particular order (the order is the domain's
    /// <see cref="StackRank"/>), and the team's default area as it stands now.
    /// A fixed number of queries.
    /// </summary>
    Task<BacklogRead> ReadAsync(BacklogScope scope, CancellationToken cancellationToken);

    /// <summary>
    /// feat-027 (api-contract § 1): the tree set of a backlog page — the team's
    /// items of every level of <paramref name="scope"/>, by mod-006's conditions —
    /// each with its place on its own level's backlog of the team, in no
    /// particular order (the tree is the domain's <see cref="BacklogTree"/>), and
    /// the team's default area. The same fixed number of queries as
    /// <see cref="ReadAsync"/>, whatever the levels and the depth.
    /// </summary>
    Task<BacklogRead> ReadTreeAsync(BacklogTreeScope scope, CancellationToken cancellationToken);

    /// <summary>
    /// feat-039 (api-contract § 1): a project's backlog or board of one level — every item of the project of the
    /// level's types in a state outside the Removed category, whatever its area or team, each with its place on the
    /// project's backlog of the level, in no particular order (the order is the domain's <see cref="StackRank"/>,
    /// from the rows' <see cref="BacklogItemRow.CreatedAt"/>). No area tree is read: one statement for the items,
    /// then the assignees' lookups, whatever the size.
    /// </summary>
    Task<IReadOnlyList<BacklogItemRow>> ReadProjectAsync(ProjectBacklogScope scope, CancellationToken cancellationToken);

    /// <summary>
    /// feat-039 (api-contract § 1): the tree set of a project's backlog page — the project's items of every level of
    /// <paramref name="scope"/>, the «Задачи» level included, each with its place on the project's backlog of its own
    /// type's level (none on the «Задачи» level), in no particular order (the tree is the domain's
    /// <see cref="BacklogTree"/>). The same fixed number of statements as <see cref="ReadProjectAsync"/>.
    /// </summary>
    Task<IReadOnlyList<BacklogItemRow>> ReadProjectTreeAsync(ProjectBacklogTreeScope scope, CancellationToken cancellationToken);

    /// <summary>
    /// feat-032 (api-contract § 1): the checklists of the cards of a board — for each of
    /// <paramref name="parentIds"/>, its direct children in <paramref name="projectId"/> that are outside the
    /// Removed category and whose type is of a <see cref="CardChecklist.Kinds"/> kind, whatever their Область or
    /// state, in the order of their numbers. A parent with no such child has no entry. A fixed number of queries:
    /// the rows in one statement, then the assignees' lookups, whatever the number of parents.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, IReadOnlyList<ChecklistChildView>>> ReadChecklistsAsync(
        Guid projectId, IReadOnlyCollection<Guid> parentIds, CancellationToken cancellationToken);

    /// <summary>
    /// feat-001: the project's items among <paramref name="workItemIds"/> that are
    /// not in «Удалена», as link ends by id — the «Родитель» cells of a backlog's
    /// rows (BR-H-8, BR-H-13). One query, whatever the count.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, WorkItemRefView>> FindLiveParentsAsync(
        Guid projectId, IReadOnlyCollection<Guid> workItemIds, CancellationToken cancellationToken);
}

/// <summary>
/// One backlog (team, level) of a project, and what its membership filters on:
/// the level's types (condition 2) and their states outside the Removed category
/// (condition 4). Condition 3, the team's areas, is resolved by the reader from
/// the project's area tree at the moment of the read.
/// </summary>
public sealed record BacklogScope(
    Guid ProjectId,
    Guid TeamId,
    Guid LevelId,
    IReadOnlyCollection<Guid> TypeIds,
    IReadOnlyCollection<Guid> ShownStateIds)
{
    /// <summary>The scope of <paramref name="levelId"/>'s backlog of the team, from the organization's process.</summary>
    public static BacklogScope Of(Guid projectId, Guid teamId, Guid levelId, IEnumerable<WorkItemTypeRow> levelTypes)
    {
        var types = levelTypes.ToList();

        return new BacklogScope(projectId, teamId, levelId, types.Select(type => type.Id).ToList(), ShownStates(types));
    }

    /// <summary>The types' states outside the Removed category (condition 4).</summary>
    internal static IReadOnlyCollection<Guid> ShownStates(IEnumerable<WorkItemTypeRow> types) =>
        types.SelectMany(type => type.States).Where(state => state.Category != StateCategory.Removed).Select(state => state.Id).ToList();
}

/// <summary>
/// feat-027 (api-contract § 1): the backlogs one page's tree is read from — the
/// team's backlog of the page's level and of every level ranked below it, the
/// «Задачи» level included — as one membership: those levels' types and their
/// states outside the Removed category. Condition 3 is resolved by the reader,
/// as for <see cref="BacklogScope"/>.
/// </summary>
public sealed record BacklogTreeScope(
    Guid ProjectId,
    Guid TeamId,
    IReadOnlyCollection<Guid> TypeIds,
    IReadOnlyCollection<Guid> ShownStateIds)
{
    /// <summary>The tree set of the team's page whose level and the levels below it have <paramref name="treeTypes"/>.</summary>
    public static BacklogTreeScope Of(Guid projectId, Guid teamId, IEnumerable<WorkItemTypeRow> treeTypes)
    {
        var types = treeTypes.ToList();

        return new BacklogTreeScope(projectId, teamId, types.Select(type => type.Id).ToList(), BacklogScope.ShownStates(types));
    }
}

/// <summary>
/// feat-039: one project's backlog of a level (api-contract § 1) — the level's types (condition 2) and their states
/// outside the Removed category (condition 3) of the project (condition 1). There is no team and no area condition.
/// </summary>
public sealed record ProjectBacklogScope(
    Guid ProjectId,
    Guid LevelId,
    IReadOnlyCollection<Guid> TypeIds,
    IReadOnlyCollection<Guid> ShownStateIds)
{
    /// <summary>The scope of <paramref name="levelId"/>'s backlog of the project, from the organization's process.</summary>
    public static ProjectBacklogScope Of(Guid projectId, Guid levelId, IEnumerable<WorkItemTypeRow> levelTypes)
    {
        var types = levelTypes.ToList();

        return new ProjectBacklogScope(projectId, levelId, types.Select(type => type.Id).ToList(), BacklogScope.ShownStates(types));
    }
}

/// <summary>
/// feat-039: the project's backlogs one page's tree is read from — the page's level and every level ranked below it,
/// the «Задачи» level included — as one membership (api-contract § 1).
/// </summary>
public sealed record ProjectBacklogTreeScope(
    Guid ProjectId,
    IReadOnlyCollection<Guid> TypeIds,
    IReadOnlyCollection<Guid> ShownStateIds)
{
    /// <summary>The tree set of the project's page whose level and the levels below it have <paramref name="treeTypes"/>.</summary>
    public static ProjectBacklogTreeScope Of(Guid projectId, IEnumerable<WorkItemTypeRow> treeTypes)
    {
        var types = treeTypes.ToList();

        return new ProjectBacklogTreeScope(projectId, types.Select(type => type.Id).ToList(), BacklogScope.ShownStates(types));
    }
}

/// <summary>What <see cref="IBacklogReadModel.ReadAsync"/> and <see cref="IBacklogReadModel.ReadTreeAsync"/> answer.</summary>
public sealed record BacklogRead(WorkItemAreaView DefaultArea, IReadOnlyList<BacklogItemRow> Items);

/// <summary>
/// An item of the backlog: mod-003's fields a row and a card show, its
/// «Приоритет» (feat-022), its place on its level's backlog of the team,
/// <c>null</c> when unplaced, and its stored parent (feat-001), whatever that
/// parent's state. feat-027: the rank of its type's level, and the three values a
/// backlog row shows beside the priority — «Трудоёмкость» in its shortest form,
/// «Бизнес-ценность», «Вид ценности» as its list key — each <c>null</c> when empty
/// or when the type has no such field. A board card shows none of the three.
/// feat-039: <paramref name="CreatedAt"/> is the item's creation time on a project's read, for the levels that have a
/// project order (BR-PB-4), and <c>null</c> on a team's read and on the «Задачи» level, whose rows stand by number;
/// <paramref name="Place"/> is then its place on the project's backlog.
/// </summary>
public sealed record BacklogItemRow(
    Guid Id,
    string Identifier,
    int Number,
    int? Place,
    WorkItemTypeView Type,
    string Title,
    WorkItemAssigneeView? Assignee,
    WorkItemStateView State,
    int Priority,
    Guid? ParentId,
    int LevelRank,
    decimal? Effort,
    int? BusinessValue,
    string? ValueArea,
    DateTimeOffset? CreatedAt = null);
