using System.Data.Common;
using Mustask.Application;
using Mustask.Application.Abstractions;
using Mustask.Application.Abstractions.Process;
using Mustask.Application.Abstractions.UserDirectory;
using Mustask.Application.Features.Boards.GetProjectBacklog;
using Mustask.Application.Features.Boards.GetProjectBoard;
using Mustask.Application.Features.Boards.ReorderProjectBacklog;
using Mustask.Infrastructure.Process;
using Mustask.Infrastructure.Tests.Organizations;
using Mustask.Infrastructure.UserDirectory;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace Mustask.Infrastructure.Tests.Boards;

/// <summary>
/// feat-039 spec § Non-functional requirements, "Query shape" (brief §§ 14, 17): reading a project's backlog or board, and
/// reordering a project's backlog, take a number of statements that does not grow with the items, the parents, the teams or
/// the depth of the tree. Each surface runs through its real handler against a real PostgreSQL for a project of 50 items per
/// level and for one of 2 000 per level, linked four levels deep (epic, feature, story, task) and spread over two teams'
/// areas, with a few assignees; every statement the context sends is counted. The two counts are equal, and equal to the
/// number this test pins, so a statement added to a read is a decision, not an accident.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(OrganizationsDatabaseCollection.Name)]
public sealed class ProjectBacklogStatementCountTests(OrganizationsDatabaseFixture database, Xunit.Abstractions.ITestOutputHelper output)
{
    private const int Small = 50;
    private const int Large = 2_000;

    /// <summary>The project, the process, the members of the backlog's level (the tree set in one), the assignees and the parents outside the set.</summary>
    [Fact]
    public async Task Backlog_StoriesAndEpics_TheSameNumberOfStatements_For50AsFor2000PerLevel()
    {
        var small = await SeedAsync(Small);
        var large = await SeedAsync(Large);

        var smallStories = await CountAsync((provider, ct) => provider.GetRequiredService<GetProjectBacklogHandler>()
            .Handle(new GetProjectBacklogQuery(small.OrganizationId.ToString(), small.ProjectId.ToString(), "stories"), ct));
        var largeStories = await CountAsync((provider, ct) => provider.GetRequiredService<GetProjectBacklogHandler>()
            .Handle(new GetProjectBacklogQuery(large.OrganizationId.ToString(), large.ProjectId.ToString(), "stories"), ct));
        var smallEpics = await CountAsync((provider, ct) => provider.GetRequiredService<GetProjectBacklogHandler>()
            .Handle(new GetProjectBacklogQuery(small.OrganizationId.ToString(), small.ProjectId.ToString(), "epics"), ct));
        var largeEpics = await CountAsync((provider, ct) => provider.GetRequiredService<GetProjectBacklogHandler>()
            .Handle(new GetProjectBacklogQuery(large.OrganizationId.ToString(), large.ProjectId.ToString(), "epics"), ct));

        Assert.Equal(smallStories, largeStories);
        Assert.Equal(smallEpics, largeEpics);
        Assert.Equal(StoriesBacklogStatements, largeStories);
        Assert.Equal(EpicsBacklogStatements, largeEpics);
    }

    [Fact]
    public async Task Board_TheSameNumberOfStatements_For50AsFor2000PerLevel()
    {
        var small = await SeedAsync(Small);
        var large = await SeedAsync(Large);

        var smallBoard = await CountAsync((provider, ct) => provider.GetRequiredService<GetProjectBoardHandler>()
            .Handle(new GetProjectBoardQuery(small.OrganizationId.ToString(), small.ProjectId.ToString(), "stories"), ct));
        var largeBoard = await CountAsync((provider, ct) => provider.GetRequiredService<GetProjectBoardHandler>()
            .Handle(new GetProjectBoardQuery(large.OrganizationId.ToString(), large.ProjectId.ToString(), "stories"), ct));

        Assert.Equal(smallBoard, largeBoard);
        Assert.Equal(BoardStatements, largeBoard);
    }

    /// <summary>The reorder of a level of 2 000 items takes the statements of one of 50: the locks, the read, the drop and one upsert.</summary>
    [Fact]
    public async Task Reorder_TheSameNumberOfStatements_For50AsFor2000PerLevel()
    {
        var small = await SeedAsync(Small);
        var large = await SeedAsync(Large);

        var smallReorder = await CountAsync((provider, ct) => provider.GetRequiredService<ReorderProjectBacklogHandler>()
            .Handle(new ReorderProjectBacklogCommand(small.OrganizationId.ToString(), small.ProjectId.ToString(), "stories", small.FirstStory.ToString(), "bottom", null), ct));
        var largeReorder = await CountAsync((provider, ct) => provider.GetRequiredService<ReorderProjectBacklogHandler>()
            .Handle(new ReorderProjectBacklogCommand(large.OrganizationId.ToString(), large.ProjectId.ToString(), "stories", large.FirstStory.ToString(), "bottom", null), ct));

        Assert.Equal(smallReorder, largeReorder);
        Assert.Equal(ReorderStatements, largeReorder);
    }

    // Pinned: a change to one of these is a change to the query shape and is made on purpose (the numbers are the first run's).
    // ReorderStatements went 12 -> 13 with mod-005 (ADR-0030 rule 6): the one statement that locks the members FOR KEY SHARE.
    private const int StoriesBacklogStatements = 9;
    private const int EpicsBacklogStatements = 8;
    private const int BoardStatements = 11;
    private const int ReorderStatements = 13;

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
        services.AddApplication();
        services.AddOrganizationsInfrastructure();
        services.AddBoardsInfrastructure();

        return services.BuildServiceProvider();
    }

    /// <summary>
    /// A project of <paramref name="perLevel"/> items on each of the four levels, item n beneath item n − perLevel of the level
    /// above, alternately owned by the two teams, with three assignees spread over them.
    /// </summary>
    private async Task<Seeded> SeedAsync(int perLevel)
    {
        var structure = await database.CreateStructureAsync();
        var organization = structure.Organization.Id;
        var project = structure.Project.Project.Id;
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
                SELECT gen_random_uuid(), @project, n, @type, @state,
                       @rootArea,
                       'Элемент ' || n, '', gen_random_uuid(), now() + n * interval '1 millisecond', 'count-test', now(), parent.id,
                       CASE WHEN n % 7 < 3 THEN (ARRAY['count-user-1', 'count-user-2', 'count-user-3'])[(n % 7) + 1] END,
                       CASE WHEN n % 2 = 0 THEN @team ELSE @otherTeam END
                FROM generate_series(@from, @to) AS n
                LEFT JOIN work_items AS parent ON parent.project_id = @project AND parent.number = n - @perLevel AND @level > 0
                """,
                ("project", project),
                ("type", type),
                ("state", state),
                ("rootArea", structure.Project.Root.Id),
                ("team", structure.Project.Team.Id),
                ("otherTeam", structure.SecondTeam.Id),
                ("from", from),
                ("to", from + perLevel - 1),
                ("perLevel", perLevel),
                ("level", level));
        }

        var story = await database.QueryAsync(
            "SELECT id::text FROM work_items WHERE project_id = @project AND number = @number",
            ("project", project),
            ("number", (2 * perLevel) + 1));

        return new Seeded(organization, project, Guid.Parse(story.Single()));
    }

    private sealed record Seeded(Guid OrganizationId, Guid ProjectId, Guid FirstStory);

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
