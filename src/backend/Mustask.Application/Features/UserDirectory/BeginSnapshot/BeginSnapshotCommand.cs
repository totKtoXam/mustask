namespace Mustask.Application.Features.UserDirectory.BeginSnapshot;

/// <summary>
/// <c>PUT /api/user-directory/feed/snapshots/{snapshotId}</c> (api-contract.md
/// B-2): begin a reconciliation snapshot with the marker the provider took
/// before it listed the realm's users.
/// </summary>
public sealed record BeginSnapshotCommand(Guid SnapshotId, long? EnumerationStart)
{
    /// <summary>The body's field, the key of a refusal.</summary>
    public const string EnumerationStartField = "enumerationStart";
}
