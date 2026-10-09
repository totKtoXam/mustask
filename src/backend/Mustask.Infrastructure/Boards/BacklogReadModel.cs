using Mustask.Application.Abstractions.Boards;
using Mustask.Application.Abstractions.UserDirectory;
using Mustask.Application.Abstractions.WorkItems;
using Mustask.Domain.Process;
using Mustask.Domain.WorkItems;
using Mustask.Infrastructure.WorkItems;
using Microsoft.EntityFrameworkCore;

namespace Mustask.Infrastructure.Boards;

/// <summary>
/// <see cref="IBacklogReadModel"/>: three queries whatever the backlog's size —
/// the project's area nodes, the members joined to their project, type, state
/// and place (one statement), and one lookup of their assignees through
/// mod-014's <see cref="IUserDirectory"/>. The whole backlog is read: there is
/// no paging (spec § Screens), and the order is put on it by the handler. The
/// rows' parents (feat-001) are one more statement, over all of a tree's levels.
/// </summary>
internal sealed class BacklogReadModel(MustaskDbContext context, IUserDirectory directory) : IBacklogReadModel
{
    public async Task<BacklogRead> ReadAsync(BacklogScope scope, CancellationToken cancellationToken)
    {
        var tree = await BacklogMembership.AreaTreeAsync(context, scope.ProjectId, cancellationToken);
        var teamAreas = BacklogMembership.TeamAreas(tree, scope.TeamId);

        var rows = await (
                from member in BacklogMembership.Members(context, scope, teamAreas)
                join project in context.Projects on member.Item.ProjectId equals project.Id
                join type in context.WorkItemTypes on member.Item.TypeId equals type.Id
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
                    member.Item.Title,
                    member.Item.AssigneeId,
                    state.Id,
                    state.Name,
                    state.Category,
                    member.Item.Priority,
                    member.Item.ParentId))
            .ToListAsync(cancellationToken);

        var assignees = await AssigneesAsync(rows, cancellationToken);

        var items = rows
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
                row.ParentId))
            .ToList();

        // The team's default area from the same tree: an active team has exactly one (mod-002 BR-M1-14).
        var defaultArea = tree.PreOrder().Single(node => node.DefaultOfTeamId == scope.TeamId);

        return new BacklogRead(new WorkItemAreaView(defaultArea.Id, tree.PathOf(defaultArea.Id)), items);
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

    private async Task<IReadOnlyDictionary<string, DirectoryUser>> AssigneesAsync(IReadOnlyCollection<Row> rows, CancellationToken cancellationToken)
    {
        var ids = rows.Select(row => row.AssigneeId).OfType<string>().Distinct(StringComparer.Ordinal).ToList();

        if (ids.Count == 0)
        {
            return new Dictionary<string, DirectoryUser>(StringComparer.Ordinal);
        }

        return (await directory.LookupAsync(ids, cancellationToken)).Known;
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
        string Title,
        string? AssigneeId,
        Guid StateId,
        string StateName,
        StateCategory StateCategory,
        int Priority,
        Guid? ParentId);
}
