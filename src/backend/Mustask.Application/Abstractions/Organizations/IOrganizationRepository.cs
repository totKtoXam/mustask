using Mustask.Domain.Organizations;

namespace Mustask.Application.Abstractions.Organizations;

/// <summary>
/// The write side of organizations. Every method that loads a row for a write
/// locks it first, by id with raw SQL, then loads it with a tracking query
/// (ADR-0010's technique), so the loaded row is the one the lock holder left
/// (plan-backend § 5.1, § 8).
/// </summary>
public interface IOrganizationRepository
{
    /// <summary>
    /// The stored name of the organization of any status whose normalized name is
    /// <paramref name="normalizedName"/>, other than <paramref name="exceptId"/>;
    /// <c>null</c> when the name is free (BR-M1-1). Tracks nothing.
    /// </summary>
    Task<string?> FindNameHolderAsync(string normalizedName, Guid? exceptId, CancellationToken cancellationToken);

    /// <summary>The organization of any status, locked <c>FOR NO KEY UPDATE</c>; <c>null</c> when there is none.</summary>
    Task<Organization?> LockAsync(Guid organizationId, CancellationToken cancellationToken);

    void Add(Organization organization);

    /// <summary>
    /// Flushes the request's context. A race on a mapped unique index becomes the
    /// same 409 code as its pre-check (plan-backend § 6.3); anything else is rethrown.
    /// </summary>
    Task SaveAsync(CancellationToken cancellationToken);
}
