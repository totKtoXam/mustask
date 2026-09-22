using Mustask.Domain.Dictionaries;
using Mustask.Infrastructure.Conventions;
using Microsoft.EntityFrameworkCore;

namespace Mustask.Infrastructure;

/// <summary>
/// EF Core context for the MUSTASK database. The naming and optimistic-concurrency
/// conventions below are wired here, as model-finalizing conventions, so they
/// apply automatically to every future entity without per-entity configuration
/// (docs/conventions/backend.md, "База данных"). See
/// <see cref="Conventions.SnakeCaseNamingConvention"/>,
/// <see cref="Conventions.OptimisticConcurrencyConvention"/> and
/// <see cref="Conventions.CodeUniqueIndexConvention"/> for why they
/// are registered this way rather than called from
/// <c>OnModelCreating</c>.
/// </summary>
public sealed class MustaskDbContext(DbContextOptions<MustaskDbContext> options) : DbContext(options)
{
    public DbSet<Language> Languages => Set<Language>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);

        configurationBuilder.Conventions.Add(_ => new OptimisticConcurrencyConvention());
        // CodeUniqueIndexConvention runs BEFORE SnakeCaseNamingConvention:
        // model-finalizing conventions execute in registration order, and the
        // default-named index it adds (e.g. "IX_languages_code") must still
        // exist when SnakeCaseNamingConvention later walks every index to
        // rename it -- registering it after would leave that one index with
        // an un-renamed, non-snake_case name.
        configurationBuilder.Conventions.Add(_ => new CodeUniqueIndexConvention());
        configurationBuilder.Conventions.Add(_ => new SnakeCaseNamingConvention());
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MustaskDbContext).Assembly);
    }
}
