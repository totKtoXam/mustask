using System.Security.Claims;
using System.Text.Json;

namespace Mustask.Api.Authentication;

/// <summary>
/// Права из <c>resource_access.&lt;audience&gt;.roles</c> → claim
/// <c>perm</c>. Чистая функция над
/// <see cref="ClaimsPrincipal"/>, вызывается из
/// <c>JwtBearerEvents.OnTokenValidated</c>. <c>realm_access</c>,
/// <c>resource_access</c> других клиентов и <c>groups</c> не читаются вовсе
/// — негативный сценарий (права из чужих источников) закрыт конструкцией,
/// а не фильтром.
/// </summary>
public static class KeycloakPermissionClaims
{
    private const string ResourceAccessClaimType = "resource_access";
    private const string RolesPropertyName = "roles";
    public const string PermissionClaimType = "perm";

    /// <summary>
    /// Отсутствие <c>resource_access</c> или пустой список ролей — не
    /// ошибка: принципал аутентифицирован без прав, любой защищённый
    /// маршрут даёт 403 (deny-all fallback не меняется).
    /// </summary>
    public static void Append(ClaimsPrincipal principal, string audience)
    {
        var identity = principal.Identity as ClaimsIdentity;

        if (identity is null)
        {
            return;
        }

        var resourceAccess = principal.FindFirst(ResourceAccessClaimType);

        if (resourceAccess is null || string.IsNullOrEmpty(resourceAccess.Value))
        {
            return;
        }

        using var document = JsonDocument.Parse(resourceAccess.Value);

        if (document.RootElement.ValueKind != JsonValueKind.Object
            || !document.RootElement.TryGetProperty(audience, out var clientAccess)
            || clientAccess.ValueKind != JsonValueKind.Object
            || !clientAccess.TryGetProperty(RolesPropertyName, out var roles)
            || roles.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (var role in roles.EnumerateArray())
        {
            if (role.ValueKind == JsonValueKind.String && role.GetString() is { Length: > 0 } code)
            {
                identity.AddClaim(new Claim(PermissionClaimType, code));
            }
        }
    }
}
