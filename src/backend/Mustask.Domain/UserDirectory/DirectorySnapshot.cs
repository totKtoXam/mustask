namespace Mustask.Domain.UserDirectory;

/// <summary>
/// A reconciliation snapshot of the retired push channel (ADR-0025 § 5). Since tech-025 (ADR-0034) nothing creates,
/// reads or changes one: the type and its table <c>directory_snapshots</c> stay only so that the model matches the
/// database without a destructive migration. Data only, on purpose.
/// </summary>
public sealed class DirectorySnapshot
{
    private DirectorySnapshot()
    {
        // EF materialization.
    }

    public Guid Id { get; private set; }

    public long EnumerationStart { get; private set; }

    public DirectorySnapshotState State { get; private set; }

    public DateTimeOffset BegunAt { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }

    public int? PageCount { get; private set; }

    public int? EntryCount { get; private set; }

    public int Received { get; private set; }

    public int Added { get; private set; }

    public int Changed { get; private set; }

    public int Unchanged { get; private set; }

    public int HeldAsDeleted { get; private set; }

    public int MarkedAbsent { get; private set; }
}
