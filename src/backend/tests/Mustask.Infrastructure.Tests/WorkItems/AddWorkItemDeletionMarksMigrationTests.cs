using Mustask.Infrastructure.Tests.Organizations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Mustask.Infrastructure.Tests.WorkItems;

/// <summary>
/// mod-005 (ADR-0030 rule 1, BR-DR-18): <c>AddWorkItemDeletionMarks</c> is additive — three nullable columns, a key, a check
/// and an index, no data moved and nothing dropped — and its rollback drops exactly those and applies again. The database is
/// this class's own: the test rolls a migration back.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
public sealed class AddWorkItemDeletionMarksMigrationTests(OrganizationsDatabaseFixture database) : IClassFixture<OrganizationsDatabaseFixture>
{
    private const string MigrationName = "_AddWorkItemDeletionMarks";

    private static readonly string[] Columns = ["deleted_at", "deleted_by", "deleted_from_state_id"];

    [Fact]
    public async Task Migrator_AddWorkItemDeletionMarks_IsAdditive_RollsBackToItsColumnsKeyCheckAndIndex_AndAppliesAgain()
    {
        var world = await SeedAsync();

        Assert.Equal(Columns, await ColumnsAsync());

        await RollBackAsync();

        Assert.Empty(await ColumnsAsync());
        Assert.Empty(await ConstraintsAsync());
        Assert.False(await IndexExistsAsync(), "Rolling back must drop the partial index.");
        Assert.Equal(["1"], await database.QueryAsync("SELECT count(*)::text FROM work_items WHERE id = @item", ("item", world.ItemId)));
        Assert.Equal(["7"], await database.QueryAsync("SELECT last_number::text FROM work_item_counters WHERE project_id = @project", ("project", world.ProjectId)));

        await using (var context = database.NewContext())
        {
            await context.Database.MigrateAsync();
        }

        Assert.Equal(Columns, await ColumnsAsync());
        Assert.Equal(
            ["ck_work_items_deletion_marks", "fk_work_items_state_definitions_deleted_from_state_id"],
            await ConstraintsAsync());
        Assert.True(await IndexExistsAsync());

        // Nothing moved: the item is as it was and carries no marks; the counter is the one it was.
        Assert.Equal(["1"], await database.QueryAsync("SELECT count(*)::text FROM work_items WHERE id = @item", ("item", world.ItemId)));
        Assert.Equal(
            ["NULL|NULL|NULL"],
            await database.QueryAsync(
                "SELECT concat_ws('|', coalesce(deleted_from_state_id::text, 'NULL'), coalesce(deleted_by, 'NULL'), coalesce(deleted_at::text, 'NULL')) FROM work_items WHERE id = @item",
                ("item", world.ItemId)));
        Assert.Equal(["7"], await database.QueryAsync("SELECT last_number::text FROM work_item_counters WHERE project_id = @project", ("project", world.ProjectId)));
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

    private async Task<World> SeedAsync()
    {
        await using (var context = database.NewContext())
        {
            await context.Database.MigrateAsync();
        }

        var structure = await database.CreateStructureAsync();
        var project = structure.Project.Project.Id;
        var row = (await database.QueryAsync(
            """
            SELECT concat_ws('|', type.id::text, state.id::text)
            FROM work_item_types AS type
            JOIN processes AS process ON process.id = type.process_id
            JOIN state_definitions AS state ON state.work_item_type_id = type.id AND state.position = 1
            WHERE process.organization_id = @organization AND type.key = 'US'
            """,
            ("organization", structure.Organization.Id))).Single().Split('|');
        var item = Guid.CreateVersion7();

        await database.ExecuteAsync(
            """
            INSERT INTO work_items (id, project_id, number, type_id, state_id, area_id, title, description, creation_request_id, created_at, created_by, changed_at)
            VALUES (@item, @project, 1, @type, @state, @area, 'История', '', gen_random_uuid(), now(), 'migration-test', now());
            INSERT INTO work_item_counters (project_id, last_number) VALUES (@project, 7)
            """,
            ("item", item),
            ("project", project),
            ("type", Guid.Parse(row[0])),
            ("state", Guid.Parse(row[1])),
            ("area", structure.Project.Root.Id));

        return new World(project, item);
    }

    /// <summary>Migrates to the migration before AddWorkItemDeletionMarks, found by name, never hard-coded.</summary>
    private async Task RollBackAsync()
    {
        await using var context = database.NewContext();

        var previous = context.Database.GetMigrations()
            .TakeWhile(id => !id.EndsWith(MigrationName, StringComparison.Ordinal))
            .Last();

        await context.GetService<IMigrator>().MigrateAsync(previous);
    }

    private Task<List<string>> ColumnsAsync() =>
        database.QueryAsync(
            "SELECT column_name FROM information_schema.columns WHERE table_name = 'work_items' AND column_name LIKE 'deleted\\_%' ORDER BY column_name");

    private Task<List<string>> ConstraintsAsync() =>
        database.QueryAsync(
            "SELECT conname FROM pg_constraint WHERE conrelid = 'work_items'::regclass AND (conname LIKE '%deletion\\_marks' OR conname LIKE '%deleted\\_from\\_state\\_id') ORDER BY conname");

    private async Task<bool> IndexExistsAsync() =>
        (await database.QueryAsync("SELECT (to_regclass('ix_work_items_deleted_from_state_id') IS NOT NULL)::text")).Single() == "true";

    private sealed record World(Guid ProjectId, Guid ItemId);
}
