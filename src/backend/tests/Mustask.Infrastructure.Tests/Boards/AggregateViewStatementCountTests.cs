using System.Data.Common;
using Mustask.Application;
using Mustask.Application.Abstractions;
using Mustask.Application.Abstractions.Boards;
using Mustask.Application.Abstractions.Organizations;
using Mustask.Application.Abstractions.Process;
using Mustask.Application.Abstractions.UserDirectory;
using Mustask.Application.Features.Boards.GetBacklogGroupRows;
using Mustask.Application.Features.Boards.GetBoardCells;
using Mustask.Application.Features.Boards.GetViewGroups;
using Mustask.Application.Features.WorkItems.GetOrganizationWorkItems;
using Mustask.Domain.Boards;
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
/// feat-048 spec § Non-functional requirements: the aggregate reads take a number of statements that does not grow with the
/// projects, the groups or the rows. Each read runs through its real handler against a real PostgreSQL for an organization of 5
/// projects and one of 50 projects (25 stories each, every other one owned by a team), and every statement the context sends is
/// counted: the grouped counts of each grouping, the first page of a group (change-time order and the project's rank order), a
/// board cell and the flat list. The two counts of each read are equal, so the statements per group opened are constant too.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(OrganizationsDatabaseCollection.Name)]
public sealed class AggregateViewStatementCountTests(OrganizationsDatabaseFixture database, Xunit.Abstractions.ITestOutputHelper output)
{
    private const int SmallProjects = 5;
    private const int LargeProjects = 50;
    private const int PerProject = 25;

    [Theory]
    [InlineData("none")]
    [InlineData("team")]
    [InlineData("project")]
    public async Task Groups_TheSameNumberOfStatements_ForFiveProjectsAsForFifty(string grouping)
    {
        var small = await SeedAsync(SmallProjects);
        var large = await SeedAsync(LargeProjects);

        foreach (var view in new[] { AggregateView.Backlogs, AggregateView.Boards })
        {
            var smallCount = await CountAsync((provider, ct) => provider.GetRequiredService<GetViewGroupsHandler>()
                .Handle(new GetViewGroupsQuery(view, new AggregateQuery(small.OrganizationId.ToString(), "stories", grouping)), ct));
            var largeCount = await CountAsync((provider, ct) => provider.GetRequiredService<GetViewGroupsHandler>()
                .Handle(new GetViewGroupsQuery(view, new AggregateQuery(large.OrganizationId.ToString(), "stories", grouping)), ct));

            Assert.Equal(smallCount, largeCount);
        }

        var smallWorkItems = await CountAsync((provider, ct) => provider.GetRequiredService<GetViewGroupsHandler>()
            .Handle(new GetViewGroupsQuery(AggregateView.WorkItems, new AggregateQuery(small.OrganizationId.ToString(), null, grouping)), ct));
        var largeWorkItems = await CountAsync((provider, ct) => provider.GetRequiredService<GetViewGroupsHandler>()
            .Handle(new GetViewGroupsQuery(AggregateView.WorkItems, new AggregateQuery(large.OrganizationId.ToString(), null, grouping)), ct));

        Assert.Equal(smallWorkItems, largeWorkItems);
    }

    [Fact]
    public async Task OpeningAGroup_TheSameNumberOfStatements_ForFiveProjectsAsForFifty()
    {
        var small = await SeedAsync(SmallProjects);
        var large = await SeedAsync(LargeProjects);

        // The change-time order (a team's group and the group of no team) and the rank order (a project's group).
        foreach (var (grouping, group) in new (string, Func<Seeded, string>)[]
        {
            ("team", seeded => seeded.Team.ToString()),
            ("team", _ => "none"),
            ("project", seeded => seeded.Projects[0].ToString()),
        })
        {
            var smallCount = await CountAsync((provider, ct) => provider.GetRequiredService<GetBacklogGroupRowsHandler>()
                .Handle(new GetBacklogGroupRowsQuery(new AggregateQuery(small.OrganizationId.ToString(), "stories", grouping, Group: group(small), Limit: "10")), ct));
            var largeCount = await CountAsync((provider, ct) => provider.GetRequiredService<GetBacklogGroupRowsHandler>()
                .Handle(new GetBacklogGroupRowsQuery(new AggregateQuery(large.OrganizationId.ToString(), "stories", grouping, Group: group(large), Limit: "10")), ct));

            Assert.Equal(smallCount, largeCount);
        }

        var smallCell = await CountAsync((provider, ct) => provider.GetRequiredService<GetBoardCellsHandler>()
            .Handle(new GetBoardCellsQuery(new AggregateQuery(small.OrganizationId.ToString(), "stories", "team", Group: small.Team.ToString(), Column: "Proposed-1", Limit: "10")), ct));
        var largeCell = await CountAsync((provider, ct) => provider.GetRequiredService<GetBoardCellsHandler>()
            .Handle(new GetBoardCellsQuery(new AggregateQuery(large.OrganizationId.ToString(), "stories", "team", Group: large.Team.ToString(), Column: "Proposed-1", Limit: "10")), ct));

        Assert.Equal(smallCell, largeCell);
    }

    [Fact]
    public async Task FlatList_TheSameNumberOfStatements_ForFiveProjectsAsForFifty_ShallowAndDeepAndByKeyword()
    {
        var small = await SeedAsync(SmallProjects);
        var large = await SeedAsync(LargeProjects);

        // Page 1 is a LATERAL over the projects, page 30 the ordered pass: each is one statement for the page whatever the projects.
        foreach (var (page, search) in new (int, string?)[] { (1, null), (3, null), (1, "Элемент 12") })
        {
            IReadOnlyList<string?>? keyword = search is null ? null : [search];
            var smallCount = await CountAsync((provider, ct) => provider.GetRequiredService<GetOrganizationWorkItemsHandler>()
                .Handle(new GetOrganizationWorkItemsQuery(new AggregateQuery(small.OrganizationId.ToString(), null, Search: keyword), page), ct));
            var largeCount = await CountAsync((provider, ct) => provider.GetRequiredService<GetOrganizationWorkItemsHandler>()
                .Handle(new GetOrganizationWorkItemsQuery(new AggregateQuery(large.OrganizationId.ToString(), null, Search: keyword), page), ct));

            Assert.Equal(smallCount, largeCount);
        }
    }

    private async Task<int> CountAsync(Func<IServiceProvider, CancellationToken, Task> run)
    {
        var counter = new StatementCounter();
        await using var provider = BuildServices(counter);
        await using var scope = provider.CreateAsyncScope();

        await run(scope.ServiceProvider, CancellationToken.None);

        output.WriteLine($"{counter.Statements.Count} statements");

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

    private sealed class AdministratorAccess : IOrganizationAccess
    {
        public OrganizationRole Role => OrganizationRole.Admin;

        public bool Holds(string code) => OrganizationRoleBundles.Holds(Role, code);
    }

    /// <summary>An organization of <paramref name="projects"/> projects linked to a team, each with 25 stories, every other one owned by the team.</summary>
    private async Task<Seeded> SeedAsync(int projects)
    {
        var structure = await database.CreateStructureAsync();
        var organization = structure.Organization.Id;
        var team = structure.FirstTeam.Id;
        var projectIds = new List<Guid> { structure.Project.Project.Id };

        await using (var context = database.NewContext())
        {
            for (var index = 1; index < projects; index++)
            {
                var project = Project.Create(
                    organization, OrganizationsDatabaseFixture.Name($"Проект {index:D2}"), $"AS{index:D2}", OrganizationsDatabaseFixture.Now, OrganizationsDatabaseFixture.Creator, "blue");
                context.Projects.Add(project.Project);
                context.Areas.Add(project.Root);
                context.ProjectTeams.Add(
                    ProjectTeam.Link(organization, project.Project.Id, team, OrganizationsDatabaseFixture.Creator, OrganizationsDatabaseFixture.Now));
                projectIds.Add(project.Project.Id);
            }

            await context.SaveChangesAsync();
        }

        var story = (await database.QueryAsync(
            """
            SELECT concat_ws('|', type.id::text, state.id::text)
            FROM work_item_types AS type
            JOIN processes AS process ON process.id = type.process_id
            JOIN state_definitions AS state ON state.work_item_type_id = type.id AND state.position = 1
            WHERE process.organization_id = @organization AND type.key = 'US'
            """,
            ("organization", organization))).Single().Split('|');

        await database.ExecuteAsync(
            """
            INSERT INTO work_items (id, project_id, number, type_id, state_id, area_id, title, description, creation_request_id,
                                    created_at, created_by, changed_at, owning_team_id)
            SELECT gen_random_uuid(), project.id, n, @type, @state, root.id,
                   'Элемент ' || n, '', gen_random_uuid(), now() + n * interval '1 millisecond', 'count-test', now() + n * interval '1 second',
                   CASE WHEN n % 2 = 0 THEN @team END
            FROM unnest(@projects) AS project(id)
            JOIN areas AS root ON root.project_id = project.id AND root.parent_id IS NULL
            CROSS JOIN generate_series(1, @perProject) AS n
            """,
            ("projects", projectIds.ToArray()),
            ("type", Guid.Parse(story[0])),
            ("state", Guid.Parse(story[1])),
            ("team", team),
            ("perProject", PerProject));

        return new Seeded(organization, team, projectIds);
    }

    private sealed record Seeded(Guid OrganizationId, Guid Team, IReadOnlyList<Guid> Projects);

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
