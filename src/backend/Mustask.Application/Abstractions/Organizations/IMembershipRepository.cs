using Mustask.Domain.Identity;
using Mustask.Domain.Organizations;

namespace Mustask.Application.Abstractions.Organizations;

/// <summary>
/// The write side of organization memberships (mod-016 plan-backend § 3.4). Like
/// the other repositories it locks by key with raw SQL before it loads with the
/// tracking query (ADR-0010). The organization row itself is locked through
/// <see cref="IOrganizationRepository.LockAsync"/> — the lock order of
/// plan-backend § 3.8 is organization row, then member row.
/// </summary>
public interface IMembershipRepository
{
    /// <summary>Whether the user has a membership in the organization. Tracks nothing, takes no lock.</summary>
    Task<bool> ExistsAsync(Guid organizationId, string userId, CancellationToken cancellationToken);

    /// <summary>
    /// The user's role in the organization, or <c>null</c> when they hold no membership. Tracks nothing, takes no lock — the
    /// caller already holds the organization row, and every change of a role or a removal takes that row first, so what is read
    /// under it stays (plan-backend § 3.8: the actor read again).
    /// </summary>
    Task<OrganizationRole?> FindRoleAsync(Guid organizationId, string userId, CancellationToken cancellationToken);

    /// <summary>The user's membership, <c>FOR UPDATE</c>, tracked; <c>null</c> when there is none.</summary>
    Task<OrganizationMembership?> LockMemberAsync(Guid organizationId, string userId, CancellationToken cancellationToken);

    void Add(OrganizationMembership membership);

    /// <summary>
    /// Deletes the membership with one raw statement; the database cascade ends the user's team memberships in the same
    /// statement (BR-M1-36). Answers whether a row was deleted.
    /// </summary>
    Task<bool> DeleteAsync(Guid organizationId, string userId, CancellationToken cancellationToken);

    /// <summary>Flushes the request's context; a lost add race on the primary key is <c>already_member</c> (BR-M1-5).</summary>
    Task SaveAsync(CancellationToken cancellationToken);
}
