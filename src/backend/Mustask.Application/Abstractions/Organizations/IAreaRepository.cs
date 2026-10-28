using Mustask.Domain.Organizations;

namespace Mustask.Application.Abstractions.Organizations;

/// <summary>
/// The write side of areas (mod-018). Every statement carries the organization, so an id of another organization locks and finds
/// nothing (BR-AW-1, ADR-0028 rule 4). The lock order is ADR-0028 rule 8's: the organization's tree lock first, then the area rows,
/// by id. <b>A locked row's presence is the lock statement's own answer</b> (A-9's corollary): the <c>Lock…</c> methods return what
/// the statement locked, and load the tracked row only after it said the row is there.
/// </summary>
public interface IAreaRepository
{
    /// <summary>
    /// Takes the organization's area-tree lock <c>FOR UPDATE</c>: every write that changes the tree's shape (a creation, a move, a
    /// deletion) holds it, then reads the tree it decides on (ADR-0028 rule 5). A rename and a link do not take it.
    /// </summary>
    Task LockTreeAsync(Guid organizationId, CancellationToken cancellationToken);

    /// <summary>The area of that organization, untracked and unlocked; <c>null</c> when there is none. For a not-found answer before any lock.</summary>
    Task<Area?> FindAsync(Guid organizationId, Guid areaId, CancellationToken cancellationToken);

    /// <summary>The area, locked <c>FOR UPDATE</c> and tracked; <c>null</c> when the locking statement found none.</summary>
    Task<Area?> LockForUpdateAsync(Guid organizationId, Guid areaId, CancellationToken cancellationToken);

    /// <summary>The area, locked <c>FOR SHARE</c> (a binder's lock) and tracked; <c>null</c> when the locking statement found none.</summary>
    Task<Area?> LockForShareAsync(Guid organizationId, Guid areaId, CancellationToken cancellationToken);

    /// <summary>
    /// Locks every one of <paramref name="areaIds"/> that exists <c>FOR SHARE</c> in one statement ordered by id, and answers the ids
    /// the statement locked with their types (a type never changes). The caller compares the count with the ids it asked for.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, AreaType>> LockManyForShareAsync(Guid organizationId, IReadOnlyCollection<Guid> areaIds, CancellationToken cancellationToken);

    /// <summary>
    /// The project's node, locked <c>FOR UPDATE</c> and tracked (a project's rename carries its name to it; a project's move locks it);
    /// <c>null</c> when the locking statement found none.
    /// </summary>
    Task<Area?> LockProjectNodeAsync(Guid organizationId, Guid projectId, CancellationToken cancellationToken);

    Task<bool> HasChildrenAsync(Guid organizationId, Guid areaId, CancellationToken cancellationToken);

    /// <summary>
    /// Whether a child of <paramref name="parentId"/> (<c>null</c>: a top-level area), other than <paramref name="exceptId"/>, holds the
    /// normalized name (BR-AW-4).
    /// </summary>
    Task<bool> IsSiblingNameTakenAsync(
        Guid organizationId, Guid? parentId, string normalizedName, Guid? exceptId, CancellationToken cancellationToken);

    /// <summary>Every node of the organization as <see cref="AreaNode"/> rows (type and project included) in display order: the input of <see cref="AreaTree"/>. Committed rows; tracks nothing.</summary>
    Task<IReadOnlyList<AreaNode>> GetNodesAsync(Guid organizationId, CancellationToken cancellationToken);

    void Add(Area area);

    void Remove(Area area);

    /// <inheritdoc cref="IOrganizationRepository.SaveAsync"/>
    Task SaveAsync(CancellationToken cancellationToken);
}
