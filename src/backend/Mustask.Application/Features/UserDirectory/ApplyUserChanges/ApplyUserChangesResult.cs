namespace Mustask.Application.Features.UserDirectory.ApplyUserChanges;

/// <summary>The handler's outcome — always <c>200</c> with the counts.</summary>
public sealed record ApplyUserChangesResult(FeedCountsResponse Response);
