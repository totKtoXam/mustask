using System.Text.Json;
using Mustask.Api.ErrorHandling;
using Mustask.Shared.Errors;
using Mustask.Shared.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Mustask.Api.IntegrationTests;

/// <summary>
/// Unit-уровневые тесты над <see cref="BusinessExceptionHandler"/>: уровень
/// логирования (Warning, без объекта исключения — иначе алерты станут
/// бесполезными), русский текст в той же записи журнала и
/// содержимое ProblemDetails проверяются здесь напрямую,
/// без поднятия хоста и Docker, на каталоге <see cref="ErrorCatalogTestData"/>.
/// Сквозная доставка записи до реального <c>ILoggerProvider</c> проверяется
/// отдельно, в
/// <c>ErrorHandlingTests.CancelEndpoint_...ThroughRealLoggerProvider</c>
/// (<c>writeToProviders: true</c> в Program.cs).
/// </summary>
[Trait("Category", "Integration")]
public sealed class BusinessExceptionHandlerTests
{
    [Fact]
    public async Task TryHandleAsync_MustaskExceptionWithoutCause_LogsWarningWithoutExceptionObject()
    {
        var logger = new CapturingLogger<BusinessExceptionHandler>();
        var handler = new BusinessExceptionHandler(logger, new FakeProblemDetailsService(), ErrorCatalogTestData.Catalog);
        var context = new DefaultHttpContext();

        var handled = await handler.TryHandleAsync(
            context,
            new NotFoundException("test.not_found"),
            CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(404, context.Response.StatusCode);
        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Null(entry.Exception);
    }

    /// <summary>
    /// Русский текст ошибки попадает в ту же запись журнала,
    /// что и код, метод и путь — раньше этого не было.
    /// </summary>
    [Fact]
    public async Task TryHandleAsync_MustaskException_LogsRussianDetailAlongsideCode()
    {
        var logger = new CapturingLogger<BusinessExceptionHandler>();
        var handler = new BusinessExceptionHandler(logger, new FakeProblemDetailsService(), ErrorCatalogTestData.Catalog);
        var context = new DefaultHttpContext();
        context.Request.Method = "GET";
        context.Request.Path = "/__test/throw/notfound";

        await handler.TryHandleAsync(context, new NotFoundException("test.not_found"), CancellationToken.None);

        var entry = Assert.Single(logger.Entries);
        Assert.Contains("test.not_found", entry.Message, StringComparison.Ordinal);
        Assert.Contains("GET", entry.Message, StringComparison.Ordinal);
        Assert.Contains("/__test/throw/notfound", entry.Message, StringComparison.Ordinal);
        Assert.Contains("Тестовая ошибка: не найдено.", entry.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Раньше в лог уходил уже отрендеренный
    /// <c>detail</c> — шаблон, склеенный со значениями <c>params</c>, одной
    /// свободной строкой (канал для CWE-117 и утечки персональных данных
    /// нераспознаваемым образом). Теперь в сообщении лога — неподставленный
    /// шаблон каталога с плейсхолдерами как есть; значения уходят отдельным
    /// структурным полем, а не склеенной фразой.
    /// </summary>
    [Fact]
    public async Task TryHandleAsync_MustaskExceptionWithParams_LogsUnrenderedTemplateNotRenderedSentence()
    {
        var logger = new CapturingLogger<BusinessExceptionHandler>();
        var handler = new BusinessExceptionHandler(logger, new FakeProblemDetailsService(), ErrorCatalogTestData.Catalog);
        var context = new DefaultHttpContext();

        await handler.TryHandleAsync(
            context,
            new ConflictException("test.conflict_with_params", new ErrorParams
            {
                ["used"] = ErrorParam.Number(50m),
                ["limit"] = ErrorParam.Number(50m),
            }),
            CancellationToken.None);

        var entry = Assert.Single(logger.Entries);
        Assert.Contains("Занято {used} из {limit}.", entry.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("Занято 50 из 50.", entry.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Конвенция обещает: причину бизнес-ошибки передают через
    /// <c>innerException</c>, наружу она не рендерится, но попадает в
    /// структурный лог. Раньше обещание не выполнялось — причина терялась
    /// и в ответе, и в логе.
    /// </summary>
    [Fact]
    public async Task TryHandleAsync_MustaskExceptionWithCause_LogsInnerExceptionAsTheCause()
    {
        var logger = new CapturingLogger<BusinessExceptionHandler>();
        var handler = new BusinessExceptionHandler(logger, new FakeProblemDetailsService(), ErrorCatalogTestData.Catalog);
        var context = new DefaultHttpContext();
        var cause = new InvalidOperationException("Причина, видная только в логах.");

        var handled = await handler.TryHandleAsync(
            context,
            new ConflictException("test.conflict", innerException: cause),
            CancellationToken.None);

        Assert.True(handled);
        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
        Assert.Same(cause, entry.Exception);
    }

    /// <summary>
    /// Код без записи в каталоге (опечатка, код
    /// мимо [ErrorCodes], рассинхрон с константой) не должен ронять
    /// обработчик исключением из середины — сквозной сценарий проверен в
    /// <c>ErrorHandlingTests.ThrowEndpoint_CodeMissingFromCatalog_ProductionReturnsProblemJsonInsteadOfCrashing</c>,
    /// здесь — что именно делает <see cref="BusinessExceptionHandler"/>:
    /// отдаёт исключение дальше (<see langword="false"/>), не трогает
    /// httpContext и алертит через Error, а не молчаливый Warning.
    /// </summary>
    [Fact]
    public async Task TryHandleAsync_CodeMissingFromCatalog_ReturnsFalseAndLogsErrorInsteadOfThrowing()
    {
        var logger = new CapturingLogger<BusinessExceptionHandler>();
        var handler = new BusinessExceptionHandler(logger, new FakeProblemDetailsService(), ErrorCatalogTestData.Catalog);
        var context = new DefaultHttpContext();
        var exception = new ConflictException("test.code_not_in_catalog");

        var handled = await handler.TryHandleAsync(context, exception, CancellationToken.None);

        Assert.False(handled);
        Assert.Equal(200, context.Response.StatusCode); // не тронут — источник истины теперь GlobalExceptionHandler.
        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.Same(exception, entry.Exception);
        Assert.Contains("test.code_not_in_catalog", entry.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Тот же класс дефекта, но на уровне отдельной причины поля
    /// <see cref="ValidationException"/>: верхнеуровневый код исключения
    /// зарегистрирован, а код одной из причин поля — нет. Тоже не должен
    /// бросать из <see cref="BusinessExceptionHandler.TryHandleAsync"/>
    /// (раньше падал бы внутри <c>BuildErrors</c>).
    /// </summary>
    [Fact]
    public async Task TryHandleAsync_ValidationException_FieldErrorCodeMissingFromCatalog_ReturnsFalseAndLogsError()
    {
        var logger = new CapturingLogger<BusinessExceptionHandler>();
        var handler = new BusinessExceptionHandler(logger, new FakeProblemDetailsService(), ErrorCatalogTestData.Catalog);
        var context = new DefaultHttpContext();
        var errors = new Dictionary<string, IReadOnlyList<FieldError>>
        {
            ["field"] = [new FieldError("test.field_code_not_in_catalog")],
        };
        var exception = new ValidationException("test.validation_failed", errors);

        var handled = await handler.TryHandleAsync(context, exception, CancellationToken.None);

        Assert.False(handled);
        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.Contains("test.field_code_not_in_catalog", entry.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TryHandleAsync_NonMustaskException_ReturnsFalseAndDoesNotLog()
    {
        var logger = new CapturingLogger<BusinessExceptionHandler>();
        var handler = new BusinessExceptionHandler(logger, new FakeProblemDetailsService(), ErrorCatalogTestData.Catalog);
        var context = new DefaultHttpContext();

        var handled = await handler.TryHandleAsync(context, new InvalidOperationException(), CancellationToken.None);

        Assert.False(handled);
        Assert.Empty(logger.Entries);
    }

    [Fact]
    public async Task TryHandleAsync_ValidationException_BuildsProblemDetailsWithErrorsAndErrorCode()
    {
        var problemDetailsService = new FakeProblemDetailsService();
        var handler = new BusinessExceptionHandler(
            new CapturingLogger<BusinessExceptionHandler>(), problemDetailsService, ErrorCatalogTestData.Catalog);
        var context = new DefaultHttpContext();
        var errors = new Dictionary<string, IReadOnlyList<FieldError>>
        {
            ["field"] = [new FieldError("test.field_required")],
        };

        await handler.TryHandleAsync(
            context,
            new ValidationException("test.validation_failed", errors),
            CancellationToken.None);

        // ValidationProblemDetails больше не используется — его Errors
        // типизирован как IDictionary<string, string[]> и объекты в себя не
        // примет. Ответ строится обычным ProblemDetails с расширением errors.
        var problemDetails = Assert.IsType<ProblemDetails>(problemDetailsService.LastContext!.ProblemDetails, exactMatch: true);
        Assert.Equal(400, problemDetails.Status);
        Assert.Equal("test.validation_failed", problemDetails.Extensions["errorCode"]);

        var fieldErrors = Assert.IsType<Dictionary<string, FieldErrorResponse[]>>(problemDetails.Extensions["errors"]);
        var fieldError = Assert.Single(fieldErrors["field"]);
        Assert.Equal("test.field_required", fieldError.Code);
        Assert.Equal("Обязательное поле.", fieldError.Detail);
        Assert.Null(fieldError.Params);
    }

    /// <summary>Params несут сырые значения плюс подсказку типа, без форматирования по локали.</summary>
    [Fact]
    public async Task TryHandleAsync_ExceptionWithParams_BuildsRawValueAndTypeHint()
    {
        var problemDetailsService = new FakeProblemDetailsService();
        var handler = new BusinessExceptionHandler(
            new CapturingLogger<BusinessExceptionHandler>(), problemDetailsService, ErrorCatalogTestData.Catalog);
        var context = new DefaultHttpContext();

        await handler.TryHandleAsync(
            context,
            new ConflictException("test.conflict_with_params", new ErrorParams
            {
                ["used"] = ErrorParam.Number(50m),
                ["limit"] = ErrorParam.Number(50m),
            }),
            CancellationToken.None);

        var problemDetails = problemDetailsService.LastContext!.ProblemDetails;
        var parameters = Assert.IsType<Dictionary<string, ErrorParamResponse>>(problemDetails.Extensions["params"]);

        Assert.Equal(50m, parameters["used"].Value);
        Assert.Equal("number", parameters["used"].Type);
        Assert.Equal("Занято 50 из 50.", problemDetails.Detail);
    }

    /// <summary>Код без параметров — ключ "params" отсутствует целиком, а не пуст.</summary>
    [Fact]
    public async Task TryHandleAsync_ExceptionWithoutParams_DoesNotAddParamsExtension()
    {
        var problemDetailsService = new FakeProblemDetailsService();
        var handler = new BusinessExceptionHandler(
            new CapturingLogger<BusinessExceptionHandler>(), problemDetailsService, ErrorCatalogTestData.Catalog);
        var context = new DefaultHttpContext();

        await handler.TryHandleAsync(context, new NotFoundException("test.not_found"), CancellationToken.None);

        Assert.False(problemDetailsService.LastContext!.ProblemDetails.Extensions.ContainsKey("params"));
    }

    /// <summary>
    /// Detail побуквенно совпадает с русской записью каталога для
    /// этого кода — единственный источник текста.
    /// </summary>
    [Fact]
    public async Task TryHandleAsync_Exception_DetailMatchesCatalogRussianEntry()
    {
        var problemDetailsService = new FakeProblemDetailsService();
        var handler = new BusinessExceptionHandler(
            new CapturingLogger<BusinessExceptionHandler>(), problemDetailsService, ErrorCatalogTestData.Catalog);
        var context = new DefaultHttpContext();

        await handler.TryHandleAsync(context, new NotFoundException("test.not_found"), CancellationToken.None);

        Assert.Equal("Тестовая ошибка: не найдено.", problemDetailsService.LastContext!.ProblemDetails.Detail);
    }

    /// <summary>
    /// Ни detail, ни params не содержат текста innerException — он
    /// уходит только в лог (см. TryHandleAsync_MustaskExceptionWithCause_...), не в ответ.
    /// </summary>
    [Fact]
    public async Task TryHandleAsync_ExceptionWithCause_DoesNotLeakInnerExceptionIntoResponse()
    {
        var problemDetailsService = new FakeProblemDetailsService();
        var handler = new BusinessExceptionHandler(
            new CapturingLogger<BusinessExceptionHandler>(), problemDetailsService, ErrorCatalogTestData.Catalog);
        var context = new DefaultHttpContext();
        var cause = new InvalidOperationException("SELECT секретный_путь FROM внутренняя_таблица");

        await handler.TryHandleAsync(
            context,
            new ConflictException("test.conflict", innerException: cause),
            CancellationToken.None);

        var problemDetails = problemDetailsService.LastContext!.ProblemDetails;
        Assert.DoesNotContain("секретный_путь", problemDetails.Detail, StringComparison.Ordinal);

        var serialized = JsonSerializer.Serialize(problemDetails.Extensions);
        Assert.DoesNotContain("секретный_путь", serialized, StringComparison.Ordinal);
    }

    /// <summary>
    /// Раньше <c>TryHandleAsync</c> возвращал
    /// наружу результат <c>TryWriteAsync</c> — если писатель не смог
    /// записать тело (например, не согласовал content type с <c>Accept</c>
    /// клиента), обработчик возвращал <c>false</c>, и
    /// <c>ExceptionHandlerMiddleware</c> передавал исключение дальше в
    /// <c>GlobalExceptionHandler</c>, который перетирал уже выставленный
    /// статус на 500. Статус, выставленный этим обработчиком, должен
    /// сохраняться независимо от результата записи.
    /// </summary>
    [Fact]
    public async Task TryHandleAsync_ProblemDetailsWriterCannotWrite_StillReportsHandledAndKeepsStatus()
    {
        var problemDetailsService = new FakeProblemDetailsService { WriteResult = false };
        var handler = new BusinessExceptionHandler(
            new CapturingLogger<BusinessExceptionHandler>(), problemDetailsService, ErrorCatalogTestData.Catalog);
        var context = new DefaultHttpContext();

        var handled = await handler.TryHandleAsync(
            context,
            new NotFoundException("test.not_found"),
            CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(404, context.Response.StatusCode);
    }

    /// <summary>
    /// То же самое, но против настоящего <see cref="IProblemDetailsService"/>
    /// (не подставного) — воспроизводит тот же дефект без Docker и без полного хоста:
    /// <c>DefaultProblemDetailsWriter.CanWrite</c> отдаёт <c>false</c>, если
    /// в запросе есть <c>Accept</c>, ни одно значение которого не
    /// совместимо с <c>application/json</c>/<c>application/problem+json</c>.
    /// </summary>
    [Theory]
    [InlineData("application/xml")]
    [InlineData("text/plain")]
    public async Task TryHandleAsync_RealProblemDetailsServiceWithNonJsonAccept_KeepsOriginalStatus(string acceptHeader)
    {
        await using var services = new ServiceCollection()
            .AddLogging()
            .AddProblemDetails()
            .BuildServiceProvider();

        var problemDetailsService = services.GetRequiredService<IProblemDetailsService>();
        var handler = new BusinessExceptionHandler(
            new CapturingLogger<BusinessExceptionHandler>(), problemDetailsService, ErrorCatalogTestData.Catalog);
        var context = new DefaultHttpContext
        {
            RequestServices = services,
            Response = { Body = new MemoryStream() },
        };
        context.Request.Headers.Accept = acceptHeader;

        var handled = await handler.TryHandleAsync(
            context,
            new NotFoundException("test.not_found"),
            CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(404, context.Response.StatusCode);
    }
}
