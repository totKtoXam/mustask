using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Mustask.Api.Features.Session;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace Mustask.Api.IntegrationTests.Features.Session;

/// <summary>
/// <c>POST /api/session/token</c>.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class SessionTokenEndpointTests
{
    private readonly MustaskApiFactory _factory;

    public SessionTokenEndpointTests(MustaskApiFactory factory)
    {
        _factory = factory;
    }

    /// <summary>
    /// Signs in for real through <c>/login</c> → <c>/callback</c> (the only
    /// way to get a genuine, decryptable <c>mustask.session</c> cookie) and
    /// returns its raw <c>name=value</c> pair, the <c>sid</c> baked into the
    /// id token (needed so a later renewal's id token can deliberately match
    /// or mismatch it), and the internal session key that actually
    /// keys <see cref="SessionAccessTokenCache"/>/<see cref="SessionRenewalLock"/>
    /// (not <c>sid</c> itself). <paramref name="factory"/>
    /// defaults to the shared collection fixture; the key-rotation tests below pass
    /// their own <c>WithWebHostBuilder</c>-derived factory instead (own
    /// <see cref="KeycloakTestRealm.FakeRealmMetadata"/>, so the shared
    /// readiness monitor's background ticks cannot affect them) — the
    /// cookie it hands back still decrypts against that factory, since
    /// <c>WithWebHostBuilder</c> only adds to the base factory's own
    /// <c>ConfigureWebHost</c>, it does not replace it (same assumption
    /// <c>SessionCallbackEndpointTests.Callback_DiscoveryNeverObtained_RedirectsWithIdpUnavailable</c>
    /// already relies on).
    /// </summary>
    private async Task<(string Cookie, string Sid, string SessionKey, string AccessToken)> SignInAsync(
        HttpClient client, string accessToken, string refreshToken = "initial-refresh-token", int? refreshExpiresIn = 1800,
        WebApplicationFactory<Program>? factory = null)
    {
        var challenge = await SessionTestSupport.ChallengeAsync(client);
        var sid = Guid.CreateVersion7().ToString();

        _factory.TokenResponder.OkResponseBodyFactory = () => KeycloakTokenEndpointResponder.BuildBody(
            accessToken: accessToken,
            idToken: KeycloakTokenEndpointResponder.BuildIdToken(sid, challenge.Nonce),
            refreshToken: refreshToken,
            expiresIn: 300,
            refreshExpiresIn: refreshExpiresIn);

        using var request = new HttpRequestMessage(
            HttpMethod.Get, $"/api/session/callback?code=test-code&state={Uri.EscapeDataString(challenge.State)}");
        SessionTestSupport.AttachCookies(request, challenge.Cookies);
        using var response = await client.SendAsync(request);

        _factory.TokenResponder.Reset();

        var cookie = SessionTestSupport.SessionCookie(response) ?? throw new InvalidOperationException("Sign-in did not set mustask.session.");
        var cookiePair = cookie.Split(';')[0];
        var sessionKey = SessionTestSupport.SessionKey(factory ?? _factory, cookie);
        return (cookiePair, sid, sessionKey, accessToken);
    }

    private static HttpRequestMessage TokenRequest(string cookie) =>
        new(HttpMethod.Post, "/api/session/token") { Headers = { { "Cookie", cookie } } };

    [Fact]
    public async Task Token_NoCookie_Returns401Anonymous()
    {
        var client = _factory.CreateNoRedirectClient();

        var response = await client.PostAsync("/api/session/token", null);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal(SessionErrorCodes.Anonymous, body.GetProperty("errorCode").GetString());
    }

    [Fact]
    public async Task Token_FreshCache_ReturnsCachedTokenWithoutCallingTokenEndpoint()
    {
        var client = _factory.CreateNoRedirectClient();
        var accessToken = KeycloakTestRealm.CreateToken(["dictionaries.languages.read"]);
        var (cookie, _, _, _) = await SignInAsync(client, accessToken);

        _factory.TokenResponder.Reset();
        var callsBefore = _factory.TokenResponder.TokenRequestCount;

        var response = await client.SendAsync(TokenRequest(cookie));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(accessToken, body.GetProperty("accessToken").GetString());
        Assert.Equal(callsBefore, _factory.TokenResponder.TokenRequestCount);
    }

    /// <summary>The panel schedules renewal from this, not from comparing "expiresAt" against its own wall clock.</summary>
    [Fact]
    public async Task Token_FreshCache_ResponseIncludesExpiresInSeconds()
    {
        var client = _factory.CreateNoRedirectClient();
        var (cookie, _, _, _) = await SignInAsync(client, KeycloakTestRealm.CreateToken());

        var response = await client.SendAsync(TokenRequest(cookie));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var expiresIn = body.GetProperty("expiresIn").GetInt32();
        // SignInAsync cached a 300 s access token a moment ago.
        Assert.InRange(expiresIn, 290, 300);
    }

    [Fact]
    public async Task Token_CacheExpired_RenewsOnce_ReturnsNewTokenAndRewritesCookie()
    {
        var client = _factory.CreateNoRedirectClient();
        var (cookie, sid, sessionKey, _) = await SignInAsync(client, KeycloakTestRealm.CreateToken());

        // Force the cached entry stale — the sign-in already cached a fresh
        // one with a 300 s expiry, well past the 60 s freshness threshold.
        using (var scope = _factory.Services.CreateScope())
        {
            var cache = scope.ServiceProvider.GetRequiredService<SessionAccessTokenCache>();
            cache.Set(sessionKey, "stale-access-token", DateTimeOffset.UtcNow);
        }

        var renewedAccessToken = KeycloakTestRealm.CreateToken(["dictionaries.languages.write"]);
        _factory.TokenResponder.OkResponseBodyFactory = () => KeycloakTokenEndpointResponder.BuildBody(
            accessToken: renewedAccessToken,
            idToken: KeycloakTokenEndpointResponder.BuildIdToken(sid, nonce: null),
            refreshToken: "rotated-refresh-token",
            expiresIn: 300,
            refreshExpiresIn: 1800);

        try
        {
            var callsBefore = _factory.TokenResponder.TokenRequestCount;
            var response = await client.SendAsync(TokenRequest(cookie));
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(renewedAccessToken, body.GetProperty("accessToken").GetString());
            Assert.Equal(callsBefore + 1, _factory.TokenResponder.TokenRequestCount);
            Assert.NotNull(SessionTestSupport.SessionCookie(response));
        }
        finally
        {
            _factory.TokenResponder.Reset();
        }
    }

    [Fact]
    public async Task Token_InvalidGrant_Returns401Expired_AndClearsCookie()
    {
        var client = _factory.CreateNoRedirectClient();
        var (cookie, _, sessionKey, _) = await SignInAsync(client, KeycloakTestRealm.CreateToken());

        using (var scope = _factory.Services.CreateScope())
        {
            scope.ServiceProvider.GetRequiredService<SessionAccessTokenCache>().Set(sessionKey, "stale", DateTimeOffset.UtcNow);
        }

        _factory.TokenResponder.CurrentMode = KeycloakTokenEndpointResponder.Mode.InvalidGrant;

        try
        {
            var response = await client.SendAsync(TokenRequest(cookie));
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.Equal(SessionErrorCodes.Expired, body.GetProperty("errorCode").GetString());

            var clearedCookie = SessionTestSupport.SessionCookie(response);
            Assert.NotNull(clearedCookie);
            // A deleted cookie is a Set-Cookie with an Expires date in the
            // past (ASP.NET Core's ResponseCookies.Delete uses the Unix
            // epoch) — checking for the year is format-agnostic enough to
            // survive a header-rendering detail changing.
            Assert.Contains("1970", clearedCookie, StringComparison.OrdinalIgnoreCase);

            // An invalid_grant must not leave a cached
            // access token behind for a session that just died.
            using var scope = _factory.Services.CreateScope();
            var cache = scope.ServiceProvider.GetRequiredService<SessionAccessTokenCache>();
            Assert.False(cache.TryGetFresh(sessionKey, TimeSpan.Zero, out _));
        }
        finally
        {
            _factory.TokenResponder.Reset();
        }
    }

    [Fact]
    public async Task Token_TokenEndpointUnavailable_Returns503WithRetryAfter_CookieUntouched()
    {
        var client = _factory.CreateNoRedirectClient();
        var (cookie, _, sessionKey, _) = await SignInAsync(client, KeycloakTestRealm.CreateToken());

        using (var scope = _factory.Services.CreateScope())
        {
            scope.ServiceProvider.GetRequiredService<SessionAccessTokenCache>().Set(sessionKey, "stale", DateTimeOffset.UtcNow);
        }

        _factory.TokenResponder.CurrentMode = KeycloakTokenEndpointResponder.Mode.Unavailable;

        try
        {
            var response = await client.SendAsync(TokenRequest(cookie));
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
    }

    /// <summary>ADR-0022 Decision 5: N concurrent renewals with one stale cache produce exactly one call to the token endpoint.</summary>
    [Fact]
    public async Task Token_ConcurrentRequestsWithStaleCache_CallsTokenEndpointExactlyOnce()
    {
        var client = _factory.CreateNoRedirectClient();
        var (cookie, sid, sessionKey, _) = await SignInAsync(client, KeycloakTestRealm.CreateToken());

        using (var scope = _factory.Services.CreateScope())
        {
            scope.ServiceProvider.GetRequiredService<SessionAccessTokenCache>().Set(sessionKey, "stale", DateTimeOffset.UtcNow);
        }

        var renewedAccessToken = KeycloakTestRealm.CreateToken();
        _factory.TokenResponder.OkResponseBodyFactory = () => KeycloakTokenEndpointResponder.BuildBody(
            accessToken: renewedAccessToken,
            idToken: KeycloakTokenEndpointResponder.BuildIdToken(sid, nonce: null),
            refreshToken: "rotated-refresh-token-concurrent",
            expiresIn: 300,
            refreshExpiresIn: 1800);

        try
        {
            var callsBefore = _factory.TokenResponder.TokenRequestCount;

            var responses = await Task.WhenAll(Enumerable.Range(0, 5)
                .Select(_ => client.SendAsync(TokenRequest(cookie))));

            Assert.All(responses, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
            Assert.Equal(callsBefore + 1, _factory.TokenResponder.TokenRequestCount);
        }
        finally
        {
            _factory.TokenResponder.Reset();
        }
    }

    /// <summary>
    /// Before this fix, an id token with no "sid" meant
    /// <c>OnTicketReceived</c> never cached the access token AND
    /// <c>TokenAsync</c> hard-required "sid" to even look at the cache —
    /// together, an already-signed-in user with no "sid" claim got 401
    /// session.anonymous forever, which the panel's own auto-retry then
    /// turned into a silent sign-in loop.
    /// </summary>
    [Fact]
    public async Task Token_IdTokenHasNoSid_SignInStillCachesAndTokenStillWorks()
    {
        var client = _factory.CreateNoRedirectClient();
        var challenge = await SessionTestSupport.ChallengeAsync(client);
        var accessToken = KeycloakTestRealm.CreateToken();

        _factory.TokenResponder.OkResponseBodyFactory = () => KeycloakTokenEndpointResponder.BuildBody(
            accessToken: accessToken,
            idToken: KeycloakTestRealm.CreateToken(configure: t =>
            {
                t.Audience = SessionOidcOptions.ClientId;
                t.Permissions = null;
                t.Nonce = challenge.Nonce;
                // t.Sid deliberately left null.
            }),
            refreshToken: "no-sid-refresh-token",
            expiresIn: 300,
            refreshExpiresIn: 1800);

        using var callbackRequest = new HttpRequestMessage(
            HttpMethod.Get, $"/api/session/callback?code=test-code&state={Uri.EscapeDataString(challenge.State)}");
        SessionTestSupport.AttachCookies(callbackRequest, challenge.Cookies);
        using var callbackResponse = await client.SendAsync(callbackRequest);
        _factory.TokenResponder.Reset();

        var cookie = SessionTestSupport.SessionCookie(callbackResponse)
            ?? throw new InvalidOperationException("Sign-in did not set mustask.session.");

        var response = await client.SendAsync(TokenRequest(cookie.Split(';')[0]));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(accessToken, body.GetProperty("accessToken").GetString());
    }

    /// <summary>A renewed id token whose "sub" does not match the ticket's is treated exactly like invalid_grant.</summary>
    [Fact]
    public async Task Token_RenewedIdTokenHasDifferentSub_Returns401Expired_AndClearsCookieAndCache()
    {
        var client = _factory.CreateNoRedirectClient();
        var (cookie, sid, sessionKey, _) = await SignInAsync(client, KeycloakTestRealm.CreateToken());

        using (var scope = _factory.Services.CreateScope())
        {
            scope.ServiceProvider.GetRequiredService<SessionAccessTokenCache>().Set(sessionKey, "stale", DateTimeOffset.UtcNow);
        }

        _factory.TokenResponder.OkResponseBodyFactory = () => KeycloakTokenEndpointResponder.BuildBody(
            accessToken: KeycloakTestRealm.CreateToken(),
            idToken: KeycloakTokenEndpointResponder.BuildIdToken(sid, nonce: null, sub: "a-completely-different-subject"),
            refreshToken: "rotated-refresh-token-different-sub",
            expiresIn: 300,
            refreshExpiresIn: 1800);

        try
        {
            var response = await client.SendAsync(TokenRequest(cookie));
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.Equal(SessionErrorCodes.Expired, body.GetProperty("errorCode").GetString());

            var clearedCookie = SessionTestSupport.SessionCookie(response);
            Assert.NotNull(clearedCookie);
            Assert.Contains("1970", clearedCookie, StringComparison.OrdinalIgnoreCase);

            using var scope = _factory.Services.CreateScope();
            Assert.False(scope.ServiceProvider.GetRequiredService<SessionAccessTokenCache>()
                .TryGetFresh(sessionKey, TimeSpan.Zero, out _));
        }
        finally
        {
            _factory.TokenResponder.Reset();
        }
    }

    /// <summary>OIDC Core §12.2: a renewed id token with a different "sid" than the ticket's is rejected the same way.</summary>
    [Fact]
    public async Task Token_RenewedIdTokenHasDifferentSid_Returns401Expired()
    {
        var client = _factory.CreateNoRedirectClient();
        var (cookie, _, sessionKey, _) = await SignInAsync(client, KeycloakTestRealm.CreateToken());

        using (var scope = _factory.Services.CreateScope())
        {
            scope.ServiceProvider.GetRequiredService<SessionAccessTokenCache>().Set(sessionKey, "stale", DateTimeOffset.UtcNow);
        }

        _factory.TokenResponder.OkResponseBodyFactory = () => KeycloakTokenEndpointResponder.BuildBody(
            accessToken: KeycloakTestRealm.CreateToken(),
            idToken: KeycloakTokenEndpointResponder.BuildIdToken(Guid.CreateVersion7().ToString(), nonce: null),
            refreshToken: "rotated-refresh-token-different-sid",
            expiresIn: 300,
            refreshExpiresIn: 1800);

        try
        {
            var response = await client.SendAsync(TokenRequest(cookie));
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.Equal(SessionErrorCodes.Expired, body.GetProperty("errorCode").GetString());
        }
        finally
        {
            _factory.TokenResponder.Reset();
        }
    }

    /// <summary>A renewed id token signed with a key not in the realm's JWKS is rejected — the renewal path validated nothing about the id token before this fix.</summary>
    [Fact]
    public async Task Token_RenewedIdTokenSignedWithUnknownKey_Returns401Expired()
    {
        var client = _factory.CreateNoRedirectClient();
        var (cookie, sid, sessionKey, _) = await SignInAsync(client, KeycloakTestRealm.CreateToken());

        using (var scope = _factory.Services.CreateScope())
        {
            scope.ServiceProvider.GetRequiredService<SessionAccessTokenCache>().Set(sessionKey, "stale", DateTimeOffset.UtcNow);
        }

        _factory.TokenResponder.OkResponseBodyFactory = () => KeycloakTokenEndpointResponder.BuildBody(
            accessToken: KeycloakTestRealm.CreateToken(),
            idToken: KeycloakTestRealm.CreateToken(configure: t =>
            {
                t.Audience = SessionOidcOptions.ClientId;
                t.Permissions = null;
                t.Sid = sid;
                t.Kid = Guid.NewGuid().ToString("N");
            }),
            refreshToken: "rotated-refresh-token-unknown-kid",
            expiresIn: 300,
            refreshExpiresIn: 1800);

        try
        {
            var response = await client.SendAsync(TokenRequest(cookie));
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.Equal(SessionErrorCodes.Expired, body.GetProperty("errorCode").GetString());
        }
        finally
        {
            _factory.TokenResponder.Reset();
        }
    }

    /// <summary>
    /// Pins a known, accepted
    /// limitation — not a bug fixed here. <see cref="RenewedIdTokenValidator"/>
    /// (:59-64) calls <c>RequestRefresh()</c> once and retries once, but the
    /// real <c>ConfigurationManager&lt;OpenIdConnectConfiguration&gt;</c>
    /// (Microsoft.IdentityModel.Protocols.OpenIdConnect 8.19.2) only
    /// schedules that refresh in the background — the immediate retry below
    /// still sees the pre-rotation keys, exactly like the fake here
    /// (<see cref="KeycloakTestRealm.FakeRealmMetadata.OkConfigurationAfterRefresh"/>
    /// stays hidden until <see cref="KeycloakTestRealm.FakeRealmMetadata.CompleteBackgroundRefresh"/>
    /// is called, which this test deliberately never does). The renewal
    /// therefore ends in 401 <c>session.expired</c> — a session
    /// Keycloak itself still considers live. Uses its own fresh
    /// <see cref="KeycloakTestRealm.FakeRealmMetadata"/> via
    /// <c>WithWebHostBuilder</c> (same technique as
    /// <c>RealmReadinessTests.CreateFreshFactory</c>) rather than the
    /// collection's shared <see cref="MustaskApiFactory.RealmMetadataFake"/>, so
    /// the shared <c>RealmReadinessMonitor</c>'s own periodic
    /// <c>RequestRefresh()</c> calls on that shared fake cannot occasionally complete this test's refresh for
    /// it and turn a real assertion into a flaky one.
    /// </summary>
    [Fact]
    public async Task Token_RenewedIdTokenSignedWithKeyAddedAfterRotation_ImmediateRetryStillStale_Returns401Expired_KnownIssueN5()
    {
        var fake = new KeycloakTestRealm.FakeRealmMetadata();
        using var factory = _factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(
            services => services.AddSingleton<IConfigurationManager<OpenIdConnectConfiguration>>(fake)));
        var client = factory.CreateNoRedirectClient();

        var (cookie, sid, sessionKey, _) = await SignInAsync(client, KeycloakTestRealm.CreateToken(), factory: factory);

        using (var scope = factory.Services.CreateScope())
        {
            scope.ServiceProvider.GetRequiredService<SessionAccessTokenCache>().Set(sessionKey, "stale", DateTimeOffset.UtcNow);
        }

        var newKid = Guid.CreateVersion7().ToString("N");
        fake.OkConfigurationAfterRefresh = KeycloakTestRealm.ConfigurationWithAdditionalKid(newKid);

        _factory.TokenResponder.OkResponseBodyFactory = () => KeycloakTokenEndpointResponder.BuildBody(
            accessToken: KeycloakTestRealm.CreateToken(),
            idToken: KeycloakTestRealm.CreateToken(configure: t =>
            {
                t.Audience = SessionOidcOptions.ClientId;
                t.Permissions = null;
                t.Sid = sid;
                t.Subject = KeycloakTokenEndpointResponder.DefaultSubject;
                t.Kid = newKid;
            }),
            refreshToken: "rotated-refresh-token-new-kid",
            expiresIn: 300,
            refreshExpiresIn: 1800);

        try
        {
            var response = await client.SendAsync(TokenRequest(cookie));
            var body = await response.Content.ReadFromJsonAsync<JsonElement>();

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.Equal(SessionErrorCodes.Expired, body.GetProperty("errorCode").GetString());
            // One retry was attempted (RenewedIdTokenValidator did call
            // RequestRefresh() and re-validate once) — it just did not help,
            // because the fake never "completed" it.
            Assert.Equal(1, fake.RefreshRequestCount);
        }
        finally
        {
            _factory.TokenResponder.Reset();
        }
    }

    /// <summary>
    /// The honest counterpart of the test
    /// above — once the real <c>ConfigurationManager</c>'s background fetch
    /// has actually completed (here, the test drives that step directly via
    /// <see cref="KeycloakTestRealm.FakeRealmMetadata.CompleteBackgroundRefresh"/>,
    /// deterministically, instead of sleeping ~300 ms), a renewal with the
    /// rotated key succeeds — on the very first validation, no retry needed
    /// at all, because <see cref="RenewedIdTokenValidator"/>'s first
    /// <c>GetConfigurationAsync()</c> call already sees the new key. Same
    /// fresh-factory isolation as the test above, for the same reason.
    /// </summary>
    [Fact]
    public async Task Token_RenewedIdTokenSignedWithKeyAddedAfterRotation_OnceBackgroundRefreshCompletes_Returns200()
    {
        var fake = new KeycloakTestRealm.FakeRealmMetadata();
        using var factory = _factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(
            services => services.AddSingleton<IConfigurationManager<OpenIdConnectConfiguration>>(fake)));
        var client = factory.CreateNoRedirectClient();

        var (cookie, sid, sessionKey, _) = await SignInAsync(client, KeycloakTestRealm.CreateToken(), factory: factory);

        using (var scope = factory.Services.CreateScope())
        {
            scope.ServiceProvider.GetRequiredService<SessionAccessTokenCache>().Set(sessionKey, "stale", DateTimeOffset.UtcNow);
        }

        var newKid = Guid.CreateVersion7().ToString("N");
        fake.OkConfigurationAfterRefresh = KeycloakTestRealm.ConfigurationWithAdditionalKid(newKid);
        fake.CompleteBackgroundRefresh();

        _factory.TokenResponder.OkResponseBodyFactory = () => KeycloakTokenEndpointResponder.BuildBody(
            accessToken: KeycloakTestRealm.CreateToken(),
            idToken: KeycloakTestRealm.CreateToken(configure: t =>
            {
                t.Audience = SessionOidcOptions.ClientId;
                t.Permissions = null;
                t.Sid = sid;
                t.Subject = KeycloakTokenEndpointResponder.DefaultSubject;
                t.Kid = newKid;
            }),
            refreshToken: "rotated-refresh-token-new-kid",
            expiresIn: 300,
            refreshExpiresIn: 1800);

        try
        {
            var response = await client.SendAsync(TokenRequest(cookie));

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            // The rotated key was already visible on the first check — no
            // SecurityTokenSignatureKeyNotFoundException, so the validator
            // never had a reason to call RequestRefresh() at all.
            Assert.Equal(0, fake.RefreshRequestCount);
        }
        finally
        {
            _factory.TokenResponder.Reset();
        }
    }
}
