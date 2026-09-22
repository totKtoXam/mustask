using System.Data;
using Mustask.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Mustask.Infrastructure;

/// <summary>
/// READ COMMITTED (уровень по умолчанию Postgres/EF) + пессимистичная
/// блокировка строки, которую берёт вызывающий код (`SELECT … FOR UPDATE`).
/// </summary>
public sealed class UnitOfWork(MustaskDbContext context) : IUnitOfWork
{
    public async Task<TResult> ExecuteInTransactionAsync<TResult>(
        Func<CancellationToken, Task<TResult>> action,
        CancellationToken cancellationToken)
    {
        var strategy = context.Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async () =>
        {
            // EnableRetryOnFailure может выполнить
            // action ПОВТОРНО после транзиентного сбоя — откат транзакции не
            // откатывает ChangeTracker. Без очистки повтор увидел бы через
            // identity map уже изменённые в памяти (первой, неудавшейся,
            // попыткой) сущности вместо свежих из БД и мог бы молча уйти по
            // неверной ветке (решение по полю, уже проставленному в памяти
            // первой попыткой, а не по значению из БД).
            context.ChangeTracker.Clear();

            await using var transaction = await context.Database
                .BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);

            var result = await action(cancellationToken);
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return result;
        });
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        context.SaveChangesAsync(cancellationToken);
}
