using Mustask.Application.Abstractions;
using Mustask.Application.Abstractions.UserDirectory;
using Mustask.Domain.UserDirectory;
using Microsoft.Extensions.Logging;

namespace Mustask.Application.Features.UserDirectory.ApplyUserChanges;

/// <summary>
/// B-1, the reference use case of the push channel (mod-014 plan § 5):
/// validate, lock and load the distinct ids, let the domain apply each push
/// (<see cref="ProjectionBatch"/>), add the new users, save — then log the
/// counts, naming users by id only (NFR Logging): every id of the request at
/// Debug, and at Information the ids whose deletion was applied — a reported
/// deletion is final (BR-UP-5) and Keycloak's own event log does not record one
/// pushed here (security pass S5).
/// </summary>
public sealed class ApplyUserChangesHandler(
    IProjectedUserStore users,
    IUnitOfWork unitOfWork,
    IClock clock,
    ILogger<ApplyUserChangesHandler> logger)
{
    public async Task<ApplyUserChangesResult> Handle(ApplyUserChangesCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var now = clock.UtcNow;
        var pushes = UserPushValidation.Validate(command.Changes, ApplyUserChangesCommand.Field, isSnapshotPage: false, now);
        var ids = pushes.Select(push => push.Id).Distinct(StringComparer.Ordinal).ToList();

        var (tally, deleted) = await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var held = await users.LockAndLoadAsync(ids, ct);
            var result = ProjectionBatch.Apply(pushes, held, now, snapshotId: null);

            users.Add(result.Added);

            return (result.Tally, result.Deleted);
        }, cancellationToken);

        logger.LogInformation(
            "User directory: changes applied — received {Received}, added {Added}, changed {Changed}, unchanged {Unchanged}, held as deleted {HeldAsDeleted}, deletions applied {Deletions}.",
            tally.Received, tally.Added, tally.Changed, tally.Unchanged, tally.HeldAsDeleted, deleted.Count);

        if (deleted.Count > 0)
        {
            logger.LogInformation("User directory: deletions applied for user ids {UserIds}.", string.Join(", ", deleted));
        }

        if (logger.IsEnabled(LogLevel.Debug))
        {
            logger.LogDebug("User directory: changes applied for user ids {UserIds}.", ids);
        }

        return new ApplyUserChangesResult(new FeedCountsResponse(
            tally.Received, tally.Added, tally.Changed, tally.Unchanged, tally.HeldAsDeleted));
    }
}
