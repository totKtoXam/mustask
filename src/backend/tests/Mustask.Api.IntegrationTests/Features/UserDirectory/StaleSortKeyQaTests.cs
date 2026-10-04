using Mustask.Application.Abstractions.UserDirectory;
using Mustask.Application.Features.UserDirectory.CompleteSnapshot;
using Mustask.Domain.UserDirectory;
using Mustask.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static Mustask.Api.IntegrationTests.Features.UserDirectory.UserDirectoryTestData;

namespace Mustask.Api.IntegrationTests.Features.UserDirectory;

/// <summary>
/// mod-014, tests added at QA re-check 2 (qa.md, "Re-check 2", RC2-19..RC2-22): what a snapshot's completion does
/// since code review F5 — it recomputes up to <see cref="CompleteSnapshotHandler.StaleSortKeysPerCompletion"/> sort
/// keys of another collation version, in the same lock call as the absence candidates. The developer's test covers one
/// reconciliation over three users; these cover the bound, an interrupted snapshot, a stale key that is also an absence
/// candidate (the same id in both sets) and the completion beside concurrent pushes for the same users.
/// </summary>
[Collection(MustaskApiCollection.Name)]
public sealed class StaleSortKeyQaTests(MustaskApiFactory factory) : IAsyncLifetime
{
    private readonly PushTestClient _push = new(factory);

    public Task InitializeAsync() => ResetAsync(factory);

    public Task DisposeAsync() => ResetAsync(factory);

    /// <summary>As if every row had been written by an API host with another ICU.</summary>
    private async Task MakeEveryKeyStaleAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<MustaskDbContext>();

        await context.Database.ExecuteSqlRawAsync("UPDATE directory_users SET sort_key_version = 'ru/another-icu', sort_key = '\\x00'::bytea");
    }

    private async Task<int> StaleCountAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<MustaskDbContext>();

        return await context.DirectoryUsers.CountAsync(user => user.SortKeyVersion != DirectoryCollation.Version);
    }

    // RC2-20: the bound. 1 001 deleted users hold a stale key and no page will ever rewrite them: one completion
    // recomputes 1 000 of them (and says nothing wrong about the 1 001st), the next one the rest.
    [Fact]
    public async Task Completion_RecomputesAtMostTheBound_andTheNextCompletionTheRest()
    {
        var bound = CompleteSnapshotHandler.StaleSortKeysPerCompletion;
        Assert.Equal(1_000, bound);
        var deleted = Enumerable.Range(0, bound + 1)
            .Select(i => Push($"gone-{i:D4}-{NewId()}", "Удалён", $"Номер{i:D4}", null, enabled: false, deleted: true))
            .ToList();

        foreach (var chunk in deleted.Chunk(500))
        {
            await _push.PostChangesAsync(chunk);
        }

        var present = Push(NewId(), "Анна", "Живая", null, moment: BaseMoment + 1_001);
        await MakeEveryKeyStaleAsync();
        Assert.Equal(bound + 1, await StaleCountAsync());

        await _push.ReconcileAsync(BaseMoment + 1_000, [present]);
        Assert.Equal(1, await StaleCountAsync());

        await _push.ReconcileAsync(BaseMoment + 2_000, [present with { Moment = BaseMoment + 2_001 }]);
        Assert.Equal(0, await StaleCountAsync());

        // Nothing but keys: every deleted user is still deleted, with the moment it had.
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<MustaskDbContext>();
        Assert.Equal(bound + 1, await context.DirectoryUsers.CountAsync(user => user.Lifecycle == ProjectedUserLifecycle.Deleted));
        Assert.Equal(0, await context.DirectoryUsers.CountAsync(user => user.Lifecycle == ProjectedUserLifecycle.Deleted && user.Moment != BaseMoment));
    }

    // RC2-20: an interrupted snapshot (begun, one page, never completed) marks nobody absent — and re-keys nothing; the
    // completion of the next whole one does.
    [Fact]
    public async Task AnInterruptedSnapshot_RecomputesNoKey_TheCompletedOneDoes()
    {
        var deleted = "del-" + NewId();
        await _push.PostChangesAsync(Push(deleted, "Удалён", "Тестов", null, enabled: false, deleted: true));
        await MakeEveryKeyStaleAsync();

        var interrupted = Guid.NewGuid();
        using (var begun = await _push.BeginAsync(interrupted, BaseMoment + 1_000))
        {
            begun.EnsureSuccessStatusCode();
        }

        using (var page = await _push.PageAsync(interrupted, 0, Push(NewId(), "Анна", "Живая", null, moment: BaseMoment + 1_001)))
        {
            page.EnsureSuccessStatusCode();
        }

        Assert.NotEqual(DirectoryCollation.Version, (await FindAsync(factory, deleted))!.SortKeyVersion);
        Assert.Equal(1, await StaleCountAsync());

        await _push.ReconcileAsync(BaseMoment + 2_000, [Push(NewId(), "Борис", "Живой", null, moment: BaseMoment + 2_001)]);

        Assert.Equal(0, await StaleCountAsync());
    }

    // RC2-19: a deletion carries the user's last state, which may say "enabled". Re-keying that deleted user must not
    // let the derived status follow the flag: BR-UP-5 keeps it inactive for good, and out of the default search.
    [Fact]
    public async Task Completion_RekeyingADeletedUserWhoseLastStateWasEnabled_KeepsItInactive()
    {
        var deleted = "del-" + NewId();
        await _push.PostChangesAsync(
            Push(deleted, "Бывший", "Сотрудников", "bivshiy@example.test", enabled: true, deleted: true));
        Assert.False((await FindAsync(factory, deleted))!.IsActive);
        await MakeEveryKeyStaleAsync();

        await _push.ReconcileAsync(BaseMoment + 1_000, [Push(NewId(), "Анна", "Живая", null, moment: BaseMoment + 1_001)]);

        var row = (await FindAsync(factory, deleted))!;
        Assert.Equal(DirectoryCollation.Version, row.SortKeyVersion);
        Assert.Equal(ProjectedUserLifecycle.Deleted, row.Lifecycle);
        Assert.False(row.IsActive);
        Assert.Empty((await SearchAsync(factory, new UserSearch("сотрудников"))).Users);
        Assert.Single((await SearchAsync(factory, new UserSearch("сотрудников", IncludeInactive: true))).Users);
    }

    // RC2-21 at the NFR volume (code review round 1's note on max_locks_per_transaction, which F5 widened): the most
    // keys one completion can lock — every one of 10 000 held users an absence candidate, plus 1 000 stale deleted users
    // that are not (their ids sort first, so they are the 1 000 the stale query takes). 11 000 advisory locks and as many
    // row locks in one transaction, against PostgreSQL's default lock table of 64 × 100 slots.
    [Fact]
    public async Task Completion_WithTenThousandCandidatesAndAThousandStaleOthers_LocksThemAllInOneTransaction()
    {
        var present = await UserDirectoryVolumeTests.SeedVolumeAsync(factory);
        var deleted = Enumerable.Range(0, CompleteSnapshotHandler.StaleSortKeysPerCompletion)
            .Select(i => Push($"00000000-0000-0000-0000-{i:D12}", "Удалён", $"Номер{i:D4}", null, enabled: false, deleted: true))
            .ToList();

        foreach (var chunk in deleted.Chunk(500))
        {
            await _push.PostChangesAsync(chunk);
        }

        await MakeEveryKeyStaleAsync();
        Assert.Equal(present.Count + deleted.Count, await StaleCountAsync());

        var counts = await _push.ReconcileAsync(BaseMoment + 1_000_000, []);

        Assert.Equal(UserDirectoryVolumeTests.TargetVolume, counts.MarkedAbsent);
        Assert.Equal(0, await StaleCountAsync());
    }

    // RC2-22: the same id in both sets of the completion's one lock call. A present user missing from the snapshot whose
    // state is older than its start is an absence candidate; its key is stale as well. It is marked absent and re-keyed
    // once, with no failure on the id's appearing twice. A present user missing from the snapshot but newer than its
    // start is no candidate: its stale key is recomputed and it stays present, state untouched.
    [Fact]
    public async Task AStaleKeyThatIsAlsoAnAbsenceCandidate_IsMarkedAbsentAndRekeyed_AndANewerUserOnlyRekeyed()
    {
        var gone = "gone-" + NewId();
        var newer = "newer-" + NewId();
        var kept = NewId();
        await _push.PostChangesAsync(
            Push(gone, "Пропал", "Яковлев", null, moment: BaseMoment),
            Push(newer, "Новее", "Яковлев", null, moment: BaseMoment + 5_000),
            Push(kept, "Остался", "Яковлев", null, moment: BaseMoment));
        await MakeEveryKeyStaleAsync();
        var newerBefore = (await FindAsync(factory, newer))!;

        var counts = await _push.ReconcileAsync(BaseMoment + 1_000, [Push(kept, "Остался", "Яковлев", null, moment: BaseMoment + 1_001)]);

        Assert.Equal(1, counts.MarkedAbsent);

        var goneRow = (await FindAsync(factory, gone))!;
        Assert.Equal(ProjectedUserLifecycle.Absent, goneRow.Lifecycle);
        Assert.Equal(DirectoryCollation.Version, goneRow.SortKeyVersion);
        Assert.Equal(DirectoryCollation.SortKey(goneRow.DisplayName), goneRow.SortKey);

        var newerRow = (await FindAsync(factory, newer))!;
        Assert.Equal(ProjectedUserLifecycle.Present, newerRow.Lifecycle);
        Assert.Equal(DirectoryCollation.Version, newerRow.SortKeyVersion);
        Assert.Equal(newerBefore.Moment, newerRow.Moment);
        Assert.Equal(newerBefore.ChangedAt, newerRow.ChangedAt);
        Assert.Equal(newerBefore.DisplayName, newerRow.DisplayName);
        Assert.Equal(0, await StaleCountAsync());
    }

    // RC2-21: the completion locks its absence candidates and its stale rows in one call, in the one key order, while
    // pushes of twenty users each take theirs in the same order — no cycle, so no deadlock, no failed request, and the
    // newest push wins. Every third user is missing from the snapshot (an absence candidate) and every key is stale, so
    // each push batch mixes users of both sets: locking the two sets in two calls would let a batch hold a key the
    // completion's second call wants while waiting for one its first call holds.
    // Ten rounds: a reconciliation beside a stream of newer changes for the same sixty users.
    [Fact]
    public async Task ACompletionRekeyingTheUsers_BesideConcurrentPushesForThem_NeitherDeadlocksNorLosesANewerPush()
    {
        const int users = 60;
        const long lastMoment = BaseMoment + 100_030;

        for (var round = 0; round < 10; round++)
        {
            await ResetAsync(factory);
            var ids = Enumerable.Range(0, users).Select(i => $"c{i:D2}-{NewId()}").ToList();
            await _push.PostChangesAsync(ids.Select((id, i) => Push(id, "Житель", $"Номер{i:D2}", null, moment: BaseMoment + 1)).ToArray());
            await MakeEveryKeyStaleAsync();

            var snapshot = ids
                .Select((id, i) => (id, i))
                .Where(user => user.i % 3 != 2)
                .Select(user => Push(user.id, "Житель", $"Номер{user.i:D2}", null, moment: BaseMoment + 50_000))
                .ToList();
            var reconciling = Task.Run(() => _push.ReconcileAsync(BaseMoment + 60_000, snapshot));
            var pushing = Task.Run(async () =>
            {
                for (var step = 1; step <= 30; step++)
                {
                    foreach (var chunk in ids.Chunk(20))
                    {
                        await _push.PostChangesAsync(chunk
                            .Select(id => Push(id, "Житель", $"Номер{ids.IndexOf(id):D2}", $"step{step}@example.test", moment: BaseMoment + 100_000 + step))
                            .ToArray());
                    }
                }
            });

            var counts = await reconciling;
            await pushing;

            Assert.InRange(counts.MarkedAbsent, 0, users / 3);
            Assert.Equal(0, await StaleCountAsync());

            foreach (var id in ids)
            {
                var row = (await FindAsync(factory, id))!;
                Assert.Equal(lastMoment, row.Moment);
                Assert.Equal(ProjectedUserLifecycle.Present, row.Lifecycle);
                Assert.Equal("step30@example.test", row.Email);
            }
        }
    }
}
