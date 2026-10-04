using Mustask.Domain.UserDirectory;

namespace Mustask.Application.Abstractions.UserDirectory;

/// <summary>
/// Reconciliation snapshots and their pages, for the push channel's snapshot
/// use cases only. Every method runs inside the caller's transaction, after
/// <see cref="LockAsync"/>.
/// </summary>
public interface IDirectorySnapshotStore
{
    /// <summary>
    /// The snapshot lock: begins, pages and completions never interleave.
    /// Taken before any per-user lock (<see cref="IProjectedUserStore"/>).
    /// </summary>
    Task LockAsync(CancellationToken cancellationToken);

    Task<DirectorySnapshot?> FindAsync(Guid snapshotId, CancellationToken cancellationToken);

    /// <summary>The greatest enumeration start of every snapshot kept, or null when none.</summary>
    Task<long?> NewestEnumerationStartAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<DirectorySnapshot>> FindOpenAsync(CancellationToken cancellationToken);

    void Add(DirectorySnapshot snapshot);

    /// <summary>Removes all but the <paramref name="keep"/> newest snapshots, with their pages.</summary>
    Task PruneAsync(int keep, CancellationToken cancellationToken);

    Task<DirectorySnapshotPage?> FindPageAsync(Guid snapshotId, int pageNumber, CancellationToken cancellationToken);

    Task<IReadOnlyList<DirectorySnapshotPage>> FindPagesAsync(Guid snapshotId, CancellationToken cancellationToken);

    void AddPage(DirectorySnapshotPage page);
}
