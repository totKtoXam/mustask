using Mustask.Application.Abstractions;
using Mustask.Domain.Dictionaries;
using Microsoft.EntityFrameworkCore;

namespace Mustask.Infrastructure.Dictionaries.Seeding;

/// <summary>
/// ADR-0021 §5: idempotent initial population under an advisory lock --
/// inserts what is missing, never updates what exists, so an
/// administrator's edit survives a restart. A new dictionary adds its own
/// `Seed…Async` step here, inside the same transaction and lock.
/// </summary>
public sealed class DictionarySeeder(MustaskDbContext context, IClock clock)
{
    // Advisory lock keys of unrelated operations must stay distinct -- they
    // must not block each other without reason.
    private const long AdvisoryLockKey = 727_003_001;
    private const string SeedAuthor = "seed";

    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        await MigrationsGuard.EnsureAppliedAsync(context, cancellationToken);

        var strategy = context.Database.CreateExecutionStrategy();

        await strategy.ExecuteAsync(async () =>
        {
            context.ChangeTracker.Clear();

            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

            await context.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT pg_advisory_xact_lock({AdvisoryLockKey})", cancellationToken);

            await SeedLanguagesAsync(cancellationToken);

            await transaction.CommitAsync(cancellationToken);
        });
    }

    private async Task SeedLanguagesAsync(CancellationToken cancellationToken)
    {
        var entries = DictionarySeedFiles.LoadLanguages(typeof(DictionarySeeder).Assembly);
        var existingCodes = await context.Languages.Select(l => l.Code).ToListAsync(cancellationToken);
        var now = clock.UtcNow;

        foreach (var entry in entries)
        {
            if (existingCodes.Contains(entry.Code))
            {
                continue;
            }

            context.Languages.Add(Language.Seed(
                Guid.CreateVersion7(), entry.Code, entry.Endonym, entry.IsDefault, entry.IsActive, entry.SortOrder, now, SeedAuthor));
        }

        await context.SaveChangesAsync(cancellationToken);
    }
}
