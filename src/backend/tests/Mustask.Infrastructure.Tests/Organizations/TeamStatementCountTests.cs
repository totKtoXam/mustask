using Mustask.Application;
using Mustask.Application.Abstractions;
using Mustask.Application.Abstractions.Organizations;
using Mustask.Application.Abstractions.Process;
using Mustask.Application.Abstractions.UserDirectory;
using Mustask.Application.Features.Organizations.AddTeam;
using Mustask.Application.Features.Organizations.CreateProject;
using Mustask.Application.Features.Organizations.DeleteTeam;
using Mustask.Application.Features.Organizations.GetOrganizationTeams;
using Mustask.Application.Features.Organizations.GetTeamDeletionCounts;
using Mustask.Application.Features.Organizations.GetTeamProjects;
using Mustask.Application.Features.Organizations.GetTeams;
using Mustask.Application.Features.Organizations.LinkProjectsToTeam;
using Mustask.Application.Features.Organizations.LinkTeamsToProject;
using Mustask.Application.Features.Organizations.SearchTeamMemberCandidates;
using Mustask.Application.Features.Organizations.UnlinkTeam;
using Mustask.Domain.Identity;
using Mustask.Domain.Organizations;
using Mustask.Domain.UserDirectory;
using Mustask.Infrastructure.Process;
using Mustask.Infrastructure.UserDirectory;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

namespace Mustask.Infrastructure.Tests.Organizations;

/// <summary>
/// feat-028 plan-backend § 9.4, "Statement counts" (NFR Volumes, ADR-0028 rule 13) against a real PostgreSQL through the real handlers: the
/// reads and the writes of the organization-level teams take a number of statements that does not grow with the teams, the projects, the
/// links, the members or the items. Each is run for a small organization and for a large one, every statement the context sends is
/// counted, and the two counts are equal and equal to the number the test pins — so a statement added is a decision, not an accident.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(OrganizationsDatabaseCollection.Name)]
public sealed class TeamStatementCountTests(OrganizationsDatabaseFixture database, ITestOutputHelper output)
{
    private const string Caller = "statement-count-caller";

    // Pinned: a change to one of these is a change to the query shape and is made on purpose (the numbers are the first run's).
    private const int OrganizationListStatements = 2;
    private const int ProjectTeamsStatements = 2;
    private const int TeamProjectsStatements = 2;
    private const int DeletionCountsStatements = 3;
    private const int LinkTeamsStatements = 4;
    private const int LinkProjectsStatements = 4;
    private const int AddTeamStatements = 5;
    private const int NewTeamMemberCandidatesStatements = 2;
    private const int CreateProjectStatements = 9;    // 6 + the area tree lock (2) and the project node insert + the department parent read (mod-018, ADR-0031)
    private const int UnlinkStatements = 13;     // 11 + the hierarchy lock's two (mod-005 QD-1)
    private const int DeletionStatements = 14;   // 11 + the same two, for every linked project at once, + the area links ended (mod-018 BR-AW-10)

    /// <summary>An organization of <c>Projects</c> projects, <c>Teams</c> teams each linked to two of them, and the first project's ids.</summary>
    private sealed record Seeded(Guid OrganizationId, IReadOnlyList<Guid> ProjectIds, IReadOnlyList<Guid> TeamIds, Guid RootAreaId)
    {
        public Guid Project => ProjectIds[0];

        public Guid Team => TeamIds[0];
    }

    // The organization's list: its teams with their projects — two statements whether 2 teams over 2 projects or 200 over 50.
    [Fact]
    public async Task OrganizationList_TheSameNumberOfStatements_For2TeamsAs200()
    {
        var small = await SeedAsync(projects: 2, teams: 2);
        var large = await SeedAsync(projects: 50, teams: 200);

        var smallCount = await CountAsync((p, ct) => p.GetRequiredService<GetOrganizationTeamsHandler>().Handle(new GetOrganizationTeamsQuery(Id(small.OrganizationId)), ct));
        var largeCount = await CountAsync((p, ct) => p.GetRequiredService<GetOrganizationTeamsHandler>().Handle(new GetOrganizationTeamsQuery(Id(large.OrganizationId)), ct));

        Assert.Equal(smallCount, largeCount);
        Assert.Equal(OrganizationListStatements, largeCount);
    }

    // The project's linked list, with the default mark and the member flag: one statement per project whatever its number of teams.
    [Fact]
    public async Task ProjectTeams_TheSameNumberOfStatements_For2TeamsAs50()
    {
        var small = await SeedAsync(projects: 2, teams: 2);
        var large = await SeedAsync(projects: 1, teams: 50, linksPerTeam: 1);

        var smallCount = await CountAsync((p, ct) => p.GetRequiredService<GetTeamsHandler>().Handle(new GetTeamsQuery(Id(small.OrganizationId), Id(small.Project)), ct));
        var largeCount = await CountAsync((p, ct) => p.GetRequiredService<GetTeamsHandler>().Handle(new GetTeamsQuery(Id(large.OrganizationId), Id(large.Project)), ct));

        Assert.Equal(smallCount, largeCount);
        Assert.Equal(ProjectTeamsStatements, largeCount);
    }

    // A team's projects: the same for a team linked to 2 projects and to 50.
    [Fact]
    public async Task TeamProjects_TheSameNumberOfStatements_For2ProjectsAs50()
    {
        var small = await SeedAsync(projects: 2, teams: 2);
        var large = await SeedAsync(projects: 50, teams: 1, linksPerTeam: 50);

        var smallCount = await CountAsync((p, ct) => p.GetRequiredService<GetTeamProjectsHandler>().Handle(new GetTeamProjectsQuery(Id(small.OrganizationId), Id(small.Team)), ct));
        var largeCount = await CountAsync((p, ct) => p.GetRequiredService<GetTeamProjectsHandler>().Handle(new GetTeamProjectsQuery(Id(large.OrganizationId), Id(large.Team)), ct));

        Assert.Equal(smallCount, largeCount);
        Assert.Equal(TeamProjectsStatements, largeCount);
    }

    // The deletion's counts, per project: the team, its links with the projects, and the counts — the same for 2 projects and for 50.
    [Fact]
    public async Task DeletionCounts_TheSameNumberOfStatements_For2ProjectsAs50()
    {
        var small = await SeedAsync(projects: 2, teams: 1, linksPerTeam: 2);
        var large = await SeedAsync(projects: 50, teams: 1, linksPerTeam: 50);
        await SeedItemsAsync(small, small.Team, 20);
        await SeedItemsAsync(large, large.Team, 200);

        var smallCount = await CountAsync((p, ct) => p.GetRequiredService<GetTeamDeletionCountsHandler>().Handle(new GetTeamDeletionCountsQuery(Id(small.OrganizationId), Id(small.Team)), ct));
        var largeCount = await CountAsync((p, ct) => p.GetRequiredService<GetTeamDeletionCountsHandler>().Handle(new GetTeamDeletionCountsQuery(Id(large.OrganizationId), Id(large.Team)), ct));

        Assert.Equal(smallCount, largeCount);
        Assert.Equal(DeletionCountsStatements, largeCount);
    }

    // A link of one pair and of fifty: one statement for all the pairs, from the project's end and from the team's.
    [Fact]
    public async Task Link_TheSameNumberOfStatements_For1PairAs50_FromBothEnds()
    {
        var small = await SeedAsync(projects: 2, teams: 2, linksPerTeam: 0);
        var large = await SeedAsync(projects: 50, teams: 50, linksPerTeam: 0);

        var smallFromProject = await CountAsync((p, ct) => p.GetRequiredService<LinkTeamsToProjectHandler>()
            .Handle(new LinkTeamsToProjectCommand(Id(small.OrganizationId), Id(small.Project), [Id(small.Team)]), ct));
        var largeFromProject = await CountAsync((p, ct) => p.GetRequiredService<LinkTeamsToProjectHandler>()
            .Handle(new LinkTeamsToProjectCommand(Id(large.OrganizationId), Id(large.Project), large.TeamIds.Select(Id).ToList()), ct));
        var smallFromTeam = await CountAsync((p, ct) => p.GetRequiredService<LinkProjectsToTeamHandler>()
            .Handle(new LinkProjectsToTeamCommand(Id(small.OrganizationId), Id(small.TeamIds[1]), [Id(small.ProjectIds[1])]), ct));
        var largeFromTeam = await CountAsync((p, ct) => p.GetRequiredService<LinkProjectsToTeamHandler>()
            .Handle(new LinkProjectsToTeamCommand(Id(large.OrganizationId), Id(large.TeamIds[1]), large.ProjectIds.Select(Id).ToList()), ct));

        Assert.Equal(smallFromProject, largeFromProject);
        Assert.Equal(smallFromTeam, largeFromTeam);
        Assert.Equal(LinkTeamsStatements, largeFromProject);
        Assert.Equal(LinkProjectsStatements, largeFromTeam);
    }

    // A team created with 50 projects and 200 members: the projects in one statement, the members in one lookup and one filter, one flush.
    [Fact]
    public async Task AddTeam_TheSameNumberOfStatements_For1ProjectAnd1MemberAs50And200()
    {
        var small = await SeedAsync(projects: 2, teams: 0);
        var large = await SeedAsync(projects: 50, teams: 0);
        var smallMembers = await SeedMembersAsync(small.OrganizationId, 1);
        var largeMembers = await SeedMembersAsync(large.OrganizationId, 200);

        var smallCount = await CountAsync((p, ct) => p.GetRequiredService<AddTeamHandler>().Handle(
            new AddTeamCommand(Id(small.OrganizationId), "Новая малая", "Описание", smallMembers, [Id(small.Project)]), ct));
        var largeCount = await CountAsync((p, ct) => p.GetRequiredService<AddTeamHandler>().Handle(
            new AddTeamCommand(Id(large.OrganizationId), "Новая большая", "Описание", largeMembers, large.ProjectIds.Select(Id).ToList()), ct));

        Assert.Equal(smallCount, largeCount);
        Assert.Equal(AddTeamStatements, largeCount);
    }

    // The creation form's «Участники» search (api-contract § 2.3a): the organization's member ids in one statement and one search of
    // the projection restricted to them — the same for 2 members and for 200, whatever the matches (all 2 or 200 match the text; the
    // answer is cut at 20). Above 500 members the restriction goes in batches of 500 (mod-014 BR-UP-17), as the team's own search does.
    [Fact]
    public async Task NewTeamMemberCandidates_TheSameNumberOfStatements_For2MembersAs200()
    {
        var small = await SeedAsync(projects: 1, teams: 0);
        var large = await SeedAsync(projects: 1, teams: 0);
        await SeedMembersAsync(small.OrganizationId, 2);
        await SeedMembersAsync(large.OrganizationId, 200);

        var smallCount = await CountAsync((p, ct) => p.GetRequiredService<SearchNewTeamMemberCandidatesHandler>()
            .Handle(new SearchNewTeamMemberCandidatesQuery(Id(small.OrganizationId), "Фамилия"), ct));
        var largeCount = await CountAsync((p, ct) => p.GetRequiredService<SearchNewTeamMemberCandidatesHandler>()
            .Handle(new SearchNewTeamMemberCandidatesQuery(Id(large.OrganizationId), "Фамилия"), ct));

        Assert.Equal(smallCount, largeCount);
        Assert.Equal(NewTeamMemberCandidatesStatements, largeCount);
    }

    // A project created naming 1 team and 50: the teams locked in one statement, the pre-checks, one flush.
    [Fact]
    public async Task CreateProject_TheSameNumberOfStatements_For1TeamAs50()
    {
        var small = await SeedAsync(projects: 1, teams: 1, linksPerTeam: 0);
        var large = await SeedAsync(projects: 1, teams: 50, linksPerTeam: 0);

        var smallCount = await CountAsync((p, ct) => p.GetRequiredService<CreateProjectHandler>().Handle(
            new CreateProjectCommand(Id(small.OrganizationId), "Малый проект", "SMLP", "Описание", [Id(small.Team)]), ct));
        var largeCount = await CountAsync((p, ct) => p.GetRequiredService<CreateProjectHandler>().Handle(
            new CreateProjectCommand(Id(large.OrganizationId), "Большой проект", "BIGP", "Описание", large.TeamIds.Select(Id).ToList()), ct));

        Assert.Equal(smallCount, largeCount);
        Assert.Equal(CreateProjectStatements, largeCount);
    }

    // The unlink: the second code, the locks, the owned items, the places, the reassignment, the setting and the link — the statements of
    // a team that owns 50 items when it owns 5 000.
    [Fact]
    public async Task Unlink_TheSameNumberOfStatements_For50ItemsAs5000()
    {
        var small = await SeedAsync(projects: 1, teams: 2, linksPerTeam: 1);
        var large = await SeedAsync(projects: 1, teams: 2, linksPerTeam: 1);
        await SeedItemsAsync(small, small.Team, 50);
        await SeedItemsAsync(large, large.Team, 5_000);

        var smallCount = await CountAsync((p, ct) => p.GetRequiredService<UnlinkTeamHandler>().Handle(
            new UnlinkTeamCommand(Id(small.OrganizationId), Id(small.Project), Id(small.Team), new UnlinkDestination(Id(small.TeamIds[1]))), ct));
        var largeCount = await CountAsync((p, ct) => p.GetRequiredService<UnlinkTeamHandler>().Handle(
            new UnlinkTeamCommand(Id(large.OrganizationId), Id(large.Project), Id(large.Team), new UnlinkDestination(Id(large.TeamIds[1]))), ct));

        Assert.Equal(smallCount, largeCount);
        Assert.Equal(UnlinkStatements, largeCount);
        Assert.Equal(
            ["5000"],
            await database.QueryAsync("SELECT count(*)::text FROM work_items WHERE owning_team_id = @team", ("team", large.TeamIds[1])));
    }

    // The deletion: 50 items over 2 projects when it is 5 000 over 50.
    [Fact]
    public async Task Deletion_TheSameNumberOfStatements_For2ProjectsAnd50ItemsAs50ProjectsAnd5000()
    {
        var small = await SeedAsync(projects: 2, teams: 1, linksPerTeam: 2);
        var large = await SeedAsync(projects: 50, teams: 1, linksPerTeam: 50);
        await SeedItemsAsync(small, small.Team, 50);
        await SeedItemsAsync(large, large.Team, 5_000);

        var smallCount = await CountAsync((p, ct) => p.GetRequiredService<DeleteTeamHandler>().Handle(new DeleteTeamCommand(Id(small.OrganizationId), Id(small.Team)), ct));
        var largeCount = await CountAsync((p, ct) => p.GetRequiredService<DeleteTeamHandler>().Handle(new DeleteTeamCommand(Id(large.OrganizationId), Id(large.Team)), ct));

        Assert.Equal(smallCount, largeCount);
        Assert.Equal(DeletionStatements, largeCount);
    }

    // The order of the locks (plan-backend § 7.4): the unlink takes the links of the pair in ONE statement ordered by team id, and no
    // statement of it locks a team row (D-5).
    [Fact]
    public async Task Unlink_LocksThePairOfLinksInOneOrderedStatement_AndNoTeamRow()
    {
        var seeded = await SeedAsync(projects: 1, teams: 2, linksPerTeam: 1);
        var log = new SqlLogInterceptor();
        await using var provider = BuildServices(log);
        await using var scope = provider.CreateAsyncScope();

        await scope.ServiceProvider.GetRequiredService<UnlinkTeamHandler>().Handle(
            new UnlinkTeamCommand(Id(seeded.OrganizationId), Id(seeded.Project), Id(seeded.Team), new UnlinkDestination(Id(seeded.TeamIds[1]))), CancellationToken.None);

        var linkLocks = log.Commands.Where(sql => sql.Contains("FROM project_teams", StringComparison.Ordinal) && sql.Contains("FOR NO KEY UPDATE", StringComparison.Ordinal)).ToList();
        Assert.Single(linkLocks);
        Assert.Contains("ORDER BY", linkLocks[0], StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(log.Commands, sql => sql.Contains("FROM teams", StringComparison.Ordinal) && sql.Contains("FOR ", StringComparison.Ordinal) && sql.Contains("UPDATE", StringComparison.Ordinal));
    }

    // mod-005 QD-1 · a parent write locks the hierarchy, then its child and its parents (not by id); the clean-ups lock the owned items by
    // id. They take the hierarchy lock of the projects they reach BEFORE the first statement that locks an item — in two statements (the
    // missing rows inserted in project-id order, then every row locked FOR UPDATE ordered by project id), whatever the number of projects.
    [Fact]
    public async Task Unlink_TakesTheProjectsHierarchyLock_BeforeLockingTheOwnedItems()
    {
        var seeded = await SeedAsync(projects: 1, teams: 2, linksPerTeam: 1);
        await SeedItemsAsync(seeded, seeded.Team, 3);
        var log = new SqlLogInterceptor();
        await using var provider = BuildServices(log);
        await using var scope = provider.CreateAsyncScope();

        await scope.ServiceProvider.GetRequiredService<UnlinkTeamHandler>().Handle(
            new UnlinkTeamCommand(Id(seeded.OrganizationId), Id(seeded.Project), Id(seeded.Team), new UnlinkDestination(Id(seeded.TeamIds[1]))), CancellationToken.None);

        AssertHierarchyBeforeItems(log.Commands, projects: 1);
    }

    [Fact]
    public async Task Deletion_TakesTheHierarchyLocksOfAllTheLinkedProjectsInTwoStatements_BeforeLockingTheOwnedItems()
    {
        var seeded = await SeedAsync(projects: 3, teams: 1, linksPerTeam: 3);
        await SeedItemsAsync(seeded, seeded.Team, 6);
        var log = new SqlLogInterceptor();
        await using var provider = BuildServices(log);
        await using var scope = provider.CreateAsyncScope();

        await scope.ServiceProvider.GetRequiredService<DeleteTeamHandler>().Handle(new DeleteTeamCommand(Id(seeded.OrganizationId), Id(seeded.Team)), CancellationToken.None);

        AssertHierarchyBeforeItems(log.Commands, projects: 3);

        // Every linked project has its row now, the ones that had never had a parent write included.
        Assert.Equal(
            ["3"],
            await database.QueryAsync("SELECT count(*)::text FROM work_item_hierarchy_locks WHERE project_id = ANY(@projects)", ("projects", seeded.ProjectIds.ToArray())));
    }

    private static void AssertHierarchyBeforeItems(List<string> commands, int projects)
    {
        var inserts = commands.Where(sql => sql.Contains("INSERT INTO work_item_hierarchy_locks", StringComparison.Ordinal)).ToList();
        var locks = commands.Where(sql => sql.Contains("FROM work_item_hierarchy_locks", StringComparison.Ordinal) && sql.Contains("FOR UPDATE", StringComparison.Ordinal)).ToList();
        var items = commands.Where(sql => sql.Contains("FROM work_items", StringComparison.Ordinal) && sql.Contains("FOR UPDATE", StringComparison.Ordinal)).ToList();

        // One insert and one lock, each ordered by project id, for any number of projects (the pins above count the same two).
        Assert.True(inserts.Count == 1 && locks.Count == 1, $"The hierarchy lock of {projects} project(s) took {inserts.Count} insert(s) and {locks.Count} lock(s), not 1 and 1.");
        Assert.Contains("ORDER BY project_id", inserts[0], StringComparison.Ordinal);
        Assert.Contains("ORDER BY project_id", locks[0], StringComparison.Ordinal);

        var item = Assert.Single(items);
        Assert.True(
            commands.IndexOf(inserts[0]) < commands.IndexOf(locks[0]) && commands.IndexOf(locks[0]) < commands.IndexOf(item),
            "The hierarchy lock must be taken, inserts first, before the statement that locks the owned items.");
    }

    // --- Arrangement -------------------------------------------------------------------------------------------------

    private static string Id(Guid id) => id.ToString();

    private async Task<int> CountAsync(Func<IServiceProvider, CancellationToken, Task> run)
    {
        var counter = new SqlLogInterceptor();
        await using var provider = BuildServices(counter);
        await using var scope = provider.CreateAsyncScope();

        await run(scope.ServiceProvider, CancellationToken.None);

        foreach (var statement in counter.Commands)
        {
            output.WriteLine(string.Join(" ", statement.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Take(14)));
        }

        output.WriteLine("---");

        return counter.Commands.Count;
    }

    private ServiceProvider BuildServices(IInterceptor interceptor)
    {
        var services = new ServiceCollection();

        services.AddDbContext<MustaskDbContext>(options => options.UseNpgsql(database.ConnectionString).AddInterceptors(interceptor));
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IUserDirectory, UserDirectoryReadModel>();
        services.AddScoped<IProcessDefinitionReadModel, ProcessDefinitionReadModel>();
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<ICurrentUser>(new FixedUser());
        services.AddScoped<IOrganizationAccess, AdministratorAccess>();
        services.AddApplication();
        services.AddOrganizationsInfrastructure();
        services.AddWorkItemsInfrastructure();
        services.AddBoardsInfrastructure();

        return services.BuildServiceProvider();
    }

    private sealed class FixedUser : ICurrentUser
    {
        public string DisplayName => "Count Caller";

        public string SubjectId => Caller;
    }

    /// <summary>The caller as an administrator of the organization: the handlers ask for their second codes.</summary>
    private sealed class AdministratorAccess : IOrganizationAccess
    {
        public OrganizationRole Role => OrganizationRole.Admin;

        public bool Holds(string code) => OrganizationRoleBundles.Holds(Role, code);
    }

    /// <summary>
    /// An organization of <paramref name="projects"/> projects and <paramref name="teams"/> teams; team n is linked to the
    /// <paramref name="linksPerTeam"/> projects n, n + 1, … (modulo the number of projects). Written through the domain in one context.
    /// </summary>
    private async Task<Seeded> SeedAsync(int projects, int teams, int linksPerTeam = 2)
    {
        var first = await database.CreateStructureAsync();
        var organization = first.Organization.Id;
        var projectIds = new List<Guid> { first.Project.Project.Id };
        var rootId = first.Project.Root.Id;

        // CreateStructureAsync brings two teams linked to its project; the counts here are the test's own, so they go.
        await database.ExecuteAsync("DELETE FROM project_teams WHERE organization_id = @organization; DELETE FROM teams WHERE organization_id = @organization", ("organization", organization));

        await using var context = database.NewContext();

        for (var index = 1; index < projects; index++)
        {
            var project = Project.Create(organization, OrganizationsDatabaseFixture.Name($"Проект {index:D3}"), $"P{index:D3}", OrganizationsDatabaseFixture.Now, OrganizationsDatabaseFixture.Creator);
            context.Projects.Add(project.Project);
            context.Areas.Add(project.Root);
            projectIds.Add(project.Project.Id);
        }

        var teamIds = new List<Guid>();

        for (var index = 0; index < teams; index++)
        {
            var team = Team.Create(organization, OrganizationsDatabaseFixture.Name($"Команда {index:D3}"));
            context.Teams.Add(team);
            teamIds.Add(team.Id);

            for (var link = 0; link < Math.Min(linksPerTeam, projects); link++)
            {
                context.ProjectTeams.Add(ProjectTeam.Link(
                    organization, projectIds[(index + link) % projects], team.Id, OrganizationsDatabaseFixture.Creator, OrganizationsDatabaseFixture.Now));
            }
        }

        await context.SaveChangesAsync();

        return new Seeded(organization, projectIds, teamIds, rootId);
    }

    /// <summary><paramref name="count"/> stories owned by <paramref name="team"/>, spread over the projects it is linked to.</summary>
    private async Task SeedItemsAsync(Seeded seeded, Guid team, int count)
    {
        var row = (await database.QueryAsync(
            """
            SELECT concat_ws('|', type.id::text, state.id::text)
            FROM work_item_types AS type
            JOIN processes AS process ON process.id = type.process_id
            JOIN state_definitions AS state ON state.work_item_type_id = type.id AND state.position = 1
            WHERE process.organization_id = @organization AND type.key = 'US'
            """,
            ("organization", seeded.OrganizationId))).Single().Split('|');
        var linked = (await database.QueryAsync("SELECT project_id::text FROM project_teams WHERE team_id = @team ORDER BY project_id", ("team", team))).Select(Guid.Parse).ToList();

        foreach (var project in linked)
        {
            var root = (await database.QueryAsync("SELECT id::text FROM areas WHERE project_id = @project AND parent_id IS NULL", ("project", project))).Single();

            await database.ExecuteAsync(
                """
                INSERT INTO work_items (id, project_id, number, type_id, state_id, area_id, title, description, creation_request_id, created_at, created_by, changed_at, owning_team_id)
                SELECT gen_random_uuid(), @project, n, @type, @state, @root, 'Элемент ' || n, '', gen_random_uuid(), now(), 'count-test', now(), @team
                FROM generate_series(1, @count) AS n;
                INSERT INTO work_item_counters (project_id, last_number) VALUES (@project, @count)
                """,
                ("project", project),
                ("type", Guid.Parse(row[0])),
                ("state", Guid.Parse(row[1])),
                ("root", Guid.Parse(root)),
                ("team", team),
                ("count", Math.Max(1, count / linked.Count)));
        }
    }

    /// <summary><paramref name="count"/> people who are members of the organization and known to the directory; their ids.</summary>
    private async Task<IReadOnlyList<string?>> SeedMembersAsync(Guid organizationId, int count)
    {
        var ids = Enumerable.Range(0, count).Select(index => $"count-member-{Guid.NewGuid():N}-{index}").ToList();

        await database.SeedUsersAsync(ids.Select(id => (id, "Имя", "Фамилия", $"{id}@example.test", true)).ToArray());
        await using var context = database.NewContext();
        context.OrganizationMemberships.AddRange(
            ids.Select(id => OrganizationMembership.Add(organizationId, id, OrganizationRole.Member, OrganizationsDatabaseFixture.Creator, OrganizationsDatabaseFixture.Now)));
        await context.SaveChangesAsync();

        return ids;
    }
}
