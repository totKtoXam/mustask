using System.Net;

namespace Mustask.Api.IntegrationTests;

// project-bootstrap: единственный проверяемый endpoint каркаса — /health.
// Требует Docker (Testcontainers поднимает реальный Postgres).
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class HealthEndpointTests
{
    private readonly MustaskApiFactory _factory;

    public HealthEndpointTests(MustaskApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetHealth_AppStarted_ReturnsHealthy()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/health");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", body);
    }
}
