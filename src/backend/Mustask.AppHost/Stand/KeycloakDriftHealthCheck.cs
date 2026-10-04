using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Mustask.AppHost.Provisioning;

namespace Mustask.AppHost.Stand;

/// <summary>
/// The <c>keycloak</c> resource's drift health check (mod-014 D-10 (a);
/// ADR-0025 § 7): unhealthy until realm <c>mustask</c> answers, then the
/// result of <see cref="KeycloakStandDriftCheck"/>. Aspire raises
/// <c>ResourceReadyEvent</c> only on a healthy resource, so on any finding
/// ADR-0023's provisioning does not run and the E2E stand file is not
/// written — the stand does not come up ready. Each new finding is logged
/// once at error through the AppHost's logger, naming the remedy; a pass is
/// cached for the AppHost process. Both hold only because the check is one
/// instance for the process — register it through <see cref="Register"/>,
/// never through a registration factory that builds it, which the health-check
/// service calls on every probe (QA D-3).
/// </summary>
public sealed class KeycloakDriftHealthCheck(
    Uri keycloakAddress,
    Func<CancellationToken, Task<KeycloakDriftCheckOptions>> options,
    ILogger logger,
    HttpMessageHandler? handler = null) : IHealthCheck
{
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(10);

    private readonly HashSet<string> _logged = new(StringComparer.Ordinal);

    private readonly SemaphoreSlim _gate = new(1, 1);

    private volatile bool _passed;

    /// <summary>
    /// Registers the check under <see cref="UserProjectionIdentifiers.DriftHealthCheckKey"/>
    /// as a single instance: <paramref name="create"/> runs once, and every probe
    /// asks that instance, so its cached pass and its record of logged findings
    /// last for the AppHost process.
    /// </summary>
    public static void Register(IServiceCollection services, Func<IServiceProvider, KeycloakDriftHealthCheck> create)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(create);

        services.AddSingleton(create);
        services.AddHealthChecks().Add(new HealthCheckRegistration(
            UserProjectionIdentifiers.DriftHealthCheckKey,
            provider => provider.GetRequiredService<KeycloakDriftHealthCheck>(),
            failureStatus: null,
            tags: null));
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        if (_passed)
        {
            return HealthCheckResult.Healthy("The live realm matches the realm import.");
        }

        await _gate.WaitAsync(cancellationToken);

        try
        {
            if (_passed)
            {
                return HealthCheckResult.Healthy("The live realm matches the realm import.");
            }

            using var http = handler is null
                ? new HttpClient { BaseAddress = keycloakAddress, Timeout = RequestTimeout }
                : new HttpClient(handler, disposeHandler: false) { BaseAddress = keycloakAddress, Timeout = RequestTimeout };

            using (var realm = await http.GetAsync("/realms/mustask", cancellationToken))
            {
                if (!realm.IsSuccessStatusCode)
                {
                    return HealthCheckResult.Unhealthy("Realm mustask is not answering yet.");
                }
            }

            var findings = await KeycloakStandDriftCheck.RunAsync(http, await options(cancellationToken), cancellationToken);

            if (findings.Count == 0)
            {
                _passed = true;
                logger.LogInformation("Keycloak store: the live realm matches the realm import.");

                return HealthCheckResult.Healthy("The live realm matches the realm import.");
            }

            foreach (var finding in findings.Where(_logged.Add))
            {
                logger.LogError("Keycloak store differs from the realm import: {Finding}", finding);
            }

            return HealthCheckResult.Unhealthy(string.Join(Environment.NewLine, findings));
        }
        catch (HttpRequestException ex)
        {
            return HealthCheckResult.Unhealthy("Keycloak is not reachable yet.", ex);
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            return HealthCheckResult.Unhealthy("Keycloak did not answer in time.", ex);
        }
        finally
        {
            _gate.Release();
        }
    }
}
