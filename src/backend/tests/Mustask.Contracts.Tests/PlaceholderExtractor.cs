using System.Text.RegularExpressions;

namespace Mustask.Contracts.Tests;

/// <summary>
/// Извлекает имена плейсхолдеров <c>{name}</c> из текста записи каталога,
/// с тем же правилом экранирования <c>{{</c>/<c>}}</c>, что и у рендерера
/// сервера (<c>Mustask.Api.ErrorHandling.Catalog.ErrorTextRenderer</c>) — иначе
/// сверка «плейсхолдеры = params» проверяла бы не то же самое, что видит
/// пользователь при подстановке.
/// </summary>
internal static partial class PlaceholderExtractor
{
    public static IReadOnlySet<string> Extract(string template)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);

        foreach (Match match in PlaceholderPattern().Matches(template))
        {
            if (match.Value is "{{" or "}}")
            {
                continue;
            }

            names.Add(match.Groups["name"].Value);
        }

        return names;
    }

    [GeneratedRegex(@"\{\{|\}\}|\{(?<name>[A-Za-z0-9_]+)\}")]
    private static partial Regex PlaceholderPattern();
}
