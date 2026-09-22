using Mustask.Infrastructure.Conventions;
using Microsoft.EntityFrameworkCore;

namespace Mustask.Infrastructure.Tests.Conventions;

/// <summary>
/// Test-only entities and a throwaway <see cref="DbContext"/> used to exercise
/// the reusable model conventions in <c>Mustask.Infrastructure.Conventions</c> in
/// isolation, without adding a fictitious entity to Mustask.Domain (there are no
/// real entities yet). Building the EF model does not require a reachable
/// database — only migrations/queries do — so these tests don't need Docker.
/// </summary>
internal sealed class ParentTestEntity
{
    public Guid Id { get; set; }

    public string DisplayName { get; set; } = string.Empty;

    public List<ChildTestEntity> Children { get; set; } = [];
}

internal sealed class ChildTestEntity
{
    public Guid Id { get; set; }

    public Guid ParentTestEntityId { get; set; }

    public ParentTestEntity ParentTestEntity { get; set; } = null!;
}

internal sealed class ConventionsTestDbContext(DbContextOptions<ConventionsTestDbContext> options)
    : DbContext(options)
{
    public DbSet<ParentTestEntity> Parents => Set<ParentTestEntity>();

    public DbSet<ChildTestEntity> Children => Set<ChildTestEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ParentTestEntity>()
            .HasIndex(p => p.DisplayName);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);

        // Same wiring as MustaskDbContext.ConfigureConventions.
        configurationBuilder.Conventions.Add(_ => new OptimisticConcurrencyConvention());
        configurationBuilder.Conventions.Add(_ => new SnakeCaseNamingConvention());
    }

    public static ConventionsTestDbContext Create()
    {
        var options = new DbContextOptionsBuilder<ConventionsTestDbContext>()
            .UseNpgsql("Host=localhost;Database=conventions_test;Username=postgres;Password=postgres")
            .Options;

        return new ConventionsTestDbContext(options);
    }
}
