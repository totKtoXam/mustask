using Mustask.Application.Abstractions;
using Mustask.Application.Abstractions.UserDirectory;
using Mustask.Domain.UserDirectory;
using Microsoft.Extensions.Logging;

namespace Mustask.Application.Features.UserDirectory.BeginSnapshot;

/// <summary>
/// B-2 (api-contract.md; ADR-0025 § 5): under the snapshot lock — the same
/// id again answers its current state when begun with the same start and is
/// a conflict otherwise; a new id must start after every snapshot begun so
/// far, supersedes the open ones and is kept with the newest few.
/// </summary>
public sealed class BeginSnapshotHandler(
    IDirectorySnapshotStore snapshots,
    IUnitOfWork unitOfWork,
    IClock clock,
    ILogger<BeginSnapshotHandler> logger)
{
    public async Task<BeginSnapshotResult> Handle(BeginSnapshotCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var now = clock.UtcNow;
        var enumerationStart = UserPushValidation.RequireMoment(
            command.EnumerationStart, BeginSnapshotCommand.EnumerationStartField, now);

        var (response, superseded) = await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            await snapshots.LockAsync(ct);

            if (await snapshots.FindAsync(command.SnapshotId, ct) is { } existing)
            {
                existing.EnsureBegunWith(enumerationStart);

                return (SnapshotStateResponse.Of(existing), 0);
            }

            var snapshot = DirectorySnapshot.Begin(
                command.SnapshotId, enumerationStart, await snapshots.NewestEnumerationStartAsync(ct), now);

            // Pruned before anything is loaded for update, so no tracked
            // snapshot is deleted under the change tracker; one slot is left
            // for the snapshot begun here.
            await snapshots.PruneAsync(UserDirectoryLimits.SnapshotsKept - 1, ct);

            var open = await snapshots.FindOpenAsync(ct);

            foreach (var older in open)
            {
                older.Supersede();
            }

            snapshots.Add(snapshot);

            return (SnapshotStateResponse.Of(snapshot), open.Count);
        }, cancellationToken);

        logger.LogInformation(
            "User directory: snapshot {SnapshotId} begun at {EnumerationStart} ({State}); {Superseded} open snapshot(s) superseded.",
            response.SnapshotId, response.EnumerationStart, response.State, superseded);

        return new BeginSnapshotResult(response);
    }
}
