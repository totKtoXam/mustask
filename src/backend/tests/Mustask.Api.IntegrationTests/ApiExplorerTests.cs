using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Mustask.Api.ApiExplorer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Mustask.Api.IntegrationTests;

/// <summary>
/// Explorer (Scalar) поверх OpenAPI-документа, включаемый флагом
/// конфигурации <c>ApiExplorer:Enabled</c>. Маршруты: explorer —
/// <c>/scalar</c>, документ — <c>/openapi/v1.json</c> (документ по умолчанию
/// <c>v1</c>).
///
/// Каждый сценарий поднимает отдельный <see cref="WebApplicationFactory{Program}"/>
/// через <see cref="MustaskApiFactory.WithWebHostBuilder"/> — переиспользует общий
/// Testcontainers-Postgres из <see cref="MustaskApiCollection"/> (см.
/// комментарий <see cref="MustaskApiFactory"/>), второй контейнер не поднимается.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class ApiExplorerTests
{
    // Не дублировать литералы — единственный источник
    // истины для ключа конфигурации и пути explorer'а живёт в
    // ApiExplorerExtensions (Mustask.Api.ApiExplorer).
    private const string ConfigKey = ApiExplorerExtensions.ConfigKey;
    private const string ExplorerRoute = ApiExplorerExtensions.RoutePath;
    private const string OpenApiDocumentRoute = "/openapi/v1.json";

    private readonly MustaskApiFactory _factory;

    public ApiExplorerTests(MustaskApiFactory factory)
    {
        _factory = factory;
    }

    private WebApplicationFactory<Program> CreateFactory(string environment, string? explorerFlag) =>
        _factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment(environment);
            if (explorerFlag is not null)
            {
                builder.UseSetting(ConfigKey, explorerFlag);
            }
        });

    // Сценарий "среда, отличная от Development, без флага" (fail-closed по
    // умолчанию).
    [Fact]
    public async Task GetExplorerAndDocument_ProductionWithoutFlag_BothReturnNotFound()
    {
        using var factory = CreateFactory("Production", explorerFlag: null);
        var client = factory.CreateClient();

        var explorerResponse = await client.GetAsync(ExplorerRoute);
        var documentResponse = await client.GetAsync(OpenApiDocumentRoute);

        Assert.Equal(HttpStatusCode.NotFound, explorerResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, documentResponse.StatusCode);
    }

    // Сценарий "флаг выключен явно".
    [Fact]
    public async Task GetExplorerAndDocument_StagingFlagExplicitlyDisabled_BothReturnNotFound()
    {
        using var factory = CreateFactory("Staging", explorerFlag: "false");
        var client = factory.CreateClient();

        var explorerResponse = await client.GetAsync(ExplorerRoute);
        var documentResponse = await client.GetAsync(OpenApiDocumentRoute);

        Assert.Equal(HttpStatusCode.NotFound, explorerResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, documentResponse.StatusCode);
    }

    // Заданный флаг сильнее среды в обе стороны — false закрывает
    // поверхность даже в Development (сценарий "флаг выключен в Development").
    [Fact]
    public async Task GetExplorerAndDocument_DevelopmentFlagExplicitlyDisabled_BothReturnNotFound()
    {
        using var factory = CreateFactory("Development", explorerFlag: "false");
        var client = factory.CreateClient();

        var explorerResponse = await client.GetAsync(ExplorerRoute);
        var documentResponse = await client.GetAsync(OpenApiDocumentRoute);

        Assert.Equal(HttpStatusCode.NotFound, explorerResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, documentResponse.StatusCode);
    }

    // Сценарий "некорректное значение флага трактуется как «выключено»":
    // старт успешен, 404, Warning в логе. Перехват
    // Console.Out — по образцу LoggingTests.
    [Theory]
    [InlineData("yes")]
    [InlineData("")]
    public async Task GetExplorer_StagingFlagUnrecognizedValue_StartsSuccessfullyReturnsNotFoundAndLogsWarning(
        string unrecognizedValue)
    {
        var originalOut = Console.Out;
        var capturedOutput = new StringWriter();
        Console.SetOut(capturedOutput);

        HttpResponseMessage explorerResponse;
        HttpResponseMessage documentResponse;
        try
        {
            using var factory = CreateFactory("Staging", explorerFlag: unrecognizedValue);
            var client = factory.CreateClient();

            // Старт завершается успешно: сам факт, что запрос дошёл до
            // ответа (а не исключение при построении хоста), уже доказывает
            // это — bool.TryParse не бросает на нераспознанной строке.
            explorerResponse = await client.GetAsync(ExplorerRoute);
            // Маршрут документа выключается вместе с explorer'ом и на этом
            // (не только на явном "false") пути тоже — изначально
            // проверялся только маршрут explorer'а.
            documentResponse = await client.GetAsync(OpenApiDocumentRoute);
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        Assert.Equal(HttpStatusCode.NotFound, explorerResponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, documentResponse.StatusCode);

        var output = capturedOutput.ToString();
        Assert.Contains(ConfigKey, output);
        // Уровень и текст
        // проверялись двумя независимыми Contains по всему буферу — тест
        // прошёл бы и в случае, когда наше сообщение ушло на Information, а
        // Warning пришёл от постороннего компонента. AssertWarningLogged
        // (см. её комментарий) даёт ту же гарантию на структурном JSON, а не
        // на "level: Category\n  message" — эта среда (Staging) вне
        // Development, поэтому консоль пишет CompactJsonFormatter.
        AssertWarningLogged(output, "not a recognized boolean");
    }

    // Production + включённый флаг = 200 и Warning при старте (сценарий
    // "флаг включён в Production").
    [Fact]
    public async Task GetExplorer_ProductionFlagEnabled_ReturnsOkAndLogsWarningAboutBeingOutsideDevelopment()
    {
        var originalOut = Console.Out;
        var capturedOutput = new StringWriter();
        Console.SetOut(capturedOutput);

        HttpResponseMessage explorerResponse;
        try
        {
            using var factory = CreateFactory("Production", explorerFlag: "true");
            var client = factory.CreateClient();

            explorerResponse = await client.GetAsync(ExplorerRoute);
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        Assert.Equal(HttpStatusCode.OK, explorerResponse.StatusCode);

        // Та же связка уровня и текста, что и в тесте на
        // нераспознанное значение флага — см. комментарий там и у
        // AssertWarningLogged.
        AssertWarningLogged(capturedOutput.ToString(), "outside the Development environment");
    }

    // Сценарий "включение флагом вне Development": 200 + Warning
    // (та же механика, что и Production — Staging тоже "вне Development").
    [Fact]
    public async Task GetExplorer_StagingFlagEnabled_ReturnsOkAndLogsWarning()
    {
        var originalOut = Console.Out;
        var capturedOutput = new StringWriter();
        Console.SetOut(capturedOutput);

        HttpResponseMessage explorerResponse;
        try
        {
            using var factory = CreateFactory("Staging", explorerFlag: "true");
            var client = factory.CreateClient();

            explorerResponse = await client.GetAsync(ExplorerRoute);
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        Assert.Equal(HttpStatusCode.OK, explorerResponse.StatusCode);
        // Голый Contains("outside the Development
        // environment") по всему буферу проходил бы даже с чужим уровнем;
        // AssertWarningLogged связывает уровень (Warning) с текстом в одной
        // JSON-записи, как в парном Production-тесте выше.
        AssertWarningLogged(capturedOutput.ToString(), "outside the Development environment");
    }

    // Development без флага — включён по умолчанию; Information-строка с
    // полным адресом explorer'а.
    // Адрес логируется из сконфигурированного urls (WebHostDefaults.ServerUrlsKey,
    // см. комментарий в Program.cs) — под TestServer он не задан по
    // умолчанию, поэтому здесь он выставляется явно через UseSetting, как и
    // остальные настройки хоста в этом файле (без второго Testcontainers).
    [Fact]
    public async Task GetExplorer_DevelopmentWithoutFlagAndConfiguredUrl_ReturnsOkAndLogsInformationWithFullAddress()
    {
        const string ConfiguredUrl = "http://localhost:12345";

        var originalOut = Console.Out;
        var capturedOutput = new StringWriter();
        Console.SetOut(capturedOutput);

        HttpResponseMessage explorerResponse;
        try
        {
            using var factory = _factory.WithWebHostBuilder(builder =>
                builder.UseSetting(WebHostDefaults.ServerUrlsKey, ConfiguredUrl));
            var client = factory.CreateClient();

            explorerResponse = await client.GetAsync(ExplorerRoute);
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        Assert.Equal(HttpStatusCode.OK, explorerResponse.StatusCode);
        Assert.Contains($"{ConfiguredUrl}{ExplorerRoute}", capturedOutput.ToString());
    }

    // «Содержимое ответа — HTML-страница explorer'а» — статус 200 сам по себе этого не доказывает (страница
    // ошибки в этом пайплайне тоже могла бы вернуть 200 с problem+json,
    // если бы кто-то перепутал маршрут). Проверяем и Content-Type, и что
    // тело действительно похоже на HTML-документ Scalar.
    [Fact]
    public async Task GetExplorer_Development_ReturnsHtmlPage()
    {
        using var factory = CreateFactory("Development", explorerFlag: null);
        var client = factory.CreateClient();

        var response = await client.GetAsync(ExplorerRoute);
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("<!doctype html", body, StringComparison.OrdinalIgnoreCase);

        // Страница обязана ссылаться именно на тот документ, который отдаёт это
        // же приложение: Scalar встраивает в свою конфигурацию URL, который
        // затем запрашивает. Без этой проверки страница откроется и при неверном
        // имени документа — explorer покажет ошибку загрузки, а тест на 200
        // останется зелёным.
        Assert.Contains("openapi/v1.json", body, StringComparison.Ordinal);

        var document = await client.GetAsync(OpenApiDocumentRoute);
        Assert.Equal(HttpStatusCode.OK, document.StatusCode);
    }

    // «Набор операций OpenAPI-документа (пары метод+путь) совпадает с
    // Development на той же сборке». Тест на маршруты ниже сравнивает только
    // маршруты (EndpointDataSource), а не содержимое
    // самого документа — это разные вещи: маршрут документа мог бы
    // существовать и отдавать 200, но с урезанным/неверным набором операций
    // (например, из-за отдельного OpenAPI-документа для не-Development сред).
    // Тест ловит регрессию, когда набор операций разойдётся между средами.
    [Fact]
    public async Task OpenApiDocument_StagingEnabledVsDevelopment_HasSameOperations()
    {
        using var developmentFactory = CreateFactory("Development", explorerFlag: null);
        using var stagingFactory = CreateFactory("Staging", explorerFlag: "true");

        var developmentOperations = await GetOperationsAsync(developmentFactory);
        var stagingOperations = await GetOperationsAsync(stagingFactory);

        Assert.Equal(developmentOperations, stagingOperations);
    }

    private async Task<HashSet<string>> GetOperationsAsync(WebApplicationFactory<Program> factory)
    {
        var client = factory.CreateClient();
        var document = await client.GetFromJsonAsync<JsonObject>(OpenApiDocumentRoute);

        // Сравнение двух пустых множеств (нет бизнес-
        // endpoint'ов) само по себе тавтологично — без этой проверки 200 с
        // произвольным посторонним JSON тоже давал бы "одинаковые операции".
        Assert.NotNull(document?["openapi"]);

        var paths = document?["paths"]?.AsObject() ?? [];

        var operations = new HashSet<string>();
        foreach (var (path, methodsNode) in paths)
        {
            if (methodsNode is not JsonObject methods)
            {
                continue;
            }

            foreach (var (method, _) in methods)
            {
                operations.Add($"{method.ToUpperInvariant()} {path}");
            }
        }

        return operations;
    }

    /// <summary>
    /// These tests used to assert against the "level: Category[Id]
    /// \n      message" shape written by the default
    /// Microsoft.Extensions.Logging Console provider — which, it turned out,
    /// was never Serilog's own output at all. That provider stayed alive
    /// only because Program.cs's <c>writeToProviders: true</c> (needed for
    /// OpenTelemetry) also kept the framework's stock Console/Debug/
    /// EventSource/EventLog providers registered, and the stock console one
    /// happened to write to the exact same <see cref="Console.Out"/> stream
    /// Serilog's own sink writes to — duplicating every event in two
    /// different, unrelated shapes (this was found via
    /// <c>app.Services.GetServices&lt;ILoggerProvider&gt;()</c> against a real
    /// host, which listed both). Program.cs now clears the stock
    /// providers before OpenTelemetry's own gets added, so Serilog's
    /// configured sink is the only writer left — outside Development, that's
    /// structured JSON (CompactJsonFormatter), not text.
    /// </summary>
    /// <remarks>
    /// Finds a JSON-formatted line whose level is Warning (compact format:
    /// <c>"@l":"Warning"</c> — omitted only for the default Information
    /// level, so its presence here is exactly the level check) and whose
    /// message template's own literal text contains
    /// <paramref name="expectedMessageSubstring"/> (<c>"@mt"</c> carries the
    /// raw template, unsubstituted placeholders aside — literal words in the
    /// original message survive verbatim, which is all these assertions
    /// ever checked). Tying level and text to the SAME record is the same
    /// guarantee the original two-part regex gave — a stray Warning from an unrelated component still cannot
    /// satisfy this on its own.
    /// </remarks>
    private static void AssertWarningLogged(string output, string expectedMessageSubstring)
    {
        var found = output
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line =>
            {
                try
                {
                    // "as JsonObject", не ".AsObject()":
                    // AsObject() бросает InvalidOperationException, а не
                    // JsonException, на валидном JSON, который не объект
                    // (строка "42" или голая строка в кавычках) — такая
                    // посторонняя строка в консольном буфере уронила бы
                    // помощник вместо того, чтобы просто не совпасть.
                    return JsonNode.Parse(line) as JsonObject;
                }
                catch (JsonException)
                {
                    return null;
                }
            })
            .Any(json => json is not null
                && (string?)json["@l"] == "Warning"
                && ((string?)json["@mt"])?.Contains(expectedMessageSubstring, StringComparison.Ordinal) == true);

        Assert.True(
            found,
            $"No Warning-level JSON log line contained \"{expectedMessageSubstring}\" — full output:\n{output}");
    }

    // Разница между набором маршрутов с включённым и с выключенным
    // флагом (в одной и той же среде) целиком укладывается в закрытый
    // список — маршрут документа и то, что регистрирует MapScalarApiReference()
    // (сама страница + статика: scalar.js, scalar.aspnetcore.js, favicon.svg).
    //
    // Ключ diff'а — пара «HTTP-метод + шаблон», а не
    // только RawText. Второй handler на уже существующем шаблоне (другой
    // HTTP-метод) схлопывался бы в HashSet строк-паттернов и не попадал бы в
    // diff. Список закрытый — сравнение на точное равенство (Assert.Equal),
    // а не подмножество + NotEmpty: последнее удовлетворялось бы одним
    // маршрутом из пяти.
    [Fact]
    public async Task EndpointRoutes_EnabledVsDisabledInSameEnvironment_OnlyDifferByExplorerAndDocumentRoutes()
    {
        var closedList = new HashSet<string>
        {
            "GET /openapi/{documentName}.json",
            "GET /scalar/{documentName?}",
            "GET /scalar/scalar.js",
            "GET /scalar/scalar.aspnetcore.js",
            "GET /scalar/favicon.svg",
        };

        using var enabledFactory = CreateFactory("Development", explorerFlag: "true");
        using var disabledFactory = CreateFactory("Development", explorerFlag: "false");

        // Гарантируем, что оба хоста реально построены (endpoint data source
        // заполняется во время построения хоста).
        enabledFactory.CreateClient();
        disabledFactory.CreateClient();

        var enabledRoutes = GetRouteKeys(enabledFactory);
        var disabledRoutes = GetRouteKeys(disabledFactory);

        var diff = enabledRoutes.Except(disabledRoutes).ToHashSet();

        Assert.True(disabledRoutes.IsSubsetOf(enabledRoutes));
        Assert.Equal(closedList, diff);
    }

    /// <summary>
    /// Ключ — «HTTP-метод(ы) endpoint'а» + пробел + <c>RoutePattern.RawText</c>.
    /// Endpoint без <see cref="HttpMethodMetadata"/> (отвечает на любой метод)
    /// помечается методом <c>ANY</c>, а не молча пропускается.
    /// </summary>
    private static HashSet<string> GetRouteKeys(WebApplicationFactory<Program> factory)
    {
        using var scope = factory.Services.CreateScope();
        var dataSources = scope.ServiceProvider.GetServices<EndpointDataSource>();

        return dataSources
            .SelectMany(dataSource => dataSource.Endpoints)
            .OfType<RouteEndpoint>()
            .SelectMany(endpoint =>
            {
                var pattern = endpoint.RoutePattern.RawText ?? string.Empty;
                var methods = endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods;

                return methods is { Count: > 0 }
                    ? methods.Select(method => $"{method} {pattern}")
                    : [$"ANY {pattern}"];
            })
            .ToHashSet();
    }

    // Флаг не меняет поведение остального API. Контрольный набор —
    // /__test/throw/{kind} и /__test/cancel (TestEndpointsStartupFilter,
    // подмешивается в любой среде), а не /health и /alive: те мапятся во
    // всех средах (ServiceDefaults.Extensions.MapDefaultEndpoints)
    // и не зависят от флага explorer'а, но и не касаются его вовсе — тест
    // здесь про explorer, а не про здоровье, поэтому нейтральные тестовые
    // маршруты остаются более прямым контролем.
    [Theory]
    [InlineData("/__test/throw/notfound")]
    [InlineData("/__test/throw/unexpected")]
    [InlineData("/this-route-does-not-exist-either")]
    public async Task OtherRoutes_EnabledVsDisabledInSameEnvironment_RespondIdentically(string path)
    {
        using var enabledFactory = CreateFactory("Staging", explorerFlag: "true");
        using var disabledFactory = CreateFactory("Staging", explorerFlag: "false");

        var enabledResponse = await enabledFactory.CreateClient().GetAsync(path);
        var enabledBody = await NormalizeBodyAsync(enabledResponse);

        var disabledResponse = await disabledFactory.CreateClient().GetAsync(path);
        var disabledBody = await NormalizeBodyAsync(disabledResponse);

        Assert.Equal(disabledResponse.StatusCode, enabledResponse.StatusCode);
        Assert.Equal(disabledResponse.Content.Headers.ContentType, enabledResponse.Content.Headers.ContentType);
        Assert.Equal(disabledBody, enabledBody);
    }

    /// <summary>
    /// ProblemDetails.Extensions["traceId"] (ErrorHandlingExtensions) — новый
    /// на каждый запрос (Activity.Current?.Id), поэтому дословное сравнение
    /// тел падало бы независимо от флага explorer'а/от того, что маршруты
    /// разные; вырезаем его всегда. <c>instance</c> вырезается только по
    /// требованию вызывающей стороны (<paramref name="removeInstance"/>) —
    /// это путь запроса (RFC 9457), легитимно совпадающий для одного и того
    /// же пути на двух хостах (<see cref="OtherRoutes_EnabledVsDisabledInSameEnvironment_RespondIdentically"/>),
    /// но законно различающийся между двумя разными путями
    /// (<see cref="GetExplorer_Disabled_IsIndistinguishableFromNonExistentRoute"/>).
    /// </summary>
    private static async Task<string> NormalizeBodyAsync(HttpResponseMessage response, bool removeInstance = false)
    {
        var body = await response.Content.ReadAsStringAsync();
        if (response.Content.Headers.ContentType?.MediaType != "application/problem+json")
        {
            return body;
        }

        var node = JsonNode.Parse(body)!.AsObject();
        node.Remove("traceId");
        if (removeInstance)
        {
            node.Remove("instance");
        }

        return node.ToJsonString();
    }

    // Выключенная поверхность неотличима от несуществующего пути —
    // тот же статус, Content-Type и тело (application/problem+json, см.
    // ErrorHandlingExtensions.UseMustaskErrorHandling → UseStatusCodePages()).
    //
    // Требуется совпадение тела целиком, а не только
    // errorCode/title — расхождение по detail/type/status/method осталось бы
    // незамеченным. Сравниваем нормализованное тело целиком; instance
    // (путь запроса, RFC 9457) вырезан — он у двух разных путей легитимно
    // различается независимо от explorer'а.
    [Fact]
    public async Task GetExplorer_Disabled_IsIndistinguishableFromNonExistentRoute()
    {
        using var factory = CreateFactory("Production", explorerFlag: null);
        var client = factory.CreateClient();

        var explorerResponse = await client.GetAsync(ExplorerRoute);
        var explorerBody = await NormalizeBodyAsync(explorerResponse, removeInstance: true);

        var randomResponse = await client.GetAsync("/some-other-nonexistent-path");
        var randomBody = await NormalizeBodyAsync(randomResponse, removeInstance: true);

        Assert.Equal(randomResponse.StatusCode, explorerResponse.StatusCode);
        Assert.Equal(randomResponse.Content.Headers.ContentType, explorerResponse.Content.Headers.ContentType);
        Assert.Equal(randomBody, explorerBody);
    }
}
