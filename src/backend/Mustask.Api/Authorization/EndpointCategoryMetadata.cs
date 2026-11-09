using Mustask.Domain.Identity;

namespace Mustask.Api.Authorization;

public enum EndpointCategoryKind
{
    /// <summary>An instance code, held in the token (ADR-0018): <c>RequiresPermission</c>.</summary>
    Permission,
    Public,
    Authenticated,

    /// <summary>An organization code, derived from the caller's membership in the organization of the address (ADR-0027): <c>RequiresOrganizationPermission</c>.</summary>
    OrganizationPermission,
}

/// <summary>
/// Ровно одна такая метка обязана быть у каждого endpoint'а —
/// проверяется инвентаризационным тестом (`Mustask.Api.IntegrationTests`).
/// </summary>
public sealed class EndpointCategoryMetadata
{
    public EndpointCategoryKind Kind { get; }

    /// <summary>The code of a <see cref="EndpointCategoryKind.Permission"/> or <see cref="EndpointCategoryKind.OrganizationPermission"/> endpoint.</summary>
    public string? PermissionCode { get; }

    public string? Reason { get; }

    /// <summary>Only the restore of an organization accepts a deactivated one (<see cref="EndpointCategoryKind.OrganizationPermission"/> only).</summary>
    public bool AcceptsDeactivated { get; }

    /// <summary>The kind of object the route addresses (mod-019); Organization for a route that names no object of a scope.</summary>
    public ResourceScope Scope { get; }

    private EndpointCategoryMetadata(EndpointCategoryKind kind, string? permissionCode, string? reason, bool acceptsDeactivated = false, ResourceScope scope = ResourceScope.Organization)
    {
        Kind = kind;
        PermissionCode = permissionCode;
        Reason = reason;
        AcceptsDeactivated = acceptsDeactivated;
        Scope = scope;
    }

    public static EndpointCategoryMetadata Permission(string code) =>
        new(EndpointCategoryKind.Permission, code, reason: null);

    public static EndpointCategoryMetadata OrganizationPermission(string code, bool acceptsDeactivated, ResourceScope scope = ResourceScope.Organization) =>
        new(EndpointCategoryKind.OrganizationPermission, code, reason: null, acceptsDeactivated, scope);

    public static EndpointCategoryMetadata Public(string reason) =>
        new(EndpointCategoryKind.Public, permissionCode: null, reason);

    public static EndpointCategoryMetadata Authenticated(string reason) =>
        new(EndpointCategoryKind.Authenticated, permissionCode: null, reason);
}
