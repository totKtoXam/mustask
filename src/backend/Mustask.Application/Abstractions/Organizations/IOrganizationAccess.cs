using Mustask.Domain.Identity;
using Mustask.Domain.Organizations;

namespace Mustask.Application.Abstractions.Organizations;

/// <summary>
/// One read, the access check's only one (ADR-0027 rule 3): the organization's
/// status joined to the caller's membership, <c>AsNoTracking</c>, nothing cached
/// beyond the request, no call to Keycloak and no read of the user projection
/// (BR-M1-32: a projected status never refuses or grants an access check).
/// </summary>
public interface IOrganizationAccessReadModel
{
    /// <summary>
    /// The organization's status and the user's role in it; <c>null</c> when there is no such organization. The role is
    /// <c>null</c> when the user holds no membership.
    /// </summary>
    Task<OrganizationAccessRow?> FindAsync(Guid organizationId, string userId, CancellationToken cancellationToken);
}

/// <param name="Status">The organization's status.</param>
/// <param name="Role">The caller's role in it, or <c>null</c> for a non-member.</param>
public sealed record OrganizationAccessRow(OrganizationStatus Status, OrganizationRole? Role);

/// <summary>
/// What the scoped check resolved for the current request (ADR-0027 rule 3,
/// step 4): the caller's role in the organization of the address. Set by the
/// check; a handler that needs a second code (a parent write needs the link
/// code as well, api-contract § 4.2) or the served access asks it. Implemented
/// in <c>Mustask.Api</c> over the request; scoped.
/// </summary>
public interface IOrganizationAccess
{
    /// <summary>The caller's role in the organization of the address.</summary>
    /// <exception cref="InvalidOperationException">The route had no scoped check (it does not carry an organization permission).</exception>
    OrganizationRole Role { get; }

    /// <summary>Whether the caller's role bundle holds <paramref name="code"/> (an organization code).</summary>
    bool Holds(string code);

    /// <summary>
    /// Whether the caller holds <paramref name="code"/> <b>on the object</b> (mod-019, ADR-0029 § 6): the role's bundle plus the bundles of
    /// the grants that reach it, the object being of the code's resource scope. An object that does not exist is judged by the role alone,
    /// so the second code of a nested id never discloses it. The default judges the role alone (a double without grants).
    /// </summary>
    Task<bool> HoldsOnAsync(string code, Guid objectId, CancellationToken cancellationToken) => Task.FromResult(Holds(code));

    /// <summary>The caller's grants and the tree, read once per request, for per-row flags and the set form (BR-SA-11).</summary>
    Task<AccessSet> SetAsync(CancellationToken cancellationToken) =>
        Task.FromResult(new AccessSet(Role, [], Empty, Empty, Empty));

    private static readonly IReadOnlyDictionary<Guid, IReadOnlyList<GrantRole>> Empty = new Dictionary<Guid, IReadOnlyList<GrantRole>>();
}
