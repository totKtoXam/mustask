using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Mustask.Infrastructure;

/// <summary>
/// ADR-0002: migrations are applied explicitly, never at startup. Every
/// hosted seeder (`DictionarySeeder`) checks this first, so
/// a fresh, unmigrated database fails with an actionable message instead of
/// a raw "relation does not exist".
/// </summary>
internal static class MigrationsGuard
{
    public static async Task EnsureAppliedAsync(DbContext context, CancellationToken cancellationToken)
    {
        try
        {
            var applied = await context.Database.GetAppliedMigrationsAsync(cancellationToken);
            if (!applied.Any())
            {
                throw NotAppliedException();
            }
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UndefinedTable)
        {
            throw NotAppliedException();
        }
    }

    private static InvalidOperationException NotAppliedException() => new(
        "Схема базы данных не создана. Примените миграции перед запуском: " +
        "'dotnet ef database update --project src/backend/Mustask.Infrastructure' (см. AGENTS.md).");
}
