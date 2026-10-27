using Mustask.Application.Abstractions.Boards;
using Mustask.Application.Abstractions.Organizations;
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
/// (feat-039) is the same two, and its team filter (mod-017) is part of the first. A team's read (feat-040) is the same two across
/// the team's projects: its membership joins the projects under the caller's organization (the tenant predicate, BR-TS-13), and
/// the project filter is part of the first statement too.
/// </summary>
internal sealed class BacklogReadModel(MustaskDbContext context, IUserDirectory directory) : IBacklogReadModel
{
    public Task<IReadOnlyList<BacklogItemRow>> ReadTeamAsync(TeamBacklogScope scope, CallerAccess caller, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(caller);

        return ItemsAsync(
            BacklogMembership.Members(context, scope, caller.OrganizationId),
            Tenant.OfOrganization(caller.OrganizationId),
            RowTime.OfTeam,
            cancellationToken);
    }

    public Task<IReadOnlyList<BacklogItemRow>> ReadTeamTreeAsync(TeamBacklogTreeScope scope, CallerAccess caller, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(caller);

        return ItemsAsync(
            BacklogMembership.TreeMembers(context, scope, caller.OrganizationId),
            Tenant.OfOrganization(caller.OrganizationId),
            RowTime.OfTeam,
            cancellationToken);
    }

    public Task<IReadOnlyList<BacklogItemRow>> ReadProjectAsync(ProjectBacklogScope scope, CancellationToken cancellationToken) =>
        ItemsAsync(ProjectBacklogMembership.Members(context, scope), Tenant.OfProject(scope.ProjectId), RowTime.OfProject, cancellationToken);

    public Task<IReadOnlyList<BacklogItemRow>> ReadProjectTreeAsync(ProjectBacklogTreeScope scope, CancellationToken cancellationToken) =>
        ItemsAsync(ProjectBacklogMembership.TreeMembers(context, scope), Tenant.OfProject(scope.ProjectId), RowTime.OfProject, cancellationToken);

    /// <summary>
    /// feat-032: the checklists of a board's cards, one statement and then the assignees' lookups, whatever the cards'
    /// number. The children are narrowed in SQL — the organization (feat-040 BR-TS-13: a team's board holds the cards of several
    /// projects, and a child is always in its parent's project, so the organization says what the project said), the stored
    /// parent among the cards, a state outside the Removed category (the <see cref="WorkItemSets.Live"/> entry point, mod-005
    /// ADR-0030 rule 2), a type of a <see cref="CardChecklist.Kinds"/> kind — and not by Область: a child of another
    /// team is in its parent's checklist (BR-C-1). Read by number, so each list keeps creation order (BR-C-5).
    /// </summary>
    public async Task<IReadOnlyDictionary<Guid, IReadOnlyList<ChecklistChildView>>> ReadChecklistsAsync(
        Guid organizationId, IReadOnlyCollection<Guid> parentIds, CancellationToken cancellationToken)
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
                where project.OrganizationId == organizationId
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

        var assignees = await AssigneesAsync(rows.Select(row => row.AssigneeId), Tenant.OfOrganization(organizationId), cancellationToken);

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
        Guid organizationId, IReadOnlyCollection<Guid> workItemIds, CancellationToken cancellationToken)
    {
        var ids = workItemIds.Distinct().ToList();

        if (ids.Count == 0)
        {
            return new Dictionary<Guid, WorkItemRefView>();
        }

        // The organization is the tenant predicate (feat-040 BR-TS-13): a parent is always in its child's project, which is in
        // the caller's organization, so a parent of another organization is never read.
        var rows = await WorkItemRefs.Rows(
                context,
                WorkItemSets.Live(context).AsNoTracking().Where(item =>
                    ids.Contains(item.Id) && context.Projects.Any(project => project.Id == item.ProjectId && project.OrganizationId == organizationId)))
            .ToListAsync(cancellationToken);

        return rows.ToDictionary(row => row.Id, WorkItemRefs.View);
    }

    /// <summary>
    /// <paramref name="members"/> as rows and cards: one statement, then one lookup of their assignees. <paramref name="time"/>
    /// says which rows carry their creation time, the first key of the unplaced items' order: a project's read (feat-039
    /// BR-PB-4) gives it for every level that has a project order, except the «Задачи» level, which has none and stands by number
    /// (feat-027 BR-T-6); a team's read (feat-040 BR-TS-6) gives it on every row, the «Задачи» level included, because its
    /// rows are of several projects and a number alone would tie. Every row gives its project and the project's key.
    /// </summary>
    private async Task<IReadOnlyList<BacklogItemRow>> ItemsAsync(
        IQueryable<BacklogMember> members, Tenant tenant, RowTime time, CancellationToken cancellationToken)
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
                    project.Id,
                    project.Name,
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

        var assignees = await AssigneesAsync(rows.Select(row => row.AssigneeId), tenant, cancellationToken);

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
                new WorkItemProjectView(row.ProjectId, row.ProjectName),
                row.ProjectKey,
                time == RowTime.OfTeam || row.LevelKind != BacklogLevelKind.Task ? row.CreatedAt : null))
            .ToList();
    }

    /// <summary>
    /// The directory's users among the rows' distinct assignees, looked up in batches of the directory's per-read limit
    /// (mod-014 api-contract § A.5: above it a consumer splits). A page's tree set holds every level of the page and the
    /// «Задачи» level, up to 20 000 rows, so its distinct assignees pass 500 in an organization of a few hundred people;
    /// one lookup of them all would answer the whole page 400 <c>user_directory.too_many_ids</c> (feat-027 review B-3).
    /// One statement per batch (<see cref="UserDirectoryBatches"/>, the one definition of the split), so the count grows
    /// with the people, never with the rows' depth or number. And — mod-016 BR-M1-26 — which of them are members of the
    /// organization right now (one more statement, <see cref="ProjectMembers"/>): of the project's organization for a project's
    /// read, of the caller's for a team's, whose rows are of several projects (feat-040 plan-backend PD-12).
    /// </summary>
    private async Task<Assignees> AssigneesAsync(IEnumerable<string?> assigneeIds, Tenant tenant, CancellationToken cancellationToken)
    {
        var ids = assigneeIds.OfType<string>().Distinct(StringComparer.Ordinal).ToList();

        if (ids.Count == 0)
        {
            return new Assignees(new Dictionary<string, DirectoryUser>(StringComparer.Ordinal), []);
        }

        var known = (await UserDirectoryBatches.LookupAllAsync(directory, ids, cancellationToken)).Known;
        var members = tenant.OrganizationId is { } organizationId
            ? await ProjectMembers.MemberIdsOfOrganizationAsync(context, organizationId, ids, cancellationToken)
            : await ProjectMembers.MemberIdsAsync(context, tenant.ProjectId!.Value, ids, cancellationToken);

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

    /// <summary>Whose members the assignees are asked of: the one organization of a team's read, or the project's of a project's.</summary>
    private readonly record struct Tenant(Guid? OrganizationId, Guid? ProjectId)
    {
        public static Tenant OfOrganization(Guid organizationId) => new(organizationId, null);

        public static Tenant OfProject(Guid projectId) => new(null, projectId);
    }

    /// <summary>Which rows give their creation time (see <see cref="ItemsAsync"/>).</summary>
    private enum RowTime
    {
        /// <summary>A project's read: every level but «Задачи».</summary>
        OfProject,

        /// <summary>A team's read: every row.</summary>
        OfTeam,
    }

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
        Guid ProjectId,
        string ProjectName,
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
