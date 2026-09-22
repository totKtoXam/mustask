using System.Net;
using Mustask.Api.Features.Session;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace Mustask.Api.IntegrationTests.Features.Session;

/// <summary>
/// Unit-level, no host: pure parsing/routing logic of
/// <see cref="RefreshGrantClient"/> against a stubbed <see cref="HttpMessageHandler"/>.
/// </summary>
public sealed class RefreshGrantClientTests
{
    private const string TokenEndpoint = "http://keycloak.invalid/realms/mustask/protocol/openid-connect/token";
    private const string EndSessionEndpoint = "http://keycloak.invalid/realms/mustask/protocol/openid-connect/logout";
    private const string ClientSecret = "test-secret";

    private static RefreshGrantClient CreateClient(
        HttpMessageHandler handler,
        IConfigurationManager<OpenIdConnectConfiguration>? configurationManager = null,
        ILogger<RefreshGrantClient>? logger = null)
    {
        var httpClientFactory = new SingleClientFactory(new HttpClient(handler));
        configurationManager ??= new StaticConfigurationManager<OpenIdConnectConfiguration>(
            new OpenIdConnectConfiguration { TokenEndpoint = TokenEndpoint, EndSessionEndpoint = EndSessionEndpoint });
        var sessionOptions = new SessionOidcOptions { ClientSecret = ClientSecret, PanelUrl = "http://panel.invalid" };

        return new RefreshGrantClient(httpClientFactory, configurationManager, sessionOptions, logger ?? NullLogger<RefreshGrantClient>.Instance);
    }

    [Fact]
    public async Task RefreshAsync_SuccessWithRefreshExpiresIn_ParsesAllFields()
    {
        var client = CreateClient(new StubHandler(_ => JsonResponse(HttpStatusCode.OK,
            """{"access_token":"at-1","token_type":"Bearer","expires_in":300,"refresh_token":"rt-1","id_token":"idt-1","refresh_expires_in":1800}""")));

        var result = await client.RefreshAsync("old-refresh-token", CancellationToken.None);

        Assert.Equal("at-1", result.AccessToken);
        Assert.Equal(TimeSpan.FromSeconds(300), result.ExpiresIn);
        Assert.Equal("rt-1", result.RefreshToken);
        Assert.Equal("idt-1", result.IdToken);
        Assert.Equal(TimeSpan.FromSeconds(1800), result.RefreshExpiresIn);
    }

    [Fact]
    public async Task RefreshAsync_NoRefreshExpiresInField_RefreshExpiresInIsNull()
    {
        var client = CreateClient(new StubHandler(_ => JsonResponse(HttpStatusCode.OK,
            """{"access_token":"at-1","token_type":"Bearer","expires_in":300,"refresh_token":"rt-1","id_token":"idt-1"}""")));

        var result = await client.RefreshAsync("old-refresh-token", CancellationToken.None);

        Assert.Null(result.RefreshExpiresIn);
    }

    [Fact]
    public async Task RefreshAsync_RefreshExpiresInIsNotANumber_TreatedAsAbsent()
    {
        var client = CreateClient(new StubHandler(_ => JsonResponse(HttpStatusCode.OK,
            """{"access_token":"at-1","token_type":"Bearer","expires_in":300,"refresh_token":"rt-1","id_token":"idt-1","refresh_expires_in":"not-a-number"}""")));

        var result = await client.RefreshAsync("old-refresh-token", CancellationToken.None);

        Assert.Null(result.RefreshExpiresIn);
    }

    [Fact]
    public async Task RefreshAsync_InvalidGrant_ThrowsRefreshGrantRejectedException()
    {
        var client = CreateClient(new StubHandler(_ => JsonResponse(
            HttpStatusCode.BadRequest, """{"error":"invalid_grant","error_description":"Session not active"}""")));

        await Assert.ThrowsAsync<RefreshGrantRejectedException>(
            () => client.RefreshAsync("dead-refresh-token", CancellationToken.None));
    }

    [Fact]
    public async Task RefreshAsync_BadRequestWithoutInvalidGrant_ThrowsPlainHttpRequestException()
    {
        var client = CreateClient(new StubHandler(_ => JsonResponse(
            HttpStatusCode.BadRequest, """{"error":"invalid_client"}""")));

        var exception = await Assert.ThrowsAsync<HttpRequestException>(
            () => client.RefreshAsync("some-refresh-token", CancellationToken.None));
        Assert.Equal(HttpStatusCode.BadRequest, exception.StatusCode);
    }

    [Fact]
    public async Task RefreshAsync_ServerError_ThrowsHttpRequestException()
    {
        var client = CreateClient(new StubHandler(_ => JsonResponse(HttpStatusCode.InternalServerError, "{}")));

        var exception = await Assert.ThrowsAsync<HttpRequestException>(
            () => client.RefreshAsync("some-refresh-token", CancellationToken.None));
        Assert.Equal(HttpStatusCode.InternalServerError, exception.StatusCode);
    }

    /// <summary>A network/timeout failure is not caught — it surfaces to the caller as-is.</summary>
    [Fact]
    public async Task RefreshAsync_NetworkFailure_PropagatesOriginalExceptionUnchanged()
    {
        var networkFailure = new HttpRequestException("Connection refused (simulated).");
        var client = CreateClient(new ThrowingHandler(networkFailure));

        var exception = await Assert.ThrowsAsync<HttpRequestException>(
            () => client.RefreshAsync("some-refresh-token", CancellationToken.None));
        Assert.Same(networkFailure, exception);
    }

    /// <summary>The real <c>ConfigurationManager&lt;T&gt;</c> throws this, not <see cref="HttpRequestException"/>, when it has never once obtained discovery.</summary>
    [Fact]
    public async Task RefreshAsync_DiscoveryNeverObtained_ThrowsHttpRequestException_NotRawInvalidOperationException()
    {
        var client = CreateClient(
            new StubHandler(_ => JsonResponse(HttpStatusCode.OK, "{}")),
            configurationManager: new ThrowingConfigurationManager());

        await Assert.ThrowsAsync<HttpRequestException>(
            () => client.RefreshAsync("some-refresh-token", CancellationToken.None));
    }

    /// <summary>A malformed/non-JSON success body must not surface as a raw parse exception — the caller only ever catches <see cref="HttpRequestException"/> for "treat as Keycloak unavailable".</summary>
    [Fact]
    public async Task RefreshAsync_UnparsableSuccessBody_ThrowsHttpRequestException_NotRawJsonException()
    {
        var client = CreateClient(new StubHandler(_ => JsonResponse(HttpStatusCode.OK, "not json at all")));

        await Assert.ThrowsAsync<HttpRequestException>(
            () => client.RefreshAsync("some-refresh-token", CancellationToken.None));
    }

    /// <summary>A success body missing a required field must not surface as a raw <see cref="KeyNotFoundException"/> either.</summary>
    [Fact]
    public async Task RefreshAsync_SuccessBodyMissingRequiredField_ThrowsHttpRequestException()
    {
        var client = CreateClient(new StubHandler(_ => JsonResponse(
            HttpStatusCode.OK, """{"access_token":"at-1","token_type":"Bearer","expires_in":300}""")));

        await Assert.ThrowsAsync<HttpRequestException>(
            () => client.RefreshAsync("some-refresh-token", CancellationToken.None));
    }

    /// <summary>An unexpected 4xx (not invalid_grant) is logged with the status and Keycloak's own error code — never the raw body or the client secret — so it stops looking identical to Keycloak being down.</summary>
    [Fact]
    public async Task RefreshAsync_UnexpectedClientError_LogsStatusAndErrorCode_NeverTheSecret()
    {
        var log = new CapturingLogger<RefreshGrantClient>();
        var client = CreateClient(
            new StubHandler(_ => JsonResponse(HttpStatusCode.Unauthorized, """{"error":"invalid_client"}""")),
            logger: log);

        await Assert.ThrowsAsync<HttpRequestException>(
            () => client.RefreshAsync("some-refresh-token", CancellationToken.None));

        var logged = Assert.Single(log.Messages);
        Assert.Contains("invalid_client", logged, StringComparison.Ordinal);
        Assert.Contains("401", logged, StringComparison.Ordinal);
        Assert.DoesNotContain(ClientSecret, logged, StringComparison.Ordinal);
    }

    [Fact]
    public async Task LogoutAsync_Success_DoesNotThrow()
    {
        var client = CreateClient(new StubHandler(_ => JsonResponse(HttpStatusCode.OK, "{}")));

        await client.LogoutAsync("some-refresh-token", CancellationToken.None);
    }

    /// <summary>Keycloak answering that the session is already gone is itself a successful (idempotent) logout.</summary>
    [Fact]
    public async Task LogoutAsync_InvalidGrant_DoesNotThrow()
    {
        var client = CreateClient(new StubHandler(_ => JsonResponse(
            HttpStatusCode.BadRequest, """{"error":"invalid_grant"}""")));

        await client.LogoutAsync("some-refresh-token", CancellationToken.None);
    }

    [Fact]
    public async Task LogoutAsync_NetworkFailure_PropagatesAsHttpRequestException()
    {
        var client = CreateClient(new ThrowingHandler(new HttpRequestException("Connection refused (simulated).")));

        await Assert.ThrowsAsync<HttpRequestException>(
            () => client.LogoutAsync("some-refresh-token", CancellationToken.None));
    }

    [Fact]
    public async Task LogoutAsync_UnexpectedClientError_ThrowsHttpRequestException()
    {
        var client = CreateClient(new StubHandler(_ => JsonResponse(
            HttpStatusCode.Unauthorized, """{"error":"invalid_client"}""")));

        await Assert.ThrowsAsync<HttpRequestException>(
            () => client.LogoutAsync("some-refresh-token", CancellationToken.None));
    }

    private static HttpResponseMessage JsonResponse(HttpStatusCode status, string json) => new(status)
    {
        Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json"),
    };

    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(respond(request));
    }

    private sealed class ThrowingHandler(Exception exception) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromException<HttpResponseMessage>(exception);
    }

    private sealed class SingleClientFactory(HttpClient client) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => client;
    }

    /// <summary>Reproduces the real <c>ConfigurationManager&lt;T&gt;</c>'s behavior when it has never once obtained discovery — <see cref="InvalidOperationException"/>, not <see cref="HttpRequestException"/>.</summary>
    private sealed class ThrowingConfigurationManager : IConfigurationManager<OpenIdConnectConfiguration>
    {
        public Task<OpenIdConnectConfiguration> GetConfigurationAsync(CancellationToken cancel) =>
            Task.FromException<OpenIdConnectConfiguration>(
                new InvalidOperationException("IDX20803: Unable to obtain configuration (simulated)."));

        public void RequestRefresh()
        {
        }
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = [];

        public IDisposable BeginScope<TState>(TState state) where TState : notnull => NoopScope.Instance;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Messages.Add(formatter(state, exception));

        private sealed class NoopScope : IDisposable
        {
            public static readonly NoopScope Instance = new();

            public void Dispose()
            {
            }
        }
    }
}
