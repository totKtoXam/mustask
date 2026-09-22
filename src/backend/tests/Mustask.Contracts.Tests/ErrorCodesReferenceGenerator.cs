using System.Text;
using Mustask.Api.ErrorHandling.Catalog;

namespace Mustask.Contracts.Tests;

/// <summary>
/// Рендерит <c>docs/error-codes.md</c> из каталога. Единственная
/// логика рендера справочника — здесь, а не в отдельном консольном
/// проекте-генераторе (лишняя строка в <c>Mustask.sln</c> и
/// второе место с той же логикой). Используется и тестом сверки
/// (<see cref="ErrorCodesReferenceTests"/>), и режимом обновления
/// (<c>MUSTASK_UPDATE_ERROR_CODES=1</c>).
/// </summary>
internal static class ErrorCodesReferenceGenerator
{
    private const string Header = """
        # API Error Code Reference

        **This file is generated** from `contracts/errors/*.json` by the
        `src/backend/tests/Mustask.Contracts.Tests` test (`ErrorCodesReferenceTests`) —
        do not edit it by hand, the edit will be lost on the next regeneration, and a
        mismatch with the catalog fails the build.

        Regenerate after editing the catalog:

        ```sh
        MUSTASK_UPDATE_ERROR_CODES=1 dotnet test src/backend/tests/Mustask.Contracts.Tests
        ```

        The "kk" column is the entry's Kazakh text; the "needs review" mark means
        the text was written by a non-native speaker and the auto-check only
        guarantees it exists and differs from the Russian, not its quality
        (`contracts/README.md`).

        | Code | HTTP status(es) | Russian | Kazakh | Origin | Deprecated | kk |
        |---|---|---|---|---|---|---|
        """;

    public static string Render(IReadOnlyCollection<ErrorCatalogEntry> entries)
    {
        var builder = new StringBuilder();
        builder.Append(Header).Append('\n');

        foreach (var entry in entries.OrderBy(e => e.Code, StringComparer.Ordinal))
        {
            var statuses = entry.Statuses.Count == 0
                ? "any"
                : string.Join(", ", entry.Statuses.OrderBy(s => s));

            var origin = entry.Origin == ErrorCatalogOrigin.Platform
                ? "assigned by HTTP status"
                : entry.Module;

            var deprecated = entry.Deprecated ? "yes" : "—";
            var kkReview = entry.KkNeedsReview ? "needs review" : "—";

            builder.Append('|').Append(Escape(entry.Code))
                .Append('|').Append(statuses)
                .Append('|').Append(Escape(entry.Ru))
                .Append('|').Append(Escape(entry.Kk))
                .Append('|').Append(Escape(origin))
                .Append('|').Append(deprecated)
                .Append('|').Append(kkReview)
                .Append('|').Append('\n');
        }

        return builder.ToString();
    }

    /// <summary>
    /// Раньше экранировалась только `|` —
    /// перенос строки в `ru`/`kk` (загрузчик его не запрещает) разбивал одну
    /// строку markdown-таблицы на несколько, а сверка (`ErrorCodesReferenceTests`)
    /// оставалась зелёной, потому что обе стороны сравнения порождены одним
    /// и тем же текстом. `\r\n` нормализуется до `\n` до экранирования, чтобы
    /// вид не зависел от перевода строк исходного файла каталога.
    /// </summary>
    private static string Escape(string value) => value
        .Replace("\r\n", "\n", StringComparison.Ordinal)
        .Replace("\n", "\\n", StringComparison.Ordinal)
        .Replace("|", "\\|", StringComparison.Ordinal);
}
