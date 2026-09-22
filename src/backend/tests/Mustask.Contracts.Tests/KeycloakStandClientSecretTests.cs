using Mustask.AppHost.Provisioning;

namespace Mustask.Contracts.Tests;

/// <summary>
/// Live finding while re-running the Docker-restarting specs:
/// <see cref="KeycloakStandClientSecret"/>'s whole job is "one value, stable
/// for this instance's lifetime" — `AppHost.cs` makes exactly one instance
/// per process, so this is the unit that actually carries the fix. Without
/// it, `AppHost.cs`'s `ResourceReadyEvent` handler generated a fresh secret
/// on every firing, and DCP fires that event repeatedly (~30s) after a bare
/// `docker stop`/`docker start` of the keycloak container.
/// </summary>
public sealed class KeycloakStandClientSecretTests
{
    [Fact]
    public void Value_IsStableAcrossRepeatedReads()
    {
        var secret = new KeycloakStandClientSecret();

        var first = secret.Value;
        var second = secret.Value;
        var third = secret.Value;

        Assert.Equal(first, second);
        Assert.Equal(first, third);
    }

    [Fact]
    public void Value_DiffersBetweenTwoInstances()
    {
        // Models two separate AppHost processes (two stand starts) — each
        // must still get its own fresh secret; only *repeat reads of one
        // instance* (the test above) are the property this fix adds.
        var first = new KeycloakStandClientSecret();
        var second = new KeycloakStandClientSecret();

        Assert.NotEqual(first.Value, second.Value);
    }
}
