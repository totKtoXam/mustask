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
        EnsureScope(permissionCode, PermissionSource.Instance, nameof(RequiresOrganizationPermission));

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
        EnsureScope(permissionCode, PermissionSource.Organization, nameof(RequiresPermission));

        builder.WithMetadata(EndpointCategoryMetadata.OrganizationPermission(permissionCode, acceptsDeactivated));
        return builder.RequireAuthorization(PermissionPolicies.OrganizationPolicyName(permissionCode, acceptsDeactivated));
    }

    /// <summary>
    /// An organization code checked <b>on an object of a scope</b> (mod-019, ADR-0029 § 7): the route carries <c>{organizationId}</c>
    /// and the id of its scope (<c>{areaId}</c>, <c>{projectId}</c> or <c>{teamId}</c>), and the caller must hold the code on that
    /// object — through the organization role's bundle or a grant that reaches it. The code's resource scope must be the route's or
    /// the organization's; any other pairing is refused at mapping time, since it would be a permanent silent 403.
    /// </summary>
    public static TBuilder RequiresOrganizationPermission<TBuilder>(this TBuilder builder, string permissionCode, ResourceScope scope)
        where TBuilder : IEndpointConventionBuilder
    {
        EnsureScope(permissionCode, PermissionSource.Organization, nameof(RequiresPermission));

        var codeScope = PermissionCatalog.ResourceScopeOf(permissionCode);

        if (scope == ResourceScope.Organization || (codeScope != ResourceScope.Organization && codeScope != scope))
        {
            throw new InvalidOperationException(
                $"'{permissionCode}' is checked on a {codeScope.ToString().ToLowerInvariant()}, not on a {scope.ToString().ToLowerInvariant()}: "
                + "declare the route's own scope, or use the overload without one for an organization route.");
        }

        builder.WithMetadata(EndpointCategoryMetadata.OrganizationPermission(permissionCode, acceptsDeactivated: false, scope));
        return builder.RequireAuthorization(PermissionPolicies.OrganizationPolicyName(permissionCode, acceptsDeactivated: false, scope));
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

    private static void EnsureScope(string permissionCode, PermissionSource expected, string rightMethod)
    {
        var actual = PermissionCatalog.SourceOf(permissionCode);

        if (actual != expected)
        {
            throw new InvalidOperationException(
                $"'{permissionCode}' is a {actual.ToString().ToLowerInvariant()} code, not a {expected.ToString().ToLowerInvariant()} one: "
                + $"declare it with {rightMethod}. A code in the wrong place would be a permanent silent 403.");
        }
    }
}
