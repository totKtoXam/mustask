using Mustask.AppHost.Provisioning;

namespace Mustask.Contracts.Tests;

/// <summary>
/// "The manual seed script and the automatic seed do not fight" is a claim
/// about disjoint identities — `scripts/keycloak-seed.sh` seeds the
/// human-facing accounts (`mustask`, `member`), `Mustask.AppHost`'s own
/// provisioning seeds the E2E-only client and fixture user, and the two
/// paths never call into each other. Without a test this is verified only
/// by a human having read both files — a future edit to either side could
/// silently violate it. These two facts
/// are the cheap half of a guard: they cannot prove the scripts never
/// *interact* at runtime, only that neither one's source even *names* the
/// other side's identifiers — which is exactly the shape a careless copy-
/// paste or a "just add one more account here" edit would produce.
/// </summary>
public sealed class KeycloakStandDisjointnessTests
{
    /// <summary>The accounts `scripts/keycloak-seed.sh` seeds today.</summary>
    private static readonly string[] HumanFacingUsernames = ["mustask", "member"];

    [Fact]
    public void KeycloakSeedScript_NeverMentionsTheE2EStandIdentifiers()
    {
        var script = File.ReadAllText(KeycloakSeedScriptPath());

        Assert.DoesNotContain(KeycloakStandIdentifiers.ClientId, script, StringComparison.Ordinal);
        Assert.DoesNotContain(KeycloakStandIdentifiers.FixtureUsername, script, StringComparison.Ordinal);
    }

    /// <summary>
    /// The reverse direction cannot be a blind substring search — "mustask" is
    /// also the first three letters of this very repository's namespaces
    /// (`Mustask.AppHost`, `Mustask.Domain`, ...), so grepping `KeycloakStandProvisioner.cs`
    /// for "mustask" would fail on its own `using`s, not on a real regression.
    /// What actually matters is the *value* AppHost hands Keycloak as the
    /// fixture username, which is exactly <see cref="KeycloakStandIdentifiers.FixtureUsername"/> —
    /// so the guard compares that one value against the human-facing
    /// usernames directly, instead of scanning text.
    /// </summary>
    [Fact]
    public void E2EFixtureUsername_IsNotAnyHumanFacingUsername()
    {
        Assert.DoesNotContain(KeycloakStandIdentifiers.FixtureUsername, HumanFacingUsernames);
    }

    private static string KeycloakSeedScriptPath()
    {
        var path = Path.Combine(RepositoryLayout.Root.FullName, "scripts", "keycloak-seed.sh");

        Assert.True(File.Exists(path), $"Не найден скрипт: {path}");

        return path;
    }
}
