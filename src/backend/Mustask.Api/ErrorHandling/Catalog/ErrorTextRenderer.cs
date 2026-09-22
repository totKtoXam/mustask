using System.Globalization;
using System.Text.RegularExpressions;
using Mustask.Shared.Errors;

namespace Mustask.Api.ErrorHandling.Catalog;

/// <summary>
/// Подставляет <see cref="ErrorParams"/> в шаблон каталога. Числа и даты
/// форматируются явными строками формата под <see cref="CultureInfo.InvariantCulture"/>,
/// а не именованной культурой (требуется стабильный вид независимо от
/// культуры процесса — этому текущий подход отвечает так же, но
/// без зависимости от ICU: <c>CultureInfo.GetCultureInfo("ru-RU")</c> в
/// статическом поле требует загруженных данных конкретной культуры и на
/// образе с <c>InvariantGlobalization</c> либо без ICU валил бы
/// <c>TypeInitializationException</c> на первом же ответе об ошибке — ровно
/// на пути, который обязан быть безотказным. Клиент всё равно форматирует
/// значения из <c>params</c> по своей локали — рендер этого текста
/// используется только как
/// запасной <c>detail</c> и текст лога, а не то, что видит пользователь при
/// известном ему коде, поэтому культуро-независимое форматирование здесь не
/// теряет требование, а снимает лишнюю зависимость. Экранирование
/// литеральной скобки — двойная скобка <c>{{</c>/<c>}}</c>.
/// </summary>
public static partial class ErrorTextRenderer
{

    /// <summary>
    /// Рендерит шаблон, подставляя значения по имени плейсхолдера. Плейсхолдер
    /// без соответствующего параметра остаётся в тексте как есть — страховка,
    /// а не отказ ответа: рендер никогда не бросает
    /// исключение из-за расхождения места броска с каталогом. Расхождение
    /// (недостающие имена) возвращается вызывающему, который решает, логировать
    /// его или нет — сам рендерер логгер не хранит.
    /// </summary>
    public static ErrorRenderResult Render(string template, ErrorParams parameters)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(parameters);

        var missing = new List<string>();

        var text = PlaceholderPattern().Replace(template, match =>
        {
            if (match.Value == "{{")
            {
                return "{";
            }

            if (match.Value == "}}")
            {
                return "}";
            }

            var name = match.Groups["name"].Value;

            if (parameters.TryGetValue(name, out var value))
            {
                return Format(value);
            }

            missing.Add(name);

            return match.Value;
        });

        return new ErrorRenderResult(text, missing);
    }

    private static string Format(ErrorParam param) => param.Type switch
    {
        ErrorParamType.Text => (string)param.Value,
        ErrorParamType.Number => ((decimal)param.Value).ToString(CultureInfo.InvariantCulture),
        ErrorParamType.Currency => ((decimal)param.Value).ToString(CultureInfo.InvariantCulture),
        ErrorParamType.Date => ((DateOnly)param.Value).ToString("dd.MM.yyyy", CultureInfo.InvariantCulture),
        ErrorParamType.DateTime => ((DateTimeOffset)param.Value).ToUniversalTime().ToString("dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture),
        _ => throw new NotSupportedException($"Неизвестный тип параметра ошибки: {param.Type}."),
    };

    [GeneratedRegex(@"\{\{|\}\}|\{(?<name>[A-Za-z0-9_]+)\}")]
    private static partial Regex PlaceholderPattern();
}

/// <summary>Результат подстановки: текст плюс имена плейсхолдеров, для которых не нашлось значения.</summary>
public sealed record ErrorRenderResult(string Text, IReadOnlyList<string> MissingParamNames)
{
    public bool HasMissingParams => MissingParamNames.Count > 0;
}
