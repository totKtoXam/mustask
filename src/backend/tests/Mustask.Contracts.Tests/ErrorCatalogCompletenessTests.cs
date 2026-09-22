using Mustask.Api.ErrorHandling.Catalog;
using Mustask.Shared.Errors;

namespace Mustask.Contracts.Tests;

/// <summary>
/// Блокирующие проверки полноты и согласованности каталога. Без Docker, без
/// HTTP-хоста, без БД — читает файлы репозитория и сборки <c>src/</c>
/// напрямую. То, что каждая проверка красная на подготовленном нарушении,
/// проверяется вручную, а не автотестом — заменить содержимое каталога на невалидное в общем фикстуре
/// уронило бы весь тестовый прогон, а не один тест с понятным сообщением.
/// </summary>
public sealed class ErrorCatalogCompletenessTests
{
    /// <summary>
    /// Каждый код приложения (платформенный или объявленный
    /// константой в классе с <see cref="ErrorCodesAttribute"/>) присутствует
    /// в каталоге. Непустота ru/kk для каждой такой записи гарантирована
    /// загрузчиком (<c>ErrorCatalogLoader</c> валит загрузку на пустом
    /// тексте — покрыто в <c>Mustask.Api.IntegrationTests.ErrorCatalogLoaderTests</c>),
    /// поэтому здесь достаточно членства в каталоге.
    /// </summary>
    [Fact]
    public void EveryApplicationCode_ExistsInCatalog()
    {
        var missing = CatalogFixture.ApplicationCodes
            .Where(appCode => !CatalogFixture.Catalog.TryGet(appCode.Code, out _))
            .ToList();

        Assert.True(
            missing.Count == 0,
            "Коды приложения без записи в каталоге: " +
            string.Join(", ", missing.Select(m => $"{m.Code} ({m.DeclaringType})")));
    }

    /// <summary>kk, побуквенно равный ru, — непереведённый текст, а не запись «пока не перевели».</summary>
    [Fact]
    public void NoEntry_HasKkIdenticalToRu()
    {
        var violations = CatalogFixture.Catalog.Entries
            .Where(entry => string.Equals(entry.Ru, entry.Kk, StringComparison.Ordinal))
            .Select(entry => entry.Code)
            .ToList();

        Assert.True(
            violations.Count == 0,
            "Записи, где kk побуквенно совпадает с ru (текст не переведён): " + string.Join(", ", violations));
    }

    /// <summary>
    /// Плейсхолдеры ru = плейсхолдеры kk = ключи
    /// params. Расхождение — тихий отказ подстановки на одном из языков
    /// (чаще на казахской ветке, если правили русскую).
    /// </summary>
    [Fact]
    public void PlaceholdersInRuAndKkMatchDeclaredParams()
    {
        var violations = new List<string>();

        foreach (var entry in CatalogFixture.Catalog.Entries)
        {
            var declared = entry.Params.Keys.ToHashSet(StringComparer.Ordinal);
            var ru = PlaceholderExtractor.Extract(entry.Ru);
            var kk = PlaceholderExtractor.Extract(entry.Kk);

            if (!declared.SetEquals(ru) || !declared.SetEquals(kk))
            {
                violations.Add(
                    $"{entry.Code}: params=[{string.Join(",", declared.OrderBy(n => n, StringComparer.Ordinal))}] " +
                    $"ru=[{string.Join(",", ru.OrderBy(n => n, StringComparer.Ordinal))}] " +
                    $"kk=[{string.Join(",", kk.OrderBy(n => n, StringComparer.Ordinal))}]");
            }
        }

        Assert.True(violations.Count == 0, "Расхождение плейсхолдеров с params: " + string.Join(" | ", violations));
    }

    /// <summary>
    /// Запись кода слайса (<c>origin: slice</c>) без
    /// соответствующей C#-константы и без <c>deprecated</c> — сирота
    /// (переименование, удалённый слайс, опечатка в ключе). Платформенные
    /// записи не проверяются: они не бросаются ни одним слайсом и не
    /// обязаны иметь константу — сам файл <c>http.json</c> и есть их
    /// определение (см. CatalogFixture).
    /// </summary>
    [Fact]
    public void EverySliceEntry_HasMatchingApplicationCodeOrIsDeprecated()
    {
        var applicationCodes = CatalogFixture.ApplicationCodes.Select(c => c.Code).ToHashSet(StringComparer.Ordinal);

        var orphans = CatalogFixture.Catalog.Entries
            .Where(entry => entry.Origin == ErrorCatalogOrigin.Slice)
            .Where(entry => !entry.Deprecated && !applicationCodes.Contains(entry.Code))
            .Select(entry => entry.Code)
            .ToList();

        Assert.True(orphans.Count == 0, "Сироты каталога (нет кода в приложении, не deprecated): " + string.Join(", ", orphans));
    }

    /// <summary>Тип каждого параметра — из закрытого набора подсказок. Загрузчик уже отвергает неизвестные подсказки при разборе; здесь — утверждение по факту загруженного каталога.</summary>
    [Fact]
    public void EveryParamType_IsFromTheClosedSet()
    {
        var knownTypes = Enum.GetValues<ErrorParamType>().ToHashSet();

        var violations = CatalogFixture.Catalog.Entries
            .SelectMany(entry => entry.Params.Select(p => (entry.Code, p.Key, p.Value)))
            .Where(p => !knownTypes.Contains(p.Value))
            .ToList();

        Assert.True(violations.Count == 0, "Параметры с типом вне закрытого набора: " + string.Join(", ", violations));
    }
}
