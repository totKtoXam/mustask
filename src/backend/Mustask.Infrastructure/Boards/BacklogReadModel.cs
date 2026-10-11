using Mustask.Application.Abstractions.Boards;
using Mustask.Application.Abstractions.UserDirectory;
using Mustask.Application.Abstractions.WorkItems;
using Mustask.Domain.Organizations;
using Mustask.Domain.Process;
using Mustask.Domain.UserDirectory;
using Mustask.Domain.WorkItems;
using Mustask.Infrastructure.WorkItems;
using Microsoft.EntityFrameworkCore;

namespace Mustask.Infrastructure.Boards;

/// <summary>
/// <see cref="IBacklogReadModel"/>: three queries whatever the backlog's size —
/// the project's area nodes, the members joined to their project, type, level,
/// state and place (one statement), and the lookup of their assignees through
/// mod-014's <see cref="IUserDirectory"/>, one call for each 500 distinct
/// assignees (the directory's per-read limit). The whole backlog is read: there is
/// no paging (spec § Screens), and the order is put on it by the handler. A
/// page's tree set (feat-027) is read the same way, its levels in the one
/// statement; the parents outside it are one more statement.
/// </summary>
internal sealed class BacklogReadModel(MustaskDbContext context, IUserDirectory directory) : IBacklogReadModel
{
    public async Task<BacklogRead> ReadAsync(BacklogScope scope, CancellationToken cancellationToken)
    {
        var tree = await BacklogMembership.AreaTreeAsync(context, scope.ProjectId, cancellationToken);
        var members = BacklogMembership.Members(context, scope, BacklogMembership.TeamAreas(tree, scope.TeamId));

        return new BacklogRead(DefaultArea(tree, scope.TeamId), await ItemsAsync(members, cancellationToken));
    }

    public async Task<BacklogRead> ReadTreeAsync(BacklogTreeScope scope, CancellationToken cancellationToken)
    {
        var tree = await BacklogMembership.AreaTreeAsync(context, scope.ProjectId, cancellationToken);
        var members = BacklogMembership.TreeMembers(context, scope, BacklogMembership.TeamAreas(tree, scope.TeamId));

        return new BacklogRead(DefaultArea(tree, scope.TeamId), await ItemsAsync(members, cancellationToken));
    }

    public async Task<IReadOnlyDictionary<Guid, WorkItemRefView>> FindLiveParentsAsync(
        Guid projectId, IReadOnlyCollection<Guid> workItemIds, CancellationToken cancellationToken)
    {
        var ids = workItemIds.Distinct().ToList();

        if (ids.Count == 0)
        {
            return new Dictionary<Guid, WorkItemRefView>();
        }

        var removedStates = WorkItemRefs.RemovedStateIds(context);

        var rows = await WorkItemRefs.Rows(
                context,
                context.WorkItems.AsNoTracking().Where(item => item.ProjectId == projectId && ids.Contains(item.Id) && !removedStates.Contains(item.StateId)))
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(row => row.Id, WorkItemRefs.View);
    }

    /// <summary>The team's default area from the same tree: an active team has exactly one (mod-002 BR-M1-14).</summary>
    private static WorkItemAreaView DefaultArea(AreaTree tree, Guid teamId)
    {
        var defaultArea = tree.PreOrder().Single(node => node.DefaultOfTeamId == teamId);

        return new WorkItemAreaView(defaultArea.Id, tree.PathOf(defaultArea.Id));
    }

    /// <summary><paramref name="members"/> as rows and cards: one statement, then one lookup of their assignees.</summary>
    private async Task<IReadOnlyList<BacklogItemRow>> ItemsAsync(IQueryable<BacklogMember> members, CancellationToken cancellationToken)
    {
        var rows = await (
                from member in members
                join project in context.Projects on member.Item.ProjectId equals project.Id
                join type in context.WorkItemTypes on member.Item.TypeId equals type.Id
                join level in context.BacklogLevels on type.BacklogLevelId equals level.Id
                join state in context.StateDefinitions on member.Item.StateId equals state.Id
                select new Row(
                    member.Item.Id,
                    project.Key,
                    member.Item.Number,
                    member.Place,
                    type.Id,
                    type.Key,
                    type.Name,
                    type.Appearance,
                    level.Rank,
                    member.Item.Title,
                    member.Item.AssigneeId,
                    state.Id,
                    state.Name,
                    state.Category,
                    member.Item.Priority,
                    member.Item.ParentId,
                    member.Item.Effort,
                    member.Item.BusinessValue,
                    member.Item.ValueArea))
            .ToListAsync(cancellationToken);

        var assignees = await AssigneesAsync(rows, cancellationToken);

        return rows
            .Select(row => new BacklogItemRow(
                row.Id,
                WorkItemIdentifier.Format(row.ProjectKey, row.TypeKey, row.Number),
                row.Number,
                row.Place,
                new WorkItemTypeView(row.TypeId, row.TypeKey, row.TypeName, row.TypeAppearance.ToWireName()),
                row.Title,
                Assignee(row.AssigneeId, assignees),
                new WorkItemStateView(
                    row.StateId,
                    row.StateName,
                    new WorkItemStateCategoryView(row.StateCategory.ToString(), StateCategoryLabels.Of(row.StateCategory))),
                row.Priority,
                row.ParentId,
                row.LevelRank,
                WorkItemReadModel.Shortest(row.Effort),
                row.BusinessValue,
                row.ValueArea?.ToString()))
            .ToList();
    }

    /// <summary>
    /// The directory's users among the rows' distinct assignees, looked up in batches of the directory's per-read limit
    /// (mod-014 api-contract § A.5: above it a consumer splits). A page's tree set holds every level of the page and the
    /// «Задачи» level, up to 20 000 rows, so its distinct assignees pass 500 in an organization of a few hundred people;
    /// one lookup of them all would answer the whole page 400 <c>user_directory.too_many_ids</c> (feat-027 review B-3).
    /// One statement per batch, so the count grows with the people, never with the rows' depth or number.
    /// </summary>
    private async Task<IReadOnlyDictionary<string, DirectoryUser>> AssigneesAsync(IReadOnlyCollection<Row> rows, CancellationToken cancellationToken)
    {
        var known = new Dictionary<string, DirectoryUser>(StringComparer.Ordinal);
        var ids = rows.Select(row => row.AssigneeId).OfType<string>().Distinct(StringComparer.Ordinal);

        foreach (var batch in ids.Chunk(UserDirectoryLimits.IdsPerRead))
        {
            foreach (var (id, user) in (await directory.LookupAsync(batch, cancellationToken)).Known)
            {
                known[id] = user;
            }
        }

        return known;
    }

    /// <summary>As mod-003's read: an id the directory does not know keeps its id and has no name («Неизвестный пользователь»).</summary>
    private static WorkItemAssigneeView? Assignee(string? assigneeId, IReadOnlyDictionary<string, DirectoryUser> known) =>
        assigneeId switch
        {
            null => null,
            { } id when known.TryGetValue(id, out var user) => new WorkItemAssigneeView(id, user.DisplayName, user.Email),
            { } id => new WorkItemAssigneeView(id, null, null),
        };

    private sealed record Row(
        Guid Id,
        string ProjectKey,
        int Number,
        int? Place,
        Guid TypeId,
        string TypeKey,
        string TypeName,
        AppearanceToken TypeAppearance,
        int LevelRank,
        string Title,
        string? AssigneeId,
        Guid StateId,
        string StateName,
        StateCategory StateCategory,
        int Priority,
        Guid? ParentId,
        decimal? Effort,
        int? BusinessValue,
        WorkItemValueArea? ValueArea);
}
