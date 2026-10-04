namespace Mustask.Application.Features.UserDirectory.ApplySnapshotPage;

/// <summary>The handler's outcome — always <c>200</c> with the page's counts (the stored ones for a page sent again).</summary>
public sealed record ApplySnapshotPageResult(PageCountsResponse Response);
