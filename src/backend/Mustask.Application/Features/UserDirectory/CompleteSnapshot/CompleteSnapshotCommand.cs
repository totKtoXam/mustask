namespace Mustask.Application.Features.UserDirectory.CompleteSnapshot;

/// <summary>
/// <c>PUT /api/user-directory/feed/snapshots/{snapshotId}/completion</c>
/// (api-contract.md B-4): the page and entry counts the provider sent, both
/// required and not negative — a realm with no users completes with
/// <c>0, 0</c>.
/// </summary>
public sealed record CompleteSnapshotCommand(Guid SnapshotId, int? PageCount, int? EntryCount)
{
    public const string PageCountField = "pageCount";

    public const string EntryCountField = "entryCount";
}
