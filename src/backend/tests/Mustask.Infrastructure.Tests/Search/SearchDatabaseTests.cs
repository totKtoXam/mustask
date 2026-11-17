using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Mustask.Application.Abstractions.Search;
using Mustask.Domain.Search;
using Mustask.Infrastructure.Search;
using Mustask.Infrastructure.UserDirectory;
using Mustask.Infrastructure.Tests.Organizations;
using Xunit.Abstractions;

namespace Mustask.Infrastructure.Tests.Search;

/// <summary>
/// feat-071 plan § 6 against a real PostgreSQL: the statement count, the trigram index pins, the similarity table and the volume
/// budget (feat-065 NFR: p95 300 ms, p99 800 ms; asserted here at twice that, the measured values go to qa.md). The database is this
/// class's own, so its large organization does not slow another class.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
public sealed class SearchDatabaseTests(OrganizationsDatabaseFixture database, ITestOutputHelper output) : IClassFixture<OrganizationsDatabaseFixture>
{
    private static readonly SearchGroupKind[] All = Enum.GetValues<SearchGroupKind>();

    private static SearchQueryText Q(string text) =>
        SearchQueryText.Judge(text, out var query) == SearchQueryKind.Valid ? query! : throw new ArgumentException(text);

    private static SearchScope Admin(Guid org) => new(org, true, SearchVisibility.Unrestricted);

    private async Task<(int Statements, IReadOnlyList<SearchGroupRows> Groups)> ReadCountedAsync(
        SearchScope scope, string text, SearchGroupKind[]? kinds = null, bool candidates = false)
    {
        var log = new SqlLogInterceptor();
        await using var context = database.NewContext(log);
        var model = new SearchReadModel(context);

        if (candidates)
        {
            await model.ReadCandidatesAsync(scope.OrganizationId, kinds ?? All, CancellationToken.None);
        }

        var groups = await model.ReadAsync([scope], Q(text), kinds ?? All, 6, CancellationToken.None);

        return (log.Commands.Count, groups);
    }

    // NFR "Volumes" (BR-GS-4): the statements a read sends do not grow with the objects: 10 or 1000, administrator or restricted.
    // A first read is candidates (1) + set_config (1) + five groups; an expansion is candidates + set_config + one group.
    [Fact]
    public async Task TheStatementCount_IsTheSame_For10Objects_As1000()
    {
        var small = await SearchDataSet.SeedAsync(database, departments: 2, projects: 5, subAreas: 10, teams: 10, members: 10);
        var large = await SearchDataSet.SeedAsync(database, departments: 20, projects: 200, subAreas: 1000, teams: 200, members: 300);

        static SearchScope Restricted(SearchDataSet set) =>
            new(set.OrganizationId, true, new SearchVisibility(set.Projects, set.Teams, set.Departments, set.Areas));

        var smallAdmin = await ReadCountedAsync(Admin(small.OrganizationId), "проект");
        var largeAdmin = await ReadCountedAsync(Admin(large.OrganizationId), "проект");
        var smallSight = await ReadCountedAsync(Restricted(small), "проект", candidates: true);
        var largeSight = await ReadCountedAsync(Restricted(large), "проект", candidates: true);
        var expansion = await ReadCountedAsync(Restricted(large), "проект", [SearchGroupKind.Projects], candidates: true);

        Assert.Equal(1 + 5, smallAdmin.Statements);
        Assert.Equal(smallAdmin.Statements, largeAdmin.Statements);
        Assert.Equal(1 + 1 + 5, smallSight.Statements);
        Assert.Equal(smallSight.Statements, largeSight.Statements);
        Assert.Equal(1 + 1 + 1, expansion.Statements);
    }

    // ADR-0044 § 2 "pinning tests": the six expression indexes and search_fold exist, search_fold is immutable.
    [Fact]
    public async Task TheSixTrigramIndexes_AndTheFoldFunction_Exist()
    {
        var indexes = await database.QueryAsync(
            "SELECT indexname FROM pg_indexes WHERE indexname LIKE '%\\_trgm' AND indexname LIKE 'ix\\_%search%' ORDER BY indexname");

        Assert.Equal(
            [
                "ix_areas_search_name_trgm", "ix_directory_users_search_email_trgm", "ix_directory_users_search_name_trgm",
                "ix_projects_search_key_trgm", "ix_projects_search_name_trgm", "ix_teams_search_name_trgm",
            ],
            indexes);
        Assert.Equal(["i"], await database.QueryAsync("SELECT provolatile::text FROM pg_proc WHERE proname = 'search_fold'"));
        Assert.Equal(["1"], await database.QueryAsync("SELECT count(*)::text FROM pg_extension WHERE extname = 'pg_trgm'"));
        Assert.All(
            await database.QueryAsync("SELECT indexdef FROM pg_indexes WHERE indexname LIKE 'ix\\_%search%\\_trgm'"),
            definition => Assert.Contains("gin_trgm_ops", definition, StringComparison.Ordinal));
    }

    // The read's predicate is the index's expression: the predicate text is built from the read model's own SQL helpers (the folded
    // column from SearchSql.Folded, the predicate from SearchSql.Matches, the users' columns from SearchUsersStatement), with a
    // sequential scan forbidden, so a drift in either text fails here. The planner "can use" the index at this size; that the read
    // picks it at volume is what the volume test's timings show.
    [Theory]
    [InlineData("projects p", "p.name", true, "ix_projects_search_name_trgm")]
    [InlineData("projects p", "p.\"key\"", true, "ix_projects_search_key_trgm")]
    [InlineData("teams t", "t.name", true, "ix_teams_search_name_trgm")]
    [InlineData("areas a", "a.name", true, "ix_areas_search_name_trgm")]
    [InlineData("directory_users u", SearchUsersStatement.NameColumn, false, "ix_directory_users_search_name_trgm")]
    [InlineData("directory_users u", SearchUsersStatement.EmailColumn, false, "ix_directory_users_search_email_trgm")]
#pragma warning disable EF1003 // test-built EXPLAIN over the read model's own predicate text and fixed literals
    public async Task TheReadsPredicate_CanUseItsTrigramIndex(string from, string column, bool folded, string index)
    {
        await SearchDataSet.SeedAsync(database, departments: 3, projects: 30, subAreas: 30, teams: 30, members: 30);

        var expression = folded ? SearchSql.Folded(column) : column;

        foreach (var similar in new[] { false, true })
        {
            var predicate = SearchSql.Matches(expression, similar).Replace("@contains", "'%абв%'", StringComparison.Ordinal)
                .Replace("@q", "'абвг'", StringComparison.Ordinal);

            await using var context = database.NewContext();
            await using var transaction = await context.Database.BeginTransactionAsync();
            await context.Database.ExecuteSqlRawAsync("SET LOCAL enable_seqscan = off");

            // The key is short and the btree on (organization, key) competes on cost; it is dropped inside this rolled-back transaction.
            await context.Database.ExecuteSqlRawAsync("DROP INDEX IF EXISTS ix_projects_organization_id_key");
            var plan = string.Join("\n", await context.Database.SqlQueryRaw<string>("EXPLAIN SELECT 1 FROM " + from + " WHERE " + predicate).ToListAsync());

            Assert.True(plan.Contains(index, StringComparison.Ordinal), plan);
        }
    }
#pragma warning restore EF1003

    // Review F2 (ADR-0044): the fold and the trigram match depend on the database's ctype. search_fold folds under the explicit ICU
    // collation; pg_trgm's word classification still follows datctype, so the migration refuses C/POSIX and this test pins the
    // Cyrillic behaviour a C-ctype image would break.
    [Fact]
    public async Task TheFoldAndTheTrigramMatch_KnowCyrillic_OnThisDatabase()
    {
        var locale = await database.QueryAsync("SELECT datctype || ' / ' || datcollate FROM pg_database WHERE datname = current_database()");
        output.WriteLine($"LOCALE datctype / datcollate: {locale.Single()}");

        Assert.Equal(["елка альфа"], await database.QueryAsync("SELECT search_fold('ЁЛКА Альфа')"));
        Assert.Equal(["true"], await database.QueryAsync("SELECT (word_similarity('логстика', 'логистика') >= 0.5)::text"));
        Assert.DoesNotMatch("^(C|POSIX) /", locale.Single());
    }

    // Review F8: one fold for the stored name/e-mail and the typed text.
    [Fact]
    public void TheTypedTextFold_IsTheProjectionsFold()
    {
        foreach (var text in new[] { "ЁЛКА Альфа", "Ёӝ и ё", "a\u0001b￾c", "Иван Петров" })
        {
            Assert.Equal(Mustask.Domain.UserDirectory.DirectorySearchText.Normalize(text), SearchQueryText.Fold(text));
        }
    }

    // US-2 "Typo tolerance" (BR-GS-17): the near-miss table at the threshold 0.5 and the minimum query length 4.
    [Theory]
    [InlineData("Логистика", "логстика", true)]
    [InlineData("Логистика", "логистка", true)]
    [InlineData("Логистика", "лгс", false)]
    [InlineData("Логистика", "лгст", false)]
    [InlineData("Логистика", "бухгалтерия", false)]
    public async Task ASimilarName_IsFound_OnlyAboveTheThreshold(string name, string typed, bool found)
    {
        var set = await SearchDataSet.SeedAsync(database, departments: 1, projects: 1, subAreas: 1, teams: 1, members: 1);
        await database.ExecuteAsync(
            "UPDATE teams SET name = @name, normalized_name = upper(@name) WHERE id = @id",
            ("name", name), ("id", set.Teams[0]));

        var answer = await ReadCountedAsync(Admin(set.OrganizationId), typed, [SearchGroupKind.Teams]);

        Assert.Equal(found, answer.Groups.Single().Rows.Any(row => row.Name == name));
    }

    // NFR "Volumes": the doc-012 dataset (1000 areas, 200 projects, 200 teams, 2000 members); 300 mixed reads, the administrator and a
    // caller who sees a tenth. Asserted at twice the spec (p95 300 -> 600 ms, p99 800 -> 1600 ms); the values are printed for qa.md.
    [Fact]
    public async Task VolumeReads_StayWithinTwiceTheSpecBudget()
    {
        var set = await SearchDataSet.SeedAsync(database, departments: 20, projects: 200, subAreas: 780, teams: 200, members: 2000);
        var tenth = new SearchScope(
            set.OrganizationId,
            true,
            new SearchVisibility(set.Projects.Take(20).ToList(), set.Teams.Take(20).ToList(), set.Departments.Take(2).ToList(), set.Areas.Take(78).ToList()));
        // The plan measures the 2-character query (the trigram index cannot serve it), a 3-character one, a no-match and the typo tier too.
        string[] texts = ["проект", "прое", "команда 1", "область 7", "имя1", "user12@", "фамилия 99", "отдел", "проэкт", "кооманда", "пр", "ко", "про", "юзз", "щщщщ"];

        Assert.Equal(SearchQueryKind.TooShort, SearchQueryText.Judge("п", out _));
        var timings = new List<double>();

        for (var round = 0; round < 300; round++)
        {
            var scope = round % 2 == 0 ? Admin(set.OrganizationId) : tenth;
            var text = texts[round % texts.Length];
            var stopwatch = Stopwatch.StartNew();
            await using var context = database.NewContext();
            var model = new SearchReadModel(context);

            if (round % 2 == 1)
            {
                await model.ReadCandidatesAsync(set.OrganizationId, All, CancellationToken.None);
            }

            await model.ReadAsync([scope], Q(text), All, 6, CancellationToken.None);
            stopwatch.Stop();
            timings.Add(stopwatch.Elapsed.TotalMilliseconds);
        }

        var ordered = timings.Order().ToList();
        var p95 = ordered[(int)(ordered.Count * 0.95) - 1];
        var p99 = ordered[(int)(ordered.Count * 0.99) - 1];
        output.WriteLine($"VOLUME search reads (database read only: no handler, sight-set building or HTTP): n={ordered.Count} p50={ordered[ordered.Count / 2]:F1} ms p95={p95:F1} ms p99={p99:F1} ms max={ordered[^1]:F1} ms");

        Assert.True(p95 <= 600, $"p95 {p95:F1} ms");
        Assert.True(p99 <= 1600, $"p99 {p99:F1} ms");
    }
}
