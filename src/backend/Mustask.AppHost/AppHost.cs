using System.Globalization;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Aspire.Hosting.ApplicationModel;
using Mustask.AppHost.Provisioning;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

var builder = DistributedApplication.CreateBuilder(args);

// The E2E stand file is deleted here — the very first thing this program
// does — not inside the provisioning step below. After a force-kill leaves
// the pinned-name Keycloak container orphaned, there is a ~90 s window where
// DCP silently recreates it, Aspire's own "Distributed application started"
// banner prints, but the fresh container's health check (and therefore
// ResourceReadyEvent, and therefore provisioning) has not fired yet — during
// that whole window the file on disk still held the *previous* run's secret,
// same shape, same issuer (same stand/port), rejected by the new instance.
// Deleting it before any resource is even declared means a suite that reads
// too early sees "no stand file" — one named cause — instead of a secret
// that looks valid and fails silently. `standFilePath` is computed once,
// here, and reused by the provisioning subscriber below so the two can never
// drift apart.
var standFilePath = Path.Combine(
    builder.AppHostDirectory, "..", "..", "..", ".e2e-stand", "keycloak-e2e-client.json");

try
{
    File.Delete(standFilePath);
}
catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
{
    // Best-effort: a locked file is rare (nothing on this stand normally
    // holds it open) and not worth failing the whole stand start over —
    // provisioning below overwrites it anyway once Keycloak is ready.
    Console.Error.WriteLine(
        $"E2E stand: could not delete the stale stand file at {standFilePath} ({ex.GetType().Name}: {ex.Message}). " +
        "It will still be overwritten once Keycloak is ready, but until then it may look valid and not be.");
}

// Порты стенда — из конфигурации. Значения по умолчанию лежат в
// appsettings.json этого проекта, переопределяются как обычная конфигурация:
// `Ports__Api=5100`, `Ports__Frontend=3001`. Числа не вписаны прямо сюда,
// потому что любое столкновение — занятый порт, второй стенд на той же
// машине — иначе требовало бы правки кода: переселить занятый порт Aspire не
// может (эндпоинты непроксируемые, см. ниже), падение при старте — штатный
// исход, а не редкий случай.
int Port(string key)
{
    var raw = builder.Configuration[$"Ports:{key}"];

    if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var port))
    {
        throw new InvalidOperationException(
            $"Порт стенда «Ports:{key}» не задан или не число (значение: «{raw}»). "
            + "Значения по умолчанию — в appsettings.json проекта Mustask.AppHost.");
    }

    return port;
}

// Доступ из локальной сети. Прокси DCP, через который Aspire по умолчанию
// публикует эндпоинты ресурсов, слушает только loopback, и режима «слушать
// на всех интерфейсах» у него нет вовсе (перечисление AddressAllocationMode
// в исходниках Aspire 13.5.1). Единственный работающий способ — отключить
// прокси для эндпоинта: тогда ресурс биндится сам, и TargetHost попадает
// прямо в ASPNETCORE_URLS / публикацию портов Docker. Плата: для
// непроксируемого эндпоинта Aspire не умеет реплики и не может переселить
// занятый порт — порт обязан быть свободен.
const string AnyInterface = "0.0.0.0";

static void ExposeToNetwork(EndpointAnnotation endpoint, int port, int targetPort)
{
    endpoint.IsProxied = false;
    endpoint.TargetHost = AnyInterface;
    endpoint.Port = port;
    endpoint.TargetPort = targetPort;
}

// Один стенд на всё: PostgreSQL, Mustask.Api, Keycloak и фронтенд.
// Пароль PostgreSQL — обязательный секрет-параметр без значения по
// умолчанию. Если пароль генерирует сам Aspire, а `WithDataVolume()` хранит
// кластер между запусками, то стоит генерации разойтись с томом (первый
// запуск не успел записать секрет, user-secrets почистили, том остался от
// прежнего стенда), как стенд запирается намертво: официальный образ читает
// POSTGRES_PASSWORD только при первичной инициализации каталога данных,
// новый пароль молча игнорируется, и всё, что видно, — «password
// authentication failed for user "postgres"» в логе БД. Фиксированный
// параметр эту рассинхронизацию исключает по построению.
var mustaskDbPassword = builder.AddParameter("mustask-db-password", secret: true);

var postgres = builder.AddPostgres("postgres", password: mustaskDbPassword, port: Port("Db"))
    // Имя пользователя оставлено дефолтным (`postgres`) сознательно: health
    // check Aspire подключается строкой без Database, а Npgsql в этом случае
    // берёт имя БД равным имени пользователя — у пользователя `postgres`
    // одноимённая БД есть всегда.
    .WithDataVolume()
    .AddDatabase("mustask");

var apiPort = Port("Api");

var api = builder.AddProject<Projects.Mustask_Api>("api")
    .WithReference(postgres)
    .WaitFor(postgres)
    // Источник порта — `Ports:Api`; в launchSettings Mustask.Api лежит вторая
    // копия того же числа (для запуска без Aspire), её держат в синхроне
    // руками. При отключённом прокси Port и TargetPort обязаны совпадать —
    // публикует ресурс сам себя.
    .WithEndpoint("http", endpoint => ExposeToNetwork(endpoint, apiPort, apiPort))
    // Ссылка «scalar» на карточке api в Aspire dashboard — иначе рабочее
    // место ручной работы с API есть, но о нём не узнать.
    .WithUrlForEndpoint("http", url => new ResourceUrlAnnotation
    {
        Url = "/scalar",
        DisplayText = "Scalar",
    });

// --- Keycloak (ADR-0018) ------------------------------------------------------

// Поставщик аутентификации: realm `mustask` для панели и `Mustask.Api`.
// Realm приезжает импортом из `deploy/keycloak/realms` — конфигурация
// realm'а живёт в репозитории, а не в кликах по консоли, иначе она
// невоспроизводима.
//
// Тома у контейнера намеренно нет: `start-dev` кладёт встроенную H2 в
// /opt/keycloak/data, то есть в записываемый слой контейнера, и вместе с ним
// она и исчезает. Следствие ровно то, что нужно стенду: правки, сделанные
// руками в консоли, живут до пересоздания контейнера, а источником истины
// остаётся импорт. Плата — созданные сидом пользователи стенда тоже
// исчезают, поэтому `scripts/keycloak-seed.sh` написан для повторного
// запуска. Оговорка: `--import-realm` не перезаписывает уже существующий
// realm, так что правку в `deploy/keycloak/realms` подхватит только
// пересоздание контейнера, а не `docker restart`.
var keycloakPort = Port("Keycloak");

// Пароль администратора самого Keycloak — секрет без значения по умолчанию,
// как и `mustask-db-password`: это учётка, из-под которой создаются все
// остальные.
var keycloakAdminPassword = builder.AddParameter("keycloak-admin-password", secret: true);

// ADR-0022 Decision 4: секрет confidential-клиента mustask-panel —
// обязательный секрет-параметр без значения по умолчанию, тем же приёмом,
// что mustask-db-password/keycloak-admin-password. На свежем клоне это
// третий обязательный секрет наравне с двумя перечисленными выше.
var keycloakPanelClientSecret = builder.AddParameter("keycloak-panel-client-secret", secret: true);

var keycloakImage = builder.Configuration["Keycloak:Image"] ?? "quay.io/keycloak/keycloak";
var keycloakTag = builder.Configuration["Keycloak:Tag"] ?? "26.4";

// Адреса клиентов realm-импорт получает переменными окружения, а не хранит
// литералами: порты стенда конфигурируемые, и прибитый в JSON
// `localhost:3000` при `Ports__Frontend=3001` дал бы у панели
// `Invalid parameter: redirect_uri` — отказ на стороне Keycloak, который в
// коде панели не найти. Плейсхолдеры в realm-импорте Keycloak подставляет
// сам, синтаксис `${ПЕРЕМЕННАЯ:значение по умолчанию}` — проверено на
// живом контейнере: подстановка работает, значение по умолчанию работает, а
// переменная без значения и без умолчания роняет старт с внятным
// `Root URL is not a valid URL`, то есть молча не ломается.
var panelUrl = FormattableString.Invariant($"http://localhost:{Port("Frontend")}");
// ADR-0022: адрес callback'а сессии — redirectUris клиента mustask-panel в
// realm-импорте указывает на Mustask.Api, а не на панель (браузер не ходит на
// Keycloak напрямую). apiPort уже посчитан для `api` выше по файлу —
// переиспользуется, не считается заново.
var apiUrl = FormattableString.Invariant($"http://localhost:{apiPort}");

// ADR-0023: имя Docker-контейнера назначается нами, а не достаётся у DCP
// постфактум: у Aspire.Hosting 13.5.1 нет публичного способа прочитать
// сгенерированный контейнер id/имя из обработчика ResourceReadyEvent, а
// WithContainerName даёт то же самое, только раньше и без интроспекции.
// Порт уже является предстендовым разделителем — двух стендов с одинаковым
// Ports:Keycloak на одной машине быть не может, так что имя, построенное на
// его основе, само по себе уникально среди одновременно поднятых стендов (в
// том числе в разных git worktree).
var keycloakContainerName = FormattableString.Invariant($"mustask-e2e-keycloak-{keycloakPort}");

// One value for this whole AppHost process, not one per ResourceReadyEvent
// firing — KeycloakStandClientSecret's own header has the incident.
// Provisioning stays idempotent in the sense that matters — the client
// converges to the same state on every repeat firing within this run — only
// a *fresh AppHost process* gets a fresh secret (generated, never
// human-chosen, never logged).
var mustaskE2eClientSecret = new KeycloakStandClientSecret();

// Ресурс именован переменной — Mustask.Api читает его endpoint напрямую
// (ниже), не через отдельный конфигурационный литерал.
var keycloak = builder.AddContainer("keycloak", keycloakImage, keycloakTag)
    .WithContainerName(keycloakContainerName)
    .WithHttpEndpoint(port: keycloakPort, targetPort: 8080)
    .WithEndpoint("http", endpoint =>
    {
        endpoint.IsProxied = false;
        endpoint.TargetHost = AnyInterface;
    })
    .WithBindMount("../../../deploy/keycloak/realms", "/opt/keycloak/data/import", isReadOnly: true)
    .WithBindMount("../../../deploy/keycloak/themes", "/opt/keycloak/themes", isReadOnly: true)
    .WithArgs("start-dev", "--import-realm")
    .WithEnvironment("KC_BOOTSTRAP_ADMIN_USERNAME", "admin")
    .WithEnvironment("KC_BOOTSTRAP_ADMIN_PASSWORD", keycloakAdminPassword)
    .WithEnvironment("MUSTASK_PANEL_URL", panelUrl)
    .WithEnvironment("MUSTASK_API_URL", apiUrl)
    // Секрет, который realm-импорт присваивает клиенту mustask-panel, и
    // секрет, который Mustask.Api отправляет в обмене кода
    // (Keycloak__ClientSecret ниже), обязаны быть одним и тем же значением —
    // без этого вход не проходит дальше "unauthorized_client".
    .WithEnvironment("MUSTASK_PANEL_CLIENT_SECRET", keycloakPanelClientSecret)
    // Проверяем не корень, а конкретный realm: 200 на `/realms/mustask` доказывает
    // не только то, что сервер поднялся, но и что импорт realm'а состоялся.
    // Штатный health-эндпоинт Keycloak с версии 25 живёт на отдельном
    // management-порту 9000, то есть на 8080 его нет вовсе.
    .WithHttpHealthCheck("/realms/mustask");

// ADR-0023: стендовый клиент с правом имперсонации и E2E-пользователь-образец
// заводятся здесь, а не realm-импортом — --import-realm пропускает уже
// существующий realm, так что вторым файлом клиента в `mustask` не добавить.
// Секрет клиента генерируется заново при каждом старте AppHost — это не тот
// же приём, что MUSTASK_PANEL_CLIENT_SECRET (тот идёт в realm-импорт через
// плейсхолдер), потому что этого клиента в realm-импорте нет вовсе:
// секрет передаётся Keycloak прямо в теле запроса на создание клиента.
// ResourceReadyEvent на ресурсе keycloak срабатывает после того, как его
// собственный health check (выше) подтвердил готовность/импорт — мастер-
// realm с учёткой admin доступен даже раньше этого момента, так что вызывать
// admin-API уже можно.
builder.Eventing.Subscribe<ResourceReadyEvent>(keycloak.Resource, async (@event, cancellationToken) =>
{
    // Grep-able log lines around provisioning: without them the timing gap
    // described at the top of this file is hard to notice. A stand that
    // provisions silently is a stand nobody can debug from the console alone.
    var logger = @event.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Mustask.AppHost.Provisioning");

    logger.LogInformation(
        "E2E stand: provisioning the stand-only client `{ClientId}` and fixture user `{FixtureUsername}`...",
        KeycloakStandIdentifiers.ClientId, KeycloakStandIdentifiers.FixtureUsername);

    try
    {
        var options = new KeycloakStandProvisioningOptions
        {
            AdminUsername = "admin",
            AdminPassword = await keycloakAdminPassword.Resource.GetValueAsync(cancellationToken)
                ?? throw new InvalidOperationException("Parameters:keycloak-admin-password resolved to no value."),
            ClientId = KeycloakStandIdentifiers.ClientId,
            ClientSecret = mustaskE2eClientSecret.Value,
            FixtureUsername = KeycloakStandIdentifiers.FixtureUsername,
            KeycloakContainerName = keycloakContainerName,
            Issuer = FormattableString.Invariant($"http://localhost:{keycloakPort}/realms/mustask"),
            // Тот же файл и тот же путь, что удалён в самом начале этого
            // файла — один литерал, не два.
            StandFilePath = standFilePath,
            // Выставляется учёткой admin, не mustask-e2e — см. комментарий у
            // вызова SetAccessTokenLifespanAsync.
            AccessTokenLifespanSeconds = KeycloakStandIdentifiers.AccessTokenLifespanSeconds,
        };

        using var http = new HttpClient { BaseAddress = new Uri(FormattableString.Invariant($"http://localhost:{keycloakPort}")) };

        await KeycloakStandProvisioner.ProvisionAsync(http, options, cancellationToken);

        logger.LogInformation(
            "E2E stand: provisioning finished — client `{ClientId}`, fixture user `{FixtureUsername}`, stand file written to {StandFilePath}.",
            KeycloakStandIdentifiers.ClientId, KeycloakStandIdentifiers.FixtureUsername, standFilePath);
    }
    catch (Exception ex)
    {
        // Rethrown, not swallowed. Verified live: a deliberately wrong admin
        // credential makes `KeycloakStandProvisioner.ProvisionAsync` throw
        // as expected, this line logs it in full, and the AppHost process
        // itself keeps running (the dashboard and the `keycloak` container
        // both stay reachable afterwards) — the rethrow does not crash the
        // whole stand. Whether the dashboard additionally marks the
        // `keycloak` resource itself as faulted was not checked; this log
        // line — naming the stand file and, via `ex`, which admin-API call
        // failed — is what actually tells the next reader what happened,
        // whatever the dashboard shows.
        logger.LogError(
            ex,
            "E2E stand: provisioning failed — the stand file at {StandFilePath} was not (re)written, " +
            "so the suite will not run against this stand: it will see a stale or missing secret, not a fresh one.",
            standFilePath);
        throw;
    }
});

// Mustask.Api как resource server realm'а `mustask`. Endpoint
// непроксируемый — для проекта-потребителя резолвится в
// `http://localhost:5010`, ровно тот `iss`, который Keycloak выводит из
// `Host` при входе через `localhost` (KC_HOSTNAME не задан). `WaitFor(keycloak)`
// у `api` намеренно НЕ ставится: API стартует независимо от Keycloak и сам
// доходит до готовности через RealmReadinessMonitor.
api.WithEnvironment("Keycloak__Authority", ReferenceExpression.Create($"{keycloak.GetEndpoint("http")}/realms/mustask"))
   .WithEnvironment("Keycloak__Audience", "mustask-api")
   // ADR-0022 Decision 4: секрет клиента виден только бэкенду, который один
   // обменивает код — никогда браузеру.
   .WithEnvironment("Keycloak__ClientSecret", keycloakPanelClientSecret);

// --- Фронтенд ----------------------------------------------------------------

// frontend/ живёт в src/frontend; AppHost — в src/backend/Mustask.AppHost,
// отсюда двойной ".." до src/.
var frontendPort = Port("Frontend");

var frontend = builder
    .AddViteApp("frontend", Path.Combine(builder.AppHostDirectory, "..", "..", "frontend"))
    .WithNpm()
    // Фиксированный dev-порт (вместо динамического): URL стенда переживает
    // перезапуск AppHost, а статический CORS-origin в appsettings.Development
    // остаётся верным и при запуске фронта без Aspire. Сам Vite слушает все
    // интерфейсы через server.host в vite.config.ts — Aspire передаёт ему
    // только --port и host не настраивает.
    .WithHttpEndpoint(port: frontendPort, env: "PORT")
    .WithEndpoint("http", endpoint => ExposeToNetwork(endpoint, frontendPort, frontendPort))
    .WithReference(api)
    .WithEnvironment("VITE_API_BASE_URL", api.GetEndpoint("http"))
    // ADR-0022: панель не является OIDC-клиентом и не ходит к Keycloak
    // напрямую вовсе, поэтому адресов Keycloak она не получает. Mustask.Api
    // сам является OIDC-клиентом mustask-panel (Features/Session); секрет
    // клиента виден только ему (Keycloak__ClientSecret выше), никогда браузеру.
    .WaitFor(api);

// Dev CORS. Origin фронта — первым элементом списка (порт фиксирован выше, а
// не выделяется Aspire). Дальше идут адреса этой машины в локальной сети: с
// другого устройства фронтенд открывают как http://<ip>:3000, и без этих
// origin'ов браузер молча заблокировал бы все запросы к API. Отражать любой
// origin предикатом нельзя — это ровно то, что запрещает
// CorsTests.GetHealth_CrossOriginRequestFromDisallowedOrigin_...
api.WithEnvironment("Cors__AllowedOrigins__0", frontend.GetEndpoint("http"));

// ADR-0022: адрес панели для абсолютного редиректа после /callback и для
// post_logout_redirect_uri. Обязана стоять здесь, а не рядом с остальными
// Keycloak__* строками выше — `frontend` объявляется физически позже в этом
// файле.
api.WithEnvironment("Session__PanelUrl", frontend.GetEndpoint("http"));

var originIndex = 1;
foreach (var address in LocalNetworkAddresses())
{
    // Origin собирается отдельной строкой: интерполяция прямо в аргументе
    // уходит в перегрузку WithEnvironment с ReferenceExpression, а она не
    // умеет форматировать int.
    var origin = FormattableString.Invariant($"http://{address}:{frontendPort}");
    api.WithEnvironment($"Cors__AllowedOrigins__{originIndex}", origin);
    originIndex++;
}

// IPv4-адреса поднятых сетевых интерфейсов, кроме loopback и служебных
// link-local (169.254.*): именно по ним стенд открывают с других машин.
static IEnumerable<string> LocalNetworkAddresses() =>
    NetworkInterface.GetAllNetworkInterfaces()
        .Where(nic => nic.OperationalStatus == OperationalStatus.Up
            && nic.NetworkInterfaceType != NetworkInterfaceType.Loopback)
        .SelectMany(nic => nic.GetIPProperties().UnicastAddresses)
        .Select(unicast => unicast.Address)
        .Where(address => address.AddressFamily == AddressFamily.InterNetwork
            && !IPAddress.IsLoopback(address)
            && !address.ToString().StartsWith("169.254.", StringComparison.Ordinal))
        .Select(address => address.ToString())
        .Distinct(StringComparer.Ordinal)
        .ToList();

builder.Build().Run();
