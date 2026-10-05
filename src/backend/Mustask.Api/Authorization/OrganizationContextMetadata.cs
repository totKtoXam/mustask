namespace Mustask.Api.Authorization;

/// <summary>
/// Marks an endpoint as organization-addressed (mod-002 D-g, BR-M1-20): its
/// route has an <c>organizationId</c> parameter, and
/// <see cref="OrganizationContextFilter"/> answers
/// <c>organizations.organization.not_found</c> before any handler runs when
/// that organization is malformed, unknown or not active. The inventory test
/// (<c>OrganizationContextInventoryTests</c>) holds every such route to it,
/// as the category inventory holds every route to a category.
/// </summary>
/// <param name="AcceptsDeactivated">
/// <c>true</c> for the one request a deactivated organization accepts, its
/// restore (api-contract § 1.7). Every other route answers not found for it.
/// </param>
public sealed record OrganizationContextMetadata(bool AcceptsDeactivated)
{
    /// <summary>The route parameter the filter reads. mod-001's process read already names it so (its D-9).</summary>
    public const string RouteParameter = "organizationId";
}
