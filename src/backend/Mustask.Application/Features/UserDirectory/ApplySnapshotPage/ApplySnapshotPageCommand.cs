using Mustask.Domain.UserDirectory;

namespace Mustask.Application.Features.UserDirectory.ApplySnapshotPage;

/// <summary>
/// <c>PUT /api/user-directory/feed/snapshots/{snapshotId}/pages/{pageNumber}</c>
/// (api-contract.md B-3): one page of a snapshot, 1 to 500 entries, each id
/// at most once, no deletion — validated by the handler through
/// <see cref="UserPushValidation"/> under the field <see cref="EntriesField"/>.
/// </summary>
public sealed record ApplySnapshotPageCommand(Guid SnapshotId, int PageNumber, IReadOnlyList<UserPushEntry?>? Entries)
{
    /// <summary>The request's list field, the root of every entry's field key.</summary>
    public const string EntriesField = "entries";

    /// <summary>The route's page number, the key of its refusal.</summary>
    public const string PageNumberField = "pageNumber";
}
