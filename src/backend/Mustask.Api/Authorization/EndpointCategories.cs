using Microsoft.AspNetCore.Builder;

namespace Mustask.Api.Authorization;

/// <summary>
/// Три расширения — единственный способ открыть endpoint. Каждое
/// одновременно вешает метаданные для инвентаризационного теста и настоящую
/// политику авторизации рантайма.
/// </summary>
public static class EndpointCategories
{
    public static TBuilder RequiresPermission<TBuilder>(this TBuilder builder, string permissionCode)
        where TBuilder : IEndpointConventionBuilder
    {
        builder.WithMetadata(EndpointCategoryMetadata.Permission(permissionCode));
        return builder.RequireAuthorization(PermissionPolicies.PolicyName(permissionCode));
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
}
