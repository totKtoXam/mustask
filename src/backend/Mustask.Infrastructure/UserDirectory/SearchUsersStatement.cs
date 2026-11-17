using Mustask.Infrastructure.Search;

namespace Mustask.Infrastructure.UserDirectory;

/// <summary>
/// The users group of the quick search (feat-071): the one place outside the projection's own reads that names its table, kept in the
/// user-directory sub-slice so the boundary rule (api-contract A.8) holds. The projection's <c>search_name</c> and <c>search_email</c> are
/// folded on write (<c>ProjectedUser</c>), so the columns are used as they are. Only the organization's current members are joined; a
/// disabled or gone account is found and flagged (BR-GS-3).
/// </summary>
internal static class SearchUsersStatement
{
    internal static string Sql(string organizations, bool similar) =>
        SearchReadModel.Wrap(
            "SELECT u.id::text AS id, u.display_name AS name, NULL::text AS key, NULL::uuid AS department_id, NULL::text AS department_name, "
            + "NULL::uuid AS project_id, NULL::text AS project_name, NULL::text AS color, u.email AS email, NOT u.is_active AS inactive, "
            + $"LEAST({SearchReadModel.TierOf("u.search_name", similar)}, {SearchReadModel.TierOf("u.search_email", similar)}) AS tier, "
            + $"{SearchReadModel.SimilarityOf(["u.search_name", "u.search_email"], similar)} AS similarity "
            + "FROM organization_memberships m "
            + "JOIN directory_users u ON u.id = m.user_id "
            + $"WHERE {organizations} AND ({SearchReadModel.Matches("u.search_name", similar)} OR {SearchReadModel.Matches("u.search_email", similar)})");
}
