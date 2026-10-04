using Mustask.Application.Abstractions;
using Mustask.Application.Features.Process;
using Mustask.Domain.Dictionaries;
using Mustask.Domain.Process;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Mustask.Infrastructure.Tests.Process;

/// <summary>
/// US-1 (mod-001-process-seed), against a real PostgreSQL: the seed seam as
/// <c>mod-002</c> will call it, <see cref="IProcessSeed"/> inside
/// <see cref="IUnitOfWork"/>, both resolved from one scope. Every test seeds its
/// own fresh organization identifier, so no test depends on another's rows.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(ProcessDatabaseCollection.Name)]
public sealed class ProcessSeedTests(ProcessDatabaseFixture database)
{
    private static IReadOnlyList<string> Blueprint() =>
        ProcessDatabaseFixture.Describe(ProcessDefinition.Seed(ProcessId.New(), Guid.NewGuid()));

    private async Task AssertOnlyCompleteProcessesAsync()
    {
        var (processes, levels, types, states) = await database.CountRowsAsync();

        Assert.Equal(processes * 4, levels);
        Assert.Equal(processes * 5, types);
        Assert.Equal(processes * 24, states);
    }

    // US-1 · "A new organization identifier receives the complete seeded process".
    [Fact]
    public async Task SeedAsync_NewOrganization_PersistsOneCompleteProcessAndReturnsItsId()
    {
        var organizationId = Guid.NewGuid();
        await using var provider = database.BuildServices();

        var id = await ProcessDatabaseFixture.SeedInUnitOfWorkAsync(provider, organizationId);

        var stored = await database.ReadProcessAsync(organizationId);
        Assert.NotNull(stored);
        Assert.Equal(id, stored.Id);
        Assert.Equal(4, stored.BacklogLevels.Count);
        Assert.Equal(5, stored.WorkItemTypes.Count);
        Assert.Equal(24, stored.WorkItemTypes.Sum(type => type.States.Count));
        Assert.Equal(Blueprint(), ProcessDatabaseFixture.Describe(stored));

        var identities = ProcessDatabaseFixture.IdentitiesOf(stored);
        Assert.Equal(34, identities.Count);
        Assert.Equal(identities.Count, identities.Distinct().Count());
    }

    // US-1 · "Each organization identifier gets a process of its own".
    [Fact]
    public async Task SeedAsync_SecondOrganization_CreatesASeparateEqualProcess()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        await using var provider = database.BuildServices();

        var firstId = await ProcessDatabaseFixture.SeedInUnitOfWorkAsync(provider, first);
        var secondId = await ProcessDatabaseFixture.SeedInUnitOfWorkAsync(provider, second);

        var storedFirst = await database.ReadProcessAsync(first);
        var storedSecond = await database.ReadProcessAsync(second);
        Assert.NotEqual(firstId, secondId);
        Assert.Equal(ProcessDatabaseFixture.Describe(storedFirst!), ProcessDatabaseFixture.Describe(storedSecond!));
        Assert.Empty(ProcessDatabaseFixture.IdentitiesOf(storedFirst!).Intersect(ProcessDatabaseFixture.IdentitiesOf(storedSecond!)));
    }

    // US-1 · "A failed seed leaves nothing behind and says so": the storage fails at the last table, and the
    // caller catches the exception and commits anyway. Nothing of the process remains.
    [Fact]
    public async Task SeedAsync_StorageFailsMidway_ThrowsAndLeavesNoRowsEvenIfCallerCommits()
    {
        var organizationId = Guid.NewGuid();
        var failing = new FailingInsertInterceptor("state_definitions");
        await using var provider = database.BuildServices(failing);
        var before = await database.CountRowsAsync();

        await using (var scope = provider.CreateAsyncScope())
        {
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var seed = scope.ServiceProvider.GetRequiredService<IProcessSeed>();
            Exception? seen = null;

            await unitOfWork.ExecuteInTransactionAsync(
                async ct =>
                {
                    try
                    {
                        await seed.SeedAsync(organizationId, ct);
                    }
                    catch (Exception exception)
                    {
                        seen = exception;
                    }

                    return 0;
                },
                CancellationToken.None);

            Assert.NotNull(seen);
            Assert.IsType<InjectedStorageFailure>(seen.GetBaseException());
        }

        Assert.Null(await database.ReadProcessAsync(organizationId));
        Assert.Equal(before, await database.CountRowsAsync());
        await AssertOnlyCompleteProcessesAsync();
    }

    // US-1 · "...a later call with the same identifier creates the complete process".
    [Fact]
    public async Task SeedAsync_AfterAFailedAttempt_CreatesTheCompleteProcess()
    {
        var organizationId = Guid.NewGuid();
        var failing = new FailingInsertInterceptor("work_item_types");
        await using var provider = database.BuildServices(failing);

        var failure = await Assert.ThrowsAnyAsync<Exception>(() => ProcessDatabaseFixture.SeedInUnitOfWorkAsync(provider, organizationId));
        Assert.IsType<InjectedStorageFailure>(failure.GetBaseException());
        Assert.Null(await database.ReadProcessAsync(organizationId));

        failing.Armed = false;
        var id = await ProcessDatabaseFixture.SeedInUnitOfWorkAsync(provider, organizationId);

        var stored = await database.ReadProcessAsync(organizationId);
        Assert.Equal(id, stored!.Id);
        Assert.Equal(Blueprint(), ProcessDatabaseFixture.Describe(stored));
        await AssertOnlyCompleteProcessesAsync();
    }

    // US-1 · "A malformed identifier is refused and nothing is created".
    [Fact]
    public async Task SeedAsync_EmptyOrganizationId_WritesNothing()
    {
        await using var provider = database.BuildServices();
        var before = await database.CountRowsAsync();

        await Assert.ThrowsAsync<ArgumentException>(() => ProcessDatabaseFixture.SeedInUnitOfWorkAsync(provider, Guid.Empty));

        Assert.Null(await database.ReadProcessAsync(Guid.Empty));
        Assert.Equal(before, await database.CountRowsAsync());
    }

    // US-1 · "A caller's failed operation takes the seed with it": the exception leaves ExecuteInTransactionAsync.
    [Fact]
    public async Task SeedAsync_CallerFailsAfterTheSeed_LeavesNoProcess()
    {
        var organizationId = Guid.NewGuid();
        await using var provider = database.BuildServices();
        var before = await database.CountRowsAsync();
        ProcessId? seeded = null;

        await using (var scope = provider.CreateAsyncScope())
        {
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var seed = scope.ServiceProvider.GetRequiredService<IProcessSeed>();

            await Assert.ThrowsAsync<InvalidOperationException>(() => unitOfWork.ExecuteInTransactionAsync<int>(
                async ct =>
                {
                    seeded = await seed.SeedAsync(organizationId, ct);
                    throw new InvalidOperationException("A later step of the caller's operation failed.");
                },
                CancellationToken.None));
        }

        Assert.NotNull(seeded);
        Assert.Null(await database.ReadProcessAsync(organizationId));
        Assert.Equal(before, await database.CountRowsAsync());
    }

    // US-1 · "A repeated call creates nothing and hands back the same process".
    [Fact]
    public async Task SeedAsync_SameOrganizationTwice_ReturnsTheSameIdAndCreatesNothing()
    {
        var organizationId = Guid.NewGuid();
        await using var provider = database.BuildServices();

        var first = await ProcessDatabaseFixture.SeedInUnitOfWorkAsync(provider, organizationId);
        var identitiesBefore = ProcessDatabaseFixture.IdentitiesOf((await database.ReadProcessAsync(organizationId))!);
        var rowsBefore = await database.CountRowsAsync();

        var second = await ProcessDatabaseFixture.SeedInUnitOfWorkAsync(provider, organizationId);

        Assert.Equal(first, second);
        Assert.Equal(rowsBefore, await database.CountRowsAsync());
        Assert.Equal(identitiesBefore, ProcessDatabaseFixture.IdentitiesOf((await database.ReadProcessAsync(organizationId))!));
    }

    // US-1 · "A repeated call ...", inside one unit of work: the rows of the first call are flushed, so the second is not refused.
    [Fact]
    public async Task SeedAsync_CalledTwiceInOneUnitOfWork_ReturnsTheSameId()
    {
        var organizationId = Guid.NewGuid();
        await using var provider = database.BuildServices();
        await using var scope = provider.CreateAsyncScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var seed = scope.ServiceProvider.GetRequiredService<IProcessSeed>();

        var ids = await unitOfWork.ExecuteInTransactionAsync(
            async ct => (First: await seed.SeedAsync(organizationId, ct), Second: await seed.SeedAsync(organizationId, ct)),
            CancellationToken.None);

        Assert.Equal(ids.First, ids.Second);
        Assert.Equal(Blueprint(), ProcessDatabaseFixture.Describe((await database.ReadProcessAsync(organizationId))!));
    }

    // US-1 · "Two racing calls leave exactly one process": call A holds its transaction open after seeding, call B
    // waits on the database, and A commits. B returns A's identity and creates nothing.
    [Fact]
    public async Task SeedAsync_TwoConcurrentCalls_LeaveExactlyOneProcess()
    {
        var organizationId = Guid.NewGuid();
        await using var provider = database.BuildServices();
        var aSeeded = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseA = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var callA = Task.Run(() => HoldTransactionAfterSeedAsync(provider, organizationId, aSeeded, releaseA, failAfterwards: false));
        await await Task.WhenAny(aSeeded.Task, callA);
        Assert.True(aSeeded.Task.IsCompleted, "Call A did not get to seed.");

        var callB = Task.Run(() => ProcessDatabaseFixture.SeedInUnitOfWorkAsync(provider, organizationId));
        await database.WaitForASessionBlockedOnALockAsync(callB);
        Assert.False(callB.IsCompleted, "Call B must wait for call A's transaction.");

        releaseA.SetResult();
        var idA = await callA;
        var idB = await callB;

        Assert.Equal(idA, idB);
        var stored = await database.ReadProcessAsync(organizationId);
        Assert.Equal(idA, stored!.Id);
        Assert.Equal(Blueprint(), ProcessDatabaseFixture.Describe(stored));
        await AssertOnlyCompleteProcessesAsync();
    }

    // US-1 · "Two racing calls ...": the rival rolls back, so the waiting call creates the process itself.
    [Fact]
    public async Task SeedAsync_RivalRollsBack_SecondCallCreatesTheProcess()
    {
        var organizationId = Guid.NewGuid();
        await using var provider = database.BuildServices();
        var aSeeded = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseA = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var callA = Task.Run(() => HoldTransactionAfterSeedAsync(provider, organizationId, aSeeded, releaseA, failAfterwards: true));
        await await Task.WhenAny(aSeeded.Task, callA);
        Assert.True(aSeeded.Task.IsCompleted, "Call A did not get to seed.");

        var callB = Task.Run(() => ProcessDatabaseFixture.SeedInUnitOfWorkAsync(provider, organizationId));
        await database.WaitForASessionBlockedOnALockAsync(callB);
        Assert.False(callB.IsCompleted, "Call B must wait for call A's transaction.");

        releaseA.SetResult();
        await Assert.ThrowsAsync<InvalidOperationException>(() => callA);
        var idB = await callB;

        var stored = await database.ReadProcessAsync(organizationId);
        Assert.Equal(idB, stored!.Id);
        Assert.Equal(Blueprint(), ProcessDatabaseFixture.Describe(stored));
        await AssertOnlyCompleteProcessesAsync();
    }

    // The seam's own guard (api-contract § 2, the caller's obligation 1), not a story scenario.
    [Fact]
    public async Task SeedAsync_WithoutAmbientTransaction_ThrowsAndWritesNothing()
    {
        var organizationId = Guid.NewGuid();
        await using var provider = database.BuildServices();
        var before = await database.CountRowsAsync();

        await using (var scope = provider.CreateAsyncScope())
        {
            var seed = scope.ServiceProvider.GetRequiredService<IProcessSeed>();

            await Assert.ThrowsAsync<InvalidOperationException>(() => seed.SeedAsync(organizationId, CancellationToken.None));
        }

        Assert.Null(await database.ReadProcessAsync(organizationId));
        Assert.Equal(before, await database.CountRowsAsync());
    }

    // The seam's own guard (api-contract § 2, the caller's obligation 2), not a story scenario.
    [Fact]
    public async Task SeedAsync_WithPendingCallerChanges_ThrowsAndWritesNothing()
    {
        var organizationId = Guid.NewGuid();
        await using var provider = database.BuildServices();
        var before = await database.CountRowsAsync();

        await using (var scope = provider.CreateAsyncScope())
        {
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var seed = scope.ServiceProvider.GetRequiredService<IProcessSeed>();
            var context = scope.ServiceProvider.GetRequiredService<MustaskDbContext>();

            await Assert.ThrowsAsync<InvalidOperationException>(() => unitOfWork.ExecuteInTransactionAsync(
                async ct =>
                {
                    context.Languages.Add(Language.Create(Guid.CreateVersion7(), "zz", null, 99, DateTimeOffset.UtcNow, "test"));

                    return await seed.SeedAsync(organizationId, ct);
                },
                CancellationToken.None));
        }

        Assert.Null(await database.ReadProcessAsync(organizationId));
        Assert.Equal(before, await database.CountRowsAsync());
        await using var check = database.NewContext();
        Assert.False(await check.Languages.AnyAsync(language => language.Code == "zz"));
    }

    // The cancellation half of "on any exception, cancellation included" (plan-backend.md § 5, step 7).
    [Fact]
    public async Task SeedAsync_CancelledMidway_LeavesNoRowsAndTheUnitOfWorkUsable()
    {
        var organizationId = Guid.NewGuid();
        using var cancellation = new CancellationTokenSource();
        var cancelling = new CancellingInsertInterceptor("state_definitions", cancellation);
        await using var provider = database.BuildServices(cancelling);
        var before = await database.CountRowsAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await using var scope = provider.CreateAsyncScope();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var seed = scope.ServiceProvider.GetRequiredService<IProcessSeed>();

            await unitOfWork.ExecuteInTransactionAsync(ct => seed.SeedAsync(organizationId, ct), cancellation.Token);
        });

        Assert.Null(await database.ReadProcessAsync(organizationId));
        Assert.Equal(before, await database.CountRowsAsync());
    }

    // QA pass 1 (qa.md A-12) · US-1 "A failed seed leaves nothing behind and says so", with the failure on the
    // database side: a trigger rejects the TASK type's «Удалена» row, so the root, the levels, the TASK type and
    // the states before it are already written in the database and the transaction is aborted when the error comes.
    // The interceptor tests above fail a statement before it is sent, which never aborts the transaction. Here the
    // caller swallows the exception and goes on: its own write must commit (the seed rolled back to its savepoint,
    // so the transaction is usable again) and nothing of the process may. Then a later call seeds the whole process.
    [Fact]
    public async Task SeedAsync_DatabaseRejectsALateRow_LeavesNoRowsAndTheCallersTransactionUsable()
    {
        var organizationId = Guid.NewGuid();
        var trigger = $"qa_reject_late_state_{organizationId:N}";
        const string callerLanguageCode = "qy";
        await using var provider = database.BuildServices();
        var before = await database.CountRowsAsync();

        await ExecuteSqlAsync(
            $"""
            CREATE FUNCTION {trigger}() RETURNS trigger LANGUAGE plpgsql AS $fn$
            BEGIN
                IF NEW.name = 'Удалена' AND EXISTS (
                    SELECT 1 FROM work_item_types t JOIN processes p ON p.id = t.process_id
                    WHERE t.id = NEW.work_item_type_id AND t.key = 'TASK' AND p.organization_id = '{organizationId}') THEN
                    RAISE EXCEPTION 'qa: the database rejected a late row of the seed';
                END IF;
                RETURN NEW;
            END
            $fn$;
            CREATE TRIGGER {trigger} BEFORE INSERT ON state_definitions FOR EACH ROW EXECUTE FUNCTION {trigger}();
            """);

        try
        {
            Exception? seen = null;

            await using (var scope = provider.CreateAsyncScope())
            {
                var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
                var seed = scope.ServiceProvider.GetRequiredService<IProcessSeed>();
                var context = scope.ServiceProvider.GetRequiredService<MustaskDbContext>();

                await unitOfWork.ExecuteInTransactionAsync(
                    async ct =>
                    {
                        try
                        {
                            await seed.SeedAsync(organizationId, ct);
                        }
                        catch (Exception exception)
                        {
                            seen = exception;
                        }

                        // The caller's own step after the failed seed, in the same transaction.
                        context.Languages.Add(Language.Create(Guid.CreateVersion7(), callerLanguageCode, null, 98, DateTimeOffset.UtcNow, "qa"));

                        return 0;
                    },
                    CancellationToken.None);
            }

            // The trigger only fires once the root and the TASK type are rows of the database: the failure is partway.
            var rejected = Assert.IsType<Npgsql.PostgresException>(seen?.GetBaseException());
            Assert.Contains("qa: the database rejected a late row of the seed", rejected.MessageText, StringComparison.Ordinal);

            Assert.Null(await database.ReadProcessAsync(organizationId));
            Assert.Equal(before, await database.CountRowsAsync());

            await using (var check = database.NewContext())
            {
                Assert.True(
                    await check.Languages.AnyAsync(language => language.Code == callerLanguageCode),
                    "The caller's own write after the failed seed did not commit: the seed left the transaction unusable.");
            }
        }
        finally
        {
            await ExecuteSqlAsync($"DROP TRIGGER IF EXISTS {trigger} ON state_definitions; DROP FUNCTION IF EXISTS {trigger}();");
            await ExecuteSqlAsync($"DELETE FROM languages WHERE code = '{callerLanguageCode}';");
        }

        var id = await ProcessDatabaseFixture.SeedInUnitOfWorkAsync(provider, organizationId);

        var stored = await database.ReadProcessAsync(organizationId);
        Assert.Equal(id, stored!.Id);
        Assert.Equal(Blueprint(), ProcessDatabaseFixture.Describe(stored));
        await AssertOnlyCompleteProcessesAsync();
    }

    // QA pass 1 · US-1 "Two racing calls leave exactly one process", without orchestration: eight calls released at
    // once. api-contract § 2 promises that every later call waits and then returns the winner's identity, so none fails.
    [Fact]
    public async Task SeedAsync_EightCallsReleasedAtOnce_LeaveOneProcessAndAllReturnItsId()
    {
        var organizationId = Guid.NewGuid();
        await using var provider = database.BuildServices();
        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var calls = Enumerable.Range(0, 8)
            .Select(_ => Task.Run(async () =>
            {
                await start.Task;

                return await ProcessDatabaseFixture.SeedInUnitOfWorkAsync(provider, organizationId);
            }))
            .ToList();

        start.SetResult();
        var ids = await Task.WhenAll(calls);

        Assert.Single(ids.Distinct());
        var stored = await database.ReadProcessAsync(organizationId);
        Assert.Equal(ids[0], stored!.Id);
        Assert.Equal(Blueprint(), ProcessDatabaseFixture.Describe(stored));
        await AssertOnlyCompleteProcessesAsync();
    }

    private async Task ExecuteSqlAsync(string sql)
    {
        await using var connection = new Npgsql.NpgsqlConnection(database.ConnectionString);
        await connection.OpenAsync();
        await using var command = new Npgsql.NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<ProcessId> HoldTransactionAfterSeedAsync(
        IServiceProvider provider,
        Guid organizationId,
        TaskCompletionSource seeded,
        TaskCompletionSource release,
        bool failAfterwards)
    {
        await using var scope = provider.CreateAsyncScope();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        var seed = scope.ServiceProvider.GetRequiredService<IProcessSeed>();

        return await unitOfWork.ExecuteInTransactionAsync(
            async ct =>
            {
                var id = await seed.SeedAsync(organizationId, ct);
                seeded.SetResult();
                await release.Task;

                return failAfterwards
                    ? throw new InvalidOperationException("The rival's operation failed after the seed.")
                    : id;
            },
            CancellationToken.None);
    }
}
