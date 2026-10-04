using Mustask.Application.Abstractions.UserDirectory;
using Mustask.Domain.UserDirectory;
using Microsoft.EntityFrameworkCore;

namespace Mustask.Infrastructure.UserDirectory;

/// <summary><see cref="IDirectorySnapshotStore"/> over <c>directory_snapshots</c> and <c>directory_snapshot_pages</c>.</summary>
public sealed class DirectorySnapshotStore(MustaskDbContext context) : IDirectorySnapshotStore
{
    public Task LockAsync(CancellationToken cancellationToken) =>
        UserDirectoryLocks.LockSnapshotsAsync(context, cancellationToken);

    public Task<DirectorySnapshot?> FindAsync(Guid snapshotId, CancellationToken cancellationToken) =>
        context.DirectorySnapshots.SingleOrDefaultAsync(snapshot => snapshot.Id == snapshotId, cancellationToken);

    public Task<long?> NewestEnumerationStartAsync(CancellationToken cancellationToken) =>
        context.DirectorySnapshots.MaxAsync(snapshot => (long?)snapshot.EnumerationStart, cancellationToken);

    public async Task<IReadOnlyList<DirectorySnapshot>> FindOpenAsync(CancellationToken cancellationToken) =>
        await context.DirectorySnapshots
            .Where(snapshot => snapshot.State == DirectorySnapshotState.Open)
            .ToListAsync(cancellationToken);

    public void Add(DirectorySnapshot snapshot) => context.DirectorySnapshots.Add(snapshot);

    /// <summary>
    /// One <c>DELETE</c>; the pages go with their snapshot by the foreign
    /// key's cascade. Called before anything of the snapshots is loaded for
    /// update in the same unit of work, so no tracked entity is deleted
    /// under the change tracker.
    /// </summary>
    public Task PruneAsync(int keep, CancellationToken cancellationToken)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(keep);

        var kept = context.DirectorySnapshots
            .OrderByDescending(snapshot => snapshot.EnumerationStart)
            .Take(keep)
            .Select(snapshot => snapshot.Id);

        return context.DirectorySnapshots
            .Where(snapshot => !kept.Contains(snapshot.Id))
            .ExecuteDeleteAsync(cancellationToken);
    }

    public Task<DirectorySnapshotPage?> FindPageAsync(Guid snapshotId, int pageNumber, CancellationToken cancellationToken) =>
        context.DirectorySnapshotPages
            .AsNoTracking()
            .SingleOrDefaultAsync(page => page.SnapshotId == snapshotId && page.PageNumber == pageNumber, cancellationToken);

    public async Task<IReadOnlyList<DirectorySnapshotPage>> FindPagesAsync(Guid snapshotId, CancellationToken cancellationToken) =>
        await context.DirectorySnapshotPages
            .AsNoTracking()
            .Where(page => page.SnapshotId == snapshotId)
            .OrderBy(page => page.PageNumber)
            .ToListAsync(cancellationToken);

    public void AddPage(DirectorySnapshotPage page) => context.DirectorySnapshotPages.Add(page);
}
