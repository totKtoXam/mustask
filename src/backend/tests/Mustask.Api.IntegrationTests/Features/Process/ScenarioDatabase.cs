using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Mustask.Infrastructure;
using Npgsql;

namespace Mustask.Api.IntegrationTests.Features.Process;

/// <summary>How far the migrations of a scenario database are applied.</summary>
internal enum ScenarioSchema
{
    /// <summary>No migration at all: not even the history table exists.</summary>
    Empty,

    /// <summary>Every migration except <c>AddProcessDefinitions</c> (the stand of an external database that was migrated before this build).</summary>
    AllButProcessDefinitions,

    /// <summary>Every migration of this build.</summary>
    Latest,
}

/// <summary>
/// A throw-away database on the PostgreSQL container the whole <c>MustaskApiCollection</c>
/// shares, for the scenarios the shared, fully migrated database cannot show: a stand
/// whose schema is behind this build, an empty one, a seed that fails. It is created
/// beside the shared database and dropped afterwards, so the tests that count rows in
/// the shared one never see it.
/// </summary>
internal sealed class ScenarioDatabase : IAsyncDisposable
{
    private const string ConnectionStringEnvVar = "ConnectionStrings__mustask";

    private readonly string _adminConnectionString;
    private readonly string _name;

    private ScenarioDatabase(string adminConnectionString, string name, string connectionString)
    {
        _adminConnectionString = adminConnectionString;
        _name = name;
        ConnectionString = connectionString;
    }

    public string ConnectionString { get; }

    /// <summary>
    /// Creates the database. Call it before any test switches the process-wide connection
    /// string variable: the shared container is found through that variable.
    /// </summary>
    public static async Task<ScenarioDatabase> CreateAsync(ScenarioSchema schema)
    {
        var shared = Environment.GetEnvironmentVariable(ConnectionStringEnvVar)
            ?? throw new InvalidOperationException($"{ConnectionStringEnvVar} is not set: the shared test host is not initialised.");
        var name = $"scenario_{Guid.NewGuid():N}";
        var connectionString = new NpgsqlConnectionStringBuilder(shared) { Database = name }.ConnectionString;

        await using (var connection = new NpgsqlConnection(shared))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = $"CREATE DATABASE \"{name}\"";
            await command.ExecuteNonQueryAsync();
        }

        var database = new ScenarioDatabase(shared, name, connectionString);

        await using var context = database.NewContext();

        switch (schema)
        {
            case ScenarioSchema.Latest:
                await context.Database.MigrateAsync();
                break;

            case ScenarioSchema.AllButProcessDefinitions:
                var migrations = context.Database.GetMigrations().ToList();
                var index = migrations.FindIndex(id => id.EndsWith("_AddProcessDefinitions", StringComparison.Ordinal));
                Assert.True(index > 0, "AddProcessDefinitions must come after at least one migration.");

                // Found by name and never by position, so a migration that lands before this one does not break it.
                await context.GetService<IMigrator>().MigrateAsync(migrations[index - 1]);
                break;
        }

        return database;
    }

    public MustaskDbContext NewContext() =>
        new(new DbContextOptionsBuilder<MustaskDbContext>().UseNpgsql(ConnectionString).Options);

    /// <summary>Whether a table exists in the scenario database.</summary>
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
