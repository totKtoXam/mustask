namespace Mustask.Application.Abstractions.UserDirectory;

/// <summary>
/// The read model of the user projection (mod-014 api-contract.md, Part A;
/// BR-UP-11, BR-UP-14..17) — the fixed interface other modules' slices
/// (<c>mod-016-organization-access</c>, <c>mod-003-work-item-core</c>) build on
/// without reopening mod-014 (D-2 (3)). Both reads answer from MUSTASK's own
/// database and never call Keycloak; nothing here writes. Registered scoped:
/// it uses the request's <c>DbContext</c> and is not safe for concurrent calls
/// within one scope.
/// </summary>
public interface IUserDirectory
{
    /// <summary>
    /// BR-UP-14: at most 20 users whose display name or e-mail contains the
    /// text, in <see cref="DirectoryUserOrder"/>. A text under 2 characters
    /// answers nobody; over 100, or more than 500 <see cref="UserSearch.AmongIds"/>,
    /// is refused with a <c>ValidationException</c> (A.6).
    /// </summary>
    Task<UserSearchResult> SearchAsync(UserSearch search, CancellationToken cancellationToken);

    /// <summary>
    /// BR-UP-16/17: each distinct id in exactly one of <see cref="UserLookupResult.Known"/>
    /// and <see cref="UserLookupResult.Unknown"/>. More than 500 entries is refused (A.6).
    /// </summary>
    Task<UserLookupResult> LookupAsync(IReadOnlyCollection<string> ids, CancellationToken cancellationToken);
}

/// <summary>BR-UP-14. <paramref name="AmongIds"/> null: the whole projection.</summary>
public sealed record UserSearch(
    string Text,
    bool IncludeInactive = false,
    IReadOnlyCollection<string>? AmongIds = null);

/// <summary>BR-UP-11: exactly these four values — nothing else about a user is offered.</summary>
/// <param name="Id">The Keycloak user id — the <c>sub</c> of that user's tokens; opaque text.</param>
/// <param name="DisplayName">BR-UP-3; never empty.</param>
/// <param name="Email">As Keycloak holds it, letter case kept; empty when none. Not unique.</param>
/// <param name="IsActive">Enabled and neither deleted nor absent; false covers all three alike.</param>
public sealed record DirectoryUser(
    string Id,
    string DisplayName,
    string Email,
    bool IsActive);

/// <summary>BR-UP-14/15: at most 20 users, in <see cref="DirectoryUserOrder"/>; <paramref name="MoreMatched"/> when more than 20 matched.</summary>
public sealed record UserSearchResult(IReadOnlyList<DirectoryUser> Users, bool MoreMatched)
{
    /// <summary>
    /// BR-UP-17: merges the answers of one search split into batches of ids —
    /// users de-duplicated by id, ordered by <see cref="DirectoryUserOrder.Comparer"/>,
    /// the first 20 kept; more matched if any batch said so or more than 20 came back.
    /// </summary>
    public static UserSearchResult Merge(IEnumerable<UserSearchResult> batches)
    {
        ArgumentNullException.ThrowIfNull(batches);

        var moreMatched = false;
        var users = new Dictionary<string, DirectoryUser>(StringComparer.Ordinal);

        foreach (var batch in batches)
        {
            ArgumentNullException.ThrowIfNull(batch);

            moreMatched |= batch.MoreMatched;

            foreach (var user in batch.Users)
            {
                users.TryAdd(user.Id, user);
            }
        }

        var ordered = users.Values.Order(DirectoryUserOrder.Comparer).ToList();

        return new UserSearchResult(
            ordered.Take(DirectoryUserOrder.SearchAnswerSize).ToList(),
            moreMatched || ordered.Count > DirectoryUserOrder.SearchAnswerSize);
    }
}

/// <summary>BR-UP-16/17: every distinct requested id is in exactly one of the two.</summary>
public sealed record UserLookupResult(
    IReadOnlyDictionary<string, DirectoryUser> Known,
    IReadOnlyCollection<string> Unknown);
