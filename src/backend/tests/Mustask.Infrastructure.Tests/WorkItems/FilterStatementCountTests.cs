using System.Data.Common;
using Mustask.Application;
using Mustask.Application.Abstractions;
using Mustask.Application.Abstractions.Process;
using Mustask.Application.Abstractions.UserDirectory;
using Mustask.Application.Features.WorkItems.GetAssigneeOptions;
using Mustask.Application.Features.WorkItems.GetWorkItems;
using Mustask.Infrastructure.Process;
using Mustask.Infrastructure.Tests.Organizations;
using Mustask.Infrastructure.UserDirectory;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

namespace Mustask.Infrastructure.Tests.WorkItems;

/// <summary>
/// feat-003 api-contract § 6 (NFR Volumes): a filter adds no statement, and the options take the lookups of mod-014 and nothing that
/// grows with the people besides. Each use case runs through its real handler against a real PostgreSQL for a project of 50 items and
/// 50 people, and for one of 5 000 items and 2 000 people (one item in twenty deleted); every statement the context sends is
/// counted. The list takes the statements of the unfiltered one, whatever the dimension and however many are set at once, for 50 items
/// or 5 000 — the <c>UNION ALL</c> of live and deleted items is part of the same two statements. The options take the project, the
/// ids, the memberships and one lookup per 500 distinct people. The numbers are pinned: a statement added is a decision, not an accident.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(OrganizationsDatabaseCollection.Name)]
public sealed class FilterStatementCountTests(OrganizationsDatabaseFixture database, ITestOutputHelper output)
{
    private const int SmallItems = 50;
    private const int SmallPeople = 50;
    private const int LargeItems = 5_000;
    private const int LargePeople = 2_000;

    // Pinned (the first run's, equal to mod-017's unfiltered list): the project, the count, the page, the area nodes, the directory
    // lookup, the memberships — and the one the project's reachability check adds.
    private const int ListStatements = 7;

    // The options: the project and its reachability (as the list's two), the ids, the memberships — and one lookup per 500 people.
    private const int OptionsStatementsWithoutLookups = 4;

    [Fact]
    public async Task List_EveryDimensionAlone_AndAllFourTogether_TakeTheStatementsOfTheUnfilteredList_For50As5000Items()
    {
        var small = await SeedAsync(SmallItems, SmallPeople);
        var large = await SeedAsync(LargeItems, LargePeople);

        foreach (var (name, states, assignees, search, teams) in Variants())
        {
            var smallCount = await CountAsync((provider, ct) => provider.GetRequiredService<GetWorkItemsHandler>()
                .Handle(new GetWorkItemsQuery(small.OrganizationId.ToString(), small.ProjectId.ToString(), 1, teams, states, assignees, search), ct));
            var largeCount = await CountAsync((provider, ct) => provider.GetRequiredService<GetWorkItemsHandler>()
                .Handle(new GetWorkItemsQuery(large.OrganizationId.ToString(), large.ProjectId.ToString(), 1, teams, states, assignees, search), ct));

            // A page of items assigned to nobody has no one to look up and no membership to read: the statements of the people go.
            var expected = assignees is ["none"] ? ListStatements - 2 : ListStatements;

            Assert.True(smallCount == largeCount, $"{name}: {smallCount} statements for {SmallItems} items, {largeCount} for {LargeItems}.");
            Assert.True(largeCount == expected, $"{name}: {largeCount} statements, not {expected}.");
        }
    }

    // A page of a list that the filter empties is answered by the count alone: never more statements than a full page's.
    [Fact]
    public async Task List_AFilterThatSelectsNothing_TakesNoMoreStatementsThanAFullPage()
    {
        var large = await SeedAsync(LargeItems, LargePeople);

        var empty = await CountAsync((provider, ct) => provider.GetRequiredService<GetWorkItemsHandler>().Handle(
            new GetWorkItemsQuery(
                large.OrganizationId.ToString(), large.ProjectId.ToString(), 1, States: ["no such state"], AssigneeIds: ["nobody"], Search: ["no such title"]),
            ct));

        Assert.True(empty <= ListStatements, $"{empty} statements for an empty answer.");
    }

    [Fact]
    public async Task Options_TakeTheLookupsOfTheDirectory_OnePer500People_AndNothingElseThatGrows()
    {
        var small = await SeedAsync(SmallItems, SmallPeople);
        var large = await SeedAsync(LargeItems, LargePeople);

        foreach (var states in new IReadOnlyList<string?>?[] { null, ["Удалена"], ["Новая", "Удалена"] })
        {
            var smallCount = await CountAsync((provider, ct) => provider.GetRequiredService<GetAssigneeOptionsHandler>()
                .Handle(new GetAssigneeOptionsQuery(small.OrganizationId.ToString(), small.ProjectId.ToString(), states), ct));
            var largeCount = await CountAsync((provider, ct) => provider.GetRequiredService<GetAssigneeOptionsHandler>()
                .Handle(new GetAssigneeOptionsQuery(large.OrganizationId.ToString(), large.ProjectId.ToString(), states), ct));

            // 50 people are one lookup, 2 000 are four (mod-014 BR-UP-17): the only difference.
            Assert.Equal(OptionsStatementsWithoutLookups + 1, smallCount);
            Assert.Equal(OptionsStatementsWithoutLookups + LargePeople / 500, largeCount);
        }
    }

    [Fact]
    public async Task Options_OfAProjectNobodyIsAssignedIn_TakeNoLookupAndNoMembershipStatement()
    {
        var empty = await SeedAsync(items: 0, people: 0);

        var statements = await CountAsync((provider, ct) => provider.GetRequiredService<GetAssigneeOptionsHandler>()
            .Handle(new GetAssigneeOptionsQuery(empty.OrganizationId.ToString(), empty.ProjectId.ToString(), ["Удалена"]), ct));

        Assert.Equal(OptionsStatementsWithoutLookups - 1, statements);
    }

    // --- Arrangement -------------------------------------------------------------------------------------------------

    /// <summary>The filters of the list: each dimension alone, then the states beside each other and all four at once.</summary>
    private static IEnumerable<(string Name, IReadOnlyList<string?>? States, IReadOnlyList<string?>? Assignees, IReadOnlyList<string?>? Search, IReadOnlyList<string?>? Teams)> Variants()
    {
        yield return ("no filter", null, null, null, null);
        yield return ("one state", ["Новая"], null, null, null);
        yield return ("the bin", ["Удалена"], null, null, null);
        yield return ("a state beside the bin", ["Новая", "Удалена"], null, null, null);
        yield return ("one person", null, ["filter-user-1"], null, null);
        yield return ("nobody assigned", null, ["none"], null, null);
        yield return ("a title", null, null, ["Элемент 8"], null);
        yield return ("an identifier", null, null, ["PRM-TASK-8"], null);
        yield return ("a number", null, null, ["8"], null);
        yield return ("a team", null, null, null, ["none"]);
        yield return ("all four, the bin among the states", ["Новая", "Удалена"], ["none", "filter-user-1"], ["Элемент"], ["none"]);
    }

    private sealed record Seeded(Guid OrganizationId, Guid ProjectId);

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
        services.AddWorkItemsInfrastructure();
        services.AddBoardsInfrastructure();

        return services.BuildServiceProvider();
    }

    /// <summary>
    /// A project of <paramref name="items"/> tasks, one in twenty deleted (with the marks a Delete leaves), item n assigned to
    /// <c>filter-user-(n mod people)</c> but every tenth to nobody, and titled «Элемент n».
    /// </summary>
    private async Task<Seeded> SeedAsync(int items, int people)
    {
        var structure = await database.CreateStructureAsync();
        var organization = structure.Organization.Id;
        var project = structure.Project.Id;

        var task = (await database.QueryAsync(
            """
            SELECT concat_ws('|', type.id::text, first.id::text, removed.id::text)
            FROM work_item_types AS type
            JOIN processes AS process ON process.id = type.process_id
            JOIN state_definitions AS first ON first.work_item_type_id = type.id AND first.position = 1
            JOIN state_definitions AS removed ON removed.work_item_type_id = type.id AND removed.category = 'Removed'
            WHERE process.organization_id = @organization AND type.key = 'TASK'
            """,
            ("organization", organization))).Single().Split('|');

        if (items > 0)
        {
            await database.ExecuteAsync(
                """
                INSERT INTO work_items (id, project_id, number, type_id, state_id, title, description, creation_request_id,
                                        created_at, created_by, changed_at, assignee_id, deleted_from_state_id, deleted_by, deleted_at)
                SELECT gen_random_uuid(), @project, n, @type, CASE WHEN n % 20 = 7 THEN @removed ELSE @first END, 'Элемент ' || n, '', gen_random_uuid(), now(), 'count-test', now() - n * interval '1 second',
                       CASE WHEN n % 10 = 0 THEN NULL ELSE 'filter-user-' || (n % @people) END,
                       CASE WHEN n % 20 = 7 THEN @first END,
                       CASE WHEN n % 20 = 7 THEN 'count-test' END,
                       CASE WHEN n % 20 = 7 THEN now() END
                FROM generate_series(1, @items) AS n
                """,
                ("project", project),
                ("type", Guid.Parse(task[0])),
                ("first", Guid.Parse(task[1])),
                ("removed", Guid.Parse(task[2])),
                ("people", people),
                ("items", items));

            await database.ExecuteAsync(
                "INSERT INTO work_item_counters (project_id, last_number) VALUES (@project, @last)",
                ("project", project),
                ("last", items));
        }

        return new Seeded(organization, project);
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
