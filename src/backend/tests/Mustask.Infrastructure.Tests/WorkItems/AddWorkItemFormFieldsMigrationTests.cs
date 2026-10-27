using Mustask.Domain.WorkItems;
using Mustask.Infrastructure.Tests.Organizations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace Mustask.Infrastructure.Tests.WorkItems;

/// <summary>
/// feat-022 US-5 · "An item created before this change gets the same defaults"
/// (BR-M3-37, api-contract § 4): <c>AddWorkItemFormFields</c> gives every item
/// that exists when it runs the values a new item of its type takes, by the
/// type's appearance token, and leaves <c>changed_at</c> — so «Изменён» and the
/// list's order — as it was. Rolling it back drops exactly its columns, and it
/// re-applies. The database is this class's own: the test rolls a migration back.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
public sealed class AddWorkItemFormFieldsMigrationTests(OrganizationsDatabaseFixture database) : IClassFixture<OrganizationsDatabaseFixture>
{
    private static readonly DateTimeOffset Created = new(2026, 10, 3, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Changed = new(2026, 10, 4, 10, 30, 0, TimeSpan.Zero);

    private static readonly string[] NewColumns =
    [
        "priority", "risk", "story_points", "effort", "business_value", "time_criticality", "start_date", "target_date", "severity",
        "activity", "original_estimate", "remaining_work", "completed_work", "value_area", "acceptance_criteria", "repro_steps", "system_info",
    ];

    [Fact]
    public async Task Migrator_AddWorkItemFormFields_GivesExistingItemsTheDefaults_KeepsChangedAt_AndRollsBack()
    {
        var structure = await database.CreateStructureAsync();
        var projectId = structure.Project.Id;

        // One item of each of the five types, as the API writes them, in the seeded order.
        await using (var context = database.NewContext())
        {
            var processId = structure.Organization.ProcessId;
            var types = await context.WorkItemTypes.Include(type => type.States).Where(type => type.ProcessId == processId).ToListAsync();
            var number = 0;

            foreach (var type in types.OrderBy(type => type.Position))
            {
                var states = new WorkItemTypeStates(
                    type.Id, type.Key, type.Appearance, type.States.Select(state => new WorkItemTypeState(state.Id, state.Position, state.Category)).ToList());

                context.WorkItems.Add(WorkItem.Create(
                    projectId, ++number, states, $"Элемент {type.Key}", Guid.CreateVersion7(), Created, "migration-test"));
            }

            await context.SaveChangesAsync();
        }

        // Values that only the rolled-back columns hold, and a «Изменён» of their own the migration must not move.
        await database.ExecuteAsync(
            "UPDATE work_items SET priority = 4, story_points = 5, changed_at = @changed WHERE project_id = @project",
            ("changed", Changed),
            ("project", projectId));

        await RollBackAsync();

        foreach (var column in NewColumns)
        {
            Assert.False(await ColumnExistsAsync(column), $"Rolling back AddWorkItemFormFields must drop work_items.{column}.");
        }

        Assert.Equal(5, await CountAsync("SELECT count(*) FROM work_items WHERE project_id = @project", projectId));

        await using (var context = database.NewContext())
        {
            await context.Database.MigrateAsync();
        }

        // key|priority|severity|value area|acceptance criteria|repro steps|system info|other fields set|changed_at kept;
        // '-' is NULL, an empty slot is "".
        Assert.Equal(
            [
                "EPIC|2|-|Business|-|-|-|0|t",
                "FEAT|2|-|Business|-|-|-|0|t",
                "US|2|-|Business||-|-|0|t",
                "BUG|2|3|-|-|||0|t",
                "TASK|2|-|-|-|-|-|0|t",
            ],
            await RowsAsync(projectId));
    }

    /// <summary>Migrates to the migration before AddWorkItemFormFields, found by name, never hard-coded.</summary>
    private async Task RollBackAsync()
    {
        await using var context = database.NewContext();

        var previous = context.Database.GetMigrations()
            .TakeWhile(id => !id.EndsWith("_AddWorkItemFormFields", StringComparison.Ordinal))
            .Last();

        await context.GetService<IMigrator>().MigrateAsync(previous);
    }

    private async Task<List<string>> RowsAsync(Guid projectId)
    {
        const string Sql =
            """
            SELECT concat_ws('|',
                kind.key,
                item.priority,
                coalesce(item.severity::text, '-'),
                coalesce(item.value_area, '-'),
                coalesce(item.acceptance_criteria, '-'),
                coalesce(item.repro_steps, '-'),
                coalesce(item.system_info, '-'),
                num_nonnulls(item.risk, item.story_points, item.effort, item.business_value, item.time_criticality, item.start_date,
                    item.target_date, item.activity, item.original_estimate, item.remaining_work, item.completed_work),
                CASE WHEN item.changed_at = @changed THEN 't' ELSE 'f' END)
            FROM work_items AS item
            JOIN work_item_types AS kind ON kind.id = item.type_id
            WHERE item.project_id = @project
            ORDER BY item.number
            """;

        await using var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(Sql, connection);
        command.Parameters.AddWithValue("changed", Changed);
        command.Parameters.AddWithValue("project", projectId);

        var rows = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            rows.Add(reader.GetString(0));
        }

        return rows;
    }

    private async Task<long> CountAsync(string sql, Guid projectId)
    {
        await using var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("project", projectId);

        return (long)(await command.ExecuteScalarAsync())!;
    }

    private async Task<bool> ColumnExistsAsync(string column)
    {
        await using var connection = new NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(
            "SELECT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = 'work_items' AND column_name = @column)",
            connection);
        command.Parameters.AddWithValue("column", column);

        return await command.ExecuteScalarAsync() is true;
    }
}
