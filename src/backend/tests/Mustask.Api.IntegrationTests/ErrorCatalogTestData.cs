using Mustask.Api.ErrorHandling;
using Mustask.Api.ErrorHandling.Catalog;

namespace Mustask.Api.IntegrationTests;

/// <summary>
/// Каталог для тестовых кодов <c>test.*</c>, которые бросает
/// <see cref="TestEndpointsStartupFilter"/>. Не кладём их в
/// <c>contracts/errors/</c> — тестовые коды продукта не касаются:
/// реальный хост в <see cref="MustaskApiFactory"/>
/// подменяет <c>ErrorCatalog</c> объединением настоящего каталога с этим,
/// через <c>ConfigureTestServices</c>.
/// </summary>
internal static class ErrorCatalogTestData
{
    private const string Json = """
        {
          "module": "test",
          "origin": "slice",
          "codes": {
            "test.not_found": {
              "statuses": [404],
              "params": {},
              "ru": "Тестовая ошибка: не найдено.",
              "kk": "Тестілік қате: табылмады."
            },
            "test.conflict": {
              "statuses": [409],
              "params": {},
              "ru": "Тестовая ошибка: конфликт.",
              "kk": "Тестілік қате: қайшылық."
            },
            "test.conflict_with_params": {
              "statuses": [409],
              "params": { "used": "number", "limit": "number" },
              "ru": "Занято {used} из {limit}.",
              "kk": "{limit} ішінен {used} пайдаланылды."
            },
            "test.forbidden": {
              "statuses": [403],
              "params": {},
              "ru": "Тестовая ошибка: доступ запрещён.",
              "kk": "Тестілік қате: кіруге тыйым салынған."
            },
            "test.unauthorized": {
              "statuses": [401],
              "params": {},
              "ru": "Тестовая ошибка: не аутентифицирован.",
              "kk": "Тестілік қате: аутентификация жоқ."
            },
            "test.validation_failed": {
              "statuses": [400],
              "params": {},
              "ru": "Тестовая ошибка: не прошла валидация.",
              "kk": "Тестілік қате: валидациядан өтпеді."
            },
            "test.field_required": {
              "statuses": [400],
              "params": {},
              "ru": "Обязательное поле.",
              "kk": "Міндетті өріс."
            },
            "test.date_param": {
              "statuses": [409],
              "params": { "date": "date" },
              "ru": "Дата: {date}.",
              "kk": "Күні: {date}."
            }
          }
        }
        """;

    /// <summary>
    /// Каталог только с <c>test.*</c> — используется unit-уровневыми тестами
    /// обработчика напрямую, без сборки хоста.
    /// </summary>
    internal static ErrorCatalog Catalog { get; } =
        ErrorCatalogLoader.Load([new ErrorCatalogSourceFile("test-codes.json", Json)]);

    /// <summary>
    /// Настоящий продуктовый каталог (<c>contracts/errors/*.json</c>),
    /// прочитанный из тех же встроенных ресурсов, что использует
    /// <c>AddMustaskErrorHandling</c> — нужен unit-уровневым тестам обработчиков
    /// платформенных кодов (например, <c>GlobalExceptionHandler</c>,
    /// <c>http.internal_error</c>) без сборки хоста.
    /// </summary>
    internal static ErrorCatalog RealProductCatalog { get; } = ErrorCatalogLoader.LoadFromEmbeddedResources(
        typeof(ErrorHandlingExtensions).Assembly, ErrorHandlingExtensions.ErrorCatalogResourcePrefix);

    /// <summary>
    /// Каталог продукта плюс <c>test.*</c> — используется реальным хостом,
    /// чтобы <c>/__test/throw/*</c> получал русский <c>detail</c> из
    /// каталога так же, как это делает любой бизнес-код. Тесты
    /// обработки ошибок по-прежнему проверяются на настоящих платформенных кодах, поэтому
    /// подмена каталога не маскирует то, что проверяет тест.
    /// </summary>
    public static ErrorCatalog MergedWithProductCatalog(ErrorCatalog productCatalog)
    {
        var merged = new Dictionary<string, ErrorCatalogEntry>(StringComparer.Ordinal);

        foreach (var entry in productCatalog.Entries)
        {
            merged[entry.Code] = entry;
        }

        foreach (var entry in Catalog.Entries)
        {
            merged[entry.Code] = entry;
        }

        return new ErrorCatalog(merged);
    }
}
