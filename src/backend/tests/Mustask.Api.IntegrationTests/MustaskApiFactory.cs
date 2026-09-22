using Mustask.Api.ErrorHandling.Catalog;
using Mustask.Api.Features.Session;
using Mustask.Api.IntegrationTests.Features.Session;
using Mustask.Infrastructure;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Testcontainers.PostgreSql;

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
/// Registered once as a shared <c>ICollectionFixture</c> for the whole
/// <see cref="MustaskApiCollection"/> (one Postgres container, not one per test
/// class) — see that type for why every test class touching this
/// process-wide environment variable also has to run in the same
/// collection.
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

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:18-alpine")
        .WithDatabase("mustask")
        .Build();

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
    /// Среда хоста. По умолчанию Development — health-эндпоинты и OpenApi
    /// требуют его. Тесты, проверяющие поведение в Production (например,
    /// отсутствие деталей исключения в ответе), переопределяют это свойство
    /// через object initializer.
    /// </summary>
    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        Environment.SetEnvironmentVariable(ConnectionStringEnvVar, _postgres.GetConnectionString());

        // ADR-0002: migrations are never applied automatically from
        // src/ (see ProgramDoesNotAutoMigrateTests) -- this fixture is the
        // "explicit step" for integration tests, the same role
        // Mustask.Infrastructure.Tests' MigrationsTests play for
        // Mustask.Infrastructure's own tests. Without this, every endpoint test
        // that touches a real table would hit "relation ... does not exist".
        await using (var context = new MustaskDbContextFactory().CreateDbContext([]))
        {
            await context.Database.MigrateAsync();
        }

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
    }

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
        Environment.SetEnvironmentVariable(ConnectionStringEnvVar, null);
        Environment.SetEnvironmentVariable(KeycloakAuthorityEnvVar, null);
        Environment.SetEnvironmentVariable(KeycloakAudienceEnvVar, null);
        Environment.SetEnvironmentVariable(KeycloakClientSecretEnvVar, null);
        Environment.SetEnvironmentVariable(SessionPanelUrlEnvVar, null);
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
        });
    }
}
