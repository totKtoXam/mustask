using System.Text.Json;
using System.Text.RegularExpressions;

namespace Mustask.Contracts.Tests;

/// <summary>
/// mod-016 US-8 · "The realm scripts keep the administrator's secrets out of sight" and "The stand seed follows the inventory"
/// (BR-M1-28, BR-M1-29, SEC-4): <c>scripts/keycloak-seed.sh</c> reads the Keycloak administrator's password from the environment,
/// keeps neither the password nor the access token on any command line (the process list shows every argument to anyone on the
/// machine), and gives the administrator the client roles <c>user</c> and <c>system-admin</c> and the optional <c>member</c> user <c>user</c> only
/// (ADR-0031). A static check of the script's text — no Keycloak, no Docker, no shell: a regression that puts the token back on a
/// <c>curl</c> line is a line of text, and a line of text is what this finds.
/// </summary>
public sealed class KeycloakSeedScriptTests
{
    private static string Script => File.ReadAllText(Path.Combine(RepositoryLayout.Root.FullName, "scripts", "keycloak-seed.sh"));

    /// <summary>The script's commands with their <c>\</c> continuations joined, comments dropped, one entry per logical line.</summary>
    private static List<string> LogicalLines()
    {
        var lines = new List<string>();
        var current = string.Empty;

        foreach (var raw in Script.Split('\n').Select(line => line.TrimEnd('\r')))
        {
            var line = raw.TrimStart();

            if (current.Length == 0 && line.StartsWith('#'))
            {
                continue;
            }

            if (line.EndsWith('\\'))
            {
                current += line[..^1] + " ";
                continue;
            }

            lines.Add((current + line).Trim());
            current = string.Empty;
        }

        return lines.Where(line => line.Length > 0).ToList();
    }

    /// <summary>The arguments of every <c>curl</c> process the script starts: the pipeline segments that begin with <c>curl</c>.</summary>
    private static List<string> CurlCommands() =>
        LogicalLines()
            .SelectMany(line => line.Split('|'))
            .Select(segment => segment.Trim())
            .Where(segment => Regex.IsMatch(segment, @"^(\$\()?curl\b"))
            .ToList();

    [Fact]
    public void TheScript_StartsCurl_AndTheFindingIsNotVacuous()
    {
        // The token request and the admin REST helper: at least these two. A scan that found none would pass any script.
        Assert.True(CurlCommands().Count >= 3, $"Only {CurlCommands().Count} curl commands were found.");
        Assert.Contains(CurlCommands(), command => command.Contains("openid-connect/token", StringComparison.Ordinal));
        Assert.Contains(CurlCommands(), command => command.Contains("/admin/realms/", StringComparison.Ordinal));
    }

    // US-8 · neither the administrator's access token nor the administrator's password is on a curl command line.
    [Fact]
    public void NoCurlCommandLine_CarriesTheAdministratorsTokenOrPassword()
    {
        foreach (var command in CurlCommands())
        {
            Assert.DoesNotContain("Bearer", command, StringComparison.Ordinal);
            Assert.DoesNotContain("Authorization", command, StringComparison.Ordinal);
            Assert.DoesNotContain("$token", command, StringComparison.Ordinal);
            Assert.DoesNotContain("${token}", command, StringComparison.Ordinal);
            Assert.DoesNotContain("KEYCLOAK_ADMIN_PASSWORD", command, StringComparison.Ordinal);
            Assert.DoesNotContain("password=", command, StringComparison.Ordinal);
        }
    }

    // SEC-4 · the token travels in a private temporary file that curl reads as its configuration, written by the `printf` builtin
    // (a builtin has no process of its own, so the token is an argument of none), removed with the other temporary files.
    [Fact]
    public void TheToken_IsKeptInAPrivateFile_ReadWithCurlsConfigOption_AndRemovedAtTheEnd()
    {
        var lines = LogicalLines();

        Assert.Contains(lines, line => line == "umask 077");
        Assert.Contains(lines, line => line.StartsWith("auth_file=$(mktemp)", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.StartsWith("printf 'header = \"Authorization: Bearer %s\"\\n' \"$token\" > \"$auth_file\"", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.StartsWith("trap ", StringComparison.Ordinal) && line.Contains("$auth_file", StringComparison.Ordinal) && line.EndsWith("EXIT", StringComparison.Ordinal));
        Assert.All(
            CurlCommands().Where(command => command.Contains("/admin/realms/", StringComparison.Ordinal)),
            command => Assert.Contains("-K \"$auth_file\"", command, StringComparison.Ordinal));
    }

    // The password is read from the environment, never from an argument or a default, and goes to Keycloak over stdin.
    [Fact]
    public void TheAdministratorsPassword_IsReadFromTheEnvironment_AndSentOverStdin()
    {
        var lines = LogicalLines();

        Assert.Contains(lines, line => line.Contains("[ -n \"${KEYCLOAK_ADMIN_PASSWORD:-}\" ]", StringComparison.Ordinal));
        Assert.DoesNotContain(lines, line => Regex.IsMatch(line, @"^(export\s+)?KEYCLOAK_ADMIN_PASSWORD="));

        var tokenRequest = CurlCommands().Single(command => command.Contains("openid-connect/token", StringComparison.Ordinal));
        Assert.Contains("--data-binary @-", tokenRequest, StringComparison.Ordinal);
    }

    // tech-024 (ADR-0031) · the stand seed gives client roles directly: the administrator `user` and `system-admin`, the optional
    // member user `user` only; no group is named anywhere and the realm is the configured one, not a literal.
    [Fact]
    public void TheSeed_GivesTheAdministratorUserAndSystemAdmin_AndTheMemberUserOnlyUser_NoGroupAnywhere()
    {
        var lines = LogicalLines();

        Assert.Contains(lines, line => line.StartsWith("seed_user \"$KEYCLOAK_REALM\" \"$MUSTASK_USER\"", StringComparison.Ordinal) && line.Contains("\"user,system-admin\"", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.StartsWith("seed_user \"$KEYCLOAK_REALM\" member \"$MEMBER_PASSWORD\"", StringComparison.Ordinal) && line.EndsWith("\"user\" || failed=1", StringComparison.Ordinal));
        Assert.DoesNotContain("Сотрудники", Script, StringComparison.Ordinal);
        Assert.DoesNotContain("Администраторы", Script, StringComparison.Ordinal);
        Assert.DoesNotContain("/groups", Script, StringComparison.Ordinal);
        Assert.DoesNotContain("realms/mustask", Script, StringComparison.Ordinal);
        Assert.Contains("KEYCLOAK_REALM=\"${KEYCLOAK_REALM:-mustask}\"", Script, StringComparison.Ordinal);
    }

    // The roles the seed names exist in the realm import (a typo here is a silent "cannot sign in").
    [Fact]
    public void TheRolesTheSeedUses_AreClientRolesOfTheRealmImport()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(
            Path.Combine(RepositoryLayout.Root.FullName, "deploy", "keycloak", "realms", "mustask-realm.json")));

        var declared = document.RootElement.GetProperty("roles").GetProperty("client").GetProperty("mustask-api").EnumerateArray()
            .Select(role => role.GetProperty("name").GetString()!)
            .ToList();

        Assert.Contains("user", declared);
        Assert.Contains("system-admin", declared);
    }
}
