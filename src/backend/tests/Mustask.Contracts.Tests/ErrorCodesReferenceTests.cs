using System.Text;
using Mustask.Api.ErrorHandling.Catalog;
using Mustask.Shared.Errors;

namespace Mustask.Contracts.Tests;

/// <summary>
/// <c>docs/error-codes.md</c> — генерируемый и
/// закоммиченный, расхождение с каталогом роняет сборку. Тот же тест умеет
/// перегенерировать файл (<c>MUSTASK_UPDATE_ERROR_CODES=1</c>) — отдельного
/// консольного генератора нет.
/// </summary>
public sealed class ErrorCodesReferenceTests
{
    private static string ReferencePath =>
        Path.Combine(RepositoryLayout.Root.FullName, "docs", "error-codes.md");

    /// <summary>
    /// У каждого кода — строка со всеми колонками; платформенный код
    /// показывает происхождение «назначается по HTTP-статусу», а не имя
    /// модуля; deprecated виден явно.
    /// И правка каталога без регенерации файла, и ручная правка
    /// файла без правки каталога — обе стороны расхождения ловятся одним и
    /// тем же сравнением содержимого.
    /// </summary>
    [Fact]
    public void ReferenceDocument_MatchesGeneratedContentFromCatalog()
    {
        var expected = Normalize(ErrorCodesReferenceGenerator.Render(CatalogFixture.Catalog.Entries));

        if (Environment.GetEnvironmentVariable("MUSTASK_UPDATE_ERROR_CODES") == "1")
        {
            File.WriteAllText(ReferencePath, expected, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            return;
        }

        Assert.True(File.Exists(ReferencePath), $"{ReferencePath} не найден — перегенерируйте: MUSTASK_UPDATE_ERROR_CODES=1 dotnet test src/backend/tests/Mustask.Contracts.Tests");

        var actual = Normalize(File.ReadAllText(ReferencePath));

        Assert.True(
            actual == expected,
            "docs/error-codes.md разошёлся с каталогом contracts/errors/*.json. " +
            "Перегенерируйте: MUSTASK_UPDATE_ERROR_CODES=1 dotnet test src/backend/tests/Mustask.Contracts.Tests");
    }

    /// <summary>Файл — UTF-8 без BOM (правило генератора, чтобы diff не шумел маркером кодировки).</summary>
    [Fact]
    public void ReferenceDocument_HasNoUtf8Bom()
    {
        var bytes = File.ReadAllBytes(ReferencePath);
        var utf8Bom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true).GetPreamble();

        Assert.False(bytes.Length >= utf8Bom.Length && bytes.AsSpan(0, utf8Bom.Length).SequenceEqual(utf8Bom));
    }

    /// <summary>
    /// Перенос строки в `ru`/`kk` не запрещён
    /// загрузчиком и раньше ломал одну строку markdown-таблицы на несколько
    /// без единого красного теста — обе стороны сравнения выше порождены
    /// одним и тем же текстом, поэтому расхождение с самим собой они не
    /// ловят. Проверяем сам генератор напрямую, на синтетической записи.
    /// </summary>
    [Fact]
    public void Render_GivenTextWithNewline_KeepsEntryOnASingleTableRow()
    {
        var entries = new[]
        {
            new ErrorCatalogEntry(
                "test.multiline",
                "test",
                ErrorCatalogOrigin.Slice,
                [400],
                new Dictionary<string, ErrorParamType>(),
                "Строка один\nСтрока два",
                "Бір жол\nЕкінші жол",
                Deprecated: false,
                KkNeedsReview: false),
        };

        var rendered = ErrorCodesReferenceGenerator.Render(entries);
        var dataLines = rendered.TrimEnd('\n').Split('\n')
            .Where(line => line.StartsWith("|test.multiline", StringComparison.Ordinal))
            .ToList();

        var row = Assert.Single(dataLines);
        Assert.Contains("Строка один\\nСтрока два", row, StringComparison.Ordinal);
        Assert.Contains("Бір жол\\nЕкінші жол", row, StringComparison.Ordinal);
        Assert.Equal(8, row.Count(c => c == '|'));
    }

    /// <summary>Перевод строк нормализуется при сравнении — редактор на другой ОС не должен красить эту проверку.</summary>
    private static string Normalize(string text) => text.Replace("\r\n", "\n", StringComparison.Ordinal).TrimEnd('\n') + "\n";
}
