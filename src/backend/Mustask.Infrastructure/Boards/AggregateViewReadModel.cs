using Mustask.Application.Abstractions.Boards;
using Mustask.Application.Abstractions.Organizations;
using Mustask.Application.Abstractions.UserDirectory;
using Mustask.Application.Abstractions.WorkItems;
using Mustask.Domain.Boards;
using Mustask.Domain.Organizations;
using Mustask.Domain.UserDirectory;
using Mustask.Domain.WorkItems;
using Mustask.Infrastructure.Organizations;
using Mustask.Infrastructure.WorkItems;
using Microsoft.EntityFrameworkCore;

namespace Mustask.Infrastructure.Boards;

/// <summary>
/// <see cref="IAggregateViewReadModel"/> (feat-048). Every read is a fixed number of statements whatever the volume, the number
/// of groups or of projects:
/// <list type="bullet">
/// <item><b>counts</b> — one <c>GROUP BY</c> over the items in scope (project, owning team, type, state), folded by the handler;</item>
/// <item><b>a change-time page</b> (a department, a team, the stream, by team inside one project) — one statement: for every
/// active project in scope a <c>LATERAL</c> subquery takes the first <c>limit + 1</c> items after the cursor in index order
/// (<c>changed_at</c> descending, <c>number</c> descending), and an outer top <c>limit + 1</c> puts the projects' candidates in
/// the one order, with the project key as the last key (BR-NV-12); then the rows of those ids, the assignees and nothing more;</item>
/// <item><b>a rank page</b> (by project, backlog and board) — one project's items joined to its places, keyset on
/// (unplaced, place, creation time, number) — the project's own order (<see cref="StackRank"/>).</item>
/// </list>
/// What an item is, is the single definition of <see cref="WorkItemSets"/> and <see cref="WorkItemFilterPredicate"/>, so the
/// aggregate views cannot disagree with the project's.
/// </summary>
internal sealed class AggregateViewReadModel(MustaskDbContext context, IUserDirectory directory) : IAggregateViewReadModel
{
    private readonly BacklogReadModel backlogs = new(context, directory);

    public async Task<IReadOnlyList<ViewCountRow>> ReadCountsAsync(
        CallerAccess caller, AggregateScope scope, bool narrowed, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(caller);
        ArgumentNullException.ThrowIfNull(scope);

        var counted = narrowed ? scope : scope with { Filter = WorkItemFilter.None, DepartmentsSet = false, DepartmentIds = [] };
        var projects = ProjectsInScope(caller.OrganizationId, counted, null);

        return await (
                from item in Items(counted)
                join project in projects on item.ProjectId equals project.Id
                group item by new { item.ProjectId, item.OwningTeamId, item.TypeId, item.StateId } into groupOfItems
                select new ViewCountRow(
                    groupOfItems.Key.ProjectId, groupOfItems.Key.OwningTeamId, groupOfItems.Key.TypeId, groupOfItems.Key.StateId, groupOfItems.Count()))
            .ToListAsync(cancellationToken);
    }

    public async Task<ViewDirectory> ReadDirectoryAsync(CallerAccess caller, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(caller);

        var organizationId = caller.OrganizationId;

        var projects = await (
                from project in context.Projects.AsNoTracking()
                where project.OrganizationId == organizationId && project.Status == ProjectStatus.Active
                join node in context.Areas.AsNoTracking().Where(area => area.OrganizationId == organizationId && area.Type == AreaType.Project)
                    on project.Id equals node.ProjectId into nodes
                from node in nodes.DefaultIfEmpty()
                select new DirectoryProject(project.Id, project.Name, project.Key, node == null ? null : node.ParentId))
            .ToListAsync(cancellationToken);

        var departments = await context.Areas.AsNoTracking()
            .Where(area => area.OrganizationId == organizationId && area.Type == AreaType.Department)
            .Select(area => new DirectoryName(area.Id, area.Name))
            .ToListAsync(cancellationToken);

        var teams = await context.Teams.AsNoTracking()
            .Where(team => team.OrganizationId == organizationId && team.Status == TeamStatus.Active)
            .Select(team => new DirectoryName(team.Id, team.Name))
            .ToListAsync(cancellationToken);

        return new ViewDirectory(projects, departments, teams);
    }

    public async Task<GroupPage> ReadGroupPageAsync(
        CallerAccess caller, AggregateScope scope, GroupSelector group, CursorPosition? after, int limit, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(caller);
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(group);
        ArgumentOutOfRangeException.ThrowIfLessThan(limit, 1);

        if (group.Grouping == AggregateGrouping.Project
            && !await context.Projects.AsNoTracking().AnyAsync(
                project => project.Id == group.Id && project.OrganizationId == caller.OrganizationId && project.Status == ProjectStatus.Active,
                cancellationToken))
        {
            return new GroupPage(false, [], null);
        }

        var byRank = scope.View != AggregateView.WorkItems && group.Grouping == AggregateGrouping.Project;
        var take = limit + 1;

        var candidates = byRank
            ? await RankCandidatesAsync(caller.OrganizationId, scope, group, after as RankPosition, take, cancellationToken)
            : await ChangedCandidatesAsync(caller.OrganizationId, scope, group, after as ChangedPosition, take, cancellationToken);

        var page = candidates.Take(limit).ToList();
        var next = candidates.Count > limit ? PositionOf(page[^1], byRank) : null;

        return new GroupPage(true, await RowsOfAsync(caller, page, cancellationToken), next);
    }

    public async Task<FlatPage> ReadFlatPageAsync(CallerAccess caller, AggregateScope scope, int page, int pageSize, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(caller);
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentOutOfRangeException.ThrowIfLessThan(page, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);

        var projects = ProjectsInScope(caller.OrganizationId, scope, null);
        var items = Items(scope);

        var count = await (from item in items join project in projects on item.ProjectId equals project.Id select item.Id).CountAsync(cancellationToken);

        var skip = (long)(page - 1) * pageSize;

        if (skip >= count)
        {
            return new FlatPage(count, []);
        }

        // Every project's first skip + size items cover the page whatever the other projects hold; the outer order cuts it.
        var reach = (int)Math.Min(skip + pageSize, int.MaxValue);

        // The second conjunct looks like nothing, and is what keeps EF from turning the correlation into a ROW_NUMBER window over the whole
        // table (it does so for a bare equality): the statement stays the LATERAL ADR-0036 wants, one index range a project.
        var lateral =
            from project in projects
            from item in items.Where(item => item.ProjectId == project.Id && project.Id != Guid.Empty)
                .OrderByDescending(item => item.ChangedAt).ThenByDescending(item => item.Number).Take(reach)
            select new Candidate { Id = item.Id, ChangedAt = item.ChangedAt, Number = item.Number, ProjectKey = project.Key };

        var candidates = await lateral
            .OrderByDescending(candidate => candidate.ChangedAt)
            .ThenByDescending(candidate => candidate.Number)
            .ThenBy(candidate => EF.Functions.Collate(candidate.ProjectKey, "C"))
            .Skip((int)skip)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new FlatPage(count, await RowsOfAsync(caller, candidates, cancellationToken));
    }

    public async Task<IReadOnlyList<AssigneeFilterOptionView>> ReadAssigneeOptionsAsync(
        CallerAccess caller, AggregateScope scope, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(caller);
        ArgumentNullException.ThrowIfNull(scope);

        // The people of the items the selectors and the chosen states reach; the assignee and the keyword do not narrow their own options.
        var optionsScope = scope with { Filter = scope.Filter with { AssigneesSet = false, AssigneeIds = [], NoAssignee = false, Keyword = null } };
        var projects = ProjectsInScope(caller.OrganizationId, optionsScope, null);

        var ids = await (
                from item in Items(optionsScope)
                join project in projects on item.ProjectId equals project.Id
                where item.AssigneeId != null
                select item.AssigneeId!)
            .Distinct()
            .ToListAsync(cancellationToken);

        var lookup = await UserDirectoryBatches.LookupAllAsync(directory, ids, cancellationToken);
        var members = await ProjectMembers.MemberIdsOfOrganizationAsync(context, caller.OrganizationId, ids, cancellationToken);

        var options = ids.Select(id => lookup.Known.TryGetValue(id, out var user)
            ? new AssigneeFilterOptionView(id, user.DisplayName, user.Email, user.IsActive, members.Contains(id))
            : new AssigneeFilterOptionView(id, null, null, false, members.Contains(id)));

        return UserRefOrder.Order(options, option => option.Id, lookup.Known);
    }

    public Task<IReadOnlyDictionary<Guid, IReadOnlyList<ChecklistChildView>>> ReadChecklistsAsync(
        CallerAccess caller, IReadOnlyCollection<Guid> parentIds, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(caller);

        return backlogs.ReadChecklistsAsync(caller.OrganizationId, parentIds, cancellationToken);
    }

    /// <summary>
    /// The items a view reads: the live ones (the work-item list also reaches «Удалена» when the filter names states, as the
    /// project's list does), of the level's types and a board cell's states, narrowed by the one filter predicate.
    /// </summary>
    private IQueryable<WorkItem> Items(AggregateScope scope)
    {
        var items = scope.View == AggregateView.WorkItems && scope.Filter.StatesSet
            ? WorkItemSets.InAnyState(context)
            : WorkItemSets.Live(context);

        if (scope.TypeIds is { } typeIds)
        {
            var types = typeIds.ToList();
            items = items.Where(item => types.Contains(item.TypeId));
        }

        if (scope.StateIds is { } stateIds)
        {
            var states = stateIds.ToList();
            items = items.Where(item => states.Contains(item.StateId));
        }

        return WorkItemFilterPredicate.Apply(context, items.AsNoTracking(), scope.Filter);
    }

    /// <summary>
    /// The active projects of the organization the selectors reach: the chosen projects, the projects placed in the chosen
    /// departments (a project's node under the department, ADR-0031), and — for a department or project group — that group's.
    /// </summary>
    private IQueryable<Project> ProjectsInScope(Guid organizationId, AggregateScope scope, GroupSelector? group)
    {
        var projects = context.Projects.AsNoTracking().Where(project => project.OrganizationId == organizationId && project.Status == ProjectStatus.Active);

        if (scope.Filter.ProjectsSet)
        {
            var ids = scope.Filter.ProjectIds.ToList();
            projects = projects.Where(project => ids.Contains(project.Id));
        }

        if (scope.DepartmentsSet)
        {
            var departments = scope.DepartmentIds.ToList();
            projects = projects.Where(project => context.Areas.Any(area =>
                area.OrganizationId == organizationId && area.Type == AreaType.Project && area.ProjectId == project.Id
                && area.ParentId != null && departments.Contains(area.ParentId.Value)));
        }

        switch (group)
        {
            case { Grouping: AggregateGrouping.Project, Id: { } projectId }:
                projects = projects.Where(project => project.Id == projectId);
                break;
            case { Grouping: AggregateGrouping.Department, Id: { } departmentId }:
                projects = projects.Where(project => context.Areas.Any(area =>
                    area.OrganizationId == organizationId && area.Type == AreaType.Project && area.ProjectId == project.Id && area.ParentId == departmentId));
                break;
            case { Grouping: AggregateGrouping.Department, Id: null }:
                projects = projects.Where(project => context.Areas.Any(area =>
                    area.OrganizationId == organizationId && area.Type == AreaType.Project && area.ProjectId == project.Id && area.ParentId == null));
                break;
        }

        return projects;
    }

    private async Task<List<Candidate>> ChangedCandidatesAsync(
        Guid organizationId, AggregateScope scope, GroupSelector group, ChangedPosition? after, int take, CancellationToken cancellationToken)
    {
        var projects = ProjectsInScope(organizationId, scope, group);
        var items = Items(scope);

        items = group switch
        {
            { Grouping: AggregateGrouping.Team, Id: { } teamId } => items.Where(item => item.OwningTeamId == teamId),
            { Grouping: AggregateGrouping.Team, Id: null } => items.Where(item => item.OwningTeamId == null),
            _ => items,
        };

        IQueryable<Candidate> lateral;

        if (after is null)
        {
            lateral =
                from project in projects
                from item in items.Where(item => item.ProjectId == project.Id && project.Id != Guid.Empty)
                    .OrderByDescending(item => item.ChangedAt).ThenByDescending(item => item.Number).Take(take)
                select new Candidate { Id = item.Id, ChangedAt = item.ChangedAt, Number = item.Number, ProjectKey = project.Key };
        }
        else
        {
            var changed = new DateTimeOffset(after.ChangedTicks, TimeSpan.Zero);
            var number = after.Number;
            var key = after.ProjectKey;

            // The rows after (changed, number, key) in the order changed desc, number desc, key asc.
            lateral =
                from project in projects
                from item in items
                    .Where(item => item.ProjectId == project.Id
                        && (item.ChangedAt < changed
                            || (item.ChangedAt == changed && item.Number < number)
                            || (item.ChangedAt == changed && item.Number == number && string.Compare(project.Key, key) > 0)))
                    .OrderByDescending(item => item.ChangedAt).ThenByDescending(item => item.Number).Take(take)
                select new Candidate { Id = item.Id, ChangedAt = item.ChangedAt, Number = item.Number, ProjectKey = project.Key };
        }

        return await lateral
            .OrderByDescending(candidate => candidate.ChangedAt)
            .ThenByDescending(candidate => candidate.Number)
            .ThenBy(candidate => EF.Functions.Collate(candidate.ProjectKey, "C"))
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    private async Task<List<Candidate>> RankCandidatesAsync(
        Guid organizationId, AggregateScope scope, GroupSelector group, RankPosition? after, int take, CancellationToken cancellationToken)
    {
        var projectId = group.Id!.Value;
        var levelId = scope.LevelId!.Value;
        var projects = ProjectsInScope(organizationId, scope, group);

        var ranked =
            from item in Items(scope)
            where item.ProjectId == projectId && projects.Any()
            join place in context.Set<ProjectBacklogPlace>().AsNoTracking() on item.Id equals place.WorkItemId into placed
            from place in placed.DefaultIfEmpty()
            select new Candidate
            {
                Id = item.Id,
                ChangedAt = item.ChangedAt,
                Number = item.Number,
                CreatedAt = item.CreatedAt,
                Unplaced = place != null && place.ProjectId == projectId && place.BacklogLevelId == levelId ? 0 : 1,
                Place = place != null && place.ProjectId == projectId && place.BacklogLevelId == levelId ? place.Position : 0,
            };

        if (after is not null)
        {
            var unplaced = after.Unplaced;
            var position = after.Place;
            var created = new DateTimeOffset(after.CreatedTicks, TimeSpan.Zero);
            var number = after.Number;

            ranked = ranked.Where(candidate =>
                candidate.Unplaced > unplaced
                || (candidate.Unplaced == unplaced && candidate.Place > position)
                || (candidate.Unplaced == unplaced && candidate.Place == position && candidate.CreatedAt > created)
                || (candidate.Unplaced == unplaced && candidate.Place == position && candidate.CreatedAt == created && candidate.Number > number));
        }

        return await ranked
            .OrderBy(candidate => candidate.Unplaced)
            .ThenBy(candidate => candidate.Place)
            .ThenBy(candidate => candidate.CreatedAt)
            .ThenBy(candidate => candidate.Number)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    private static CursorPosition PositionOf(Candidate last, bool byRank) =>
        byRank
            ? new RankPosition(last.Unplaced, last.Place, last.CreatedAt.UtcTicks, last.Number)
            : new ChangedPosition(last.ChangedAt.UtcTicks, last.Number, last.ProjectKey);

    /// <summary>The page's rows, dressed in one statement and put back in the page's order.</summary>
    private async Task<IReadOnlyList<GroupRow>> RowsOfAsync(CallerAccess caller, IReadOnlyList<Candidate> page, CancellationToken cancellationToken)
    {
        if (page.Count == 0)
        {
            return [];
        }

        var rows = (await backlogs.ReadByIdsAsync(page.Select(candidate => candidate.Id).ToList(), caller.OrganizationId, cancellationToken))
            .ToDictionary(row => row.Id);

        return page.Select(candidate => new GroupRow(rows[candidate.Id], candidate.ChangedAt)).ToList();
    }

    /// <summary>An item chosen for a page: its id, the keys of both orders and the project key (a change-time order's last key).</summary>
    private sealed class Candidate
    {
        public Guid Id { get; init; }

        public DateTimeOffset ChangedAt { get; init; }

        public int Number { get; init; }

        public string ProjectKey { get; init; } = string.Empty;

        public DateTimeOffset CreatedAt { get; init; }

        public int Unplaced { get; init; }

        public int Place { get; init; }
    }
}
