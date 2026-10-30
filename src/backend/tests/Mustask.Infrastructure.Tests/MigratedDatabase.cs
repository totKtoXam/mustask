using Microsoft.EntityFrameworkCore;
using Mustask.Testing;

namespace Mustask.Infrastructure.Tests;

/// <summary>
/// The real migrations applied once per test run to a template on the shared
/// server; a fixture that needs a migrated database gets a copy of it
/// (tech-047). A copy is the same database a migration of its own would build,
/// in a fraction of the time.
/// </summary>
internal static class MigratedDatabase
{
    public static SharedPostgres.Template Template { get; } = new("migrated_template", async connectionString =>
    {
        await using var context = new MustaskDbContext(new DbContextOptionsBuilder<MustaskDbContext>().UseNpgsql(connectionString).Options);
        await context.Database.MigrateAsync();
    });
}
