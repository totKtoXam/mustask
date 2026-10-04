using Mustask.Api.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Mustask.Api.IntegrationTests.Features.Authorization;

/// <summary>
/// Вторая, независимая от рантайма сетка — перебирает реальные endpoint'ы через
/// <see cref="EndpointDataSource"/> и требует у каждого ровно одну
/// <see cref="EndpointCategoryMetadata"/>. Endpoint'ы `/__test/*`
/// (<see cref="TestEndpointsStartupFilter"/>) существуют только в тестовом
/// хосте и в инвентаризацию не входят — иначе тест проверял бы сам себя.
/// Служебные endpoint'ы фреймворка (health/alive/openapi), которые наша
/// разметка не касается вовсе, зафиксированы отдельным allow-list'ом и
/// дополнительно проверены на анонимность.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class EndpointCategoryInventoryTests
{
    private readonly MustaskApiFactory _factory;

    public EndpointCategoryInventoryTests(MustaskApiFactory factory)
    {
        _factory = factory;
    }

    // Единственные endpoint'ы категории "публичный" среди тех, что размечены
    // нашей системой категорий (health/alive/openapi — отдельный allow-list
    // ниже, они вне неё).
    // Список фиксируется здесь СОЗНАТЕЛЬНО (роль теста — заставить назвать
    // каждый публичный маршрут поимённо, а не открыть всё молча).
    private static readonly HashSet<string> ExpectedPublicRoutes = new(StringComparer.Ordinal)
    {
        // Собственного контура входа нет (ADR-0018): вход — через Keycloak.
        // ADR-0022: сессия панели — все четыре публичны по отношению к схеме
        // Bearer/deny-all fallback; каждый эндпоинт аутентифицируется явно
        // против cookie-схемы внутри своего обработчика.
        "GET /api/session/login",
        // Mapped for both methods — form_post delivers the authorization
        // code by POST, not GET.
        "GET/POST /api/session/callback",
        "POST /api/session/token",
        "POST /api/session/logout",
        // mod-014 (ADR-0025 § 3): the user projection's push channel — public
        // with respect to Bearer, authenticated by its own HMAC signature in
        // SignedPushBody; called by the Keycloak provider only.
        "POST /api/user-directory/feed/changes",
        "PUT /api/user-directory/feed/snapshots/{snapshotId:guid}",
        "PUT /api/user-directory/feed/snapshots/{snapshotId:guid}/pages/{pageNumber:int}",
        "PUT /api/user-directory/feed/snapshots/{snapshotId:guid}/completion",
    };

    // Служебные endpoint'ы фреймворка: не размечены
    // EndpointCategoryMetadata (сторонний код), но обязаны быть публичными.
    // Маршруты explorer'а (ApiExplorerExtensions) добавлены при
    // интеграции с deny-all fallback — сам explorer размечает их
    // .AllowAnonymous(), не нашей системой категорий (сторонний код Scalar).
    private static readonly HashSet<string> ExpectedUnmarkedFrameworkRoutes = new(StringComparer.Ordinal)
    {
        "ANY /health",
        "ANY /alive",
        "GET /openapi/{documentName}.json",
        "GET /scalar/{documentName?}",
        "GET /scalar/scalar.js",
        "GET /scalar/scalar.aspnetcore.js",
        "GET /scalar/favicon.svg",
    };

    private IReadOnlyList<RouteEndpoint> RealEndpoints()
    {
        using var scope = _factory.Services.CreateScope();
        return scope.ServiceProvider.GetServices<EndpointDataSource>()
            .SelectMany(ds => ds.Endpoints)
            .OfType<RouteEndpoint>()
            // Тестовые endpoint'ы существуют только в этом хосте
            // (TestEndpointsStartupFilter) — не часть реального API.
            .Where(e => !e.RoutePattern.RawText!.StartsWith("/__test/", StringComparison.Ordinal))
            .ToList();
    }

    private static string RouteKey(RouteEndpoint endpoint)
    {
        var methods = endpoint.Metadata.GetMetadata<Microsoft.AspNetCore.Routing.HttpMethodMetadata>()?.HttpMethods
            ?? [];
        // Health-check endpoint'ы (MapHealthChecks) не ограничивают метод —
        // без HttpMethodMetadata (пусто) endpoint отвечает на любой глагол.
        var method = methods.Count switch
        {
            0 => "ANY",
            1 => methods[0],
            _ => string.Join("/", methods.OrderBy(m => m, StringComparer.Ordinal)),
        };
        return $"{method} {endpoint.RoutePattern.RawText}";
    }

    [Fact]
    public void RealEndpoints_AreDiscoverable_Canary()
    {
        // Канарейка: если EndpointDataSource однажды перестанет резолвиться
        // через DI (например, после смены версии ASP.NET Core), остальные
        // тесты этого файла молча прошли бы на пустом множестве — это не
        // должно происходить незамеченным.
        var endpoints = RealEndpoints();

        Assert.True(endpoints.Count >= 6, $"Ожидались как минимум 6 реальных endpoint'ов, обнаружено {endpoints.Count}.");
    }

    [Fact]
    public void EveryRealEndpoint_HasExactlyOneCategory_UnlessExplicitlyAllowListedFrameworkEndpoint()
    {
        var endpoints = RealEndpoints();

        foreach (var endpoint in endpoints)
        {
            var key = RouteKey(endpoint);
            var categories = endpoint.Metadata.GetOrderedMetadata<EndpointCategoryMetadata>();

            if (ExpectedUnmarkedFrameworkRoutes.Contains(key))
            {
                Assert.True(
                    categories.Count == 0,
                    $"Служебный endpoint '{key}' неожиданно размечен EndpointCategoryMetadata — " +
                    "уберите его из allow-list'а служебных, он теперь часть реальной разметки.");
                Assert.True(
                    endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null,
                    $"Служебный endpoint '{key}' обязан быть анонимным.");
            }
            else
            {
                Assert.True(
                    categories.Count == 1,
                    $"Endpoint '{key}' имеет {categories.Count} EndpointCategoryMetadata " +
                    "(требуется ровно одна — Public/AuthenticatedOnly/RequiresPermission).");
            }
        }
    }

    [Fact]
    public void PublicRoutes_MatchTheFixedListFromApiContract()
    {
        var endpoints = RealEndpoints();

        var actualPublic = endpoints
            .Where(e => e.Metadata.GetOrderedMetadata<EndpointCategoryMetadata>()
                .Any(m => m.Kind == EndpointCategoryKind.Public))
            .Select(RouteKey)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Equal(ExpectedPublicRoutes, actualPublic);
    }

    [Fact]
    public void UnmarkedFrameworkRoutes_MatchTheFixedAllowList()
    {
        var endpoints = RealEndpoints();

        var actualUnmarked = endpoints
            .Where(e => e.Metadata.GetOrderedMetadata<EndpointCategoryMetadata>().Count == 0)
            .Select(RouteKey)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Equal(ExpectedUnmarkedFrameworkRoutes, actualUnmarked);
    }
}
