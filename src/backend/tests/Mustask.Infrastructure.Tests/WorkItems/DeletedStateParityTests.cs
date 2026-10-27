using Mustask.Application;
using Mustask.Application.Abstractions;
using Mustask.Application.Abstractions.Boards;
using Mustask.Application.Abstractions.Process;
using Mustask.Application.Abstractions.UserDirectory;
using Mustask.Application.Abstractions.WorkItems;
using Mustask.Domain.Boards;
using Mustask.Domain.Process;
using Mustask.Domain.WorkItems;
using Mustask.Infrastructure.Boards;
using Mustask.Infrastructure.Process;
using Mustask.Infrastructure.Tests.Organizations;
using Mustask.Infrastructure.UserDirectory;
using Mustask.Infrastructure.WorkItems;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Mustask.Infrastructure.Tests.WorkItems;

/// <summary>
/// mod-005 (ADR-0030 rule 2): "deleted" has three expressions that must agree — in memory
/// (<see cref="WorkItemTypeStates.IsRemoved"/>, from which the hierarchy's <see cref="HierarchyTypes.IsDeleted"/> reads), in
/// LINQ (<see cref="WorkItemSets"/>) and in the backlogs' memberships and locking raw SQL, which take the scope's shown-state
/// ids. One process holds an item in <b>every state of every type</b> — the seeded five and a test-case type, as
/// feat-032's tests add it — written by SQL; the expected set is read from the in-memory definition, and every read
/// of the line is asserted equal to it. A read that spells the category test for itself, or a change to one expression that
/// the others miss, fails here.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(OrganizationsDatabaseCollection.Name)]
public sealed class DeletedStateParityTests(OrganizationsDatabaseFixture database)
{
    // The hierarchy's copy of the definition, read over the loaded entities.
    [Fact]
    public async Task HierarchyTypes_IsDeleted_AgreesWithTheInMemoryDefinition_ForEveryItem()
    {
        var world = await SeedAsync();
        var hierarchy = ProcessHierarchy.Of(world.Process);

        await using var context = database.NewContext();
        var entities = await context.WorkItems.AsNoTracking().Where(item => item.ProjectId == world.ProjectId).ToListAsync();

        Assert.Equal(world.All.Count, entities.Count);

        foreach (var entity in entities)
        {
            var type = ProcessWorkItemTypes.OfItem(world.Process, entity.TypeId);

            Assert.Equal(world.Deleted.Contains(entity.Id), hierarchy.IsDeleted(entity));
            Assert.Equal(world.Deleted.Contains(entity.Id), entity.IsDeleted(type));
        }
    }

    // The LINQ expression: Live and Deleted are a partition of InAnyState, and each is the in-memory set.
    [Fact]
    public async Task EntryPoints_AreAPartition_AndEqualTheInMemoryDefinition()
    {
        var world = await SeedAsync();

        await using var context = database.NewContext();
        var all = await Ids(WorkItemSets.InAnyState(context), world.ProjectId);
        var live = await Ids(WorkItemSets.Live(context), world.ProjectId);
        var deleted = await Ids(WorkItemSets.Deleted(context), world.ProjectId);

        Assert.Equal(world.All.ToHashSet(), all);
        Assert.Equal(world.Live.ToHashSet(), live);
        Assert.Equal(world.Deleted.ToHashSet(), deleted);
        Assert.Empty(live.Intersect(deleted));
        Assert.Equal(all, live.Union(deleted).ToHashSet());
        Assert.NotEmpty(deleted);
        Assert.NotEmpty(live);
    }

    // The sanity of the fixture itself: every non-Removed state and every Removed state of every type holds an item.
    [Fact]
    public async Task TheFixture_HoldsAnItemInEveryStateOfEveryType_TestCaseTypeIncluded()
    {
        var world = await SeedAsync();

        var states = world.Process.WorkItemTypes.SelectMany(type => type.States).Select(state => state.Id).ToHashSet();

        Assert.Equal(states.Count, world.All.Count);
        Assert.Contains(world.Process.WorkItemTypes, type => type.Appearance == AppearanceToken.Testcase);
        Assert.Equal(world.Process.WorkItemTypes.Count, world.Deleted.Count);
    }

    // The list, page by page, and its count (US-2 "The list neither shows nor counts a deleted item").
    [Fact]
    public async Task List_PageAndCount_AreTheLiveItems_OnEveryPage()
    {
        var world = await SeedAsync();

        await using var scope = Services().CreateAsyncScope();
        var readModel = scope.ServiceProvider.GetRequiredService<IWorkItemReadModel>();
        var seen = new List<Guid>();

        for (var page = 1; page <= 20; page++)
        {
            var answer = await readModel.GetPageAsync(world.ProjectId, page, 7, WorkItemFilter.None, CancellationToken.None);

            Assert.Equal(world.Live.Count, answer.Count);
            seen.AddRange(answer.Items.Select(item => item.Id));

            if (answer.Items.Count == 0)
            {
                break;
            }
        }

        Assert.Equal(world.Live.Count, seen.Count);
        Assert.Equal(world.Live.ToHashSet(), seen.ToHashSet());
    }

    // feat-003 BR-WS-4 (ADR-0030 rule 2): the list under a state filter reaches the deleted items by the chosen name, over the item in
    // every state of every type — «Удалена» lists exactly the in-memory definition's deleted items, the other states' names exactly
    // the live ones, every name together all of them; every page holds an item once.
    [Fact]
    public async Task List_UnderAStateFilter_IsTheItemsOfTheChosenNames_DeletedOnesIncluded_OnEveryPage()
    {
        var world = await SeedAsync();
        var states = world.Process.WorkItemTypes.SelectMany(type => type.States).ToList();
        var removedNames = states.Where(state => state.Category == StateCategory.Removed).Select(state => state.Name).Distinct().ToList();
        var liveNames = states.Where(state => state.Category != StateCategory.Removed).Select(state => state.Name).Distinct().ToList();

        Assert.NotEmpty(removedNames);
        Assert.NotEmpty(liveNames);
        Assert.Empty(removedNames.Intersect(liveNames));

        Assert.Equal(world.Deleted.ToHashSet(), await ListedAsync(world, removedNames));
        Assert.Equal(world.Live.ToHashSet(), await ListedAsync(world, liveNames));
        Assert.Equal(world.All.ToHashSet(), await ListedAsync(world, removedNames.Concat(liveNames).ToList()));
        Assert.Empty(await ListedAsync(world, ["no such state"]));
    }

    // feat-003 BR-WS-6: the «Исполнитель» options read the same sets — the people of the live items, and those of the deleted items
    // only while a chosen name is a Removed state's.
    [Fact]
    public async Task AssigneeOptions_AreThePeopleOfTheLiveItems_AndOfTheDeletedOnesWhenTheirStateIsChosen()
    {
        var world = await SeedAsync();
        var removedName = world.Process.WorkItemTypes.SelectMany(type => type.States).First(state => state.Category == StateCategory.Removed).Name;
        var liveName = world.Process.WorkItemTypes.SelectMany(type => type.States).First(state => state.Category != StateCategory.Removed).Name;

        await database.ExecuteAsync(
            "UPDATE work_items SET assignee_id = 'parity-live' WHERE project_id = @project AND id = ANY(@ids)",
            ("project", world.ProjectId),
            ("ids", world.Live.ToArray()));
        await database.ExecuteAsync(
            "UPDATE work_items SET assignee_id = 'parity-deleted' WHERE project_id = @project AND id = ANY(@ids)",
            ("project", world.ProjectId),
            ("ids", world.Deleted.ToArray()));

        await using var scope = Services().CreateAsyncScope();
        var readModel = scope.ServiceProvider.GetRequiredService<IWorkItemReadModel>();

        async Task<string[]> Offered(params string[] names) =>
            (await readModel.GetAssigneeOptionsAsync(world.ProjectId, names, CancellationToken.None)).Select(option => option.Id).Order(StringComparer.Ordinal).ToArray();

        Assert.Equal(["parity-live"], await Offered());
        Assert.Equal(["parity-live"], await Offered(liveName));
        Assert.Equal(["parity-deleted", "parity-live"], await Offered(removedName));
        Assert.Equal(["parity-deleted", "parity-live"], await Offered(liveName, removedName));
        Assert.Equal(["parity-live"], await Offered("no such state"));
    }

    [Fact]
    public async Task ParentCandidates_AreTheLiveItems()
    {
        var world = await SeedAsync();

        await using var scope = Services().CreateAsyncScope();
        var readModel = scope.ServiceProvider.GetRequiredService<IWorkItemReadModel>();

        var candidates = await readModel.FindParentCandidatesAsync(
            new ParentCandidateSearch(
                world.ProjectId, "PRM", Guid.CreateVersion7(), world.Process.WorkItemTypes.Select(type => type.Id).ToList(), string.Empty, 1_000),
            CancellationToken.None);

        Assert.False(candidates.MoreMatched);
        Assert.Equal(world.Live.ToHashSet(), candidates.Items.Select(item => item.Id).ToHashSet());
        Assert.Equal(world.Live.Count, candidates.Items.Count);
    }

    [Fact]
    public async Task LiveParents_AndChecklists_AreTheLiveItems()
    {
        var world = await SeedAsync();

        await using var scope = Services().CreateAsyncScope();
        var backlogs = scope.ServiceProvider.GetRequiredService<IBacklogReadModel>();

        var parents = await backlogs.FindLiveParentsAsync(world.OrganizationId, world.All, CancellationToken.None);
        Assert.Equal(world.Live.ToHashSet(), parents.Keys.ToHashSet());

        // Every task, bug and test case stands beneath the one story: its checklist holds the live ones, whatever the kind.
        var checklists = await backlogs.ReadChecklistsAsync(world.OrganizationId, [world.Parent], CancellationToken.None);
        var expected = world.ChecklistKinds.Where(world.Live.Contains).ToHashSet();

        Assert.NotEmpty(expected);
        Assert.Equal(expected, checklists[world.Parent].Select(child => child.Id).ToHashSet());
    }

    // The memberships take the scope's shown-state ids (the process's non-Removed states): the team's backlog of every level,
    // the team's tree set, the project's backlog of every level and the project's tree set.
    [Fact]
    public async Task Memberships_TeamAndProject_LevelsAndTrees_AreTheLiveItemsOfTheirTypes()
    {
        var world = await SeedAsync();

        await using var context = database.NewContext();

        foreach (var level in world.Process.BacklogLevels)
        {
            var types = world.Process.WorkItemTypes.Where(type => type.BacklogLevelId == level.Id).ToList();
            var expected = world.LiveOfTypes(types).ToHashSet();

            var team = await BacklogMembership.Members(context, TeamBacklogScope.Of(world.TeamId, level.Id, types), world.OrganizationId)
                .Select(member => member.Item.Id).ToListAsync();
            var project = await ProjectBacklogMembership.Members(context, ProjectBacklogScope.Of(world.ProjectId, level.Id, types))
                .Select(member => member.Item.Id).ToListAsync();

            Assert.Equal(expected, team.ToHashSet());
            Assert.Equal(expected, project.ToHashSet());
            Assert.Equal(expected.Count, team.Count);
            Assert.Equal(expected.Count, project.Count);
        }

        var every = world.Process.WorkItemTypes;
        var teamTree = await BacklogMembership.TreeMembers(context, TeamBacklogTreeScope.Of(world.TeamId, every), world.OrganizationId)
            .Select(member => member.Item.Id).ToListAsync();
        var projectTree = await ProjectBacklogMembership.TreeMembers(context, ProjectBacklogTreeScope.Of(world.ProjectId, every))
            .Select(member => member.Item.Id).ToListAsync();

        Assert.Equal(world.Live.ToHashSet(), teamTree.ToHashSet());
        Assert.Equal(world.Live.ToHashSet(), projectTree.ToHashSet());
    }

    // The raw locking reads of the two reorders state the same conditions in SQL; the project's is pinned to the membership.
    [Fact]
    public async Task LockingReads_OfTheTeamAndProjectReorders_AreTheLiveItemsOfTheirTypes()
    {
        var world = await SeedAsync();

        await using var context = database.NewContext();
        var team = new BacklogRepository(context);
        var project = new ProjectBacklogRepository(context);

        foreach (var level in world.Process.BacklogLevels)
        {
            var types = world.Process.WorkItemTypes.Where(type => type.BacklogLevelId == level.Id).ToList();
            var expected = world.LiveOfTypes(types).ToHashSet();

            var teamEntries = await team.ReadEntriesAsync(world.OrganizationId, TeamBacklogScope.Of(world.TeamId, level.Id, types), CancellationToken.None);
            var projectScope = ProjectBacklogScope.Of(world.ProjectId, level.Id, types);
            var projectEntries = await project.ReadEntriesAsync(projectScope, CancellationToken.None);
            var membership = await ProjectBacklogMembership.Members(context, projectScope).Select(member => member.Item.Id).ToListAsync();

            Assert.Equal(expected, teamEntries.Select(entry => entry.ItemId).ToHashSet());
            Assert.Equal(expected, projectEntries.Select(entry => entry.ItemId).ToHashSet());
            Assert.Equal(membership.ToHashSet(), projectEntries.Select(entry => entry.ItemId).ToHashSet());
        }
    }

    // feat-040 (ADR-0030 rule 2, plan-backend § 10.2): the team's membership across projects. The team's items stand in two active
    // projects and in a deactivated one, an item in every state of every type in each: the membership and the locking read of a
    // level are the live items of that level's types of the two active projects — the in-memory definition on each — and nothing of the
    // deactivated project or of another organization's tenant (a read under another organization's id finds none).
    [Fact]
    public async Task TeamMembership_AndLockingRead_AcrossProjects_AreTheLiveItemsOfTheActiveProjects_NotADeactivatedOnesNorAnotherTenants()
    {
        var world = await SeedAsync();
        var second = await database.AddProjectAsync(world.OrganizationId, "Второй", "SEC");
        var sleeping = await database.AddProjectAsync(world.OrganizationId, "Спящий", "DEA");
        await database.LinkAsync(world.OrganizationId, second.Project.Id, world.TeamId);
        await database.LinkAsync(world.OrganizationId, sleeping.Project.Id, world.TeamId);

        foreach (var (project, root) in new[] { (second.Project.Id, second.Root.Id), (sleeping.Project.Id, sleeping.Root.Id) })
        {
            await database.ExecuteAsync(
                """
                INSERT INTO work_items (id, project_id, number, type_id, state_id, area_id, owning_team_id, title, description,
                                        creation_request_id, created_at, created_by, changed_at)
                SELECT gen_random_uuid(), @project, number, type_id, state_id, @root, @team, title, '', gen_random_uuid(), now(), 'parity-test', now()
                FROM work_items WHERE project_id = @source
                """,
                ("project", project),
                ("root", root),
                ("team", world.TeamId),
                ("source", world.ProjectId));
        }

        await database.ExecuteAsync("UPDATE projects SET status = 'Deactivated' WHERE id = @project", ("project", sleeping.Project.Id));

        var inMemory = world.Process.WorkItemTypes.ToDictionary(type => type.Id, ProcessWorkItemTypes.StatesOf);
        var copies = new Dictionary<Guid, (Guid Type, Guid State)>();

        foreach (var row in await database.QueryAsync(
            "SELECT concat_ws('|', id::text, type_id::text, state_id::text) FROM work_items WHERE project_id = @project",
            ("project", second.Project.Id)))
        {
            var parts = row.Split('|');
            copies[Guid.Parse(parts[0])] = (Guid.Parse(parts[1]), Guid.Parse(parts[2]));
        }

        var sleepingIds = (await database.QueryAsync("SELECT id::text FROM work_items WHERE project_id = @project", ("project", sleeping.Project.Id)))
            .Select(Guid.Parse)
            .ToHashSet();
        Assert.NotEmpty(sleepingIds);

        await using var context = database.NewContext();
        var repository = new BacklogRepository(context);

        foreach (var level in world.Process.BacklogLevels)
        {
            var types = world.Process.WorkItemTypes.Where(type => type.BacklogLevelId == level.Id).ToList();
            var typeIds = types.Select(type => type.Id).ToHashSet();
            var expected = world.LiveOfTypes(types)
                .Concat(copies.Where(copy => typeIds.Contains(copy.Value.Type) && !inMemory[copy.Value.Type].IsRemoved(copy.Value.State)).Select(copy => copy.Key))
                .ToHashSet();
            var scope = TeamBacklogScope.Of(world.TeamId, level.Id, types);

            var members = await BacklogMembership.Members(context, scope, world.OrganizationId).Select(member => member.Item.Id).ToListAsync();
            var entries = await repository.ReadEntriesAsync(world.OrganizationId, scope, CancellationToken.None);

            Assert.NotEmpty(expected);
            Assert.Equal(expected, members.ToHashSet());
            Assert.Equal(expected.Count, members.Count);
            Assert.Equal(expected, entries.Select(entry => entry.ItemId).ToHashSet());
            Assert.Empty(members.Intersect(sleepingIds));
            Assert.Empty(await BacklogMembership.Members(context, scope, Guid.CreateVersion7()).Select(member => member.Item.Id).ToListAsync());
            Assert.Empty(await repository.ReadEntriesAsync(Guid.CreateVersion7(), scope, CancellationToken.None));
        }

        var every = world.Process.WorkItemTypes;
        var tree = await BacklogMembership.TreeMembers(context, TeamBacklogTreeScope.Of(world.TeamId, every), world.OrganizationId)
            .Select(member => member.Item.Id)
            .ToListAsync();

        Assert.Equal(world.Live.Concat(copies.Where(copy => !inMemory[copy.Value.Type].IsRemoved(copy.Value.State)).Select(copy => copy.Key)).ToHashSet(), tree.ToHashSet());
        Assert.Empty(tree.Intersect(sleepingIds));
    }

    // --- Arrangement -------------------------------------------------------------------------------------------------

    private ServiceProvider Services()
    {
        var services = new ServiceCollection();

        services.AddDbContext<MustaskDbContext>(options => options.UseNpgsql(database.ConnectionString));
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IUserDirectory, UserDirectoryReadModel>();
        services.AddScoped<IProcessDefinitionReadModel, ProcessDefinitionReadModel>();
        services.AddApplication();
        services.AddOrganizationsInfrastructure();
        services.AddWorkItemsInfrastructure();
        services.AddBoardsInfrastructure();

        return services.BuildServiceProvider();
    }

    /// <summary>The ids the list answers for a state filter of <paramref name="names"/>, page by page; fails when an item repeats or the count lies.</summary>
    private async Task<HashSet<Guid>> ListedAsync(World world, IReadOnlyCollection<string> names)
    {
        await using var scope = Services().CreateAsyncScope();
        var readModel = scope.ServiceProvider.GetRequiredService<IWorkItemReadModel>();
        var filter = WorkItemFilter.Of("PRM", null, names, null, null);
        var seen = new List<Guid>();
        var count = 0;

        for (var page = 1; page <= 30; page++)
        {
            var answer = await readModel.GetPageAsync(world.ProjectId, page, 7, filter, CancellationToken.None);

            count = answer.Count;
            seen.AddRange(answer.Items.Select(item => item.Id));

            if (answer.Items.Count == 0)
            {
                break;
            }
        }

        Assert.Equal(count, seen.Count);
        Assert.Equal(seen.Count, seen.Distinct().Count());

        return seen.ToHashSet();
    }

    private static async Task<HashSet<Guid>> Ids(IQueryable<WorkItem> items, Guid projectId)
    {
        return (await items.AsNoTracking().Where(item => item.ProjectId == projectId).Select(item => item.Id).ToListAsync()).ToHashSet();
    }

    private sealed record World(
        Guid ProjectId,
        Guid TeamId,
        Guid Parent,
        ProcessDefinitionRow Process,
        IReadOnlyList<Guid> All,
        IReadOnlyList<Guid> Live,
        IReadOnlyList<Guid> Deleted,
        IReadOnlyList<Guid> ChecklistKinds,
        IReadOnlyDictionary<Guid, Guid> TypeOf,
        Guid OrganizationId)
    {
        public IEnumerable<Guid> LiveOfTypes(IEnumerable<WorkItemTypeRow> types)
        {
            var typeIds = types.Select(type => type.Id).ToHashSet();

            return Live.Where(id => typeIds.Contains(TypeOf[id]));
        }
    }

    /// <summary>
    /// A project with a team, a test-case type added to the organization's process, and one item in every state of every type,
    /// all owned by the team; every task, bug and test case beneath one story, which is live. The expected sets come from the
    /// in-memory definition over the process as the read model reads it.
    /// </summary>
    private async Task<World> SeedAsync()
    {
        var structure = await database.CreateStructureAsync();
        var organization = structure.Organization.Id;
        var project = structure.Project.Project.Id;

        var (typeId, ready, closed, removed) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        await database.ExecuteAsync(
            """
            INSERT INTO work_item_types (id, process_id, backlog_level_id, key, name, appearance, position)
            SELECT @type, task.process_id, task.backlog_level_id, 'TC', 'Тест-кейс', 'testcase', 99
            FROM work_item_types AS task
            WHERE task.key = 'TASK' AND task.process_id = (SELECT id FROM processes WHERE organization_id = @organization);
            INSERT INTO state_definitions (id, work_item_type_id, name, category, position) VALUES
                (@ready, @type, 'Готов', 'Proposed', 1),
                (@closed, @type, 'Закрыта', 'Completed', 2),
                (@removed, @type, 'Удалена', 'Removed', 3)
            """,
            ("type", typeId),
            ("organization", organization),
            ("ready", ready),
            ("closed", closed),
            ("removed", removed));

        // One item in every state of every type of the process; numbers in the order of the types and states.
        await database.ExecuteAsync(
            """
            INSERT INTO work_items (id, project_id, number, type_id, state_id, area_id, owning_team_id, title, description,
                                    creation_request_id, created_at, created_by, changed_at)
            SELECT gen_random_uuid(), @project,
                   row_number() OVER (ORDER BY type.key, state.position)::integer,
                   type.id, state.id, @area, @team,
                   'Элемент ' || type.key || ' ' || state.name, '', gen_random_uuid(), now(), 'parity-test', now()
            FROM work_item_types AS type
            JOIN processes AS process ON process.id = type.process_id
            JOIN state_definitions AS state ON state.work_item_type_id = type.id
            WHERE process.organization_id = @organization
            """,
            ("project", project),
            ("area", structure.Project.Root.Id),
            ("team", structure.FirstTeam.Id),
            ("organization", organization));

        var process = await ReadProcessAsync(organization);
        var typeOf = new Dictionary<Guid, Guid>();
        var stateOf = new Dictionary<Guid, Guid>();

        foreach (var row in await database.QueryAsync(
            "SELECT concat_ws('|', id::text, type_id::text, state_id::text) FROM work_items WHERE project_id = @project",
            ("project", project)))
        {
            var parts = row.Split('|');
            typeOf[Guid.Parse(parts[0])] = Guid.Parse(parts[1]);
            stateOf[Guid.Parse(parts[0])] = Guid.Parse(parts[2]);
        }

        var inMemory = process.WorkItemTypes.ToDictionary(type => type.Id, ProcessWorkItemTypes.StatesOf);
        var all = typeOf.Keys.ToList();
        var deleted = all.Where(id => inMemory[typeOf[id]].IsRemoved(stateOf[id])).ToList();
        var live = all.Except(deleted).ToList();

        // The story beneath which the checklist kinds stand: a live one, the first state of the story type.
        var storyType = process.WorkItemTypes.Single(type => type.Key == "US");
        var parent = all.Single(id => typeOf[id] == storyType.Id && stateOf[id] == inMemory[storyType.Id].FirstStateId);
        var kinds = process.WorkItemTypes.Where(type => CardChecklist.Holds(type.Appearance)).Select(type => type.Id).ToHashSet();
        var children = all.Where(id => kinds.Contains(typeOf[id])).ToList();

        await database.ExecuteAsync(
            "UPDATE work_items SET parent_id = @parent WHERE project_id = @project AND id = ANY(@children)",
            ("parent", parent),
            ("project", project),
            ("children", children.ToArray()));

        return new World(project, structure.FirstTeam.Id, parent, process, all, live, deleted, children, typeOf, organization);
    }

    private async Task<ProcessDefinitionRow> ReadProcessAsync(Guid organizationId)
    {
        await using var context = database.NewContext();

        return await new ProcessDefinitionReadModel(context).FindByOrganizationAsync(organizationId, CancellationToken.None)
            ?? throw new InvalidOperationException("The organization has a process.");
    }
}
