using System.Data.Common;
using Mustask.Application;
using Mustask.Application.Abstractions;
using Mustask.Application.Abstractions.Process;
using Mustask.Application.Abstractions.UserDirectory;
using Mustask.Application.Features.WorkItems.DeleteWorkItem;
using Mustask.Application.Features.WorkItems.PurgeWorkItem;
using Mustask.Application.Features.WorkItems.RestoreWorkItem;
using Mustask.Infrastructure.Process;
using Mustask.Infrastructure.Tests.Organizations;
using Mustask.Infrastructure.UserDirectory;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Mustask.Infrastructure.Tests.WorkItems;

/// <summary>
/// mod-005 plan-backend D-P3 and § 9.3, "Statement counts" (NFR "fixed number of queries"): Delete, Restore and Purge take the same
/// number of statements for a project of 50 items as for one of 5 000, and a purge the same for an item with no child as for one with
/// 5 000 — the children are cleared by one set-based statement, the item and its children locked by one ordered statement. Each runs
/// through its real handler against a real PostgreSQL and every statement the context sends is counted; the counts are equal and equal
/// to the numbers pinned here, so a statement added is a decision, not an accident. The item read's and the list's own counts
/// (<c>OwningTeamStatementCountTests</c>: 7 and 7) are unchanged by this task: the deletion marks and the deleter are read in the
/// item's own statement and the directory call the assignee already makes.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(OrganizationsDatabaseCollection.Name)]
public sealed class DeletionStatementCountTests(OrganizationsDatabaseFixture database, Xunit.Abstractions.ITestOutputHelper output)
{
    private const int Small = 50;
    private const int Large = 5_000;
    private const string User = "deletion-count-user";

    // Pinned: a change to one of these is a change to the query shape and is made on purpose (the numbers are the first run's).
    private const int DeleteStatements = 13;
    private const int RestoreStatements = 11;
    private const int PurgeStatements = 13;

    [Fact]
    public async Task Delete_AndRestore_TheSameNumberOfStatements_For50As5000Items()
    {
        var small = await SeedAsync(Small, children: 0);
        var large = await SeedAsync(Large, children: 0);

        var smallDelete = await CountAsync((provider, ct) => provider.GetRequiredService<DeleteWorkItemHandler>()
            .Handle(new DeleteWorkItemCommand(small.OrganizationId.ToString(), small.ProjectId.ToString(), small.TargetIdentifier), ct));
        var largeDelete = await CountAsync((provider, ct) => provider.GetRequiredService<DeleteWorkItemHandler>()
            .Handle(new DeleteWorkItemCommand(large.OrganizationId.ToString(), large.ProjectId.ToString(), large.TargetIdentifier), ct));

        Assert.Equal(smallDelete, largeDelete);
        Assert.Equal(DeleteStatements, largeDelete);

        var smallRestore = await CountAsync((provider, ct) => provider.GetRequiredService<RestoreWorkItemHandler>()
            .Handle(new RestoreWorkItemCommand(small.OrganizationId.ToString(), small.ProjectId.ToString(), small.TargetIdentifier), ct));
        var largeRestore = await CountAsync((provider, ct) => provider.GetRequiredService<RestoreWorkItemHandler>()
            .Handle(new RestoreWorkItemCommand(large.OrganizationId.ToString(), large.ProjectId.ToString(), large.TargetIdentifier), ct));

        Assert.Equal(smallRestore, largeRestore);
        Assert.Equal(RestoreStatements, largeRestore);
    }

    [Fact]
    public async Task Purge_TheSameNumberOfStatements_For0As5000Children_AndFor50As5000Items()
    {
        var childless = await SeedAsync(Small, children: 0);
        var parent = await SeedAsync(Large, children: Large);
        await DeleteDirectlyAsync(childless.TargetNumber, childless.ProjectId);
        await DeleteDirectlyAsync(parent.TargetNumber, parent.ProjectId);

        var withNone = await CountAsync((provider, ct) => provider.GetRequiredService<PurgeWorkItemHandler>()
            .Handle(new PurgeWorkItemCommand(childless.OrganizationId.ToString(), childless.ProjectId.ToString(), childless.TargetIdentifier), ct));
        var withMany = await CountAsync((provider, ct) => provider.GetRequiredService<PurgeWorkItemHandler>()
            .Handle(new PurgeWorkItemCommand(parent.OrganizationId.ToString(), parent.ProjectId.ToString(), parent.TargetIdentifier), ct));

        Assert.Equal(withNone, withMany);
        Assert.Equal(PurgeStatements, withMany);
        Assert.Equal(["0"], await database.QueryAsync("SELECT count(*)::text FROM work_items WHERE project_id = @project AND parent_id IS NOT NULL", ("project", parent.ProjectId)));
    }

    // --- Arrangement -------------------------------------------------------------------------------------------------

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

        services.AddLogging();
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

    private sealed record Seeded(Guid OrganizationId, Guid ProjectId, int TargetNumber, string TargetIdentifier);

    /// <summary>
    /// A project of <paramref name="items"/> tasks, and an epic numbered after them (the target) with <paramref name="children"/> tasks
    /// beneath it; the task with the highest number among the first is the target when there are no children. The target of a
    /// delete and restore is an ordinary task; of a purge, the epic.
    /// </summary>
    private async Task<Seeded> SeedAsync(int items, int children)
    {
        var structure = await database.CreateStructureAsync();
        var project = structure.Project.Project.Id;
        var row = (await database.QueryAsync(
            """
            SELECT concat_ws('|', type.key, type.id::text, state.id::text)
            FROM work_item_types AS type
            JOIN processes AS process ON process.id = type.process_id
            JOIN state_definitions AS state ON state.work_item_type_id = type.id AND state.position = 1
            WHERE process.organization_id = @organization AND type.key IN ('TASK', 'EPIC')
            ORDER BY type.key DESC
            """,
            ("organization", structure.Organization.Id))).Select(line => line.Split('|')).ToDictionary(parts => parts[0], parts => parts);

        // The tasks 1..items, then the target numbered items + 1; with children, the target is an epic and the children follow it.
        var targetKey = children > 0 ? "EPIC" : "TASK";
        var target = items + 1;

        await database.ExecuteAsync(
            """
            INSERT INTO work_items (id, project_id, number, type_id, state_id, area_id, owning_team_id, title, description,
                                    creation_request_id, created_at, created_by, changed_at)
            SELECT gen_random_uuid(), @project, n, @taskType, @taskState, @root, @team, 'Задача ' || n, '', gen_random_uuid(), now(), 'count-test', now()
            FROM generate_series(1, @items) AS n
            """,
            ("project", project),
            ("taskType", Guid.Parse(row["TASK"][1])),
            ("taskState", Guid.Parse(row["TASK"][2])),
            ("root", structure.Project.Root.Id),
            ("team", structure.FirstTeam.Id),
            ("items", items));
        await database.ExecuteAsync(
            """
            INSERT INTO work_items (id, project_id, number, type_id, state_id, area_id, owning_team_id, title, description,
                                    creation_request_id, created_at, created_by, changed_at)
            VALUES (gen_random_uuid(), @project, @target, @type, @state, @root, @team, 'Цель', '', gen_random_uuid(), now(), 'count-test', now())
            """,
            ("project", project),
            ("target", target),
            ("type", Guid.Parse(row[targetKey][1])),
            ("state", Guid.Parse(row[targetKey][2])),
            ("root", structure.Project.Root.Id),
            ("team", structure.FirstTeam.Id));

        if (children > 0)
        {
            await database.ExecuteAsync(
                """
                INSERT INTO work_items (id, project_id, number, type_id, state_id, area_id, owning_team_id, title, description,
                                        creation_request_id, created_at, created_by, changed_at, parent_id)
                SELECT gen_random_uuid(), @project, @target + n, @taskType, @taskState, @root, @team, 'Дочерняя ' || n, '', gen_random_uuid(), now(), 'count-test', now(),
                       (SELECT id FROM work_items WHERE project_id = @project AND number = @target)
                FROM generate_series(1, @children) AS n
                """,
                ("project", project),
                ("target", target),
                ("taskType", Guid.Parse(row["TASK"][1])),
                ("taskState", Guid.Parse(row["TASK"][2])),
                ("root", structure.Project.Root.Id),
                ("team", structure.FirstTeam.Id),
                ("children", children));
        }

        await database.ExecuteAsync(
            "INSERT INTO work_item_counters (project_id, last_number) VALUES (@project, @last)",
            ("project", project),
            ("last", target + children));

        return new Seeded(structure.Organization.Id, project, target, $"PRM-{targetKey}-{target}");
    }

    /// <summary>Puts the target into «Удалена» with the three marks, as a Delete would have left it, without a handler's statements.</summary>
    private Task DeleteDirectlyAsync(int number, Guid project) =>
        database.ExecuteAsync(
            """
            UPDATE work_items AS item
            SET deleted_from_state_id = item.state_id,
                deleted_by = 'count-test',
                deleted_at = now(),
                state_id = (SELECT removed.id FROM state_definitions AS removed WHERE removed.work_item_type_id = item.type_id AND removed.category = 'Removed')
            WHERE item.project_id = @project AND item.number = @number
            """,
            ("project", project),
            ("number", number));

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
