using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Mustask.Api.Authorization;
using Mustask.Domain.Identity;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Mustask.Api.IntegrationTests.Features.Authorization;

/// <summary>
/// Права приходят только из
/// <c>resource_access.mustask-api.roles</c> — <c>realm_access</c> и
/// <c>resource_access</c> других клиентов не учитываются вовсе. Перебор по
/// <see cref="EndpointDataSource"/>, как в
/// <see cref="EndpointCategoryInventoryTests"/>, но по каждому маршруту,
/// размеченному <see cref="EndpointCategoryKind.Permission"/> — независимо от
/// модуля: правило «роли realm'а и роли чужих клиентов прав не дают»
/// действует для КАЖДОГО защищённого маршрута, не только для одного модуля.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed partial class PermissionClaimsTests
{
    private readonly MustaskApiFactory _factory;

    public PermissionClaimsTests(MustaskApiFactory factory)
    {
        _factory = factory;
    }

    [GeneratedRegex(@"\{[^}]+\}")]
    private static partial Regex RouteParameterPlaceholder();

    /// <summary>
    /// Substitutes every <c>{param}</c>/<c>{param?}</c> segment with a
    /// literal that satisfies routing — authorization runs before model
    /// binding, so the literal's business validity does not matter, but a
    /// route constraint (e.g. <c>{id:guid}</c>) is
    /// still enforced by routing itself: a non-GUID literal fails to match
    /// the route at all and falls through to 404 before authorization ever
    /// runs, which would silently make this scan skip the endpoint.
    /// </summary>
    private static string ConcreteRoute(string routeTemplate) =>
        RouteParameterPlaceholder().Replace(routeTemplate, match =>
            match.Value.Contains(":guid", StringComparison.OrdinalIgnoreCase)
                ? Guid.NewGuid().ToString()
                : "x");

    private IReadOnlyList<(HttpMethod Method, string Route, string PermissionCode)> PermissionProtectedRoutes()
    {
        using var scope = _factory.Services.CreateScope();

        return scope.ServiceProvider.GetServices<EndpointDataSource>()
            .SelectMany(ds => ds.Endpoints)
            .OfType<RouteEndpoint>()
            .Select(endpoint => (Endpoint: endpoint, Category: endpoint.Metadata.GetOrderedMetadata<EndpointCategoryMetadata>()
                .FirstOrDefault(m => m.Kind == EndpointCategoryKind.Permission)))
            .Where(x => x.Category is not null)
            .Select(x =>
            {
                var methods = x.Endpoint.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? ["GET"];
                return (Method: new HttpMethod(methods[0]), Route: ConcreteRoute(x.Endpoint.RoutePattern.RawText!), PermissionCode: x.Category!.PermissionCode!);
            })
            .ToList();
    }

    /// <summary>Право из resource_access.mustask-api.roles пропускает запрос дальше авторизации.</summary>
    [Fact]
    public async Task RightPermission_ReturnsSuccessNotForbidden()
    {
        var token = KeycloakTestRealm.CreateToken([PermissionCatalog.DictionariesLanguagesRead]);
        var client = _factory.CreateClient();

        var response = await client.SendAsync(
            new HttpRequestMessage(HttpMethod.Get, "/api/dictionaries/languages").WithBearer(token));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// «Роли realm'а и роли чужих клиентов прав не дают»: токен несёт коды
    /// прав только в realm_access и в resource_access чужого клиента
    /// (<see cref="KeycloakTestRealm.ForeignClient"/>) — ни то, ни другое не читается
    /// <see cref="Mustask.Api.Authentication.KeycloakPermissionClaims"/>, поэтому
    /// каждый маршрут, требующий права, отвечает 403 (не 401 — принципал
    /// аутентифицирован).
    /// </summary>
    [Fact]
    public async Task RealmRolesAndForeignClientRoles_GrantNothing_EveryProtectedRouteReturnsForbidden()
    {
        var client = _factory.CreateClient();
        var routes = PermissionProtectedRoutes();

        Assert.NotEmpty(routes);

        foreach (var (method, route, permissionCode) in routes)
        {
            var token = KeycloakTestRealm.CreateToken(configure: t =>
            {
                t.Permissions = null; // resource_access.mustask-api не пишется вовсе
                t.RealmAccessRoles = ["admin", "member", "offline_access"];
                t.ForeignClientPermissions = [permissionCode];
            });

            var response = await client.SendAsync(new HttpRequestMessage(method, route).WithBearer(token));

            Assert.True(
                response.StatusCode == HttpStatusCode.Forbidden,
                $"{method} {route} (требует {permissionCode}): ожидался 403, получен {response.StatusCode}.");
        }
    }

    /// <summary>Аутентифицирован, но список прав пуст — без http.forbidden деньги deny-all неотличимы от отсутствия конкретного права.</summary>
    [Fact]
    public async Task NoPermissions_ReturnsForbiddenWithErrorCode()
    {
        var token = KeycloakTestRealm.CreateToken([]);
        var client = _factory.CreateClient();

        var response = await client.SendAsync(
            new HttpRequestMessage(HttpMethod.Get, "/api/dictionaries/languages").WithBearer(token));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("http.forbidden", body.GetProperty("errorCode").GetString());
    }

    /// <summary>
    /// «Снятое право перестаёт действовать»: два токена одного
    /// пользователя — второй без кода права. Отзыв не мгновенный
    /// (действует до истечения выданного токена), но НОВЫЙ токен без кода уже
    /// не проходит — сервер не хранит состояние между запросами, поэтому
    /// каждый запрос судится по собственному токену.
    /// </summary>
    [Fact]
    public async Task PermissionRemovedFromNextToken_StopsWorking()
    {
        var client = _factory.CreateClient();
        var tokenWithPermission = KeycloakTestRealm.CreateToken([PermissionCatalog.DictionariesLanguagesRead]);
        var tokenWithoutPermission = KeycloakTestRealm.CreateToken([]);

        var first = await client.SendAsync(
            new HttpRequestMessage(HttpMethod.Get, "/api/dictionaries/languages").WithBearer(tokenWithPermission));
        var second = await client.SendAsync(
            new HttpRequestMessage(HttpMethod.Get, "/api/dictionaries/languages").WithBearer(tokenWithoutPermission));

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, second.StatusCode);
    }
}
