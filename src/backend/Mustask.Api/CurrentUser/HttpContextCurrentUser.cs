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

    /// <summary>The token's subject. <c>MapInboundClaims</c> is off (<c>JwtBearerSetup</c>), so it arrives under its JWT name.</summary>
    private const string SubjectClaimType = "sub";

    /// <summary>
    /// The <c>sub</c> claim (mod-002 BR-M1-21). Keycloak always sets it, so a
    /// signed-in request without one is a defect, not a user to record as
    /// "anonymous": a creator record must name someone.
    /// </summary>
    public string SubjectId
    {
        get
        {
            var user = httpContextAccessor.HttpContext?.User;

            if (user?.Identity is not { IsAuthenticated: true })
            {
                throw new InvalidOperationException("The subject is read only on an authenticated request.");
            }

            var subject = user.FindFirst(SubjectClaimType)?.Value;

            return string.IsNullOrEmpty(subject)
                ? throw new InvalidOperationException("The authenticated request's token carries no subject (sub).")
                : subject;
        }
    }

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
