using System.Data.Common;
using Mustask.Application;
using Mustask.Application.Abstractions;
using Mustask.Application.Abstractions.Process;
using Mustask.Application.Abstractions.UserDirectory;
using Mustask.Application.Features.Boards.GetBacklog;
using Mustask.Application.Features.Boards.GetBoard;
using Mustask.Application.Features.Boards.GetProjectBacklog;
using Mustask.Application.Features.Boards.GetProjectBoard;
using Mustask.Application.Features.Boards.ReorderBacklog;
using Mustask.Application.Features.Organizations.DeleteTeam;
using Mustask.Application.Features.Organizations.GetTeamOwnedItemCount;
using Mustask.Application.Features.Organizations.GetTeams;
using Mustask.Application.Features.WorkItems.GetWorkItems;
using Mustask.Domain.Organizations;
using Mustask.Infrastructure.Process;
using Mustask.Infrastructure.Tests.Organizations;
using Mustask.Infrastructure.UserDirectory;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace Mustask.Infrastructure.Tests.WorkItems;

/// <summary>
/// mod-017 plan-backend § 9.4, "Statement counts" (NFR Volumes, brief § 14): the reads and the writes of the owning team take a
/// number of statements that does not grow with the items, the teams or the depth of the tree. Each use case runs through its real
/// handler against a real PostgreSQL for a project of 50 items per level and two teams, and for one of 1 250 items per level (5 000
/// in all) and fifty teams, linked four levels deep and owned by the teams in turn; every statement the context sends is counted.
/// The two counts are equal, and equal to the number the test pins, so a statement added is a decision, not an accident. A filter
/// adds no statement: the filtered list, backlog and board take the statements of the unfiltered ones.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(OrganizationsDatabaseCollection.Name)]
public sealed class OwningTeamStatementCountTests(OrganizationsDatabaseFixture database, Xunit.Abstractions.ITestOutputHelper output)
{
    private const int SmallPerLevel = 50;
    private const int LargePerLevel = 1_250;
    private const string User = "count-user-1";

    // Pinned: a change to one of these is a change to the query shape and is made on purpose (the numbers are the first run's).
    private const int ListStatements = 7;
    private const int TeamBacklogStatements = 11;
    private const int TeamBoardStatements = 13;
    private const int TeamsListStatements = 1;
    private const int OwnedCountStatements = 3;
    private const int DeletionStatements = 9;
    private const int ReorderStatements = 15;

    [Fact]
    public async Task List_Unfiltered_Team_And_None_TheSameNumberOfStatements_For50AsFor5000Items()
    {
        var (small, large) = await SeedBothAsync();

        foreach (var filter in new IReadOnlyList<string?>?[] { null, [Id(small.OwnTeam)], ["none"], [Id(small.OwnTeam), "none"] })
        {
            var smallCount = await CountAsync((provider, ct) => provider.GetRequiredService<GetWorkItemsHandler>()
                .Handle(new GetWorkItemsQuery(small.OrganizationId.ToString(), small.ProjectId.ToString(), 1, Own(filter, small, large, small)), ct));
            var largeCount = await CountAsync((provider, ct) => provider.GetRequiredService<GetWorkItemsHandler>()
                .Handle(new GetWorkItemsQuery(large.OrganizationId.ToString(), large.ProjectId.ToString(), 1, Own(filter, small, large, large)), ct));

            Assert.Equal(smallCount, largeCount);
            Assert.Equal(ListStatements, largeCount);
        }
    }

    [Fact]
    public async Task TeamBacklog_StoriesAndEpics_TheSameNumberOfStatements_For50AsFor5000Items()
    {
        var (small, large) = await SeedBothAsync();

        var smallStories = await CountAsync((provider, ct) => provider.GetRequiredService<GetBacklogHandler>()
            .Handle(new GetBacklogQuery(small.OrganizationId.ToString(), small.ProjectId.ToString(), Id(small.OwnTeam), "stories"), ct));
        var largeStories = await CountAsync((provider, ct) => provider.GetRequiredService<GetBacklogHandler>()
            .Handle(new GetBacklogQuery(large.OrganizationId.ToString(), large.ProjectId.ToString(), Id(large.OwnTeam), "stories"), ct));
        var smallEpics = await CountAsync((provider, ct) => provider.GetRequiredService<GetBacklogHandler>()
            .Handle(new GetBacklogQuery(small.OrganizationId.ToString(), small.ProjectId.ToString(), Id(small.OwnTeam), "epics"), ct));
        var largeEpics = await CountAsync((provider, ct) => provider.GetRequiredService<GetBacklogHandler>()
            .Handle(new GetBacklogQuery(large.OrganizationId.ToString(), large.ProjectId.ToString(), Id(large.OwnTeam), "epics"), ct));

        Assert.Equal(smallStories, largeStories);
        Assert.Equal(smallEpics, largeEpics);
        Assert.Equal(TeamBacklogStatements, largeStories);
    }

    [Fact]
    public async Task TeamBoard_TheSameNumberOfStatements_For50AsFor5000Items()
    {
        var (small, large) = await SeedBothAsync();

        var smallBoard = await CountAsync((provider, ct) => provider.GetRequiredService<GetBoardHandler>()
            .Handle(new GetBoardQuery(small.OrganizationId.ToString(), small.ProjectId.ToString(), Id(small.OwnTeam), "stories"), ct));
        var largeBoard = await CountAsync((provider, ct) => provider.GetRequiredService<GetBoardHandler>()
            .Handle(new GetBoardQuery(large.OrganizationId.ToString(), large.ProjectId.ToString(), Id(large.OwnTeam), "stories"), ct));

        Assert.Equal(smallBoard, largeBoard);
        Assert.Equal(TeamBoardStatements, largeBoard);
    }

    // A filter adds no statement: the project's backlog and board, filtered by a team, by none and by both, take the statements of
    // the unfiltered ones (mod-017 BR-OT-8).
    [Fact]
    public async Task ProjectBacklogAndBoard_Filtered_TakeTheStatementsOfTheUnfiltered()
    {
        var (_, large) = await SeedBothAsync();
        var unfilteredBacklog = await CountAsync((provider, ct) => provider.GetRequiredService<GetProjectBacklogHandler>()
            .Handle(new GetProjectBacklogQuery(large.OrganizationId.ToString(), large.ProjectId.ToString(), "stories"), ct));
        var unfilteredBoard = await CountAsync((provider, ct) => provider.GetRequiredService<GetProjectBoardHandler>()
            .Handle(new GetProjectBoardQuery(large.OrganizationId.ToString(), large.ProjectId.ToString(), "stories"), ct));

        foreach (var filter in new IReadOnlyList<string?>[] { [Id(large.OwnTeam)], ["none"], [Id(large.OwnTeam), "none"] })
        {
            var backlog = await CountAsync((provider, ct) => provider.GetRequiredService<GetProjectBacklogHandler>()
                .Handle(new GetProjectBacklogQuery(large.OrganizationId.ToString(), large.ProjectId.ToString(), "stories", filter), ct));
            var board = await CountAsync((provider, ct) => provider.GetRequiredService<GetProjectBoardHandler>()
                .Handle(new GetProjectBoardQuery(large.OrganizationId.ToString(), large.ProjectId.ToString(), "stories", filter), ct));

            Assert.Equal(unfilteredBacklog, backlog);
            Assert.Equal(unfilteredBoard, board);
        }
    }

    [Fact]
    public async Task TeamsList_TheSameNumberOfStatements_For2AsFor50Teams()
    {
        var (small, large) = await SeedBothAsync();

        var smallList = await CountAsync((provider, ct) => provider.GetRequiredService<GetTeamsHandler>()
            .Handle(new GetTeamsQuery(small.OrganizationId.ToString(), small.ProjectId.ToString()), ct));
        var largeList = await CountAsync((provider, ct) => provider.GetRequiredService<GetTeamsHandler>()
            .Handle(new GetTeamsQuery(large.OrganizationId.ToString(), large.ProjectId.ToString()), ct));

        Assert.Equal(smallList, largeList);
        Assert.Equal(TeamsListStatements, largeList);
    }

    [Fact]
    public async Task OwnedCount_TheSameNumberOfStatements_For50AsFor5000Items()
    {
        var (small, large) = await SeedBothAsync();

        var smallCount = await CountAsync((provider, ct) => provider.GetRequiredService<GetTeamOwnedItemCountHandler>()
            .Handle(new GetTeamOwnedItemCountQuery(small.OrganizationId.ToString(), small.ProjectId.ToString(), Id(small.OwnTeam)), ct));
        var largeCount = await CountAsync((provider, ct) => provider.GetRequiredService<GetTeamOwnedItemCountHandler>()
            .Handle(new GetTeamOwnedItemCountQuery(large.OrganizationId.ToString(), large.ProjectId.ToString(), Id(large.OwnTeam)), ct));

        Assert.Equal(smallCount, largeCount);
        Assert.Equal(OwnedCountStatements, largeCount);
    }

    // The team deletion — the locks of every team, the owned items, the places, the release, the setting, the memberships, the
    // status — takes the statements of a small project when the project has fifty teams and five thousand items.
    [Fact]
    public async Task Deletion_TheSameNumberOfStatements_For50ItemsAnd2AsFor5000ItemsAnd50Teams()
    {
        var (small, large) = await SeedBothAsync();

        var smallCount = await CountAsync((provider, ct) => provider.GetRequiredService<DeleteTeamHandler>()
            .Handle(new DeleteTeamCommand(small.OrganizationId.ToString(), small.ProjectId.ToString(), Id(small.OwnTeam)), ct));
        var largeCount = await CountAsync((provider, ct) => provider.GetRequiredService<DeleteTeamHandler>()
            .Handle(new DeleteTeamCommand(large.OrganizationId.ToString(), large.ProjectId.ToString(), Id(large.OwnTeam)), ct));

        Assert.Equal(smallCount, largeCount);
        Assert.Equal(DeletionStatements, largeCount);
    }

    [Fact]
    public async Task TeamReorder_TheSameNumberOfStatements_For50AsFor5000Items()
    {
        var (small, large) = await SeedBothAsync();

        var smallReorder = await CountAsync((provider, ct) => provider.GetRequiredService<ReorderBacklogHandler>()
            .Handle(new ReorderBacklogCommand(small.OrganizationId.ToString(), small.ProjectId.ToString(), Id(small.OwnTeam), "stories", small.FirstOwnStory.ToString(), "bottom", null), ct));
        var largeReorder = await CountAsync((provider, ct) => provider.GetRequiredService<ReorderBacklogHandler>()
            .Handle(new ReorderBacklogCommand(large.OrganizationId.ToString(), large.ProjectId.ToString(), Id(large.OwnTeam), "stories", large.FirstOwnStory.ToString(), "bottom", null), ct));

        Assert.Equal(smallReorder, largeReorder);
        Assert.Equal(ReorderStatements, largeReorder);
    }

    // --- Arrangement -------------------------------------------------------------------------------------------------

    private static string Id(Guid id) => id.ToString();

    /// <summary>The filter with the team of the project it is sent to (the ids differ between the two projects).</summary>
    private static IReadOnlyList<string?>? Own(IReadOnlyList<string?>? filter, Seeded small, Seeded large, Seeded target) =>
        filter?.Select(value => value == Id(small.OwnTeam) ? Id(target.OwnTeam) : value).ToList();

    private async Task<(Seeded Small, Seeded Large)> SeedBothAsync() =>
        (await SeedAsync(SmallPerLevel, teams: 2), await SeedAsync(LargePerLevel, teams: 50));

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
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<ICurrentUser>(new FixedUser());
        services.AddApplication();
        services.AddOrganizationsInfrastructure();
        services.AddWorkItemsInfrastructure();
        services.AddBoardsInfrastructure();

        return services.BuildServiceProvider();
    }

    private sealed class FixedUser : ICurrentUser
    {
        public string DisplayName => "Count User";

        public string SubjectId => User;
    }

    private sealed record Seeded(Guid OrganizationId, Guid ProjectId, Guid OwnTeam, Guid FirstOwnStory);

    /// <summary>
    /// A project of <paramref name="perLevel"/> items on each of the four levels, item n beneath item n − perLevel of the level
    /// above, owned by the project's <paramref name="teams"/> teams in turn and by none every seventh, with three assignees spread
    /// over them. <see cref="Seeded.OwnTeam"/> is the project's own team, which owns the first item of every level.
    /// </summary>
    private async Task<Seeded> SeedAsync(int perLevel, int teams)
    {
        var structure = await database.CreateStructureAsync();
        var organization = structure.Organization.Id;
        var project = structure.Project.Project.Id;
        var teamIds = new List<Guid> { structure.Project.Team.Id, structure.SecondTeam.Id };

        await using (var context = database.NewContext())
        {
            for (var index = teamIds.Count; index < teams; index++)
            {
                var team = Team.Create(project, OrganizationsDatabaseFixture.Name($"Команда {index:D2}"));
                context.Teams.Add(team);
                teamIds.Add(team.Id);
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
                INSERT INTO work_items (id, project_id, number, type_id, state_id, area_id, title, description, creation_request_id,
                                        created_at, created_by, changed_at, parent_id, assignee_id, owning_team_id)
                SELECT gen_random_uuid(), @project, n, @type, @state, @rootArea,
                       'Элемент ' || n, '', gen_random_uuid(), now() + n * interval '1 millisecond', 'count-test', now(), parent.id,
                       CASE WHEN n % 5 < 3 THEN (ARRAY['count-user-1', 'count-user-2', 'count-user-3'])[(n % 5) + 1] END,
                       CASE WHEN n % 7 = 6 THEN NULL ELSE (@teams)[((n - @from) % @teamCount) + 1] END
                FROM generate_series(@from, @to) AS n
                LEFT JOIN work_items AS parent ON parent.project_id = @project AND parent.number = n - @perLevel AND @level > 0
                """,
                ("project", project),
                ("type", type),
                ("state", state),
                ("rootArea", structure.Project.Root.Id),
                ("teams", teamIds.ToArray()),
                ("teamCount", teamIds.Count),
                ("from", from),
                ("to", from + perLevel - 1),
                ("perLevel", perLevel),
                ("level", level));
        }

        var story = await database.QueryAsync(
            "SELECT id::text FROM work_items WHERE project_id = @project AND number = @number",
            ("project", project),
            ("number", (2 * perLevel) + 1));

        return new Seeded(organization, project, structure.Project.Team.Id, Guid.Parse(story.Single()));
    }

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
