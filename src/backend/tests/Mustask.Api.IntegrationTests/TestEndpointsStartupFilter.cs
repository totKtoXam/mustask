using Mustask.Api.Authorization;
using Mustask.Application.Abstractions;
using Mustask.Shared.Errors;
using Mustask.Shared.Exceptions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Mustask.Api.IntegrationTests;

/// <summary>
/// Тестовые endpoint'ы для проверки глобальной обработки ошибок:
/// <c>/__test/throw/{kind}</c> бросает конкретный наследник
/// <see cref="MustaskException"/> (или непредвиденное исключение), <c>/__test/cancel</c>
/// ждёт отмены запроса клиентом. Подмешивается только в тестовый хост через
/// <see cref="Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory{TEntryPoint}.ConfigureWebHost"/>
/// → <c>ConfigureTestServices</c> — продовый код Mustask.Api тестовыми маршрутами
/// не засоряется.
///
/// Deny-all: <c>/__test/throw/*</c> и
/// <c>/__test/cancel</c> размечены <c>AllowAnonymous()</c> явно — они
/// проверяют маппинг ошибок, а не авторизацию, и обязаны оставаться
/// достижимыми анонимно, как и до введения fallback-policy. Инвентаризационный
/// тест (`EndpointCategoryInventoryTests`) не считает эти endpoint'ы частью
/// реального API и исключает весь префикс `/__test/` явным allow-list'ом —
/// они существуют только в тестовом хосте. Отдельный <c>/__test/deny-all-probe</c>
/// НАМЕРЕННО не размечен вовсе — единственный endpoint, которым рантайм-тест
/// deny-all проверяет само поведение fallback-policy на живом HTTP-конвейере.
/// </summary>
/// <remarks>
/// <c>/__test/cancel</c> заворачивает <see cref="OperationCanceledException"/>
/// в <see cref="InvalidOperationException"/> (имитация
/// NpgsqlException/DbUpdateException) осознанно: сам
/// ASP.NET Core (<c>ExceptionHandlerMiddlewareImpl.HandleException</c>)
/// перехватывает голый <see cref="OperationCanceledException"/> при
/// <c>HttpContext.RequestAborted.IsCancellationRequested</c> ДО вызова
/// зарегистрированных <c>IExceptionHandler</c>'ов и сам выставляет 499 —
/// наш <see cref="Mustask.Api.ErrorHandling.OperationCanceledExceptionHandler"/> в
/// этом случае вообще не вызывается.
/// Единственный путь, которым реально можно пронаблюдать наш обработчик
/// через настоящий HTTP-конвейер — обёрнутое исключение, для которого
/// фреймворк это короткое замыкание не делает (проверяет только точный тип,
/// не идёт по <c>InnerException</c>).
/// </remarks>
public sealed class TestEndpointsStartupFilter : IStartupFilter
{
    /// <summary>
    /// Флаг «обработчик выполнялся» для рантайм-теста deny-all: тест
    /// сбрасывает его перед запросом и проверяет, что после 401/403 он
    /// остался <c>false</c> — авторизация отсекла запрос ДО обработчика.
    /// </summary>
    public static bool DenyAllProbeHandlerExecuted { get; set; }

    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)
    {
        return app =>
        {
            next(app);

            // НЕ вызываем app.UseRouting() здесь
            // повторно. Раньше повторный вызов создавал ВТОРОЙ,
            // независимый EndpointRouteBuilder (свой набор DataSources), не
            // видимый первому (неявному, вставленному WebApplication перед
            // UseAuthorization) проходу маршрутизации — из-за этого
            // UseAuthorization решал судьбу запроса к /__test/* по
            // NULL-endpoint'у (deny-all fallback, а не по метаданным
            // AllowAnonymous, которые вешались на endpoint из ВТОРОГО,
            // недостижимого для авторизации набора). Program.cs теперь
            // всегда мапит endpoint'ы (например, /api/dictionaries/*) до
            // app.Run(), поэтому WebApplication уже вставил UseRouting
            // неявно к этому моменту — переиспользуем его через
            // app.UseEndpoints(...), которое находит существующий
            // EndpointRouteBuilder по тому же свойству IApplicationBuilder,
            // что использовала неявная вставка.
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapGet("/__test/throw/{kind}", (string kind) => Throw(kind)).AllowAnonymous();

                // Устанавливает произвольный статус без бизнес-исключения —
                // проверяет код-«последнюю надежду» (PlatformErrorCodes.FallbackCode)
                // для статусов без собственной записи в contracts/errors/http.json
                // (тест на 415).
                // AllowAnonymous — как и у соседних /__test/*: без него deny-all
                // закрывает endpoint и тест получает 401 вместо проверяемого
                // статуса.
                endpoints.MapGet("/__test/status/{code:int}", (int code, HttpContext context) =>
                {
                    context.Response.StatusCode = code;
                    return Results.Empty;
                }).AllowAnonymous();

                endpoints.MapGet("/__test/cancel", async (HttpContext context) =>
                {
                    try
                    {
                        await Task.Delay(Timeout.InfiniteTimeSpan, context.RequestAborted);
                    }
                    catch (OperationCanceledException oce)
                    {
                        // См. <remarks> класса: заворачиваем, чтобы дойти до
                        // OperationCanceledExceptionHandler, а не до
                        // встроенного короткого замыкания ASP.NET Core.
                        throw new InvalidOperationException(
                            "Имитация NpgsqlException/DbUpdateException, завернувшего отмену клиентом.",
                            oce);
                    }
                }).AllowAnonymous();

                // Намеренно БЕЗ .AllowAnonymous()/.RequireAuthorization(...) —
                // единственный endpoint, проверяющий deny-all fallback-policy
                // на живом HTTP-конвейере.
                endpoints.MapGet("/__test/deny-all-probe", () =>
                {
                    DenyAllProbeHandlerExecuted = true;
                    return Results.Ok();
                });

                // Диагностика ICurrentUser —
                // не часть продуктового API, поэтому не через
                // Mustask.Api.Authorization.EndpointCategories (та разметка
                // питает EndpointCategoryInventoryTests, которая весь
                // префикс /__test/* исключает явным allow-list'ом). Аутентифицированный
                // вариант проверяет name → preferred_username, публичный —
                // константу anonymous («запрос без пользователя»).
                endpoints.MapGet("/__test/current-user", (ICurrentUser currentUser) =>
                    Results.Ok(new { displayName = currentUser.DisplayName }))
                    .RequireAuthorization(PermissionPolicies.AuthenticatedOnlyPolicyName);

                endpoints.MapGet("/__test/current-user-public", (ICurrentUser currentUser) =>
                    Results.Ok(new { displayName = currentUser.DisplayName }))
                    .AllowAnonymous();
            });
        };
    }

    private static IResult Throw(string kind) => kind switch
    {
        "notfound" => throw new NotFoundException("test.not_found"),
        "conflict" => throw new ConflictException("test.conflict"),
        // Отдельный код (не переиспользует "conflict") — нужен ответ,
        // фактически несущий params, для тестов на реальном
        // HTTP-конвейере (ErrorHandlingTests).
        "conflict_with_params" => throw new ConflictException("test.conflict_with_params", new ErrorParams
        {
            ["used"] = ErrorParam.Number(50m),
            ["limit"] = ErrorParam.Number(50m),
        }),
        // Код, которого нет ни в ErrorCatalogTestData, ни в contracts/errors/:
        // воспроизводит путь "код объявлен не константой под [ErrorCodes]"
        // (enforcement проверяется отдельно) — тест проверяет, что сам путь
        // ошибки при этом не падает непредсказуемо, а не что такой
        // код запрещён на входе.
        "unknown_code" => throw new ConflictException("test.code_not_in_catalog"),
        // Контракт обещает "date" как JSON-строку
        // "YYYY-MM-DD" — до этого теста ни один автотест не проверял
        // фактический вид даты в реальном HTTP-ответе (только рендер текста
        // и конструирование ErrorParam на уровне юнит-тестов).
        "date_param" => throw new ConflictException("test.date_param", new ErrorParams
        {
            ["date"] = ErrorParam.Date(new DateOnly(2026, 1, 12)),
        }),
        "forbidden" => throw new ForbiddenException("test.forbidden"),
        "unauthorized" => throw new UnauthorizedException("test.unauthorized"),
        "validation" => throw new ValidationException(
            "test.validation_failed",
            new Dictionary<string, IReadOnlyList<FieldError>> { ["field"] = [new FieldError("test.field_required")] }),
        "unexpected" => throw new InvalidOperationException(
            "Секретная деталь непредвиденного исключения, которая не должна попасть клиенту."),
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Неизвестный вид тестовой ошибки."),
    };
}
