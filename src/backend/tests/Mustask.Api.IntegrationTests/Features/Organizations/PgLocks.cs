using Npgsql;

namespace Mustask.Api.IntegrationTests.Features.Organizations;

/// <summary>
/// Waits on the database's own view of its sessions, so a race test never
/// sleeps for a guessed time (mod-002 plan-backend § 8.3; mod-001's race test
/// does the same). The integration collection runs sequentially, so a blocked
/// session of the test database belongs to the running test.
/// </summary>
public static class PgLocks
{
    private static readonly TimeSpan Deadline = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Returns once some session of the database waits on a lock, or as soon as
    /// <paramref name="finished"/> completes (the second request did not conflict).
    /// </summary>
    public static async Task WaitUntilBlockedAsync(string connectionString, Task? finished = null)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();

        var deadline = DateTime.UtcNow + Deadline;

        while (DateTime.UtcNow < deadline)
        {
            if (finished is { IsCompleted: true })
            {
                return;
            }

            await using var command = connection.CreateCommand();
            command.CommandText =
                "SELECT count(*) FROM pg_stat_activity WHERE datname = current_database() AND pid <> pg_backend_pid() AND wait_event_type = 'Lock'";

            if ((long)(await command.ExecuteScalarAsync())! > 0)
            {
                return;
            }

            await Task.Delay(25);
        }

        throw new TimeoutException($"No session of the database was blocked on a lock within {Deadline.TotalSeconds} seconds.");
    }
}
