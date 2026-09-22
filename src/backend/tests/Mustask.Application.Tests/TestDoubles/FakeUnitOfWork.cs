using Mustask.Application.Abstractions;

namespace Mustask.Application.Tests.TestDoubles;

/// <summary>
/// В unit-тестах не нужна реальная транзакция — просто выполняет action.
/// </summary>
public sealed class FakeUnitOfWork : IUnitOfWork
{
    public int SaveChangesCallCount { get; private set; }

    public Task<TResult> ExecuteInTransactionAsync<TResult>(
        Func<CancellationToken, Task<TResult>> action,
        CancellationToken cancellationToken) => action(cancellationToken);

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        SaveChangesCallCount++;
        return Task.CompletedTask;
    }
}
