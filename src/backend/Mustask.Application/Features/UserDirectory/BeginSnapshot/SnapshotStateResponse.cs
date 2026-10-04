using Mustask.Domain.UserDirectory;

namespace Mustask.Application.Features.UserDirectory.BeginSnapshot;

/// <summary><c>SnapshotState</c> (api-contract.md B.4): <c>state</c> is <c>open</c>, <c>completed</c> or <c>superseded</c>.</summary>
public sealed record SnapshotStateResponse(Guid SnapshotId, long EnumerationStart, string State)
{
    public static SnapshotStateResponse Of(DirectorySnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        return new SnapshotStateResponse(snapshot.Id, snapshot.EnumerationStart, StateName(snapshot.State));
    }

    /// <summary>The contract's spelling of a state — fixed words, not the enum's member names.</summary>
    public static string StateName(DirectorySnapshotState state) => state switch
    {
        DirectorySnapshotState.Open => "open",
        DirectorySnapshotState.Completed => "completed",
        DirectorySnapshotState.Superseded => "superseded",
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, "Unknown snapshot state."),
    };
}
