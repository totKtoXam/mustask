using System.Globalization;
using System.Security.Claims;
using Mustask.Api.Authentication;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace Mustask.Api.Features.Session;

/// <summary>
/// Wires the panel's session on top of the resource-server half already
/// built by <c>JwtBearerSetup.AddMustaskAuthentication()</c>. Registers two
/// more schemes without ever calling
/// <c>services.AddAuthentication(name)</c> again — that overload would
/// overwrite <see cref="Microsoft.AspNetCore.Authentication.AuthenticationOptions.DefaultScheme"/>,
/// which every ordinary <c>Mustask.Api</c> route depends on staying
/// <see cref="Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme"/>.
/// <c>services.AddAuthentication()</c> with no argument is the documented
/// way to append schemes to an already-configured
/// <see cref="AuthenticationBuilder"/> without touching that default.
/// </summary>
public static class SessionAuthenticationSetup
{
    /// <summary>Claim carrying the id token's OIDC session id — the key <see cref="SessionAccessTokenCache"/>/<see cref="SessionRenewalLock"/> use.</summary>
    public const string SidClaimType = "sid";

    /// <summary>Renewal triggers under 60 s remaining on the cached access token.</summary>
    public static readonly TimeSpan AccessTokenFreshnessThreshold = TimeSpan.FromSeconds(60);

    public const string CookieName = "mustask.session";

    /// <summary>Cookie does not travel with any other <c>Mustask.Api</c> route.</summary>
    public const string CookiePath = "/api/session";

    public const string CallbackPath = "/api/session/callback";

    /// <summary>
    /// <see cref="AuthenticationProperties.Items"/> key used to smuggle the
    /// token endpoint's non-standard <c>refresh_expires_in</c> field from
    /// <c>OnTokenValidated</c> (the only OIDC event whose context carries
    /// <see cref="Microsoft.AspNetCore.Authentication.OpenIdConnect.TokenValidatedContext.TokenEndpointResponse"/>)
    /// to <c>OnTicketReceived</c> (the event that actually shapes the
    /// ticket that gets written to the cookie) — the two events share the
    /// same <see cref="AuthenticationProperties"/> instance, which is the
    /// framework's own mechanism for this kind of hand-off (it is exactly
    /// how <c>SaveTokens</c> itself passes data between events). Removed
    /// again before the ticket is persisted, so it never leaks into the
    /// cookie itself.
    /// </summary>
    private const string RefreshExpiresInItemsKey = "tech119.refresh_expires_in";

    /// <summary>
    /// <see cref="AuthenticationProperties.Items"/> key holding a fallback
    /// cache/lock key for the one case <c>sid</c> cannot
    /// cover: an id token with no <c>sid</c> claim at all — OIDC does not
    /// require Keycloak to send one, and a client-scope edit or a different
    /// IdP tomorrow could drop it, which is exactly what sent an
    /// already-signed-in user into a 401 <c>session.anonymous</c> loop
    /// before this fix. <c>sid</c> itself is the key the overwhelming
    /// majority of the time (Keycloak 26 always sends it with scope
    /// <c>basic</c>) and costs nothing extra in the cookie, since
    /// it is already a claim on the principal the ticket serializes
    /// regardless of whether anything reads it as a cache key; a value
    /// stored here only in the no-<c>sid</c> fallback, by contrast, does
    /// grow the cookie — acceptable because that path is the rare one.
    /// </summary>
    internal const string SessionKeyItemsKey = "tech119.session_key";

    public static IServiceCollection AddMustaskSessionAuthentication(
        this IServiceCollection services, KeycloakOptions keycloak, SessionOidcOptions sessionOptions)
    {
        services.AddSingleton(sessionOptions);
        services.AddSingleton<SessionAccessTokenCache>();
        services.AddSingleton<SessionRenewalLock>();
        services.AddHttpClient(RefreshGrantClient.HttpClientName)
            .ConfigureHttpClient(client => client.Timeout = TimeSpan.FromSeconds(5));
        services.AddSingleton<RefreshGrantClient>();
        // Validates the id token of every renewal — the OIDC
        // handler itself only validates the one from the code exchange.
        // KeycloakOptions/IConfigurationManager<OpenIdConnectConfiguration>
        // are both already singletons (Program.cs) — no second discovery
        // point.
        services.AddSingleton<RenewedIdTokenValidator>();

        services.AddAuthentication()
            .AddCookie(SessionSchemes.Session, options =>
            {
                options.Cookie.Name = CookieName;
                options.Cookie.HttpOnly = true;
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.Cookie.Path = CookiePath;

                // Every renewal already rewrites the
                // cookie itself (TokenAsync's own SignInAsync carries the
                // rotated refresh token) — the framework's own sliding
                // renewal adds nothing and actively hurts here. Left at the
                // default (true), any AuthenticateAsync call past the
                // cookie's half-life — including the "cache is still fresh,
                // answer immediately" branch of POST /api/session/token,
                // which never calls SignInAsync itself — schedules an
                // OnStarting rewrite of the ticket AS ALREADY READ, i.e.
                // with whatever refresh token this request's cookie
                // happened to carry. Two tabs racing a real renewal after
                // that point can have the browser end up holding this
                // stale rewritten cookie instead of the rotated one a
                // sibling request just wrote — and presenting a reused
                // refresh token kills the whole session, not just
                // that one request.
                options.SlidingExpiration = false;
            })
            .AddOpenIdConnect(SessionSchemes.Oidc, _ => { });

        // Configure (not the AddOpenIdConnect callback above) so the shared
        // discovery/JWKS singleton and the cache/clock can be resolved from
        // DI — the same technique JwtBearerSetup already uses for
        // JwtBearerOptions.
        services.AddOptions<OpenIdConnectOptions>(SessionSchemes.Oidc)
            .Configure<IConfigurationManager<OpenIdConnectConfiguration>, SessionAccessTokenCache, TimeProvider>(
                (options, configurationManager, cache, clock) =>
                {
                    options.ClientId = SessionOidcOptions.ClientId;
                    options.ClientSecret = sessionOptions.ClientSecret;
                    options.Authority = keycloak.Authority;
                    options.RequireHttpsMetadata = keycloak.RequireHttpsMetadata;
                    options.ConfigurationManager = configurationManager;
                    options.ResponseType = OpenIdConnectResponseType.Code;
                    options.UsePkce = true;
                    options.CallbackPath = CallbackPath;
                    options.SignInScheme = SessionSchemes.Session;
                    options.SaveTokens = true;

                    options.Scope.Clear();
                    options.Scope.Add("openid");
                    options.Scope.Add("profile");

                    // Defensive, not strictly required: id-token claims already
                    // flow onto the principal unfiltered (verified against the
                    // OIDC handler's default ClaimActions — "sid" is not among
                    // the deleted protocol claims), but an explicit mapping
                    // removes any doubt and costs nothing if the claim is
                    // already there.
                    options.ClaimActions.MapUniqueJsonKey(SidClaimType, SidClaimType);

                    // TokenAsync reads "sub" off this principal
                    // as ClaimTypes.NameIdentifier, not raw "sub" — unlike
                    // "sid", "sub" IS one of the OIDC handler's own default
                    // ClaimActions (MapUniqueJsonKey(NameIdentifier, "sub")
                    // in OpenIdConnectOptions's constructor), and adding a
                    // second action for the same "sub" JSON key here did not
                    // also produce a raw "sub" claim (confirmed by reading
                    // the actual ticket) — NameIdentifier is where the value
                    // already lives, so that is what gets compared.

                    options.Events = new OpenIdConnectEvents
                    {
                        OnTokenValidated = OnTokenValidated,
                        OnTicketReceived = context => OnTicketReceived(context, cache, sessionOptions, clock),
                        OnRemoteFailure = context => OnRemoteFailure(context, sessionOptions),
                    };
                });

        return services;
    }

    private static Task OnTokenValidated(TokenValidatedContext context)
    {
        var refreshExpiresIn = context.TokenEndpointResponse?.GetParameter("refresh_expires_in");

        if (!string.IsNullOrEmpty(refreshExpiresIn) && context.Properties is not null)
        {
            context.Properties.Items[RefreshExpiresInItemsKey] = refreshExpiresIn;
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Shapes the ticket before it is signed into the cookie (ADR-0022
    /// Decision 1/5): caches the access
    /// token by <c>sid</c>, strips it from the ticket, reduces the ticket's
    /// principal and <see cref="AuthenticationProperties.Items"/> to what
    /// this module actually reads back later, sets the cookie's expiry from
    /// <c>refresh_expires_in</c>, and — because the panel and <c>Mustask.Api</c>
    /// are different origins — rewrites the post-sign-in redirect to an
    /// absolute panel address. The redirect the
    /// base <c>RemoteAuthenticationHandler</c> actually performs afterward
    /// reads <see cref="TicketReceivedContext.ReturnUri"/>, not
    /// <c>Properties.RedirectUri</c> (which the handler already cleared by
    /// this point) — confirmed by reading
    /// <c>RemoteAuthenticationHandler.HandleRequestAsync</c>.
    /// </summary>
    private static Task OnTicketReceived(
        TicketReceivedContext context, SessionAccessTokenCache cache, SessionOidcOptions sessionOptions, TimeProvider clock)
    {
        var properties = context.Properties ?? new AuthenticationProperties();

        // The cache/lock key is "sid" when the id token
        // has one (the overwhelmingly common case — Keycloak 26 always sends
        // it with scope "basic") and costs nothing extra, since "sid" is
        // already a claim on the principal the ticket serializes regardless
        // of whether anything reads it as a cache key. Only the rare
        // fallback — an id token with no "sid" at all, which used to send an
        // already-signed-in user into a silent 401 session.anonymous loop —
        // pays for a freshly minted key stored in Items, and only then.
        var sid = context.Principal?.FindFirst(SidClaimType)?.Value;
        var sessionKey = sid ?? Guid.CreateVersion7().ToString();

        var tokens = properties.GetTokens().ToList();
        var accessToken = tokens.FirstOrDefault(t => string.Equals(t.Name, "access_token", StringComparison.Ordinal))?.Value;
        var refreshToken = tokens.FirstOrDefault(t => string.Equals(t.Name, "refresh_token", StringComparison.Ordinal))?.Value;
        var idToken = tokens.FirstOrDefault(t => string.Equals(t.Name, "id_token", StringComparison.Ordinal))?.Value;
        var expiresAtRaw = tokens.FirstOrDefault(t => string.Equals(t.Name, "expires_at", StringComparison.Ordinal))?.Value;

        if (!string.IsNullOrEmpty(accessToken))
        {
            var expiresAtUtc = !string.IsNullOrEmpty(expiresAtRaw)
                && DateTimeOffset.TryParse(expiresAtRaw, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var parsed)
                ? parsed
                : clock.GetUtcNow();

            cache.Set(sessionKey, accessToken, expiresAtUtc);
        }

        var hasRefreshExpiresIn = properties.Items.TryGetValue(RefreshExpiresInItemsKey, out var refreshExpiresInRaw);

        // A real Keycloak ticket measured live carried
        // three cookie chunks (~5.9 KB) even with the access token already
        // stripped — the OIDC handler's own default ClaimActions copy every
        // other id-token claim (name, preferred_username, email,
        // given_name/family_name, groups, ...) onto this principal
        // unfiltered, and SaveTokens keeps "token_type"/"expires_at"
        // alongside the refresh/id tokens, none of which anything in this
        // module ever reads back. Clearing Items and rebuilding it from
        // scratch — rather than removing known keys one by one — also drops
        // whatever the handler itself used only for the code exchange
        // (e.g. the redirect_uri it sent to the token endpoint, the scheme
        // name) without this code needing to know those internal key names,
        // and keeps working if a future .NET version adds one more such
        // entry.
        properties.Items.Clear();

        if (sid is null)
        {
            properties.Items[SessionKeyItemsKey] = sessionKey;
        }

        // ADR-0022 Decision 1: the access token is stripped from the ticket —
        // the browser already has it (SessionAccessTokenCache). So are
        // "token_type"/"expires_at" — SessionEndpoints/RefreshGrantClient
        // never read either back off the cookie, only the refresh token
        // (renewal) and the id token (renewed-id-token validation,
        // logout's id_token_hint).
        var keptTokens = new List<AuthenticationToken>(2);

        if (!string.IsNullOrEmpty(refreshToken))
        {
            keptTokens.Add(new AuthenticationToken { Name = "refresh_token", Value = refreshToken });
        }

        if (!string.IsNullOrEmpty(idToken))
        {
            keptTokens.Add(new AuthenticationToken { Name = "id_token", Value = idToken });
        }

        properties.StoreTokens(keptTokens);

        if (hasRefreshExpiresIn
            && long.TryParse(refreshExpiresInRaw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var refreshExpiresInSeconds))
        {
            properties.IsPersistent = true;
            properties.ExpiresUtc = clock.GetUtcNow().AddSeconds(refreshExpiresInSeconds);
        }
        else
        {
            // No refresh_expires_in in the token response — a session
            // cookie, gone when the browser closes.
            properties.IsPersistent = false;
            properties.ExpiresUtc = null;
        }

        // The principal itself keeps only "sub" (mapped by the OIDC
        // handler's own default ClaimActions to ClaimTypes.NameIdentifier —
        // SessionEndpoints.TokenAsync's expectedSub read, confirmed by
        // reading the actual ticket, same as before this fix) and "sid"
        // (the session key above, and the expectedSid read) —
        // everything else the handler copied off the id token unfiltered is
        // dropped rather than merely left unread.
        var sub = context.Principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var identity = new ClaimsIdentity(context.Principal?.Identity?.AuthenticationType ?? CookieAuthenticationDefaults.AuthenticationScheme);

        if (sub is not null)
        {
            identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, sub));
        }

        if (sid is not null)
        {
            identity.AddClaim(new Claim(SidClaimType, sid));
        }

        context.Principal = new ClaimsPrincipal(identity);

        var returnUrl = ReturnUrlValidator.IsLocalPath(context.ReturnUri) ? context.ReturnUri! : "/";
        context.ReturnUri = sessionOptions.PanelUrl + returnUrl;

        return Task.CompletedTask;
    }

    /// <summary>
    /// The closed, three-value <c>authError</c> vocabulary. Not a
    /// catalog error code — this is a query parameter on a browser redirect,
    /// which has no body to carry a <c>errorCode</c> in.
    /// </summary>
    private static Task OnRemoteFailure(RemoteFailureContext context, SessionOidcOptions sessionOptions)
    {
        context.HandleResponse();

        var returnUrl = ReturnUrlValidator.IsLocalPath(context.Properties?.RedirectUri)
            ? context.Properties!.RedirectUri!
            : "/";

        var code = ClassifyFailure(context.Failure);

        // Plain interpolation put a second "?" in front
        // of authError whenever returnUrl already carried a query string
        // (e.g. "/tasks?status=active"), producing a malformed Location
        // the panel could not parse — AddQueryString appends with "&" when
        // one is already there.
        context.HttpContext.Response.Redirect(
            QueryHelpers.AddQueryString($"{sessionOptions.PanelUrl}{returnUrl}", "authError", code));

        return Task.CompletedTask;
    }

    /// <summary>
    /// Internal, not private: unit-tested directly
    /// (<c>SessionAuthenticationSetupClassifyFailureTests</c>) rather than
    /// through a live discovery failure the current test doubles cannot
    /// reproduce faithfully (the real <c>ConfigurationManager&lt;T&gt;</c>
    /// throws <see cref="InvalidOperationException"/>, not
    /// <see cref="HttpRequestException"/>, once it has never obtained
    /// configuration at all — <see cref="Mustask.Api.IntegrationTests.KeycloakTestRealm.FakeRealmMetadata"/>
    /// only ever throws the latter).
    /// </summary>
    internal static string ClassifyFailure(Exception? failure) => failure switch
    {
        OpenIdConnectProtocolException protocolException when IsAccessDenied(protocolException) => "access_denied",
        HttpRequestException or TaskCanceledException or TimeoutException or IOException or InvalidOperationException => "idp_unavailable",
        _ => "sign_in_failed",
    };

    private static bool IsAccessDenied(OpenIdConnectProtocolException exception) =>
        exception.Data.Contains("error")
        && string.Equals(exception.Data["error"] as string, "access_denied", StringComparison.Ordinal);
}
