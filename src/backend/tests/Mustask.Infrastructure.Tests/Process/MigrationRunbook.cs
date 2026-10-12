using System.Text.RegularExpressions;

namespace Mustask.Infrastructure.Tests.Process;

/// <summary>
/// The statements <c>docs/runbooks/local-migrations.md</c> hands to a person after a migration that reports
/// (section "Migrations that report"), read from the file itself. A test that runs them runs what the runbook says,
/// not a copy of it; <see cref="MoveResolvedStatesToResolvedCategoryRunbookTests"/> pins them to the migration's own SQL.
/// </summary>
internal static class MigrationRunbook
{
    private const string Heading = "## Migrations that report";

    /// <summary>The query that names the processes in which a state name still stands in more than one category.</summary>
    internal static string CheckSql => Statements().Single(statement => statement.StartsWith("SELECT", StringComparison.Ordinal));

    /// <summary>The idempotent move a person runs again after an older build wrote the old shape to a migrated database.</summary>
    internal static string MoveSql => Statements().Single(statement => statement.StartsWith("UPDATE", StringComparison.Ordinal));

    /// <summary>Every <c>sql</c> fence of the section, trimmed. Exactly two exist; a section that lost one fails here.</summary>
    internal static IReadOnlyList<string> Statements()
    {
        var text = File.ReadAllText(Path.Combine(RepositoryRoot(), "docs", "runbooks", "local-migrations.md"));
        var start = text.IndexOf(Heading, StringComparison.Ordinal);

        Assert.True(start >= 0, $"The runbook has no section \"{Heading}\".");

        var end = text.IndexOf("\n## ", start + Heading.Length, StringComparison.Ordinal);
        var section = end < 0 ? text[start..] : text[start..end];

        var statements = Regex.Matches(section, @"```sql\r?\n(?<sql>.*?)```", RegexOptions.Singleline)
            .Select(match => match.Groups["sql"].Value.Trim())
            .ToList();

        Assert.Equal(2, statements.Count);

        return statements;
    }

    /// <summary>One line of single spaces, no trailing semicolon: two renderings of a statement compare equal.</summary>
    internal static string Normalize(string sql) => Regex.Replace(sql, @"\s+", " ").Trim().TrimEnd(';').TrimEnd();

    internal static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Mustask.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException($"Mustask.sln was not found above {AppContext.BaseDirectory}.");
    }
}
