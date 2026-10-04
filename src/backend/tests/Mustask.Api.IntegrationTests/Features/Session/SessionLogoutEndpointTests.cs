using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Mustask.Api.Features.Session;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace Mustask.Api.IntegrationTests.Features.Session;

/// <summary>
/// <c>POST /api/session/logout</c>: back-channel logout at Keycloak now happens before the cookie is cleared,
/// so an unreachable Keycloak can no longer leave its own SSO session alive
/// while our cookie is already gone.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class SessionLogoutEndpointTests
{
    private readonly MustaskApiFactory _factory;

    public SessionLogoutEndpointTests(MustaskApiFactory factory)
    {
        _factory = factory;
    }

    private async Task<(string Cookie, string SessionKey)> SignInAsync(HttpClient client)
    {
        var challenge = await SessionTestSupport.ChallengeAsync(client);
        var sid = Guid.CreateVersion7().ToString();

        _factory.TokenResponder.OkResponseBodyFactory = () => KeycloakTokenEndpointResponder.BuildBody(
            accessToken: KeycloakTestRealm.CreateToken(),
            idToken: KeycloakTokenEndpointResponder.BuildIdToken(sid, challenge.Nonce),
            refreshToken: "logout-test-refresh-token",
            expiresIn: 300,
            refreshExpiresIn: 1800);

        using var request = new HttpRequestMessage(
            HttpMethod.Get, $"/api/session/callback?code=test-code&state={Uri.EscapeDataString(challenge.State)}");
        SessionTestSupport.AttachCookies(request, challenge.Cookies);
        using var response = await client.SendAsync(request);
        _factory.TokenResponder.Reset();

        var rawCookie = SessionTestSupport.SessionCookie(response) ?? throw new InvalidOperationException("Sign-in did not set mustask.session.");
        return (rawCookie.Split(';')[0], SessionTestSupport.SessionKey(_factory, rawCookie));
    }

    [Fact]
    public async Task Logout_WithCookie_ReturnsLogoutUrlWithIdTokenHint_AndClearsCookie()
    {
        var client = _factory.CreateNoRedirectClient();
        var (cookie, _) = await SignInAsync(client);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/session/logout")
        {
            Headers = { { "Cookie", cookie } },
        };
        var response = await client.SendAsync(request);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var logoutUrl = body.GetProperty("logoutUrl").GetString()!;
        Assert.StartsWith(KeycloakTestRealm.Configuration.EndSessionEndpoint, logoutUrl, StringComparison.Ordinal);
        Assert.Contains("id_token_hint=", logoutUrl, StringComparison.Ordinal);
        Assert.Contains($"client_id={SessionOidcOptions.ClientId}", logoutUrl, StringComparison.Ordinal);
        Assert.Contains(Uri.EscapeDataString("http://panel.invalid"), logoutUrl, StringComparison.Ordinal);

        var clearedCookie = SessionTestSupport.SessionCookie(response);
        Assert.NotNull(clearedCookie);
        Assert.Contains("1970", clearedCookie, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>The panel's "Вы вышли из системы" screen lives at "/signed-out" — the bare panel root re-entered the sign-in loop.</summary>
    [Fact]
    public async Task Logout_WithCookie_PostLogoutRedirectUriPointsAtSignedOutPath()
    {
        var client = _factory.CreateNoRedirectClient();
        var (cookie, _) = await SignInAsync(client);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/session/logout")
        {
            Headers = { { "Cookie", cookie } },
        };
        var response = await client.SendAsync(request);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        var logoutUrl = body.GetProperty("logoutUrl").GetString()!;
        var query = QueryHelpers.ParseQuery(new Uri(logoutUrl).Query);

        Assert.Equal("http://panel.invalid/signed-out", query["post_logout_redirect_uri"].ToString());
    }

    [Fact]
    public async Task Logout_WithoutCookie_ReturnsLogoutUrlWithoutIdTokenHint_Idempotently()
    {
        var client = _factory.CreateNoRedirectClient();

        var response = await client.PostAsync("/api/session/logout", null);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var logoutUrl = body.GetProperty("logoutUrl").GetString()!;
        Assert.DoesNotContain("id_token_hint=", logoutUrl, StringComparison.Ordinal);
    }

    /// <summary>The cached access token must not outlive the session that just logged out.</summary>
    [Fact]
    public async Task Logout_WithCookie_EvictsCachedAccessToken()
    {
        var client = _factory.CreateNoRedirectClient();
        var (cookie, sessionKey) = await SignInAsync(client);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/session/logout")
        {
            Headers = { { "Cookie", cookie } },
        };
        await client.SendAsync(request);

        using var scope = _factory.Services.CreateScope();
        var cache = scope.ServiceProvider.GetRequiredService<SessionAccessTokenCache>();
        Assert.False(cache.TryGetFresh(sessionKey, TimeSpan.Zero, out _));
    }

    /// <summary>Keycloak unreachable at logout must not clear the cookie — "Повторить выход" needs a session to retry with.</summary>
    [Fact]
    public async Task Logout_KeycloakUnreachable_Returns503_CookieUntouched()
    {
        var client = _factory.CreateNoRedirectClient();
        var (cookie, _) = await SignInAsync(client);

        _factory.TokenResponder.CurrentMode = KeycloakTokenEndpointResponder.Mode.Unavailable;

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/session/logout")
            {
                Headers = { { "Cookie", cookie } },
            };
            var response = await client.SendAsync(request);
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();

            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
            Assert.Equal("30", response.Headers.GetValues("Retry-After").Single());
            Assert.Equal("http.service_unavailable", body.GetProperty("errorCode").GetString());
            Assert.Null(SessionTestSupport.SessionCookie(response));
        }
        finally
        {
            _factory.TokenResponder.Reset();
        }

        // The session must still be alive: /token on the same cookie still
        // authenticates once Keycloak answers again.
        var tokenResponse = await client.SendAsync(
            new HttpRequestMessage(HttpMethod.Post, "/api/session/token") { Headers = { { "Cookie", cookie } } });
        Assert.Equal(HttpStatusCode.OK, tokenResponse.StatusCode);
    }

    /// <summary>Discovery itself unreachable at logout is the same case as the token endpoint being unreachable — 503, cookie untouched, not a raw 500.</summary>
    [Fact]
    public async Task Logout_DiscoveryUnavailable_Returns503_CookieUntouched()
    {
        var client = _factory.CreateNoRedirectClient();
        var (cookie, _) = await SignInAsync(client);

        var fake = new KeycloakTestRealm.FakeRealmMetadata { CurrentMode = KeycloakTestRealm.FakeRealmMetadata.Mode.Unavailable };
        using var factory = _factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(
            services => services.AddSingleton<IConfigurationManager<OpenIdConnectConfiguration>>(fake)));
        var discoveryDownClient = factory.CreateNoRedirectClient();

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/session/logout")
        {
            Headers = { { "Cookie", cookie } },
        };
        var response = await discoveryDownClient.SendAsync(request);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("30", response.Headers.GetValues("Retry-After").Single());
        Assert.Null(SessionTestSupport.SessionCookie(response));
    }

    /// <summary>An unexpected 4xx (e.g. a client-secret mismatch) must not silently look identical to Keycloak being down — logged with the status and Keycloak's own error code, never the secret.</summary>
    [Fact]
    public async Task Logout_UnexpectedKeycloakError_Returns503_AndLogsErrorCodeWithoutSecret()
    {
        var client = _factory.CreateNoRedirectClient();
        var (cookie, _) = await SignInAsync(client);

        // The line asserted on below is written through RefreshGrantClient's ILogger<T>. Serilog
        // binds an ILogger<T> to the process-wide Serilog.Log.Logger at the moment it is created —
        // for this lazily-resolved singleton, the first logout/token request the host serves — and
        // every host disposed anywhere in the run replaces that static with a silent logger. On the
        // shared host the line was therefore swallowed or not depending on whether an earlier test
        // (Logout_DiscoveryUnavailable_..., a child host of its own) had disposed a host by then.
        // A host of this test's own is built right here, so its loggers bind to its own Log.Logger.
        // Only hosts built from Program call UseSerilog, and every class that builds one is in
        // MustaskApiCollection, which runs sequentially; the slim probe hosts outside it never touch
        // Serilog.Log.Logger. A host swaps the static asynchronously, shortly after Dispose() returns:
        // the sign-in above gives an earlier test's dispose time to land before this host is built.
        // tech-006 removes the dependency on the static logger.
        using var factory = _factory.WithWebHostBuilder(_ => { });
        var logoutClient = factory.CreateNoRedirectClient();

        _factory.TokenResponder.CurrentMode = KeycloakTokenEndpointResponder.Mode.InvalidClient;

        var originalOut = Console.Out;
        var capturedOutput = new StringWriter();
        HttpStatusCode statusCode;
        try
        {
            Console.SetOut(capturedOutput);
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/session/logout")
            {
                Headers = { { "Cookie", cookie } },
            };
            using var response = await logoutClient.SendAsync(request);
            statusCode = response.StatusCode;
        }
        finally
        {
            Console.SetOut(originalOut);
            _factory.TokenResponder.Reset();
        }

        var output = capturedOutput.ToString();

        Assert.Equal(HttpStatusCode.ServiceUnavailable, statusCode);
        Assert.Contains("invalid_client", output, StringComparison.Ordinal);
        Assert.DoesNotContain(MustaskApiFactory.TestKeycloakClientSecret, output, StringComparison.Ordinal);
    }
}
