using System.Text.Json;
using Mustask.AppHost.Provisioning;

namespace Mustask.Contracts.Tests;

/// <summary>
/// mod-014 on the stand (ADR-0025 § 3, § 7): the push secret AppHost
/// generates, and what the realm import declares for the feed.
/// </summary>
public sealed class UserProjectionStandTests
{
    private static JsonElement RealmImport()
    {
        var path = Path.Combine(RepositoryLayout.Root.FullName, "deploy", "keycloak", "realms", "mustask-realm.json");

        using var document = JsonDocument.Parse(File.ReadAllText(path));

        return document.RootElement.Clone();
    }

    // US-6 "A fresh clone needs no new secret": generated, stable for one AppHost process, fresh for the next.
    [Fact]
    public void PushSecret_IsStablePerInstance_DistinctAcrossInstances_AndLongEnough()
    {
        var first = new UserProjectionPushSecret();
        var second = new UserProjectionPushSecret();

        Assert.Equal(first.Value, first.Value);
        Assert.NotEqual(first.Value, second.Value);
        Assert.True(first.Value.Length >= 32, "MUSTASK refuses a secret under 32 characters at startup.");
        Assert.Matches("^[A-Za-z0-9_-]+$", first.Value);
    }

    // ADR-0025 § 7, "Enabling": the import enables the provider in realm mustask, keeping Keycloak's own logger.
    [Fact]
    public void RealmImport_EnablesTheProviderBesideJbossLogging()
    {
        var listeners = RealmImport().GetProperty("eventsListeners").EnumerateArray().Select(item => item.GetString()).ToList();

        Assert.Equal(["jboss-logging", UserProjectionIdentifiers.ProviderId], listeners);
    }

    // US-6 "The credential comes only from environment or user secrets": nothing of the feed's credential in the import.
    [Fact]
    public void RealmImport_HoldsNoPushCredential()
    {
        var text = File.ReadAllText(Path.Combine(RepositoryLayout.Root.FullName, "deploy", "keycloak", "realms", "mustask-realm.json"));

        Assert.DoesNotContain("push-secret", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("PUSH_SECRET", text, StringComparison.Ordinal);
        Assert.DoesNotContain("UserDirectory", text, StringComparison.Ordinal);
    }

    // D-15: the realm import keeps editUsernameAllowed off; the rename scenario is proven at the push level.
    [Fact]
    public void RealmImport_KeepsUsernameEditingOff()
    {
        var realm = RealmImport();

        Assert.False(realm.TryGetProperty("editUsernameAllowed", out var editUsername) && editUsername.GetBoolean());
    }

    // QA D-1 (US-5 catch-up): the provider reaches the api at its own port on the Docker host — never through
    // Aspire's container tunnel — and the host's name is mapped to the host gateway, so a Linux engine resolves it too.
    [Fact]
    public void TheFeedEndpoint_IsTheApisPortOnTheDockerHost_MappedOnEveryEngine()
    {
        Assert.Equal("http://host.docker.internal:5000", UserProjectionIdentifiers.FeedEndpoint(5000));
        Assert.Equal("http://host.docker.internal:5100", UserProjectionIdentifiers.FeedEndpoint(5100));
        Assert.Equal("--add-host=host.docker.internal:host-gateway", UserProjectionIdentifiers.DockerHostMapping);
    }

    [Fact]
    public void TheProvidersSpiOptions_AreSpelledForItsId()
    {
        var expectedPrefix = "KC_SPI_EVENTS_LISTENER__" + UserProjectionIdentifiers.ProviderId.Replace('-', '_').ToUpperInvariant() + "__";

        Assert.StartsWith(expectedPrefix, UserProjectionIdentifiers.EndpointVariable, StringComparison.Ordinal);
        Assert.StartsWith(expectedPrefix, UserProjectionIdentifiers.PushSecretVariable, StringComparison.Ordinal);
        Assert.StartsWith(expectedPrefix, UserProjectionIdentifiers.RequireHttpsVariable, StringComparison.Ordinal);
        Assert.StartsWith(expectedPrefix, UserProjectionIdentifiers.ReconcilePeriodVariable, StringComparison.Ordinal);
    }
}
