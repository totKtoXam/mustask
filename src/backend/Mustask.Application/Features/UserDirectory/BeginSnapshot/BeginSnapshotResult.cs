namespace Mustask.Application.Features.UserDirectory.BeginSnapshot;

/// <summary>The handler's outcome — always <c>200</c> with the snapshot's state.</summary>
public sealed record BeginSnapshotResult(SnapshotStateResponse Response);
