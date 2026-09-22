using Microsoft.AspNetCore.Diagnostics;

namespace Mustask.Api.ErrorHandling;

/// <summary>
/// Первый обработчик в цепочке: отличает отвалившегося клиента (запрос
/// отменён) от настоящего дефекта. Без него каждый отменённый запрос шумел
/// бы в логах как 500.
/// </summary>
/// <remarks>
/// Отмену распознаёт и тогда, когда <see cref="OperationCanceledException"/>
/// приходит не напрямую, а завёрнутой в исключение драйвера/ORM (например,
/// <c>NpgsqlException</c>/<c>DbUpdateException</c> при отмене клиентом
/// запроса к БД) — <see cref="Exception.GetBaseException"/> идёт по цепочке
/// <see cref="Exception.InnerException"/> до самого внутреннего исключения.
/// Границу применимости стоит
/// пересмотреть на первом слайсе с реальными запросами к БД.
/// </remarks>
public sealed class OperationCanceledExceptionHandler : IExceptionHandler
{
    private readonly ILogger<OperationCanceledExceptionHandler> _logger;

    public OperationCanceledExceptionHandler(ILogger<OperationCanceledExceptionHandler> logger)
    {
        _logger = logger;
    }

    public ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception.GetBaseException() is not OperationCanceledException || !httpContext.RequestAborted.IsCancellationRequested)
        {
            return ValueTask.FromResult(false);
        }

        _logger.LogInformation(
            "Client disconnected for {Method} {Path}",
            httpContext.Request.Method,
            httpContext.Request.Path);

        httpContext.Response.StatusCode = 499;

        return ValueTask.FromResult(true);
    }
}
