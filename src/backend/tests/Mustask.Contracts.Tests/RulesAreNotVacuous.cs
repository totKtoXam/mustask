namespace Mustask.Contracts.Tests;

/// <summary>
/// Самопроверка набора правил (зеркало
/// <c>Mustask.Architecture.Tests.RulesAreNotVacuous</c>): зелёный тест бывает по
/// двум причинам — нарушений нет, или проверка ничего не читает (переехал
/// каталог, сломался парсер сборок). Сегодня в <c>src/</c> шесть
/// платформенных кодов и ноль бизнес-слайсов (<c>ApplicationCodes</c> от
/// <c>[ErrorCodes]</c>-классов пуст) — эти тесты отличают «пусто и всё
/// хорошо» от «сканер сломан».
/// </summary>
public sealed class RulesAreNotVacuous
{
    [Fact]
    public void Repository_root_is_found()
    {
        Assert.True(
            File.Exists(Path.Combine(RepositoryLayout.Root.FullName, "Mustask.sln")),
            $"Корень репозитория определён как {RepositoryLayout.Root.FullName}, но Mustask.sln там нет.");
    }

    [Fact]
    public void Catalog_files_are_discovered()
    {
        var files = RepositoryLayout.CatalogFiles();

        Assert.True(files.Count > 0, "Не найдено ни одного файла каталога в contracts/errors/.");
        Assert.DoesNotContain(files, f => f.Name == "schema.json");
    }

    [Fact]
    public void Catalog_has_entries()
    {
        Assert.True(CatalogFixture.Catalog.Entries.Count > 0, "Каталог загружен, но пуст.");
    }

    /// <summary>Известная существующая запись — если парсер каталога однажды начнёт возвращать пустой словарь, тесты полноты станут зелёными молча.</summary>
    [Fact]
    public void Known_platform_code_is_visible()
    {
        Assert.True(CatalogFixture.Catalog.TryGet("http.not_found", out _), "Заведомо существующий http.not_found не найден в загруженном каталоге.");
    }

    /// <summary>
    /// Доказывает, что <see cref="AppErrorCodeScanner"/> реально находит
    /// <c>[ErrorCodes]</c>-классы и их константы — на синтетической
    /// сборке (эта же), а не на пустом сегодня множестве слайсов
    /// <c>src/</c>.
    /// </summary>
    [Fact]
    public void Scanner_finds_known_marked_constant()
    {
        var found = AppErrorCodeScanner.Scan([typeof(ScannerSelfTestFixture).Assembly])
            .Select(c => c.Code)
            .ToList();

        Assert.Contains(ScannerSelfTestFixture.SampleCode, found);
        Assert.DoesNotContain("contracts_tests.not_a_const", found);
    }

    [Theory]
    [InlineData("Mustask.Shared")]
    [InlineData("Mustask.Domain")]
    [InlineData("Mustask.Application")]
    public void Source_assembly_is_loadable(string simpleName)
    {
        var assembly = CatalogFixture.SourceAssemblies.SingleOrDefault(a => a.GetName().Name == simpleName);

        Assert.True(assembly is not null, $"Сборка {simpleName} не найдена среди CatalogFixture.SourceAssemblies.");
    }
}
