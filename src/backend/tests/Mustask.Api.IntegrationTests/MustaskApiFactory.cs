using Mustask.Api.ErrorHandling.Catalog;
using Mustask.Api.Features.Session;
using Mustask.Api.IntegrationTests.Features.Organizations;
using Mustask.Api.IntegrationTests.Features.Session;
using Mustask.Application.Abstractions;
using Mustask.Infrastructure;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.Extensions.Hosting;
using Mustask.Testing;

namespace Mustask.Api.IntegrationTests;

/// <summary>
/// Boots Mustask.Api against a real Postgres container (Testcontainers) so
/// integration tests exercise the same wiring as production (Aspire
/// connection string "mustask" consumed by Mustask.Infrastructure). The connection
/// string is passed via the "ConnectionStrings__mustask" environment variable —
/// the same mechanism Aspire's AppHost uses at runtime — because
/// <c>Program.cs</c> reads it via <c>builder.AddInfrastructure()</c> as a
/// top-level statement, before <c>WebApplicationBuilder.Build()</c> runs.
/// <c>WebApplicationFactory</c>'s own overrides
/// (<c>ConfigureWebHost</c>/<c>IWebHostBuilder.UseSetting</c>) are only
/// merged in when the deferred host builder's <c>Build()</c> is invoked, so
/// they are not visible yet to that top-level statement — an environment
/// variable, already loaded by the default configuration providers at
/// <c>WebApplication.CreateBuilder(args)</c>, is the mechanism that actually
/// works here.
///
/// Registered as the <c>ICollectionFixture</c> of <see cref="MustaskApiCollection"/>:
/// one instance per shard of it, each over a database of its own on the
/// assembly's one Postgres container (tech-047) — see that type for how the
/// process-wide environment variable is kept apart between shards.
/// </summary>
public sealed class MustaskApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private const string ConnectionStringEnvVar = "ConnectionStrings__mustask";

    // Переменными окружения, а не UseSetting фабрики и не
    // appsettings.Development.json: KeycloakOptions.FromConfiguration читает
    // builder.Configuration до Build(), а часть тестов поднимает хост в
    // Production/Staging, где файл Development не читается.
    private const string KeycloakAuthorityEnvVar = "Keycloak__Authority";
    private const string KeycloakAudienceEnvVar = "Keycloak__Audience";

    // ADR-0022: SessionOidcOptions.FromConfiguration читает эти же
    // ключи до Build() — тем же приёмом, что Keycloak:Authority/Audience выше.
    private const string KeycloakClientSecretEnvVar = "Keycloak__ClientSecret";
    private const string SessionPanelUrlEnvVar = "Session__PanelUrl";

    /// <summary>
    /// ≥ 32 characters and not one of
    /// the two known placeholder literals — every test that forces
    /// <c>Production</c> (e.g. <c>KeycloakOptionsTests</c>) would otherwise
    /// trip the new outside-Development secret check by accident, on a value
    /// that has nothing to do with what that particular test means to check.
    /// </summary>
    public const string TestKeycloakClientSecret = "test-client-secret-0123456789abcdef";

    /// <summary>
    /// mod-014: the push channel's shared secret for this host — read by
    /// <c>PushChannelOptions.FromConfiguration</c> before <c>Build()</c>, so an
    /// environment variable like the ones above. A test value, never a real
    /// one; <c>PushTestClient</c> signs with it.
    /// </summary>
    public const string TestPushSecret = "test-push-secret-not-real-0123456789abcdef";

    private const string PushSecretEnvVar = "UserDirectory__Push__Secret";

    /// <summary>
    /// ADR-0002: migrations are never applied automatically from src/ --
    /// this template is the "explicit step"
    /// for integration tests, the same role Mustask.Infrastructure.Tests'
    /// MigrationsTests play for Mustask.Infrastructure's own tests. Migrated
    /// once per run; every factory gets a copy of it (tech-047). Without it,
    /// every endpoint test that touches a real table would hit "relation ...
    /// does not exist".
    /// </summary>
    private static readonly SharedPostgres.Template MigratedTemplate = new("api_migrated_template", async connectionString =>
    {
        await using var context = new MustaskDbContext(new DbContextOptionsBuilder<MustaskDbContext>().UseNpgsql(connectionString).Options);
        await context.Database.MigrateAsync();
    });

    private static readonly Lock HostBuildGate = new();

    private static int _liveFactories;
    // Set once this factory is in _liveFactories: a factory whose database
    // copy failed must not take another shard's count down on dispose.
    private bool _counted;

    private string _connectionString = "";

    /// <summary>
    /// Общий на коллекцию фейк discovery/JWKS realm'а:
    /// регистрируется как <see cref="IConfigurationManager{T}"/> последней
    /// (побеждает регистрацию продукта), режим по умолчанию — <c>Ok</c>.
    /// Тесты, переключающие режим (<c>RealmReadinessTests</c>,
    /// <c>BearerTokenValidationTests</c>), обязаны вернуть его в <c>Ok</c> в
    /// <c>finally</c> — та же дисциплина, что у <see cref="TokenResponder"/>.
    /// </summary>
    public KeycloakTestRealm.FakeRealmMetadata RealmMetadataFake { get; } = new();

    /// <summary>
    /// ADR-0022: фейк token endpoint'а Keycloak, общий на
    /// коллекцию — оба потребителя (<c>RefreshGrantClient</c> и backchannel
    /// схемы <see cref="SessionSchemes.Oidc"/>) роутятся на него ниже.
    /// Тест, переключивший <see cref="KeycloakTokenEndpointResponder.CurrentMode"/>/
    /// <see cref="KeycloakTokenEndpointResponder.OkResponseBodyFactory"/>,
    /// обязан вызвать <see cref="KeycloakTokenEndpointResponder.Reset"/> в
    /// <c>finally</c> — та же дисциплина, что у <see cref="RealmMetadataFake"/>.
    /// </summary>
    public KeycloakTokenEndpointResponder TokenResponder { get; } = new();

    /// <summary>
    /// mod-002 plan-backend § 8.3: named gates a request can be held at, after
    /// its handler's work and before its unit of work commits, so a race test
    /// makes "at the same moment" deterministic. A request opts in with the
    /// <see cref="CommitGates.Header"/> header; every other request passes
    /// straight through.
    /// </summary>
    public CommitGates CommitGates { get; } = new();

    /// <summary>The shared test database, for <see cref="PgLocks"/> and for reading back what a request wrote.</summary>
    public string ConnectionString => _connectionString;

    /// <summary>
    /// Среда хоста. По умолчанию Development — health-эндпоинты и OpenApi
    /// требуют его. Тесты, проверяющие поведение в Production (например,
    /// отсутствие деталей исключения в ответе), переопределяют это свойство
    /// через object initializer.
    /// </summary>
    public async Task InitializeAsync()
    {
        _connectionString = await SharedPostgres.CreateDatabaseFromTemplateAsync(MigratedTemplate);

        lock (HostBuildGate)
        {
            // The first live factory sets what every host of the process reads;
            // the last one to go clears it (several shards are alive at once).
            _counted = true;
            if (_liveFactories++ == 0)
            {
                // Значения тестового realm'а — сверяются с
                // KeycloakOptions.Issuer/Audience при проверке токенов и с
                // RealmMetadataFake.Configuration.Issuer при проверке готовности.
                Environment.SetEnvironmentVariable(KeycloakAuthorityEnvVar, KeycloakTestRealm.Issuer);
                Environment.SetEnvironmentVariable(KeycloakAudienceEnvVar, KeycloakTestRealm.Audience);
                // ADR-0022: SessionOidcOptions.FromConfiguration требует оба
                // ключа на старте — значения произвольные (тесты не ходят на реальный
                // Keycloak и не открывают реальную панель), лишь бы хост поднялся.
                Environment.SetEnvironmentVariable(KeycloakClientSecretEnvVar, TestKeycloakClientSecret);
                Environment.SetEnvironmentVariable(SessionPanelUrlEnvVar, "http://panel.invalid");
                Environment.SetEnvironmentVariable(PushSecretEnvVar, TestPushSecret);
            }

            // For a host a test builds straight from WebApplicationFactory<Program>
            // (ProductionCorsTests): it reads the variable as it finds it, and
            // such a class runs alone, after the factory of its collection.
            Environment.SetEnvironmentVariable(ConnectionStringEnvVar, _connectionString);
        }
    }

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();

        lock (HostBuildGate)
        {
            if (_counted && --_liveFactories == 0)
            {
                Environment.SetEnvironmentVariable(ConnectionStringEnvVar, null);
                Environment.SetEnvironmentVariable(KeycloakAuthorityEnvVar, null);
                Environment.SetEnvironmentVariable(KeycloakAudienceEnvVar, null);
                Environment.SetEnvironmentVariable(KeycloakClientSecretEnvVar, null);
                Environment.SetEnvironmentVariable(SessionPanelUrlEnvVar, null);
                Environment.SetEnvironmentVariable(PushSecretEnvVar, null);
            }
        }
    }

    /// <summary>
    /// Every host of this factory — its own and each one a test derives with
    /// <c>WithWebHostBuilder</c>, which comes back here — is built under one
    /// process-wide lock with this factory's connection string in
    /// <c>ConnectionStrings__mustask</c> (tech-047). Program.cs reads the
    /// variable before <c>Build()</c>, so the lock is what keeps a host of one
    /// shard from starting on another shard's database; it also keeps two
    /// builds from swapping Serilog's static <c>Log.Logger</c> under each
    /// other (Program.cs takes it right after <c>Build()</c>).
    /// </summary>
    protected override IHost CreateHost(IHostBuilder builder)
    {
        lock (HostBuildGate)
        {
            Environment.SetEnvironmentVariable(ConnectionStringEnvVar, _connectionString);
            return base.CreateHost(builder);
        }
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<IStartupFilter, TestEndpointsStartupFilter>();

            // /__test/throw/* бросает коды test.* — их не кладём в
            // contracts/errors/ (тестовые коды продукта не касаются).
            // Продуктовый ErrorCatalog уже зарегистрирован
            // как готовый экземпляр (AddMustaskErrorHandling), поэтому его можно
            // забрать прямо из ServiceDescriptor, не собирая второй провайдер
            // и не перечитывая embedded resources заново. Регистрация,
            // добавленная здесь последней, и есть та, которую разрешит DI.
            var productCatalog = (ErrorCatalog)services
                .Last(descriptor => descriptor.ServiceType == typeof(ErrorCatalog))
                .ImplementationInstance!;

            services.AddSingleton(ErrorCatalogTestData.MergedWithProductCatalog(productCatalog));

            // Регистрация, добавленная здесь
            // последней, и есть та, которую разрешит DI — та же техника, что
            // для ErrorCatalog выше. Живого Keycloak в тестах нет вовсе:
            // discovery/JWKS подменяются этим фейком.
            services.AddSingleton<IConfigurationManager<OpenIdConnectConfiguration>>(RealmMetadataFake);

            // ADR-0022: живого Keycloak в тестах нет — оба потребителя тела
            // token endpoint'а роутятся на один и тот же TokenResponder:
            // RefreshGrantClient через свой именованный HttpClient (вторая
            // регистрация AddHttpClient с тем же именем заменяет primary
            // handler — Microsoft.Extensions.Http хранит последнюю фабрику
            // ConfigurePrimaryHttpMessageHandler для именованного клиента), и
            // сама OIDC-схема через её собственный Backchannel (единственный
            // HttpClient, который AddOpenIdConnect не резолвит через
            // IHttpClientFactory, поэтому подменяется напрямую на объекте
            // опций — регистрация здесь, добавленная после production-кода в
            // SessionAuthenticationSetup, побеждает по тому же принципу
            // именованных опций, что и остальные Configure/PostConfigure).
            services.AddHttpClient(RefreshGrantClient.HttpClientName)
                .ConfigurePrimaryHttpMessageHandler(() => new KeycloakTokenEndpointStubHandler(TokenResponder));

            services.Configure<OpenIdConnectOptions>(
                SessionSchemes.Oidc,
                options => options.Backchannel = new HttpClient(new KeycloakTokenEndpointStubHandler(TokenResponder)));

            // mod-002 plan-backend § 8.3: the unit of work behind a test-only decorator that can hold a request at its
            // commit. Registered last, so it is the IUnitOfWork every handler resolves.
            services.AddScoped<UnitOfWork>();
            services.AddScoped<IUnitOfWork>(provider => new CommitGateUnitOfWork(
                provider.GetRequiredService<UnitOfWork>(),
                CommitGates,
                provider.GetRequiredService<IHttpContextAccessor>()));
        });
    }
}
