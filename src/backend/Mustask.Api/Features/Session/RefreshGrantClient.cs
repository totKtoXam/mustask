using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace Mustask.Api.Features.Session;

/// <summary>
/// Two direct calls to Keycloak's token endpoint:
/// <c>grant_type=refresh_token</c> for renewal, and the confidential
/// client's back-channel logout (<c>POST end_session_endpoint</c> with
/// <c>refresh_token</c>) for sign-out. The endpoint addresses are not
/// configured separately — both come from the same
/// <see cref="IConfigurationManager{OpenIdConnectConfiguration}"/> singleton
/// that <c>JwtBearerSetup</c> and <c>RealmReadinessMonitor</c> already share
/// — a third consumer of one discovery point, not a new one.
/// </summary>
public sealed class RefreshGrantClient(
    IHttpClientFactory httpClientFactory,
    IConfigurationManager<OpenIdConnectConfiguration> configurationManager,
    SessionOidcOptions sessionOptions,
    ILogger<RefreshGrantClient> logger)
{
    /// <summary>Named <see cref="HttpClient"/> registered by <see cref="SessionAuthenticationSetup"/>.</summary>
    public const string HttpClientName = "keycloak-token";

    public sealed record RefreshResult(
        string AccessToken,
        TimeSpan ExpiresIn,
        string RefreshToken,
        string IdToken,
        TimeSpan? RefreshExpiresIn);

    /// <summary>
    /// Redeems <paramref name="refreshToken"/> for a new token set.
    /// </summary>
    /// <exception cref="RefreshGrantRejectedException">
    /// Keycloak answered 400 with <c>error=invalid_grant</c> — the caller
    /// turns this into <c>session.expired</c> after clearing the cookie.
    /// </exception>
    /// <remarks>
    /// Any other failure (network, timeout, 5xx, discovery never obtained, an
    /// unparsable body) is deliberately not caught here — it surfaces to the
    /// caller as an <see cref="HttpRequestException"/> (or
    /// <see cref="TaskCanceledException"/>), which <c>POST /api/session/token</c>
    /// turns into 503. Two gaps in that translation are closed: discovery
    /// that was never obtained at all used to surface as a raw
    /// <see cref="InvalidOperationException"/>
    /// (500, not 503), and a malformed success body as a raw
    /// <see cref="JsonException"/>/<see cref="KeyNotFoundException"/> (500 as
    /// well) — both are now folded into the same <see cref="HttpRequestException"/>
    /// path.
    /// </remarks>
    public async Task<RefreshResult> RefreshAsync(string refreshToken, CancellationToken cancellationToken)
    {
        var configuration = await GetConfigurationAsync(cancellationToken);
        var httpClient = httpClientFactory.CreateClient(HttpClientName);

        using var request = new HttpRequestMessage(HttpMethod.Post, configuration.TokenEndpoint)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = refreshToken,
                ["client_id"] = SessionOidcOptions.ClientId,
                ["client_secret"] = sessionOptions.ClientSecret,
            }),
        };

        using var response = await httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            if (response.StatusCode == HttpStatusCode.BadRequest && IsInvalidGrant(body))
            {
                throw new RefreshGrantRejectedException();
            }

            LogUnexpectedFailure("refresh_token grant", response.StatusCode, body);

            throw new HttpRequestException(
                $"Keycloak token endpoint responded {(int)response.StatusCode} to a refresh_token grant.",
                inner: null,
                response.StatusCode);
        }

        try
        {
            return ParseSuccess(body);
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException)
        {
            // Never the body itself (Keycloak's own error responses
            // can echo request fields back) — a fixed message names the
            // failure, nothing it read.
            logger.LogError("Keycloak token endpoint returned an unparsable body for a refresh_token grant.");
            throw new HttpRequestException(
                "Keycloak token endpoint returned an unparsable body for a refresh_token grant.", ex);
        }
    }

    /// <summary>
    /// Back-channel logout — a confidential client POSTs
    /// <c>client_id</c>/<c>client_secret</c>/<c>refresh_token</c> to
    /// Keycloak's own <c>end_session_endpoint</c>, which ends the SSO session
    /// immediately, before <c>SessionEndpoints.LogoutAsync</c> clears our own
    /// cookie. <c>invalid_grant</c> (the session was already gone at
    /// Keycloak) is treated as success, same as an ordinary 2xx — logout is
    /// idempotent either way.
    /// </summary>
    /// <exception cref="HttpRequestException">
    /// Network, timeout, discovery never obtained, or any status other than
    /// success/<c>invalid_grant</c> — the caller (<c>SessionEndpoints.LogoutAsync</c>)
    /// turns this into 503 without clearing the cookie.
    /// </exception>
    public async Task LogoutAsync(string refreshToken, CancellationToken cancellationToken)
    {
        var configuration = await GetConfigurationAsync(cancellationToken);
        var httpClient = httpClientFactory.CreateClient(HttpClientName);

        using var request = new HttpRequestMessage(HttpMethod.Post, configuration.EndSessionEndpoint)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = SessionOidcOptions.ClientId,
                ["client_secret"] = sessionOptions.ClientSecret,
                ["refresh_token"] = refreshToken,
            }),
        };

        using var response = await httpClient.SendAsync(request, cancellationToken);

        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (response.StatusCode == HttpStatusCode.BadRequest && IsInvalidGrant(body))
        {
            return;
        }

        LogUnexpectedFailure("back-channel logout", response.StatusCode, body);

        throw new HttpRequestException(
            $"Keycloak end_session_endpoint responded {(int)response.StatusCode} to a back-channel logout.",
            inner: null,
            response.StatusCode);
    }

    /// <summary>
    /// <c>SessionEndpoints.LogoutAsync</c> needs
    /// <c>end_session_endpoint</c> to build the front-channel <c>logoutUrl</c>
    /// it returns to the panel — through this same wrapper, so a discovery
    /// failure right here counts as "Keycloak unreachable" exactly like it
    /// does for <see cref="RefreshAsync"/>/<see cref="LogoutAsync"/> (503,
    /// cookie untouched), not a raw 500.
    /// </summary>
    public Task<OpenIdConnectConfiguration> GetLogoutDiscoveryAsync(CancellationToken cancellationToken) =>
        GetConfigurationAsync(cancellationToken);

    /// <summary>
    /// The real <c>ConfigurationManager&lt;T&gt;</c>
    /// throws <see cref="InvalidOperationException"/>, not
    /// <see cref="HttpRequestException"/>, when it has never once obtained
    /// discovery (Keycloak was already down at process start) — folded here
    /// into the same exception type as every other "Keycloak unreachable"
    /// case, so callers have exactly one type to catch.
    /// </summary>
    private async Task<OpenIdConnectConfiguration> GetConfigurationAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await configurationManager.GetConfigurationAsync(cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            throw new HttpRequestException("Keycloak realm discovery is unavailable.", ex);
        }
    }

    /// <summary>
    /// A 4xx other than <c>invalid_grant</c> — most often
    /// <c>invalid_client</c> from a client-secret mismatch — used to surface
    /// only as an endless, silent 503, indistinguishable from Keycloak
    /// actually being down. Logs the status and Keycloak's own <c>error</c>
    /// code, and nothing else: never the raw body, which could echo
    /// the refresh token or the secret back on a malformed request.
    /// </summary>
    private void LogUnexpectedFailure(string operation, HttpStatusCode statusCode, string body) =>
        logger.LogError(
            "Keycloak rejected a {Operation} with {Status} {ErrorCode} — the panel sees a 503 either way, " +
            "but this one is worth investigating: a client misconfiguration looks identical to Keycloak being down otherwise.",
            operation, (int)statusCode, ExtractErrorCode(body) ?? "(no error field)");

    private static string? ExtractErrorCode(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            return document.RootElement.TryGetProperty("error", out var error) ? error.GetString() : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static RefreshResult ParseSuccess(string body)
    {
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;

        return new RefreshResult(
            AccessToken: root.GetProperty("access_token").GetString()!,
            ExpiresIn: TimeSpan.FromSeconds(root.GetProperty("expires_in").GetInt64()),
            RefreshToken: root.GetProperty("refresh_token").GetString()!,
            IdToken: root.GetProperty("id_token").GetString()!,
            // ValueKind check first: JsonElement.TryGetInt64 throws
            // InvalidOperationException (not a graceful false) when the
            // element isn't a number at all — a non-numeric
            // refresh_expires_in (or any other unexpected shape) must be
            // treated as absent, not crash the renewal.
            RefreshExpiresIn: root.TryGetProperty("refresh_expires_in", out var refreshExpiresIn)
                    && refreshExpiresIn.ValueKind == JsonValueKind.Number
                    && refreshExpiresIn.TryGetInt64(out var seconds)
                ? TimeSpan.FromSeconds(seconds)
                : null);
    }

    private static bool IsInvalidGrant(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            return document.RootElement.TryGetProperty("error", out var error)
                && string.Equals(error.GetString(), "invalid_grant", StringComparison.Ordinal);
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
