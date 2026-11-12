using Mustask.Application.Abstractions;
using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Identity;

namespace Mustask.Api.Authorization;

/// <summary>
/// <see cref="IOrganizationAccess"/> over the current request: the role the scoped check resolved
/// (<see cref="OrganizationPermissionHandler"/>), kept in <c>HttpContext.Items</c> for exactly this request. Asking on a
/// route without the scoped check is a defect, not a missing right: it throws. The object-scoped questions (mod-019) read the
/// caller's grants through <see cref="IScopedAccessReadModel"/>; the set form is read once per request.
/// </summary>
internal sealed class HttpContextOrganizationAccess(
    IHttpContextAccessor httpContextAccessor, IScopedAccessReadModel scopedAccess, ICurrentUser currentUser) : IOrganizationAccess
{
    /// <summary>The key of the resolved access in <c>HttpContext.Items</c>.</summary>
    internal const string ItemKey = "Mustask.Api.Authorization.OrganizationAccess";

    private const string SetKey = "Mustask.Api.Authorization.AccessSet";

    private ResolvedOrganizationAccess Resolved =>
        httpContextAccessor.HttpContext?.Items[ItemKey] as ResolvedOrganizationAccess
            ?? throw new InvalidOperationException(
                "No organization access was resolved for this request: the route does not carry RequiresOrganizationPermission.");

    public OrganizationRole Role => Resolved.Role;

    public bool Holds(string code) => OrganizationRoleBundles.Holds(Role, code);

    public async Task<bool> HoldsOnAsync(string code, Guid objectId, CancellationToken cancellationToken)
    {
        var resolved = Resolved;
        var scope = PermissionCatalog.ResourceScopeOf(code);

        if (scope == ResourceScope.Organization || resolved.Role == OrganizationRole.Admin)
        {
            return OrganizationRoleBundles.Holds(resolved.Role, code);
        }

        var reaching = await scopedAccess.ReachingGrantsAsync(resolved.OrganizationId, currentUser.SubjectId, scope, objectId, cancellationToken) ?? [];

        return EffectivePermissions.Holds(resolved.Role, scope, reaching, code);
    }

    public async Task<AccessSet> SetAsync(CancellationToken cancellationToken)
    {
        var items = httpContextAccessor.HttpContext!.Items;

        if (items[SetKey] is AccessSet cached)
        {
            return cached;
        }

        var resolved = Resolved;
        var set = await scopedAccess.LoadSetAsync(resolved.OrganizationId, currentUser.SubjectId, resolved.Role, cancellationToken);
        items[SetKey] = set;

        return set;
    }
}
