using Mustask.Domain.UserDirectory;

namespace Mustask.Application.Abstractions.UserDirectory;

/// <summary>
/// mod-014 BR-UP-17 for consumers: a lookup or a member-restricted search takes at
/// most <see cref="UserDirectoryLimits.IdsPerRead"/> ids, and an organization may
/// hold more members than that, so a consumer splits the ids into batches of that
/// size and merges the answers — it never refuses (mod-016 spec § Volumes, US-1
/// "Past 500 ids…"). The one definition, here in Abstractions, so that no slice
/// calls another (backend.md, "Slice rules").
/// </summary>
public static class UserDirectoryBatches
{
    /// <summary>
    /// <see cref="IUserDirectory.LookupAsync"/> over any number of ids: one statement per 500 distinct ids, the answers
    /// merged. Each distinct id is in exactly one of the two sides of the result.
    /// </summary>
    public static async Task<UserLookupResult> LookupAllAsync(
        IUserDirectory directory, IEnumerable<string> ids, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(directory);
        ArgumentNullException.ThrowIfNull(ids);

        var known = new Dictionary<string, DirectoryUser>(StringComparer.Ordinal);
        var unknown = new List<string>();

        foreach (var batch in Batches(ids))
        {
            var result = await directory.LookupAsync(batch, cancellationToken);

            foreach (var (id, user) in result.Known)
            {
                known[id] = user;
            }

            unknown.AddRange(result.Unknown);
        }

        return new UserLookupResult(known, unknown);
    }

    /// <summary>
    /// <see cref="IUserDirectory.SearchAsync"/> restricted to <paramref name="ids"/> (active users only), split into
    /// batches of 500 and merged with <see cref="UserSearchResult.Merge"/>: at most 20 users in
    /// <see cref="DirectoryUserOrder"/>, <c>MoreMatched</c> when any batch or the merge says so. An empty restriction
    /// still goes to the directory once, so a text it would refuse is refused the same way.
    /// </summary>
    public static async Task<UserSearchResult> SearchAmongAsync(
        IUserDirectory directory, string text, IEnumerable<string> ids, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(directory);
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(ids);

        var batches = Batches(ids);

        if (batches.Count == 0)
        {
            return await directory.SearchAsync(new UserSearch(text, IncludeInactive: false, AmongIds: []), cancellationToken);
        }

        var answers = new List<UserSearchResult>(batches.Count);

        foreach (var batch in batches)
        {
            answers.Add(await directory.SearchAsync(new UserSearch(text, IncludeInactive: false, AmongIds: batch), cancellationToken));
        }

        return UserSearchResult.Merge(answers);
    }

    private static List<string[]> Batches(IEnumerable<string> ids) =>
        ids.Distinct(StringComparer.Ordinal)
            .Chunk(UserDirectoryLimits.IdsPerRead)
            .ToList();
}
