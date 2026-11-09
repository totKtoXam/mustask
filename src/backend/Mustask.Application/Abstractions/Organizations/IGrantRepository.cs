using Mustask.Domain.Identity;

namespace Mustask.Application.Abstractions.Organizations;

/// <summary>One stored grant as the handlers see it (mod-019 BR-SA-1).</summary>
public sealed record GrantRecord(string UserId, GrantRole Role, string GrantedBy, DateTimeOffset GrantedAt);

/// <summary>The department above a project's node and whether the node inherits its access (api-contract § 2, <c>GrantList.inheritance</c>).</summary>
public sealed record ProjectInheritance(Guid DepartmentId, string DepartmentName, bool InheritsAccess);

/// <summary>What the addressed object of a grant route is (api-contract § 2, order of answers).</summary>
public enum GrantObjectState
{
    /// <summary>No such object in the organization, or one that is deactivated or deleted: the object's not-found.</summary>
    NotFound,

    /// <summary>The object exists but holds no grant: a project node or a sub-area (BR-SA-4, OQ-1 A).</summary>
    ScopeNotAllowed,

    /// <summary>A department, an active project or an active team.</summary>
    Ok,
}

/// <summary>
/// The grant tables (mod-019 plan-backend P-2) behind one interface, the scope choosing the table: slices share Abstractions, never
/// each other. The object is resolved and its row locked <c>FOR SHARE</c> (or <c>FOR NO KEY UPDATE</c> for a project) by the
/// caller's transaction, so a grant never lands on an object being deleted.
/// </summary>
public interface IGrantRepository
{
    /// <summary>The object of the address: its state, and — when <paramref name="lockRow"/> — its row locked in the current transaction.</summary>
    Task<GrantObjectState> ResolveAsync(ResourceScope scope, Guid organizationId, Guid objectId, bool lockRow, CancellationToken cancellationToken);

    /// <summary>The grants held on the object, unordered. Tracks nothing.</summary>
    Task<IReadOnlyList<GrantRecord>> ListAsync(ResourceScope scope, Guid objectId, CancellationToken cancellationToken);

    /// <summary>The grant a user holds on the object; <c>null</c> when none. Tracks nothing.</summary>
    Task<GrantRecord?> FindAsync(ResourceScope scope, Guid objectId, string userId, CancellationToken cancellationToken);

    /// <summary>The department above the project's node and the node's switch; <c>null</c> for a top-level project.</summary>
    Task<ProjectInheritance?> FindProjectInheritanceAsync(Guid organizationId, Guid projectId, CancellationToken cancellationToken);

    /// <summary>
    /// Stores a new grant. A concurrent give that committed first answers <c>grant.changed</c>; a membership that ended meanwhile
    /// answers <c>membership.not_found</c> (the composite key, BR-SA-3).
    /// </summary>
    Task AddAsync(
        ResourceScope scope, Guid organizationId, Guid objectId, string userId, GrantRole role, string grantedBy, DateTimeOffset now,
        CancellationToken cancellationToken);

    /// <summary>Replaces the role; <c>false</c> when the grant is gone or changed under the write.</summary>
    Task<bool> ChangeRoleAsync(
        ResourceScope scope, Guid objectId, string userId, GrantRole role, string grantedBy, DateTimeOffset now,
        CancellationToken cancellationToken);

    /// <summary>Removes the grant; <c>false</c> when there was none.</summary>
    Task<bool> RemoveAsync(ResourceScope scope, Guid objectId, string userId, CancellationToken cancellationToken);

    /// <summary>Removes every grant held on the object (a soft-deleted team ends its grants in its own transaction, BR-SA-3).</summary>
    Task RemoveAllAsync(ResourceScope scope, Guid objectId, CancellationToken cancellationToken);
}

/// <summary>
/// The grants that reach an object for the scoped check (ADR-0029 § 6, Reach, as ADR-0031 and ADR-0038 read it): one read for the
/// caller. <c>null</c> when the object is not in the organization, so the check judges the organization bundle alone.
/// </summary>
public interface IScopedAccessReadModel
{
    Task<IReadOnlyList<Mustask.Domain.Identity.ReachingGrant>?> ReachingGrantsAsync(
        Guid organizationId, string userId, ResourceScope target, Guid objectId, CancellationToken cancellationToken);

    /// <summary>The caller's grants and the organization's tree, read once, for the set form (lists, per-row flags, the tree's writable ids).</summary>
    Task<AccessSet> LoadSetAsync(Guid organizationId, string userId, OrganizationRole role, CancellationToken cancellationToken);
}
