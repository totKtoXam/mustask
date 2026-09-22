using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Mustask.Api.Authentication.Readiness;

/// <summary>
/// Health check <c>keycloak-realm</c> — без тега
/// <c>live</c>, поэтому <c>/health</c> отражает готовность realm'а, а
/// <c>/alive</c> (<c>Predicate = live</c>) её не видит и остаётся «жив»
/// (перезапускать нечего, пока realm восстанавливается).
/// </summary>
public sealed class RealmReadinessHealthCheck(RealmReadiness readiness) : IHealthCheck
{
    /// <summary>Имя записи в отчёте <c>HealthCheckService</c>.</summary>
    public const string Name = "keycloak-realm";

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var state = readiness.Current;

        var result = state.IsReady
            ? HealthCheckResult.Healthy()
            : HealthCheckResult.Unhealthy($"Keycloak realm is not ready: {state.Reason}.");

        return Task.FromResult(result);
    }
}
