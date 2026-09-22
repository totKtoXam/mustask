using Mustask.Api.ErrorHandling;
using Mustask.Api.ErrorHandling.Catalog;

namespace Mustask.Api.IntegrationTests;

/// <summary>
/// Unit-уровневые тесты над <see cref="PlatformErrorCodes.EnsureRequiredCodesArePresent"/>
/// — startup-проверка, закрывающая для <see cref="GlobalExceptionHandler"/> и
/// <c>ErrorHandlingExtensions.CustomizeProblemDetails</c> известный класс
/// отказа: код-«последняя надежда», переданный в <see cref="ErrorCatalog.RenderRu"/>
/// на пути обработки запроса, обязан существовать в каталоге — здесь это
/// проверяется на старте, а не по факту первого запроса.
/// </summary>
[Trait("Category", "Integration")]
public sealed class PlatformErrorCodesTests
{
    [Fact]
    public void EnsureRequiredCodesArePresent_RealProductCatalog_DoesNotThrow()
    {
        var exception = Record.Exception(
            () => PlatformErrorCodes.EnsureRequiredCodesArePresent(ErrorCatalogTestData.RealProductCatalog));

        Assert.Null(exception);
    }

    [Fact]
    public void EnsureRequiredCodesArePresent_CatalogMissingFallbackCode_Throws()
    {
        var catalog = ErrorCatalogLoader.Load([new ErrorCatalogSourceFile("broken.json", """
            {
              "module": "http",
              "origin": "platform",
              "codes": {
                "http.internal_error": {
                  "statuses": [500],
                  "params": {},
                  "ru": "Внутренняя ошибка сервера.",
                  "kk": "Сервердің ішкі қатесі."
                }
              }
            }
            """)]);

        var exception = Assert.Throws<InvalidOperationException>(
            () => PlatformErrorCodes.EnsureRequiredCodesArePresent(catalog));

        Assert.Contains(PlatformErrorCodes.FallbackCode, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void EnsureRequiredCodesArePresent_CatalogMissingInternalErrorCode_Throws()
    {
        var catalog = ErrorCatalogLoader.Load([new ErrorCatalogSourceFile("broken.json", """
            {
              "module": "http",
              "origin": "platform",
              "codes": {
                "http.request_failed": {
                  "statuses": [],
                  "params": {},
                  "ru": "Не удалось обработать запрос.",
                  "kk": "Сұранысты өңдеу мүмкін болмады."
                }
              }
            }
            """)]);

        var exception = Assert.Throws<InvalidOperationException>(
            () => PlatformErrorCodes.EnsureRequiredCodesArePresent(catalog));

        Assert.Contains(GlobalExceptionHandler.InternalErrorCode, exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// До этого теста в дереве не было ни одного
    /// теста на дубль статуса среди платформенных кодов — потерю единственного
    /// стартового вызова <see cref="PlatformErrorCodes.BuildCodeByStatus"/>
    /// (ушёл вместе с локальной переменной каталога при одном из фиксов) никто не
    /// заметил. Оба обязательных кода на месте — падать должна именно
    /// проверка дубля статуса, а не проверки наличия кодов выше.
    /// </summary>
    [Fact]
    public void EnsureRequiredCodesArePresent_DuplicateStatusAmongPlatformCodes_Throws()
    {
        var catalog = ErrorCatalogLoader.Load([new ErrorCatalogSourceFile("broken.json", """
            {
              "module": "http",
              "origin": "platform",
              "codes": {
                "http.internal_error": {
                  "statuses": [500],
                  "params": {},
                  "ru": "Внутренняя ошибка сервера.",
                  "kk": "Сервердің ішкі қатесі."
                },
                "http.request_failed": {
                  "statuses": [],
                  "params": {},
                  "ru": "Не удалось обработать запрос.",
                  "kk": "Сұранысты өңдеу мүмкін болмады."
                },
                "http.duplicate_status": {
                  "statuses": [500],
                  "params": {},
                  "ru": "Дубль статуса.",
                  "kk": "Мәртебенің қайталануы."
                }
              }
            }
            """)]);

        var exception = Assert.Throws<InvalidOperationException>(
            () => PlatformErrorCodes.EnsureRequiredCodesArePresent(catalog));

        Assert.Contains("500", exception.Message, StringComparison.Ordinal);
    }
}
