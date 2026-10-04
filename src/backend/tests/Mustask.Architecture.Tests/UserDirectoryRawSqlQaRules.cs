namespace Mustask.Architecture.Tests;

/// <summary>
/// QA re-check 2 (RC2-24), the residual code review round 2 named for F10: <see cref="UserDirectoryBoundaryRules"/>
/// matches the projection's type and set names, so a consumer's raw SQL on its <em>tables</em> — a
/// <c>FromSqlRaw("SELECT … FROM directory_users")</c>, an <c>ExecuteSql</c> — passes it by. api-contract.md A.8 forbids
/// reading the tables just as it forbids the entities, and BR-UP-1 says nothing else writes them. The same places that
/// may name the entities may name the tables.
/// </summary>
public sealed class UserDirectoryRawSqlQaRules
{
    private static readonly string[] Layers = ["Mustask.Domain", "Mustask.Application", "Mustask.Infrastructure", "Mustask.Api"];

    private static readonly string[] Tables = ["directory_users", "directory_snapshots", "directory_snapshot_pages"];

    /// <summary>Where the entities may be named (<c>UserDirectoryBoundaryRules.InternalsAllowedIn</c>), and so the tables.</summary>
    private static readonly string[] AllowedIn =
    [
        "Mustask.Domain/UserDirectory/",
        "Mustask.Application/Abstractions/UserDirectory/",
        "Mustask.Application/Features/UserDirectory/",
        "Mustask.Infrastructure/UserDirectory/",
        "Mustask.Infrastructure/DependencyInjection.cs",
        "Mustask.Infrastructure/MustaskDbContext.cs",
        "Mustask.Infrastructure/Migrations/",
    ];

    [Fact]
    public void Raw_sql_on_the_projections_tables_stays_in_the_sub_slice()
    {
        var violations = Violations(
            Layers.SelectMany(RepositoryLayout.SourceFilesOf).Select(file => (RelativePath(file), File.ReadLines(file.FullName))));

        Assert.True(
            violations.Count == 0,
            "Code outside the user directory's sub-slice names the projection's tables — consumers read IUserDirectory only "
            + "(api-contract.md A.8) and the provider is the only writer (BR-UP-1):"
            + Environment.NewLine + string.Join(Environment.NewLine, violations));
    }

    /// <summary>The rule cannot pass vacuously: every table is named somewhere it is allowed, and a use elsewhere is flagged.</summary>
    [Fact]
    public void Raw_sql_rule_is_not_vacuous()
    {
        var sources = Layers.SelectMany(RepositoryLayout.SourceFilesOf).ToList();

        foreach (var table in Tables)
        {
            Assert.True(
                sources.Any(file => File.ReadLines(file.FullName).Any(line => !IsComment(line) && line.Contains(table, StringComparison.Ordinal))),
                $"No source names the table `{table}` — the rule checks for something that no longer exists.");
        }

        var flagged = Violations(
        [
            ("Mustask.Infrastructure/Organizations/MemberStore.cs", ["context.Users.FromSqlRaw(\"SELECT id FROM directory_users\")"]),
            ("Mustask.Application/Features/Organizations/Sync/SyncHandler.cs", ["\"DELETE FROM directory_snapshots\""]),
            ("Mustask.Api/Features/Organizations/Endpoints.cs", ["const string Sql = \"SELECT 1 FROM directory_snapshot_pages\";"]),
            ("Mustask.Infrastructure/UserDirectory/UserDirectoryReadModel.cs", ["FROM directory_users"]),
            ("Mustask.Infrastructure/Organizations/Comments.cs", ["// reads directory_users"]),
        ]);

        Assert.Equal(
            [
                "Mustask.Infrastructure/Organizations/MemberStore.cs: directory_users",
                "Mustask.Application/Features/Organizations/Sync/SyncHandler.cs: directory_snapshots",
                "Mustask.Api/Features/Organizations/Endpoints.cs: directory_snapshot_pages",
            ],
            flagged);
    }

    private static List<string> Violations(IEnumerable<(string Path, IEnumerable<string> Lines)> files) =>
        files
            .Where(file => !AllowedIn.Any(prefix => file.Path.StartsWith(prefix, StringComparison.Ordinal)))
            .SelectMany(file => file.Lines
                .Where(line => !IsComment(line))
                .SelectMany(line => Tables.Where(table => line.Contains(table, StringComparison.Ordinal)))
                .Distinct(StringComparer.Ordinal)
                .Select(table => $"{file.Path}: {table}"))
            .ToList();

    private static bool IsComment(string line)
    {
        var trimmed = line.TrimStart();

        return trimmed.StartsWith("//", StringComparison.Ordinal) || trimmed.StartsWith('*');
    }

    private static string RelativePath(FileInfo file) =>
        Path.GetRelativePath(RepositoryLayout.Source.FullName, file.FullName).Replace('\\', '/');
}
