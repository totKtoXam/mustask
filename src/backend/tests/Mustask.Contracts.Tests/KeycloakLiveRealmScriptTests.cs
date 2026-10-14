using Mustask.AppHost.Provisioning;

namespace Mustask.Contracts.Tests;

/// <summary>
/// mod-016 US-8 · the wrappers of the live-realm command (<c>scripts/migrate-live-realm.sh</c> and <c>.ps1</c>, plan-backend § 4.7)
/// are what a human runs on the stand: a static check that they start the command the AppHost handles, read the administrator's
/// password from the environment only, never put it on a command line, and say what src/backend/AGENTS.md says — the drift check's
/// findings about the old realm are cleared by this command, never by the reset. No shell, no Docker.
/// </summary>
public sealed class KeycloakLiveRealmScriptTests
{
    private static string Read(string name) => File.ReadAllText(Path.Combine(RepositoryLayout.Root.FullName, "scripts", name));

    [Fact]
    public void TheShellScript_StartsTheCommandTheAppHostHandles_WithNoPasswordArgument()
    {
        var script = Read("migrate-live-realm.sh");
        var lines = script.Split('\n').Select(line => line.TrimEnd('\r').Trim()).Where(line => line.Length > 0 && !line.StartsWith('#')).ToList();

        var exec = Assert.Single(lines, line => line.StartsWith("exec ", StringComparison.Ordinal));

        Assert.Equal($"exec dotnet run --project src/backend/Mustask.AppHost -- {KeycloakLiveRealmCommand.Flag}", exec);
        Assert.DoesNotContain(lines, line => line.Contains("$KEYCLOAK_ADMIN_PASSWORD", StringComparison.Ordinal) && !line.StartsWith("[ -n", StringComparison.Ordinal));
        Assert.Contains(lines, line => line.StartsWith($"[ -n \"${{{KeycloakLiveRealmCommand.PasswordVariable}:-}}\" ]", StringComparison.Ordinal));
    }

    [Fact]
    public void TheShellScript_SaysThatTheDriftChecksFindingsAreClearedByTheCommand_NotByTheReset()
    {
        var script = Read("migrate-live-realm.sh");

        Assert.Contains("scripts/stand-reset.sh", script, StringComparison.Ordinal);
        Assert.Contains("NOT by scripts/stand-reset.sh", script, StringComparison.Ordinal);
        Assert.Contains("every grant before any removal", script, StringComparison.Ordinal);
        Assert.Contains("STOPS before any change", script, StringComparison.Ordinal);
    }

    [Fact]
    public void ThePowerShellScript_RunsTheShellScript()
    {
        var script = Read("migrate-live-realm.ps1");

        Assert.Contains("'scripts/migrate-live-realm.sh'", script, StringComparison.Ordinal);
        Assert.Contains("invoke-sh.ps1", script, StringComparison.Ordinal);
        Assert.DoesNotContain("Password", script.Replace("KEYCLOAK_ADMIN_PASSWORD and KEYCLOAK_URI come from the environment", string.Empty, StringComparison.Ordinal), StringComparison.Ordinal);
    }

    // The flag the wrapper passes is recognized, and is not the reset's: a typo would otherwise start the whole stand.
    [Fact]
    public void TheFlag_IsNotTheResets_AndTheEnvironmentVariablesAreTheDocumentedOnes()
    {
        Assert.NotEqual(StandResetRules.ResetFlag, KeycloakLiveRealmCommand.Flag);
        Assert.Equal("KEYCLOAK_ADMIN_PASSWORD", KeycloakLiveRealmCommand.PasswordVariable);
        Assert.Equal("KEYCLOAK_URI", KeycloakLiveRealmCommand.UriVariable);
        Assert.Equal("http://localhost:5010", KeycloakLiveRealmCommand.DefaultUri);
    }
}
