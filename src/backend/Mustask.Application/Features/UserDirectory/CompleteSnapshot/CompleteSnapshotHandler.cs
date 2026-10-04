using Mustask.Application.Abstractions;
using Mustask.Application.Abstractions.UserDirectory;
using Mustask.Domain.UserDirectory;
using Mustask.Shared.Errors;
using Mustask.Shared.Exceptions;
using Microsoft.Extensions.Logging;

namespace Mustask.Application.Features.UserDirectory.CompleteSnapshot;

/// <summary>
/// B-4 (api-contract.md; ADR-0025 § 5; BR-UP-8): under the snapshot lock —
/// unknown → 404; completed → the stored counts; superseded → 409; then the
/// proof of completeness (pages <c>0..pageCount-1</c> exactly, and exactly
/// <c>entryCount</c> users stamped with the snapshot), else 409 and nobody
/// marked. Only a proved snapshot marks absences: the candidates are locked
/// per user and re-read for update, and the domain re-checks each, so a push
/// that landed meanwhile wins. The same completion re-keys up to
/// <see cref="StaleSortKeysPerCompletion"/> users whose sort key another
/// collation version produced (ADR-0025 § 9) — the deleted and absent users
/// no page rewrites — locked together with the candidates.
/// </summary>
public sealed class CompleteSnapshotHandler(
    IDirectorySnapshotStore snapshots,
    IProjectedUserStore users,
    IUnitOfWork unitOfWork,
    IClock clock,
    ILogger<CompleteSnapshotHandler> logger)
{
    /// <summary>
    /// How many stale sort keys one completion recomputes: after an ICU change on the API host the deleted and
    /// absent users converge over successive reconciliations, and no completion's transaction grows with them.
    /// </summary>
    public const int StaleSortKeysPerCompletion = 1_000;

    public async Task<CompleteSnapshotResult> Handle(CompleteSnapshotCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var (pageCount, entryCount) = Validate(command);
        var now = clock.UtcNow;

        var result = await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            await snapshots.LockAsync(ct);

            var snapshot = await snapshots.FindAsync(command.SnapshotId, ct)
                ?? throw new NotFoundException(UserDirectoryErrorCodes.SnapshotNotFound);

            if (snapshot.State == DirectorySnapshotState.Completed)
            {
                return new CompleteSnapshotResult(SnapshotCountsResponse.Of(snapshot), CompletedNow: false);
            }

            snapshot.EnsureOpen();

            var pages = await snapshots.FindPagesAsync(command.SnapshotId, ct);
            var stamped = await users.CountStampedAsync(command.SnapshotId, ct);

            snapshot.EnsureCompletable(pageCount, entryCount, pages.Select(page => page.PageNumber).ToList(), stamped);

            var candidates = await users.FindAbsenceCandidatesAsync(command.SnapshotId, snapshot.EnumerationStart, ct);
            var staleKeys = await users.FindStaleSortKeysAsync(DirectoryCollation.Version, StaleSortKeysPerCompletion, ct);

            // One lock call for both sets: within it the per-user locks follow the one order every writer uses
            // (ADR-0025 § 6); a second call would take its keys after the first call's, out of that order.
            var held = await users.LockAndLoadAsync([.. candidates, .. staleKeys], ct);
            var absenceCandidates = candidates.ToHashSet(StringComparer.Ordinal);
            var markedAbsent = held.Values.Count(user =>
                absenceCandidates.Contains(user.Id)
                && user.LastSnapshotId != command.SnapshotId
                && user.MarkAbsent(snapshot.EnumerationStart, now));

            foreach (var user in held.Values)
            {
                user.RefreshSortKey();
            }

            snapshot.Complete(pageCount, entryCount, FeedTally.Sum(pages.Select(page => page.Tally)), markedAbsent, now);

            return new CompleteSnapshotResult(SnapshotCountsResponse.Of(snapshot), CompletedNow: true);
        }, cancellationToken);

        if (result.CompletedNow)
        {
            var counts = result.Response;

            // NFR Logging / B.6: the same counts the provider logs on its side;
            // users are not named here at all.
            logger.LogInformation(
                "User directory: snapshot {SnapshotId} completed — received {Received}, added {Added}, changed {Changed}, marked absent {MarkedAbsent}, held as deleted {HeldAsDeleted} ({PageCount} page(s), {EntryCount} entries).",
                command.SnapshotId, counts.Received, counts.Added, counts.Changed, counts.MarkedAbsent, counts.HeldAsDeleted, counts.PageCount, counts.EntryCount);
        }

        return result;
    }

    private static (int PageCount, int EntryCount) Validate(CompleteSnapshotCommand command)
    {
        var errors = new Dictionary<string, IReadOnlyList<FieldError>>(StringComparer.Ordinal);

        if (UserPushValidation.NonNegativeCountError(command.PageCount) is { } pageCountError)
        {
            errors[CompleteSnapshotCommand.PageCountField] = [pageCountError];
        }

        if (UserPushValidation.NonNegativeCountError(command.EntryCount) is { } entryCountError)
        {
            errors[CompleteSnapshotCommand.EntryCountField] = [entryCountError];
        }

        if (errors.Count > 0)
        {
            throw UserPushValidation.Refusal(errors);
        }

        return (command.PageCount!.Value, command.EntryCount!.Value);
    }
}
