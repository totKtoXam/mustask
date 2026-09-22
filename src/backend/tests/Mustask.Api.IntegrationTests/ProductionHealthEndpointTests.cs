using System.Net;
using Microsoft.AspNetCore.Hosting;

namespace Mustask.Api.IntegrationTests;

// Раньше
// ServiceDefaults.Extensions.MapDefaultEndpoints мапил /health и /alive
// только под IsDevelopment() — в проде /health отдавал 404, инфраструктура
// не могла проверить готовность/живость приложения. Проверяем и Development
// (HealthEndpointTests, DatabaseHealthCheckTests), и среды вне неё здесь —
// той же логикой, что ApiExplorerTests использует для Production/Staging
// (WithWebHostBuilder поверх уже поднятого MustaskApiFactory, второй Postgres-
// контейнер не создаётся).
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class ProductionHealthEndpointTests
{
    private readonly MustaskApiFactory _factory;

    public ProductionHealthEndpointTests(MustaskApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task GetHealth_ProductionEnvironment_ReturnsHealthy()
    {
        using var factory = _factory.WithWebHostBuilder(builder => builder.UseEnvironment("Production"));
        var client = factory.CreateClient();

        var response = await client.GetAsync("/health");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        // Наружу должен уходить только сам статус, ни имён
        // отдельных health check'ов, ни деталей БД/исключений — это
        // гарантирует отсутствие кастомного ResponseWriter у MapHealthChecks.
        // Точное равенство, а не Contains — если кто-то
        // добавит ResponseWriter с деталями, тело перестанет быть голым
        // "Healthy", и тест это поймает.
        Assert.Equal("Healthy", body);
    }

    [Fact]
    public async Task GetAlive_ProductionEnvironment_ReturnsHealthy()
    {
        using var factory = _factory.WithWebHostBuilder(builder => builder.UseEnvironment("Production"));
        var client = factory.CreateClient();

        var response = await client.GetAsync("/alive");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", body);
    }
}
