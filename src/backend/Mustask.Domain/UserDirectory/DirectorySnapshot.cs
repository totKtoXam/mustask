using Mustask.Shared.Errors;
using Mustask.Shared.Exceptions;

namespace Mustask.Domain.UserDirectory;

/// <summary>
/// A reconciliation snapshot (BR-UP-8; ADR-0025 § 5; api-contract.md B-2..B-4):
/// begun with the provider's enumeration start, filled by numbered pages, and
/// completed only when MUSTASK can prove it whole — the only moment absences
/// are marked. Only a snapshot with a newer enumeration start supersedes an
/// open one, and a superseded or interrupted snapshot marks nobody absent.
/// </summary>
public sealed class DirectorySnapshot
{
    private DirectorySnapshot()
    {
        // EF materialization.
    }

    public Guid Id { get; private set; }

    /// <summary>The marker the provider took before listing the realm's users.</summary>
    public long EnumerationStart { get; private set; }

    public DirectorySnapshotState State { get; private set; }

    public DateTimeOffset BegunAt { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }

    /// <summary>The page count the completion declared; null until completed.</summary>
    public int? PageCount { get; private set; }

    /// <summary>The entry count the completion declared; null until completed.</summary>
    public int? EntryCount { get; private set; }

    public int Received { get; private set; }

    public int Added { get; private set; }

    public int Changed { get; private set; }

    public int Unchanged { get; private set; }

    public int HeldAsDeleted { get; private set; }

    /// <summary>The users newly marked absent by this snapshot's completion.</summary>
    public int MarkedAbsent { get; private set; }

    /// <summary>The page totals stored at completion.</summary>
    public FeedTally Tally => new(Received, Added, Changed, Unchanged, HeldAsDeleted);

    /// <summary>
    /// A new snapshot. Its enumeration start must be greater than that of
    /// every snapshot begun so far (<paramref name="newestBegun"/>), or a
    /// replayed "begin" of an older run could supersede a newer one.
    /// </summary>
    public static DirectorySnapshot Begin(Guid id, long enumerationStart, long? newestBegun, DateTimeOffset now)
    {
        if (newestBegun is { } newest && enumerationStart <= newest)
        {
            throw new ConflictException(UserDirectoryErrorCodes.SnapshotConflict);
        }

        return new DirectorySnapshot
        {
            Id = id,
            EnumerationStart = enumerationStart,
            State = DirectorySnapshotState.Open,
            BegunAt = now,
        };
    }

    /// <summary>The same id begun again: idempotent with the same start, a conflict with another.</summary>
    public void EnsureBegunWith(long enumerationStart)
    {
        if (EnumerationStart != enumerationStart)
        {
            throw new ConflictException(UserDirectoryErrorCodes.SnapshotConflict);
        }
    }

    /// <summary>A newer snapshot was begun: an open one stops for good.</summary>
    public void Supersede()
    {
        if (State == DirectorySnapshotState.Open)
        {
            State = DirectorySnapshotState.Superseded;
        }
    }

    /// <summary>Pages are accepted only while open.</summary>
    public void EnsureOpen()
    {
        if (State != DirectorySnapshotState.Open)
        {
            throw new ConflictException(UserDirectoryErrorCodes.SnapshotNotOpen);
        }
    }

    /// <summary>
    /// The proof of completeness (ADR-0025 § 5): pages <c>0..pageCount-1</c>
    /// all applied and no other, and exactly <paramref name="entryCount"/>
    /// distinct users carrying this snapshot. Otherwise the snapshot stays
    /// open and nobody is marked.
    /// </summary>
    public void EnsureCompletable(int pageCount, int entryCount, IReadOnlyCollection<int> appliedPageNumbers, int stampedUsers)
    {
        ArgumentNullException.ThrowIfNull(appliedPageNumbers);

        EnsureOpen();

        var pagesWhole = appliedPageNumbers.Count == pageCount
            && appliedPageNumbers.All(page => page >= 0 && page < pageCount)
            && appliedPageNumbers.Distinct().Count() == pageCount;

        if (!pagesWhole || stampedUsers != entryCount)
        {
            throw new ConflictException(
                UserDirectoryErrorCodes.SnapshotIncomplete,
                new ErrorParams
                {
                    ["expected"] = ErrorParam.Number(entryCount),
                    ["received"] = ErrorParam.Number(stampedUsers),
                });
        }
    }

    /// <summary>Records a proved completion; call <see cref="EnsureCompletable"/> first.</summary>
    public void Complete(int pageCount, int entryCount, FeedTally pagesTally, int markedAbsent, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(pagesTally);

        EnsureOpen();

        State = DirectorySnapshotState.Completed;
        CompletedAt = now;
        PageCount = pageCount;
        EntryCount = entryCount;
        Received = pagesTally.Received;
        Added = pagesTally.Added;
        Changed = pagesTally.Changed;
        Unchanged = pagesTally.Unchanged;
        HeldAsDeleted = pagesTally.HeldAsDeleted;
        MarkedAbsent = markedAbsent;
    }
}
