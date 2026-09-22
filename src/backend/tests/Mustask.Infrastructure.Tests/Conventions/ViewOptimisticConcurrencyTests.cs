using Mustask.Infrastructure.Conventions;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace Mustask.Infrastructure.Tests.Conventions;

// ApplyOptimisticConcurrency вешало
// shadow-свойство xmin на каждый entity type без исключений, включая keyless
// проекции на представления (HasNoKey().ToView(...)). PostgreSQL-представление
// не имеет собственной системной колонки xmin, поэтому запрос падал
// (42703: column ... xmin does not exist). Нужен реальный PostgreSQL, чтобы
// это воспроизвести -- этот тест остаётся красным на до-переписанной
// реализации (ApplyOptimisticConcurrency, пропуск только entity.IsKeyless).
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
public sealed class KeylessViewOptimisticConcurrencyIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:18-alpine")
        .WithDatabase("mustask")
        .Build();

    public Task InitializeAsync() => _postgres.StartAsync();

    public Task DisposeAsync() => _postgres.DisposeAsync().AsTask();

    private KeylessViewTestDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<KeylessViewTestDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options);

    private async Task CreateSchemaAsync(KeylessViewTestDbContext context)
    {
        // GenerateCreateScript() only emits DDL for the real (keyed) table --
        // EF never generates a CREATE TABLE for a keyless entity type mapped
        // to a view, so this is safe to run unconditionally.
        await context.Database.ExecuteSqlRawAsync(context.Database.GenerateCreateScript());

        var rowEntity = context.Model.FindEntityType(typeof(TaskTestRow))!;
        var rowTable = rowEntity.GetTableName();
        var displayNameColumn = rowEntity.GetEffectiveColumnName(nameof(TaskTestRow.DisplayName));

        // Plain (non-interpolated) string concatenation, not
        // ExecuteSqlRawAsync(FormattableString) / ExecuteSqlAsync: the values
        // here are identifiers (table/column names) read back from the
        // model's own metadata, not user input, and identifiers can't be
        // passed as SQL parameters anyway.
        var createView = "CREATE VIEW task_summary_test_view AS SELECT "
            + displayNameColumn + " AS display_name, 1 AS total_count FROM " + rowTable + ";";
        await context.Database.ExecuteSqlRawAsync(createView);
    }

    [Fact]
    public async Task ToListAsync_KeylessViewEntity_DoesNotSelectXminColumn()
    {
        await using var context = CreateContext();
        await CreateSchemaAsync(context);

        context.Rows.Add(new TaskTestRow { Id = Guid.NewGuid(), DisplayName = "task A" });
        await context.SaveChangesAsync();

        var exception = await Record.ExceptionAsync(() => context.Summaries.ToListAsync());

        Assert.Null(exception);
        var summary = Assert.Single(await context.Summaries.ToListAsync());
        Assert.Equal("task A", summary.DisplayName);
    }
}

internal sealed class TaskTestRow
{
    public Guid Id { get; set; }

    public string DisplayName { get; set; } = string.Empty;
}

internal sealed class TaskSummaryTestView
{
    public string DisplayName { get; set; } = string.Empty;

    public int TotalCount { get; set; }
}

/// <summary>
/// Отдельный тип контекста -- см. пояснение в XminConcurrencyIntegrationTests
/// о кэше модели EF Core по типу DbContext.
/// </summary>
internal sealed class KeylessViewTestDbContext(DbContextOptions<KeylessViewTestDbContext> options) : DbContext(options)
{
    public DbSet<TaskTestRow> Rows => Set<TaskTestRow>();

    public DbSet<TaskSummaryTestView> Summaries => Set<TaskSummaryTestView>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<TaskSummaryTestView>().HasNoKey().ToView("task_summary_test_view");
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);

        // Та же регистрация, что в MustaskDbContext.ConfigureConventions.
        configurationBuilder.Conventions.Add(_ => new OptimisticConcurrencyConvention());
        configurationBuilder.Conventions.Add(_ => new SnakeCaseNamingConvention());
    }
}

// Старый фильтр `entity.IsKeyless` не покрывал ToView С
// КЛЮЧОМ (HasKey + ToView) -- такой тип по-прежнему получал xmin и падал на
// живом Postgres с 42703 (колонки p.xmin не существует у представления).
// Проверка здесь -- на уровне метаданных модели (без Docker): именно там и
// была дыра в фильтре, GenerateCreateScript() дополнительно подтверждает,
// что реальная (keyed) таблица строит DDL без xmin вообще. Красный на
// до-переписанной реализации: `entity.IsKeyless` для HasKey+ToView равно
// false, поэтому xmin добавлялся -- проверено вручную тем же способом, что
// описано в OwnedTypeConventionTests.
public sealed class KeyedViewOptimisticConcurrencyTests
{
    [Fact]
    public void OptimisticConcurrencyConvention_KeyedToView_DoesNotAddXmin()
    {
        using var context = KeyedViewTestDbContext.Create();

        var view = context.Model.FindEntityType(typeof(KeyedSummaryTestView))!;
        var row = context.Model.FindEntityType(typeof(KeyedRowTestEntity))!;

        Assert.Null(view.GetTableName());
        // The model configures ToView("KeyedSummaryTestView")
        // (PascalCase) -- asserting the snake_case name here actually exercises
        // SnakeCaseNamingConvention's view-renaming, rather than asserting the
        // same already-snake_case string that was fed in.
        Assert.Equal("keyed_summary_test_view", view.GetViewName());
        Assert.Null(view.FindProperty(OptimisticConcurrencyConvention.XminColumnName));

        // Контрольная точка: реальная (keyed) таблица её всё ещё получает.
        Assert.NotNull(row.FindProperty(OptimisticConcurrencyConvention.XminColumnName));
    }

    [Fact]
    public void GenerateCreateScript_KeyedToView_OmitsXminFromUnderlyingTable()
    {
        using var context = KeyedViewTestDbContext.Create();

        var script = context.Database.GenerateCreateScript();

        Assert.DoesNotContain("xmin", script, StringComparison.OrdinalIgnoreCase);
    }
}

internal sealed class KeyedRowTestEntity
{
    public Guid Id { get; set; }

    public string DisplayName { get; set; } = string.Empty;
}

internal sealed class KeyedSummaryTestView
{
    public Guid Id { get; set; }

    public string DisplayName { get; set; } = string.Empty;
}

/// <summary>
/// Отдельный тип контекста -- см. пояснение в XminConcurrencyIntegrationTests
/// о кэше модели EF Core по типу DbContext.
/// </summary>
internal sealed class KeyedViewTestDbContext(DbContextOptions<KeyedViewTestDbContext> options) : DbContext(options)
{
    public DbSet<KeyedRowTestEntity> Rows => Set<KeyedRowTestEntity>();

    public DbSet<KeyedSummaryTestView> Summaries => Set<KeyedSummaryTestView>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<KeyedSummaryTestView>().HasKey(v => v.Id);
        modelBuilder.Entity<KeyedSummaryTestView>().ToView("KeyedSummaryTestView");
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);

        configurationBuilder.Conventions.Add(_ => new OptimisticConcurrencyConvention());
        configurationBuilder.Conventions.Add(_ => new SnakeCaseNamingConvention());
    }

    public static KeyedViewTestDbContext Create() =>
        new(new DbContextOptionsBuilder<KeyedViewTestDbContext>()
            .UseNpgsql("Host=localhost;Database=conventions_test;Username=postgres;Password=postgres")
            .Options);
}
