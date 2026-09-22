using Mustask.Api.ApiExplorer;
using Mustask.Api.CurrentUser;
using Mustask.Api.ErrorHandling;
using Mustask.Application.Abstractions;
using Mustask.Api.Authentication;
using Mustask.Api.Authentication.Readiness;
using Mustask.Api.Authorization;
using Mustask.Api.Features.Dictionaries;
using Mustask.Api.Features.Session;
using Gridify;
using Mustask.Application;
using Mustask.Infrastructure;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Scalar.AspNetCore;
using Serilog;
using Serilog.Events;

const string DevelopmentCorsPolicy = "DevelopmentFrontend";

// ADR-0015: Gridify — единый механизм пагинации/сортировки/фильтрации
// списочных чтений. Глобальная конфигурация — процессная (static), поэтому
// выставляется один раз до построения хоста:
// - EF-слой совместимости обязателен: без него выражения фильтра строятся
//   в форме, которую EF Core транслирует хуже (без параметризации);
// - DefaultPageSize приводится к нашему умолчанию (10 строк — плотные
//   реестры, дефолт Gridify — 20); потолок pageSize (100) Gridify не
//   ограничивает — его держит валидация слайса (400, не тихое усечение).
GridifyGlobalConfiguration.EnableEntityFrameworkCompatibilityLayer();
GridifyGlobalConfiguration.DefaultPageSize = 10;

var builder = WebApplication.CreateBuilder(args);

// WebApplication.CreateBuilder registers a
// stock set of ILoggerProviders (Console/Debug/EventSource/EventLog) before
// this line runs. writeToProviders: true below deliberately keeps "other"
// providers alive for OpenTelemetry's sake (see its comment) — but that
// includes these stock ones too, and Microsoft.Extensions.Logging.Console's
// own ConsoleLoggerProvider writes DIRECTLY to Console.Out, independently of
// and racing with Serilog's own configured Console sink. Both then render
// the SAME event to the SAME stream in two different, incompatible shapes
// (Serilog's configured formatter vs. the framework's default "info:
// Category[Id]\n      message" template with Serilog's own default,
// quoted-string rendering embedded as the message) — confirmed by dumping
// app.Services.GetServices<ILoggerProvider>() against a real host before
// this fix landed, and by two integration tests (LoggingTests/
// ProductionLoggingTests) that intermittently captured the wrong of the two
// lines. Clearing here, BEFORE AddServiceDefaults() adds its own
// OpenTelemetry provider below, removes only the stock/redundant ones —
// OpenTelemetry's provider (the one writeToProviders actually needs to keep
// working) is unaffected because it isn't registered yet.
builder.Logging.ClearProviders();

builder.AddServiceDefaults();

// writeToProviders: true — иначе Serilog вытесняет остальные ILoggerProvider
// из DI, в т.ч. OpenTelemetry-провайдер из ServiceDefaults (Aspire dashboard
// перестал бы получать логи; см. ClearProviders() выше — единственный
// оставшийся "другой" провайдер после него именно OpenTelemetry-шный).
// Sinks/enrichers/уровни целиком приходят из конфигурации (секция
// "Serilog" в appsettings*.json) — здесь нет ни одного захардкоженного
// WriteTo/Enrich, чтобы прод (JSON-консоль) и Development (читаемая
// консоль) не требовали правки кода. Без секции "Serilog" в конфигурации
// ReadFrom.Configuration() не добавил бы ни одного sink'а — она обязана
// существовать в appsettings.json.
builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services),
    writeToProviders: true);

builder.AddInfrastructure();
builder.Services.AddApplication();

builder.Services.AddOpenApi();

// CORS — только для локальной разработки: в Aspire фронтенд (Vite dev-сервер)
// и Api живут на разных origin'ах (разных портах на localhost), поэтому
// браузер блокирует cross-origin fetch без явного разрешения. Origins берутся
// из конфигурации (Cors:AllowedOrigins), не хардкодятся — см.
// appsettings.Development.json и src/backend/Mustask.AppHost/AppHost.cs (пробрасывает
// реальный dev-порт фронта через Cors__AllowedOrigins__0). В production CORS
// не включается вовсе: это задача инфраструктуры развёртывания.
if (builder.Environment.IsDevelopment())
{
    // TrimEnd('/') — Aspire передаёт origin фронта через
    // Cors__AllowedOrigins__0 как EndpointReference.ToString(), которая может
    // содержать завершающий слеш; браузерный заголовок Origin его никогда не
    // содержит, так что без нормализации WithOrigins() не совпал бы (см.
    // AppHost.cs).
    var allowedOrigins = builder.Configuration
        .GetSection("Cors:AllowedOrigins")
        .Get<string[]>()
        ?.Select(origin => origin.TrimEnd('/'))
        .ToArray() ?? [];

    builder.Services.AddCors(options => options.AddPolicy(
        DevelopmentCorsPolicy,
        policy => policy
            .WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials()));
    // ADR-0022: .AllowCredentials() вернулся — эта строка
    // раньше несла комментарий "снят" (шаг сноса старого cookie/anti-CSRF
    // контура), и с тех пор он не совпадал с кодом:
    // credentials здесь снова нужны, но новой сессии, не старой. Ordinary
    // resource-server-запросы (Authorization: Bearer) в credentials не
    // нуждаются, но fetch(..., { credentials: 'include' }) на
    // POST /api/session/token и /logout — кросс-origin для целей CORS
    // (localhost:3000 → localhost:5000, порт входит в origin), хотя один
    // сайт для целей SameSite («один сайт» и «не нужен
    // CORS-credentials» не одно и то же утверждение). Список origin'ов не
    // расширяется этой правкой.
}

builder.Services.AddMustaskErrorHandling();
// Регистрирует и аутентификацию (JwtBearer), и авторизацию (политики по
// правам + deny-all fallback) — см. Authentication/JwtBearerSetup.cs.
builder.Services.AddMustaskAuthentication();

// Current-user resolution for feature slices that record an author:
// reads name/preferred_username off the authenticated Keycloak
// principal — see HttpContextCurrentUser for the exact fallback order and
// the anonymous constant.
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpContextCurrentUser>();

// ADR-0018: Mustask.Api как resource server realm'а Keycloak. Значения
// читаются один раз из builder.Configuration до Build() — единственный
// механизм отказа старта при незаданном ключе.
var keycloak = KeycloakOptions.FromConfiguration(builder.Configuration, builder.Environment);
builder.Services.AddSingleton(keycloak);

// Именованный HttpClient для discovery/JWKS realm'а — общая
// точка для JwtBearer и гейта готовности, поэтому регистрируется как
// singleton IConfigurationManager<OpenIdConnectConfiguration>, а не создаётся
// заново в каждом потребителе.
builder.Services.AddHttpClient(RealmMetadata.HttpClientName);
builder.Services.AddSingleton<IConfigurationManager<OpenIdConnectConfiguration>>(sp =>
    RealmMetadata.Create(keycloak, sp.GetRequiredService<IHttpClientFactory>()));

builder.Services.AddSingleton<RealmReadiness>();
// AddHostedService (не AddSingleton<IHostedLifecycleService, …>) требует
// именно двух строк: AddHostedService<T>() сам T не регистрирует, а тестам
// нужно резолвить RealmReadinessMonitor напрямую и звать CheckAsync() без
// ожидания цикла.
builder.Services.AddSingleton<RealmReadinessMonitor>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<RealmReadinessMonitor>());

// ADR-0022: сессия панели — Mustask.Api сам становится OIDC-клиентом
// mustask-panel. AddMustaskSessionAuthentication дозаписывает две схемы (cookie +
// OIDC) поверх уже настроенной AddMustaskAuthentication() выше через
// services.AddAuthentication() без аргумента, не трогая DefaultScheme
// (Bearer) — см. SessionAuthenticationSetup. keycloak —
// тот же экземпляр KeycloakOptions, что уже используется для JwtBearer и
// discovery/JWKS выше — не второй разбор конфигурации.
var sessionOptions = SessionOidcOptions.FromConfiguration(builder.Configuration, builder.Environment);
builder.Services.AddMustaskSessionAuthentication(keycloak, sessionOptions);

// keycloak-realm — без тега "live", поэтому виден /health, но не
// /alive (Mustask.ServiceDefaults/Extensions.cs, Predicate = live).
builder.Services.AddHealthChecks().AddCheck<RealmReadinessHealthCheck>(RealmReadinessHealthCheck.Name);

var app = builder.Build();

// Found running the real test suite against Docker:
// Serilog.AspNetCore's UseSerilogRequestLogging middleware, when
// options.Logger isn't set explicitly, uses the AMBIENT STATIC
// Serilog.Log.Logger fresh on EVERY request rather than a properly
// host-scoped logger from DI — and UseSerilog(...) above reassigns that
// SAME static property on every host Build() (preserveStaticLogger isn't
// passed). A single production process only ever builds one host, so this
// never mattered there — but the integration test suite builds many
// WebApplicationFactory-backed hosts within ONE process
// (WithWebHostBuilder clones for Production/Staging scenarios across many
// test classes), and whichever host was built — or even already disposed —
// MOST RECENTLY anywhere in the run silently became every OTHER host's
// request logger too. A disposed Serilog logger discards writes entirely,
// which is exactly why this surfaced as "no console output at all",
// reliably in isolation (few hosts built) but intermittently in the full
// suite (many). Capturing Log.Logger right here — this host's own,
// correctly-configured value at the exact moment its own Build() call just
// finished — and pinning it via options.Logger below makes this host's
// request logging immune to any later host build/dispose elsewhere in the
// process; behavior for a single-host process (production) is unchanged,
// since that's the same value the ambient fallback would have used anyway.
var requestLoggingLogger = Log.Logger;

// UseSerilogRequestLogging стоит СНАРУЖИ обработчика исключений — иначе оно
// видит исключение до того, как UseMustaskErrorHandling превратит его в
// ProblemDetails, и пишет каждую ожидаемую бизнес-ошибку как
// "responded 500" уровня Error со стеком. Снаружи оно видит уже
// сконвертированный ответ: 404 бизнес-ошибки логируется как Information,
// непредвиденная ошибка остаётся 500 и Error.
//
// Вне Development /health и /alive отвечают на
// каждую пробу инфраструктуры (обычно раз в несколько секунд) — без этого
// GetLevel каждая проба писала бы полноценное Information-событие в прод.
// Те же два пути уже исключены из OpenTelemetry-трейсов
// (Mustask.ServiceDefaults/Extensions.cs, ConfigureOpenTelemetry) — здесь то же
// решение для логов, тем же источником путей. Verbose ниже дефолтного
// Serilog:MinimumLevel:Default (Information) — событие просто не долетает
// до sink'а, но GetLevel всё равно вызывается на каждый запрос, поэтому
// остальные пути (в т.ч. ошибочные — see ниже) продолжают логироваться как
// раньше: копия дефолтной логики Serilog.AspNetCore.RequestLoggingOptions.GetLevel,
// с единственным добавленным условием сверху.
app.UseSerilogRequestLogging(options =>
{
    options.Logger = requestLoggingLogger;
    options.GetLevel = (httpContext, _, exception) =>
    {
        // Глушится только успешная проба: 401/403/404 от /health — признак
        // регрессии (сняли AllowAnonymous, увели маршрут), она должна быть
        // видна в логах.
        if (exception is null
            && httpContext.Response.StatusCode < 400
            && (httpContext.Request.Path.StartsWithSegments(Microsoft.Extensions.Hosting.Extensions.HealthEndpointPath)
                || httpContext.Request.Path.StartsWithSegments(Microsoft.Extensions.Hosting.Extensions.AlivenessEndpointPath)))
        {
            return LogEventLevel.Verbose;
        }

        return exception is not null || httpContext.Response.StatusCode > 499
            ? LogEventLevel.Error
            : LogEventLevel.Information;
    };
});

// Перехватывает исключения раньше всего, что может бросить.
// UseDeveloperExceptionPage() сюда сознательно не возвращается: он
// перехватывал бы раньше наших handler'ов и отдавал HTML вместо
// problem+json, а MustaskApiFactory поднимает хост в Development, так что
// интеграционные тесты проверяли бы не тот путь, что работает в проде.
// Диагностика в dev не теряется — её даёт расширение exception в
// GlobalExceptionHandler.
app.UseMustaskErrorHandling();

app.MapDefaultEndpoints();

// CORS остаётся только под средой: флаг explorer'а её не трогает.
if (app.Environment.IsDevelopment())
{
    app.UseCors(DevelopmentCorsPolicy);
}

// Explorer (Scalar) + маршрут OpenAPI-документа, оба под флагом
// ApiExplorer:Enabled — логика в
// Mustask.Api/ApiExplorer/ApiExplorerExtensions.cs. Вне блока
// IsDevelopment намеренно: флаг может включать explorer и в
// Production/Staging. Маршруты помечены .AllowAnonymous() внутри
// UseMustaskApiExplorer() — deny-all fallback ниже иначе закрыл бы их и для
// Development (см. её комментарий про допуск инвентаризационным тестом).
// Порядок относительно UseUnmatchedRouteAsNotFound/UseAuthentication/
// UseAuthorization не имеет значения: это только Map-вызовы (регистрация
// endpoint'ов в EndpointDataSource), не Use-middleware — реальную позицию в
// конвейере запросов задаёт неявный UseRouting перед первым Use-middleware,
// которому нужны endpoint'ы (см. UnmatchedRouteMiddleware).
app.UseMustaskApiExplorer();

// Endpoint не найден вовсе (неизвестный маршрут или
// известный маршрут с неверным методом) — 404/405 без обращения к
// аутентификации/авторизации. Deny-all fallback иначе перехватывает и
// эти случаи тоже, превращая их в 401/403 (см. UnmatchedRouteMiddleware).
// Этот middleware терминален для незарегистрированных
// маршрутов — короткозамыкает запрос сам, не вызывая next() дальше по
// цепочке (см. UnmatchedRouteMiddleware.UseUnmatchedRouteAsNotFound). Любая
// прослойка, которая должна отвечать на "неизвестный" маршрут (например,
// UseStaticFiles или SPA-fallback, если они когда-нибудь понадобятся),
// обязана быть зарегистрирована ВЫШЕ этого вызова — иначе она молча
// перестанет получать запросы.
// Обязательный порядок дальше: аутентификация (кто ты) → авторизация (что
// тебе можно, включая deny-all fallback) → маппинг endpoint'ов.
app.UseUnmatchedRouteAsNotFound();
// 503 до аутентификации, пока realm не готов —
// клиент не должен путать «сервис не готов» с «сессия истекла» (401).
app.UseRealmReadinessGate();
app.UseAuthentication();
app.UseAuthorization();

app.MapDictionaryEndpoints();
app.MapSessionEndpoints();

app.Run();

// Exposed for WebApplicationFactory<Program> in integration tests.
public partial class Program;
