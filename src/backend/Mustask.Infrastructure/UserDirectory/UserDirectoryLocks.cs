using Microsoft.EntityFrameworkCore;

namespace Mustask.Infrastructure.UserDirectory;

/// <summary>
/// The user projection's transaction-scoped advisory locks (ADR-0025 § 6;
/// ADR-0011), the SQL in one place. A per-user lock also covers an id not
/// held yet, which a row lock cannot; the snapshot lock keeps begins, pages
/// and completions from interleaving. Both are released by the commit or
/// rollback of the transaction that took them.
/// </summary>
internal static class UserDirectoryLocks
{
    private const string UserKeyPrefix = "user_directory.user:";

    private const string SnapshotKey = "user_directory.snapshot";

    /// <summary>
    /// Locks every id's key, the keys in ascending order: two transactions
    /// locking overlapping sets take the shared keys in the same order, so
    /// they never wait on each other in a cycle. <c>pg_advisory_xact_lock</c>
    /// is volatile, so PostgreSQL evaluates it after the <c>ORDER BY</c>.
    /// </summary>
    public static async Task LockUsersAsync(MustaskDbContext context, string[] ids, CancellationToken cancellationToken)
    {
        if (ids.Length == 0)
        {
            return;
        }

        await context.Database.ExecuteSqlInterpolatedAsync(
            $"""
            SELECT pg_advisory_xact_lock(user_key)
            FROM (
                SELECT DISTINCT hashtextextended({UserKeyPrefix} || id, 0) AS user_key
                FROM unnest({ids}::text[]) AS requested(id)
            ) AS keys
            ORDER BY user_key
            """,
            cancellationToken);
    }

    public static async Task LockSnapshotsAsync(MustaskDbContext context, CancellationToken cancellationToken) =>
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock(hashtextextended({SnapshotKey}, 0))",
            cancellationToken);

    /// <summary>
    /// Row locks on the held users among <paramref name="ids"/>, in id order —
    /// taken after the advisory locks, before the tracking query loads the
    /// rows (the "lock by id with raw SQL, load with the regular query"
    /// technique of ADR-0010, so <c>xmin</c> is read the usual way).
    /// </summary>
    public static async Task LockHeldRowsAsync(MustaskDbContext context, string[] ids, CancellationToken cancellationToken)
    {
        if (ids.Length == 0)
        {
            return;
        }

        await context.Database.ExecuteSqlInterpolatedAsync(
            $"""
            SELECT id FROM directory_users
            WHERE id = ANY({ids}::text[])
            ORDER BY id COLLATE "C"
            FOR UPDATE
            """,
            cancellationToken);
    }
}
