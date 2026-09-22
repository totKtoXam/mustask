using System.Net;
using Mustask.Api.Features.Session;
using Mustask.Api.IntegrationTests.Features.Session;
using Mustask.Domain.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace Mustask.Api.IntegrationTests;

/// <summary>
/// «Токен не утекает в ответы и журналы» — захват
/// <see cref="Console.Out"/> (как <c>LoggingTests</c>) на запросах,
/// получивших 401, 403 и 503: ни тело ответа, ни консольный вывод не
/// содержат токен целиком или любой из его трёх сегментов (заголовок,
/// полезная нагрузка, подпись).
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class TokenLeakTests
{
    private const string ProtectedRoute = "/api/dictionaries/languages";

    private readonly MustaskApiFactory _factory;

    public TokenLeakTests(MustaskApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Unauthorized_TokenDoesNotLeakIntoResponseOrLogs()
    {
        // Чужой aud -> 401. Принципал при этом ещё не проверен, но
        // токен уже был предъявлен серверу целиком заголовком Authorization —
        // именно этот случай и проверяем.
        var token = KeycloakTestRealm.CreateToken(configure: t => t.Audience = "someone-elses-audience");

        await AssertNoLeakAsync(token, HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Forbidden_TokenDoesNotLeakIntoResponseOrLogs()
    {
        var token = KeycloakTestRealm.CreateToken([]);

        await AssertNoLeakAsync(token, HttpStatusCode.Forbidden);
    }

    /// <summary>
    /// «После Ready недоступность realm'а не сбрасывает
    /// готовность» — общая фабрика к этому тесту уже <c>Ready</c>, поэтому
    /// переключение <see cref="MustaskApiFactory.RealmMetadataFake"/> в
    /// <c>Unavailable</c> ничего не показало бы (guard в
    /// <c>RealmReadinessMonitor</c> держит прежнее состояние для этой
    /// причины). Нужен хост, который ещё ни разу не был <c>Ready</c> —
    /// отдельная, произведённая через <c>WithWebHostBuilder</c> фабрика со
    /// своим <c>RealmReadiness</c> (тот же приём, что в
    /// <c>RealmReadinessTests.CreateFreshFactory</c>).
    /// </summary>
    [Fact]
    public async Task ServiceUnavailable_TokenDoesNotLeakIntoResponseOrLogs()
    {
        var fake = new KeycloakTestRealm.FakeRealmMetadata { CurrentMode = KeycloakTestRealm.FakeRealmMetadata.Mode.Unavailable };
        using var factory = _factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(
            services => services.AddSingleton<IConfigurationManager<OpenIdConnectConfiguration>>(fake)));

        var token = KeycloakTestRealm.CreateToken([PermissionCatalog.DictionariesLanguagesRead]);

        await AssertNoLeakAsync(token, HttpStatusCode.ServiceUnavailable, factory);
    }

    /// <summary>
    /// This class extends to <c>/api/session/*</c> ("лог и тело ответа
    /// /api/session/* не содержат refresh_token/code/client_secret"). A
    /// successful callback exchange sends the authorization code and the
    /// confidential client's secret to the (fake) token endpoint and receives
    /// a refresh token back; none of the three may reach the response body,
    /// the `Set-Cookie` header (the ticket is encrypted, but the header is
    /// still worth asserting on directly) or the console log.
    /// </summary>
    [Fact]
    public async Task Callback_SuccessfulExchange_DoesNotLeakCodeRefreshTokenOrClientSecret()
    {
        var client = _factory.CreateNoRedirectClient();
        var challenge = await SessionTestSupport.ChallengeAsync(client, "/dashboard");
        var sid = Guid.CreateVersion7().ToString();
        const string refreshToken = "leak-probe-refresh-token";
        const string authorizationCode = "leak-probe-authorization-code";

        _factory.TokenResponder.OkResponseBodyFactory = () => KeycloakTokenEndpointResponder.BuildBody(
            accessToken: KeycloakTestRealm.CreateToken(),
            idToken: KeycloakTokenEndpointResponder.BuildIdToken(sid, challenge.Nonce),
            refreshToken: refreshToken,
            expiresIn: 300,
            refreshExpiresIn: 1800);

        var originalOut = Console.Out;
        var capturedOutput = new StringWriter();
        string body;
        string? setCookie;
        try
        {
            Console.SetOut(capturedOutput);
            using var request = new HttpRequestMessage(
                HttpMethod.Get, $"/api/session/callback?code={authorizationCode}&state={Uri.EscapeDataString(challenge.State)}");
            SessionTestSupport.AttachCookies(request, challenge.Cookies);
            using var response = await client.SendAsync(request);
            body = await response.Content.ReadAsStringAsync();
            setCookie = SessionTestSupport.SessionCookie(response);
        }
        finally
        {
            Console.SetOut(originalOut);
            _factory.TokenResponder.Reset();
        }

        var output = capturedOutput.ToString();

        foreach (var secret in new[] { refreshToken, authorizationCode, MustaskApiFactory.TestKeycloakClientSecret })
        {
            Assert.DoesNotContain(secret, body, StringComparison.Ordinal);
            Assert.DoesNotContain(secret, output, StringComparison.Ordinal);
            Assert.DoesNotContain(secret, setCookie ?? string.Empty, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// Same requirement as above — the renewal path:
    /// the stale refresh token pulled out of the cookie and the confidential
    /// client's secret go out over the wire to redeem a new token set, and
    /// Keycloak rejects the grant. Neither the refresh token nor the secret
    /// should show up in the 401 body or the console log.
    /// </summary>
    [Fact]
    public async Task Token_InvalidGrant_DoesNotLeakRefreshTokenOrClientSecret()
    {
        var client = _factory.CreateNoRedirectClient();
        var challenge = await SessionTestSupport.ChallengeAsync(client, "/dashboard");
        var sid = Guid.CreateVersion7().ToString();
        const string refreshToken = "leak-probe-refresh-token-2";

        _factory.TokenResponder.OkResponseBodyFactory = () => KeycloakTokenEndpointResponder.BuildBody(
            accessToken: KeycloakTestRealm.CreateToken(),
            idToken: KeycloakTokenEndpointResponder.BuildIdToken(sid, challenge.Nonce),
            refreshToken: refreshToken,
            expiresIn: 300,
            refreshExpiresIn: 1800);

        using var signInRequest = new HttpRequestMessage(
            HttpMethod.Get, $"/api/session/callback?code=test-code&state={Uri.EscapeDataString(challenge.State)}");
        SessionTestSupport.AttachCookies(signInRequest, challenge.Cookies);
        using var signInResponse = await client.SendAsync(signInRequest);
        _factory.TokenResponder.Reset();
        var rawSignInCookie = SessionTestSupport.SessionCookie(signInResponse)
            ?? throw new InvalidOperationException("Sign-in did not set mustask.session.");
        var cookie = rawSignInCookie.Split(';')[0];
        var sessionKey = SessionTestSupport.SessionKey(_factory, rawSignInCookie);

        using (var scope = _factory.Services.CreateScope())
        {
            scope.ServiceProvider.GetRequiredService<SessionAccessTokenCache>().Set(sessionKey, "stale", DateTimeOffset.UtcNow);
        }

        _factory.TokenResponder.CurrentMode = KeycloakTokenEndpointResponder.Mode.InvalidGrant;

        var originalOut = Console.Out;
        var capturedOutput = new StringWriter();
        string body;
        try
        {
            Console.SetOut(capturedOutput);
            using var tokenRequest = new HttpRequestMessage(HttpMethod.Post, "/api/session/token")
            {
                Headers = { { "Cookie", cookie } },
            };
            using var response = await client.SendAsync(tokenRequest);
            body = await response.Content.ReadAsStringAsync();
        }
        finally
        {
            Console.SetOut(originalOut);
            _factory.TokenResponder.Reset();
        }

        var output = capturedOutput.ToString();

        foreach (var secret in new[] { refreshToken, MustaskApiFactory.TestKeycloakClientSecret })
        {
            Assert.DoesNotContain(secret, body, StringComparison.Ordinal);
            Assert.DoesNotContain(secret, output, StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// A successful renewal both reads the
    /// cookie's current refresh token and receives a rotated refresh token
    /// and a new id token back from Keycloak — none of the three, nor the
    /// client secret sent alongside them, may reach the response body or the
    /// console log. The access token IS expected in the body (that is the
    /// whole point of <c>POST /api/session/token</c>) — this test does not
    /// assert its absence.
    /// </summary>
    [Fact]
    public async Task Token_SuccessfulRenewal_DoesNotLeakRefreshOrIdTokenOrClientSecret()
    {
        var client = _factory.CreateNoRedirectClient();
        var challenge = await SessionTestSupport.ChallengeAsync(client, "/dashboard");
        var sid = Guid.CreateVersion7().ToString();
        const string initialRefreshToken = "leak-probe-renewal-initial-refresh-token";

        _factory.TokenResponder.OkResponseBodyFactory = () => KeycloakTokenEndpointResponder.BuildBody(
            accessToken: KeycloakTestRealm.CreateToken(),
            idToken: KeycloakTokenEndpointResponder.BuildIdToken(sid, challenge.Nonce),
            refreshToken: initialRefreshToken,
            expiresIn: 300,
            refreshExpiresIn: 1800);

        using var signInRequest = new HttpRequestMessage(
            HttpMethod.Get, $"/api/session/callback?code=test-code&state={Uri.EscapeDataString(challenge.State)}");
        SessionTestSupport.AttachCookies(signInRequest, challenge.Cookies);
        using var signInResponse = await client.SendAsync(signInRequest);
        _factory.TokenResponder.Reset();
        var rawSignInCookie = SessionTestSupport.SessionCookie(signInResponse)
            ?? throw new InvalidOperationException("Sign-in did not set mustask.session.");
        var cookie = rawSignInCookie.Split(';')[0];
        var sessionKey = SessionTestSupport.SessionKey(_factory, rawSignInCookie);

        using (var scope = _factory.Services.CreateScope())
        {
            scope.ServiceProvider.GetRequiredService<SessionAccessTokenCache>().Set(sessionKey, "stale", DateTimeOffset.UtcNow);
        }

        const string renewedRefreshToken = "leak-probe-renewal-rotated-refresh-token";
        var renewedIdToken = KeycloakTokenEndpointResponder.BuildIdToken(sid, nonce: null);
        _factory.TokenResponder.OkResponseBodyFactory = () => KeycloakTokenEndpointResponder.BuildBody(
            accessToken: KeycloakTestRealm.CreateToken(),
            idToken: renewedIdToken,
            refreshToken: renewedRefreshToken,
            expiresIn: 300,
            refreshExpiresIn: 1800);

        var originalOut = Console.Out;
        var capturedOutput = new StringWriter();
        string body;
        try
        {
            Console.SetOut(capturedOutput);
            using var tokenRequest = new HttpRequestMessage(HttpMethod.Post, "/api/session/token")
            {
                Headers = { { "Cookie", cookie } },
            };
            using var response = await client.SendAsync(tokenRequest);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            body = await response.Content.ReadAsStringAsync();
        }
        finally
        {
            Console.SetOut(originalOut);
            _factory.TokenResponder.Reset();
        }

        var output = capturedOutput.ToString();

        foreach (var secret in new[] { initialRefreshToken, renewedRefreshToken, MustaskApiFactory.TestKeycloakClientSecret })
        {
            Assert.DoesNotContain(secret, body, StringComparison.Ordinal);
            Assert.DoesNotContain(secret, output, StringComparison.Ordinal);
        }

        Assert.DoesNotContain(renewedIdToken, body, StringComparison.Ordinal);
        Assert.DoesNotContain(renewedIdToken, output, StringComparison.Ordinal);

        // Only the payload and signature segments — not the header (index 0):
        // KeycloakTestRealm signs every token with the same default "kid" by
        // default, so the header segment (base64 of {"alg":"RS256","kid":"…"})
        // is byte-for-byte identical between the id token and the access
        // token this same response legitimately returns in the body. That
        // match is not a leak — it carries no session-specific information —
        // and asserting on it would make this test fail on the access token
        // it is supposed to ignore.
        foreach (var segment in renewedIdToken.Split('.').Skip(1))
        {
            Assert.DoesNotContain(segment, body, StringComparison.Ordinal);
            Assert.DoesNotContain(segment, output, StringComparison.Ordinal);
        }
    }

    /// <summary>A successful logout reads the cookie's refresh token and sends it (plus the client secret) to the back-channel logout — neither may reach the response body or the console log.</summary>
    [Fact]
    public async Task Logout_Success_DoesNotLeakRefreshTokenOrClientSecret()
    {
        var client = _factory.CreateNoRedirectClient();
        var challenge = await SessionTestSupport.ChallengeAsync(client, "/dashboard");
        var sid = Guid.CreateVersion7().ToString();
        const string refreshToken = "leak-probe-logout-success-refresh-token";

        _factory.TokenResponder.OkResponseBodyFactory = () => KeycloakTokenEndpointResponder.BuildBody(
            accessToken: KeycloakTestRealm.CreateToken(),
            idToken: KeycloakTokenEndpointResponder.BuildIdToken(sid, challenge.Nonce),
            refreshToken: refreshToken,
            expiresIn: 300,
            refreshExpiresIn: 1800);

        using var signInRequest = new HttpRequestMessage(
            HttpMethod.Get, $"/api/session/callback?code=test-code&state={Uri.EscapeDataString(challenge.State)}");
        SessionTestSupport.AttachCookies(signInRequest, challenge.Cookies);
        using var signInResponse = await client.SendAsync(signInRequest);
        _factory.TokenResponder.Reset();
        var rawSignInCookie = SessionTestSupport.SessionCookie(signInResponse)
            ?? throw new InvalidOperationException("Sign-in did not set mustask.session.");
        var cookie = rawSignInCookie.Split(';')[0];

        var originalOut = Console.Out;
        var capturedOutput = new StringWriter();
        string body;
        try
        {
            Console.SetOut(capturedOutput);
            using var logoutRequest = new HttpRequestMessage(HttpMethod.Post, "/api/session/logout")
            {
                Headers = { { "Cookie", cookie } },
            };
            using var response = await client.SendAsync(logoutRequest);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            body = await response.Content.ReadAsStringAsync();
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        var output = capturedOutput.ToString();

        foreach (var secret in new[] { refreshToken, MustaskApiFactory.TestKeycloakClientSecret })
        {
            Assert.DoesNotContain(secret, body, StringComparison.Ordinal);
            Assert.DoesNotContain(secret, output, StringComparison.Ordinal);
        }
    }

    /// <summary>The same as above, on the path where Keycloak's back channel refuses the logout request (503, cookie untouched) — the refresh token read from the cookie and the client secret sent with it still must not reach the response body or the console log.</summary>
    [Fact]
    public async Task Logout_KeycloakUnavailable_DoesNotLeakRefreshTokenOrClientSecret()
    {
        var client = _factory.CreateNoRedirectClient();
        var challenge = await SessionTestSupport.ChallengeAsync(client, "/dashboard");
        var sid = Guid.CreateVersion7().ToString();
        const string refreshToken = "leak-probe-logout-503-refresh-token";

        _factory.TokenResponder.OkResponseBodyFactory = () => KeycloakTokenEndpointResponder.BuildBody(
            accessToken: KeycloakTestRealm.CreateToken(),
            idToken: KeycloakTokenEndpointResponder.BuildIdToken(sid, challenge.Nonce),
            refreshToken: refreshToken,
            expiresIn: 300,
            refreshExpiresIn: 1800);

        using var signInRequest = new HttpRequestMessage(
            HttpMethod.Get, $"/api/session/callback?code=test-code&state={Uri.EscapeDataString(challenge.State)}");
        SessionTestSupport.AttachCookies(signInRequest, challenge.Cookies);
        using var signInResponse = await client.SendAsync(signInRequest);
        _factory.TokenResponder.Reset();
        var rawSignInCookie = SessionTestSupport.SessionCookie(signInResponse)
            ?? throw new InvalidOperationException("Sign-in did not set mustask.session.");
        var cookie = rawSignInCookie.Split(';')[0];

        _factory.TokenResponder.CurrentMode = KeycloakTokenEndpointResponder.Mode.Unavailable;

        var originalOut = Console.Out;
        var capturedOutput = new StringWriter();
        string body;
        try
        {
            Console.SetOut(capturedOutput);
            using var logoutRequest = new HttpRequestMessage(HttpMethod.Post, "/api/session/logout")
            {
                Headers = { { "Cookie", cookie } },
            };
            using var response = await client.SendAsync(logoutRequest);
            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
            body = await response.Content.ReadAsStringAsync();
        }
        finally
        {
            Console.SetOut(originalOut);
            _factory.TokenResponder.Reset();
        }

        var output = capturedOutput.ToString();

        foreach (var secret in new[] { refreshToken, MustaskApiFactory.TestKeycloakClientSecret })
        {
            Assert.DoesNotContain(secret, body, StringComparison.Ordinal);
            Assert.DoesNotContain(secret, output, StringComparison.Ordinal);
        }
    }

    /// <summary>A failed callback (Keycloak rejects the code exchange) still sent the authorization code and the client secret over the wire — neither may reach the redirect response or the console log.</summary>
    [Fact]
    public async Task Callback_Failed_DoesNotLeakAuthorizationCodeOrClientSecret()
    {
        var client = _factory.CreateNoRedirectClient();
        var challenge = await SessionTestSupport.ChallengeAsync(client, "/dashboard");
        const string authorizationCode = "leak-probe-failed-callback-authorization-code";

        _factory.TokenResponder.CurrentMode = KeycloakTokenEndpointResponder.Mode.InvalidGrant;

        var originalOut = Console.Out;
        var capturedOutput = new StringWriter();
        string body;
        try
        {
            Console.SetOut(capturedOutput);
            using var request = new HttpRequestMessage(
                HttpMethod.Get, $"/api/session/callback?code={authorizationCode}&state={Uri.EscapeDataString(challenge.State)}");
            SessionTestSupport.AttachCookies(request, challenge.Cookies);
            using var response = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            body = await response.Content.ReadAsStringAsync();
        }
        finally
        {
            Console.SetOut(originalOut);
            _factory.TokenResponder.Reset();
        }

        var output = capturedOutput.ToString();

        foreach (var secret in new[] { authorizationCode, MustaskApiFactory.TestKeycloakClientSecret })
        {
            Assert.DoesNotContain(secret, body, StringComparison.Ordinal);
            Assert.DoesNotContain(secret, output, StringComparison.Ordinal);
        }
    }

    private async Task AssertNoLeakAsync(string token, HttpStatusCode expectedStatus, WebApplicationFactory<Program>? factoryOverride = null)
    {
        var originalOut = Console.Out;
        var capturedOutput = new StringWriter();
        Console.SetOut(capturedOutput);

        string body;
        HttpStatusCode actualStatus;
        try
        {
            var client = (factoryOverride ?? _factory).CreateClient();
            var response = await client.SendAsync(
                new HttpRequestMessage(HttpMethod.Get, ProtectedRoute).WithBearer(token));
            actualStatus = response.StatusCode;
            body = await response.Content.ReadAsStringAsync();
        }
        finally
        {
            Console.SetOut(originalOut);
        }

        var output = capturedOutput.ToString();
        Assert.Equal(expectedStatus, actualStatus);

        Assert.DoesNotContain(token, body, StringComparison.Ordinal);
        Assert.DoesNotContain(token, output, StringComparison.Ordinal);

        foreach (var segment in token.Split('.'))
        {
            Assert.DoesNotContain(segment, body, StringComparison.Ordinal);
            Assert.DoesNotContain(segment, output, StringComparison.Ordinal);
        }
    }
}
