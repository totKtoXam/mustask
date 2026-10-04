namespace Mustask.Application.Features.UserDirectory.CompleteSnapshot;

/// <summary>The handler's outcome — always <c>200</c> with the snapshot's counts (the stored ones when completed already).</summary>
/// <param name="CompletedNow">Whether this request completed it — only then is the completion logged.</param>
public sealed record CompleteSnapshotResult(SnapshotCountsResponse Response, bool CompletedNow);
