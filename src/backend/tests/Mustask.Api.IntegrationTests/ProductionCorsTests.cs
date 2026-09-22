using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Mustask.Api.IntegrationTests;

// The claim "CORS is only enabled in Development" was previously
// verified by reading
// Program.cs, not by a test. This proves it end-to-end in Production.
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class ProductionCorsTests : IDisposable
{
    private readonly WebApplicationFactory<Program> _factory;

    // The MustaskApiFactory parameter is not used directly: its only purpose is
    // to keep this class inside MustaskApiCollection, so xUnit has already run
    // MustaskApiFactory.InitializeAsync() (and therefore populated the
    // process-wide ConnectionStrings__mustask environment variable) before this
    // factory's own Program.cs top-level statements run — see MustaskApiFactory
    // for why AddInfrastructure() needs that env var to already be set.
    public ProductionCorsTests(MustaskApiFactory _)
    {
        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder => builder.UseEnvironment("Production"));
    }

    [Fact]
    public async Task GetHealth_ProductionWithOriginHeader_DoesNotReturnAccessControlAllowOriginHeader()
    {
        var client = _factory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.Add("Origin", "http://localhost:3000");

        var response = await client.SendAsync(request);

        // /health is mapped in every environment (see
        // ServiceDefaults.Extensions.MapDefaultEndpoints), so this now
        // actually returns 200 — but the status code is irrelevant to this
        // test either way, only the absence of the CORS header matters here.
        Assert.False(response.Headers.Contains("Access-Control-Allow-Origin"));
    }

    public void Dispose() => _factory.Dispose();
}
