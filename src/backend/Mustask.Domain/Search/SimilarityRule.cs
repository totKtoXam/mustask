namespace Mustask.Domain.Search;

/// <summary>
/// The typo tier (BR-GS-17, ADR-0044 § 2): <c>word_similarity(query, text) &gt;= Threshold</c> for a query of at least
/// <see cref="MinQueryLength"/> characters, only to fill a group below its limit. The threshold is one constant, pinned by the
/// near-miss table of the tests (<c>SearchDatabaseTests</c>); changing it is a code change with a test change.
/// </summary>
public static class SimilarityRule
{
    /// <summary>Starts at the prototype's 0.5.</summary>
    public const double Threshold = 0.5;

    /// <summary>The shortest folded query that adds similar rows (4 characters).</summary>
    public const int MinQueryLength = 4;

    public static bool Applies(int foldedQueryLength) => foldedQueryLength >= MinQueryLength;

    /// <summary>
    /// The tier-5 rule over one group's rows as the read returned them (ordered by tier, at most <c>limit + 1</c>): when tiers 1-4
    /// already give at least <paramref name="limit"/> rows the similar rows are dropped; otherwise they fill the group up to the
    /// limit and never displace one. <c>HasMore</c> is true when more than <paramref name="limit"/> rows remain after the rule, which
    /// can only come from visible rows: the read never saw a hidden one.
    /// </summary>
    public static (IReadOnlyList<T> Rows, bool HasMore) Fill<T>(IReadOnlyList<T> rows, Func<T, int> tierOf, int limit)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(tierOf);
        ArgumentOutOfRangeException.ThrowIfLessThan(limit, 1);

        var exact = rows.Where(row => tierOf(row) != MatchTier.Similar).ToList();

        if (exact.Count >= limit)
        {
            return (exact.Take(limit).ToList(), exact.Count > limit);
        }

        return (rows.Take(limit).ToList(), rows.Count > limit);
    }
}
