using Mustask.Application.Abstractions.Process;
using Mustask.Domain.Process;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Mustask.Infrastructure.Process;

/// <summary>
/// The storage half of the seed (status.md D-10): idempotent by
/// insert-on-conflict-do-nothing then read, inside a savepoint of the caller's
/// transaction. It runs on the scoped <see cref="MustaskDbContext"/>, the same
/// instance the caller's <see cref="UnitOfWork"/> holds, and never begins, commits
/// or rolls back that transaction itself.
/// </summary>
/// <remarks>
/// <para>
/// <b>Races (BR-M2-9).</b> Under READ COMMITTED, the level <see cref="UnitOfWork"/>
/// opens, a second insert for the same organization blocks on the unique index
/// until the first transaction ends. If the first commits, the second does
/// nothing and the read after it (a new statement, so a new snapshot) returns
/// the winner's identity. If the first rolls back, the second inserts. No call
/// ever sees a half-built process.
/// </para>
/// <para>
/// <b>Why a savepoint.</b> A failure in the middle of the seed must leave nothing
/// of the process behind, whatever the caller does next (a caller that catches
/// the exception and commits anyway included). Rolling back to the savepoint
/// removes the root row and every child in one step, and leaves the caller's
/// transaction usable.
/// </para>
/// </remarks>
public sealed class ProcessSeedStore(MustaskDbContext context) : IProcessSeedStore
{
    private const string SavepointName = "process_seed";

    public async Task<ProcessId> InsertIfAbsentAsync(ProcessDefinition candidate, CancellationToken cancellationToken)
    {
        // 1. Preconditions, checked before any write.
        var transaction = context.Database.CurrentTransaction
            ?? throw new InvalidOperationException(
                "The process seed must run inside IUnitOfWork.ExecuteInTransactionAsync: without an ambient "
                + "transaction the root and its children would commit separately, and a failure midway would leave "
                + "a root with no children that idempotency would hand back forever.");

        if (context.ChangeTracker.HasChanges())
        {
            throw new InvalidOperationException(
                "The process seed must run before the caller tracks any change of its own: its flush would write "
                + "the caller's pending change inside the seed's savepoint, and a failure of that change would "
                + "surface as a failed seed.");
        }

        // 2. A savepoint, not a transaction: the caller's one stays the caller's.
        await transaction.CreateSavepointAsync(SavepointName, cancellationToken);

        try
        {
            // 3. The root, by raw SQL, because LINQ cannot say ON CONFLICT. Parameterized
            // (interpolation = parameters). The unique index on organization_id is the target.
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"""
                INSERT INTO processes (id, organization_id)
                VALUES ({candidate.Id.Value}, {candidate.OrganizationId})
                ON CONFLICT (organization_id) DO NOTHING
                """,
                cancellationToken);

            // 4. Whose is the row now: ours, an earlier one's, or a racing call's that committed first.
            var storedId = await context.Processes
                .AsNoTracking()
                .Where(process => process.OrganizationId == candidate.OrganizationId)
                .Select(process => process.Id)
                .SingleAsync(cancellationToken);

            // 5. Already existed: nothing is created and nothing is tracked.
            if (storedId != candidate.Id)
            {
                await transaction.ReleaseSavepointAsync(SavepointName, cancellationToken);
                return storedId;
            }

            // 6. Ours: add the levels and the types (the states follow through the types). The
            // root is never tracked, because step 3 inserted it and no child navigates back to it.
            // EF orders the inserts by the model's foreign keys.
            context.BacklogLevels.AddRange(candidate.BacklogLevels);
            context.WorkItemTypes.AddRange(candidate.WorkItemTypes);
            await context.SaveChangesAsync(cancellationToken);

            await transaction.ReleaseSavepointAsync(SavepointName, cancellationToken);
            return candidate.Id;
        }
        catch
        {
            // 7. On any exception, cancellation included: nothing of the process remains.
            await AbandonAsync(transaction, candidate);
            throw;
        }
    }

    /// <summary>
    /// Rolls back to the savepoint and detaches every entity of the candidate. The
    /// rollback runs with no cancellation token, so a cancelled call still cleans
    /// up. A failure of the rollback itself never masks the original exception: the
    /// caller abandoning its unit of work is the second line of defence, not the
    /// only one.
    /// </summary>
    private async Task AbandonAsync(IDbContextTransaction transaction, ProcessDefinition candidate)
    {
        try
        {
            await transaction.RollbackToSavepointAsync(SavepointName, CancellationToken.None);
        }
        catch (Exception)
        {
            // Deliberately swallowed, for the reason above.
        }

        var candidateEntities = new HashSet<object>(ReferenceEqualityComparer.Instance);
        candidateEntities.UnionWith(candidate.BacklogLevels);
        candidateEntities.UnionWith(candidate.WorkItemTypes);
        candidateEntities.UnionWith(candidate.WorkItemTypes.SelectMany(type => type.States));

        foreach (var entry in context.ChangeTracker.Entries().Where(entry => candidateEntities.Contains(entry.Entity)).ToList())
        {
            entry.State = EntityState.Detached;
        }
    }
}
