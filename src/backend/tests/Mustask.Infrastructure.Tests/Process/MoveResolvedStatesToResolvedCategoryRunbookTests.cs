using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Mustask.Infrastructure.Migrations;

namespace Mustask.Infrastructure.Tests.Process;

/// <summary>
/// bug-004 D-3, review round 1 (R1-1, R1-3) · the migration's report does not reach the terminal of whoever applies it
/// (<c>dotnet ef database update</c> prints no <c>NOTICE</c> or <c>WARNING</c>), so the check it makes is a step the
/// runbook hands over, and a person may have to run its move again after an older build wrote the old shape. This pins
/// the runbook's two statements, and the migration's own remarks, to the SQL the migration runs: the text a person
/// copies is the text the migration reports with. No Docker needed; the statements run against a database in
/// <see cref="MoveResolvedStatesToResolvedCategoryMigrationTests"/>.
/// </summary>
public sealed class MoveResolvedStatesToResolvedCategoryRunbookTests
{
    private static string UpSql() =>
        MigrationRunbook.Normalize(string.Join(
            " ", new MoveResolvedStatesToResolvedCategory().UpOperations.OfType<SqlOperation>().Select(operation => operation.Sql)));

    [Fact]
    public void Runbook_CheckAndMove_AreTheSqlTheMigrationRuns()
    {
        var up = UpSql();

        Assert.Contains(MigrationRunbook.Normalize(MigrationRunbook.CheckSql), up, StringComparison.Ordinal);
        Assert.Contains(MigrationRunbook.Normalize(MigrationRunbook.MoveSql), up, StringComparison.Ordinal);
    }

    [Fact]
    public void MigrationRemarks_QuoteTheCheck_Verbatim()
    {
        var source = File.ReadAllText(Path.Combine(
            MigrationRunbook.RepositoryRoot(),
            "src", "backend", "Mustask.Infrastructure", "Migrations", "20261011214251_MoveResolvedStatesToResolvedCategory.cs"));
        var remarks = MigrationRunbook.Normalize(Regex.Replace(source, @"^\s*///\s?", string.Empty, RegexOptions.Multiline));

        Assert.Contains(MigrationRunbook.Normalize(MigrationRunbook.CheckSql), remarks, StringComparison.Ordinal);
    }
}
