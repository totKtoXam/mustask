using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Testcontainers.PostgreSql;

namespace Mustask.Infrastructure.Tests;

// ADR-0002: migrations are
// applied by an explicit call to Database.MigrateAsync() (dotnet ef database
// update locally / same call reused by tests and future CI), never
// automatically from Mustask.Api's startup. These tests exercise that exact
// mechanism against a real, disposable Postgres (Testcontainers) using the
// real migrations (InitialCreate): applies on a
// clean DB, re-running is idempotent, and rollback works.
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
public sealed class MigrationsTests : IAsyncLifetime
{
    private const string ConnectionStringEnvVar = "ConnectionStrings__mustask";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:18-alpine")
        .WithDatabase("mustask")
        .Build();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        Environment.SetEnvironmentVariable(ConnectionStringEnvVar, _postgres.GetConnectionString());
    }

    public async Task DisposeAsync()
    {
        await _postgres.DisposeAsync();
        Environment.SetEnvironmentVariable(ConnectionStringEnvVar, null);
    }

    private static MustaskDbContext CreateContext() => new MustaskDbContextFactory().CreateDbContext([]);

    [Fact]
    public async Task MigrateAsync_CleanDatabase_AppliesInitialCreateMigration()
    {
        using var context = CreateContext();

        await context.Database.MigrateAsync();

        var applied = await context.Database.GetAppliedMigrationsAsync();
        Assert.Contains(applied, id => id.EndsWith("_InitialCreate", StringComparison.Ordinal));
    }

    [Fact]
    public async Task MigrateAsync_CalledTwiceOnSameDatabase_IsIdempotent()
    {
        using var context = CreateContext();
        await context.Database.MigrateAsync();

        var exception = await Record.ExceptionAsync(() => context.Database.MigrateAsync());

        Assert.Null(exception);
        // Считаем ожидаемое число миграций динамически (не хардкодим), чтобы
        // тест не ломался при каждой новой миграции.
        Assert.Equal(context.Database.GetMigrations().Count(), (await context.Database.GetAppliedMigrationsAsync()).Count());
    }

    [Fact]
    public async Task Migrator_RollbackToZeroThenReapply_RemovesAndRestoresMigrationHistory()
    {
        using var context = CreateContext();
        await context.Database.MigrateAsync();

        var migrator = context.GetService<IMigrator>();
        await migrator.MigrateAsync("0");
        Assert.Empty(await context.Database.GetAppliedMigrationsAsync());

        await context.Database.MigrateAsync();
        // Считаем ожидаемое число миграций динамически (не хардкодим), чтобы
        // тест не ломался при каждой новой миграции.
        Assert.Equal(context.Database.GetMigrations().Count(), (await context.Database.GetAppliedMigrationsAsync()).Count());
    }

    private static readonly string[] DictionaryTables = ["languages"];

    /// <summary>
    /// InitialCreate creates the dictionary tables, and its Down removes them
    /// again. Reversibility is checked by behavior (rolling back to "0"
    /// drops the tables), not by reading the migration code — backend.md,
    /// "Every migration is reversible".
    /// </summary>
    [Fact]
    public async Task Migrator_InitialCreate_CreatesDictionaryTables_AndRollbackRemovesThem()
    {
        using var context = CreateContext();
        await context.Database.MigrateAsync();

        foreach (var table in DictionaryTables)
        {
            Assert.True(
                await TableExistsAsync(context, table),
                $"Таблица справочника '{table}' обязана появиться после InitialCreate.");
        }

        var migrator = context.GetService<IMigrator>();
        await migrator.MigrateAsync("0");

        foreach (var table in DictionaryTables)
        {
            Assert.False(
                await TableExistsAsync(context, table),
                $"Откат InitialCreate обязан удалить таблицу '{table}'.");
        }
    }

    private static readonly string[] ProcessTables = ["processes", "backlog_levels", "work_item_types", "state_definitions"];

    /// <summary>
    /// mod-001-process-seed, "Migrations (DoD)": AddProcessDefinitions creates the
    /// four M2 tables, rolling back to the migration before it removes exactly
    /// those and leaves the earlier ones, and reapplying brings them back. The
    /// target of the rollback is whatever precedes AddProcessDefinitions in the
    /// migration list, found by this migration's name and never hard-coded, so a
    /// migration that lands before this one does not break the test.
    /// </summary>
    [Fact]
    public async Task Migrator_AddProcessDefinitions_CreatesTheFourTables_AndRollbackRemovesThem()
    {
        using var context = CreateContext();
        await context.Database.MigrateAsync();

        foreach (var table in ProcessTables)
        {
            Assert.True(await TableExistsAsync(context, table), $"Process table '{table}' must exist after AddProcessDefinitions.");
        }

        var migrations = context.Database.GetMigrations().ToList();
        var index = migrations.FindIndex(id => id.EndsWith("_AddProcessDefinitions", StringComparison.Ordinal));
        Assert.True(index > 0, "AddProcessDefinitions must come after at least one migration.");

        var migrator = context.GetService<IMigrator>();
        await migrator.MigrateAsync(migrations[index - 1]);

        foreach (var table in ProcessTables)
        {
            Assert.False(await TableExistsAsync(context, table), $"Rolling back AddProcessDefinitions must drop '{table}'.");
        }

        foreach (var table in DictionaryTables)
        {
            Assert.True(await TableExistsAsync(context, table), $"Rolling back AddProcessDefinitions must keep '{table}'.");
        }

        await context.Database.MigrateAsync();

        foreach (var table in ProcessTables)
        {
            Assert.True(await TableExistsAsync(context, table), $"Reapplying AddProcessDefinitions must recreate '{table}'.");
        }
    }

    private static readonly string[] OrganizationTables = ["organizations", "projects", "teams", "areas"];

    /// <summary>
    /// mod-002-organizations-projects-teams (stage 4 DoD, "Migrations apply on a
    /// clean database and repeatedly"): AddOrganizationStructure creates M1's four
    /// tables, rolling back to the migration before it drops exactly those and
    /// keeps the process tables its key points into, and reapplying brings them
    /// back. The rollback target is found by name, never hard-coded.
    /// </summary>
    [Fact]
    public async Task Migrator_AddOrganizationStructure_CreatesTheFourTables_AndRollbackRemovesThem()
    {
        using var context = CreateContext();
        await context.Database.MigrateAsync();

        foreach (var table in OrganizationTables)
        {
            Assert.True(await TableExistsAsync(context, table), $"Table '{table}' must exist after AddOrganizationStructure.");
        }

        var migrations = context.Database.GetMigrations().ToList();
        var index = migrations.FindIndex(id => id.EndsWith("_AddOrganizationStructure", StringComparison.Ordinal));
        Assert.True(index > 0, "AddOrganizationStructure must come after at least one migration.");

        var migrator = context.GetService<IMigrator>();
        await migrator.MigrateAsync(migrations[index - 1]);

        foreach (var table in OrganizationTables)
        {
            Assert.False(await TableExistsAsync(context, table), $"Rolling back AddOrganizationStructure must drop '{table}'.");
        }

        foreach (var table in ProcessTables)
        {
            Assert.True(await TableExistsAsync(context, table), $"Rolling back AddOrganizationStructure must keep '{table}'.");
        }

        await context.Database.MigrateAsync();

        foreach (var table in OrganizationTables)
        {
            Assert.True(await TableExistsAsync(context, table), $"Reapplying AddOrganizationStructure must recreate '{table}'.");
        }
    }

    private static async Task<bool> TableExistsAsync(MustaskDbContext context, string tableName)
    {
        var connection = context.Database.GetDbConnection();
        await context.Database.OpenConnectionAsync();
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText =
                "SELECT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = @table_name)";

            var parameter = command.CreateParameter();
            parameter.ParameterName = "table_name";
            parameter.Value = tableName;
            command.Parameters.Add(parameter);

            var result = await command.ExecuteScalarAsync();
            return result is true;
        }
        finally
        {
            await context.Database.CloseConnectionAsync();
        }
    }

    private static readonly string[] UserDirectoryTables = ["directory_users", "directory_snapshots", "directory_snapshot_pages"];

    /// <summary>
    /// mod-014: AddUserDirectory is additive and reversible — rolling back to
    /// the migration before it drops its three tables and nothing else, and
    /// re-applying restores them.
    /// </summary>
    [Fact]
    public async Task Migrator_AddUserDirectory_CreatesItsTables_AndRollbackToThePreviousRemovesOnlyThem()
    {
        using var context = CreateContext();
        await context.Database.MigrateAsync();

        foreach (var table in UserDirectoryTables)
        {
            Assert.True(await TableExistsAsync(context, table), $"Table '{table}' must exist after AddUserDirectory.");
        }

        var previous = context.Database.GetMigrations()
            .TakeWhile(id => !id.EndsWith("_AddUserDirectory", StringComparison.Ordinal))
            .Last();

        var migrator = context.GetService<IMigrator>();
        await migrator.MigrateAsync(previous);

        foreach (var table in UserDirectoryTables)
        {
            Assert.False(await TableExistsAsync(context, table), $"Rolling back AddUserDirectory must drop '{table}'.");
        }

        Assert.True(await TableExistsAsync(context, "languages"), "Rolling back AddUserDirectory must leave earlier tables.");

        await context.Database.MigrateAsync();

        foreach (var table in UserDirectoryTables)
        {
            Assert.True(await TableExistsAsync(context, table), $"Re-applying AddUserDirectory must restore '{table}'.");
        }
    }

    /// <summary>
    /// feat-001: AddWorkItemLinks is additive and reversible — rolling back to
    /// the migration before it drops <c>work_items.parent_id</c> and
    /// <c>work_item_hierarchy_locks</c> and leaves <c>work_items</c> itself, and
    /// re-applying restores both.
    /// </summary>
    [Fact]
    public async Task Migrator_AddWorkItemLinks_AddsTheParentAndTheLockTable_AndRollbackRemovesOnlyThem()
    {
        using var context = CreateContext();
        await context.Database.MigrateAsync();

        Assert.True(await ColumnExistsAsync(context, "work_items", "parent_id"), "work_items.parent_id must exist after AddWorkItemLinks.");
        Assert.True(await TableExistsAsync(context, "work_item_hierarchy_locks"), "work_item_hierarchy_locks must exist after AddWorkItemLinks.");

        var previous = context.Database.GetMigrations()
            .TakeWhile(id => !id.EndsWith("_AddWorkItemLinks", StringComparison.Ordinal))
            .Last();

        await context.GetService<IMigrator>().MigrateAsync(previous);

        Assert.False(await ColumnExistsAsync(context, "work_items", "parent_id"), "Rolling back AddWorkItemLinks must drop work_items.parent_id.");
        Assert.False(await TableExistsAsync(context, "work_item_hierarchy_locks"), "Rolling back AddWorkItemLinks must drop work_item_hierarchy_locks.");
        Assert.True(await TableExistsAsync(context, "work_items"), "Rolling back AddWorkItemLinks must leave work_items.");

        await context.Database.MigrateAsync();

        Assert.True(await ColumnExistsAsync(context, "work_items", "parent_id"), "Re-applying AddWorkItemLinks must restore work_items.parent_id.");
        Assert.True(await TableExistsAsync(context, "work_item_hierarchy_locks"), "Re-applying AddWorkItemLinks must restore work_item_hierarchy_locks.");
    }

    private static async Task<bool> ColumnExistsAsync(MustaskDbContext context, string tableName, string columnName)
    {
        var connection = context.Database.GetDbConnection();
        await context.Database.OpenConnectionAsync();
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText =
                "SELECT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema = 'public' AND table_name = @table_name AND column_name = @column_name)";

            var table = command.CreateParameter();
            table.ParameterName = "table_name";
            table.Value = tableName;
            command.Parameters.Add(table);

            var column = command.CreateParameter();
            column.ParameterName = "column_name";
            column.Value = columnName;
            command.Parameters.Add(column);

            return await command.ExecuteScalarAsync() is true;
        }
        finally
        {
            await context.Database.CloseConnectionAsync();
        }
    }
}
