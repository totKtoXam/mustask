using System.Data.Common;
using Mustask.Application;
using Mustask.Application.Abstractions;
using Mustask.Application.Abstractions.Organizations;
using Mustask.Application.Abstractions.Process;
using Mustask.Application.Abstractions.UserDirectory;
using Mustask.Application.Features.Boards.GetBacklog;
using Mustask.Application.Features.Boards.GetBoard;
using Mustask.Application.Features.Boards.ReorderBacklog;
using Mustask.Domain.Identity;
using Mustask.Domain.Organizations;
using Mustask.Infrastructure.Process;
using Mustask.Infrastructure.Tests.Organizations;
using Mustask.Infrastructure.UserDirectory;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace Mustask.Infrastructure.Tests.Boards;

/// <summary>
/// feat-040 spec § Non-functional requirements, "Query shape", and US-7 "A team-view read does not grow its statements with its
/// data" (brief §§ 14, 17; BR-TS-13): reading a team's backlog or board, filtered or not, and reordering a team's backlog take a
/// number of statements that does not grow with the rows, the projects, the filter's values or the depth of the tree. Each surface
/// runs through its real handler against a real PostgreSQL for a team of one project and 10 items per level, and for one of 50
/// projects and 5 000 items (25 on each of the four levels of each project, linked four levels deep), with a few assignees and ten values in
/// the project filter; every statement the context sends is counted. The two counts are equal, and equal to the number this test
/// pins, so a statement added to a read is a decision, not an accident. They replace mod-017's one-project counts of the same
/// surfaces (<c>OwningTeamStatementCountTests</c>, retired by name).
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(OrganizationsDatabaseCollection.Name)]
public sealed class TeamViewStatementCountTests(OrganizationsDatabaseFixture database, Xunit.Abstractions.ITestOutputHelper output)
{
    private const int SmallProjects = 1;
    private const int SmallPerLevel = 10;
    private const int LargeProjects = 50;
    private const int LargePerLevel = 25;
    private const int FilterValues = 10;

    // Pinned: a change to one of these is a change to the query shape and is made on purpose (the numbers are the first run's).
    // A backlog read: the team (1), the process (3), the team's linked projects (1), the tree set (1), its assignees (one lookup and
    // one membership: 2), the parents outside the set (1, none on the top level: «Эпики» has no parent outside), and the team again
    // (1). A board: the same without the parents, plus the checklists (1) and their assignees (2). A reorder: the handler's reads
    // (team, process: 4), the transaction's locks (team, active check, members, order row: 5), the entries (1), the drop and the
    // upsert (2).
    private const int StoriesBacklogStatements = 10;
    private const int EpicsBacklogStatements = 9;
    private const int BoardStatements = 12;
    private const int ReorderStatements = 12;

    [Fact]
    public async Task Backlog_StoriesAndEpics_TheSameNumberOfStatements_ForOneProjectAsForFifty_FilteredToo()
    {
        var small = await SeedAsync(SmallProjects, SmallPerLevel);
        var large = await SeedAsync(LargeProjects, LargePerLevel);

        var smallStories = await CountAsync((provider, ct) => provider.GetRequiredService<GetBacklogHandler>()
            .Handle(new GetBacklogQuery(small.OrganizationId.ToString(), small.Team.ToString(), "stories"), ct));
        var largeStories = await CountAsync((provider, ct) => provider.GetRequiredService<GetBacklogHandler>()
            .Handle(new GetBacklogQuery(large.OrganizationId.ToString(), large.Team.ToString(), "stories"), ct));
        var smallEpics = await CountAsync((provider, ct) => provider.GetRequiredService<GetBacklogHandler>()
            .Handle(new GetBacklogQuery(small.OrganizationId.ToString(), small.Team.ToString(), "epics"), ct));
        var largeEpics = await CountAsync((provider, ct) => provider.GetRequiredService<GetBacklogHandler>()
            .Handle(new GetBacklogQuery(large.OrganizationId.ToString(), large.Team.ToString(), "epics"), ct));
        var filtered = await CountAsync((provider, ct) => provider.GetRequiredService<GetBacklogHandler>()
            .Handle(new GetBacklogQuery(large.OrganizationId.ToString(), large.Team.ToString(), "stories", large.Projects.Take(FilterValues).Select(id => (string?)id.ToString()).ToList()), ct));

        Assert.Equal(smallStories, largeStories);
        Assert.Equal(smallEpics, largeEpics);
        Assert.Equal(StoriesBacklogStatements, largeStories);
        Assert.Equal(EpicsBacklogStatements, largeEpics);
        Assert.Equal(largeStories, filtered);
    }

    [Fact]
    public async Task Board_TheSameNumberOfStatements_ForOneProjectAsForFifty_FilteredToo()
    {
        var small = await SeedAsync(SmallProjects, SmallPerLevel);
        var large = await SeedAsync(LargeProjects, LargePerLevel);

        var smallBoard = await CountAsync((provider, ct) => provider.GetRequiredService<GetBoardHandler>()
            .Handle(new GetBoardQuery(small.OrganizationId.ToString(), small.Team.ToString(), "stories"), ct));
        var largeBoard = await CountAsync((provider, ct) => provider.GetRequiredService<GetBoardHandler>()
            .Handle(new GetBoardQuery(large.OrganizationId.ToString(), large.Team.ToString(), "stories"), ct));
        var filtered = await CountAsync((provider, ct) => provider.GetRequiredService<GetBoardHandler>()
            .Handle(new GetBoardQuery(large.OrganizationId.ToString(), large.Team.ToString(), "stories", large.Projects.Take(FilterValues).Select(id => (string?)id.ToString()).ToList()), ct));

        Assert.Equal(smallBoard, largeBoard);
        Assert.Equal(BoardStatements, largeBoard);
        Assert.Equal(largeBoard, filtered);
    }

    /// <summary>The reorder of a level of 5 000 items over 50 projects takes the statements of one of 10 items: the locks, the read, the drop and one upsert.</summary>
    [Fact]
    public async Task Reorder_TheSameNumberOfStatements_ForOneProjectAsForFifty()
    {
        var small = await SeedAsync(SmallProjects, SmallPerLevel);
        var large = await SeedAsync(LargeProjects, LargePerLevel);

        var smallReorder = await CountAsync((provider, ct) => provider.GetRequiredService<ReorderBacklogHandler>()
            .Handle(new ReorderBacklogCommand(small.OrganizationId.ToString(), small.Team.ToString(), "stories", small.FirstStory.ToString(), "bottom", null), ct));
        var largeReorder = await CountAsync((provider, ct) => provider.GetRequiredService<ReorderBacklogHandler>()
            .Handle(new ReorderBacklogCommand(large.OrganizationId.ToString(), large.Team.ToString(), "stories", large.FirstStory.ToString(), "bottom", null), ct));

        Assert.Equal(smallReorder, largeReorder);
        Assert.Equal(ReorderStatements, largeReorder);
    }

    private async Task<int> CountAsync(Func<IServiceProvider, CancellationToken, Task> run)
    {
        var counter = new StatementCounter();
        await using var provider = BuildServices(counter);
        await using var scope = provider.CreateAsyncScope();

        await run(scope.ServiceProvider, CancellationToken.None);

        foreach (var statement in counter.Statements)
        {
            output.WriteLine(string.Join(" ", statement.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Take(14)));
        }

        output.WriteLine("---");

        return counter.Statements.Count;
    }

    private ServiceProvider BuildServices(IInterceptor interceptor)
    {
        var services = new ServiceCollection();

        services.AddDbContext<MustaskDbContext>(options => options.UseNpgsql(database.ConnectionString).AddInterceptors(interceptor));
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IUserDirectory, UserDirectoryReadModel>();
        services.AddScoped<IProcessDefinitionReadModel, ProcessDefinitionReadModel>();
        services.AddSingleton<ICurrentUser>(new FixedUser());
        services.AddScoped<IOrganizationAccess, AdministratorAccess>();
        services.AddApplication();
        services.AddOrganizationsInfrastructure();
        services.AddBoardsInfrastructure();

        return services.BuildServiceProvider();
    }

    private sealed class FixedUser : ICurrentUser
    {
        public string DisplayName => "Count User";

        public string SubjectId => "count-user-1";
    }

    /// <summary>The caller as an administrator of the organization (the scoped check's result for the handlers that read the role).</summary>
    private sealed class AdministratorAccess : IOrganizationAccess
    {
        public OrganizationRole Role => OrganizationRole.Admin;

        public bool Holds(string code) => OrganizationRoleBundles.Holds(Role, code);
    }

    /// <summary>
    /// A team linked to <paramref name="projects"/> projects, owning <paramref name="perLevel"/> items on each of the four levels
    /// in each of them, item n beneath item n − perLevel of the level above in the same project, with three assignees spread over
    /// them. <see cref="Seeded.FirstStory"/> is a story of the first project.
    /// </summary>
    private async Task<Seeded> SeedAsync(int projects, int perLevel)
    {
        var structure = await database.CreateStructureAsync();
        var organization = structure.Organization.Id;
        var team = structure.FirstTeam.Id;
        var projectIds = new List<Guid> { structure.Project.Id };

        // The further projects, their roots and their links to the team, in one save.
        await using (var context = database.NewContext())
        {
            for (var index = 1; index < projects; index++)
            {
                var project = Project.Create(
                    organization, OrganizationsDatabaseFixture.Name($"Проект {index:D2}"), $"CT{index:D2}", OrganizationsDatabaseFixture.Now, OrganizationsDatabaseFixture.Creator);
                context.Projects.Add(project);
                context.ProjectTeams.Add(
                    ProjectTeam.Link(organization, project.Id, team, OrganizationsDatabaseFixture.Creator, OrganizationsDatabaseFixture.Now));
                projectIds.Add(project.Id);
            }

            await context.SaveChangesAsync();
        }

        var types = new Dictionary<string, (Guid Type, Guid State)>();

        foreach (var row in await database.QueryAsync(
            """
            SELECT concat_ws('|', type.key, type.id::text, state.id::text)
            FROM work_item_types AS type
            JOIN processes AS process ON process.id = type.process_id
            JOIN state_definitions AS state ON state.work_item_type_id = type.id AND state.position = 1
            WHERE process.organization_id = @organization
            """,
            ("organization", organization)))
        {
            var parts = row.Split('|');
            types[parts[0]] = (Guid.Parse(parts[1]), Guid.Parse(parts[2]));
        }

        var levels = new[] { "EPIC", "FEAT", "US", "TASK" };

        for (var level = 0; level < levels.Length; level++)
        {
            var (type, state) = types[levels[level]];
            var from = (level * perLevel) + 1;

            await database.ExecuteAsync(
                """
                INSERT INTO work_items (id, project_id, number, type_id, state_id, title, description, creation_request_id,
                                        created_at, created_by, changed_at, parent_id, assignee_id, owning_team_id)
                SELECT gen_random_uuid(), project.id, n, @type, @state, 'Элемент ' || n, '', gen_random_uuid(), now() + n * interval '1 millisecond', 'count-test', now(), parent.id,
                       CASE WHEN n % 7 < 3 THEN (ARRAY['count-user-1', 'count-user-2', 'count-user-3'])[(n % 7) + 1] END,
                       @team
                FROM unnest(@projects) AS project(id)
                JOIN areas AS root ON root.project_id = project.id AND root.parent_id IS NULL
                CROSS JOIN generate_series(@from, @to) AS n
                LEFT JOIN work_items AS parent ON parent.project_id = project.id AND parent.number = n - @perLevel AND @level > 0
                """,
                ("projects", projectIds.ToArray()),
                ("type", type),
                ("state", state),
                ("team", team),
                ("from", from),
                ("to", from + perLevel - 1),
                ("perLevel", perLevel),
                ("level", level));
        }

        var story = await database.QueryAsync(
            "SELECT id::text FROM work_items WHERE project_id = @project AND number = @number",
            ("project", projectIds[0]),
            ("number", (2 * perLevel) + 1));

        return new Seeded(organization, team, projectIds, Guid.Parse(story.Single()));
    }

    private sealed record Seeded(Guid OrganizationId, Guid Team, IReadOnlyList<Guid> Projects, Guid FirstStory);

    /// <summary>Records the text of every statement a context sends, reads and writes alike.</summary>
    private sealed class StatementCounter : DbCommandInterceptor
    {
        public List<string> Statements { get; } = [];

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            Statements.Add(command.CommandText);

            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            Statements.Add(command.CommandText);

            return base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
        }

        public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<object> result, CancellationToken cancellationToken = default)
        {
            Statements.Add(command.CommandText);

            return base.ScalarExecutingAsync(command, eventData, result, cancellationToken);
        }
    }
}
