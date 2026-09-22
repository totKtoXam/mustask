using System.Diagnostics;
using Mustask.Api.ErrorHandling.Catalog;
using Mustask.Shared.Errors;
using Microsoft.Extensions.DependencyInjection;

namespace Mustask.Api.ErrorHandling;

/// <summary>
/// Подключение единого механизма обработки ошибок (см. ADR-0004).
/// Четыре <see cref="IExceptionHandler"/> регистрируются строго в порядке
/// cancel → business → malformed → global: порядок регистрации в DI
/// определяет порядок вызова цепочкой <c>ExceptionHandlerMiddleware</c>.
/// <c>malformed</c> (<see cref="MalformedRequestExceptionHandler"/>) добавлен,
/// потому что без него <see cref="Microsoft.AspNetCore.Http.BadHttpRequestException"/> от
/// провала биндинга query-параметров (<c>?pageSize=abc</c>) доходило до
/// <c>global</c> и безусловно превращалось в 500.
/// </summary>
/// <remarks>
/// <b>[ИЗМЕНЕНИЕ vs ADR-0004]</b> Раньше <c>Title</c> и код по умолчанию для
/// статусов, которые сама платформа выставляет в обход трёх наших
/// <c>IExceptionHandler</c> (404/405 из <c>UseStatusCodePages()</c>, 500),
/// жили здесь захардкоженными словарями. Теперь единственный источник этих
/// текстов — <see cref="ErrorCatalog"/> (ADR-0006, решение
/// «Title уходит в каталог наравне с текстом ошибки»):
/// платформенные коды каталога (<c>origin: platform</c>) несут и заголовок
/// класса ответа, и запасной <c>detail</c> одним и тем же текстом — они
/// генерические и не завязаны на конкретную бизнес-ошибку.
/// </remarks>
public static class ErrorHandlingExtensions
{
    /// <summary>
    /// Логическое имя, под которым файлы <c>contracts/errors/*.json</c>
    /// встроены в сборку (<c>Mustask.Api.csproj</c>, <c>EmbeddedResource</c> с
    /// явным <c>LogicalName</c>). Публичный — переиспользуется тестами,
    /// которым нужен настоящий продуктовый каталог без хоста
    /// (см. <c>Mustask.Api.IntegrationTests.ErrorCatalogTestData</c>).
    /// </summary>
    public const string ErrorCatalogResourcePrefix = "ErrorCatalog/";

    public static IServiceCollection AddMustaskErrorHandling(this IServiceCollection services)
    {
        // Разбирается немедленно, не лениво — синглтон получает уже готовый
        // экземпляр, а не фабрику: каталог с дублем кода, неизвестной
        // подсказкой типа или пустым текстом валит старт приложения, а не
        // первый запрос (ADR-0006).
        var catalog = ErrorCatalogLoader.LoadFromEmbeddedResources(
            typeof(ErrorHandlingExtensions).Assembly, ErrorCatalogResourcePrefix);

        // Тот же принцип, что и у остальных структурных проверок загрузчика:
        // коды-«последняя надежда», на которые ниже безусловно полагаются
        // GlobalExceptionHandler и CustomizeProblemDetails, обязаны
        // присутствовать в каталоге — иначе они рискуют бросить
        // KeyNotFoundException прямо на пути запроса.
        PlatformErrorCodes.EnsureRequiredCodesArePresent(catalog);

        services.AddSingleton(catalog);

        services.AddProblemDetails(options =>
        {
            options.CustomizeProblemDetails = context =>
            {
                context.ProblemDetails.Extensions["traceId"] =
                    Activity.Current?.Id ?? context.HttpContext.TraceIdentifier;

                // Instance — URI reference (RFC 9457); "GET /path" им не
                // является. Метод — отдельным расширением.
                context.ProblemDetails.Instance = context.HttpContext.Request.Path;
                context.ProblemDetails.Extensions["method"] = context.HttpContext.Request.Method;

                if (context.ProblemDetails.Status is not { } status)
                {
                    return;
                }

                // Раньше это замыкание держало
                // локальную переменную `catalog`, а не резолвило её из DI —
                // регистрация синглтона выше была декоративной для этого
                // пути. Тесты (MustaskApiFactory) подменяют регистрацию
                // ErrorCatalog в контейнере уже после AddMustaskErrorHandling
                // (объединённый продуктовый + тестовый каталог) — замыкание
                // на локальную переменную эту подмену не увидело бы, и два
                // источника истины разошлись бы молча. Карта статус → код
                // строится из того же резолвленного каталога на каждый
                // ответ — дешёво (не хот-путь: только при построении тела
                // ошибки), а не один раз при старте из локальной переменной.
                var requestCatalog = context.HttpContext.RequestServices.GetRequiredService<ErrorCatalog>();
                var codeByStatus = PlatformErrorCodes.BuildCodeByStatus(requestCatalog);

                var platformCode = codeByStatus.TryGetValue(status, out var mappedCode)
                    ? mappedCode
                    : PlatformErrorCodes.FallbackCode;

                var platformText = requestCatalog.RenderRu(platformCode, ErrorParams.None).Text;

                // Title назначается по статусу безусловно, перезаписывая
                // то, что мог бы положить обработчик — он описывает класс
                // ответа, а не конкретную ошибку (её различает errorCode,
                // который ниже добавляется через TryAdd, а не перезаписывается).
                context.ProblemDetails.Title = platformText;

                context.ProblemDetails.Extensions.TryAdd("errorCode", platformCode);

                // Detail для платформенного пути (UseStatusCodePages, нет
                // своей бизнес-ошибки) — тот же текст, что и Title: запись
                // каталога генерическая и не завязана на конкретный случай.
                // Если Detail уже выставлен (BusinessExceptionHandler,
                // GlobalExceptionHandler) — не перетираем: их текст точнее.
                if (string.IsNullOrEmpty(context.ProblemDetails.Detail))
                {
                    context.ProblemDetails.Detail = platformText;
                }
            };
        });

        services.AddExceptionHandler<OperationCanceledExceptionHandler>();
        services.AddExceptionHandler<BusinessExceptionHandler>();
        services.AddExceptionHandler<MalformedRequestExceptionHandler>();
        services.AddExceptionHandler<GlobalExceptionHandler>();

        return services;
    }

    public static WebApplication UseMustaskErrorHandling(this WebApplication app)
    {
        // UseExceptionHandler() первым в pipeline: перехватывает исключения
        // раньше остальной middleware-цепочки.
        app.UseExceptionHandler();

        // 404 на несуществующий маршрут и 405 тоже отдают application/problem+json,
        // а не пустое тело.
        app.UseStatusCodePages();

        return app;
    }
}
