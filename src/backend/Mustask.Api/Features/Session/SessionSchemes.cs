namespace Mustask.Api.Features.Session;

/// <summary>
/// Authentication scheme names for the panel's session (ADR-0022).
/// Deliberately distinct from
/// <see cref="Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerDefaults.AuthenticationScheme"/>
/// ("Bearer"), which stays the process-wide default scheme
/// (<c>JwtBearerSetup.AddMustaskAuthentication</c>) — every ordinary resource-
/// server route keeps authenticating against Bearer without knowing these
/// two schemes exist at all.
/// </summary>
public static class SessionSchemes
{
    /// <summary>Cookie scheme — issues and reads <c>mustask.session</c>.</summary>
    public const string Session = "Session";

    /// <summary>OpenID Connect scheme — <c>Mustask.Api</c> as the OIDC client for <c>mustask-panel</c>.</summary>
    public const string Oidc = "SessionOidc";
}
