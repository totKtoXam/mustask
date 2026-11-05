using System.Data.Common;
using Mustask.Application;
using Mustask.Application.Abstractions;
using Mustask.Application.Abstractions.Organizations;
using Mustask.Application.Abstractions.Process;
using Mustask.Application.Abstractions.UserDirectory;
using Mustask.Application.Features.Organizations.UnlinkTeam;
using Mustask.Domain.Identity;
using Mustask.Infrastructure.Process;
using Mustask.Infrastructure.Tests.Organizations;
using Mustask.Infrastructure.UserDirectory;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace Mustask.Infrastructure.Tests.WorkItems;

/// <summary>
/// feat-028 US-4, "5 000 items, all or nothing" and the NFR Atomicity (plan-backend § 7.2) against a real PostgreSQL: the unlink is one
/// operation — the link locks, the owned items, the places, the reassignment, the setting and the link itself — in one transaction. Every
/// statement the handler sends is made to fail in turn, once each; after each failure the link stands, the team's items are still its own
/// with their «Изменён» and their place, and the setting still names the team. (A failure at a step is a failure at the statement that step
/// sends.)
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(OrganizationsDatabaseCollection.Name)]
public sealed class UnlinkTeamAtomicityTests(OrganizationsDatabaseFixture database)
{
    private static readonly DateTimeOffset Changed = new(2026, 10, 3, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Unlink_FailureAtEachStatement_RollsEverythingBack()
    {
        var statements = await CountStatementsAsync();
        Assert.True(statements >= 6, $"The unlink sends {statements} statements; fewer than its steps.");

        for (var failAt = 1; failAt <= statements; failAt++)
        {
            var world = await SeedAsync();

            var failure = await Assert.ThrowsAnyAsync<Exception>(() => UnlinkAsync(world, new FailingAt(failAt)));

            Assert.Contains("injected", failure.ToString(), StringComparison.Ordinal);
            Assert.Equal(Untouched(world), await StateAsync(world));
        }
    }

    // The same unlink with no failure: the state the failures leave behind is exactly the one it changes.
    [Fact]
    public async Task Unlink_WithoutFailure_ReassignsDropsClearsAndRemovesTheLink()
    {
        var world = await SeedAsync();

        await UnlinkAsync(world, null);

        Assert.Equal([$"0|0|0|NULL|0|{world.Destination}"], await StateAsync(world));
    }

    // --- Arrangement -------------------------------------------------------------------------------------------------

    private sealed record World(Guid OrganizationId, Guid ProjectId, Guid Team, Guid Destination, Guid Item);

    private async Task<World> SeedAsync()
    {
        var structure = await database.CreateStructureAsync();
        var team = structure.SecondTeam.Id;
        var destination = structure.FirstTeam.Id;
        var row = (await database.QueryAsync(
            """
            SELECT concat_ws('|', type.id::text, state.id::text, type.backlog_level_id::text)
            FROM work_item_types AS type
            JOIN processes AS process ON process.id = type.process_id
            JOIN state_definitions AS state ON state.work_item_type_id = type.id AND state.position = 1
            WHERE process.organization_id = @organization AND type.key = 'US'
            """,
            ("organization", structure.Organization.Id))).Single().Split('|');
        var item = Guid.CreateVersion7();

        await database.ExecuteAsync(
            """
            INSERT INTO work_items (id, project_id, number, type_id, state_id, area_id, title, description, creation_request_id, created_at, created_by, changed_at, owning_team_id)
            SELECT CASE WHEN n = 1 THEN @item ELSE gen_random_uuid() END, @project, n, @type, @state, @area, 'Элемент ' || n, '', gen_random_uuid(), @changed, 'atomic-test', @changed, @team
            FROM generate_series(1, 3) AS n;
            INSERT INTO backlog_orders (team_id, backlog_level_id) VALUES (@team, @level);
            INSERT INTO backlog_places (work_item_id, team_id, backlog_level_id, position) VALUES (@item, @team, @level, 1);
            UPDATE projects SET default_team_id = @team WHERE id = @project
            """,
            ("item", item),
            ("project", structure.Project.Project.Id),
            ("type", Guid.Parse(row[0])),
            ("state", Guid.Parse(row[1])),
            ("level", Guid.Parse(row[2])),
            ("area", structure.Project.Root.Id),
            ("changed", Changed),
            ("team", team));

        return new World(structure.Organization.Id, structure.Project.Project.Id, team, destination, item);
    }

    /// <summary>
    /// The state of the link, the items, the place and the setting, as "link|items owned by the team|items unchanged|setting|places of the
    /// team|owner of the first item": the failures must leave what <see cref="Untouched"/> says, the success what the first test says.
    /// </summary>
    private async Task<List<string>> StateAsync(World world) =>
        await database.QueryAsync(
            """
            SELECT concat_ws('|',
                (SELECT count(*) FROM project_teams WHERE project_id = @project AND team_id = @team),
                (SELECT count(*) FROM work_items WHERE owning_team_id = @team),
                (SELECT count(*) FROM work_items WHERE project_id = @project AND changed_at = @changed),
                (SELECT coalesce(default_team_id::text, 'NULL') FROM projects WHERE id = @project),
                (SELECT count(*) FROM backlog_places WHERE team_id = @team),
                (SELECT coalesce(owning_team_id::text, 'NULL') FROM work_items WHERE id = @item))
            """,
            ("team", world.Team),
            ("project", world.ProjectId),
            ("item", world.Item),
            ("changed", Changed));

    /// <summary>What a seeded world is before any unlink: linked, three items owned and unchanged, the setting, a place, the first item the team's.</summary>
    private static List<string> Untouched(World world) => [$"1|3|3|{world.Team}|1|{world.Team}"];

    private async Task UnlinkAsync(World world, IInterceptor? interceptor)
    {
        await using var provider = BuildServices(interceptor);
        await using var scope = provider.CreateAsyncScope();

        await scope.ServiceProvider.GetRequiredService<UnlinkTeamHandler>().Handle(
            new UnlinkTeamCommand(
                world.OrganizationId.ToString(), world.ProjectId.ToString(), world.Team.ToString(), new UnlinkDestination(world.Destination.ToString())),
            CancellationToken.None);
    }

    private async Task<int> CountStatementsAsync()
    {
        var counter = new FailingAt(int.MaxValue);
        await UnlinkAsync(await SeedAsync(), counter);

        return counter.Seen;
    }

    private ServiceProvider BuildServices(IInterceptor? interceptor)
    {
        var services = new ServiceCollection();

        services.AddDbContext<MustaskDbContext>(options =>
        {
            options.UseNpgsql(database.ConnectionString);

            if (interceptor is not null)
            {
                options.AddInterceptors(interceptor);
            }
        });
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IUserDirectory, UserDirectoryReadModel>();
        services.AddScoped<IProcessDefinitionReadModel, ProcessDefinitionReadModel>();
        services.AddSingleton<IClock, SystemClock>();
        services.AddScoped<IOrganizationAccess, AdministratorAccess>();
        services.AddApplication();
        services.AddOrganizationsInfrastructure();
        services.AddWorkItemsInfrastructure();
        services.AddBoardsInfrastructure();

        return services.BuildServiceProvider();
    }

    /// <summary>The caller as an administrator of the organization: the unlink asks for its second code.</summary>
    private sealed class AdministratorAccess : IOrganizationAccess
    {
        public OrganizationRole Role => OrganizationRole.Admin;

        public bool Holds(string code) => OrganizationRoleBundles.Holds(Role, code);
    }

    /// <summary>Counts the statements a context sends and makes the one numbered <c>failAt</c> throw before it runs.</summary>
    private sealed class FailingAt(int failAt) : DbCommandInterceptor
    {
        public int Seen { get; private set; }

        private void Next()
        {
            Seen++;

            if (Seen == failAt)
            {
                throw new InvalidOperationException($"injected failure at statement {failAt}");
            }
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            Next();

            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            Next();

            return base.NonQueryExecutingAsync(command, eventData, result, cancellationToken);
        }

        public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<object> result, CancellationToken cancellationToken = default)
        {
            Next();

            return base.ScalarExecutingAsync(command, eventData, result, cancellationToken);
        }
    }
}
