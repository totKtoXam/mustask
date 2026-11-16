using Mustask.Infrastructure.Tests.Organizations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace Mustask.Infrastructure.Tests.Iterations;

/// <summary>
/// mod-007 plan-backend (ADR-0028 rule 9): <c>AddSprints</c> is additive — it creates three tables and one nullable column of
/// <c>work_items</c> and touches nothing else, so the items are what they were — and its rollback drops exactly those and applies
/// again. The tables keep the integrity the sprints rely on: one Active sprint per team, a name once per team ignoring case, a
/// number once per team, an item's sprint belongs to the item's owning team, and a sprint of a team cannot be left without it. The
/// database is this class's own: the test rolls a migration back.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
public sealed class AddSprintsMigrationTests(OrganizationsDatabaseFixture database) : IClassFixture<OrganizationsDatabaseFixture>
{
    private const string MigrationName = "_AddSprints";

    [Fact]
    public async Task Migrator_AddSprints_IsAdditive_RollsBackToItsTablesAndColumn_AndAppliesAgain()
    {
        // The world is seeded at the head of the migrations, with the current model (mod-021 put a required colour and a department on the
        // rows the fixtures write); the rollback below then walks back through every later migration down to the one before AddSprints.
        await using (var head = database.NewContext())
        {
            await head.Database.MigrateAsync();
        }
        var world = await SeedStoryAsync();

        await RollBackOneStepAsync();

        Assert.False(await TableExistsAsync("sprints"), "Rolling back must drop sprints.");
        Assert.False(await TableExistsAsync("sprint_counters"), "Rolling back must drop sprint_counters.");
        Assert.False(await TableExistsAsync("sprint_snapshot_items"), "Rolling back must drop sprint_snapshot_items.");
        Assert.Equal(["0"], await database.QueryAsync(
            "SELECT count(*)::text FROM information_schema.columns WHERE table_name = 'work_items' AND column_name = 'sprint_id'"));
        Assert.Equal(["1"], await database.QueryAsync("SELECT count(*)::text FROM work_items WHERE id = @item", ("item", world.Item)));

        await MigrateToAsync(MigrationName);

        Assert.True(await TableExistsAsync("sprints"));
        Assert.Equal(["1"], await database.QueryAsync("SELECT count(*)::text FROM work_items WHERE id = @item AND sprint_id IS NULL", ("item", world.Item)));
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

    [Fact]
    public async Task Tables_KeepTheirIntegrity()
    {
        await using (var context = database.NewContext())
        {
            await context.Database.MigrateAsync();
        }

        var world = await SeedStoryAsync();
        var first = await InsertSprintAsync(world, 1, "Один", "Active");

        // One Active sprint per team; a number and a name (ignoring case, kept normalized) once per team.
        await Assert.ThrowsAsync<PostgresException>(() => InsertSprintAsync(world, 2, "Два", "Active"));
        await Assert.ThrowsAsync<PostgresException>(() => InsertSprintAsync(world, 1, "Три", "Planned"));
        await Assert.ThrowsAsync<PostgresException>(() => InsertSprintAsync(world, 3, "Один", "Planned"));
        var other = await InsertSprintAsync(world, 4, "Четыре", "Planned");

        // A sprint that is Active or Closed has dates; dates come in a pair and in order.
        await Assert.ThrowsAsync<PostgresException>(() => database.ExecuteAsync(
            "UPDATE sprints SET start_date = NULL, end_date = NULL WHERE id = @id", ("id", first)));
        await Assert.ThrowsAsync<PostgresException>(() => database.ExecuteAsync(
            "UPDATE sprints SET start_date = DATE '2026-09-10', end_date = DATE '2026-09-01' WHERE id = @id", ("id", other)));

        // The item's sprint is one of its owning team, and an item with no team has none.
        await database.ExecuteAsync(
            "UPDATE work_items SET owning_team_id = @team, sprint_id = @sprint WHERE id = @item",
            ("team", world.Team), ("sprint", first), ("item", world.Item));
        await Assert.ThrowsAsync<PostgresException>(() => database.ExecuteAsync(
            "UPDATE work_items SET owning_team_id = @team WHERE id = @item", ("team", world.OtherTeam), ("item", world.Item)));
        await Assert.ThrowsAsync<PostgresException>(() => database.ExecuteAsync(
            "UPDATE work_items SET owning_team_id = NULL WHERE id = @item", ("item", world.Item)));

        // A sprint holding an item cannot be deleted, and the snapshot has no key to the item: a purge leaves it.
        await Assert.ThrowsAsync<PostgresException>(() => database.ExecuteAsync("DELETE FROM sprints WHERE id = @id", ("id", first)));
        await database.ExecuteAsync(
            """
            INSERT INTO sprint_snapshot_items (sprint_id, work_item_id, project_id, type_id, number, level_id, assignee_id, story_points, is_done)
            SELECT @sprint, @item, work_item.project_id, work_item.type_id, work_item.number, type.backlog_level_id, NULL, 3, true
            FROM work_items AS work_item JOIN work_item_types AS type ON type.id = work_item.type_id WHERE work_item.id = @item
            """,
            ("sprint", first), ("item", world.Item));
        await database.ExecuteAsync("UPDATE work_items SET sprint_id = NULL WHERE id = @item", ("item", world.Item));
        await database.ExecuteAsync("DELETE FROM work_items WHERE id = @item", ("item", world.Item));

        Assert.Equal(["1"], await database.QueryAsync("SELECT count(*)::text FROM sprint_snapshot_items WHERE sprint_id = @sprint", ("sprint", first)));
    }

    private async Task<Guid> InsertSprintAsync(World world, int number, string name, string state)
    {
        var id = Guid.CreateVersion7();
        var dated = state != "Planned";

        await database.ExecuteAsync(
            """
            INSERT INTO sprints (id, organization_id, team_id, number, name, normalized_name, state, start_date, end_date, created_at, created_by)
            VALUES (@id, @organization, @team, @number, @name, upper(@name), @state,
                    CASE WHEN @dated THEN DATE '2026-09-01' END, CASE WHEN @dated THEN DATE '2026-09-12' END, now(), 'migration-test')
            """,
            ("id", id),
            ("organization", world.Organization),
            ("team", world.Team),
            ("number", number),
            ("name", name),
            ("state", state),
            ("dated", dated));

        return id;
    }

    /// <summary>A project with one story owned by its first team, and the second team of the organization.</summary>
    private async Task<World> SeedStoryAsync()
    {
        var structure = await database.CreateStructureAsync();
        var organization = structure.Organization.Id;
        var row = (await database.QueryAsync(
            """
            SELECT concat_ws('|', type.id::text, state.id::text)
            FROM work_item_types AS type
            JOIN processes AS process ON process.id = type.process_id
            JOIN state_definitions AS state ON state.work_item_type_id = type.id AND state.position = 1
            WHERE process.organization_id = @organization AND type.key = 'US'
            """,
            ("organization", organization))).Single().Split('|');
        var item = Guid.CreateVersion7();

        await database.ExecuteAsync(
            """
            INSERT INTO work_items (id, project_id, number, type_id, state_id, area_id, title, description, creation_request_id, created_at, created_by, changed_at, owning_team_id)
            VALUES (@item, @project, 1, @type, @state, @area, 'История', '', gen_random_uuid(), now(), 'migration-test', now(), @team)
            """,
            ("item", item),
            ("project", structure.Project.Project.Id),
            ("type", Guid.Parse(row[0])),
            ("state", Guid.Parse(row[1])),
            ("area", structure.Project.Root.Id),
            ("team", structure.FirstTeam.Id));

        return new World(organization, structure.FirstTeam.Id, structure.SecondTeam.Id, item);
    }

    /// <summary>Migrates to the migration before AddSprints, found by name, never hard-coded.</summary>
    private async Task RollBackOneStepAsync()
    {
        await using var context = database.NewContext();

        var previous = context.Database.GetMigrations()
            .TakeWhile(id => !id.EndsWith(MigrationName, StringComparison.Ordinal))
            .Last();

        await context.GetService<IMigrator>().MigrateAsync(previous);
    }

    /// <summary>Migrates to AddSprints itself, forward or back, found by name.</summary>
    private async Task MigrateToAsync(string suffix)
    {
        await using var context = database.NewContext();
        var target = context.Database.GetMigrations().Single(id => id.EndsWith(suffix, StringComparison.Ordinal));

        await context.GetService<IMigrator>().MigrateAsync(target);
    }

    private async Task<bool> TableExistsAsync(string table) =>
        (await database.QueryAsync("SELECT (to_regclass(@table) IS NOT NULL)::text", ("table", table))).Single() == "true";

    private sealed record World(Guid Organization, Guid Team, Guid OtherTeam, Guid Item);
}
