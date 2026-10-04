using System.Text.RegularExpressions;

namespace Mustask.Architecture.Tests;

/// <summary>
/// mod-014 US-6 "A fresh clone needs no new secret" and "The credential comes
/// only from environment or user secrets" (ADR-0025 § 3): <c>AppHost.cs</c>
/// generates the push secret once per process — the lesson of
/// <see cref="KeycloakStandClientSecretPlacementRules"/> — and hands it over as
/// a parameter that carries its value, so the human-set secrets of a fresh
/// clone stay exactly the three it had before this task.
/// </summary>
public sealed partial class UserProjectionPushSecretPlacementRules
{
    private const string AppHostFileName = "AppHost.cs";

    /// <summary>The parameters a human sets on a fresh clone (src/backend/AGENTS.md) — no fourth.</summary>
    private static readonly string[] HumanSetSecrets = ["mustask-db-password", "keycloak-admin-password", "keycloak-panel-client-secret"];

    private static string AppHostCode()
    {
        var file = RepositoryLayout.SourceFilesOf("Mustask.AppHost").SingleOrDefault(candidate => candidate.Name == AppHostFileName)
            ?? throw new InvalidOperationException($"{AppHostFileName} was not found — this check verifies nothing.");

        return string.Join('\n', File.ReadAllLines(file.FullName).Where(line => !line.TrimStart().StartsWith("//", StringComparison.Ordinal)));
    }

    [Fact]
    public void AppHostCs_GeneratesThePushSecretExactlyOnce_BeforeAnySubscription()
    {
        var code = AppHostCode();
        var constructions = Regex.Matches(code, @"new\s+UserProjectionPushSecret\s*\(");

        Assert.True(constructions.Count == 1, $"Expected one `new UserProjectionPushSecret(` in {AppHostFileName}, found {constructions.Count}.");

        var firstSubscription = code.IndexOf("builder.Eventing.Subscribe", StringComparison.Ordinal);

        Assert.True(firstSubscription < 0 || constructions[0].Index < firstSubscription,
            "The push secret is generated inside or after an event subscription — a repeat firing would mint a second value.");
    }

    [Fact]
    public void AppHostCs_PassesThePushSecretParameterItsGeneratedValue()
    {
        Assert.Matches(
            @"AddParameter\(\s*UserProjectionIdentifiers\.PushSecretParameter\s*,\s*userProjectionPushSecret\.Value\s*,\s*secret:\s*true\s*\)",
            AppHostCode());
    }

    [Fact]
    public void AppHostCs_HumanSetSecrets_StayTheThreeOfAFreshClone()
    {
        var valueless = ValuelessSecretParameter().Matches(AppHostCode()).Select(match => match.Groups["name"].Value).ToList();

        Assert.Equal(HumanSetSecrets.Order(StringComparer.Ordinal), valueless.Order(StringComparer.Ordinal));
    }

    [GeneratedRegex(@"AddParameter\(\s*""(?<name>[^""]+)""\s*,\s*secret:\s*true\s*\)")]
    private static partial Regex ValuelessSecretParameter();
}
