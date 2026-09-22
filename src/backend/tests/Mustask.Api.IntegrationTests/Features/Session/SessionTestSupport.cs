using System.Net;
using Mustask.Api.Features.Session;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Mustask.Api.IntegrationTests.Features.Session;

/// <summary>
/// Shared plumbing for the session module's integration tests: a raw
/// <see cref="HttpClient"/> cannot run the OIDC handshake like a browser
/// does (no cookie jar, no navigation), so these tests drive it by hand —
/// capture whatever <c>Set-Cookie</c>/<c>Location</c> the real
/// <c>OpenIdConnectHandler</c> produced at <c>/login</c> and replay it on
/// <c>/callback</c>, the same way a browser would.
/// </summary>
internal static class SessionTestSupport
{
    public const string ReturnUrl = "/dashboard";

    public static HttpClient CreateNoRedirectClient(this WebApplicationFactory<Program> factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    /// <summary>
    /// Calls <c>GET /api/session/login</c> and returns everything a
    /// subsequent <c>POST /callback</c> call needs
    /// (Keycloak's <c>form_post</c> delivers the authorization response by
    /// POST, form-encoded): the correlation/nonce cookies the OIDC handler
    /// set, and the <c>state</c>/<c>nonce</c> it embedded in the
    /// authorization request URL.
    /// </summary>
    public static async Task<LoginChallenge> ChallengeAsync(HttpClient client, string returnUrl = ReturnUrl)
    {
        using var response = await client.GetAsync($"/api/session/login?returnUrl={Uri.EscapeDataString(returnUrl)}");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        var location = response.Headers.Location
            ?? throw new InvalidOperationException("Login challenge did not carry a Location header.");
        var query = QueryHelpers.ParseQuery(location.Query);

        return new LoginChallenge(
            Cookies: CapturedCookies(response),
            State: query["state"].ToString(),
            Nonce: query.TryGetValue("nonce", out var nonce) ? nonce.ToString() : null,
            AuthorizationLocation: location);
    }

    public sealed record LoginChallenge(IReadOnlyList<string> Cookies, string State, string? Nonce, Uri AuthorizationLocation);

    /// <summary>
    /// Name=value pairs only (attributes stripped) — exactly what a browser
    /// would send back in a <c>Cookie:</c> header on the next request.
    /// </summary>
    public static IReadOnlyList<string> CapturedCookies(HttpResponseMessage response) =>
        response.Headers.TryGetValues("Set-Cookie", out var values)
            ? values.Select(header => header.Split(';')[0]).ToList()
            : [];

    public static void AttachCookies(HttpRequestMessage request, IEnumerable<string> cookies)
    {
        var joined = string.Join("; ", cookies);

        if (joined.Length > 0)
        {
            request.Headers.Add("Cookie", joined);
        }
    }

    /// <summary>Only the <c>mustask.session=...</c> pair, if the response set it (fresh login, renewal, or logout's clearing rewrite).</summary>
    public static string? SessionCookie(HttpResponseMessage response) =>
        response.Headers.TryGetValues("Set-Cookie", out var values)
            ? values.FirstOrDefault(header => header.StartsWith($"{SessionAuthenticationSetup.CookieName}=", StringComparison.Ordinal))
            : null;

    /// <summary>
    /// Decrypts the <c>mustask.session</c> cookie's ticket through the same
    /// <see cref="CookieAuthenticationOptions.TicketDataFormat"/> the scheme
    /// itself uses — the direct way to prove the ticket carries no access
    /// token (ADR-0022 Decision 1), rather than inferring it indirectly.
    /// Takes the base <see cref="WebApplicationFactory{TEntryPoint}"/> type
    /// (not <see cref="MustaskApiFactory"/> specifically) so a test's own
    /// <c>WithWebHostBuilder</c>-derived factory (same technique as
    /// <c>RealmReadinessTests.CreateFreshFactory</c>)
    /// can decrypt a cookie issued by the shared collection fixture too.
    /// </summary>
    public static AuthenticationTicket UnprotectSessionTicket(WebApplicationFactory<Program> factory, string setCookieHeader)
    {
        var value = setCookieHeader[(SessionAuthenticationSetup.CookieName.Length + 1)..].Split(';')[0];

        using var scope = factory.Services.CreateScope();
        var options = scope.ServiceProvider
            .GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(SessionSchemes.Session);

        return options.TicketDataFormat.Unprotect(Uri.UnescapeDataString(value))
            ?? throw new InvalidOperationException("Could not unprotect the mustask.session ticket.");
    }

    /// <summary>
    /// The same key that actually keys
    /// <see cref="SessionAccessTokenCache"/>/<see cref="SessionRenewalLock"/>
    /// since this fix — the ticket's own <c>sid</c> claim when it has one
    /// (the common case, and the same value the caller's own <c>sid</c>
    /// variable already holds), falling back to
    /// <see cref="SessionAuthenticationSetup.SessionKeyItemsKey"/> only for a
    /// ticket whose id token never had one — same precedence as
    /// <c>SessionEndpoints.TokenAsync</c>.
    /// </summary>
    public static string SessionKey(WebApplicationFactory<Program> factory, string setCookieHeader)
    {
        var ticket = UnprotectSessionTicket(factory, setCookieHeader);

        var sid = ticket.Principal.FindFirst(SessionAuthenticationSetup.SidClaimType)?.Value;

        if (!string.IsNullOrEmpty(sid))
        {
            return sid;
        }

        return ticket.Properties.Items.TryGetValue(SessionAuthenticationSetup.SessionKeyItemsKey, out var storedKey) && storedKey is not null
            ? storedKey
            : throw new InvalidOperationException("mustask.session ticket did not carry a sid claim or a session key.");
    }

    /// <summary>Re-encrypts a (possibly hand-tampered) ticket the same way the cookie scheme itself would, for tests that need to forge a cookie value directly.</summary>
    public static string ProtectSessionTicket(MustaskApiFactory factory, AuthenticationTicket ticket)
    {
        using var scope = factory.Services.CreateScope();
        var options = scope.ServiceProvider
            .GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(SessionSchemes.Session);

        return $"{SessionAuthenticationSetup.CookieName}={Uri.EscapeDataString(options.TicketDataFormat.Protect(ticket))}";
    }
}
