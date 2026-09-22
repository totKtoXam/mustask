using Microsoft.AspNetCore.Hosting;

namespace Mustask.Api.IntegrationTests;

/// <summary>
/// Без обязательного ключа <c>Mustask.Api</c> не
/// стартует «без проверки токенов» — старт валится с сообщением, называющим
/// отсутствующий ключ. <see cref="Mustask.Api.Authentication.KeycloakOptions.FromConfiguration"/>
/// читает <c>Keycloak:*</c> из <c>builder.Configuration</c> до
/// <c>WebApplicationBuilder.Build()</c> — <c>WithWebHostBuilder</c>'овские
/// <c>UseSetting</c>/<c>ConfigureAppConfiguration</c> производной фабрики
/// применяются позже и до этого кода не долетают, поэтому единственный
/// работающий рычаг — process-wide переменная окружения. Класс лежит в
/// <see cref="MustaskApiCollection"/> (xUnit её не параллелит), это законно ровно
/// потому же, почему это уже сделано для <c>ConnectionStrings__mustask</c>.
///
/// Производная фабрика обязана форсировать окружение <c>Production</c>: у
/// <c>MustaskApiFactory</c> оно по умолчанию <c>Development</c>, а
/// <c>appsettings.Development.json</c> сам держит
/// <c>Keycloak:Authority</c>/<c>Keycloak:Audience</c> («для запуска без
/// AppHost») — без переключения на <c>Production</c>
/// (у него такой секции нет) снятая переменная
/// окружения молча перекрывалась бы этим фолбэком, и тест проверял бы не
/// «ключ не задан», а «ключ читается из appsettings».
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class KeycloakOptionsTests
{
    private readonly MustaskApiFactory _factory;

    public KeycloakOptionsTests(MustaskApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public void MissingAuthority_HostDoesNotStart_NamesTheKey()
    {
        var previous = Environment.GetEnvironmentVariable("Keycloak__Authority");
        Environment.SetEnvironmentVariable("Keycloak__Authority", null);

        try
        {
            using var factory = _factory.WithWebHostBuilder(builder => builder.UseEnvironment("Production"));

            var exception = Assert.Throws<InvalidOperationException>(() => factory.CreateClient());
            Assert.Equal("Missing configuration value Keycloak:Authority.", exception.Message);
        }
        finally
        {
            Environment.SetEnvironmentVariable("Keycloak__Authority", previous);
        }
    }

    [Fact]
    public void MissingAudience_HostDoesNotStart_NamesTheKey()
    {
        var previous = Environment.GetEnvironmentVariable("Keycloak__Audience");
        Environment.SetEnvironmentVariable("Keycloak__Audience", null);

        try
        {
            using var factory = _factory.WithWebHostBuilder(builder => builder.UseEnvironment("Production"));

            var exception = Assert.Throws<InvalidOperationException>(() => factory.CreateClient());
            Assert.Equal("Missing configuration value Keycloak:Audience.", exception.Message);
        }
        finally
        {
            Environment.SetEnvironmentVariable("Keycloak__Audience", previous);
        }
    }

    /// <summary>
    /// <c>Keycloak:ClientSecret</c> is one of the required configuration keys ("секрет клиента mustask-panel...
    /// для сессионного бэкенда панели"), and <see cref="Mustask.Api.Features.Session.SessionOidcOptions.FromConfiguration"/>
    /// reads it with the exact same fail-fast convention as
    /// <see cref="Mustask.Api.Authentication.KeycloakOptions.FromConfiguration"/> —
    /// this was untested before this round.
    /// </summary>
    [Fact]
    public void MissingSessionClientSecret_HostDoesNotStart_NamesTheKey()
    {
        var previous = Environment.GetEnvironmentVariable("Keycloak__ClientSecret");
        Environment.SetEnvironmentVariable("Keycloak__ClientSecret", null);

        try
        {
            using var factory = _factory.WithWebHostBuilder(builder => builder.UseEnvironment("Production"));

            var exception = Assert.Throws<InvalidOperationException>(() => factory.CreateClient());
            Assert.Equal("Missing configuration value Keycloak:ClientSecret.", exception.Message);
        }
        finally
        {
            Environment.SetEnvironmentVariable("Keycloak__ClientSecret", previous);
        }
    }

    /// <summary>Same gap as above, for <c>Session:PanelUrl</c>.</summary>
    [Fact]
    public void MissingSessionPanelUrl_HostDoesNotStart_NamesTheKey()
    {
        var previous = Environment.GetEnvironmentVariable("Session__PanelUrl");
        Environment.SetEnvironmentVariable("Session__PanelUrl", null);

        try
        {
            using var factory = _factory.WithWebHostBuilder(builder => builder.UseEnvironment("Production"));

            var exception = Assert.Throws<InvalidOperationException>(() => factory.CreateClient());
            Assert.Equal("Missing configuration value Session:PanelUrl.", exception.Message);
        }
        finally
        {
            Environment.SetEnvironmentVariable("Session__PanelUrl", previous);
        }
    }

    /// <summary>
    /// The realm import's own default
    /// (<c>dev-only-placeholder-secret</c>, also the value
    /// <c>appsettings.Development.json</c> ships) is a known literal outside
    /// Development — a deployment that copied it "to make it work" gets a
    /// loud refusal to start instead of a silently useless confidential
    /// client (ADR-0022 Decision 4).
    /// </summary>
    [Theory]
    [InlineData("dev-only-placeholder-secret")]
    [InlineData("${MUSTASK_PANEL_CLIENT_SECRET}")]
    [InlineData("short-secret")]
    public void KnownOrShortSessionClientSecret_OutsideDevelopment_HostDoesNotStart(string secret)
    {
        var previous = Environment.GetEnvironmentVariable("Keycloak__ClientSecret");
        Environment.SetEnvironmentVariable("Keycloak__ClientSecret", secret);

        try
        {
            using var factory = _factory.WithWebHostBuilder(builder => builder.UseEnvironment("Production"));

            var exception = Assert.Throws<InvalidOperationException>(() => factory.CreateClient());
            Assert.Contains("Keycloak:ClientSecret", exception.Message, StringComparison.Ordinal);
        }
        finally
        {
            Environment.SetEnvironmentVariable("Keycloak__ClientSecret", previous);
        }
    }

    /// <summary>The same placeholder is exactly what makes the Development stand work without a real secret — the check must not fire there.</summary>
    [Fact]
    public void PlaceholderSessionClientSecret_InDevelopment_HostStillStarts()
    {
        var previous = Environment.GetEnvironmentVariable("Keycloak__ClientSecret");
        Environment.SetEnvironmentVariable("Keycloak__ClientSecret", "dev-only-placeholder-secret");

        try
        {
            using var factory = _factory.WithWebHostBuilder(builder => builder.UseEnvironment("Development"));

            using var client = factory.CreateClient();
            Assert.NotNull(client);
        }
        finally
        {
            Environment.SetEnvironmentVariable("Keycloak__ClientSecret", previous);
        }
    }
}
