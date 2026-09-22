using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Mustask.Api.ErrorHandling;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Mustask.Api.IntegrationTests;

/// <summary>
/// Проверяет единый механизм обработки ошибок (ADR-0004) через тестовые
/// endpoint'ы <c>/__test/throw/{kind}</c> и маршрут, которого не существует.
/// Требует Docker (Testcontainers поднимает реальный Postgres).
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class ErrorHandlingTests
{
    private readonly MustaskApiFactory _factory;

    public ErrorHandlingTests(MustaskApiFactory factory)
    {
        _factory = factory;
    }

    public static TheoryData<string, HttpStatusCode, string> MustaskExceptionKinds => new()
    {
        { "notfound", HttpStatusCode.NotFound, "test.not_found" },
        { "conflict", HttpStatusCode.Conflict, "test.conflict" },
        { "forbidden", HttpStatusCode.Forbidden, "test.forbidden" },
        { "unauthorized", HttpStatusCode.Unauthorized, "test.unauthorized" },
        { "validation", HttpStatusCode.BadRequest, "test.validation_failed" },
    };

    [Theory]
    [MemberData(nameof(MustaskExceptionKinds))]
    public async Task ThrowEndpoint_MustaskExceptionKind_ReturnsProblemJsonWithErrorCodeAndTraceId(
        string kind, HttpStatusCode expectedStatus, string expectedErrorCode)
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync($"/__test/throw/{kind}");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(expectedStatus, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(expectedErrorCode, body.GetProperty("errorCode").GetString());
        Assert.False(string.IsNullOrEmpty(body.GetProperty("traceId").GetString()));
    }

    /// <summary>
    /// <c>instance</c> — URI reference (RFC 9457), не
    /// строка "МЕТОД путь" с пробелом; метод запроса — отдельное поле.
    /// </summary>
    [Fact]
    public async Task ThrowEndpoint_MustaskException_InstanceIsPathAndMethodIsSeparateExtension()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/__test/throw/notfound");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal("/__test/throw/notfound", body.GetProperty("instance").GetString());
        Assert.Equal("GET", body.GetProperty("method").GetString());
    }

    /// <summary>
    /// Причина ошибки поля — код реестра плюс свой detail из
    /// каталога (тот же контракт, что и у ошибки верхнего уровня), а не
    /// готовый текст напрямую в значении.
    /// </summary>
    [Fact]
    public async Task ThrowEndpoint_Validation_Returns400WithErrorsAsCodeAndDetailObjects()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/__test/throw/validation");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("test.validation_failed", body.GetProperty("errorCode").GetString());

        var fieldError = body.GetProperty("errors").GetProperty("field")[0];

        Assert.Equal("test.field_required", fieldError.GetProperty("code").GetString());
        Assert.Equal("Обязательное поле.", fieldError.GetProperty("detail").GetString());
        Assert.False(fieldError.TryGetProperty("params", out _));
    }

    /// <summary>Params несут сырые значения плюс подсказку типа, без форматирования по локали.</summary>
    [Fact]
    public async Task ThrowEndpoint_ConflictWithParams_ReturnsRawValuesAndTypeHints()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/__test/throw/conflict_with_params");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("test.conflict_with_params", body.GetProperty("errorCode").GetString());

        var used = body.GetProperty("params").GetProperty("used");
        Assert.Equal(50, used.GetProperty("value").GetInt32());
        Assert.Equal("number", used.GetProperty("type").GetString());

        // Detail побуквенно совпадает с русской записью каталога —
        // подстановка выполнена по тем же правилам, что и у "value" выше.
        Assert.Equal("Занято 50 из 50.", body.GetProperty("detail").GetString());
    }

    /// <summary>
    /// Params — сырые значения плюс подсказка типа:
    /// тип "date" обязан уйти клиенту как JSON-строка "YYYY-MM-DD", без
    /// форматирования по локали (не "12 января 2026 г."). До этого теста
    /// формат даты в реальном HTTP-ответе не проверялся ни одним тестом —
    /// только рендер текста в detail и конструирование ErrorParam отдельно.
    /// </summary>
    [Fact]
    public async Task ThrowEndpoint_DateParam_SerializesAsIsoDateString()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/__test/throw/date_param");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        var date = body.GetProperty("params").GetProperty("date");
        Assert.Equal("2026-01-12", date.GetProperty("value").GetString());
        Assert.Equal("date", date.GetProperty("type").GetString());
        Assert.Equal("Дата: 12.01.2026.", body.GetProperty("detail").GetString());
    }

    /// <summary>Код без параметров — ключ "params" отсутствует, ответ остаётся валидным.</summary>
    [Fact]
    public async Task ThrowEndpoint_NotFound_DoesNotHaveParamsKey()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/__test/throw/notfound");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.False(body.TryGetProperty("params", out _));
        Assert.False(string.IsNullOrEmpty(body.GetProperty("detail").GetString()));
    }

    /// <summary>500 несёт непустой errorCode.</summary>
    [Fact]
    public async Task ThrowEndpoint_Unexpected_Returns500AndDoesNotLeakOriginalExceptionMessage()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/__test/throw/unexpected");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("http.internal_error", body.GetProperty("errorCode").GetString());

        var detail = body.GetProperty("detail").GetString();

        Assert.False(string.IsNullOrEmpty(detail));
        Assert.DoesNotContain("Секретная деталь", detail);
    }

    [Fact]
    public async Task ThrowEndpoint_UnexpectedInDevelopment_IncludesExceptionExtension()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/__test/throw/unexpected");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.True(body.TryGetProperty("exception", out var exception));
        Assert.Contains("Секретная деталь", exception.GetProperty("message").GetString());
    }

    [Fact]
    public async Task ThrowEndpoint_UnexpectedInProduction_DoesNotIncludeExceptionExtension()
    {
        // WithWebHostBuilder переиспользует контейнер общей фабрики и не
        // трогает process-wide ConnectionStrings__mustask — поднимать вторую
        // MustaskApiFactory внутри коллекции нельзя: её DisposeAsync обнуляет
        // переменную, и следующий построенный хост остаётся без строки
        // подключения.
        var client = _factory
            .WithWebHostBuilder(builder => builder.UseEnvironment("Production"))
            .CreateClient();

        var response = await client.GetAsync("/__test/throw/unexpected");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.False(body.TryGetProperty("exception", out _));
    }

    /// <summary>
    /// Регрессия: бизнес-код, у которого нет записи в каталоге (опечатка,
    /// рассинхрон с константой, код без <c>[ErrorCodes]</c> — enforcement
    /// проверяется отдельно, но путь ошибки обязан остаться устойчивым, а не
    /// рухнуть непредсказуемо).
    /// До фикса <c>BusinessExceptionHandler</c> звал <c>ErrorCatalog.Get</c>
    /// и бросал <c>KeyNotFoundException</c> прямо из
    /// <c>IExceptionHandler.TryHandleAsync</c>; теперь он проверяет каталог
    /// до любого побочного эффекта и отдаёт исключение дальше по цепочке.
    /// Тест фиксирует наблюдаемый клиентом результат: ответ с телом
    /// problem+json и непустым errorCode, а не голый сбой без тела.
    /// </summary>
    [Fact]
    public async Task ThrowEndpoint_CodeMissingFromCatalog_ProductionReturnsProblemJsonInsteadOfCrashing()
    {
        var prodClient = _factory
            .WithWebHostBuilder(builder => builder.UseEnvironment("Production"))
            .CreateClient();

        // Ответ строит GlobalExceptionHandler: BusinessExceptionHandler
        // вернул false, ничего не записав в httpContext, поэтому статус и
        // тело принадлежат обезличенному 500 (http.internal_error). До
        // фикса исходное исключение долетало до клиента необработанным
        // — у внешнего интегратора не было HTTP-ответа вовсе.
        var response = await prodClient.GetAsync("/__test/throw/unknown_code");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.True(
            body.TryGetProperty("errorCode", out var errorCode) && !string.IsNullOrEmpty(errorCode.GetString()),
            $"Ответ на код без записи в каталоге не содержит errorCode: {body}");
        Assert.False(body.TryGetProperty("exception", out _));
    }

    [Fact]
    public async Task UnknownRoute_Returns404ProblemJson()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/this-route-does-not-exist");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        // UseStatusCodePages() не знает про errorCode и
        // отдаёт англоязычный Title по умолчанию — CustomizeProblemDetails
        // должен проставить оба поля так же, как это делают три
        // IExceptionHandler'а для бизнес-ошибок.
        Assert.Equal("http.not_found", body.GetProperty("errorCode").GetString());
        Assert.Equal("Ресурс не найден.", body.GetProperty("title").GetString());
    }

    [Fact]
    public async Task KnownRouteWrongMethod_Returns405ProblemJson()
    {
        var client = _factory.CreateClient();

        // /__test/throw/{kind} замаплен через MapGet (только GET) —
        // PUT на тот же маршрут должен упасть в 405 через встроенный
        // HttpMethodMatcherPolicy, а UseStatusCodePages() превратить его в
        // problem+json, а не пустое тело. (/health сюда не годится: MapHealthChecks
        // не ограничивает метод.)
        var response = await client.PutAsync("/__test/throw/notfound", content: null);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("http.method_not_allowed", body.GetProperty("errorCode").GetString());
        Assert.Equal("Метод не поддерживается для этого маршрута.", body.GetProperty("title").GetString());
    }

    /// <summary>
    /// Ловит саму регрессию, которую исправляет
    /// <c>UnmatchedRouteMiddleware</c> — deny-all fallback-политика не должна распространяться на запросы, для которых
    /// маршрутизация вообще не нашла endpoint. Без фикса анонимный запрос на
    /// опечатанный/удалённый URL получает 401 вместо 404, что в SPA уводит
    /// клиента в продление сессии и разлогинивает пользователя вместо того,
    /// чтобы просто показать «страница не найдена».
    /// </summary>
    [Fact]
    public async Task UnknownRoute_WithoutToken_Returns404NotUnauthorized()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/this-route-does-not-exist");

        // Assert.NotEqual(Unauthorized/
        // Forbidden, ...) сразу после Assert.Equal(NotFound, ...) не может
        // упасть, если предыдущая ассерция прошла, — убраны как тавтология,
        // не несущая дополнительного сигнала (обе альтернативы уже
        // исключены точным равенством).
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>
    /// Проверяет дыру «по существу, а не по симптому» — не только для анонима, но и для
    /// аутентифицированного пользователя с ДЕЙСТВУЮЩИМ токеном.
    /// <c>UnmatchedRouteMiddleware</c> короткозамыкает запрос ДО
    /// <c>UseAuthentication()</c>/<c>UseAuthorization()</c>, поэтому валидный
    /// Bearer-токен не должен ничего менять: 404, а не 403 (Forbid из-за
    /// deny-all) и не 401.
    /// </summary>
    [Fact]
    public async Task UnknownRoute_WithValidAccessToken_Returns404NotForbidden()
    {
        var client = _factory.CreateClient();
        // Токен из KeycloakTestRealm — собственного маршрута логина у API нет.
        var accessToken = KeycloakTestRealm.CreateToken();

        var response = await client.SendAsync(
            new HttpRequestMessage(HttpMethod.Get, "/this-route-does-not-exist").WithBearer(accessToken));

        // См. комментарий в UnknownRoute_WithoutToken_Returns404NotUnauthorized.
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>
    /// Та же дыра, зеркально: ПРОТУХШИЙ токен на несуществующем маршруте тоже
    /// не должен доходить до аутентификации — иначе 401 от истёкшего токена
    /// маскировал бы факт, что маршрута вообще не существует.
    /// </summary>
    [Fact]
    public async Task UnknownRoute_WithExpiredAccessToken_Returns404NotUnauthorized()
    {
        var client = _factory.CreateClient();
        // KeycloakTestRealm.CreateToken(configure: ...) вместо
        // переподписи собственным Jwt__SigningKey__PrivateKeyPem, которого в
        // окружении больше нет.
        var expiredToken = KeycloakTestRealm.CreateToken(
            configure: t => t.Expires = DateTimeOffset.UtcNow.AddMinutes(-5));

        var response = await client.SendAsync(
            new HttpRequestMessage(HttpMethod.Get, "/this-route-does-not-exist").WithBearer(expiredToken));

        // См. комментарий в UnknownRoute_WithoutToken_Returns404NotUnauthorized.
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>
    /// Известный маршрут, неверный метод, но с валидным токеном — тоже 405,
    /// а не 401/403 (deny-all по-прежнему не должен видеть этот запрос).
    /// </summary>
    [Fact]
    public async Task KnownRouteWrongMethod_WithValidAccessToken_Returns405NotForbidden()
    {
        var client = _factory.CreateClient();
        var accessToken = KeycloakTestRealm.CreateToken();

        var response = await client.SendAsync(
            new HttpRequestMessage(HttpMethod.Put, "/__test/throw/notfound").WithBearer(accessToken));

        Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>
    /// Title назначается по статусу, а не по коду — две разные
    /// бизнес-ошибки, отображаемые в один статус 409, получают одинаковый
    /// Title и разные errorCode.
    /// </summary>
    [Fact]
    public async Task TwoBusinessErrorsOnSameStatus_ShareTitleButDifferInErrorCode()
    {
        var client = _factory.CreateClient();

        var first = await client.GetAsync("/__test/throw/conflict");
        var second = await client.GetAsync("/__test/throw/conflict_with_params");
        var firstBody = await first.Content.ReadFromJsonAsync<JsonElement>();
        var secondBody = await second.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.Conflict, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal(firstBody.GetProperty("title").GetString(), secondBody.GetProperty("title").GetString());
        Assert.NotEqual(
            firstBody.GetProperty("errorCode").GetString(),
            secondBody.GetProperty("errorCode").GetString());
    }

    /// <summary>
    /// Код-«последняя надежда» (<c>http.request_failed</c>) назначается
    /// статусам, у которых тело есть, а собственной записи в каталоге нет —
    /// здесь 415, платформенным кодом не покрытый ни одной записью
    /// contracts/errors/http.json.
    /// </summary>
    [Fact]
    public async Task UnmappedStatus_UsesRequestFailedFallbackCode()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/__test/status/415");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal((HttpStatusCode)415, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("http.request_failed", body.GetProperty("errorCode").GetString());
        Assert.False(string.IsNullOrEmpty(body.GetProperty("detail").GetString()));
        Assert.False(string.IsNullOrEmpty(body.GetProperty("title").GetString()));
    }

    /// <summary>
    /// Штатный <c>DefaultProblemDetailsWriter</c>
    /// не пишет тело, если ни одно значение <c>Accept</c> не совместимо с
    /// <c>application/json</c>/<c>application/problem+json</c> — раньше
    /// <c>BusinessExceptionHandler</c> отдавал этот отказ наружу, и
    /// <c>ExceptionHandlerMiddleware</c> передавал исключение дальше в
    /// <c>GlobalExceptionHandler</c>, который перетирал уже выставленный 404
    /// на 500. Внешний интегратор, честно проставляющий <c>Accept</c>, не
    /// должен получать 500 вместо статуса, заданного бизнес-ошибкой.
    /// </summary>
    [Theory]
    [InlineData("application/xml")]
    [InlineData("text/plain")]
    public async Task ThrowEndpoint_NonJsonAccept_PreservesOriginalStatusInsteadOf500(string acceptHeader)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Accept.Clear();
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(acceptHeader));

        var response = await client.GetAsync("/__test/throw/notfound");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>
    /// Проверяет <c>/__test/cancel</c> и заодно регрессию: без
    /// <c>writeToProviders: true</c> (Program.cs) событие
    /// <see cref="OperationCanceledExceptionHandler"/> дошло бы только до
    /// подставного логгера в юнит-тестах, а не до реального
    /// <see cref="ILoggerProvider"/>, подключённого через DI — здесь провайдер настоящий, подмешан в полный хост.
    /// Эндпоинт заворачивает отмену в постороннее исключение намеренно: сам
    /// ASP.NET Core перехватывает голый <see cref="OperationCanceledException"/>
    /// раньше зарегистрированных <c>IExceptionHandler</c>'ов и сам
    /// выставляет 499, минуя весь наш обработчик целиком (см.
    /// <c>TestEndpointsStartupFilter</c>) — только
    /// обёрнутый случай реально доходит до
    /// <see cref="OperationCanceledExceptionHandler"/> через настоящий
    /// HTTP-конвейер.
    /// </summary>
    [Fact]
    public async Task CancelEndpoint_ClientCancelsRequest_ServerLogsInformationThroughRealLoggerProvider()
    {
        var capturingProvider = new CapturingLoggerProvider();

        await using var loggingFactory = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddSingleton<ILoggerProvider>(capturingProvider)));

        var client = loggingFactory.CreateClient();
        using var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(200));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => client.GetAsync("/__test/cancel", cts.Token));

        var category = typeof(OperationCanceledExceptionHandler).FullName;
        var deadline = DateTime.UtcNow.AddSeconds(10);

        while (DateTime.UtcNow < deadline && !HasExpectedEntry())
        {
            await Task.Delay(50);
        }

        Assert.True(
            HasExpectedEntry(),
            "Ожидалась запись Information от OperationCanceledExceptionHandler в реальном ILoggerProvider. Captured: "
                + string.Join(" | ", capturingProvider.Entries.Select(e => $"{e.Category}/{e.Level}: {e.Message}")));

        bool HasExpectedEntry() => capturingProvider.Entries.Any(entry =>
            entry.Category == category && entry.Level == LogLevel.Information);
    }

    /// <summary>
    /// Закрепляет порядок middleware: UseSerilogRequestLogging должно стоять
    /// СНАРУЖИ UseMustaskErrorHandling. Внутри оно видит исключение до конвертации
    /// и пишет ожидаемую бизнес-ошибку как "responded 500" уровня Error со
    /// стеком — вторая цель фичи («ожидаемые ошибки не алертят») при этом не
    /// работает ни в одной среде.
    /// </summary>
    [Fact]
    public async Task ThrowEndpoint_BusinessError_RequestLoggingDoesNotReportItAsServerError()
    {
        var capturingProvider = new CapturingLoggerProvider();

        await using var loggingFactory = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services => services.AddSingleton<ILoggerProvider>(capturingProvider)));

        var response = await loggingFactory.CreateClient().GetAsync("/__test/throw/notfound");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var requestLogEntries = capturingProvider.Entries
            .Where(entry => entry.Category.StartsWith("Serilog.AspNetCore", StringComparison.Ordinal))
            .ToList();

        Assert.NotEmpty(requestLogEntries);
        Assert.DoesNotContain(requestLogEntries, entry => entry.Level >= LogLevel.Error);
    }
}
