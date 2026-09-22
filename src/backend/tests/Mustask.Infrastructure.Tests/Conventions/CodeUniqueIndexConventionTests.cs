using Mustask.Infrastructure.Conventions;
using Microsoft.EntityFrameworkCore;

namespace Mustask.Infrastructure.Tests.Conventions;

/// <summary>
/// docs/conventions/backend.md, "База данных": a business `Code` is unique by
/// convention, the same way `xmin` is added automatically
/// (<see cref="OptimisticConcurrencyConvention"/>). Building the model does
/// not require a reachable database -- see <see cref="ConventionsTestDbContext"/>'s
/// remarks.
/// </summary>
public sealed class CodeUniqueIndexConventionTests
{
    [Fact]
    public void OrdinaryCodeProperty_GetsAUniqueIndex()
    {
        using var context = CodeConventionTestDbContext.Create();
        var entity = context.Model.FindEntityType(typeof(CodeAsOrdinaryPropertyEntity))!;

        var index = Assert.Single(entity.GetIndexes());
        Assert.True(index.IsUnique);
        Assert.Equal([nameof(CodeAsOrdinaryPropertyEntity.Code)], index.Properties.Select(p => p.Name));
    }

    [Fact]
    public void CodeAsSingleColumnPrimaryKey_GetsNoExtraIndex()
    {
        using var context = CodeConventionTestDbContext.Create();
        var entity = context.Model.FindEntityType(typeof(CodeAsPrimaryKeyEntity))!;

        Assert.Empty(entity.GetIndexes());
    }

    [Fact]
    public void CodeAlreadyUniquelyIndexedExplicitly_GetsNoDuplicateIndex()
    {
        using var context = CodeConventionTestDbContext.Create();
        var entity = context.Model.FindEntityType(typeof(CodeAlreadyIndexedEntity))!;

        var index = Assert.Single(entity.GetIndexes());
        Assert.True(index.IsUnique);
    }

    [Fact]
    public void CodeOnKeylessViewEntity_IsSkipped()
    {
        using var context = CodeConventionTestDbContext.Create();
        var entity = context.Model.FindEntityType(typeof(CodeOnViewEntity))!;

        Assert.Empty(entity.GetIndexes());
    }

    /// <summary>
    /// The pattern actually used in `MustaskDbContext`: a `Code` property already
    /// covered by an alternate key (rather than an index) is skipped too --
    /// same reasoning as the explicit-index case above, different EF Core
    /// mechanism.
    /// </summary>
    [Fact]
    public void CodeCoveredByAnAlternateKey_GetsNoDuplicateIndex()
    {
        using var context = CodeConventionTestDbContext.Create();
        var entity = context.Model.FindEntityType(typeof(CodeAsAlternateKeyEntity))!;

        Assert.Empty(entity.GetIndexes());
    }
}

internal sealed class CodeAsOrdinaryPropertyEntity
{
    public Guid Id { get; set; }

    public string Code { get; set; } = string.Empty;
}

internal sealed class CodeAsPrimaryKeyEntity
{
    public string Code { get; set; } = string.Empty;
}

internal sealed class CodeAlreadyIndexedEntity
{
    public Guid Id { get; set; }

    public string Code { get; set; } = string.Empty;
}

internal sealed class CodeAsAlternateKeyEntity
{
    public Guid Id { get; set; }

    public string Code { get; set; } = string.Empty;
}

internal sealed class CodeOnViewRowEntity
{
    public Guid Id { get; set; }

    public string Code { get; set; } = string.Empty;
}

internal sealed class CodeOnViewEntity
{
    public string Code { get; set; } = string.Empty;
}

internal sealed class CodeConventionTestDbContext(DbContextOptions<CodeConventionTestDbContext> options) : DbContext(options)
{
    public DbSet<CodeAsOrdinaryPropertyEntity> OrdinaryCodeEntities => Set<CodeAsOrdinaryPropertyEntity>();

    public DbSet<CodeAsPrimaryKeyEntity> PrimaryKeyCodeEntities => Set<CodeAsPrimaryKeyEntity>();

    public DbSet<CodeAlreadyIndexedEntity> AlreadyIndexedEntities => Set<CodeAlreadyIndexedEntity>();

    public DbSet<CodeAsAlternateKeyEntity> AlternateKeyEntities => Set<CodeAsAlternateKeyEntity>();

    public DbSet<CodeOnViewRowEntity> ViewRows => Set<CodeOnViewRowEntity>();

    public DbSet<CodeOnViewEntity> ViewEntities => Set<CodeOnViewEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<CodeAsPrimaryKeyEntity>().HasKey(e => e.Code);

        modelBuilder.Entity<CodeAlreadyIndexedEntity>().HasIndex(e => e.Code).IsUnique();

        modelBuilder.Entity<CodeAsAlternateKeyEntity>().HasAlternateKey(e => e.Code);

        modelBuilder.Entity<CodeOnViewEntity>().HasNoKey().ToView("code_on_view_entity_view");
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);

        // Same wiring and order as MustaskDbContext.ConfigureConventions.
        configurationBuilder.Conventions.Add(_ => new OptimisticConcurrencyConvention());
        configurationBuilder.Conventions.Add(_ => new CodeUniqueIndexConvention());
        configurationBuilder.Conventions.Add(_ => new SnakeCaseNamingConvention());
    }

    public static CodeConventionTestDbContext Create()
    {
        var options = new DbContextOptionsBuilder<CodeConventionTestDbContext>()
            .UseNpgsql("Host=localhost;Database=code_convention_test;Username=postgres;Password=postgres")
            .Options;

        return new CodeConventionTestDbContext(options);
    }
}
