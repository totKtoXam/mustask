using Mustask.Infrastructure.Conventions;
using Microsoft.EntityFrameworkCore;

namespace Mustask.Infrastructure.Tests.Conventions;

// В TPH-иерархии наследники отдаются
// GetEntityTypes() раньше базового типа -- добавление xmin на наследнике и
// затем на базовом типе конфликтовали ("... cannot be added ... because a
// property ... already exists on the 'Bus' type"). Этот тест остаётся
// красным на до-переписанной реализации (ApplyOptimisticConcurrency,
// ModelBuilderExtensions) по той же причине.
public sealed class TphOptimisticConcurrencyTests
{
    [Fact]
    public void OptimisticConcurrencyConvention_TphHierarchy_AddsExactlyOneSharedXminProperty()
    {
        using var context = TphTestDbContext.Create();

        var baseType = context.Model.FindEntityType(typeof(VehicleTestBase))!;
        var truck = context.Model.FindEntityType(typeof(TruckTestVehicle))!;
        var bus = context.Model.FindEntityType(typeof(BusTestVehicle))!;

        Assert.Equal(baseType.GetTableName(), truck.GetTableName());
        Assert.Equal(baseType.GetTableName(), bus.GetTableName());

        var baseXmin = baseType.FindProperty(OptimisticConcurrencyConvention.XminColumnName);

        Assert.NotNull(baseXmin);
        Assert.Same(baseXmin, truck.FindProperty(OptimisticConcurrencyConvention.XminColumnName));
        Assert.Same(baseXmin, bus.FindProperty(OptimisticConcurrencyConvention.XminColumnName));
    }

    [Fact]
    public void SnakeCaseNamingConvention_TphHierarchy_ConvertsColumnsToSnakeCase()
    {
        using var context = TphTestDbContext.Create();

        var baseType = context.Model.FindEntityType(typeof(VehicleTestBase))!;
        var truck = context.Model.FindEntityType(typeof(TruckTestVehicle))!;

        Assert.Equal("plate_number", baseType.GetEffectiveColumnName(nameof(VehicleTestBase.PlateNumber)));
        Assert.Equal("axle_count", truck.GetEffectiveColumnName(nameof(TruckTestVehicle.AxleCount)));
    }
}

internal abstract class VehicleTestBase
{
    public Guid Id { get; set; }

    public string PlateNumber { get; set; } = string.Empty;
}

internal sealed class TruckTestVehicle : VehicleTestBase
{
    public int AxleCount { get; set; }
}

internal sealed class BusTestVehicle : VehicleTestBase
{
    public int SeatCount { get; set; }
}

/// <summary>
/// Отдельный тип контекста -- см. пояснение в XminConcurrencyIntegrationTests
/// о кэше модели EF Core по типу DbContext.
/// </summary>
internal sealed class TphTestDbContext(DbContextOptions<TphTestDbContext> options) : DbContext(options)
{
    public DbSet<VehicleTestBase> Vehicles => Set<VehicleTestBase>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<VehicleTestBase>();
        modelBuilder.Entity<TruckTestVehicle>();
        modelBuilder.Entity<BusTestVehicle>();
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);

        // Та же регистрация, что в MustaskDbContext.ConfigureConventions.
        configurationBuilder.Conventions.Add(_ => new OptimisticConcurrencyConvention());
        configurationBuilder.Conventions.Add(_ => new SnakeCaseNamingConvention());
    }

    public static TphTestDbContext Create()
    {
        var options = new DbContextOptionsBuilder<TphTestDbContext>()
            .UseNpgsql("Host=localhost;Database=conventions_test;Username=postgres;Password=postgres")
            .Options;

        return new TphTestDbContext(options);
    }
}
