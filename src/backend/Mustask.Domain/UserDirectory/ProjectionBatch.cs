namespace Mustask.Domain.UserDirectory;

/// <summary>
/// The one place both apply paths — a changes request and a snapshot page —
/// go through (ADR-0025 § 6): each id's pushes in moment order, a missing user
/// created, the snapshot stamped on every user a page carried, and one count
/// per push (api-contract.md B.4).
/// </summary>
public static class ProjectionBatch
{
    /// <param name="pushes">The validated entries of one request, in request order.</param>
    /// <param name="held">The users already held for those ids, locked for update by the caller.</param>
    /// <param name="now">MUSTASK's clock, for <see cref="ProjectedUser.ChangedAt"/>.</param>
    /// <param name="snapshotId">The snapshot of a page, or null for a changes request.</param>
    public static ProjectionBatchResult Apply(
        IReadOnlyList<UserPush> pushes,
        IReadOnlyDictionary<string, ProjectedUser> held,
        DateTimeOffset now,
        Guid? snapshotId)
    {
        ArgumentNullException.ThrowIfNull(pushes);
        ArgumentNullException.ThrowIfNull(held);

        var users = new Dictionary<string, ProjectedUser>(held, StringComparer.Ordinal);
        var added = new List<ProjectedUser>();
        var deleted = new List<string>();
        var tally = FeedTally.Empty;

        foreach (var pushesOfOneUser in pushes.GroupBy(push => push.Id, StringComparer.Ordinal))
        {
            // Newest moment last, so it is the one that stays; on equal
            // moments a deletion last — a deletion wins anyway (BR-UP-6).
            var ordered = pushesOfOneUser
                .OrderBy(push => push.Moment)
                .ThenBy(push => push.Deleted);

            foreach (var push in ordered)
            {
                PushOutcome outcome;

                if (users.TryGetValue(push.Id, out var user))
                {
                    outcome = user.Apply(push, now);
                }
                else
                {
                    user = ProjectedUser.Create(push, now);
                    users.Add(push.Id, user);
                    added.Add(user);
                    outcome = PushOutcome.Added;
                }

                tally = tally.Count(outcome);

                // A deletion that changed something: the user was added as deleted, or held and now deleted. One that
                // finds the user deleted already is a duplicate (BR-UP-5) and changes nothing.
                if (push.Deleted && outcome is PushOutcome.Added or PushOutcome.Changed)
                {
                    deleted.Add(push.Id);
                }
            }

            if (snapshotId is { } snapshot)
            {
                users[pushesOfOneUser.Key].StampSnapshot(snapshot);
            }
        }

        return new ProjectionBatchResult(added, tally, deleted);
    }
}

/// <summary>The outcome of <see cref="ProjectionBatch.Apply"/>.</summary>
/// <param name="Added">The users the batch created; the caller adds them to the store.</param>
/// <param name="Tally">One count per push.</param>
/// <param name="Deleted">
/// The ids whose deletion the batch applied, once each, in request order (security pass S5): a deletion is final
/// (BR-UP-5), so the log names them. A deletion that found the user already deleted is not among them.
/// </param>
public sealed record ProjectionBatchResult(IReadOnlyList<ProjectedUser> Added, FeedTally Tally, IReadOnlyList<string> Deleted);
