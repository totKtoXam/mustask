using Mustask.Domain.UserDirectory;

namespace Mustask.Application.Abstractions.UserDirectory;

/// <summary>
/// The write side of the user projection, for the push channel's use cases
/// only (<c>Features/UserDirectory</c>) — consumers read through
/// <see cref="IUserDirectory"/> and never touch this (BR-UP-1; an
/// architecture rule holds it). Every method runs inside the caller's
/// transaction (<see cref="IUnitOfWork.ExecuteInTransactionAsync{TResult}"/>).
/// </summary>
/// <remarks>
/// Lock discipline (ADR-0025 § 6): a writer takes the per-user locks of every
/// id it will write, in one fixed order, before reading those users for
/// update — so two requests touching the same user apply one after the other,
/// and two requests never wait on each other in a cycle. Snapshot operations
/// take the snapshot lock (<see cref="IDirectorySnapshotStore.LockAsync"/>)
/// first; a changes request never takes it.
/// </remarks>
public interface IProjectedUserStore
{
    /// <summary>
    /// Takes the per-user locks of <paramref name="ids"/> — also of ids not
    /// held yet — and loads the held users among them for update.
    /// </summary>
    Task<IReadOnlyDictionary<string, ProjectedUser>> LockAndLoadAsync(IReadOnlyCollection<string> ids, CancellationToken cancellationToken);

    /// <summary>Adds users a batch created; saved with the transaction.</summary>
    void Add(IEnumerable<ProjectedUser> users);

    /// <summary>How many distinct users carry <paramref name="snapshotId"/> as their last snapshot.</summary>
    Task<int> CountStampedAsync(Guid snapshotId, CancellationToken cancellationToken);

    /// <summary>
    /// BR-UP-8's candidates for an absence: present users not carried by the
    /// snapshot whose held moment is older than its enumeration start. The
    /// domain re-checks each after it is locked.
    /// </summary>
    Task<IReadOnlyList<string>> FindAbsenceCandidatesAsync(Guid snapshotId, long enumerationStart, CancellationToken cancellationToken);

    /// <summary>
    /// Up to <paramref name="limit"/> users, of any lifecycle, whose stored sort key was produced by a collation
    /// version other than <paramref name="collationVersion"/> (ADR-0025 § 9), in id order. Read without locks; the
    /// caller locks them with the rest of its users in one <see cref="LockAndLoadAsync"/>.
    /// </summary>
    Task<IReadOnlyList<string>> FindStaleSortKeysAsync(string collationVersion, int limit, CancellationToken cancellationToken);
}
