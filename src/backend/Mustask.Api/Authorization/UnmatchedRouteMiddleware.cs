using Microsoft.AspNetCore.Routing;

namespace Mustask.Api.Authorization;

/// <summary>
/// Deny-all fallback-политика
/// (<see cref="JwtBearerSetup"/> ниже по пайплайну) применяется ASP.NET Core
/// не только к endpoint'ам без явной разметки, но и к запросам, для которых
/// маршрутизация не нашла НАШ endpoint — это касается двух разных случаев,
/// проверенных эмпирически: (1) полностью неизвестный маршрут —
/// <c>context.GetEndpoint()</c> остаётся <c>null</c>; (2) известный маршрут
/// с неверным методом — routing сам подставляет служебный endpoint routing'а
/// (тип <see cref="Microsoft.AspNetCore.Http.Endpoint"/>, НЕ
/// <see cref="RouteEndpoint"/>, без единой метаданной, <c>DisplayName</c>
/// "405 HTTP Method Not Supported"), чей <c>RequestDelegate</c> и должен был
/// выставить 405, но раньше него срабатывает fallback-политика (оба случая —
/// официально задокументированное поведение <c>SetFallbackPolicy</c>, не
/// дефект authorization middleware). Без этой прослойки оба случая коротко
/// замыкаются на Challenge/Forbid (401/403) вместо 404/405 — платформенная
/// регрессия для всего API, а не только для этой фичи (deny-all касается
/// endpoint'ов приложения, а не отсутствующих маршрутов).
/// </summary>
public static class UnmatchedRouteMiddleware
{
    /// <summary>
    /// Должна стоять СТРОГО между разрешением маршрута (routing уже
    /// отработал к этому месту — Program.cs полагается на неявную вставку
    /// <c>UseRouting()</c> перед первым middleware, которому нужны
    /// endpoint'ы) и <c>UseAuthentication()</c>/<c>UseAuthorization()</c>.
    /// Настоящие endpoint'ы приложения (минимальные API, health checks,
    /// OpenAPI) всегда регистрируются как <see cref="RouteEndpoint"/> —
    /// только для них выполняется обычный
    /// аутентификация → авторизация (включая deny-all) → обработчик.
    /// Во всех остальных случаях запрос короткозамыкается ЗДЕСЬ:
    /// аутентификация и авторизация не выполняются вовсе, поэтому deny-all
    /// им нечего запрещать. Итоговый статус (404 на неизвестный маршрут либо
    /// 405, выставленный запуском служебного <c>RequestDelegate</c> самого
    /// routing'а на известном маршруте с неверным методом) долетает без
    /// изменений до <c>UseStatusCodePages()</c> (стоит раньше в пайплайне и
    /// оборачивает всё нижестоящее), которая, как и до введения deny-all,
    /// превращает его в <c>application/problem+json</c> с прежними
    /// <c>errorCode</c>/<c>title</c>.
    /// </summary>
    public static IApplicationBuilder UseUnmatchedRouteAsNotFound(this IApplicationBuilder app)
    {
        return app.Use(static async (context, next) =>
        {
            var endpoint = context.GetEndpoint();

            if (endpoint is RouteEndpoint)
            {
                await next(context);
                return;
            }

            if (endpoint?.RequestDelegate is { } requestDelegate)
            {
                // Служебный endpoint самого routing'а (например, "405 HTTP
                // Method Not Supported" от HttpMethodMatcherPolicy) — не наш,
                // метаданных авторизации/категории у него нет и быть не
                // может. Выполняем его RequestDelegate напрямую (сам
                // выставляет статус и заголовок Allow), минуя
                // аутентификацию/авторизацию.
                await requestDelegate(context);
                return;
            }

            // Endpoint не найден вовсе (неизвестный маршрут). Трогаем
            // статус, только если он ещё не отклонился от дефолтного 200 —
            // на случай, если когда-нибудь появится другой служебный
            // endpoint без RequestDelegate, но с уже выставленным статусом.
            if (context.Response.StatusCode == StatusCodes.Status200OK)
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
            }
        });
    }
}
