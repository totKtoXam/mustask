using System.Security.Claims;
using System.Text.Json;
using Mustask.Domain.Identity;

namespace Mustask.Api.Authentication;

/// <summary>
/// Роли из <c>resource_access.&lt;audience&gt;.roles</c> → claim <c>perm</c>
/// по таблице <see cref="InstanceRoles"/> (tech-024, ADR-0033): токен несёт
/// имена четырёх клиентских ролей, а не коды прав; неизвестное имя роли
/// игнорируется, а принципал без роли <c>user</c> не получает ни одного
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

        foreach (var code in InstanceRoles.CodesOf(ReadRoles(resourceAccess.Value, audience)).Order(StringComparer.Ordinal))
        {
            identity.AddClaim(new Claim(PermissionClaimType, code));
        }
    }

    /// <summary>
    /// The role names of <c>resource_access.&lt;audience&gt;.roles</c> out of the claim's JSON (a whole
    /// <c>resource_access</c> object); empty when the claim is not that shape.
    /// </summary>
    public static IReadOnlyList<string> ReadRoles(string resourceAccessJson, string audience)
    {
        using var document = JsonDocument.Parse(resourceAccessJson);

        return ReadRoles(document.RootElement, audience);
    }

    /// <inheritdoc cref="ReadRoles(string, string)"/>
    public static IReadOnlyList<string> ReadRoles(JsonElement resourceAccess, string audience)
    {
        if (resourceAccess.ValueKind != JsonValueKind.Object
            || !resourceAccess.TryGetProperty(audience, out var clientAccess)
            || clientAccess.ValueKind != JsonValueKind.Object
            || !clientAccess.TryGetProperty(RolesPropertyName, out var roles)
            || roles.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return roles.EnumerateArray()
            .Where(role => role.ValueKind == JsonValueKind.String)
            .Select(role => role.GetString()!)
            .Where(role => role.Length > 0)
            .ToList();
    }

    /// <summary>True when the roles of <paramref name="audience"/> in <paramref name="resourceAccess"/> include <c>user</c>.</summary>
    public static bool HoldsUserRole(JsonElement resourceAccess, string audience) =>
        ReadRoles(resourceAccess, audience).Contains(InstanceRoles.User, StringComparer.Ordinal);
}
