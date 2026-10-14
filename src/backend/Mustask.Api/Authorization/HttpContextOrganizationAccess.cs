using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Identity;

namespace Mustask.Api.Authorization;

/// <summary>
/// <see cref="IOrganizationAccess"/> over the current request: the role the scoped check resolved
/// (<see cref="OrganizationPermissionHandler"/>), kept in <c>HttpContext.Items</c> for exactly this request. Asking on a
/// route without the scoped check is a defect, not a missing right: it throws.
/// </summary>
internal sealed class HttpContextOrganizationAccess(IHttpContextAccessor httpContextAccessor) : IOrganizationAccess
{
    /// <summary>The key of the resolved access in <c>HttpContext.Items</c>.</summary>
    internal const string ItemKey = "Mustask.Api.Authorization.OrganizationAccess";

    public OrganizationRole Role =>
        httpContextAccessor.HttpContext?.Items[ItemKey] is ResolvedOrganizationAccess resolved
            ? resolved.Role
            : throw new InvalidOperationException(
                "No organization access was resolved for this request: the route does not carry RequiresOrganizationPermission.");

    public bool Holds(string code) => OrganizationRoleBundles.Holds(Role, code);
}
