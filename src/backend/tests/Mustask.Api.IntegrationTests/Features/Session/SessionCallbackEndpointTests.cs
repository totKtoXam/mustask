using System.Net;
using Mustask.Api.Features.Session;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace Mustask.Api.IntegrationTests.Features.Session;

/// <summary>
/// <c>POST /api/session/callback</c> — not a real handler, the
/// <c>CallbackPath</c> of the OIDC scheme: all behavior comes from
/// <c>SessionAuthenticationSetup</c>'s <c>OnTicketReceived</c>/<c>OnRemoteFailure</c>.
/// Drives the handshake by hand — capture whatever the real login challenge
/// set, replay it here. The real Keycloak delivers the
/// authorization response by **POST**, form-encoded — <c>ResponseMode.FormPost</c>
/// stays so the code never travels in a URL — so these tests post a form
/// body too rather than a query string, exercising the same path production
/// traffic does. <see cref="SessionCallbackMethodRegressionTests"/> covers the
/// GET-only regression on its own, without the rest of the handshake.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class SessionCallbackEndpointTests
{
    private readonly MustaskApiFactory _factory;

    public SessionCallbackEndpointTests(MustaskApiFactory factory)
    {
        _factory = factory;
    }

    private async Task<HttpResponseMessage> CallbackAsync(
        HttpClient client, SessionTestSupport.LoginChallenge challenge, params (string Name, string Value)[] fields)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/session/callback")
        {
            Content = new FormUrlEncodedContent(fields.Select(f => new KeyValuePair<string, string>(f.Name, f.Value))),
        };
        SessionTestSupport.AttachCookies(request, challenge.Cookies);
        return await client.SendAsync(request);
    }

    [Fact]
    public async Task Callback_SuccessfulExchange_SetsCookieAndRedirectsToAbsolutePanelUrl_AndCachesAccessTokenWithoutPuttingItInTheTicket()
    {
        var client = _factory.CreateNoRedirectClient();
        var challenge = await SessionTestSupport.ChallengeAsync(client, "/dashboard");

        var sid = Guid.CreateVersion7().ToString();
        var accessToken = KeycloakTestRealm.CreateToken(["dictionaries.languages.read"]);

        _factory.TokenResponder.OkResponseBodyFactory = () => KeycloakTokenEndpointResponder.BuildBody(
            accessToken: accessToken,
            idToken: KeycloakTokenEndpointResponder.BuildIdToken(sid, challenge.Nonce),
            refreshToken: "test-refresh-token-1",
            expiresIn: 300,
            refreshExpiresIn: 1800);

        try
        {
            var response = await CallbackAsync(client, challenge, ("code", "test-code"), ("state", challenge.State));

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Equal("http://panel.invalid/dashboard", response.Headers.Location!.ToString());

            var sessionCookie = SessionTestSupport.SessionCookie(response);
            Assert.NotNull(sessionCookie);
            Assert.Contains("httponly", sessionCookie, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("secure", sessionCookie, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("samesite=lax", sessionCookie, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("path=/api/session", sessionCookie, StringComparison.OrdinalIgnoreCase);
            // refresh_expires_in was present -> persistent cookie, not a session cookie.
            Assert.Contains("expires=", sessionCookie, StringComparison.OrdinalIgnoreCase);

            var ticket = SessionTestSupport.UnprotectSessionTicket(_factory, sessionCookie);
            var sessionKey = SessionTestSupport.SessionKey(_factory, sessionCookie);

            using var scope = _factory.Services.CreateScope();
            var cache = scope.ServiceProvider.GetRequiredService<SessionAccessTokenCache>();
            Assert.True(cache.TryGetFresh(sessionKey, TimeSpan.Zero, out var cached));
            Assert.Equal(accessToken, cached.AccessToken);
            var tokenNames = ticket.Properties.GetTokens().Select(t => t.Name).ToList();
            Assert.DoesNotContain("access_token", tokenNames);
            Assert.Contains("refresh_token", tokenNames);
            Assert.Contains("id_token", tokenNames);
            Assert.DoesNotContain(accessToken, ticket.Properties.Items.Values.Select(v => v ?? string.Empty));
        }
        finally
        {
            _factory.TokenResponder.Reset();
        }
    }

    [Fact]
    public async Task Callback_NoRefreshExpiresIn_SessionCookieHasNoExpiry()
    {
        var client = _factory.CreateNoRedirectClient();
        var challenge = await SessionTestSupport.ChallengeAsync(client, "/dashboard");
        var sid = Guid.CreateVersion7().ToString();

        _factory.TokenResponder.OkResponseBodyFactory = () => KeycloakTokenEndpointResponder.BuildBody(
            accessToken: KeycloakTestRealm.CreateToken(),
            idToken: KeycloakTokenEndpointResponder.BuildIdToken(sid, challenge.Nonce),
            refreshToken: "test-refresh-token-2",
            expiresIn: 300,
            refreshExpiresIn: null);

        try
        {
            var response = await CallbackAsync(client, challenge, ("code", "test-code"), ("state", challenge.State));

            var sessionCookie = SessionTestSupport.SessionCookie(response);
            Assert.NotNull(sessionCookie);
            Assert.DoesNotContain("expires=", sessionCookie, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            _factory.TokenResponder.Reset();
        }
    }

    [Fact]
    public async Task Callback_TokenExchangeRejected_RedirectsWithSignInFailed_AndNoCookie()
    {
        var client = _factory.CreateNoRedirectClient();
        var challenge = await SessionTestSupport.ChallengeAsync(client, "/dashboard");

        _factory.TokenResponder.CurrentMode = KeycloakTokenEndpointResponder.Mode.InvalidGrant;

        try
        {
            var response = await CallbackAsync(client, challenge, ("code", "test-code"), ("state", challenge.State));

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Equal(
                "http://panel.invalid/dashboard?authError=sign_in_failed",
                response.Headers.Location!.ToString());
            Assert.Null(SessionTestSupport.SessionCookie(response));
        }
        finally
        {
            _factory.TokenResponder.Reset();
        }
    }

    [Fact]
    public async Task Callback_TokenEndpointUnavailable_RedirectsWithIdpUnavailable()
    {
        var client = _factory.CreateNoRedirectClient();
        var challenge = await SessionTestSupport.ChallengeAsync(client, "/dashboard");

        _factory.TokenResponder.CurrentMode = KeycloakTokenEndpointResponder.Mode.Unavailable;

        try
        {
            var response = await CallbackAsync(client, challenge, ("code", "test-code"), ("state", challenge.State));

            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Equal(
                "http://panel.invalid/dashboard?authError=idp_unavailable",
                response.Headers.Location!.ToString());
        }
        finally
        {
            _factory.TokenResponder.Reset();
        }
    }

    [Fact]
    public async Task Callback_UserDeniedConsentAtKeycloak_RedirectsWithAccessDenied()
    {
        var client = _factory.CreateNoRedirectClient();
        var challenge = await SessionTestSupport.ChallengeAsync(client, "/dashboard");

        // Keycloak's own error redirect back to us — form_post carries this
        // one too: ResponseMode governs the whole authorization
        // response, success or denial, not only the successful case.
        var response = await CallbackAsync(
            client, challenge, ("error", "access_denied"), ("state", challenge.State));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(
            "http://panel.invalid/dashboard?authError=access_denied",
            response.Headers.Location!.ToString());
        Assert.Null(SessionTestSupport.SessionCookie(response));
    }

    /// <summary>
    /// A sign-in that starts fine (the
    /// login challenge succeeds against the collection's normal, healthy
    /// fake realm) but whose discovery has never been obtained by the time
    /// the callback runs the code exchange — the real
    /// <c>ConfigurationManager&lt;T&gt;</c>'s own failure mode
    /// (<see cref="InvalidOperationException"/>, IDX20803), not the
    /// <see cref="HttpRequestException"/> <c>Mode.Unavailable</c> models. A
    /// second <see cref="WebApplicationFactory{TEntryPoint}"/> built from the
    /// same base (<c>WithWebHostBuilder</c>, same technique as
    /// <c>SessionLogoutEndpointTests.Logout_DiscoveryUnavailable_Returns503_CookieUntouched</c>)
    /// swaps in the broken fake for the callback call only, replaying the
    /// correlation cookies the first (healthy) login challenge set. Proves
    /// end-to-end what <c>SessionAuthenticationSetupTests</c>'s
    /// <c>ClassifyFailure</c> unit test already proves in isolation: this
    /// exception type reaches <c>idp_unavailable</c>, not <c>sign_in_failed</c>.
    /// </summary>
    [Fact]
    public async Task Callback_DiscoveryNeverObtained_RedirectsWithIdpUnavailable()
    {
        var healthyClient = _factory.CreateNoRedirectClient();
        var challenge = await SessionTestSupport.ChallengeAsync(healthyClient, "/dashboard");

        var brokenFake = new KeycloakTestRealm.FakeRealmMetadata
        {
            CurrentMode = KeycloakTestRealm.FakeRealmMetadata.Mode.NeverObtained,
        };
        using var brokenFactory = _factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(
            services => services.AddSingleton<IConfigurationManager<OpenIdConnectConfiguration>>(brokenFake)));
        var brokenClient = brokenFactory.CreateNoRedirectClient();

        var response = await CallbackAsync(
            brokenClient, challenge, ("code", "test-code"), ("state", challenge.State));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(
            "http://panel.invalid/dashboard?authError=idp_unavailable",
            response.Headers.Location!.ToString());
    }

    /// <summary>The same query-string-builder fix as the login route — a returnUrl that already carries a query string must get "&authError=…", not a second "?".</summary>
    [Fact]
    public async Task Callback_UserDeniedConsent_ReturnUrlWithQueryString_AppendsAuthErrorWithAmpersand()
    {
        var client = _factory.CreateNoRedirectClient();
        var challenge = await SessionTestSupport.ChallengeAsync(client, "/tasks?status=active");

        var response = await CallbackAsync(
            client, challenge, ("error", "access_denied"), ("state", challenge.State));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(
            "http://panel.invalid/tasks?status=active&authError=access_denied",
            response.Headers.Location!.ToString());
    }
}
