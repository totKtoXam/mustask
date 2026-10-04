using Mustask.Domain.UserDirectory;

namespace Mustask.Application.Features.UserDirectory.CompleteSnapshot;

/// <summary>
/// <c>SnapshotCounts</c> (api-contract.md B.4): the sums of the snapshot's
/// pages, the users newly marked absent by its completion, and the counts the
/// completion declared.
/// </summary>
public sealed record SnapshotCountsResponse(
    int Received,
    int Added,
    int Changed,
    int Unchanged,
    int HeldAsDeleted,
    int MarkedAbsent,
    int PageCount,
    int EntryCount)
{
    /// <summary>The counts a completed snapshot stored.</summary>
    public static SnapshotCountsResponse Of(DirectorySnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        return new SnapshotCountsResponse(
            snapshot.Received,
            snapshot.Added,
            snapshot.Changed,
            snapshot.Unchanged,
            snapshot.HeldAsDeleted,
            snapshot.MarkedAbsent,
            snapshot.PageCount ?? 0,
            snapshot.EntryCount ?? 0);
    }
}
