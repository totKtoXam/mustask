namespace Mustask.Application.Features.UserDirectory.ApplySnapshotPage;

/// <summary>
/// <c>FeedCounts</c> of one page (api-contract.md B.4): <c>added + changed +
/// unchanged + heldAsDeleted = received</c>. The same JSON shape as a changes
/// request's answer; slices do not reference each other, so this is its own copy.
/// </summary>
public sealed record PageCountsResponse(int Received, int Added, int Changed, int Unchanged, int HeldAsDeleted);
