using Mustask.Application.Abstractions;

namespace Mustask.Api.CurrentUser;

/// <summary>
/// Reads the identity of the acting employee from the current HTTP
/// request's Keycloak access token: <c>name</c>
/// (<see cref="Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerOptions.TokenValidationParameters"/>'s
/// <c>NameClaimType</c>, set to "name" in <c>JwtBearerSetup</c>) first, then
/// <c>preferred_username</c>, then the anonymous constant -- both claims are
/// put into the access token by the "profile" scope of the "mustask-panel"
/// client (deploy/keycloak/realms/mustask-realm.json). There is deliberately no
/// fallback to "sub": it is a UUID, not a name, and would silently pollute
/// dictionary authorship fields.
/// </summary>
internal sealed class HttpContextCurrentUser(IHttpContextAccessor httpContextAccessor) : ICurrentUser
{
    /// <summary>Truthful placeholder for "no acting user" -- an unauthenticated (public) request, or a token missing both claims.</summary>
    private const string Anonymous = "anonymous";

    private const string PreferredUsernameClaimType = "preferred_username";

    public string DisplayName
    {
        get
        {
            var user = httpContextAccessor.HttpContext?.User;

            if (user?.Identity is not { IsAuthenticated: true })
            {
                return Anonymous;
            }

            if (user.Identity.Name is { Length: > 0 } name)
            {
                return name;
            }

            var preferredUsername = user.FindFirst(PreferredUsernameClaimType)?.Value;

            return string.IsNullOrEmpty(preferredUsername) ? Anonymous : preferredUsername;
        }
    }
}
