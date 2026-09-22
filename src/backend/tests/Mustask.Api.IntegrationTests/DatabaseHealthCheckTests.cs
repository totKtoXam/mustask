using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Mustask.Api.IntegrationTests;

// DB availability is checked via
// AddNpgsqlDbContext (Aspire client integration), which registers a health
// check by default — MapHealthChecks("/health") in Mustask.ServiceDefaults runs
// all registered checks (no tag filter), so this is already exercised by
// HealthEndpointTests' green "/health" response. This test additionally
// pins down *why* it's green: a DB-specific check is actually registered and
// healthy, not just that /health happens to report OK with nothing to check.
//
// This class used to be IClassFixture<MustaskApiFactory> instead of
// [Collection(MustaskApiCollection.Name)] — the one case in the project that
// wasn't following MustaskApiCollection's own rule. A private IClassFixture spins
// up a second Postgres container and its own xUnit collection, which then
// races every other test class for the process-wide ConnectionStrings__mustask
// env var (see MustaskApiCollection/MustaskApiFactory), making the whole
// Mustask.Api.IntegrationTests run non-deterministic.
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class DatabaseHealthCheckTests
{
    private readonly MustaskApiFactory _factory;

    public DatabaseHealthCheckTests(MustaskApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task CheckHealthAsync_DatabaseReachable_ReportsAHealthyDatabaseEntry()
    {
        using var scope = _factory.Services.CreateScope();
        var healthCheckService = scope.ServiceProvider.GetRequiredService<HealthCheckService>();

        var report = await healthCheckService.CheckHealthAsync();

        // "self" is Mustask.ServiceDefaults' own liveness check. AddNpgsqlDbContext<MustaskDbContext>
        // names its check after the DbContext type (verified against the
        // real report, not assumed) -- asserting that specific key, rather
        // than merely "a second entry exists", is what actually
        // distinguishes a DB-availability check from any other unrelated
        // check that could later be added.
        Assert.Contains("MustaskDbContext", report.Entries.Keys);
        Assert.All(report.Entries, entry => Assert.Equal(HealthStatus.Healthy, entry.Value.Status));
        Assert.Equal(HealthStatus.Healthy, report.Status);
    }
}
