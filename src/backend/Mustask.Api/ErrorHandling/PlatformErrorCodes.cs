using Mustask.Api.ErrorHandling.Catalog;

namespace Mustask.Api.ErrorHandling;

/// <summary>
/// Карта HTTP-статус → платформенный код, построенная из каталога.
/// Заменяет прежние захардкоженные <c>DefaultTitlesByStatus</c> и
/// <c>DefaultErrorCodesByStatus</c> — единственный источник этих текстов
/// теперь каталог (ADR-0006), а не строки в этом файле.
/// </summary>
public static class PlatformErrorCodes
{
    /// <summary>
    /// Код-«последняя надежда»: назначается статусам, у которых тело ответа
    /// есть (<c>problem+json</c>), а собственной записи в каталоге нет —
    /// гарантирует ответ с кодом без необходимости перечислять каждый мыслимый статус.
    /// </summary>
    public const string FallbackCode = "http.request_failed";

    /// <summary>
    /// Строит карту статус → код по записям каталога с <c>origin: platform</c>.
    /// Deprecated-записи не участвуют — их статус, если он ещё не занят
    /// действующим кодом, должен уйти на <see cref="FallbackCode"/>, а не
    /// молча воскресить выведенный код.
    /// </summary>
    public static IReadOnlyDictionary<int, string> BuildCodeByStatus(ErrorCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);

        var result = new Dictionary<int, string>();

        foreach (var entry in catalog.Entries)
        {
            if (entry.Origin != ErrorCatalogOrigin.Platform || entry.Deprecated)
            {
                continue;
            }

            foreach (var status in entry.Statuses)
            {
                if (!result.TryAdd(status, entry.Code))
                {
                    throw new InvalidOperationException(
                        $"Несколько платформенных кодов каталога заявляют статус {status}: " +
                        $"\"{result[status]}\" и \"{entry.Code}\".");
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Проверяет на старте, что коды-«последняя надежда» — на которые
    /// <see cref="GlobalExceptionHandler"/> и <c>CustomizeProblemDetails</c>
    /// (<see cref="ErrorHandlingExtensions"/>) безусловно полагаются на пути
    /// обработки запроса, без права упасть, — присутствуют в каталоге.
    /// Тот же принцип, что и у остальных структурных проверок
    /// <see cref="ErrorCatalogLoader"/>: каталог, потерявший один из этих
    /// кодов, не должен дожить до первого запроса, а не ронять его
    /// необработанным исключением.
    /// </summary>
    /// <remarks>
    /// Заодно вызывает <see cref="BuildCodeByStatus"/> и отбрасывает
    /// результат — сама эта функция бросает исключение при
    /// дубле статуса среди платформенных кодов, а раньше её единственный
    /// вызов на старте жил в <c>AddMustaskErrorHandling</c> вместе с локальной
    /// переменной каталога. Когда та переменная ушла (резолв теперь из DI на
    /// каждый запрос — см. <see cref="ErrorHandlingExtensions"/>), вместе с
    /// ней по недосмотру ушёл и единственный вызов на старте: дубль статуса
    /// стал доживать до первого ответа и падать прямо в
    /// <c>CustomizeProblemDetails</c> — тот же класс отказа на пути запроса,
    /// что устраняет проверка наличия кодов на старте. Здесь эта проверка
    /// не зависит от того, где и как часто резолвится каталог на запросе.
    /// </remarks>
    public static void EnsureRequiredCodesArePresent(ErrorCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);

        EnsureCode(catalog, FallbackCode);
        EnsureCode(catalog, GlobalExceptionHandler.InternalErrorCode);

        // Результат не нужен здесь — нужен сам факт, что построение не
        // бросило на дубле статуса среди платформенных кодов.
        _ = BuildCodeByStatus(catalog);
    }

    private static void EnsureCode(ErrorCatalog catalog, string code)
    {
        if (!catalog.TryGet(code, out _))
        {
            throw new InvalidOperationException(
                $"Каталог ошибок не содержит обязательный платформенный код \"{code}\": " +
                "без него обработка непредвиденных ошибок на горячем пути не может " +
                "гарантированно отрендерить обезличенный ответ.");
        }
    }
}
