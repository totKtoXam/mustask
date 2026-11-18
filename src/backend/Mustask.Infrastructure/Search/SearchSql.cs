using Mustask.Infrastructure.Organizations;

namespace Mustask.Infrastructure.Search;

/// <summary>
/// The SQL text both quick-search statement builders share (<see cref="SearchReadModel"/> for the structure groups and
/// <c>UserDirectory.SearchUsersStatement</c> for the users group): the folded column expression, the five tiers, the match predicate,
/// the similarity and the ordering wrapper. Neutral on purpose, so neither builder reaches into the other.
/// </summary>
internal static class SearchSql
{
    private static readonly string Collation = $"COLLATE \"{OrganizationsCollation.Russian}\"";

    /// <summary>The expression of an indexed folded column; the migration's index expressions are written the same.</summary>
    internal static string Folded(string column) => $"search_fold({column})";

    // The five tiers of BR-GS-8 over one folded text; NULL when the text does not match. The similar tier exists only for a query of
    // 4 characters or more (BR-GS-17), so a shorter query's statement does not mention it and the planner has nothing to doubt.
    internal static string TierOf(string folded, bool similar) =>
        $"CASE WHEN {folded} = @q THEN 1 WHEN {folded} LIKE @prefix ESCAPE '\\' THEN 2 WHEN {folded} ~ @word THEN 3 "
        + $"WHEN {folded} LIKE @contains ESCAPE '\\' THEN 4{(similar ? $" WHEN {folded} %> @q THEN 5" : string.Empty)} END";

    internal static string Matches(string folded, bool similar) =>
        $"({folded} LIKE @contains ESCAPE '\\'{(similar ? $" OR {folded} %> @q" : string.Empty)})";

    internal static string SimilarityOf(IEnumerable<string> folded, bool similar) =>
        similar ? "GREATEST(" + string.Join(", ", folded.Select(text => $"word_similarity(@q, {text})")) + ")" : "0.0";

    internal static string Wrap(string inner) =>
        "SELECT s.id::text AS \"Id\", s.name AS \"Name\", s.key AS \"Key\", s.department_id AS \"DepartmentId\", s.department_name AS \"DepartmentName\", "
        + "s.project_id AS \"ProjectId\", s.project_name AS \"ProjectName\", s.color AS \"Color\", s.email AS \"Email\", s.inactive AS \"Inactive\", s.tier AS \"Tier\" "
        + $"FROM ({inner}) s "
        + $"ORDER BY s.tier, CASE WHEN s.tier = 5 THEN -s.similarity END, s.name {Collation}, s.id LIMIT @take";
}
