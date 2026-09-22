using System.Diagnostics;
using System.Security.Claims;
using Mustask.Api.Authentication.Readiness;
using Mustask.Api.Authorization;
using Mustask.Shared.Exceptions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace Mustask.Api.Features.Session;

/// <summary>
/// <c>/api/session/*</c> — the panel's token-mediating backend (ADR-0022).
/// All four routes are <c>.Public(...)</c> with respect to the Bearer
/// scheme (bypass it and the deny-all fallback)
/// and bypass the realm readiness gate; each
/// authenticates explicitly against the cookie scheme inside its own
/// handler.
/// </summary>
public static class SessionEndpoints
{
    public static IEndpointRouteBuilder MapSessionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/session");

        group.MapGet("/login", LoginAsync)
            .Public("OIDC challenge к Keycloak — маршрут входа, аутентифицирован быть не может по определению")
            .WithSummary("Вход через Keycloak");

        // Не настоящий обработчик — CallbackPath схемы
        // SessionOidc перехватывается AuthenticationMiddleware ещё внутри
        // UseAuthentication(), раньше маршрутизации к этому делегату. Замаплен
        // только затем, чтобы UseUnmatchedRouteAsNotFound() (стоит раньше в
        // конвейере, Program.cs) увидел непустой RouteEndpoint на этом пути и
        // не отдал 404/405 самостоятельно — тело делегата не исполняется
        // никогда (OnTicketReceived/OnRemoteFailure в SessionAuthenticationSetup
        // несут всю логику).
        // Оба метода, GET и POST, — маршрут `form_post`
        // остаётся (код авторизации не должен попадать в адрес, историю
        // браузера и журналы доступа), а значит Keycloak отдаёт код именно
        // POST'ом. Заглушка, замапленная только на GET, оставляла POST без
        // зарегистрированного метода на этом пути —
        // UseUnmatchedRouteAsNotFound() отвечает на «известный маршрут с
        // неверным методом» до аутентификации, поэтому
        // запрос получал 405 раньше, чем до него добирался OpenIdConnect-
        // обработчик.
        group.MapMethods(
                "/callback",
                [HttpMethods.Get, HttpMethods.Post],
                IResult () => throw new UnreachableException(
                    "callback intercepted by the OpenIdConnect handler before routing reaches this delegate"))
            .Public("перехватывается OpenIdConnect-схемой раньше маршрутизации — тело недостижимо")
            .WithSummary("Callback OIDC (деталь реализации)");

        group.MapPost("/token", TokenAsync)
            .Public("аутентифицируется явно против cookie-схемы внутри обработчика, а не против Bearer")
            .WithSummary("Текущий access-токен панели (продление при необходимости)");

        group.MapPost("/logout", LogoutAsync)
            .Public("аутентифицируется явно против cookie-схемы внутри обработчика, а не против Bearer")
            .WithSummary("Адрес выхода Keycloak — панель переходит по нему сама");

        return app;
    }

    private static IResult LoginAsync(
        string? returnUrl, RealmReadiness readiness, SessionOidcOptions sessionOptions)
    {
        var target = returnUrl ?? "/";

        if (!ReturnUrlValidator.IsLocalPath(target))
        {
            throw new InvalidReturnUrlException();
        }

        if (!readiness.Current.IsReady)
        {
            // Keycloak недоступен — challenge не
            // выпускается вовсе, браузер сразу возвращается на панель. Не
            // ProblemDetails: это переход браузера, JSON человеку не покажешь.
            // AddQueryString, не interpolation — a
            // returnUrl that already carries a query string (e.g.
            // "/tasks?status=active") must get "&authError=…", not a
            // second "?" the panel cannot parse.
            return Results.Redirect(
                QueryHelpers.AddQueryString($"{sessionOptions.PanelUrl}{target}", "authError", "idp_unavailable"));
        }

        return Results.Challenge(new AuthenticationProperties { RedirectUri = target }, [SessionSchemes.Oidc]);
    }

    private static async Task<IResult> TokenAsync(
        HttpContext httpContext,
        SessionAccessTokenCache cache,
        SessionRenewalLock renewalLock,
        RefreshGrantClient refreshGrantClient,
        RenewedIdTokenValidator idTokenValidator,
        ILogger<Program> logger,
        TimeProvider clock,
        CancellationToken cancellationToken)
    {
        var authenticateResult = await httpContext.AuthenticateAsync(SessionSchemes.Session);

        if (!authenticateResult.Succeeded || authenticateResult.Principal is null || authenticateResult.Properties is null)
        {
            throw new UnauthorizedException(SessionErrorCodes.Anonymous);
        }

        // "sid" first — the common case, and the same
        // value OnTicketReceived cached under — falling back to the minted
        // key (SessionAuthenticationSetup.SessionKeyItemsKey) only for a
        // ticket whose id token never had a "sid" at all. A ticket carrying
        // neither (in principle, one signed in before this fix) has nothing
        // safe to key the cache/lock by and is treated as no session at all.
        var sessionKey = authenticateResult.Principal.FindFirst(SessionAuthenticationSetup.SidClaimType)?.Value
            ?? (authenticateResult.Properties.Items.TryGetValue(SessionAuthenticationSetup.SessionKeyItemsKey, out var storedKey) ? storedKey : null);

        if (string.IsNullOrEmpty(sessionKey))
        {
            throw new UnauthorizedException(SessionErrorCodes.Anonymous);
        }

        if (cache.TryGetFresh(sessionKey, SessionAuthenticationSetup.AccessTokenFreshnessThreshold, out var fresh))
        {
            return Results.Ok(SessionTokenResponse.From(fresh, clock));
        }

        // ADR-0022 Decision 5: renewal is serialized per session; re-check
        // the cache once the lock is held — a neighbour may have already
        // renewed while this request was waiting.
        await using var release = await renewalLock.AcquireAsync(sessionKey, cancellationToken);

        if (cache.TryGetFresh(sessionKey, SessionAuthenticationSetup.AccessTokenFreshnessThreshold, out fresh))
        {
            return Results.Ok(SessionTokenResponse.From(fresh, clock));
        }

        var properties = authenticateResult.Properties;
        var refreshToken = properties.GetTokenValue("refresh_token");

        if (string.IsNullOrEmpty(refreshToken))
        {
            throw new UnauthorizedException(SessionErrorCodes.Anonymous);
        }

        // What the renewed id token is required to match. The
        // OIDC handler's own default ClaimActions store "sub" as
        // ClaimTypes.NameIdentifier on this ticket's principal, not raw
        // "sub" (confirmed by reading the actual ticket — a second,
        // explicit ClaimAction for "sub" does not also produce a raw one).
        var expectedSub = authenticateResult.Principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var expectedSid = authenticateResult.Principal.FindFirst(SessionAuthenticationSetup.SidClaimType)?.Value;

        RefreshGrantClient.RefreshResult refreshed;

        try
        {
            refreshed = await refreshGrantClient.RefreshAsync(refreshToken, cancellationToken);
            await idTokenValidator.ValidateAsync(refreshed.IdToken, expectedSub, expectedSid, cancellationToken);
        }
        catch (Exception ex) when (ex is RefreshGrantRejectedException or RenewedIdTokenRejectedException)
        {
            // A renewed id token that fails validation is treated
            // exactly like invalid_grant — cookie cleared, cache evicted,
            // 401 session.expired, and (for the id-token case, which
            // Keycloak itself never rejected) a log line naming the reason,
            // never the token.
            if (ex is RenewedIdTokenRejectedException rejected)
            {
                logger.LogWarning("Renewed id token rejected ({Reason}); clearing the session cookie.", rejected.Reason);
            }

            cache.Remove(sessionKey);

            // Deferred to OnStarting, not called right here: throwing next
            // routes this request through UseExceptionHandler(), whose
            // ExceptionHandlerMiddlewareImpl.ClearHttpContext calls
            // Response.Clear() before building the 401 body — a Set-Cookie
            // added here (SignOutAsync writes it immediately) would be wiped
            // out along with everything else that call clears. OnStarting
            // callbacks are a separate response feature that Response.Clear()
            // does not touch, and they run right before headers actually go
            // out — after the exception handler has already rebuilt the
            // response around the thrown UnauthorizedException below.
            httpContext.Response.OnStarting(static state =>
                ((HttpContext)state).SignOutAsync(SessionSchemes.Session), httpContext);
            throw new UnauthorizedException(SessionErrorCodes.Expired);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or TimeoutException)
        {
            // Same code and Retry-After as the
            // readiness gate — cookie is untouched, the session is not
            // considered lost.
            httpContext.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            httpContext.Response.Headers.RetryAfter = "30";
            return Results.Empty;
        }

        var expiresAtUtc = clock.GetUtcNow() + refreshed.ExpiresIn;
        cache.Set(sessionKey, refreshed.AccessToken, expiresAtUtc);

        // Ticket in the cookie carries only id_token/refresh_token
        // (ADR-0022 Decision 1) — replace both with the rotated pair.
        properties.StoreTokens(
        [
            new AuthenticationToken { Name = "id_token", Value = refreshed.IdToken },
            new AuthenticationToken { Name = "refresh_token", Value = refreshed.RefreshToken },
        ]);

        if (refreshed.RefreshExpiresIn is { } refreshExpiresIn)
        {
            properties.IsPersistent = true;
            properties.ExpiresUtc = clock.GetUtcNow() + refreshExpiresIn;
        }
        else
        {
            properties.IsPersistent = false;
            properties.ExpiresUtc = null;
        }

        await httpContext.SignInAsync(SessionSchemes.Session, authenticateResult.Principal, properties);

        return Results.Ok(new SessionTokenResponse(refreshed.AccessToken, expiresAtUtc, SecondsUntil(expiresAtUtc, clock)));
    }

    private static async Task<IResult> LogoutAsync(
        HttpContext httpContext,
        RefreshGrantClient refreshGrantClient,
        SessionOidcOptions sessionOptions,
        SessionAccessTokenCache cache,
        ILogger<Program> logger,
        CancellationToken cancellationToken)
    {
        var authenticateResult = await httpContext.AuthenticateAsync(SessionSchemes.Session);
        var refreshToken = authenticateResult.Succeeded
            ? authenticateResult.Properties?.GetTokenValue("refresh_token")
            : null;
        var idToken = authenticateResult.Succeeded
            ? authenticateResult.Properties?.GetTokenValue("id_token")
            : null;
        // Same "sid"-first, minted-key-fallback
        // precedence as TokenAsync — see the comment there.
        var sessionKey = authenticateResult.Succeeded
            ? authenticateResult.Principal?.FindFirst(SessionAuthenticationSetup.SidClaimType)?.Value
                ?? (authenticateResult.Properties is not null
                    && authenticateResult.Properties.Items.TryGetValue(SessionAuthenticationSetup.SessionKeyItemsKey, out var storedKey)
                    ? storedKey
                    : null)
            : null;

        OpenIdConnectConfiguration configuration;

        try
        {
            // Discovery is read, and — if there is a
            // cookie — the back-channel logout is sent to Keycloak, BEFORE
            // anything here is torn down. An unreachable Keycloak must not
            // leave its own SSO session alive while our cookie is already
            // gone: the next "Войти" on this machine would
            // then pass without a password once Keycloak comes back.
            configuration = await refreshGrantClient.GetLogoutDiscoveryAsync(cancellationToken);

            if (!string.IsNullOrEmpty(refreshToken))
            {
                await refreshGrantClient.LogoutAsync(refreshToken, cancellationToken);
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or TimeoutException)
        {
            logger.LogWarning(
                ex, "Logout could not reach Keycloak; the session cookie is kept alive so \"Повторить выход\" can retry.");
            httpContext.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            httpContext.Response.Headers.RetryAfter = "30";
            return Results.Empty;
        }

        // The cached access token must not outlive the
        // session that owned it — up to AccessTokenFreshnessThreshold longer
        // otherwise, handed out to a copy of the cookie already captured
        // before logout.
        if (!string.IsNullOrEmpty(sessionKey))
        {
            cache.Remove(sessionKey);
        }

        // Unconditional and idempotent from here on — no cookie at all still
        // reaches this point and gets 200 with no id_token_hint.
        await httpContext.SignOutAsync(SessionSchemes.Session);

        var parameters = new Dictionary<string, string?>
        {
            ["client_id"] = SessionOidcOptions.ClientId,
            // The panel's "Вы вышли из системы" screen is
            // reachable only if RP-initiated logout actually lands on it —
            // the bare panel root re-entered the sign-in loop instead.
            ["post_logout_redirect_uri"] = $"{sessionOptions.PanelUrl}/signed-out",
        };

        if (!string.IsNullOrEmpty(idToken))
        {
            parameters["id_token_hint"] = idToken;
        }

        var logoutUrl = QueryHelpers.AddQueryString(configuration.EndSessionEndpoint, parameters);

        return Results.Ok(new SessionLogoutResponse(logoutUrl));
    }

    private static int SecondsUntil(DateTimeOffset expiresAtUtc, TimeProvider clock) =>
        (int)Math.Max(0, Math.Ceiling((expiresAtUtc - clock.GetUtcNow()).TotalSeconds));

    /// <summary><c>expiresIn</c> alongside the existing <c>expiresAt</c> — additive.</summary>
    private sealed record SessionTokenResponse(string AccessToken, DateTimeOffset ExpiresAt, int ExpiresIn)
    {
        public static SessionTokenResponse From(SessionAccessTokenCache.Entry entry, TimeProvider clock) =>
            new(entry.AccessToken, entry.ExpiresAtUtc, SecondsUntil(entry.ExpiresAtUtc, clock));
    }

    private sealed record SessionLogoutResponse(string LogoutUrl);
}
