using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Mustask.Api.Authentication;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace Mustask.Api.Features.Session;

/// <summary>
/// The id token of every renewal is validated the
/// same way the OIDC handler already validates the one from the code
/// exchange — signature against the realm's JWKS, <c>iss</c>, <c>aud</c> =
/// <c>mustask-panel</c>, lifetime — plus OIDC Core §12.2's own requirement that a
/// refreshed id token carries the same <c>sub</c> and (if the ticket had
/// one) the same <c>sid</c> as before. <see cref="RefreshGrantClient"/> talks
/// to Keycloak's token endpoint directly, not through
/// <c>OpenIdConnectHandler</c> — nothing validates this id token unless this
/// class does.
/// </summary>
public sealed class RenewedIdTokenValidator(
    IConfigurationManager<OpenIdConnectConfiguration> configurationManager, KeycloakOptions keycloak)
{
    // MapInboundClaims defaults to true on a bare JwtSecurityTokenHandler —
    // it would rename "sub"/"sid" to their long ClaimTypes.* URIs, and the
    // comparisons below read the raw claim types (same convention as
    // JwtBearerSetup.MapInboundClaims = false and the cookie ticket's own
    // principal, which the OIDC handler validates with mapping already off).
    private static readonly JwtSecurityTokenHandler Handler = new() { MapInboundClaims = false };

    /// <exception cref="RenewedIdTokenRejectedException">
    /// Signature/issuer/audience/lifetime failed, or <c>sub</c>/<c>sid</c>
    /// does not match <paramref name="expectedSub"/>/<paramref name="expectedSid"/>.
    /// <see cref="RenewedIdTokenRejectedException.Reason"/> is safe to log —
    /// it never contains the token.
    /// </exception>
    public async Task<ClaimsPrincipal> ValidateAsync(
        string idToken, string? expectedSub, string? expectedSid, CancellationToken cancellationToken)
    {
        var configuration = await configurationManager.GetConfigurationAsync(cancellationToken);

        ClaimsPrincipal principal;

        try
        {
            principal = Handler.ValidateToken(idToken, BuildParameters(configuration), out _);
        }
        catch (SecurityTokenSignatureKeyNotFoundException)
        {
            // RequestRefresh() only marks
            // the cached configuration stale and schedules a background
            // fetch — Microsoft.IdentityModel.Protocols.OpenIdConnect
            // 8.19.2's ConfigurationManager<T> does not fetch synchronously.
            // The GetConfigurationAsync() call right below therefore usually
            // still returns the pre-rotation keys (the discovery + JWKS
            // round trip finishes in the background, ~300 ms later), so this
            // retry often fails on the very same unknown "kid" as the first
            // attempt. The practical effect: the first renewal after a
            // realm key rotation can still end in 401 session.expired
            // — the browser is bounced back to /login, but Keycloak's
            // own SSO session is untouched, so the re-login itself needs no
            // password. What this retry does buy is real, just narrower
            // than it looks: it shrinks the window from the readiness
            // monitor's tick (RealmReadinessMonitor.CheckInterval, ≤ 30 s)
            // to RefreshInterval (≤ 10 s, RealmMetadata.cs) plus one JWKS
            // fetch, instead of leaving every renewal in that window
            // rejected. Making the first renewal succeed too — e.g.
            // bypassing the cache and fetching a guaranteed-current
            // configuration on this exception — is left as a possible
            // follow-up, not done here. No loop, and no
            // refresh for any other validation failure — a bad
            // iss/aud/lifetime/sub/sid is not something a refresh can fix.
            configurationManager.RequestRefresh();
            configuration = await configurationManager.GetConfigurationAsync(cancellationToken);

            try
            {
                principal = Handler.ValidateToken(idToken, BuildParameters(configuration), out _);
            }
            catch (Exception ex) when (ex is SecurityTokenException or ArgumentException)
            {
                throw new RenewedIdTokenRejectedException("signature/issuer/audience/lifetime check failed", ex);
            }
        }
        catch (Exception ex) when (ex is SecurityTokenException or ArgumentException)
        {
            throw new RenewedIdTokenRejectedException("signature/issuer/audience/lifetime check failed", ex);
        }

        var sub = principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;

        if (!string.Equals(sub, expectedSub, StringComparison.Ordinal))
        {
            throw new RenewedIdTokenRejectedException("sub does not match the session's sub");
        }

        var sid = principal.FindFirst(SessionAuthenticationSetup.SidClaimType)?.Value;

        // OIDC Core §12.2: a refreshed id token MUST carry the same sid as
        // the original, when the original had one at all. A ticket that
        // never had a sid has nothing to compare against — that gap
        // is not this check's job to close.
        if (expectedSid is not null && !string.Equals(sid, expectedSid, StringComparison.Ordinal))
        {
            throw new RenewedIdTokenRejectedException("sid does not match the session's sid (OIDC Core §12.2)");
        }

        return principal;
    }

    private TokenValidationParameters BuildParameters(OpenIdConnectConfiguration configuration) => new()
    {
        ValidateIssuer = true,
        ValidIssuer = keycloak.Issuer,
        ValidateAudience = true,
        ValidAudience = SessionOidcOptions.ClientId,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        RequireSignedTokens = true,
        RequireExpirationTime = true,
        IssuerSigningKeys = configuration.SigningKeys,
        // Same restriction as the resource-server half (JwtBearerSetup):
        // only RS256, and only a key whose "kid" is actually registered
        // — never "try every key we happen to have".
        ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
        TryAllIssuerSigningKeys = false,
        ClockSkew = TimeSpan.FromSeconds(30),
    };
}
