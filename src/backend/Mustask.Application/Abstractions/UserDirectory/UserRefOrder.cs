namespace Mustask.Application.Abstractions.UserDirectory;

/// <summary>
/// The order of every list that names people (mod-016 api-contract § 0.5):
/// known users in <see cref="DirectoryUserOrder"/> (display name under the
/// <c>ru</c> collation at primary strength, then id by code point), then the
/// users the projection does not know, by id. It sorts in memory the items a
/// page already holds; backend.md bans reading extra rows, not this.
/// </summary>
public static class UserRefOrder
{
    public static IReadOnlyList<T> Order<T>(
        IEnumerable<T> items,
        Func<T, string> userIdOf,
        IReadOnlyDictionary<string, DirectoryUser> known)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(userIdOf);
        ArgumentNullException.ThrowIfNull(known);

        var list = items.ToList();

        var named = list
            .Where(item => known.ContainsKey(userIdOf(item)))
            .OrderBy(item => known[userIdOf(item)], DirectoryUserOrder.Comparer);
        var nameless = list
            .Where(item => !known.ContainsKey(userIdOf(item)))
            .OrderBy(userIdOf, DirectoryUserOrder.IdComparer);

        return named.Concat(nameless).ToList();
    }
}
