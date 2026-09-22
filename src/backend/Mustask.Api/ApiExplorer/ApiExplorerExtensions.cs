using Scalar.AspNetCore;

namespace Mustask.Api.ApiExplorer;

/// <summary>
/// Интерактивный explorer (Scalar) поверх уже существующего
/// OpenAPI-документа. Форма — по образцу <c>Mustask.Api/ErrorHandling</c>
/// (<c>ErrorHandlingExtensions</c>):
/// один <c>Use...</c>-метод, вызываемый из <c>Program.cs</c>.
/// Своего <c>Add...</c>-метода нет — <c>AddOpenApi()</c> существовал до
/// explorer'а и регистрирует генерацию документа безусловно, независимо от
/// флага explorer'а; он остаётся в <c>Program.cs</c>.
/// </summary>
public static class ApiExplorerExtensions
{
    // Ключ конфигурации. Заданное значение сильнее среды в
    // обе стороны. `true` открывает explorer и OpenAPI-документ в любой
    // среде, включая Production (с Warning при старте); `false` закрывает их
    // и в Development. Не задан — решает среда (открыт только в
    // Development). Не пересекается с `/api/` (docs/conventions/backend.md, § API).
    public const string ConfigKey = "ApiExplorer:Enabled";

    // Единственный источник истины для пути explorer'а —
    // умолчание пакета MapScalarApiReference() было четвёртым, неявным
    // источником (Program.cs, AGENTS.md, тесты дублировали литерал "/scalar").
    // Значение совпадает с умолчанием пакета, но задаётся явно — перегрузкой
    // с endpointPrefix — чтобы обновление пакета не могло молча его сменить.
    public const string RoutePath = "/scalar";

    /// <summary>
    /// Флаг читается из <c>app.Configuration</c> ПОСЛЕ <c>Build()</c> —
    /// иначе его нельзя переопределить из <c>WebApplicationFactory</c>
    /// (<c>UseSetting</c> / <c>ConfigureAppConfiguration</c>), см. комментарий
    /// <c>MustaskApiFactory</c> про <c>ConnectionStrings__mustask</c> — та же ловушка
    /// с top-level-операторами до <c>Build()</c>.
    /// </summary>
    public static WebApplication UseMustaskApiExplorer(this WebApplication app)
    {
        if (!ResolveApiExplorerEnabled(app.Configuration, app.Environment, app.Logger))
        {
            return app;
        }

        // Маршрут explorer'а и маршрут OpenAPI-документа включаются и
        // выключаются вместе — иначе при выключенном UI наружу продолжает
        // уходить полное описание API.
        //
        // Оба вызова помечены .AllowAnonymous() — deny-all fallback
        // (JwtBearerSetup.AddMustaskAuthentication) иначе запрашивал бы
        // Challenge/Forbid у анонима, превращая дев-инструмент в 401/403
        // всюду, где он включён (в т.ч. Production+флаг).
        // .AllowAnonymous() на builder'е MapScalarApiReference() каскадно
        // распространяется на статические ассеты (scalar.js,
        // scalar.aspnetcore.js, favicon.svg), которые пакет регистрирует
        // отдельными endpoint'ами внутри той же группы (проверено
        // эмпирически: EndpointDataSource после вызова показывает
        // IAllowAnonymous на всех пяти маршрутах, не только на странице) —
        // отдельной разметки на каждый ассет не требуется.
        app.MapOpenApi().AllowAnonymous();
        app.MapScalarApiReference(RoutePath, options => options
            // Безопасность: встроенный бандл Scalar несёт
            // Agent Scalar (AI-чат «Ask AI») и MCP-интеграцию — клиентский
            // канал к сторонним сервисам, не видимый ни одному серверному
            // тесту. Для закрытого контура это недопустимо по
            // умолчанию: поверхность фичи — explorer и
            // документ, не более.
            .DisableAgent()
            .DisableMcp())
            .AllowAnonymous();

        if (!app.Environment.IsDevelopment())
        {
            // Включённая вне Development поверхность обязана быть
            // видна в логе при каждом старте, иначе забытый на стенде флаг
            // живёт годами незамеченным.
            app.Logger.LogWarning(
                "API explorer (Scalar) is enabled outside the Development environment " +
                "(current: {EnvironmentName}). It exposes an anonymous, unauthenticated UI " +
                "for sending requests to this API.",
                app.Environment.EnvironmentName);
        }

        LogExplorerAddress(app);

        return app;
    }

    /// <summary>
    /// Адрес explorer'а виден без чтения кода — Information-строка при
    /// старте. Источник — сконфигурированный urls
    /// (<c>WebHostDefaults.ServerUrlsKey</c>), не
    /// <c>IServerAddressesFeature</c>: тот отражает реально забинженные
    /// Kestrel-адреса и остаётся пустым под <c>TestServer</c>
    /// (<c>WebApplicationFactory</c>), из-за чего сценарий было бы нечем
    /// проверить интеграционным тестом без второго Testcontainers-хоста
    /// поверх настоящего Kestrel.
    /// </summary>
    private static void LogExplorerAddress(WebApplication app)
    {
        var configuredUrl = app.Configuration[WebHostDefaults.ServerUrlsKey]
            ?.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault();

        if (configuredUrl is not null)
        {
            app.Logger.LogInformation(
                "API explorer is available at {ExplorerUrl}",
                $"{configuredUrl.TrimEnd('/')}{RoutePath}");
            return;
        }

        // Опубликованная сборка без ASPNETCORE_URLS/urls (Kestrel слушает
        // дефолтные порты) не задаёт WebHostDefaults.ServerUrlsKey — без этой
        // ветки адрес explorer'а тихо не логировался бы вовсе. Полного адреса
        // здесь нет (он не сконфигурирован, а получен Kestrel'ом во время
        // запуска), поэтому даём относительный путь и подсказку, где искать базу.
        app.Logger.LogInformation(
            "API explorer is available at {ExplorerRoutePath} on this host " +
            "(see the \"Now listening on\" line above for the base address).",
            RoutePath);
    }

    /// <summary>
    /// Заданное значение сильнее среды в обе стороны. Разбор терпимый
    /// (<see cref="bool.TryParse(string?, out bool)"/>), не биндинг — нераспознанное
    /// значение не должно ронять старт приложения (некорректное значение
    /// флага трактуется как «выключено»).
    /// </summary>
    private static bool ResolveApiExplorerEnabled(
        IConfiguration configuration,
        IWebHostEnvironment environment,
        ILogger logger)
    {
        var rawValue = configuration[ConfigKey];

        if (rawValue is null)
        {
            // Флаг не задан — решает среда, доступно только в Development.
            return environment.IsDevelopment();
        }

        if (!bool.TryParse(rawValue, out var enabled))
        {
            // Опечатка в значении не должна тихо выключать
            // explorer на стенде — тот, кто её сделал, должен увидеть Warning.
            logger.LogWarning(
                "Configuration value {ConfigKey}=\"{RawValue}\" is not a recognized boolean; " +
                "the API explorer is treated as disabled.",
                ConfigKey, rawValue);
            return false;
        }

        return enabled;
    }
}
