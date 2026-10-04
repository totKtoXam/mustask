namespace Mustask.Domain.UserDirectory;

/// <summary>
/// One applied page of a snapshot, kept so that a page sent again answers the
/// counts stored the first time instead of being applied again (api-contract.md
/// B-3), and so that completion can prove which pages arrived (B-4).
/// </summary>
public sealed class DirectorySnapshotPage
{
    private DirectorySnapshotPage()
    {
        // EF materialization.
    }

    public Guid SnapshotId { get; private set; }

    public int PageNumber { get; private set; }

    public int Received { get; private set; }

    public int Added { get; private set; }

    public int Changed { get; private set; }

    public int Unchanged { get; private set; }

    public int HeldAsDeleted { get; private set; }

    public DateTimeOffset AppliedAt { get; private set; }

    public FeedTally Tally => new(Received, Added, Changed, Unchanged, HeldAsDeleted);

    public static DirectorySnapshotPage Record(Guid snapshotId, int pageNumber, FeedTally tally, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(tally);
        ArgumentOutOfRangeException.ThrowIfNegative(pageNumber);

        return new DirectorySnapshotPage
        {
            SnapshotId = snapshotId,
            PageNumber = pageNumber,
            Received = tally.Received,
            Added = tally.Added,
            Changed = tally.Changed,
            Unchanged = tally.Unchanged,
            HeldAsDeleted = tally.HeldAsDeleted,
            AppliedAt = now,
        };
    }
}
