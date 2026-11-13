using System.Data.Common;
using Mustask.Application;
using Mustask.Application.Abstractions;
using Mustask.Application.Abstractions.Process;
using Mustask.Application.Abstractions.UserDirectory;
using Mustask.Application.Features.Iterations.CloseSprint;
using Mustask.Application.Features.Iterations.GetSprintDynamics;
using Mustask.Application.Features.Iterations.GetSprintOutcomeItems;
using Mustask.Application.Features.Iterations.GetSprintParticipants;
using Mustask.Application.Features.Iterations.GetSprints;
using Mustask.Infrastructure.Process;
using Mustask.Infrastructure.Tests.Organizations;
using Mustask.Infrastructure.UserDirectory;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace Mustask.Infrastructure.Tests.Iterations;

/// <summary>
/// mod-007 spec § Non-functional requirements, "Query shape": the close of a sprint and the reads of the outcome page take a
/// number of statements that does not grow with the items, the people or the sprints of the range. Each surface runs through its
/// real handler against a real PostgreSQL for a team of 50 items per sprint and for one of 2 000, over five earlier sprints and
/// three assignees; every statement the context sends is counted. The counts are equal, and equal to the number this test pins, so
/// a statement added to a read is a decision, not an accident.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(OrganizationsDatabaseCollection.Name)]
public sealed class SprintStatementCountTests(OrganizationsDatabaseFixture database, Xunit.Abstractions.ITestOutputHelper output)
{
    private const int Small = 50;
    private const int Large = 2_000;

    [Fact]
    public async Task OutcomeReads_TheSameNumberOfStatements_For50AsFor2000ItemsPerSprint()
    {
        var small = await SeedClosedAsync(Small);
        var large = await SeedClosedAsync(Large);

        var smallDynamics = await CountAsync((provider, ct) => provider.GetRequiredService<GetSprintDynamicsHandler>()
            .Handle(new GetSprintDynamicsQuery(small.OrganizationId, small.TeamId, small.SprintId, null, null, null), ct));
        var largeDynamics = await CountAsync((provider, ct) => provider.GetRequiredService<GetSprintDynamicsHandler>()
            .Handle(new GetSprintDynamicsQuery(large.OrganizationId, large.TeamId, large.SprintId, null, null, null), ct));
        var smallParticipants = await CountAsync((provider, ct) => provider.GetRequiredService<GetSprintParticipantsHandler>()
            .Handle(new GetSprintParticipantsQuery(small.OrganizationId, small.TeamId, small.SprintId, null, null, null, null), ct));
        var largeParticipants = await CountAsync((provider, ct) => provider.GetRequiredService<GetSprintParticipantsHandler>()
            .Handle(new GetSprintParticipantsQuery(large.OrganizationId, large.TeamId, large.SprintId, null, null, null, null), ct));
        var smallItems = await CountAsync((provider, ct) => provider.GetRequiredService<GetSprintOutcomeItemsHandler>()
            .Handle(new GetSprintOutcomeItemsQuery(small.OrganizationId, small.TeamId, small.SprintId, null), ct));
        var largeItems = await CountAsync((provider, ct) => provider.GetRequiredService<GetSprintOutcomeItemsHandler>()
            .Handle(new GetSprintOutcomeItemsQuery(large.OrganizationId, large.TeamId, large.SprintId, null), ct));
        var smallList = await CountAsync((provider, ct) => provider.GetRequiredService<GetSprintsHandler>()
            .Handle(new GetSprintsQuery(small.OrganizationId, small.TeamId, null, null), ct));
        var largeList = await CountAsync((provider, ct) => provider.GetRequiredService<GetSprintsHandler>()
            .Handle(new GetSprintsQuery(large.OrganizationId, large.TeamId, null, null), ct));

        Assert.Equal(smallDynamics, largeDynamics);
        Assert.Equal(smallParticipants, largeParticipants);
        Assert.Equal(smallItems, largeItems);
        Assert.Equal(smallList, largeList);
        Assert.Equal(DynamicsStatements, largeDynamics);
        Assert.Equal(ParticipantsStatements, largeParticipants);
        Assert.Equal(ItemsStatements, largeItems);
        Assert.Equal(ListStatements, largeList);
    }

    [Fact]
    public async Task Close_TheSameNumberOfStatements_For50AsFor2000Items()
    {
        var small = await SeedActiveAsync(Small);
        var large = await SeedActiveAsync(Large);

        var smallClose = await CountAsync((provider, ct) => provider.GetRequiredService<CloseSprintHandler>()
            .Handle(new CloseSprintCommand(small.OrganizationId, small.TeamId, small.SprintId, small.DestinationId), ct));
        var largeClose = await CountAsync((provider, ct) => provider.GetRequiredService<CloseSprintHandler>()
            .Handle(new CloseSprintCommand(large.OrganizationId, large.TeamId, large.SprintId, large.DestinationId), ct));

        Assert.Equal(smallClose, largeClose);
        Assert.Equal(CloseStatements, largeClose);
    }

    // NFR · the count does not grow with the range, the participants or the items: 5 sprints and 3 people against 21 sprints and 300.
    [Fact]
    public async Task OutcomeReads_TheSameNumberOfStatements_WhateverTheRangeAndTheNumberOfParticipants()
    {
        var narrow = await SeedClosedAsync(40, sprints: 6, people: 3);
        var wide = await SeedClosedAsync(600, sprints: 21, people: 300);

        async Task<int> DynamicsAsync(Seeded s) => await CountAsync((provider, ct) => provider.GetRequiredService<GetSprintDynamicsHandler>()
            .Handle(new GetSprintDynamicsQuery(s.OrganizationId, s.TeamId, s.SprintId, null, "2026-01-01", "2026-12-31"), ct));
        async Task<int> ParticipantsAsync(Seeded s) => await CountAsync((provider, ct) => provider.GetRequiredService<GetSprintParticipantsHandler>()
            .Handle(new GetSprintParticipantsQuery(s.OrganizationId, s.TeamId, s.SprintId, null, "2026-01-01", "2026-12-31", null), ct));

        Assert.Equal(await DynamicsAsync(narrow), await DynamicsAsync(wide));
        Assert.Equal(await ParticipantsAsync(narrow), await ParticipantsAsync(wide));
    }

    // NFR · the close dialog's counts are a fixed number of statements whatever the items.
    [Fact]
    public async Task ClosePreview_TheSameNumberOfStatements_For50AsFor2000Items()
    {
        var small = await SeedActiveAsync(Small);
        var large = await SeedActiveAsync(Large);

        async Task<int> PreviewAsync(Seeded s) => await CountAsync((provider, ct) => provider.GetRequiredService<Mustask.Application.Features.Iterations.GetCloseSprintPreview.GetCloseSprintPreviewHandler>()
            .Handle(new Mustask.Application.Features.Iterations.GetCloseSprintPreview.GetCloseSprintPreviewQuery(s.OrganizationId, s.TeamId, s.SprintId), ct));

        Assert.Equal(await PreviewAsync(small), await PreviewAsync(large));
    }

    // Pinned: a change to one of these is a change to the query shape and is made on purpose (the numbers are the first run's).
    private const int DynamicsStatements = 8;
    private const int ParticipantsStatements = 8;
    private const int ItemsStatements = 4;
    private const int ListStatements = 6;
    private const int CloseStatements = 17;

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
        services.AddIterationsInfrastructure();
        services.AddBoardsInfrastructure();

        return services.BuildServiceProvider();
    }

    private sealed class FixedUser : ICurrentUser
    {
        public string DisplayName => "Count User";

        public string SubjectId => "count-user-1";
    }

    private sealed record Seeded(string OrganizationId, string TeamId, string SprintId, string? DestinationId);

    /// <summary>A Closed sprint of <paramref name="items"/> snapshot items and five earlier Closed ones of the same size, three assignees.</summary>
    private async Task<Seeded> SeedClosedAsync(int items, int sprints = 6, int people = 3)
    {
        var structure = await database.CreateStructureAsync();
        var organization = structure.Organization.Id;
        var team = structure.FirstTeam.Id;
        var project = structure.Project.Project.Id;

        var level = Guid.Parse((await database.QueryAsync(
            """
            SELECT level.id::text FROM backlog_levels AS level
            JOIN processes AS process ON process.id = level.process_id
            WHERE process.organization_id = @organization ORDER BY level.rank DESC LIMIT 1
            """,
            ("organization", organization))).First());
        var type = Guid.Parse((await database.QueryAsync(
            """
            SELECT type.id::text FROM work_item_types AS type
            JOIN processes AS process ON process.id = type.process_id
            WHERE process.organization_id = @organization AND type.key = 'US'
            """,
            ("organization", organization))).Single());

        await database.ExecuteAsync(
            """
            INSERT INTO sprints (id, organization_id, team_id, number, name, normalized_name, state, start_date, end_date, created_at, created_by, closed_at, closed_by)
            SELECT gen_random_uuid(), @organization, @team, n, 'S' || n, 'S' || n, 'Closed',
                   DATE '2026-01-01' + n * 14, DATE '2026-01-01' + n * 14 + 10, now(), 'seed', now(), 'seed'
            FROM generate_series(1, @sprints) AS n
            """,
            ("organization", organization),
            ("team", team),
            ("sprints", sprints));
        await database.ExecuteAsync(
            """
            INSERT INTO sprint_snapshot_items (sprint_id, work_item_id, project_id, type_id, number, level_id, assignee_id, story_points, is_done)
            SELECT sprint.id, gen_random_uuid(), @project, @type, n, @level,
                   CASE WHEN n % 7 <> 0 THEN 'count-user-' || ((n % @people) + 1) END,
                   (n % 5) + 1, n % 2 = 0
            FROM sprints AS sprint
            CROSS JOIN generate_series(1, @items) AS n
            WHERE sprint.team_id = @team
            """,
            ("project", project),
            ("type", type),
            ("level", level),
            ("items", items),
            ("team", team),
            ("people", people));

        var last = (await database.QueryAsync(
            "SELECT id::text FROM sprints WHERE team_id = @team AND number = @sprints",
            ("team", team),
            ("sprints", sprints))).Single();

        return new Seeded(organization.ToString(), team.ToString(), last, null);
    }

    /// <summary>An Active sprint holding <paramref name="items"/> stories, a Planned sprint as the destination.</summary>
    private async Task<Seeded> SeedActiveAsync(int items)
    {
        var structure = await database.CreateStructureAsync();
        var organization = structure.Organization.Id;
        var team = structure.FirstTeam.Id;
        var project = structure.Project.Project.Id;

        var parts = (await database.QueryAsync(
            """
            SELECT concat_ws('|', type.id::text, state.id::text)
            FROM work_item_types AS type
            JOIN processes AS process ON process.id = type.process_id
            JOIN state_definitions AS state ON state.work_item_type_id = type.id AND state.position = 1
            WHERE process.organization_id = @organization AND type.key = 'US'
            """,
            ("organization", organization))).Single().Split('|');

        var active = Guid.NewGuid();
        var planned = Guid.NewGuid();

        await database.ExecuteAsync(
            """
            INSERT INTO sprint_counters (team_id, last_number) VALUES (@team, 2);
            INSERT INTO sprints (id, organization_id, team_id, number, name, normalized_name, state, start_date, end_date, created_at, created_by)
            VALUES (@active, @organization, @team, 1, 'A', 'A', 'Active', DATE '2026-09-07', DATE '2026-09-18', now(), 'seed'),
                   (@planned, @organization, @team, 2, 'P', 'P', 'Planned', NULL, NULL, now(), 'seed')
            """,
            ("organization", organization),
            ("team", team),
            ("active", active),
            ("planned", planned));
        await database.ExecuteAsync(
            """
            INSERT INTO work_items (id, project_id, number, type_id, state_id, area_id, title, description, creation_request_id,
                                    created_at, created_by, changed_at, assignee_id, owning_team_id, sprint_id, story_points)
            SELECT gen_random_uuid(), @project, n, @type, @state, @rootArea, 'Элемент ' || n, '', gen_random_uuid(), now(), 'count-test', now(),
                   CASE WHEN n % 7 < 3 THEN (ARRAY['count-user-1', 'count-user-2', 'count-user-3'])[(n % 7) + 1] END,
                   @team, @active, (n % 5) + 1
            FROM generate_series(1, @items) AS n
            """,
            ("project", project),
            ("type", Guid.Parse(parts[0])),
            ("state", Guid.Parse(parts[1])),
            ("rootArea", structure.Project.Root.Id),
            ("team", team),
            ("active", active),
            ("items", items));

        return new Seeded(organization.ToString(), team.ToString(), active.ToString(), planned.ToString());
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
