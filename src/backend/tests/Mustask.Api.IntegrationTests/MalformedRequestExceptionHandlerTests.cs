using Mustask.Api.ErrorHandling;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Mustask.Api.IntegrationTests;

/// <summary>
/// Unit-уровневые тесты над <see cref="MalformedRequestExceptionHandler"/> в
/// изоляции от HTTP-конвейера (без поднятия хоста/Docker) — тот же приём, что
/// и у <see cref="GlobalExceptionHandlerTests"/>/<see cref="OperationCanceledExceptionHandlerTests"/>.
/// Заполнение <c>errorCode</c>/<c>detail</c> из каталога по статусу
/// (<c>CustomizeProblemDetails</c>, <see cref="ErrorHandlingExtensions"/>) —
/// поведение реального <c>DefaultProblemDetailsService</c>, которого здесь
/// нет; сквозная проверка через HTTP-запрос добавляется вместе с первым
/// реестровым endpoint'ом.
/// </summary>
[Trait("Category", "Integration")]
public sealed class MalformedRequestExceptionHandlerTests
{
    [Fact]
    public async Task TryHandleAsync_BadHttpRequestException_UsesItsOwnStatusCodeAndLogsWarningNotError()
    {
        var logger = new CapturingLogger<MalformedRequestExceptionHandler>();
        var problemDetailsService = new FakeProblemDetailsService();
        var handler = new MalformedRequestExceptionHandler(logger, problemDetailsService);
        var context = new DefaultHttpContext();
        var exception = new BadHttpRequestException(
            "Failed to bind parameter \"Nullable<Int32> PageSize\" from \"abc\".");

        var handled = await handler.TryHandleAsync(context, exception, CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(400, context.Response.StatusCode);
        Assert.Equal(400, problemDetailsService.LastContext!.ProblemDetails.Status);

        // Ожидаемый отказ клиента, не дефект приложения -- Warning, не
        // Error/алерт (тот же принцип, что и BusinessExceptionHandler).
        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Same(exception, entry.Exception);
    }

    /// <summary>
    /// BadHttpRequestException несёт собственный StatusCode -- не всегда 400
    /// (413/415 для превышения размера тела/неверного content-type). Хендлер
    /// обязан использовать его, а не захардкоженную константу.
    /// </summary>
    [Fact]
    public async Task TryHandleAsync_BadHttpRequestExceptionWithNonDefaultStatusCode_UsesThatStatusCode()
    {
        var handler = new MalformedRequestExceptionHandler(
            new CapturingLogger<MalformedRequestExceptionHandler>(), new FakeProblemDetailsService());
        var context = new DefaultHttpContext();
        var exception = new BadHttpRequestException("Request body too large.", StatusCodes.Status413PayloadTooLarge);

        await handler.TryHandleAsync(context, exception, CancellationToken.None);

        Assert.Equal(413, context.Response.StatusCode);
    }

    [Fact]
    public async Task TryHandleAsync_NotBadHttpRequestException_ReturnsFalseAndDoesNotTouchResponse()
    {
        var handler = new MalformedRequestExceptionHandler(
            new CapturingLogger<MalformedRequestExceptionHandler>(), new FakeProblemDetailsService());
        var context = new DefaultHttpContext();

        var handled = await handler.TryHandleAsync(
            context, new InvalidOperationException("настоящий дефект, не провал биндинга"), CancellationToken.None);

        Assert.False(handled);
        Assert.Equal(200, context.Response.StatusCode);
    }

    /// <summary>
    /// Исходное сообщение исключения (эхо переданного клиентом
    /// значения) не должно попадать в тело ответа -- только в лог. Единственный
    /// источник текста ответа -- каталог (через CustomizeProblemDetails),
    /// который здесь не участвует, поэтому Detail остаётся пустым на этом
    /// уровне -- проверяем именно то, что хендлер сам его не проставляет из
    /// exception.Message.
    /// </summary>
    [Fact]
    public async Task TryHandleAsync_BadHttpRequestException_DoesNotCopyOriginalMessageIntoProblemDetails()
    {
        var problemDetailsService = new FakeProblemDetailsService();
        var handler = new MalformedRequestExceptionHandler(
            new CapturingLogger<MalformedRequestExceptionHandler>(), problemDetailsService);
        var context = new DefaultHttpContext();
        var exception = new BadHttpRequestException("Секретная деталь провала биндинга параметра.");

        await handler.TryHandleAsync(context, exception, CancellationToken.None);

        var detail = problemDetailsService.LastContext!.ProblemDetails.Detail;
        Assert.True(string.IsNullOrEmpty(detail) || !detail.Contains("Секретная деталь"));
    }

    /// <summary>
    /// CWE-117: раньше <c>Message</c>
    /// исключения (URL-декодированное, эхом содержащее значение клиента)
    /// выносился отдельным параметром шаблона лога -- `?page=1%0A&lt;строка&gt;`
    /// подмешивал бы перевод строки в plain-text-приёмник как будто новую
    /// запись. Проверяем итоговую отформатированную строку сообщения (не
    /// исключение целиком -- оно по-прежнему передаётся первым аргументом и
    /// логируется структурно, это ожидаемо): она не должна содержать
    /// значение, которое не удалось разобрать.
    /// </summary>
    [Fact]
    public async Task TryHandleAsync_BadHttpRequestException_DoesNotEmbedRawMessageInFormattedLogLine()
    {
        var logger = new CapturingLogger<MalformedRequestExceptionHandler>();
        var handler = new MalformedRequestExceptionHandler(logger, new FakeProblemDetailsService());
        var context = new DefaultHttpContext();
        var exception = new BadHttpRequestException(
            "Failed to bind parameter \"Nullable<Int32> Page\" from \"1\n<forged-log-line>\".");

        await handler.TryHandleAsync(context, exception, CancellationToken.None);

        var entry = Assert.Single(logger.Entries);
        Assert.DoesNotContain("forged-log-line", entry.Message);
        Assert.DoesNotContain("Failed to bind parameter", entry.Message);
    }
}
