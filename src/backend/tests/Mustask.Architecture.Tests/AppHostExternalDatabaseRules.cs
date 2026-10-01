using System.Text.RegularExpressions;

namespace Mustask.Architecture.Tests;

/// <summary>
/// tech-002: when <c>ConnectionStrings:mustask</c> is configured, the stand
/// uses that PostgreSQL server instead of starting its own container, and in
/// that mode it must not ask for the container's secret
/// (<c>Parameters:mustask-db-password</c>). The failure this guards against is
/// quiet: hoisting the <c>AddParameter</c> call back above the switch (an
/// innocent-looking "declare the parameters together" tidy-up) still builds,
/// still passes every other test, and degrades only the external-database
/// stand — Aspire 13.5.1 reports the unreferenced parameter as unresolved
/// ("Waiting for user input", an "Unresolved parameters" prompt in the
/// dashboard) and nags for a secret nobody has a reason to set. The same
/// reasoning covers an inverted condition or swapped branches: the stand would
/// start a container when a server is configured, or fail to find a connection
/// string when none is. <c>AppHost.cs</c> is top-level statements and cannot
/// be called from a test, so this reads the source, the same idiom as
/// <see cref="KeycloakStandClientSecretPlacementRules"/>.
/// </summary>
public sealed class AppHostExternalDatabaseRules
{
    private const string AppHostFileName = "AppHost.cs";

    // The whole `if` header, exact: no `!`, and `externalDb` is the value read from
    // `ConnectionStrings:mustask` below. The container branch is the `if` body, the
    // connection-string branch is the `else`.
    private const string SwitchStatement = "if (string.IsNullOrWhiteSpace(externalDb))";

    private const string ExternalConnectionStringKey = "builder.Configuration[\"ConnectionStrings:mustask\"]";

    private const string ExternalDbDeclaration = "var externalDb = " + ExternalConnectionStringKey + ";";

    private const string PasswordParameterCall = "AddParameter(\"mustask-db-password\"";

    private const string ContainerCall = "AddPostgres(";

    private const string ExternalCall = "AddConnectionString(\"mustask\")";

    [Fact]
    public void AppHostCs_DeclaresTheContainerAndItsPasswordOnlyBehindTheExternalDatabaseSwitch()
    {
        var code = ReadAppHostCode();

        var configReads = CountOf(code, ExternalConnectionStringKey);

        Assert.True(
            configReads == 1,
            $"Expected exactly one `{ExternalConnectionStringKey}` read in {AppHostFileName}, found {configReads}. "
            + "The external-database switch is decided once, in one place.");

        Assert.True(
            code.Contains(ExternalDbDeclaration, StringComparison.Ordinal),
            $"`{ExternalDbDeclaration}` is gone from {AppHostFileName}: the switch no longer reads "
            + "the external-database connection string into `externalDb`, or was renamed without updating this test.");

        var (containerBranch, externalBranch) = SplitSwitch(code);

        AssertOnlyIn(code, containerBranch, PasswordParameterCall,
            "The container's password parameter must be declared inside the container branch (the `if` body): "
            + "hoisted above the switch, the external-database stand reports a missing value for, and the "
            + "dashboard prompts for, a secret it has no use for.");

        AssertOnlyIn(code, containerBranch, ContainerCall,
            "The PostgreSQL container must be declared in the container branch (the `if` body): above the "
            + "switch, or in the `else`, the stand starts its own container even when ConnectionStrings:mustask "
            + "points at an existing server.");

        AssertOnlyIn(code, externalBranch, ExternalCall,
            "The connection-string resource that Mustask.Api references must be declared in the external-database "
            + "branch (the `else` body): in the `if` body, a configured server is ignored and an unconfigured "
            + "one has no connection string.");
    }

    [Fact]
    public void AppHostCs_ApiReferencesAndWaitsForTheSingleDatabaseResourceOfEitherBranch()
    {
        var code = ReadAppHostCode();

        // `database` is the one variable both branches assign; `api` must go through it,
        // not through a branch-specific resource, or one of the two modes loses the wiring.
        Assert.Contains(".WithReference(database)", code, StringComparison.Ordinal);
        Assert.Contains(".WaitFor(database)", code, StringComparison.Ordinal);
    }

    private static string ReadAppHostCode()
    {
        var appHostFile = RepositoryLayout.SourceFilesOf("Mustask.AppHost")
            .SingleOrDefault(file => file.Name == AppHostFileName)
            ?? throw new InvalidOperationException(
                $"{AppHostFileName} was not found in Mustask.AppHost — this check verifies nothing.");

        // Comment lines are dropped: the file explains the very calls this test looks
        // for, and a mention in a comment must neither satisfy nor break the check.
        var codeLines = File.ReadAllLines(appHostFile.FullName)
            .Where(line => !line.TrimStart().StartsWith("//", StringComparison.Ordinal));

        return string.Join('\n', codeLines);
    }

    private static int CountOf(string text, string fragment)
    {
        var count = 0;
        var index = text.IndexOf(fragment, StringComparison.Ordinal);

        while (index >= 0)
        {
            count++;
            index = text.IndexOf(fragment, index + fragment.Length, StringComparison.Ordinal);
        }

        return count;
    }

    /// <summary>
    /// Cuts the switch into its two blocks: the body of <see cref="SwitchStatement"/> and the
    /// body of the plain <c>else</c> that follows it (not an <c>else if</c>). Braces are matched
    /// naively — there are no braces inside string literals in either block, and one added
    /// there makes the match drift and the assertions below fail, not pass.
    /// </summary>
    private static (string ContainerBranch, string ExternalBranch) SplitSwitch(string code)
    {
        var statementCount = CountOf(code, SwitchStatement);

        Assert.True(
            statementCount == 1,
            $"Expected exactly one `{SwitchStatement}` in {AppHostFileName}, found {statementCount}: the "
            + "external-database switch (tech-002) was removed, inverted (`!`), duplicated or renamed "
            + "without updating this test.");

        var afterCondition = code.IndexOf(SwitchStatement, StringComparison.Ordinal) + SwitchStatement.Length;

        var ifOpen = Regex.Match(code[afterCondition..], @"\A\s*\{");

        Assert.True(ifOpen.Success, $"`{SwitchStatement}` must be followed by a braced block.");

        var (containerBranch, afterIfBlock) = ReadBlock(code, afterCondition + ifOpen.Length - 1);

        var elseOpen = Regex.Match(code[afterIfBlock..], @"\A\s*else\s*\{");

        Assert.True(
            elseOpen.Success,
            $"The block of `{SwitchStatement}` must be followed directly by a plain `else` block: the "
            + "connection-string branch lives there.");

        var (externalBranch, _) = ReadBlock(code, afterIfBlock + elseOpen.Length - 1);

        return (containerBranch, externalBranch);
    }

    private static (string Body, int EndIndex) ReadBlock(string code, int openBraceIndex)
    {
        var depth = 0;

        for (var i = openBraceIndex; i < code.Length; i++)
        {
            if (code[i] == '{')
            {
                depth++;
            }
            else if (code[i] == '}')
            {
                depth--;

                if (depth == 0)
                {
                    return (code[(openBraceIndex + 1)..i], i + 1);
                }
            }
        }

        throw new InvalidOperationException($"Unbalanced braces in {AppHostFileName} — this check verifies nothing.");
    }

    // Exactly one occurrence in the whole file, and it sits in `branch`: so it is in that
    // branch and not in the other one, and not above or below the switch.
    private static void AssertOnlyIn(string code, string branch, string fragment, string reason)
    {
        var total = CountOf(code, fragment);

        Assert.True(
            total == 1,
            $"Expected exactly one `{fragment}` in {AppHostFileName}, found {total}. {reason}");

        Assert.True(
            CountOf(branch, fragment) == 1,
            $"`{fragment}` in {AppHostFileName} is not in the branch of `{SwitchStatement}` where it belongs. {reason}");
    }
}
