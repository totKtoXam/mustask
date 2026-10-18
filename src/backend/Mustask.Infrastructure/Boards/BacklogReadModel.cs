using Mustask.Application.Abstractions.Boards;
using Mustask.Application.Abstractions.UserDirectory;
using Mustask.Application.Abstractions.WorkItems;
using Mustask.Domain.Boards;
using Mustask.Domain.Organizations;
using Mustask.Domain.Process;
using Mustask.Domain.UserDirectory;
using Mustask.Domain.WorkItems;
using Mustask.Infrastructure.Organizations;
using Mustask.Infrastructure.WorkItems;
using Microsoft.EntityFrameworkCore;

namespace Mustask.Infrastructure.Boards;

/// <summary>
/// <see cref="IBacklogReadModel"/>: two statements whatever the backlog's size —
/// the members joined to their project, type, level, state, owning team and place (one statement; mod-017 reads no area
/// tree), and the lookup of their assignees through
/// mod-014's <see cref="IUserDirectory"/>, one call for each 500 distinct
/// assignees (the directory's per-read limit) and the organization membership of the assignees. The whole backlog is read: there is
/// no paging (spec § Screens), and the order is put on it by the handler. A
/// page's tree set (feat-027) is read the same way, its levels in the one
/// statement; the parents outside it are one more statement. A project's read
/// (feat-039) is the same two, and its team filter (mod-017) is part of the first.
/// </summary>
internal sealed class BacklogReadModel(MustaskDbContext context, IUserDirectory directory) : IBacklogReadModel
{
    public Task<IReadOnlyList<BacklogItemRow>> ReadAsync(BacklogScope scope, CancellationToken cancellationToken) =>
        ItemsAsync(BacklogMembership.Members(context, scope), scope.ProjectId, ordersByCreation: false, cancellationToken);

    public Task<IReadOnlyList<BacklogItemRow>> ReadTreeAsync(BacklogTreeScope scope, CancellationToken cancellationToken) =>
        ItemsAsync(BacklogMembership.TreeMembers(context, scope), scope.ProjectId, ordersByCreation: false, cancellationToken);

    public Task<IReadOnlyList<BacklogItemRow>> ReadProjectAsync(ProjectBacklogScope scope, CancellationToken cancellationToken) =>
        ItemsAsync(ProjectBacklogMembership.Members(context, scope), scope.ProjectId, ordersByCreation: true, cancellationToken);

    public Task<IReadOnlyList<BacklogItemRow>> ReadProjectTreeAsync(ProjectBacklogTreeScope scope, CancellationToken cancellationToken) =>
        ItemsAsync(ProjectBacklogMembership.TreeMembers(context, scope), scope.ProjectId, ordersByCreation: true, cancellationToken);

    /// <summary>
    /// feat-032: the checklists of a board's cards, one statement and then the assignees' lookups, whatever the cards'
    /// number. The children are narrowed in SQL — the project, the stored parent among the cards, a state outside the
    /// Removed category (the <see cref="WorkItemSets.Live"/> entry point, mod-005 ADR-0030 rule 2), a type of a
    /// <see cref="CardChecklist.Kinds"/> kind — and not by Область: a child of another
    /// team is in its parent's checklist (BR-C-1). Read by number, so each list keeps creation order (BR-C-5).
    /// </summary>
    public async Task<IReadOnlyDictionary<Guid, IReadOnlyList<ChecklistChildView>>> ReadChecklistsAsync(
        Guid projectId, IReadOnlyCollection<Guid> parentIds, CancellationToken cancellationToken)
    {
        var parents = parentIds.Distinct().ToList();

        if (parents.Count == 0)
        {
            return new Dictionary<Guid, IReadOnlyList<ChecklistChildView>>();
        }

        var kinds = CardChecklist.Kinds.ToList();

        var rows = await (
                from item in WorkItemSets.Live(context).AsNoTracking()
                join project in context.Projects on item.ProjectId equals project.Id
                join type in context.WorkItemTypes on item.TypeId equals type.Id
                join state in context.StateDefinitions on item.StateId equals state.Id
                where item.ProjectId == projectId
                    && item.ParentId != null
                    && parents.Contains(item.ParentId.Value)
                    && kinds.Contains(type.Appearance)
                orderby item.Number
                select new ChecklistRow(
                    item.ParentId!.Value,
                    item.Id,
                    project.Key,
                    item.Number,
                    type.Id,
                    type.Key,
                    type.Name,
                    type.Appearance,
                    item.Title,
                    item.AssigneeId,
                    state.Id,
                    state.Name,
                    state.Category))
            .ToListAsync(cancellationToken);

        var assignees = await AssigneesAsync(rows.Select(row => row.AssigneeId), projectId, cancellationToken);

        return rows
            .GroupBy(row => row.ParentId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<ChecklistChildView>)group
                    .Select(row => new ChecklistChildView(
                        row.Id,
                        WorkItemIdentifier.Format(row.ProjectKey, row.TypeKey, row.Number),
                        row.Number,
                        new WorkItemTypeView(row.TypeId, row.TypeKey, row.TypeName, row.TypeAppearance.ToWireName()),
                        row.Title,
                        Assignee(row.AssigneeId, assignees),
                        new WorkItemStateView(
                            row.StateId,
                            row.StateName,
                            new WorkItemStateCategoryView(row.StateCategory.ToString(), StateCategoryLabels.Of(row.StateCategory)))))
                    .ToList());
    }

    public async Task<IReadOnlyDictionary<Guid, WorkItemRefView>> FindLiveParentsAsync(
        Guid projectId, IReadOnlyCollection<Guid> workItemIds, CancellationToken cancellationToken)
    {
        var ids = workItemIds.Distinct().ToList();

        if (ids.Count == 0)
        {
            return new Dictionary<Guid, WorkItemRefView>();
        }

        var rows = await WorkItemRefs.Rows(
                context,
                WorkItemSets.Live(context).AsNoTracking().Where(item => item.ProjectId == projectId && ids.Contains(item.Id)))
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(row => row.Id, WorkItemRefs.View);
    }

    /// <summary>
    /// <paramref name="members"/> as rows and cards: one statement, then one lookup of their assignees.
    /// <paramref name="ordersByCreation"/> is a project's read (feat-039 BR-PB-4): each row then carries its creation time,
    /// which orders its unplaced items before their number — except a row of the «Задачи» level, which has no project
    /// order and stands by number (feat-027 BR-T-6). A team's read leaves it out and keeps the number alone.
    /// </summary>
    private async Task<IReadOnlyList<BacklogItemRow>> ItemsAsync(
        IQueryable<BacklogMember> members, Guid projectId, bool ordersByCreation, CancellationToken cancellationToken)
    {
        var rows = await (
                from member in members
                join project in context.Projects on member.Item.ProjectId equals project.Id
                join type in context.WorkItemTypes on member.Item.TypeId equals type.Id
                join level in context.BacklogLevels on type.BacklogLevelId equals level.Id
                join state in context.StateDefinitions on member.Item.StateId equals state.Id
                join team in context.Teams on member.Item.OwningTeamId equals team.Id into owning
                from team in owning.DefaultIfEmpty()
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
                    member.Item.ValueArea,
                    team == null ? null : team.Id,
                    team == null ? null : team.Name,
                    member.Item.CreatedAt,
                    level.Kind))
            .ToListAsync(cancellationToken);

        var assignees = await AssigneesAsync(rows.Select(row => row.AssigneeId), projectId, cancellationToken);

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
                row.ValueArea?.ToString(),
                row.TeamId is { } teamId ? new WorkItemTeamView(teamId, row.TeamName!) : null,
                ordersByCreation && row.LevelKind != BacklogLevelKind.Task ? row.CreatedAt : null))
            .ToList();
    }

    /// <summary>
    /// The directory's users among the rows' distinct assignees, looked up in batches of the directory's per-read limit
    /// (mod-014 api-contract § A.5: above it a consumer splits). A page's tree set holds every level of the page and the
    /// «Задачи» level, up to 20 000 rows, so its distinct assignees pass 500 in an organization of a few hundred people;
    /// one lookup of them all would answer the whole page 400 <c>user_directory.too_many_ids</c> (feat-027 review B-3).
    /// One statement per batch (<see cref="UserDirectoryBatches"/>, the one definition of the split), so the count grows
    /// with the people, never with the rows' depth or number. And — mod-016 BR-M1-26 — which of them are members of the
    /// project's organization right now (one more statement, <see cref="ProjectMembers"/>).
    /// </summary>
    private async Task<Assignees> AssigneesAsync(IEnumerable<string?> assigneeIds, Guid projectId, CancellationToken cancellationToken)
    {
        var ids = assigneeIds.OfType<string>().Distinct(StringComparer.Ordinal).ToList();

        if (ids.Count == 0)
        {
            return new Assignees(new Dictionary<string, DirectoryUser>(StringComparer.Ordinal), []);
        }

        var known = (await UserDirectoryBatches.LookupAllAsync(directory, ids, cancellationToken)).Known;
        var members = await ProjectMembers.MemberIdsAsync(context, projectId, ids, cancellationToken);

        return new Assignees(known, members);
    }

    /// <summary>As mod-003's read: an id the directory does not know keeps its id and has no name («Неизвестный пользователь»).</summary>
    private static WorkItemAssigneeView? Assignee(string? assigneeId, Assignees assignees) =>
        assigneeId switch
        {
            null => null,
            { } id when assignees.Known.TryGetValue(id, out var user) =>
                new WorkItemAssigneeView(id, user.DisplayName, user.Email, assignees.Members.Contains(id)),
            { } id => new WorkItemAssigneeView(id, null, null, assignees.Members.Contains(id)),
        };

    private sealed record ChecklistRow(
        Guid ParentId,
        Guid Id,
        string ProjectKey,
        int Number,
        Guid TypeId,
        string TypeKey,
        string TypeName,
        AppearanceToken TypeAppearance,
        string Title,
        string? AssigneeId,
        Guid StateId,
        string StateName,
        StateCategory StateCategory);

    /// <summary>The directory's answer for the backlog's assignees and the ones among them who are members of the organization.</summary>
    private sealed record Assignees(IReadOnlyDictionary<string, DirectoryUser> Known, HashSet<string> Members);

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
        WorkItemValueArea? ValueArea,
        Guid? TeamId,
        string? TeamName,
        DateTimeOffset CreatedAt,
        BacklogLevelKind LevelKind);
}
