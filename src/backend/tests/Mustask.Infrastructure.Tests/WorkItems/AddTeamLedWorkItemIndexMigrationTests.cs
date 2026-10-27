using Mustask.Infrastructure.Tests.Organizations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Mustask.Infrastructure.Tests.WorkItems;

/// <summary>
/// feat-040 api-contract § 9 (ADR-0028 rule 13, BR-TS-14): <c>AddTeamLedWorkItemIndex</c> adds one plain index over
/// <c>work_items (owning_team_id, type_id)</c> and nothing else — no table, column or row is touched and nothing is dropped
/// or remapped — and its rollback drops exactly that index and applies again. The database is this class's own: the test
/// rolls a migration back.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
public sealed class AddTeamLedWorkItemIndexMigrationTests(OrganizationsDatabaseFixture database) : IClassFixture<OrganizationsDatabaseFixture>
{
    private const string MigrationName = "_AddTeamLedWorkItemIndex";

    private const string IndexName = "ix_work_items_owning_team_id_type_id";

    [Fact]
    public async Task Migrator_AddTeamLedWorkItemIndex_IsAdditive_RollsBackToTheIndex_AndAppliesAgain()
    {
        var world = await SeedAsync();

        Assert.Equal([$"{IndexName}|owning_team_id,type_id|f"], await IndexAsync());

        await RollBackAsync();

        Assert.Empty(await IndexAsync());
        Assert.Equal(["1"], await database.QueryAsync("SELECT count(*)::text FROM work_items WHERE id = @item", ("item", world.ItemId)));
        Assert.Equal(["7"], await database.QueryAsync("SELECT last_number::text FROM work_item_counters WHERE project_id = @project", ("project", world.ProjectId)));

        await using (var context = database.NewContext())
        {
            await context.Database.MigrateAsync();
        }

        // Applied again: the same plain index (not partial, not unique), and the rows are as they were.
        Assert.Equal([$"{IndexName}|owning_team_id,type_id|f"], await IndexAsync());
        Assert.Equal(["1"], await database.QueryAsync("SELECT count(*)::text FROM work_items WHERE id = @item", ("item", world.ItemId)));
        Assert.Equal(["7"], await database.QueryAsync("SELECT last_number::text FROM work_item_counters WHERE project_id = @project", ("project", world.ProjectId)));
    }

    // The project-led index stays: the project views and the owner key still read through it.
    [Fact]
    public async Task Migrator_KeepsTheProjectLedIndex()
    {
        await using (var context = database.NewContext())
        {
            await context.Database.MigrateAsync();
        }

        Assert.Equal(
            ["ix_work_items_project_id_owning_team_id_type_id"],
            await database.QueryAsync(
                "SELECT indexname FROM pg_indexes WHERE tablename = 'work_items' AND indexname = 'ix_work_items_project_id_owning_team_id_type_id'"));
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
        var project = structure.Project.Id;
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
            INSERT INTO work_items (id, project_id, number, type_id, state_id, title, description, creation_request_id, created_at, created_by, changed_at)
            VALUES (@item, @project, 1, @type, @state, 'История', '', gen_random_uuid(), now(), 'migration-test', now());
            INSERT INTO work_item_counters (project_id, last_number) VALUES (@project, 7)
            """,
            ("item", item),
            ("project", project),
            ("type", Guid.Parse(row[0])),
            ("state", Guid.Parse(row[1])));

        return new World(project, item);
    }

    /// <summary>Migrates to the migration before AddTeamLedWorkItemIndex, found by name, never hard-coded.</summary>
    private async Task RollBackAsync()
    {
        await using var context = database.NewContext();

        var previous = context.Database.GetMigrations()
            .TakeWhile(id => !id.EndsWith(MigrationName, StringComparison.Ordinal))
            .Last();

        await context.GetService<IMigrator>().MigrateAsync(previous);
    }

    /// <summary>The index by name: <c>name|columns in order|is unique or partial</c>, so a changed shape shows.</summary>
    private Task<List<string>> IndexAsync() =>
        database.QueryAsync(
            $"""
             SELECT concat_ws(
                 '|',
                 index_class.relname,
                 (SELECT string_agg(attribute.attname, ',' ORDER BY keys.ordinality)
                  FROM unnest(index.indkey) WITH ORDINALITY AS keys(attnum, ordinality)
                  JOIN pg_attribute AS attribute ON attribute.attrelid = index.indrelid AND attribute.attnum = keys.attnum),
                 CASE WHEN index.indisunique OR index.indpred IS NOT NULL THEN 't' ELSE 'f' END)
             FROM pg_index AS index
             JOIN pg_class AS index_class ON index_class.oid = index.indexrelid
             WHERE index.indrelid = 'work_items'::regclass AND index_class.relname = '{IndexName}'
             """);

    private sealed record World(Guid ProjectId, Guid ItemId);
}
