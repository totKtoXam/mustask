using Mustask.Domain.Identity;
using Microsoft.AspNetCore.Builder;

namespace Mustask.Api.Authorization;

/// <summary>
/// Четыре расширения — единственный способ открыть endpoint. Каждое
/// одновременно вешает метаданные для инвентаризационного теста и настоящую
/// политику авторизации рантайма.
/// </summary>
public static class EndpointCategories
{
    /// <summary>
    /// An <b>instance</b> code, held in the token. A code of the other scope is refused at mapping time: an organization
    /// code is never in a token, so the route would answer 403 to everyone, silently and for good (ADR-0027).
    /// </summary>
    public static TBuilder RequiresPermission<TBuilder>(this TBuilder builder, string permissionCode)
        where TBuilder : IEndpointConventionBuilder
    {
        EnsureScope(permissionCode, PermissionScope.Instance, nameof(RequiresOrganizationPermission));

        builder.WithMetadata(EndpointCategoryMetadata.Permission(permissionCode));
        return builder.RequireAuthorization(PermissionPolicies.PolicyName(permissionCode));
    }

    /// <summary>
    /// An <b>organization</b> code (ADR-0027, mod-016): the route carries <c>{organizationId}</c>, and the caller's membership in
    /// that organization must hold the code. The one declaration is the category and the enforcement together (the
    /// authorization requirement runs before the body is read, so 401, 404, 403 come in that order). A code of the other scope
    /// is refused at mapping time.
    /// </summary>
    /// <param name="acceptsDeactivated">Only the organization's restore passes <c>true</c>.</param>
    public static TBuilder RequiresOrganizationPermission<TBuilder>(this TBuilder builder, string permissionCode, bool acceptsDeactivated = false)
        where TBuilder : IEndpointConventionBuilder
    {
        EnsureScope(permissionCode, PermissionScope.Organization, nameof(RequiresPermission));

        builder.WithMetadata(EndpointCategoryMetadata.OrganizationPermission(permissionCode, acceptsDeactivated));
        return builder.RequireAuthorization(PermissionPolicies.OrganizationPolicyName(permissionCode, acceptsDeactivated));
    }

    public static TBuilder Public<TBuilder>(this TBuilder builder, string reason)
        where TBuilder : IEndpointConventionBuilder
    {
        builder.WithMetadata(EndpointCategoryMetadata.Public(reason));
        return builder.AllowAnonymous();
    }

    public static TBuilder AuthenticatedOnly<TBuilder>(this TBuilder builder, string reason)
        where TBuilder : IEndpointConventionBuilder
    {
        builder.WithMetadata(EndpointCategoryMetadata.Authenticated(reason));
        return builder.RequireAuthorization(PermissionPolicies.AuthenticatedOnlyPolicyName);
    }

    private static void EnsureScope(string permissionCode, PermissionScope expected, string rightMethod)
    {
        var actual = PermissionCatalog.ScopeOf(permissionCode);

        if (actual != expected)
        {
            throw new InvalidOperationException(
                $"'{permissionCode}' is a {actual.ToString().ToLowerInvariant()} code, not a {expected.ToString().ToLowerInvariant()} one: "
                + $"declare it with {rightMethod}. A code in the wrong place would be a permanent silent 403.");
        }
    }
}
