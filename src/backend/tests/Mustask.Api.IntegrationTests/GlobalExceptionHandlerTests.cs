using Mustask.Api.ErrorHandling;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Mustask.Api.IntegrationTests;

/// <summary>
/// Unit-уровневые тесты над <see cref="GlobalExceptionHandler"/>: уровень
/// логирования (Error, с объектом исключения — со stack trace, иначе
/// непредвиденные ошибки не алертят) и состав ProblemDetails по средам
/// проверяются здесь напрямую, без поднятия хоста и Docker, на настоящем
/// продуктовом каталоге (<see cref="ErrorCatalogTestData.RealProductCatalog"/>).
/// Сквозная доставка записи до реального <c>ILoggerProvider</c> проверяется
/// отдельно, в
/// <c>ErrorHandlingTests.CancelEndpoint_...ThroughRealLoggerProvider</c>
/// (<c>writeToProviders: true</c> в Program.cs).
/// </summary>
[Trait("Category", "Integration")]
public sealed class GlobalExceptionHandlerTests
{
    [Fact]
    public async Task TryHandleAsync_AnyException_LogsErrorWithExceptionObjectAndStackTrace()
    {
        var logger = new CapturingLogger<GlobalExceptionHandler>();
        var environment = new FakeHostEnvironment { EnvironmentName = Environments.Production };
        var handler = new GlobalExceptionHandler(
            logger, environment, new FakeProblemDetailsService(), ErrorCatalogTestData.RealProductCatalog);
        var context = new DefaultHttpContext();
        var exception = new InvalidOperationException("непредвиденная ошибка");

        var handled = await handler.TryHandleAsync(context, exception, CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(500, context.Response.StatusCode);
        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.Same(exception, entry.Exception);
    }

    [Fact]
    public async Task TryHandleAsync_Production_DoesNotIncludeExceptionExtensionOrOriginalMessage()
    {
        var problemDetailsService = new FakeProblemDetailsService();
        var environment = new FakeHostEnvironment { EnvironmentName = Environments.Production };
        var handler = new GlobalExceptionHandler(
            new CapturingLogger<GlobalExceptionHandler>(), environment, problemDetailsService, ErrorCatalogTestData.RealProductCatalog);
        var context = new DefaultHttpContext();

        await handler.TryHandleAsync(context, new InvalidOperationException("Секретная деталь"), CancellationToken.None);

        var problemDetails = problemDetailsService.LastContext!.ProblemDetails;
        Assert.False(problemDetails.Extensions.ContainsKey("exception"));
        Assert.DoesNotContain("Секретная деталь", problemDetails.Detail);
    }

    [Fact]
    public async Task TryHandleAsync_Development_IncludesExceptionExtension()
    {
        var problemDetailsService = new FakeProblemDetailsService();
        var environment = new FakeHostEnvironment { EnvironmentName = Environments.Development };
        var handler = new GlobalExceptionHandler(
            new CapturingLogger<GlobalExceptionHandler>(), environment, problemDetailsService, ErrorCatalogTestData.RealProductCatalog);
        var context = new DefaultHttpContext();

        await handler.TryHandleAsync(context, new InvalidOperationException("Секретная деталь"), CancellationToken.None);

        Assert.True(problemDetailsService.LastContext!.ProblemDetails.Extensions.ContainsKey("exception"));
    }

    /// <summary>500 несёт непустой errorCode, взятый из каталога, а не захардкоженный в этом файле.</summary>
    [Fact]
    public async Task TryHandleAsync_AnyException_SetsInternalErrorCodeFromCatalog()
    {
        var problemDetailsService = new FakeProblemDetailsService();
        var environment = new FakeHostEnvironment { EnvironmentName = Environments.Production };
        var handler = new GlobalExceptionHandler(
            new CapturingLogger<GlobalExceptionHandler>(), environment, problemDetailsService, ErrorCatalogTestData.RealProductCatalog);
        var context = new DefaultHttpContext();

        await handler.TryHandleAsync(context, new InvalidOperationException(), CancellationToken.None);

        var problemDetails = problemDetailsService.LastContext!.ProblemDetails;
        Assert.Equal("http.internal_error", problemDetails.Extensions["errorCode"]);
    }

    /// <summary>Поле detail остаётся и у 500, теперь взят из каталога, а не из захардкоженной строки.</summary>
    [Fact]
    public async Task TryHandleAsync_AnyException_DetailComesFromCatalogAndIsNotEmpty()
    {
        var problemDetailsService = new FakeProblemDetailsService();
        var environment = new FakeHostEnvironment { EnvironmentName = Environments.Production };
        var handler = new GlobalExceptionHandler(
            new CapturingLogger<GlobalExceptionHandler>(), environment, problemDetailsService, ErrorCatalogTestData.RealProductCatalog);
        var context = new DefaultHttpContext();

        await handler.TryHandleAsync(context, new InvalidOperationException(), CancellationToken.None);

        var expectedDetail = ErrorCatalogTestData.RealProductCatalog.Get("http.internal_error").Ru;
        Assert.Equal(expectedDetail, problemDetailsService.LastContext!.ProblemDetails.Detail);
    }
}
