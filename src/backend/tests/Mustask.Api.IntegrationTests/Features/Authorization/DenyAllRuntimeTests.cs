using System.Net;
using Mustask.Domain.Identity;

namespace Mustask.Api.IntegrationTests.Features.Authorization;

// Fallback-политика deny-all проверяется на живом HTTP-конвейере,
// не только инвентаризационным тестом (см. EndpointCategoryInventoryTests).
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class DenyAllRuntimeTests
{
    private readonly MustaskApiFactory _factory;

    public DenyAllRuntimeTests(MustaskApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task UnmarkedEndpoint_Anonymous_ReturnsUnauthorized_HandlerNeverRan()
    {
        TestEndpointsStartupFilter.DenyAllProbeHandlerExecuted = false;
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/__test/deny-all-probe");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.False(TestEndpointsStartupFilter.DenyAllProbeHandlerExecuted);
    }

    [Fact]
    public async Task UnmarkedEndpoint_AuthenticatedWithFullPermissionSet_ReturnsForbidden_HandlerNeverRan()
    {
        TestEndpointsStartupFilter.DenyAllProbeHandlerExecuted = false;
        var client = _factory.CreateClient();
        // Токен полного набора прав из KeycloakTestRealm, не через
        // удалённый /api/auth/login («закрыто по умолчанию»).
        var accessToken = KeycloakTestRealm.CreateToken([.. PermissionCatalog.AllCodes]);

        var response = await client.SendAsync(
            new HttpRequestMessage(HttpMethod.Get, "/__test/deny-all-probe").WithBearer(accessToken));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.False(TestEndpointsStartupFilter.DenyAllProbeHandlerExecuted);
    }
}
