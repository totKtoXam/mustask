using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Mustask.Api.Authentication.Readiness;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace Mustask.Api.IntegrationTests;

/// <summary>
/// Гейт 503 до аутентификации, пока realm не
/// готов. Общая фабрика (<see cref="MustaskApiCollection"/>) — каждый тест,
/// переключающий <see cref="MustaskApiFactory.RealmMetadataFake"/>, обязан
/// вернуть его в <c>Ok</c> и восстановить готовность в <c>finally</c>, иначе
/// сломает остальные тесты коллекции (та же дисциплина, что у
/// <see cref="MustaskApiFactory.TokenResponder"/>).
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class RealmReadinessTests
{
    private const string ProtectedRoute = "/api/dictionaries/languages";

    private readonly MustaskApiFactory _factory;

    public RealmReadinessTests(MustaskApiFactory factory)
    {
        _factory = factory;
    }

    private async Task<RealmReadinessState> RefreshAsync()
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<RealmReadinessMonitor>().CheckAsync();
    }

    private async Task RestoreReadyAsync()
    {
        _factory.RealmMetadataFake.CurrentMode = KeycloakTestRealm.FakeRealmMetadata.Mode.Ok;
        var state = await RefreshAsync();
        Assert.True(state.IsReady, "Не удалось вернуть общую фабрику в готовое состояние после теста.");
    }

    /// <summary>
    /// «После Ready недоступность realm'а не сбрасывает
    /// готовность» — переключение общей фабрики (уже <c>Ready</c> к моменту,
    /// когда эти тесты запускаются) в <c>Unavailable</c>/<c>EmptyKeys</c>
    /// ничего не показало бы: guard в <c>RealmReadinessMonitor</c> нарочно
    /// держит прежнее состояние для этих двух причин. Наблюдать реальный
    /// переход в <c>NotReady</c> можно только у хоста, который ещё НИ РАЗУ
    /// не был <c>Ready</c> — отдельная, произведённая через
    /// <c>WithWebHostBuilder</c> фабрика со своим <see cref="RealmReadiness"/>
    /// (тот же приём, что у <c>ForeignIssuerAtStartup</c>): её самая
    /// первая проверка (в <c>StartAsync</c>, до старта Kestrel) видит
    /// выбранный режим напрямую.
    /// </summary>
    private (WebApplicationFactory<Program> Factory, KeycloakTestRealm.FakeRealmMetadata Fake) CreateFreshFactory(
        KeycloakTestRealm.FakeRealmMetadata.Mode initialMode)
    {
        var fake = new KeycloakTestRealm.FakeRealmMetadata { CurrentMode = initialMode };
        var factory = _factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(
            services => services.AddSingleton<IConfigurationManager<OpenIdConnectConfiguration>>(fake)));

        return (factory, fake);
    }

    private static async Task<RealmReadinessState> RefreshAsync(WebApplicationFactory<Program> factory)
    {
        using var scope = factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<RealmReadinessMonitor>().CheckAsync();
    }

    [Theory]
    [InlineData(KeycloakTestRealm.FakeRealmMetadata.Mode.Unavailable)]
    [InlineData(KeycloakTestRealm.FakeRealmMetadata.Mode.EmptyKeys)]
    public async Task NotReady_HealthUnhealthy_AliveHealthy_ProtectedRoute503_HandlerNotInvoked(
        KeycloakTestRealm.FakeRealmMetadata.Mode mode)
    {
        var (factory, _) = CreateFreshFactory(mode);
        using var disposableFactory = factory;

        var client = factory.CreateClient();

        var health = await client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, health.StatusCode);

        var alive = await client.GetAsync("/alive");
        Assert.Equal(HttpStatusCode.OK, alive.StatusCode);

        TestEndpointsStartupFilter.DenyAllProbeHandlerExecuted = false;
        var probe = await client.GetAsync("/__test/deny-all-probe");
        var probeBody = await probe.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.ServiceUnavailable, probe.StatusCode);
        Assert.Equal("http.service_unavailable", probeBody.GetProperty("errorCode").GetString());
        Assert.Equal("30", probe.Headers.GetValues("Retry-After").Single());
        Assert.False(TestEndpointsStartupFilter.DenyAllProbeHandlerExecuted);

        var protectedResponse = await client.GetAsync(ProtectedRoute);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, protectedResponse.StatusCode);
    }

    /// <summary>
    /// Публичный маршрут (<c>Public</c>, здесь — <c>POST /api/session/token</c>)
    /// обязан оставаться достижимым независимо от готовности realm'а — гейт
    /// распознаёт его по <c>IAllowAnonymous</c>, а не по префиксу пути.
    /// </summary>
    [Fact]
    public async Task NotReady_PublicRoute_IsNotGated()
    {
        var (factory, _) = CreateFreshFactory(KeycloakTestRealm.FakeRealmMetadata.Mode.Unavailable);
        using var disposableFactory = factory;
        var client = factory.CreateClient();

        var response = await client.PostAsync("/api/session/token", content: null);

        Assert.NotEqual(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task RealmRecovers_HealthHealthy_ProtectedRouteRespondsNormally()
    {
        var (factory, fake) = CreateFreshFactory(KeycloakTestRealm.FakeRealmMetadata.Mode.Unavailable);
        using var disposableFactory = factory;
        var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (await client.GetAsync(ProtectedRoute)).StatusCode);

        fake.CurrentMode = KeycloakTestRealm.FakeRealmMetadata.Mode.Ok;
        var state = await RefreshAsync(factory);
        Assert.True(state.IsReady);

        var health = await client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, health.StatusCode);

        // Realm снова готов -> гейт пропускает, дальше обычный 401 без токена.
        var protectedResponse = await client.GetAsync(ProtectedRoute);
        Assert.Equal(HttpStatusCode.Unauthorized, protectedResponse.StatusCode);
    }

    /// <summary>
    /// Чужой <c>issuer</c> при старте — ошибка
    /// конфигурации, которая сама не исправится. Отдельная,
    /// произведённая через <c>WithWebHostBuilder</c> фабрика — общий хост
    /// коллекции при этом не трогается.
    /// </summary>
    [Fact]
    public void ForeignIssuerAtStartup_HostDoesNotStart_MessageNamesBothIssuers()
    {
        var foreignIssuerFake = new KeycloakTestRealm.FakeRealmMetadata
        {
            CurrentMode = KeycloakTestRealm.FakeRealmMetadata.Mode.ForeignIssuer,
        };

        using var factory = _factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(
            services => services.AddSingleton<IConfigurationManager<OpenIdConnectConfiguration>>(foreignIssuerFake)));

        var exception = Assert.Throws<InvalidOperationException>(() => factory.CreateClient());

        Assert.Contains(KeycloakTestRealm.Issuer, exception.Message, StringComparison.Ordinal);
        Assert.Contains(KeycloakTestRealm.ForeignIssuer, exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// «Чужой issuer обнаружен уже после старта»: расхождение,
    /// пойманное фоновой проверкой уже готового хоста, переводит его в
    /// NotReady бессрочно и пишет ошибку с обоими issuer'ами в журнал — но не
    /// останавливает хост.
    /// </summary>
    [Fact]
    public async Task ForeignIssuerInBackground_AfterReady_BecomesNotReady_LogsBothIssuers()
    {
        await RestoreReadyAsync();

        var originalOut = Console.Out;
        var capturedOutput = new StringWriter();
        Console.SetOut(capturedOutput);

        RealmReadinessState state;
        try
        {
            _factory.RealmMetadataFake.CurrentMode = KeycloakTestRealm.FakeRealmMetadata.Mode.ForeignIssuer;
            state = await RefreshAsync();
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        try
        {
            Assert.False(state.IsReady);
            Assert.Equal(RealmReadinessReason.IssuerMismatch, state.Reason);

            var output = capturedOutput.ToString();
            Assert.Contains(KeycloakTestRealm.Issuer, output, StringComparison.Ordinal);
            Assert.Contains(KeycloakTestRealm.ForeignIssuer, output, StringComparison.Ordinal);
        }
        finally
        {
            await RestoreReadyAsync();
        }
    }

    /// <summary>
    /// <c>CheckAsync</c> ловит любое исключение, не только
    /// <see cref="HttpRequestException"/> — иначе умолчание хоста
    /// (<c>BackgroundServiceExceptionBehavior.StopHost</c>) остановило бы
    /// <c>Mustask.Api</c> целиком на первом непредвиденном сбое проверки.
    /// Отдельная производная фабрика: подменяет
    /// <see cref="IConfigurationManager{T}"/> на реализацию, которая бросает
    /// не <see cref="HttpRequestException"/>, а произвольное исключение.
    /// </summary>
    [Fact]
    public async Task UnexpectedExceptionFromConfigurationManager_HostStaysUp_StateIsNotReady()
    {
        using var factory = _factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(
            services => services.AddSingleton<IConfigurationManager<OpenIdConnectConfiguration>>(
                new ThrowsUnexpectedExceptionConfigurationManager())));

        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    private sealed class ThrowsUnexpectedExceptionConfigurationManager : IConfigurationManager<OpenIdConnectConfiguration>
    {
        public Task<OpenIdConnectConfiguration> GetConfigurationAsync(CancellationToken cancel) =>
            throw new InvalidOperationException("Boom — a non-HttpRequestException failure (test double).");

        public void RequestRefresh()
        {
        }
    }
}
