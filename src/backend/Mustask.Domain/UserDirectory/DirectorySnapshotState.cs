namespace Mustask.Domain.UserDirectory;

/// <summary>The state of a reconciliation snapshot (api-contract.md B-2..B-4). Stored as a string.</summary>
public enum DirectorySnapshotState
{
    /// <summary>Begun; pages may arrive; completion may be attempted.</summary>
    Open,

    /// <summary>Proved whole and its absences marked; final.</summary>
    Completed,

    /// <summary>A snapshot with a newer enumeration start was begun; final, marks nobody absent.</summary>
    Superseded,
}
