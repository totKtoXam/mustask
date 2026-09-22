using Mustask.Infrastructure.Conventions;
using Microsoft.EntityFrameworkCore;

namespace Mustask.Infrastructure.Tests.Conventions;

// У неявной join-сущности implicit many-to-many (skip navigation)
// не было ни одного закоммиченного регрессионного теста -- покрытие
// существовало только в разовом scratchpad-пробнике вне репозитория.
// Здесь -- то же самое (join-таблица, её колонки/PK/FK/индекс приводятся к
// snake_case), закреплено как постоянный unit-тест. xmin на join-сущности --
// принятое поведение (лишний токен, не дефект), а не то, что нужно
// было бы исправлять; тест фиксирует его явно, чтобы будущее изменение было
// осознанным, а не случайным.
public sealed class ManyToManyNamingConventionTests
{
    [Fact]
    public void SnakeCaseNamingConvention_ImplicitManyToManyJoinEntity_ConvertsNamesToSnakeCase()
    {
        using var context = ManyToManyTestDbContext.Create();

        var article = context.Model.FindEntityType(typeof(Article))!;
        var join = article.FindSkipNavigation(nameof(Article.Labels))!.JoinEntityType;

        Assert.Equal("article_label", join.GetTableName());

        var foreignKeys = join.GetForeignKeys().ToList();
        Assert.Equal(2, foreignKeys.Count);

        var toArticles = foreignKeys.Single(fk => fk.PrincipalEntityType == article);
        var articleFkPropertyName = Assert.Single(toArticles.Properties).Name;
        Assert.Equal("articles_id", join.GetEffectiveColumnName(articleFkPropertyName));

        Assert.Equal(
            "pk_article_label",
            join.FindPrimaryKey()!.GetName());

        Assert.Contains(
            join.GetForeignKeys(),
            fk => fk.GetConstraintName() == "fk_article_label_articles_articles_id");

        var index = Assert.Single(join.GetIndexes());
        Assert.Equal("ix_article_label_labels_id", index.GetDatabaseName());
    }

    [Fact]
    public void OptimisticConcurrencyConvention_ImplicitManyToManyJoinEntity_AddsXmin()
    {
        using var context = ManyToManyTestDbContext.Create();

        var article = context.Model.FindEntityType(typeof(Article))!;
        var join = article.FindSkipNavigation(nameof(Article.Labels))!.JoinEntityType;

        // Принятое поведение: join-таблица реальная и получает
        // свой xmin, как любая другая таблица -- лишний токен, но не дефект.
        Assert.NotNull(join.FindProperty(OptimisticConcurrencyConvention.XminColumnName));
    }
}

internal sealed class Article
{
    public Guid Id { get; set; }

    public List<Label> Labels { get; set; } = [];
}

internal sealed class Label
{
    public Guid Id { get; set; }

    public List<Article> Articles { get; set; } = [];
}

/// <summary>
/// Отдельный тип контекста -- см. пояснение в XminConcurrencyIntegrationTests
/// о кэше модели EF Core по типу DbContext.
/// </summary>
internal sealed class ManyToManyTestDbContext(DbContextOptions<ManyToManyTestDbContext> options) : DbContext(options)
{
    public DbSet<Article> Articles => Set<Article>();

    public DbSet<Label> Labels => Set<Label>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);

        configurationBuilder.Conventions.Add(_ => new OptimisticConcurrencyConvention());
        configurationBuilder.Conventions.Add(_ => new SnakeCaseNamingConvention());
    }

    public static ManyToManyTestDbContext Create() =>
        new(new DbContextOptionsBuilder<ManyToManyTestDbContext>()
            .UseNpgsql("Host=localhost;Database=conventions_test;Username=postgres;Password=postgres")
            .Options);
}
