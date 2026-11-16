using Mustask.Api.Features.Iterations;
using Mustask.Api.ApiExplorer;
using Mustask.Api.CurrentUser;
using Mustask.Api.ErrorHandling;
using Mustask.Application.Abstractions;
using Mustask.Application.Abstractions.Organizations;
using Mustask.Api.Authentication;
using Mustask.Api.Authentication.Readiness;
using Mustask.Api.Authorization;
using Mustask.Api.Features.Boards;
using Mustask.Api.Features.Dictionaries;
using Mustask.Api.Features.Organizations;
using Mustask.Api.Features.Process;
using Mustask.Api.Features.Session;
using Mustask.Api.Features.WorkItems;
using Mustask.Api.Hosting;
using Gridify;
using Mustask.Application;
using Mustask.Domain.UserDirectory;
using Mustask.Infrastructure;
using Microsoft.AspNetCore.Authorization;
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
    writeToProviders: true,
    // tech-006: the host logger lives and dies with the host. Without this Serilog assigns it to the static
    // Log.Logger on Build() and runs Log.CloseAndFlush() on dispose, so disposing any WebApplicationFactory
    // child host silenced every ILogger<T> created later on a sibling host.
    preserveStaticLogger: true);

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
            .AllowCredentials()
            .WithExposedHeaders("Retry-After")));
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

// mod-002 D-p: an endpoint that names its own forbidden code (WithForbiddenErrorCode)
// answers it instead of the platform's http.forbidden; every other endpoint, and
// every challenge, keeps the framework's behaviour.
builder.Services.AddSingleton<IAuthorizationMiddlewareResultHandler, ForbiddenErrorCodeResultHandler>();

// Current-user resolution for feature slices that record an author:
// reads name/preferred_username off the authenticated Keycloak
// principal — see HttpContextCurrentUser for the exact fallback order and
// the anonymous constant.
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpContextCurrentUser>();

// mod-016 (ADR-0027): the role the scoped check resolved for this request, for the handlers that need it (the served access,
// the second code of a parent write). Asking on a route without the check throws.
builder.Services.AddScoped<IOrganizationAccess, HttpContextOrganizationAccess>();

// The read limiter of the aggregate views (feat-048 OQ-2): one per API process, on the clock the host runs on.
builder.Services.AddSingleton<AggregateReadLimiter>();
builder.Services.AddScoped<AggregateReadLimitFilter>();

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
// tech-025 (ADR-0034): the session endpoints record the signed-in person through a port of their own; the user directory
// implements it. Scoped: it uses the request's DbContext through the Application handler.
builder.Services.AddScoped<Mustask.Api.Authentication.ISessionSignInRecorder, Mustask.Api.Features.UserDirectory.SessionUserProjection>();

// The read model of the user projection orders by the ICU 'ru' collation, so an invariant-globalization runtime is
// refused here, before anything runs (mod-014, ADR-0025 § 9). The projection itself is written at sign-in (tech-025,
// ADR-0034): there is no push channel and no shared secret.
DirectoryCollation.EnsureSupported();

// keycloak-realm — без тега "live", поэтому виден /health, но не
// /alive (Mustask.ServiceDefaults/Extensions.cs, Predicate = live).
builder.Services.AddHealthChecks().AddCheck<RealmReadinessHealthCheck>(RealmReadinessHealthCheck.Name);

var app = builder.Build();

// tech-006: the request-log line is written through this host's own Serilog logger, taken from DI.
// UseSerilog(..., preserveStaticLogger: true) above registers it there and never touches the static
// Serilog.Log.Logger, so another host's build or dispose cannot change where this host logs.
var requestLoggingLogger = app.Services.GetRequiredService<Serilog.ILogger>();

// ADR-0037: served under Hosting:PathBase (e.g. /mustask) when it is set; empty is the root and adds nothing.
// First in the pipeline, so every later middleware, the router and the OIDC handler see Request.PathBase. The proxy
// passes the path unchanged; a request without the prefix (/health from the VM itself) is left as it is.
if (sessionOptions.PathBase.Length > 0)
{
    app.UsePathBase(sessionOptions.PathBase);
    app.UsePathBaseOnRelativeLocation();
}

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
app.MapOrganizationEndpoints();
app.MapRescueEndpoints();
app.MapMembershipEndpoints();
app.MapGrantEndpoints();
app.MapProcessEndpoints();
app.MapSessionEndpoints();
app.MapWorkItemEndpoints();
app.MapBoardEndpoints();
app.MapIterationsEndpoints();
app.MapAggregateViewEndpoints();

app.Run();

// Exposed for WebApplicationFactory<Program> in integration tests.
public partial class Program;
