using Mustask.Api.Features.Session;
using Microsoft.AspNetCore.Authentication;

namespace Mustask.Api.IntegrationTests.Features.Session;

/// <summary>
/// <c>CookieAuthenticationHandler</c>'s own sliding
/// expiration used to fire on <b>every</b> <c>AuthenticateAsync</c> call past
/// a cookie's half-life — including <c>POST /api/session/token</c>'s "cache
/// is still fresh, answer immediately" branch, which never calls
/// <c>SignInAsync</c> itself. That automatic renewal rewrites the ticket
/// exactly as it was read, i.e. with whatever refresh token this particular
/// request's cookie happened to carry — which, in a race between two tabs,
/// can be the one a sibling request's real renewal had already rotated away
/// (presenting a stale refresh token kills the whole session, not just
/// one request).
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class SessionSlidingExpirationTests
{
    private readonly MustaskApiFactory _factory;

    public SessionSlidingExpirationTests(MustaskApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Token_FreshCacheWithOldTicket_DoesNotReissueCookie()
    {
        var client = _factory.CreateNoRedirectClient();
        var challenge = await SessionTestSupport.ChallengeAsync(client);

        var accessToken = KeycloakTestRealm.CreateToken();
        _factory.TokenResponder.OkResponseBodyFactory = () => KeycloakTokenEndpointResponder.BuildBody(
            accessToken: accessToken,
            idToken: KeycloakTokenEndpointResponder.BuildIdToken(Guid.CreateVersion7().ToString(), challenge.Nonce),
            refreshToken: "sliding-expiration-refresh-token",
            expiresIn: 300,
            refreshExpiresIn: 28_800); // 8h, same order of magnitude as the real stand.

        using var signInRequest = new HttpRequestMessage(
            HttpMethod.Get, $"/api/session/callback?code=test-code&state={Uri.EscapeDataString(challenge.State)}");
        SessionTestSupport.AttachCookies(signInRequest, challenge.Cookies);
        using var signInResponse = await client.SendAsync(signInRequest);
        _factory.TokenResponder.Reset();

        var freshCookie = SessionTestSupport.SessionCookie(signInResponse)
            ?? throw new InvalidOperationException("Sign-in did not set mustask.session.");

        // Forge a cookie carrying the SAME principal/tokens/session key —
        // sign-in already cached this session's access token, so /token's
        // "fresh cache" branch answers without ever calling SignInAsync — but
        // with IssuedUtc/ExpiresUtc rewound so more than half the cookie's
        // lifetime has "elapsed" by wall-clock time. This is exactly the
        // condition CookieAuthenticationHandler's default SlidingExpiration
        // uses to decide whether to silently re-issue the cookie.
        var ticket = SessionTestSupport.UnprotectSessionTicket(_factory, freshCookie);
        var backdated = new AuthenticationProperties(ticket.Properties.Items, ticket.Properties.Parameters)
        {
            IsPersistent = true,
            IssuedUtc = DateTimeOffset.UtcNow.AddHours(-7),
            ExpiresUtc = DateTimeOffset.UtcNow.AddHours(1),
        };
        var backdatedTicket = new AuthenticationTicket(ticket.Principal, backdated, ticket.AuthenticationScheme);
        var backdatedCookie = SessionTestSupport.ProtectSessionTicket(_factory, backdatedTicket);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/session/token")
        {
            Headers = { { "Cookie", backdatedCookie } },
        };
        using var response = await client.SendAsync(request);

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.Null(SessionTestSupport.SessionCookie(response));
    }
}
