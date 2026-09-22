using Mustask.Api.Authentication.Readiness;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Mustask.Api.IntegrationTests;

/// <summary>
/// В дополнение к <see cref="DatabaseHealthCheckTests"/>,
/// а не вместо: <c>/health</c> обязан отражать готовность realm'а
/// (<see cref="RealmReadinessHealthCheck.Name"/>, без тега <c>live</c>) и
/// по-прежнему видеть здоровую базу — хранилище остаётся под справочниками
/// (ADR-0021).
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class RealmReadinessHealthCheckTests
{
    private readonly MustaskApiFactory _factory;

    public RealmReadinessHealthCheckTests(MustaskApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task CheckHealthAsync_RealmReady_ReportsKeycloakRealmEntryHealthy_AlongsideDatabase()
    {
        using var scope = _factory.Services.CreateScope();
        // Общая фабрика — realm мог быть переключён другим тестом; вернуть в
        // Ok перед проверкой (та же дисциплина, что у KeycloakTokenEndpointResponder).
        _factory.RealmMetadataFake.CurrentMode = KeycloakTestRealm.FakeRealmMetadata.Mode.Ok;
        await scope.ServiceProvider.GetRequiredService<RealmReadinessMonitor>().CheckAsync();

        var healthCheckService = scope.ServiceProvider.GetRequiredService<HealthCheckService>();
        var report = await healthCheckService.CheckHealthAsync();

        Assert.Contains(RealmReadinessHealthCheck.Name, report.Entries.Keys);
        Assert.Contains("MustaskDbContext", report.Entries.Keys);
        Assert.All(report.Entries, entry => Assert.Equal(HealthStatus.Healthy, entry.Value.Status));
        Assert.Equal(HealthStatus.Healthy, report.Status);

        // Без тега "live" — не входит в набор /alive (Predicate = live).
        Assert.DoesNotContain("live", report.Entries[RealmReadinessHealthCheck.Name].Tags);
    }
}
