using Mustask.Shared.Errors;

namespace Mustask.Domain.UserDirectory;

/// <summary>
/// The error codes of module <c>user_directory</c> — the registry of
/// ADR-0005, texts in <c>contracts/errors/user_directory.json</c>
/// (mod-014 api-contract.md, Part C). Declared in Domain because the domain
/// rules (<see cref="DirectorySnapshot"/>) throw some of them, and by Onion
/// the domain cannot reference Application.
/// </summary>
[ErrorCodes]
public static class UserDirectoryErrorCodes
{
    /// <summary>Top-level code of every validation refusal (A.6 and Part B).</summary>
    public const string ValidationFailed = "user_directory.validation_failed";

    /// <summary>Field reason: a required value missing or empty.</summary>
    public const string ValueRequired = "user_directory.value_required";

    /// <summary>Field reason: over a length limit (<c>params.limit</c>).</summary>
    public const string ValueTooLong = "user_directory.value_too_long";

    /// <summary>Field reason: a moment out of range, <c>deleted</c> in a snapshot page, a repeated id in a page, a negative number.</summary>
    public const string ValueInvalid = "user_directory.value_invalid";

    /// <summary>Field reason (<c>changes</c>, <c>entries</c>): over the entries limit of one push (<c>params.limit</c>).</summary>
    public const string TooManyEntries = "user_directory.too_many_entries";

    /// <summary>Field reason (<c>amongIds</c>, <c>ids</c>): over the ids limit of one read (<c>params.limit</c>).</summary>
    public const string TooManyIds = "user_directory.too_many_ids";

    /// <summary>The push body is not JSON of the declared shape, or over the size limit.</summary>
    public const string PushMalformed = "user_directory.push_malformed";

    /// <summary>The push signature or timestamp does not hold — which check failed is not said.</summary>
    public const string PushUnauthorized = "user_directory.push_unauthorized";

    /// <summary>No snapshot with this id.</summary>
    public const string SnapshotNotFound = "user_directory.snapshot_not_found";

    /// <summary>The snapshot is completed or superseded.</summary>
    public const string SnapshotNotOpen = "user_directory.snapshot_not_open";

    /// <summary>The snapshot id is begun with another enumeration start, or a newer snapshot is begun already.</summary>
    public const string SnapshotConflict = "user_directory.snapshot_conflict";

    /// <summary>The completion's counts do not match what arrived (<c>params.expected</c>, <c>params.received</c>).</summary>
    public const string SnapshotIncomplete = "user_directory.snapshot_incomplete";
}
