using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Mustask.Infrastructure.Tests.Process;
using Npgsql;

namespace Mustask.Infrastructure.Tests;

/// <summary>
/// mod-001-process-seed, US-2 · "An unmigrated Development stand starts without the
/// demonstration process" (status.md D-13): <see cref="MigrationsState"/> is what the
/// Development seed asks before it writes anything. It only reads, and it answers "how
/// many migrations of this build are pending" for every state a stand can be in.
/// Scenario databases are created beside the fixture's own and dropped afterwards.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(ProcessDatabaseCollection.Name)]
public sealed class MigrationsStateTests(ProcessDatabaseFixture database)
{
    [Fact]
    public async Task CountPendingAsync_FullyMigratedDatabase_IsZero()
    {
        await using var context = database.NewContext();

        Assert.Equal(0, await MigrationsState.CountPendingAsync(context, CancellationToken.None));
    }

    [Fact]
    public async Task CountPendingAsync_DatabaseBehindThisBuild_CountsTheMissingMigrations()
    {
        await using var scenario = await Scenario.CreateAsync(database.ConnectionString);
        await using var context = scenario.NewContext();
        var migrations = context.Database.GetMigrations().ToList();
        var index = migrations.FindIndex(id => id.EndsWith("_AddProcessDefinitions", StringComparison.Ordinal));
        Assert.True(index > 0, "AddProcessDefinitions must come after at least one migration.");

        // Everything before this task's migration is applied, found by name and never by position.
        await context.GetService<IMigrator>().MigrateAsync(migrations[index - 1]);

        Assert.Equal(migrations.Count - index, await MigrationsState.CountPendingAsync(context, CancellationToken.None));
    }

    [Fact]
    public async Task CountPendingAsync_DatabaseWithNoHistoryTable_IsEveryMigrationAndAppliesNone()
    {
        await using var scenario = await Scenario.CreateAsync(database.ConnectionString);
        await using var context = scenario.NewContext();

        var pending = await MigrationsState.CountPendingAsync(context, CancellationToken.None);

        Assert.Equal(context.Database.GetMigrations().Count(), pending);
        Assert.False(
            await scenario.TableExistsAsync("__EFMigrationsHistory"),
            "Asking about the migrations must never apply one: ADR-0002.");
    }

    [Fact]
    public async Task CountPendingAsync_DatabaseThatDoesNotExist_IsEveryMigration()
    {
        var missing = new NpgsqlConnectionStringBuilder(database.ConnectionString) { Database = $"missing_{Guid.NewGuid():N}" }.ConnectionString;
        await using var context = new MustaskDbContext(new DbContextOptionsBuilder<MustaskDbContext>().UseNpgsql(missing).Options);

        var pending = await MigrationsState.CountPendingAsync(context, CancellationToken.None);

        Assert.Equal(context.Database.GetMigrations().Count(), pending);
    }

    /// <summary>An empty database beside the fixture's, dropped when the test is done.</summary>
    private sealed class Scenario : IAsyncDisposable
    {
        private readonly string _adminConnectionString;
        private readonly string _name;

        private Scenario(string adminConnectionString, string name, string connectionString)
        {
            _adminConnectionString = adminConnectionString;
            _name = name;
            ConnectionString = connectionString;
        }

        private string ConnectionString { get; }

        public static async Task<Scenario> CreateAsync(string adminConnectionString)
        {
            var name = $"scenario_{Guid.NewGuid():N}";

            await using var connection = new NpgsqlConnection(adminConnectionString);
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = $"CREATE DATABASE \"{name}\"";
            await command.ExecuteNonQueryAsync();

            return new Scenario(
                adminConnectionString,
                name,
                new NpgsqlConnectionStringBuilder(adminConnectionString) { Database = name }.ConnectionString);
        }

        public MustaskDbContext NewContext() =>
            new(new DbContextOptionsBuilder<MustaskDbContext>().UseNpgsql(ConnectionString).Options);

        public async Task<bool> TableExistsAsync(string table)
        {
            await using var connection = new NpgsqlConnection(ConnectionString);
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT to_regclass(@name) IS NOT NULL";
            command.Parameters.AddWithValue("name", table);

            return (bool)(await command.ExecuteScalarAsync())!;
        }

        public async ValueTask DisposeAsync()
        {
            NpgsqlConnection.ClearAllPools();

            await using var connection = new NpgsqlConnection(_adminConnectionString);
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = $"DROP DATABASE IF EXISTS \"{_name}\" WITH (FORCE)";
            await command.ExecuteNonQueryAsync();
        }
    }
}
