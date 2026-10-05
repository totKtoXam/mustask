using Mustask.Domain.Organizations;

namespace Mustask.Application.Abstractions.Organizations;

/// <summary>
/// The write side of areas. Locks follow one order (plan-backend § 8.2): the
/// project row, then team rows, then area rows, each in ascending id when there
/// are several. Ascending is <see cref="Guid.CompareTo(Guid)"/>, which orders as
/// PostgreSQL orders <c>uuid</c> (the canonical text, left to right).
/// </summary>
public interface IAreaRepository
{
    /// <summary>
    /// The area of that project, untracked and unlocked; <c>null</c> when there is
    /// none. For a not-found answer before the locks are taken: a later lock
    /// method loads the row again, as the lock holder left it.
    /// </summary>
    Task<Area?> FindAsync(Guid projectId, Guid areaId, CancellationToken cancellationToken);

    /// <summary>The area of that project, locked <c>FOR UPDATE</c> and tracked; <c>null</c> when there is none.</summary>
    Task<Area?> LockForUpdateAsync(Guid projectId, Guid areaId, CancellationToken cancellationToken);

    /// <summary>The area of that project, locked <c>FOR SHARE</c> and tracked; <c>null</c> when there is none.</summary>
    Task<Area?> LockForShareAsync(Guid projectId, Guid areaId, CancellationToken cancellationToken);

    /// <summary>The areas of that project among <paramref name="areaIds"/>, locked <c>FOR UPDATE</c> in ascending id order, tracked.</summary>
    Task<IReadOnlyList<Area>> LockManyForUpdateAsync(Guid projectId, IReadOnlyCollection<Guid> areaIds, CancellationToken cancellationToken);

    /// <summary>The project's root, locked <c>FOR UPDATE</c> or <c>FOR SHARE</c>, tracked.</summary>
    Task<Area> LockRootAsync(Guid projectId, bool forUpdate, CancellationToken cancellationToken);

    /// <summary>The team's default area, locked <c>FOR UPDATE</c> and tracked; <c>null</c> for a deleted team.</summary>
    Task<Area?> LockDefaultOfAsync(Guid teamId, CancellationToken cancellationToken);

    Task<bool> HasChildrenAsync(Guid areaId, CancellationToken cancellationToken);

    /// <summary>Whether a child of <paramref name="parentId"/>, other than <paramref name="exceptId"/>, holds the normalized name (BR-M1-12).</summary>
    Task<bool> IsSiblingNameTakenAsync(Guid parentId, string normalizedName, Guid? exceptId, CancellationToken cancellationToken);

    /// <summary>Every area of the project as <see cref="AreaNode"/> rows, the input of <see cref="AreaTree"/>. Reads the committed rows; tracks nothing.</summary>
    Task<IReadOnlyList<AreaNode>> GetNodesAsync(Guid projectId, CancellationToken cancellationToken);

    void Add(Area area);

    void Remove(Area area);

    /// <inheritdoc cref="IOrganizationRepository.SaveAsync"/>
    Task SaveAsync(CancellationToken cancellationToken);
}
