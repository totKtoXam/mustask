using System.Text.RegularExpressions;

namespace Mustask.Architecture.Tests;

/// <summary>
/// mod-014 on the stand (ADR-0025 § 4, § 7): pieces of <c>AppHost.cs</c>
/// wiring whose failure is quiet. <c>AppHost.cs</c> is top-level statements and
/// cannot be called from a test, so this reads the source — the idiom of
/// <see cref="AppHostExternalDatabaseRules"/>; what the wired code itself does
/// is tested beside it (<c>UserProjectionStandTests</c>,
/// <c>KeycloakDriftHealthCheckTests</c> in Mustask.Contracts.Tests).
/// </summary>
public sealed class UserProjectionStandWiringRules
{
    private const string AppHostFileName = "AppHost.cs";

    private static string AppHostCode()
    {
        var file = RepositoryLayout.SourceFilesOf("Mustask.AppHost").SingleOrDefault(candidate => candidate.Name == AppHostFileName)
            ?? throw new InvalidOperationException($"{AppHostFileName} was not found — this check verifies nothing.");

        // Comment lines are dropped: the file explains the very calls this test looks for.
        return string.Join('\n', File.ReadAllLines(file.FullName).Where(line => !line.TrimStart().StartsWith("//", StringComparison.Ordinal)));
    }

    // QA D-1 (US-5 "The projection catches up after MUSTASK comes back"): Aspire's container tunnel stops
    // forwarding to the api once the keycloak resource has restarted and the api restarts after it, so the
    // provider is pointed at the api's own port on the Docker host, a name mapped on every engine.
    [Fact]
    public void AppHostCs_PointsTheProviderAtTheApisPortOnTheDockerHost_NotThroughAspiresTunnel()
    {
        var code = AppHostCode();

        Assert.Matches(
            @"\.WithEnvironment\(\s*UserProjectionIdentifiers\.EndpointVariable\s*,\s*UserProjectionIdentifiers\.FeedEndpoint\(\s*apiPort\s*\)\s*\)",
            code);
        Assert.DoesNotMatch(@"UserProjectionIdentifiers\.EndpointVariable\s*,\s*\w+\.GetEndpoint\(", code);
        Assert.Matches(@"\.WithContainerRuntimeArgs\(\s*UserProjectionIdentifiers\.DockerHostMapping\s*\)", code);
    }

    // QA D-3 (ADR-0025 § 7 "A passing result is cached for the AppHost process"): a HealthCheckRegistration
    // whose factory builds the check runs that factory on every probe, so nothing the check remembers lasts
    // longer than one probe. The check is registered once, as one instance, through its own Register.
    [Fact]
    public void AppHostCs_RegistersTheDriftCheckAsOneInstance_NeverAFactoryPerProbe()
    {
        var code = AppHostCode();

        Assert.Single(Regex.Matches(code, @"KeycloakDriftHealthCheck\.Register\(\s*builder\.Services\s*,"));
        Assert.DoesNotMatch(@"new\s+HealthCheckRegistration\(", code);
        Assert.Single(Regex.Matches(code, @"new\s+KeycloakDriftHealthCheck\("));
    }

    // Code review F4 (ADR-0025 § 7, the reset "refuses while a stand runs"): a refused reset touches nothing,
    // so the start-of-program deletion of the E2E stand file — which a running stand's suite still reads — is
    // skipped under --stand-reset; StandReset deletes the file itself only after it has wiped the pair.
    [Fact]
    public void AppHostCs_KeepsTheE2eStandFile_WhenRunAsAStandReset()
    {
        var code = AppHostCode();

        Assert.Single(Regex.Matches(code, @"File\.Delete\("));
        Assert.Matches(
            @"if\s*\(\s*!\s*standResetArguments\.Reset\s*\)\s*\{\s*try\s*\{\s*File\.Delete\(\s*standFilePath\s*\)",
            code);
    }
}
