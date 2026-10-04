using Mustask.Application.Abstractions;
using Mustask.Application.Abstractions.UserDirectory;
using Mustask.Domain.UserDirectory;
using Mustask.Shared.Exceptions;
using Microsoft.Extensions.Logging;

namespace Mustask.Application.Features.UserDirectory.ApplySnapshotPage;

/// <summary>
/// B-3 (api-contract.md; ADR-0025 § 5): validate, then under the snapshot
/// lock — unknown → 404, not open → 409, a page already stored → its stored
/// counts; otherwise lock and load the page's users, apply the entries as
/// pushes stamping each user with the snapshot (<see cref="ProjectionBatch"/>),
/// and store the page's counts so completion can prove which pages arrived.
/// </summary>
public sealed class ApplySnapshotPageHandler(
    IDirectorySnapshotStore snapshots,
    IProjectedUserStore users,
    IUnitOfWork unitOfWork,
    IClock clock,
    ILogger<ApplySnapshotPageHandler> logger)
{
    public async Task<ApplySnapshotPageResult> Handle(ApplySnapshotPageCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (command.PageNumber < 0)
        {
            throw UserPushValidation.FieldRefusal(ApplySnapshotPageCommand.PageNumberField, UserDirectoryErrorCodes.ValueInvalid);
        }

        var now = clock.UtcNow;
        var pushes = UserPushValidation.Validate(command.Entries, ApplySnapshotPageCommand.EntriesField, isSnapshotPage: true, now);
        var ids = pushes.Select(push => push.Id).ToList();

        var (tally, appliedNow) = await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            await snapshots.LockAsync(ct);

            var snapshot = await snapshots.FindAsync(command.SnapshotId, ct)
                ?? throw new NotFoundException(UserDirectoryErrorCodes.SnapshotNotFound);

            snapshot.EnsureOpen();

            if (await snapshots.FindPageAsync(command.SnapshotId, command.PageNumber, ct) is { } stored)
            {
                return (stored.Tally, false);
            }

            var held = await users.LockAndLoadAsync(ids, ct);
            var result = ProjectionBatch.Apply(pushes, held, now, command.SnapshotId);

            users.Add(result.Added);
            snapshots.AddPage(DirectorySnapshotPage.Record(command.SnapshotId, command.PageNumber, result.Tally, now));

            return (result.Tally, true);
        }, cancellationToken);

        if (appliedNow)
        {
            logger.LogInformation(
                "User directory: snapshot {SnapshotId} page {PageNumber} applied — received {Received}, added {Added}, changed {Changed}, unchanged {Unchanged}, held as deleted {HeldAsDeleted}.",
                command.SnapshotId, command.PageNumber, tally.Received, tally.Added, tally.Changed, tally.Unchanged, tally.HeldAsDeleted);
        }

        return new ApplySnapshotPageResult(new PageCountsResponse(
            tally.Received, tally.Added, tally.Changed, tally.Unchanged, tally.HeldAsDeleted));
    }
}
