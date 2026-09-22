using System.Net;

namespace Mustask.Api.IntegrationTests;

/// <summary>
/// Собственный контур входа и админка демонтированы
/// — каждый снесённый маршрут отвечает 404 и отсутствует в
/// <c>/openapi/v1.json</c>.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class RemovedRoutesTests
{
    private readonly MustaskApiFactory _factory;

    public RemovedRoutesTests(MustaskApiFactory factory)
    {
        _factory = factory;
    }

    public static TheoryData<HttpMethod, string> RemovedRoutes => new()
    {
        { HttpMethod.Post, "/api/auth/login" },
        { HttpMethod.Post, "/api/auth/refresh" },
        { HttpMethod.Post, "/api/auth/logout" },
        { HttpMethod.Get, "/api/auth/me" },
        { HttpMethod.Get, "/api/admin/roles" },
        // Six routes were removed, but this theory used to cover only
        // five — the composite-role write endpoint was
        // missing from the inventory entirely.
        { HttpMethod.Put, "/api/admin/roles/some-role-id/permissions" },
    };

    [Theory]
    [MemberData(nameof(RemovedRoutes))]
    public async Task RemovedRoute_ReturnsNotFound(HttpMethod method, string route)
    {
        var client = _factory.CreateClient();

        var response = await client.SendAsync(new HttpRequestMessage(method, route));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task OpenApiDocument_DoesNotListRemovedRoutes()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/openapi/v1.json");
        var document = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain("/api/auth/", document, StringComparison.Ordinal);
        Assert.DoesNotContain("/api/admin/", document, StringComparison.Ordinal);
    }
}
