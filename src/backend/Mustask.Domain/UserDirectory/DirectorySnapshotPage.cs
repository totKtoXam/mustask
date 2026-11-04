namespace Mustask.Domain.UserDirectory;

/// <summary>
/// One applied page of a snapshot of the retired push channel (ADR-0025 § 5). Dormant since tech-025 (ADR-0034), like
/// <see cref="DirectorySnapshot"/>: data only, kept so that the model matches the table
/// <c>directory_snapshot_pages</c> without a destructive migration.
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
}
