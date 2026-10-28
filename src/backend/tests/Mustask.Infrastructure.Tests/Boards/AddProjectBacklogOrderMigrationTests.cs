using Mustask.Infrastructure.Tests.Organizations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace Mustask.Infrastructure.Tests.Boards;

/// <summary>
/// feat-039 api-contract § 6 (ADR-0028 rule 9): <c>AddProjectBacklogOrder</c> is additive — it creates the project's lock rows
/// and places and touches nothing else, so a team's places, the items and their orders are what they were — and its rollback
/// drops exactly its two tables and applies again. The tables keep the integrity the order relies on: a place belongs to an
/// order row, is at least 1, and goes with its item. The database is this class's own: the test rolls a migration back.
/// </summary>
/// <remarks>
/// feat-028 plan-backend § 9.1b (BR-OL-13): <c>OrganizationLevelTeams</c> empties the teams and their places, so a team place no
/// longer survives a roll-back that crosses it. The round trip is therefore made <b>at this migration's own state</b>: the
/// database is rolled back to <c>AddProjectBacklogOrder</c> itself, the legacy rows are written there with SQL, and only
/// <c>AddProjectBacklogOrder</c> is rolled back and applied again — this line's migration is never crossed with data that matters.
/// </remarks>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
public sealed class AddProjectBacklogOrderMigrationTests(OrganizationsDatabaseFixture database) : IClassFixture<OrganizationsDatabaseFixture>
{
    private const string MigrationName = "_AddProjectBacklogOrder";

    [Fact]
    public async Task Migrator_AddProjectBacklogOrder_IsAdditive_RollsBackToItsTwoTables_AndAppliesAgain()
    {
        await MigrateToAsync(MigrationName);
        var world = await SeedAtTheMigrationAsync();
        await database.ExecuteAsync(
            """
            INSERT INTO project_backlog_orders (project_id, backlog_level_id) VALUES (@project, @level);
            INSERT INTO project_backlog_places (work_item_id, project_id, backlog_level_id, position) VALUES (@item, @project, @level, 1)
            """,
            ("project", world.ProjectId),
            ("level", world.LevelId),
            ("item", world.ItemId));

        await RollBackOneStepAsync();

        Assert.False(await TableExistsAsync("project_backlog_orders"), "Rolling back must drop project_backlog_orders.");
        Assert.False(await TableExistsAsync("project_backlog_places"), "Rolling back must drop project_backlog_places.");
        Assert.True(await TableExistsAsync("backlog_orders"));
        Assert.True(await TableExistsAsync("backlog_places"));
        Assert.Equal(["1"], await database.QueryAsync("SELECT count(*)::text FROM backlog_places WHERE work_item_id = @item", ("item", world.ItemId)));
        Assert.Equal(["1"], await database.QueryAsync("SELECT count(*)::text FROM work_items WHERE id = @item", ("item", world.ItemId)));

        await MigrateToAsync(MigrationName);

        Assert.True(await TableExistsAsync("project_backlog_orders"));
        Assert.True(await TableExistsAsync("project_backlog_places"));
        Assert.Equal(["0"], await database.QueryAsync("SELECT count(*)::text FROM project_backlog_places"));
        Assert.Equal(["1"], await database.QueryAsync("SELECT count(*)::text FROM backlog_places WHERE work_item_id = @item", ("item", world.ItemId)));
        Assert.Equal(["1"], await database.QueryAsync("SELECT count(*)::text FROM work_items WHERE id = @item", ("item", world.ItemId)));
    }

    // The model and the migrations agree: nothing is left for a later migration to add.
    [Fact]
    public async Task Model_HasNoPendingChanges_AfterTheMigrations()
    {
        await using var context = database.NewContext();

        await context.Database.MigrateAsync();

        Assert.Contains(await context.Database.GetAppliedMigrationsAsync(), id => id.EndsWith(MigrationName, StringComparison.Ordinal));
        Assert.False(context.Database.HasPendingModelChanges(), "The model has changes no migration carries.");
    }

    // A place belongs to the order row of its backlog, holds a position of 1 or more, and goes with its item; an order row that
    // holds places cannot be deleted (ADR-0026: nothing beneath is purged).
    [Fact]
    public async Task Tables_KeepTheirIntegrity()
    {
        await using (var context = database.NewContext())
        {
            await context.Database.MigrateAsync();
        }

        var world = await SeedAtHeadAsync();
        const string InsertPlace = "INSERT INTO project_backlog_places (work_item_id, project_id, backlog_level_id, position) VALUES (@item, @project, @level, @position)";

        await Assert.ThrowsAsync<PostgresException>(() => database.ExecuteAsync(
            InsertPlace, ("item", world.ItemId), ("project", world.ProjectId), ("level", world.LevelId), ("position", 1)));

        await database.ExecuteAsync(
            "INSERT INTO project_backlog_orders (project_id, backlog_level_id) VALUES (@project, @level)",
            ("project", world.ProjectId),
            ("level", world.LevelId));
        var zero = await Assert.ThrowsAsync<PostgresException>(() => database.ExecuteAsync(
            InsertPlace, ("item", world.ItemId), ("project", world.ProjectId), ("level", world.LevelId), ("position", 0)));
        Assert.Equal("ck_project_backlog_places_position", zero.ConstraintName);

        await database.ExecuteAsync(InsertPlace, ("item", world.ItemId), ("project", world.ProjectId), ("level", world.LevelId), ("position", 1));
        await Assert.ThrowsAsync<PostgresException>(() => database.ExecuteAsync(
            InsertPlace, ("item", world.ItemId), ("project", world.ProjectId), ("level", world.LevelId), ("position", 2)));
        await Assert.ThrowsAsync<PostgresException>(() => database.ExecuteAsync(
            "DELETE FROM project_backlog_orders WHERE project_id = @project", ("project", world.ProjectId)));

        await database.ExecuteAsync("DELETE FROM work_items WHERE id = @item", ("item", world.ItemId));

        Assert.Equal(["0"], await database.QueryAsync("SELECT count(*)::text FROM project_backlog_places WHERE work_item_id = @item", ("item", world.ItemId)));
    }

    /// <summary>A project with one story (no owning team: the current shape), as the API writes it, found by name rather than by an id fixed in the test.</summary>
    private async Task<World> SeedAtHeadAsync()
    {
        var structure = await database.CreateStructureAsync();
        var (levelId, item) = await InsertStoryAsync(structure.Organization.Id, structure.Project.Project.Id, structure.Project.Root.Id, null);

        return new World(structure.Project.Project.Id, levelId, item);
    }

    /// <summary>
    /// A project in the shape this migration found (a first team owning the root, written with SQL), one story and a team place on
    /// it with the place's order row: <c>work_items</c> has no owning team at this point and the place names the area's team.
    /// </summary>
    private async Task<World> SeedAtTheMigrationAsync()
    {
        var structure = await database.CreateLegacyStructureAsync();
        var (levelId, item) = await InsertStoryAsync(structure.OrganizationId, structure.ProjectId, structure.RootAreaId, structure.FirstTeamId);

        return new World(structure.ProjectId, levelId, item);
    }

    private async Task<(Guid LevelId, Guid Item)> InsertStoryAsync(Guid organizationId, Guid projectId, Guid rootAreaId, Guid? placeTeam)
    {
        var row = (await database.QueryAsync(
            """
            SELECT concat_ws('|', type.id::text, state.id::text, type.backlog_level_id::text)
            FROM work_item_types AS type
            JOIN processes AS process ON process.id = type.process_id
            JOIN state_definitions AS state ON state.work_item_type_id = type.id AND state.position = 1
            WHERE process.organization_id = @organization AND type.key = 'US'
            """,
            ("organization", organizationId))).Single().Split('|');
        var item = Guid.CreateVersion7();
        var level = Guid.Parse(row[2]);

        await database.ExecuteAsync(
            """
            INSERT INTO work_items (id, project_id, number, type_id, state_id, area_id, title, description, creation_request_id, created_at, created_by, changed_at)
            VALUES (@item, @project, 1, @type, @state, @area, 'История', '', gen_random_uuid(), now(), 'migration-test', now())
            """,
            ("item", item),
            ("project", projectId),
            ("type", Guid.Parse(row[0])),
            ("state", Guid.Parse(row[1])),
            ("area", rootAreaId));

        if (placeTeam is { } team)
        {
            await database.ExecuteAsync(
                """
                INSERT INTO backlog_orders (team_id, backlog_level_id) VALUES (@team, @level);
                INSERT INTO backlog_places (work_item_id, team_id, backlog_level_id, position) VALUES (@item, @team, @level, 1)
                """,
                ("item", item),
                ("team", team),
                ("level", level));
        }

        return (level, item);
    }

    /// <summary>Migrates to the migration before AddProjectBacklogOrder, found by name, never hard-coded.</summary>
    private async Task RollBackOneStepAsync()
    {
        await using var context = database.NewContext();

        var previous = context.Database.GetMigrations()
            .TakeWhile(id => !id.EndsWith(MigrationName, StringComparison.Ordinal))
            .Last();

        await context.GetService<IMigrator>().MigrateAsync(previous);
    }

    /// <summary>Migrates to AddProjectBacklogOrder itself, forward or back, found by name.</summary>
    private async Task MigrateToAsync(string suffix)
    {
        await using var context = database.NewContext();
        var target = context.Database.GetMigrations().Single(id => id.EndsWith(suffix, StringComparison.Ordinal));

        await context.GetService<IMigrator>().MigrateAsync(target);
    }

    private async Task<bool> TableExistsAsync(string table) =>
        (await database.QueryAsync("SELECT (to_regclass(@table) IS NOT NULL)::text", ("table", table))).Single() == "true";

    private sealed record World(Guid ProjectId, Guid LevelId, Guid ItemId);
}
