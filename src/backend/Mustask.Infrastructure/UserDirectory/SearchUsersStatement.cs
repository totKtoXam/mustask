using Mustask.Infrastructure.Search;

namespace Mustask.Infrastructure.UserDirectory;

/// <summary>
/// The users group of the quick search (feat-071): the statement that joins the user projection's table. This is an accepted
/// exception to the rule that consumers never read the projection's tables (mod-014 api-contract A.8), decided in ADR-0044 § 1 and
/// plan § 2.3: the search joins <c>directory_users</c> in its own statement, so a page of members costs one query. It lives in this
/// sub-slice so the projection's table name stays in one place; the SQL it shares with the structure groups is <see cref="SearchSql"/>.
/// The projection's <c>search_name</c> and <c>search_email</c> are folded on write (<c>ProjectedUser</c>), so the columns are used as
/// they are. Only the organization's current members are joined; a disabled or gone account is found and flagged (BR-GS-3).
/// </summary>
internal static class SearchUsersStatement
{
    /// <summary>The folded display-name column, as the read compares it and as its index (<c>ix_directory_users_search_name_trgm</c>) is built.</summary>
    internal const string NameColumn = "u.search_name";

    /// <summary>The folded e-mail column (<c>ix_directory_users_search_email_trgm</c>).</summary>
    internal const string EmailColumn = "u.search_email";

    internal static string Sql(string organizations, bool similar) =>
        SearchSql.Wrap(
            "SELECT u.id::text AS id, u.display_name AS name, NULL::text AS key, NULL::uuid AS department_id, NULL::text AS department_name, "
            + "NULL::uuid AS project_id, NULL::text AS project_name, NULL::text AS color, u.email AS email, NOT u.is_active AS inactive, "
            + $"LEAST({SearchSql.TierOf(NameColumn, similar)}, {SearchSql.TierOf(EmailColumn, similar)}) AS tier, "
            + $"{SearchSql.SimilarityOf([NameColumn, EmailColumn], similar)} AS similarity "
            + "FROM organization_memberships m "
            + "JOIN directory_users u ON u.id = m.user_id "
            + $"WHERE {organizations} AND ({SearchSql.Matches(NameColumn, similar)} OR {SearchSql.Matches(EmailColumn, similar)})");
}
