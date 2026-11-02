using System.Text.Json;
using Microsoft.IdentityModel.JsonWebTokens;
using Mustask.Api.Authentication;

namespace Mustask.Api.Features.Session;

/// <summary>
/// Whether an access token that came straight from Keycloak's token endpoint carries the client role
/// <c>user</c> of <c>mustask-api</c> — the condition of having a session at all (tech-024, ADR-0033). The token is read,
/// not validated: it arrives over the back channel from the issuer the exchange was made with, and every later use of it
/// is validated by the bearer scheme.
/// </summary>
internal static class SessionAccessGate
{
    public static bool HoldsUserRole(string? accessToken, string audience)
    {
        if (string.IsNullOrEmpty(accessToken))
        {
            return false;
        }

        try
        {
            var token = new JsonWebToken(accessToken);

            return token.TryGetPayloadValue<JsonElement>("resource_access", out var resourceAccess)
                && KeycloakPermissionClaims.HoldsUserRole(resourceAccess, audience);
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}
