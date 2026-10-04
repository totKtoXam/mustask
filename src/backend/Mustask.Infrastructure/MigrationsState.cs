using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Mustask.Infrastructure;

/// <summary>
/// What the database lacks of the migrations this build ships. It only reads:
/// migrations are applied explicitly, never at startup (ADR-0002). The one reader
/// today is the Development stand's demonstration seed (mod-001, BR-M2-1), which
/// skips with a warning when the schema is behind this build instead of failing
/// the start. <see cref="MigrationsGuard"/> answers a different question (is there
/// any schema at all) and stays as it is.
/// </summary>
public static class MigrationsState
{
    /// <summary>
    /// The number of migrations of this build that the database has not applied.
    /// A database with no history table at all has every migration pending, and so
    /// does a database that does not exist yet (PostgreSQL 3D000), because both mean
    /// "nothing is applied", not "the question cannot be answered".
    /// </summary>
    public static async Task<int> CountPendingAsync(DbContext context, CancellationToken cancellationToken)
    {
        try
        {
            return (await context.Database.GetPendingMigrationsAsync(cancellationToken)).Count();
        }
        catch (PostgresException ex) when (ex.SqlState is PostgresErrorCodes.InvalidCatalogName or PostgresErrorCodes.UndefinedTable)
        {
            return context.Database.GetMigrations().Count();
        }
    }
}
