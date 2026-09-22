using Mustask.Api.ErrorHandling;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Mustask.Api.IntegrationTests;

/// <summary>
/// Unit-уровневые тесты над <see cref="OperationCanceledExceptionHandler"/> в
/// изоляции от HTTP-конвейера: настоящую отмену запроса клиентом невозможно
/// пронаблюдать через <c>HttpClient</c> end-to-end (клиент, отвалившийся по
/// определению, не читает ответ) — поэтому <c>HttpContext.RequestAborted</c>
/// подменяется напрямую.
/// </summary>
[Trait("Category", "Integration")]
public sealed class OperationCanceledExceptionHandlerTests
{
    [Fact]
    public async Task TryHandleAsync_RequestAbortedAndOperationCanceledException_Returns499AndLogsInformation()
    {
        var logger = new CapturingLogger<OperationCanceledExceptionHandler>();
        var handler = new OperationCanceledExceptionHandler(logger);
        var context = new DefaultHttpContext();
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        context.RequestAborted = cts.Token;

        var handled = await handler.TryHandleAsync(context, new OperationCanceledException(), CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(499, context.Response.StatusCode);
        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Information, entry.Level);
    }

    [Fact]
    public async Task TryHandleAsync_RequestNotAborted_ReturnsFalseRegardlessOfExceptionType()
    {
        var handler = new OperationCanceledExceptionHandler(new CapturingLogger<OperationCanceledExceptionHandler>());
        var context = new DefaultHttpContext();

        var handled = await handler.TryHandleAsync(context, new OperationCanceledException(), CancellationToken.None);

        Assert.False(handled);
    }

    /// <summary>
    /// Исправленный дефект: обработчик раньше решал только по
    /// <c>HttpContext.RequestAborted</c>, не проверяя тип исключения. Если
    /// запрос клиента отменён одновременно с реальным дефектом (например,
    /// NullReferenceException где-то в той же обработке), он тоже
    /// поглощался как «клиент отвалился» — Information/499 вместо
    /// Error/500 через GlobalExceptionHandler, алерт терялся.
    /// </summary>
    [Fact]
    public async Task TryHandleAsync_RequestAbortedButUnrelatedException_DoesNotSwallowRealBugAs499()
    {
        var handler = new OperationCanceledExceptionHandler(new CapturingLogger<OperationCanceledExceptionHandler>());
        var context = new DefaultHttpContext();
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        context.RequestAborted = cts.Token;

        var handled = await handler.TryHandleAsync(
            context,
            new InvalidOperationException("настоящий баг, не связанный с отменой запроса"),
            CancellationToken.None);

        Assert.False(handled);
    }

    /// <summary>
    /// Зеркало дефекта выше: реальный обрыв клиента, прошедший
    /// через EF Core/Npgsql, часто приходит не голым
    /// <see cref="OperationCanceledException"/>, а завёрнутым в исключение
    /// драйвера/ORM. Раньше такой случай не распознавался и уходил в
    /// <c>GlobalExceptionHandler</c> как 500/Error — шум, зеркальный дефекту выше.
    /// </summary>
    [Fact]
    public async Task TryHandleAsync_RequestAbortedAndOperationCanceledExceptionWrappedInOtherException_Returns499()
    {
        var logger = new CapturingLogger<OperationCanceledExceptionHandler>();
        var handler = new OperationCanceledExceptionHandler(logger);
        var context = new DefaultHttpContext();
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        context.RequestAborted = cts.Token;

        var wrapped = new InvalidOperationException(
            "имитация NpgsqlException/DbUpdateException, завёрнувшего отмену клиентом",
            new OperationCanceledException());

        var handled = await handler.TryHandleAsync(context, wrapped, CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(499, context.Response.StatusCode);
    }
}
