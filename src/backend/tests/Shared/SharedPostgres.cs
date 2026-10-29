using Npgsql;
using Testcontainers.PostgreSql;

namespace Mustask.Testing;

/// <summary>
/// One PostgreSQL container per test assembly (tech-047). Before it every
/// fixture — and in Mustask.Infrastructure.Tests every test of the classes
/// with an <c>IAsyncLifetime</c> of their own — started a container and paid
/// for the start; now each of them gets a database of its own on this one
/// server instead: empty from <see cref="CreateDatabaseAsync"/>, or a copy of a
/// migrated template from <see cref="CreateDatabaseFromTemplateAsync"/>.
/// A database is never shared between two fixtures, so what a test sees is
/// what it saw with a server of its own. The file is linked into every test
/// project that needs it, so there is one implementation.
/// </summary>
internal static class SharedPostgres
{
    private static readonly Lazy<Task<PostgreSqlContainer>> Server = new(StartAsync);

    // CREATE DATABASE ... TEMPLATE refuses while another session is connected
    // to the template. Copies are made one at a time as a precaution; it costs
    // well under a second per fixture.
    private static readonly SemaphoreSlim TemplateCopy = new(1, 1);

    /// <summary>A new, empty database on the shared server; its connection string.</summary>
    public static async Task<string> CreateDatabaseAsync()
    {
        var server = await Server.Value;
        var name = NewName();

        await ExecuteAsync(server, $"CREATE DATABASE \"{name}\"");

        return ConnectionStringFor(server, name);
    }

    /// <summary>A new database copied from <paramref name="template"/>, which is built on first use.</summary>
    public static async Task<string> CreateDatabaseFromTemplateAsync(Template template)
    {
        var server = await Server.Value;
        var templateName = await template.EnsureBuiltAsync(server);
        var name = NewName();

        await TemplateCopy.WaitAsync();
        try
        {
            await ExecuteAsync(server, $"CREATE DATABASE \"{name}\" TEMPLATE \"{templateName}\"");
        }
        finally
        {
            TemplateCopy.Release();
        }

        return ConnectionStringFor(server, name);
    }

    /// <summary>
    /// A database built once per process — migrated, say — and copied for each
    /// fixture that asks for it. <c>build</c> gets the template's connection
    /// string; every connection it opened is cleared from the pool afterwards,
    /// since a template with a live session cannot be copied.
    /// </summary>
    public sealed class Template(string name, Func<string, Task> build)
    {
        private readonly SemaphoreSlim _gate = new(1, 1);
        private bool _built;

        internal async Task<string> EnsureBuiltAsync(PostgreSqlContainer server)
        {
            await _gate.WaitAsync();
            try
            {
                if (!_built)
                {
                    await ExecuteAsync(server, $"CREATE DATABASE \"{name}\"");
                    var connectionString = ConnectionStringFor(server, name);
                    await build(connectionString);
                    await using (var connection = new NpgsqlConnection(connectionString))
                    {
                        NpgsqlConnection.ClearPool(connection);
                    }

                    _built = true;
                }

                return name;
            }
            finally
            {
                _gate.Release();
            }
        }
    }

    private static async Task<PostgreSqlContainer> StartAsync()
    {
        var server = new PostgreSqlBuilder("postgres:18-alpine")
            .WithDatabase("mustask")
            // Every fixture's host keeps a connection pool of its own on the one
            // server; the default of 100 is the old budget of a single fixture.
            .WithCommand("-c", "max_connections=500")
            .Build();

        await server.StartAsync();

        // The resource reaper removes the container when the process ends; this
        // is for a run with the reaper turned off.
        AppDomain.CurrentDomain.ProcessExit += (_, _) => server.DisposeAsync().AsTask().Wait(TimeSpan.FromSeconds(10));

        return server;
    }

    private static string NewName() => $"test_{Guid.NewGuid():N}";

    private static string ConnectionStringFor(PostgreSqlContainer server, string database) =>
        new NpgsqlConnectionStringBuilder(server.GetConnectionString()) { Database = database }.ConnectionString;

    private static async Task ExecuteAsync(PostgreSqlContainer server, string sql)
    {
        await using var connection = new NpgsqlConnection(server.GetConnectionString());
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }
}
