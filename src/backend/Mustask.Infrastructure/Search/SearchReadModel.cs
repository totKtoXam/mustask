using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Mustask.Application.Abstractions.Search;
using Mustask.Domain.Search;
using Npgsql;
using NpgsqlTypes;

namespace Mustask.Infrastructure.Search;

/// <summary>
/// <see cref="ISearchReadModel"/> (feat-071 plan § 2, ADR-0044 § 1). Raw, parameterised SQL, because LINQ cannot express the tiers, the
/// <c>LIKE ... ESCAPE</c> over the fold or the trigram similarity operator. The typed text is only ever a parameter. Statements:
/// one for the candidate ids (none for the administrator), one per group, and, only when the typo tier applies, one that sets
/// <c>pg_trgm.word_similarity_threshold</c> for the transaction the group statements share, so the <c>%&gt;</c> operator the GIN
/// index serves uses <see cref="SimilarityRule.Threshold"/>.
/// <para>
/// The folded column side is the <see cref="SearchSql.Folded"/> expression, the very text of the migration's index expressions
/// (<c>search_fold(name)</c>); <c>SearchDatabaseTests</c> pins that the two are the same, because a predicate that differs by one
/// character silently stops using the index. The users' columns are folded on write and used as they are.
/// </para>
/// </summary>
internal sealed class SearchReadModel(MustaskDbContext context) : ISearchReadModel
{
    public async Task<SearchCandidates> ReadCandidatesAsync(
        Guid organizationId, IReadOnlyCollection<SearchGroupKind> kinds, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(kinds);

        var parts = new List<string>();

        if (kinds.Contains(SearchGroupKind.Projects))
        {
            parts.Add("SELECT 0 AS \"Kind\", p.id AS \"Id\" FROM projects p WHERE p.organization_id = @org AND p.status = 'Active'");
        }

        if (kinds.Contains(SearchGroupKind.Teams))
        {
            parts.Add("SELECT 1, t.id FROM teams t WHERE t.organization_id = @org AND t.status = 'Active'");
        }

        if (kinds.Contains(SearchGroupKind.Departments))
        {
            parts.Add("SELECT 2, a.id FROM areas a WHERE a.organization_id = @org AND a.type = 1");
        }

        if (kinds.Contains(SearchGroupKind.Areas))
        {
            parts.Add(
                "SELECT 3, a.id FROM areas a JOIN projects p ON p.id = a.project_id AND p.status = 'Active' "
                + "WHERE a.organization_id = @org AND a.type = 3");
        }

        if (parts.Count == 0)
        {
            return new SearchCandidates([], [], [], []);
        }

        var rows = await context.Database
            .SqlQueryRaw<CandidateRow>(string.Join(" UNION ALL ", parts), new NpgsqlParameter("org", organizationId))
            .ToListAsync(cancellationToken);

        List<Guid> Of(int kind) => rows.Where(row => row.Kind == kind).Select(row => row.Id).ToList();

        return new SearchCandidates(Of(0), Of(1), Of(2), Of(3));
    }

    public async Task<IReadOnlyList<SearchGroupRows>> ReadAsync(
        IReadOnlyList<SearchScope> scopes,
        SearchQueryText query,
        IReadOnlyCollection<SearchGroupKind> kinds,
        int take,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scopes);
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(kinds);

        var ordered = Enum.GetValues<SearchGroupKind>().Where(kinds.Contains).ToList();

        if (ordered.Count == 0 || scopes.Count == 0)
        {
            return [];
        }

        var similar = query.AllowsSimilar;
        var ownsTransaction = similar && context.Database.CurrentTransaction is null;
        var transaction = ownsTransaction ? await context.Database.BeginTransactionAsync(cancellationToken) : null;

        try
        {
            if (similar)
            {
                await context.Database.ExecuteSqlRawAsync(
                    "SELECT set_config('pg_trgm.word_similarity_threshold', @threshold, true)",
                    [new NpgsqlParameter("threshold", SimilarityRule.Threshold.ToString("0.0#", CultureInfo.InvariantCulture))],
                    cancellationToken);
            }

            var groups = new List<SearchGroupRows>();

            foreach (var kind in ordered)
            {
                var statement = StatementFor(kind, scopes, query, take);

                if (statement is null)
                {
                    groups.Add(new SearchGroupRows(kind, []));

                    continue;
                }

                var rows = await context.Database
                    .SqlQueryRaw<RawRow>(statement.Sql, statement.Parameters)
                    .ToListAsync(cancellationToken);

                groups.Add(new SearchGroupRows(kind, rows.Select(row => row.ToRow(kind)).ToList()));
            }

            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }

            return groups;
        }
        finally
        {
            if (transaction is not null)
            {
                await transaction.DisposeAsync();
            }
        }
    }

    private sealed record Statement(string Sql, object[] Parameters);

    /// <summary>The statement of one group, or <c>null</c> when no organization of the scope can show the kind (no query is sent).</summary>
    private static Statement? StatementFor(SearchGroupKind kind, IReadOnlyList<SearchScope> scopes, SearchQueryText query, int take)
    {
        var parameters = new List<object>
        {
            new NpgsqlParameter("q", query.Folded),
            new NpgsqlParameter("prefix", query.PrefixPattern),
            new NpgsqlParameter("word", query.WordPrefixPattern),
            new NpgsqlParameter("contains", query.ContainsPattern),
            new NpgsqlParameter("take", take),
        };

        string sight;
        string organizations;

        if (kind == SearchGroupKind.Users)
        {
            var allowed = scopes.Where(scope => scope.UsersAllowed).Select(scope => scope.OrganizationId).ToArray();

            if (allowed.Length == 0)
            {
                return null;
            }

            parameters.Add(new NpgsqlParameter("orgs", NpgsqlDbType.Array | NpgsqlDbType.Uuid) { Value = allowed });
            organizations = "m.organization_id = ANY(@orgs)";
            sight = string.Empty;
        }
        else
        {
            var visibilities = scopes.Select(scope => (scope.OrganizationId, Ids: IdsOf(scope.Visibility, kind))).ToList();
            var full = visibilities.Where(pair => pair.Ids is null).Select(pair => pair.OrganizationId).ToArray();
            var visible = visibilities.Where(pair => pair.Ids is not null).SelectMany(pair => pair.Ids!).ToArray();

            if (full.Length == 0 && visible.Length == 0)
            {
                return null;
            }

            var alias = kind switch
            {
                SearchGroupKind.Projects => "p",
                SearchGroupKind.Teams => "t",
                _ => "a",
            };

            parameters.Add(new NpgsqlParameter("orgs", NpgsqlDbType.Array | NpgsqlDbType.Uuid) { Value = scopes.Select(scope => scope.OrganizationId).ToArray() });
            organizations = $"{alias}.organization_id = ANY(@orgs)";

            if (full.Length > 0 && visible.Length == 0)
            {
                parameters.Add(new NpgsqlParameter("full", NpgsqlDbType.Array | NpgsqlDbType.Uuid) { Value = full });
                sight = $" AND {alias}.organization_id = ANY(@full)";
            }
            else if (full.Length == 0)
            {
                parameters.Add(new NpgsqlParameter("visible", NpgsqlDbType.Array | NpgsqlDbType.Uuid) { Value = visible });
                sight = $" AND {alias}.id = ANY(@visible)";
            }
            else
            {
                parameters.Add(new NpgsqlParameter("full", NpgsqlDbType.Array | NpgsqlDbType.Uuid) { Value = full });
                parameters.Add(new NpgsqlParameter("visible", NpgsqlDbType.Array | NpgsqlDbType.Uuid) { Value = visible });
                sight = $" AND ({alias}.organization_id = ANY(@full) OR {alias}.id = ANY(@visible))";
            }
        }

        var similar = query.AllowsSimilar;
        var sql = kind switch
        {
            SearchGroupKind.Projects => ProjectsSql(organizations, sight, similar),
            SearchGroupKind.Teams => TeamsSql(organizations, sight, similar),
            SearchGroupKind.Departments => DepartmentsSql(organizations, sight, similar),
            SearchGroupKind.Areas => AreasSql(organizations, sight, similar),
            _ => UserDirectory.SearchUsersStatement.Sql(organizations, similar),
        };

        return new Statement(sql, [.. parameters]);
    }

    private static IReadOnlyList<Guid>? IdsOf(SearchVisibility visibility, SearchGroupKind kind) => kind switch
    {
        SearchGroupKind.Projects => visibility.Projects,
        SearchGroupKind.Teams => visibility.Teams,
        SearchGroupKind.Departments => visibility.Departments,
        _ => visibility.Areas,
    };

    private static string ProjectsSql(string organizations, string sight, bool similar)
    {
        var name = SearchSql.Folded("p.name");
        var key = SearchSql.Folded("p.\"key\"");

        return SearchSql.Wrap(
            "SELECT p.id, p.name, p.\"key\" AS key, dep.id AS department_id, dep.name AS department_name, NULL::uuid AS project_id, NULL::text AS project_name, "
            + "p.color, NULL::text AS email, NULL::boolean AS inactive, "
            + $"LEAST({SearchSql.TierOf(name, similar)}, {SearchSql.TierOf(key, similar)}) AS tier, {SearchSql.SimilarityOf([name, key], similar)} AS similarity "
            + "FROM projects p "
            + "LEFT JOIN areas node ON node.project_id = p.id AND node.type = 2 "
            + "LEFT JOIN areas dep ON dep.id = node.parent_id "
            + $"WHERE {organizations} AND p.status = 'Active'{sight} AND ({SearchSql.Matches(name, similar)} OR {SearchSql.Matches(key, similar)})");
    }

    private static string TeamsSql(string organizations, string sight, bool similar)
    {
        var name = SearchSql.Folded("t.name");

        return SearchSql.Wrap(
            "SELECT t.id, t.name, NULL::text AS key, dep.id AS department_id, dep.name AS department_name, NULL::uuid AS project_id, NULL::text AS project_name, "
            + "t.color, NULL::text AS email, NULL::boolean AS inactive, "
            + $"{SearchSql.TierOf(name, similar)} AS tier, {SearchSql.SimilarityOf([name], similar)} AS similarity "
            + "FROM teams t "
            + "LEFT JOIN areas dep ON dep.id = t.department_id "
            + $"WHERE {organizations} AND t.status = 'Active'{sight} AND {SearchSql.Matches(name, similar)}");
    }

    private static string DepartmentsSql(string organizations, string sight, bool similar)
    {
        var name = SearchSql.Folded("a.name");

        return SearchSql.Wrap(
            "SELECT a.id, a.name, NULL::text AS key, NULL::uuid AS department_id, NULL::text AS department_name, NULL::uuid AS project_id, NULL::text AS project_name, "
            + "a.color, NULL::text AS email, NULL::boolean AS inactive, "
            + $"{SearchSql.TierOf(name, similar)} AS tier, {SearchSql.SimilarityOf([name], similar)} AS similarity "
            + "FROM areas a "
            + $"WHERE {organizations} AND a.type = 1{sight} AND {SearchSql.Matches(name, similar)}");
    }

    private static string AreasSql(string organizations, string sight, bool similar)
    {
        var name = SearchSql.Folded("a.name");

        return SearchSql.Wrap(
            "SELECT a.id, a.name, NULL::text AS key, NULL::uuid AS department_id, dep.name AS department_name, p.id AS project_id, p.name AS project_name, "
            + "NULL::text AS color, NULL::text AS email, NULL::boolean AS inactive, "
            + $"{SearchSql.TierOf(name, similar)} AS tier, {SearchSql.SimilarityOf([name], similar)} AS similarity "
            + "FROM areas a "
            + "JOIN projects p ON p.id = a.project_id AND p.status = 'Active' "
            + "LEFT JOIN areas node ON node.project_id = p.id AND node.type = 2 "
            + "LEFT JOIN areas dep ON dep.id = node.parent_id "
            + $"WHERE {organizations} AND a.type = 3{sight} AND {SearchSql.Matches(name, similar)}");
    }

    private sealed class CandidateRow
    {
        public int Kind { get; set; }

        public Guid Id { get; set; }
    }

    private sealed class RawRow
    {
        public string Id { get; set; } = null!;

        public string Name { get; set; } = null!;

        public string? Key { get; set; }

        public Guid? DepartmentId { get; set; }

        public string? DepartmentName { get; set; }

        public Guid? ProjectId { get; set; }

        public string? ProjectName { get; set; }

        public string? Color { get; set; }

        public string? Email { get; set; }

        public bool? Inactive { get; set; }

        public int Tier { get; set; }

        public SearchRow ToRow(SearchGroupKind kind) =>
            new(kind, Id, Name, Key, DepartmentId, DepartmentName, ProjectId, ProjectName, Color, Email, Inactive, Tier);
    }
}
