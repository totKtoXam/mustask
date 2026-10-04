using System.Text.RegularExpressions;

namespace Mustask.Contracts.Tests;

/// <summary>
/// mod-014 ADR-0025 § 1: the stand's Keycloak image is built from
/// <c>deploy/keycloak/Dockerfile</c>, and a Keycloak upgrade re-verifies the
/// provider by construction — the Dockerfile's default tag and image must
/// equal <c>Mustask.AppHost</c>'s defaults, so bumping one without the other
/// fails here, and bumping both lands in <c>scripts/verify.sh</c>'s
/// <c>provider</c> bucket. The final stage puts the provider's jar where
/// Keycloak loads providers from.
/// </summary>
[Trait("Category", "KeycloakProvider")]
public sealed partial class KeycloakProviderImageTests
{
    private static readonly string KeycloakDirectory = Path.Combine(RepositoryLayout.Root.FullName, "deploy", "keycloak");

    private static string Dockerfile() => File.ReadAllText(Path.Combine(KeycloakDirectory, "Dockerfile"));

    private static string AppHost() => File.ReadAllText(Path.Combine(RepositoryLayout.Source.FullName, "Mustask.AppHost", "AppHost.cs"));

    private static string Single(Regex pattern, string text, string what)
    {
        var matches = pattern.Matches(text);

        Assert.True(matches.Count == 1, $"Expected exactly one {what}, found {matches.Count}.");

        return matches[0].Groups["value"].Value;
    }

    [Fact]
    public void DockerfileDefaultTag_EqualsAppHostsDefaultTag()
    {
        var dockerfileTag = Single(DockerfileTagArg(), Dockerfile(), "ARG KEYCLOAK_TAG=… in the Dockerfile");
        var appHostTag = Single(AppHostTagDefault(), AppHost(), "Keycloak:Tag default in AppHost.cs");

        Assert.Equal(appHostTag, dockerfileTag);
    }

    [Fact]
    public void DockerfileDefaultImage_EqualsAppHostsDefaultImage()
    {
        var dockerfileImage = Single(DockerfileImageArg(), Dockerfile(), "ARG KEYCLOAK_IMAGE=… in the Dockerfile");
        var appHostImage = Single(AppHostImageDefault(), AppHost(), "Keycloak:Image default in AppHost.cs");

        Assert.Equal(appHostImage, dockerfileImage);
    }

    [Fact]
    public void AppHost_BuildsKeycloakFromTheDockerfile_PassingImageAndTag()
    {
        var appHost = AppHost();

        Assert.Contains("AddDockerfile(\"keycloak\", \"../../../deploy/keycloak\")", appHost, StringComparison.Ordinal);
        Assert.Contains(".WithBuildArg(\"KEYCLOAK_IMAGE\", keycloakImage)", appHost, StringComparison.Ordinal);
        Assert.Contains(".WithBuildArg(\"KEYCLOAK_TAG\", keycloakTag)", appHost, StringComparison.Ordinal);
        Assert.DoesNotContain("AddContainer(\"keycloak\"", appHost, StringComparison.Ordinal);
    }

    [Fact]
    public void FinalStage_PutsTheProvidersJarIntoKeycloaksProviders()
    {
        Assert.Contains("/opt/keycloak/providers/mustask-user-projection.jar", Dockerfile(), StringComparison.Ordinal);
    }

    [Fact]
    public void ProviderStage_ReadsTheKeycloakVersionFromTheImageItBuildsFor()
    {
        var dockerfile = Dockerfile();

        Assert.Contains("FROM ${KEYCLOAK_IMAGE}:${KEYCLOAK_TAG} AS keycloak", dockerfile, StringComparison.Ordinal);
        Assert.Contains("AS provider", dockerfile, StringComparison.Ordinal);
        Assert.Contains("-Dkeycloak.version=", dockerfile, StringComparison.Ordinal);
    }

    [Fact]
    public void Provider_IsRegisteredUnderTheIdTheRealmImportEnables()
    {
        var services = Path.Combine(
            KeycloakDirectory, "providers", "user-projection", "src", "main", "resources", "META-INF", "services",
            "org.keycloak.events.EventListenerProviderFactory");

        Assert.Equal("mustask.keycloak.userprojection.UserProjectionListenerFactory", File.ReadAllText(services).Trim());
    }

    [GeneratedRegex(@"^ARG KEYCLOAK_TAG=(?<value>\S+)\s*$", RegexOptions.Multiline)]
    private static partial Regex DockerfileTagArg();

    [GeneratedRegex(@"^ARG KEYCLOAK_IMAGE=(?<value>\S+)\s*$", RegexOptions.Multiline)]
    private static partial Regex DockerfileImageArg();

    [GeneratedRegex(@"Configuration\[""Keycloak:Tag""\]\s*\?\?\s*""(?<value>[^""]+)""")]
    private static partial Regex AppHostTagDefault();

    [GeneratedRegex(@"Configuration\[""Keycloak:Image""\]\s*\?\?\s*""(?<value>[^""]+)""")]
    private static partial Regex AppHostImageDefault();
}
