using Mustask.Domain.WorkItems;

namespace Mustask.Application.Abstractions.WorkItems;

/// <summary>
/// The write side of work items (mod-003). Locks follow one order: the
/// project's number counter (creation only), then the item, then the area it
/// moves into. Each lock is taken before the row it guards is read, so the read
/// sees the row as the previous lock holder left it.
/// </summary>
public interface IWorkItemRepository
{
    /// <summary>
    /// Locks the project's number counter <c>FOR UPDATE</c>, creating it on the
    /// project's first creation. From here to the commit no other creation of
    /// the project proceeds (BR-M3-5).
    /// </summary>
    Task LockNumberSequenceAsync(Guid projectId, CancellationToken cancellationToken);

    /// <summary>
    /// The next number of the project: one more than every number it issued
    /// before. Needs <see cref="LockNumberSequenceAsync"/> first.
    /// </summary>
    Task<int> TakeNextNumberAsync(Guid projectId, CancellationToken cancellationToken);

    /// <summary>The item the submission <paramref name="requestId"/> created in the project, or <c>null</c> (BR-M3-26).</summary>
    Task<Guid?> FindCreatedByRequestAsync(Guid projectId, Guid requestId, CancellationToken cancellationToken);

    /// <summary>The project's item with that number, locked <c>FOR UPDATE</c> and tracked; <c>null</c> when there is none.</summary>
    Task<WorkItem?> LockAsync(Guid projectId, int number, CancellationToken cancellationToken);

    /// <summary>
    /// Locks the area of that project <c>FOR SHARE</c> and answers whether it
    /// exists (BR-M3-23, BR-M3-24). The area deletion locks it <c>FOR UPDATE</c>,
    /// so the two serialize: an area deleted meanwhile is gone when this lock is
    /// granted, and one that is still there cannot be deleted until the commit.
    /// </summary>
    Task<bool> LockAreaAsync(Guid projectId, Guid areaId, CancellationToken cancellationToken);

    void Add(WorkItem item);

    /// <summary>Flushes the tracked changes inside the caller's transaction.</summary>
    Task SaveAsync(CancellationToken cancellationToken);
}
