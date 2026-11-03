using System.Text.RegularExpressions;

namespace Mustask.Contracts.Tests;

/// <summary>
/// The stand's Keycloak image is built from <c>deploy/keycloak/Dockerfile</c> (the image and tag arguments are
/// <c>Mustask.AppHost</c>'s): the Dockerfile's default tag and image must equal AppHost's defaults, so bumping one
/// without the other fails here. Since tech-025 (ADR-0034) the image carries no provider.
/// </summary>
public sealed partial class KeycloakImageTests
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

    // tech-025 (ADR-0034): the event-listener provider is gone — no build stage, no jar, no Java sources.
    [Fact]
    public void Dockerfile_BuildsNoProvider_AndNoProviderSourcesRemain()
    {
        Assert.DoesNotContain("AS provider", Dockerfile(), StringComparison.Ordinal);
        Assert.DoesNotContain("providers/", Dockerfile(), StringComparison.Ordinal);
        Assert.False(Directory.Exists(Path.Combine(KeycloakDirectory, "providers")), "deploy/keycloak/providers must not exist.");
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
