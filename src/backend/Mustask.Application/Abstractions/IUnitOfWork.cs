namespace Mustask.Application.Abstractions;

/// <summary>
/// Транзакционная граница use case'а. <see cref="ExecuteInTransactionAsync{T}"/> —
/// READ COMMITTED (уровень по умолчанию) + вызывающий код сам берёт
/// пессимистичные блокировки, где нужно (`SELECT … FOR UPDATE`).
/// </summary>
public interface IUnitOfWork
{
    Task<TResult> ExecuteInTransactionAsync<TResult>(
        Func<CancellationToken, Task<TResult>> action,
        CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
