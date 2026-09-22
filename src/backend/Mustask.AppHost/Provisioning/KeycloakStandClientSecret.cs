using System.Security.Cryptography;

namespace Mustask.AppHost.Provisioning;

/// <summary>
/// Live finding while re-running the Docker-restarting specs:
/// one random value, generated lazily on first read and stable for the
/// lifetime of whatever holds this instance — which `AppHost.cs` makes
/// exactly one of, per process, so every `ResourceReadyEvent` firing within
/// one stand start reads the same value.
///
/// The bug this exists to make impossible: DCP re-fires
/// `ResourceReadyEvent` on the `keycloak` resource repeatedly (observed up
/// to ~30s) after a bare `docker stop`/`docker start` of the container —
/// exactly what the E2E suite's Docker-restarting specs do. Generating the
/// secret inline inside the event handler meant every firing minted a fresh
/// one and re-provisioned `mustask-e2e` with it, rotating the live client's
/// secret out from under a stand file the suite had already read
/// (`unauthorized_client`, caught live by two separate specs). The original rule
/// still holds — generated, never human-chosen, never logged — only the
/// "one process, one value" half was missing.
///
/// <see cref="Lazy{T}"/>'s default thread-safety mode additionally means
/// concurrent firings (not observed, but not ruled out either) still only
/// ever generate one value.
/// </summary>
public sealed class KeycloakStandClientSecret
{
    private readonly Lazy<string> _value = new(() => RandomNumberGenerator.GetHexString(40));

    public string Value => _value.Value;
}
