using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Mustask.Api.ErrorHandling;

/// <summary>
/// Ловит <see cref="BadHttpRequestException"/> — исключение, которым сам
/// ASP.NET Core сигнализирует о провале биндинга запроса (route/query/
/// <c>[AsParameters]</c>, например <c>?pageSize=abc</c> в
/// <c>Nullable&lt;int&gt;</c> или <c>?submittedFrom=notadate</c> в
/// <c>DateOnly</c>). Это отказ клиента, не дефект приложения: у исключения
/// уже есть корректный HTTP-статус в <see cref="BadHttpRequestException.StatusCode"/>
/// (обычно 400, у него же 413/415 для превышения размера тела/неверного
/// content-type — платформа переиспользует один и тот же тип для всей группы
/// "запрос нечитаем").
/// </summary>
/// <remarks>
/// Без этого обработчика исключение попадало бы в <see cref="GlobalExceptionHandler"/>
/// — тот безусловно форсирует статус 500 для всего, что не
/// <c>MustaskException</c>/<c>OperationCanceledException</c>, перекрывая
/// собственный корректный статус <see cref="BadHttpRequestException"/>.
/// Опечатка в любом списочном query-параметре реестра (`page`, `pageSize`
/// и т.п.) роняла бы запрос в 500 вместо 400. Это платформенное решение
/// (касается любого endpoint'а с <c>[AsParameters]</c>) — зафиксировано
/// строкой в ADR-0004, не только здесь.
/// <para>
/// Тело ответа намеренно не строится здесь вручную: обработчик выставляет
/// только <see cref="ProblemDetails.Status"/> и уходит через тот же
/// <see cref="IProblemDetailsService.TryWriteAsync"/>, что и остальные два
/// обработчика цепочки, — <c>CustomizeProblemDetails</c>
/// (<see cref="ErrorHandlingExtensions"/>) сама подставит <c>Title</c>,
/// <c>Detail</c> и <c>errorCode</c> по статусу через
/// <see cref="PlatformErrorCodes.BuildCodeByStatus"/> (тот же путь, что уже
/// работает для 404/405 через <c>UseStatusCodePages()</c> — см.
/// <c>ErrorHandlingTests.UnknownRoute_Returns404ProblemJson</c>). Для статуса
/// 400 это существующий платформенный код <c>http.bad_request</c>
/// (<c>contracts/errors/http.json</c>) — новый код заводить не пришлось.
/// Оригинальное сообщение исключения (оно эхом содержит переданное клиентом
/// значение, например имя параметра и текст, который не удалось разобрать)
/// клиенту не отдаётся — только в лог, тем же принципом, что и у
/// <see cref="GlobalExceptionHandler"/>.
/// </para>
/// </remarks>
public sealed class MalformedRequestExceptionHandler : IExceptionHandler
{
    private readonly ILogger<MalformedRequestExceptionHandler> _logger;
    private readonly IProblemDetailsService _problemDetailsService;

    public MalformedRequestExceptionHandler(
        ILogger<MalformedRequestExceptionHandler> logger,
        IProblemDetailsService problemDetailsService)
    {
        _logger = logger;
        _problemDetailsService = problemDetailsService;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not BadHttpRequestException badRequestException)
        {
            return false;
        }

        // Ожидаемый отказ клиента (неверно набранный query-параметр), не
        // дефект приложения — Warning для диагностики, не Error/алерт (тот же
        // принцип, что и BusinessExceptionHandler для MustaskException).
        // CWE-117: `badRequestException.
        // Message` эхом содержит URL-декодированное значение клиента (имя
        // параметра и не разобравшийся текст) -- раньше оно шло отдельным
        // параметром шаблона в plain-text-лог, `?page=1%0A<строка>` подмешивал
        // бы перевод строки как будто новую запись лога. Значение больше не
        // выносится отдельным параметром: exception и так передан первым
        // аргументом, Serilog пишет его как структурный exception-блок, а не
        // как часть шаблонной строки.
        _logger.LogWarning(
            exception,
            "Malformed request for {Method} {Path}",
            httpContext.Request.Method,
            httpContext.Request.Path);

        httpContext.Response.StatusCode = badRequestException.StatusCode;

        var problemDetails = new ProblemDetails
        {
            Status = badRequestException.StatusCode,
        };

        await _problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = problemDetails,
        });

        return true;
    }
}
