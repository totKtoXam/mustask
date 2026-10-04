namespace Mustask.AppHost.Provisioning;

/// <summary>
/// The names the stand uses to wire the user projection's feed (mod-014
/// plan-backend.md § 1; ADR-0025 § 3) — one place, so <c>AppHost.cs</c>, the
/// drift check and the contract tests cannot each type their own copy.
/// </summary>
public static class UserProjectionIdentifiers
{
    /// <summary>The Keycloak event-listener provider's id, as realm <c>mustask</c>'s <c>eventsListeners</c> name it.</summary>
    public const string ProviderId = "mustask-user-projection";

    /// <summary>The AppHost secret parameter both sides read; generated per AppHost process, never human-set.</summary>
    public const string PushSecretParameter = "user-projection-push-secret";

    /// <summary>Mustask.Api's configuration key of the shared secret, as an environment variable.</summary>
    public const string ApiPushSecretVariable = "UserDirectory__Push__Secret";

    /// <summary>
    /// The provider's SPI options as Keycloak 26's environment variables
    /// (<c>KC_SPI_&lt;SPI&gt;__&lt;PROVIDER&gt;__&lt;OPTION&gt;</c>).
    /// </summary>
    public const string EndpointVariable = "KC_SPI_EVENTS_LISTENER__MUSTASK_USER_PROJECTION__ENDPOINT";

    public const string PushSecretVariable = "KC_SPI_EVENTS_LISTENER__MUSTASK_USER_PROJECTION__PUSH_SECRET";

    public const string RequireHttpsVariable = "KC_SPI_EVENTS_LISTENER__MUSTASK_USER_PROJECTION__REQUIRE_HTTPS";

    public const string ReconcilePeriodVariable = "KC_SPI_EVENTS_LISTENER__MUSTASK_USER_PROJECTION__RECONCILE_PERIOD_MINUTES";

    /// <summary>
    /// The name the Keycloak container reaches the Docker host by. Mustask.Api's
    /// endpoint is not proxied and listens on every interface, so the host's own
    /// port answers there directly — not through Aspire's container tunnel
    /// (<c>aspire.dev.internal</c>), which stops forwarding to Mustask.Api for
    /// good once the <c>keycloak</c> resource has restarted and the api restarts
    /// after it (mod-014 QA D-1; plan-backend.md § 13, assumption 4).
    /// </summary>
    public const string DockerHost = "host.docker.internal";

    /// <summary>
    /// The container runtime argument that maps <see cref="DockerHost"/> to the
    /// host on every engine: Docker Desktop resolves the name by itself, a Linux
    /// Docker Engine only through this mapping.
    /// </summary>
    public const string DockerHostMapping = "--add-host=" + DockerHost + ":host-gateway";

    /// <summary>The provider's <c>endpoint</c> on the stand: Mustask.Api's port on the Docker host.</summary>
    public static string FeedEndpoint(int apiPort) =>
        FormattableString.Invariant($"http://{DockerHost}:{apiPort}");

    /// <summary>AppHost configuration: the reconciliation period on the stand, for a QA run that cannot wait an hour.</summary>
    public const string ReconcilePeriodSetting = "Keycloak:UserProjection:ReconcilePeriodMinutes";

    /// <summary>The <c>keycloak</c> resource's health check that compares the live realm with the import.</summary>
    public const string DriftHealthCheckKey = "keycloak-realm-drift";

    /// <summary>The reset's command, as the drift check's findings name it.</summary>
    public const string ResetCommand = "scripts/stand-reset.sh (PowerShell: scripts/stand-reset.ps1)";
}
