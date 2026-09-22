using Mustask.Api.ErrorHandling.Catalog;
using Mustask.Shared.Errors;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Mustask.Api.ErrorHandling;

/// <summary>
/// Последний рубеж: всё, что не является <c>MustaskException</c> и не отменённым
/// запросом — дефект или отказ инфраструктуры. Полный stack trace идёт в лог
/// (алертит), а клиенту — обезличенный текст и <c>traceId</c>; текст исходного
/// исключения наружу не уходит. Расширение <c>exception</c> с деталями — только
/// в Development.
/// </summary>
/// <remarks>
/// <b>[ИЗМЕНЕНИЕ vs ADR-0004]</b> Раньше <c>Title</c>/<c>Detail</c> были
/// захардкожены здесь. Теперь код <c>http.internal_error</c> и его текст
/// (единый на оба поля — запись каталога генерическая) приходят из
/// <see cref="ErrorCatalog"/>, как и у бизнес-ошибок (ADR-0006):
/// единственный источник русского текста — каталог, без исключений для 500.
/// </remarks>
public sealed class GlobalExceptionHandler : IExceptionHandler
{
    /// <summary>Платформенный код для непредвиденных ошибок — до этой задачи у 500 кода не было вовсе.</summary>
    public const string InternalErrorCode = "http.internal_error";

    private readonly ILogger<GlobalExceptionHandler> _logger;
    private readonly IHostEnvironment _environment;
    private readonly IProblemDetailsService _problemDetailsService;
    private readonly ErrorCatalog _catalog;

    public GlobalExceptionHandler(
        ILogger<GlobalExceptionHandler> logger,
        IHostEnvironment environment,
        IProblemDetailsService problemDetailsService,
        ErrorCatalog catalog)
    {
        _logger = logger;
        _environment = environment;
        _problemDetailsService = problemDetailsService;
        _catalog = catalog;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        _logger.LogError(
            exception,
            "Unhandled exception for {Method} {Path}",
            httpContext.Request.Method,
            httpContext.Request.Path);

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;

        var detail = _catalog.RenderRu(InternalErrorCode, ErrorParams.None).Text;

        var problemDetails = new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Detail = detail,
        };

        problemDetails.Extensions["errorCode"] = InternalErrorCode;

        if (_environment.IsDevelopment())
        {
            problemDetails.Extensions["exception"] = new
            {
                type = exception.GetType().FullName,
                message = exception.Message,
                stackTrace = exception.StackTrace,
            };
        }

        await _problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = problemDetails,
        });

        return true;
    }
}
