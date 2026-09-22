using System.Net;
using Mustask.Domain.Identity;

namespace Mustask.Api.IntegrationTests;

/// <summary>
/// «CSRF-контур снят»: cookie-примитивы демонтированы вместе с
/// собственным контуром входа — ни один ответ не выставляет
/// <c>__Host-mustask_csrf</c>/<c>mustask_rt</c>, и ни один маршрут не требует
/// заголовок <c>X-CSRF-Token</c>.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class CsrfRemovedTests
{
    private readonly MustaskApiFactory _factory;

    public CsrfRemovedTests(MustaskApiFactory factory)
    {
        _factory = factory;
    }

    private static void AssertNoAuthCookies(HttpResponseMessage response)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out var setCookies))
        {
            return;
        }

        Assert.DoesNotContain(setCookies, c => c.StartsWith("__Host-mustask_csrf=", StringComparison.Ordinal));
        Assert.DoesNotContain(setCookies, c => c.StartsWith("mustask_rt=", StringComparison.Ordinal));
    }

    [Fact]
    public async Task PublicRoute_DoesNotSetAuthCookies()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/health");

        AssertNoAuthCookies(response);
    }

    [Fact]
    public async Task ProtectedRoute_WithoutCsrfHeader_DoesNotFailBecauseOfIt_AndDoesNotSetAuthCookies()
    {
        var token = KeycloakTestRealm.CreateToken([PermissionCatalog.DictionariesLanguagesRead]);
        var client = _factory.CreateClient();

        // Намеренно без X-CSRF-Token: если бы контур ещё требовал его, ответ
        // отличался бы от обычного результата авторизованного GET.
        var response = await client.SendAsync(
            new HttpRequestMessage(HttpMethod.Get, "/api/dictionaries/languages").WithBearer(token));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AssertNoAuthCookies(response);
    }
}
