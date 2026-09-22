using Mustask.Infrastructure.Conventions;
using Microsoft.EntityFrameworkCore;

namespace Mustask.Infrastructure.Tests.Conventions;

// Владелец с ОДНОЙ owned-навигацией собирался и раньше, но с ДВУМЯ
// owned-навигациями, делящими таблицу с владельцем (типичная форма: юридический и фактический
// адрес на одной сущности), ApplyOptimisticConcurrency вешало xmin на КАЖДУЮ
// owned-навигацию по отдельности -- построение модели падало:
// "Entity type '...HomeAddress#...' doesn't contain a property mapped to
// the store-generated concurrency token column 'work_address_xmin' which is
// used by another entity type sharing the table". С одной навигацией тот же
// код тихо создавал мусорную колонку "address_xmin" в DDL (EF её никогда не
// пишет). Вложенные owned-типы на разных навигациях -- прежний код
// брал только ОДИН уровень navigation-префикса, поэтому
// HomeAddress.Geo.LatitudeValue и WorkAddress.Geo.LatitudeValue молча
// мапились в одну и ту же колонку "geo_latitude_value".
//
// Оба новых сценария (два владения и вложенное владение) проверены как
// красные на до-переписанной реализации
// (ModelBuilderExtensions.ApplySnakeCaseNaming + ApplyOptimisticConcurrency,
// вызываемые из OnModelCreating): построение модели бросает ровно то
// исключение, что описано выше, для обоих сценариев (двух-навигационный
// случай ловит его раньше, чем успевает проявиться коллизия колонок
// вложенного владения) -- проверено вручную вне репозитория, подменой ModelBuilderExtensions.cs
// на версию до переписывания на IModelFinalizingConvention.
public sealed class OwnedTypeConventionTests
{
    [Fact]
    public void SnakeCaseNamingConvention_SingleOwnedNavigation_MirrorsSharedPrimaryKeyAndPrefixesOwnColumns()
    {
        using var context = SingleOwnedDbContext.Create();

        var owner = context.Model.FindEntityType(typeof(SingleOwnedOwner))!;
        var address = owner.FindNavigation(nameof(SingleOwnedOwner.Address))!.TargetEntityType;

        Assert.Equal("owners", owner.GetTableName());
        Assert.Equal(owner.GetTableName(), address.GetTableName());
        Assert.Equal(
            owner.GetEffectiveColumnName(nameof(SingleOwnedOwner.Id)),
            address.GetEffectiveColumnName("SingleOwnedOwnerId"));

        Assert.Equal("address_city_name", address.GetEffectiveColumnName(nameof(SingleOwnedAddress.CityName)));
        Assert.Equal("address_street_name", address.GetEffectiveColumnName(nameof(SingleOwnedAddress.StreetName)));
    }

    // Владелец с двумя owned-навигациями, делящими одну таблицу.
    [Fact]
    public void GenerateCreateScript_TwoOwnedNavigationsSharingTable_HasNoXminColumnsAndExactlyOneXminOnOwner()
    {
        using var context = TwoOwnedDbContext.Create();

        var script = context.Database.GenerateCreateScript();

        // Единственный настоящий xmin (на владельце) -- системная колонка
        // PostgreSQL, стоковый Npgsql-генератор вырезает её из DDL целиком
        // -- поэтому в правильном DDL "xmin" не должно
        // встречаться вообще, ни как отдельная колонка, ни как суффикс
        // "*_xmin" в имени.
        Assert.DoesNotContain("xmin", script, StringComparison.OrdinalIgnoreCase);

        var owner = context.Model.FindEntityType(typeof(TwoOwnedOwner))!;
        var homeAddress = owner.FindNavigation(nameof(TwoOwnedOwner.HomeAddress))!.TargetEntityType;
        var workAddress = owner.FindNavigation(nameof(TwoOwnedOwner.WorkAddress))!.TargetEntityType;

        Assert.NotNull(owner.FindProperty(OptimisticConcurrencyConvention.XminColumnName));
        Assert.Null(homeAddress.FindProperty(OptimisticConcurrencyConvention.XminColumnName));
        Assert.Null(workAddress.FindProperty(OptimisticConcurrencyConvention.XminColumnName));
    }

    [Fact]
    public void SnakeCaseNamingConvention_TwoOwnedNavigationsSharingTable_PrefixesColumnsPerNavigation()
    {
        using var context = TwoOwnedDbContext.Create();

        var owner = context.Model.FindEntityType(typeof(TwoOwnedOwner))!;
        var homeAddress = owner.FindNavigation(nameof(TwoOwnedOwner.HomeAddress))!.TargetEntityType;
        var workAddress = owner.FindNavigation(nameof(TwoOwnedOwner.WorkAddress))!.TargetEntityType;

        Assert.Equal(
            "home_address_city_name",
            homeAddress.GetEffectiveColumnName(nameof(TwoOwnedAddress.CityName)));
        Assert.Equal(
            "work_address_city_name",
            workAddress.GetEffectiveColumnName(nameof(TwoOwnedAddress.CityName)));
    }

    // Вложенный owned-тип (Address.Geo.LatitudeValue) на двух разных
    // навигациях верхнего уровня -- накопленный префикс должен различать
    // колонки, а не сталкивать их в одну.
    [Fact]
    public void SnakeCaseNamingConvention_NestedOwnedTypeOnTwoNavigations_AccumulatesDistinctPrefixedColumns()
    {
        using var context = NestedOwnedDbContext.Create();

        var owner = context.Model.FindEntityType(typeof(NestedOwnedOwner))!;
        var homeAddress = owner.FindNavigation(nameof(NestedOwnedOwner.HomeAddress))!.TargetEntityType;
        var workAddress = owner.FindNavigation(nameof(NestedOwnedOwner.WorkAddress))!.TargetEntityType;
        var homeGeo = homeAddress.FindNavigation(nameof(NestedOwnedAddress.Geo))!.TargetEntityType;
        var workGeo = workAddress.FindNavigation(nameof(NestedOwnedAddress.Geo))!.TargetEntityType;

        var homeLatitudeColumn = homeGeo.GetEffectiveColumnName(nameof(Geo.LatitudeValue));
        var workLatitudeColumn = workGeo.GetEffectiveColumnName(nameof(Geo.LatitudeValue));

        Assert.Equal("home_address_geo_latitude_value", homeLatitudeColumn);
        Assert.Equal("work_address_geo_latitude_value", workLatitudeColumn);
        Assert.NotEqual(homeLatitudeColumn, workLatitudeColumn);
    }

    [Fact]
    public void GenerateCreateScript_NestedOwnedTypeOnTwoNavigations_BuildsModelWithoutError()
    {
        using var context = NestedOwnedDbContext.Create();

        var script = context.Database.GenerateCreateScript();

        Assert.Contains("home_address_geo_latitude_value", script);
        Assert.Contains("work_address_geo_latitude_value", script);
    }

    // Owner's key column has an explicit HasColumnName
    // (a form the legacy schema uses routinely, e.g. mapping onto a
    // pre-existing "OwnerIdentifier" column) plus an OwnsOne with table
    // splitting. The now-removed GetTableSplitMirroredKeyName mirrored the
    // principal *property*'s name ("Id") instead of its already-resolved
    // *column* name ("OwnerIdentifier"), so the owned type's shared-key
    // shadow property and the owner's own key property snake_cased to two
    // different strings ("id" vs "owner_identifier") and model validation
    // failed with "The keys ... are both mapped to ... but with different
    // columns". Plain store-scoped GetColumnName (used once the helper is
    // gone) already returns the mirrored, already-renamed column for both
    // sides -- see the class remarks on SnakeCaseNamingConvention.
    [Fact]
    public void SnakeCaseNamingConvention_ExplicitColumnNameOnOwnerKeyPlusOwnedTableSplitting_BuildsModelWithMatchingColumns()
    {
        using var context = ExplicitKeyOwnedDbContext.Create();

        var owner = context.Model.FindEntityType(typeof(ExplicitKeyOwner))!;
        var address = owner.FindNavigation(nameof(ExplicitKeyOwner.Address))!.TargetEntityType;

        var ownerKeyColumn = owner.GetEffectiveColumnName(nameof(ExplicitKeyOwner.Id));
        var addressKeyColumn = address.GetEffectiveColumnName("ExplicitKeyOwnerId");

        Assert.Equal("owner_identifier", ownerKeyColumn);
        Assert.Equal(ownerKeyColumn, addressKeyColumn);

        var script = context.Database.GenerateCreateScript();
        Assert.Contains("owner_identifier", script);
    }
}

internal sealed class SingleOwnedAddress
{
    public string CityName { get; set; } = string.Empty;

    public string StreetName { get; set; } = string.Empty;
}

internal sealed class SingleOwnedOwner
{
    public Guid Id { get; set; }

    public string DisplayName { get; set; } = string.Empty;

    public SingleOwnedAddress Address { get; set; } = null!;
}

/// <summary>
/// Отдельный тип контекста для каждой формы модели -- см. пояснение в
/// XminConcurrencyIntegrationTests о кэше модели EF Core по типу DbContext.
/// </summary>
internal sealed class SingleOwnedDbContext(DbContextOptions<SingleOwnedDbContext> options) : DbContext(options)
{
    public DbSet<SingleOwnedOwner> Owners => Set<SingleOwnedOwner>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<SingleOwnedOwner>().OwnsOne(o => o.Address);
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);

        configurationBuilder.Conventions.Add(_ => new OptimisticConcurrencyConvention());
        configurationBuilder.Conventions.Add(_ => new SnakeCaseNamingConvention());
    }

    public static SingleOwnedDbContext Create() =>
        new(new DbContextOptionsBuilder<SingleOwnedDbContext>()
            .UseNpgsql("Host=localhost;Database=conventions_test;Username=postgres;Password=postgres")
            .Options);
}

internal sealed class TwoOwnedAddress
{
    public string CityName { get; set; } = string.Empty;

    public string StreetName { get; set; } = string.Empty;
}

// Юридический и фактический адрес на одной сущности -- рядовая форма для
// организации, а не искусственный крайний случай.
internal sealed class TwoOwnedOwner
{
    public Guid Id { get; set; }

    public TwoOwnedAddress HomeAddress { get; set; } = null!;

    public TwoOwnedAddress WorkAddress { get; set; } = null!;
}

/// <summary>
/// Отдельный тип контекста -- см. пояснение в XminConcurrencyIntegrationTests
/// о кэше модели EF Core по типу DbContext.
/// </summary>
internal sealed class TwoOwnedDbContext(DbContextOptions<TwoOwnedDbContext> options) : DbContext(options)
{
    public DbSet<TwoOwnedOwner> Owners => Set<TwoOwnedOwner>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<TwoOwnedOwner>(builder =>
        {
            builder.OwnsOne(o => o.HomeAddress);
            builder.OwnsOne(o => o.WorkAddress);
        });
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);

        configurationBuilder.Conventions.Add(_ => new OptimisticConcurrencyConvention());
        configurationBuilder.Conventions.Add(_ => new SnakeCaseNamingConvention());
    }

    public static TwoOwnedDbContext Create() =>
        new(new DbContextOptionsBuilder<TwoOwnedDbContext>()
            .UseNpgsql("Host=localhost;Database=conventions_test;Username=postgres;Password=postgres")
            .Options);
}

internal sealed class Geo
{
    public double LatitudeValue { get; set; }

    public double LongitudeValue { get; set; }
}

internal sealed class NestedOwnedAddress
{
    public string CityName { get; set; } = string.Empty;

    public Geo Geo { get; set; } = null!;
}

internal sealed class NestedOwnedOwner
{
    public Guid Id { get; set; }

    public NestedOwnedAddress HomeAddress { get; set; } = null!;

    public NestedOwnedAddress WorkAddress { get; set; } = null!;
}

/// <summary>
/// Отдельный тип контекста -- см. пояснение в XminConcurrencyIntegrationTests
/// о кэше модели EF Core по типу DbContext.
/// </summary>
internal sealed class NestedOwnedDbContext(DbContextOptions<NestedOwnedDbContext> options) : DbContext(options)
{
    public DbSet<NestedOwnedOwner> Owners => Set<NestedOwnedOwner>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<NestedOwnedOwner>(builder =>
        {
            builder.OwnsOne(o => o.HomeAddress, a => a.OwnsOne(x => x.Geo));
            builder.OwnsOne(o => o.WorkAddress, a => a.OwnsOne(x => x.Geo));
        });
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);

        configurationBuilder.Conventions.Add(_ => new OptimisticConcurrencyConvention());
        configurationBuilder.Conventions.Add(_ => new SnakeCaseNamingConvention());
    }

    public static NestedOwnedDbContext Create() =>
        new(new DbContextOptionsBuilder<NestedOwnedDbContext>()
            .UseNpgsql("Host=localhost;Database=conventions_test;Username=postgres;Password=postgres")
            .Options);
}

internal sealed class ExplicitKeyAddress
{
    public string CityName { get; set; } = string.Empty;
}

internal sealed class ExplicitKeyOwner
{
    public Guid Id { get; set; }

    public ExplicitKeyAddress Address { get; set; } = null!;
}

/// <summary>
/// Отдельный тип контекста -- см. пояснение в XminConcurrencyIntegrationTests
/// о кэше модели EF Core по типу DbContext.
/// </summary>
internal sealed class ExplicitKeyOwnedDbContext(DbContextOptions<ExplicitKeyOwnedDbContext> options) : DbContext(options)
{
    public DbSet<ExplicitKeyOwner> Owners => Set<ExplicitKeyOwner>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ExplicitKeyOwner>(builder =>
        {
            builder.Property(o => o.Id).HasColumnName("OwnerIdentifier");
            builder.OwnsOne(o => o.Address);
        });
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);

        configurationBuilder.Conventions.Add(_ => new OptimisticConcurrencyConvention());
        configurationBuilder.Conventions.Add(_ => new SnakeCaseNamingConvention());
    }

    public static ExplicitKeyOwnedDbContext Create() =>
        new(new DbContextOptionsBuilder<ExplicitKeyOwnedDbContext>()
            .UseNpgsql("Host=localhost;Database=conventions_test;Username=postgres;Password=postgres")
            .Options);
}
