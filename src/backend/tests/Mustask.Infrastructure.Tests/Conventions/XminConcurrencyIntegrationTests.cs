using Mustask.Infrastructure.Conventions;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace Mustask.Infrastructure.Tests.Conventions;

/// <summary>
/// До этого `xmin` проверялся только на уровне
/// метаданных модели (свойство есть, IsConcurrencyToken = true). Само
/// поведение — что конкурентная правка действительно отбивается — не
/// проверялось ничем. Здесь конвенция прогоняется против настоящего
/// PostgreSQL: DDL из модели применяется к чистой БД, затем два контекста
/// правят одну строку.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
public sealed class XminConcurrencyIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:18-alpine")
        .WithDatabase("mustask")
        .Build();

    public Task InitializeAsync() => _postgres.StartAsync();

    public Task DisposeAsync() => _postgres.DisposeAsync().AsTask();

    private XminTestDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<XminTestDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
            .Options);

    private async Task CreateSchemaAsync()
    {
        await using var context = CreateContext();
        await context.Database.ExecuteSqlRawAsync(context.Database.GenerateCreateScript());
    }

    // "xmin" — системная колонка PostgreSQL, объявить её в DDL
    // нельзя. Раньше это вырезал наш собственный MustaskNpgsqlMigrationsSqlGenerator;
    // Проверка показала, что стоковый NpgsqlMigrationsSqlGenerator (10.0.3) делает это
    // сам (SystemColumnNames), поэтому кастомный генератор удалён как
    // избыточный, а этот тест теперь закрепляет поведение стокового генератора
    // на живом Postgres -- будущий апгрейд пакета, который перестанет
    // фильтровать системные колонки, сломает именно этот тест.
    [Fact]
    public async Task GenerateCreateScript_EntityWithXminConvention_OmitsXminColumnSoDdlIsApplicable()
    {
        await using var context = CreateContext();
        var script = context.Database.GenerateCreateScript();

        Assert.DoesNotContain("xmin", script, StringComparison.OrdinalIgnoreCase);

        // Главное доказательство — DDL действительно исполняется на живой БД.
        var exception = await Record.ExceptionAsync(() => context.Database.ExecuteSqlRawAsync(script));
        Assert.Null(exception);
    }

    [Fact]
    public async Task SaveChangesAsync_RowChangedByAnotherContext_ThrowsDbUpdateConcurrencyException()
    {
        await CreateSchemaAsync();

        var id = Guid.NewGuid();
        await using (var seed = CreateContext())
        {
            seed.Rows.Add(new XminTestRow { Id = id, DisplayName = "initial" });
            await seed.SaveChangesAsync();
        }

        await using var first = CreateContext();
        await using var second = CreateContext();

        var fromFirst = await first.Rows.SingleAsync(r => r.Id == id);
        var fromSecond = await second.Rows.SingleAsync(r => r.Id == id);

        fromFirst.DisplayName = "written by the first context";
        await first.SaveChangesAsync();

        fromSecond.DisplayName = "written by the second context";

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());
    }

    [Fact]
    public async Task SaveChangesAsync_NoCompetingWriter_UpdatesRowAndAdvancesXmin()
    {
        await CreateSchemaAsync();

        var id = Guid.NewGuid();
        await using var context = CreateContext();
        context.Rows.Add(new XminTestRow { Id = id, DisplayName = "initial" });
        await context.SaveChangesAsync();

        var afterInsert = context.Entry(context.Rows.Local.Single()).Property("xmin").CurrentValue;

        var row = await context.Rows.SingleAsync(r => r.Id == id);
        row.DisplayName = "updated";
        await context.SaveChangesAsync();

        var afterUpdate = context.Entry(row).Property("xmin").CurrentValue;

        Assert.NotNull(afterInsert);
        Assert.NotNull(afterUpdate);
        Assert.NotEqual(afterInsert, afterUpdate);
    }
}

internal sealed class XminTestRow
{
    public Guid Id { get; set; }

    public string DisplayName { get; set; } = string.Empty;
}

/// <summary>
/// Отдельный тип контекста (а не переиспользование
/// <see cref="ConventionsTestDbContext"/>): EF Core кэширует построенную
/// модель по типу контекста, поэтому у каждой тестовой формы модели должен
/// быть свой тип — иначе тест молча получает модель, построенную первым
/// использованием, и становится ложно-зелёным.
/// </summary>
internal sealed class XminTestDbContext(DbContextOptions<XminTestDbContext> options) : DbContext(options)
{
    public DbSet<XminTestRow> Rows => Set<XminTestRow>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);

        // Та же регистрация, что в MustaskDbContext.ConfigureConventions.
        configurationBuilder.Conventions.Add(_ => new OptimisticConcurrencyConvention());
        configurationBuilder.Conventions.Add(_ => new SnakeCaseNamingConvention());
    }
}
