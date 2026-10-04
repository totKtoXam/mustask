using Mustask.Application.Abstractions.UserDirectory;
using Mustask.Domain.UserDirectory;
using Microsoft.EntityFrameworkCore;

namespace Mustask.Infrastructure.UserDirectory;

/// <summary><see cref="IProjectedUserStore"/> over <c>directory_users</c>.</summary>
public sealed class ProjectedUserStore(MustaskDbContext context) : IProjectedUserStore
{
    public async Task<IReadOnlyDictionary<string, ProjectedUser>> LockAndLoadAsync(
        IReadOnlyCollection<string> ids,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ids);

        var ordered = ids.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();

        if (ordered.Length == 0)
        {
            return new Dictionary<string, ProjectedUser>(StringComparer.Ordinal);
        }

        await UserDirectoryLocks.LockUsersAsync(context, ordered, cancellationToken);
        await UserDirectoryLocks.LockHeldRowsAsync(context, ordered, cancellationToken);

        var users = await context.DirectoryUsers
            .Where(user => ordered.Contains(user.Id))
            .ToListAsync(cancellationToken);

        return users.ToDictionary(user => user.Id, StringComparer.Ordinal);
    }

    public void Add(IEnumerable<ProjectedUser> users) => context.DirectoryUsers.AddRange(users);

    public Task<int> CountStampedAsync(Guid snapshotId, CancellationToken cancellationToken) =>
        context.DirectoryUsers.CountAsync(user => user.LastSnapshotId == snapshotId, cancellationToken);

    public async Task<IReadOnlyList<string>> FindAbsenceCandidatesAsync(
        Guid snapshotId,
        long enumerationStart,
        CancellationToken cancellationToken) =>
        await context.DirectoryUsers
            .AsNoTracking()
            .Where(user => user.Lifecycle == ProjectedUserLifecycle.Present
                && (user.LastSnapshotId == null || user.LastSnapshotId != snapshotId)
                && user.Moment < enumerationStart)
            .Select(user => user.Id)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<string>> FindStaleSortKeysAsync(
        string collationVersion,
        int limit,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(collationVersion);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(limit);

        return await context.DirectoryUsers
            .AsNoTracking()
            .Where(user => user.SortKeyVersion != collationVersion)
            .OrderBy(user => user.Id)
            .Take(limit)
            .Select(user => user.Id)
            .ToListAsync(cancellationToken);
    }
}
