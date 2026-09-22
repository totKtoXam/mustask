using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Mustask.Api.Authentication.Readiness;

/// <summary>
/// 503 до аутентификации, пока realm не готов. Стоит
/// строго между <c>UseUnmatchedRouteAsNotFound()</c> и
/// <c>UseAuthentication()</c> — routing уже отработал
/// (<c>context.GetEndpoint()</c> доступен), но проверка токена ещё не
/// начиналась, поэтому клиент не путает «сервис не готов» с «сессия
/// истекла». Пропускает любой endpoint с <see cref="IAllowAnonymous"/> —
/// это единственный признак, которым сам <c>UseAuthorization</c> отличает
/// открытый маршрут, и он покрывает и нашу категорию <c>Public</c>, и
/// health-checks, и explorer. Никакого исключения по префиксу пути гейт не
/// делает.
/// </summary>
public static class RealmReadinessGateMiddleware
{
    /// <summary>Заголовок ответа при 503.</summary>
    private const int RetryAfterSeconds = 30;

    public static IApplicationBuilder UseRealmReadinessGate(this IApplicationBuilder app)
    {
        return app.Use(static async (context, next) =>
        {
            var endpoint = context.GetEndpoint();

            if (endpoint?.Metadata.GetMetadata<IAllowAnonymous>() is not null)
            {
                await next(context);
                return;
            }

            var readiness = context.RequestServices.GetRequiredService<RealmReadiness>();

            if (readiness.Current.IsReady)
            {
                await next(context);
                return;
            }

            // Пустое тело — UseStatusCodePages() (стоит раньше в конвейере)
            // превращает его в application/problem+json с errorCode
            // http.service_unavailable.
            context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
            context.Response.Headers.RetryAfter = RetryAfterSeconds.ToString(CultureInfo.InvariantCulture);
        });
    }
}
