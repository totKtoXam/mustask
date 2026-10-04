namespace Mustask.Application.Features.UserDirectory.ApplyUserChanges;

/// <summary>
/// <c>FeedCounts</c> (api-contract.md B.4): <c>added + changed + unchanged +
/// heldAsDeleted = received</c>. Slices do not reference each other, so the
/// snapshot page's identical shape is its own copy.
/// </summary>
public sealed record FeedCountsResponse(int Received, int Added, int Changed, int Unchanged, int HeldAsDeleted);
