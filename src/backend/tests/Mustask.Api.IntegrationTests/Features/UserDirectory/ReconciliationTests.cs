using Mustask.Application.Abstractions.UserDirectory;
using Mustask.Domain.UserDirectory;
using Mustask.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static Mustask.Api.IntegrationTests.Features.UserDirectory.UserDirectoryTestData;

namespace Mustask.Api.IntegrationTests.Features.UserDirectory;

/// <summary>
/// mod-014 US-4, the MUSTASK half of backfill and reconciliation (BR-UP-8;
/// ADR-0025 § 5, § 6): whole snapshots sent the way the provider sends them
/// (<see cref="PushTestClient.ReconcileAsync"/>). The provider's half is in
/// its own Java tests and the opt-in live tests.
/// </summary>
[Collection(MustaskApiCollection.Name)]
public sealed class ReconciliationTests(MustaskApiFactory factory) : IAsyncLifetime
{
    private const int RealmSize = 200;

    private readonly PushTestClient _push = new(factory);

    public Task InitializeAsync() => ResetAsync(factory);

    public Task DisposeAsync() => Task.CompletedTask;

    /// <summary>200 users, five of them disabled, as one reconciliation reads them at <paramref name="moment"/>.</summary>
    private static List<UserPush> Realm(IReadOnlyList<string> ids, long moment) =>
        ids.Select((id, i) => Push(id, "Житель", $"Реалмов{i}", $"resident{i}@realm.test", enabled: i >= 5, moment: moment + i)).ToList();

    private static List<string> Ids(int count) => Enumerable.Range(0, count).Select(_ => NewId()).ToList();

    // US-4 "Existing users are loaded into an empty projection".
    [Fact]
    public async Task Reconcile_IntoAnEmptyProjection_LoadsEveryUserWithItsStatus()
    {
        var ids = Ids(RealmSize);

        var counts = await _push.ReconcileAsync(BaseMoment, Realm(ids, BaseMoment + 1));

        Assert.Equal(new SnapshotCountsBody(RealmSize, RealmSize, 0, 0, 0, 0, 1, RealmSize), counts);
        var lookup = await LookupAsync(factory, [.. ids]);
        Assert.Equal(RealmSize, lookup.Known.Count);
        Assert.Equal(5, lookup.Known.Values.Count(user => !user.IsActive));
        Assert.Equal("Житель Реалмов7", lookup.Known[ids[7]].DisplayName);
    }

    // US-4 "A second stand start keeps the same users under the same ids" (the MUSTASK half) and
    // "A repeated reconciliation over an unchanged realm changes nothing".
    [Fact]
    public async Task Reconcile_AnUnchangedRealmAgain_CountsNoAdditionChangeAbsenceOrDeletion()
    {
        var ids = Ids(RealmSize);
        await _push.ReconcileAsync(BaseMoment, Realm(ids, BaseMoment + 1));

        var counts = await _push.ReconcileAsync(BaseMoment + 1_000, Realm(ids, BaseMoment + 1_001));

        Assert.Equal(new SnapshotCountsBody(RealmSize, 0, 0, RealmSize, 0, 0, 1, RealmSize), counts);
        Assert.Equal(RealmSize, await CountAsync(factory));
    }

    // US-4 "A deletion whose push was lost is caught by the next reconciliation".
    [Fact]
    public async Task Reconcile_WithoutAHeldUser_MarksItAbsentInactiveWithItsNameKept()
    {
        var gone = NewId();
        await _push.PostChangesAsync(Push(gone, "Ушедший", "Пользователь", "gone@example.test"));
        var stays = NewId();

        var counts = await _push.ReconcileAsync(BaseMoment + 1_000, [Push(stays, "Оставшийся", "Пользователь", null, moment: BaseMoment + 1_001)]);

        Assert.Equal(1, counts.MarkedAbsent);
        var user = (await LookupAsync(factory, gone)).Known[gone];
        Assert.Equal(new DirectoryUser(gone, "Ушедший Пользователь", "gone@example.test", false), user);
        Assert.Equal(ProjectedUserLifecycle.Absent, (await FindAsync(factory, gone))!.Lifecycle);
        Assert.Empty((await SearchAsync(factory, new UserSearch("ушедший"))).Users);
        Assert.Single((await SearchAsync(factory, new UserSearch("ушедший", IncludeInactive: true))).Users);
    }

    // US-4 "A change whose push was lost is caught by the next reconciliation".
    [Fact]
    public async Task Reconcile_WithAChangedUser_FollowsTheChange()
    {
        var id = NewId();
        await _push.PostChangesAsync(Push(id, "Анна", "Смирнова", "anna@example.test"));

        var counts = await _push.ReconcileAsync(BaseMoment + 1_000, [Push(id, "Анна", "Кузнецова", "anna@example.test", enabled: false, moment: BaseMoment + 1_001)]);

        Assert.Equal(1, counts.Changed);
        Assert.Equal(new DirectoryUser(id, "Анна Кузнецова", "anna@example.test", false), (await LookupAsync(factory, id)).Known[id]);
    }

    // US-4 "A user created during a reconciliation stays active".
    [Fact]
    public async Task Reconcile_UserPushedAfterTheEnumerationStart_IsNotMarkedAbsent()
    {
        const long enumerationStart = BaseMoment + 1_000;
        var snapshotId = Guid.NewGuid();
        using (var begun = await _push.BeginAsync(snapshotId, enumerationStart))
        {
            begun.EnsureSuccessStatusCode();
        }

        var created = NewId();
        await _push.PostChangesAsync(Push(created, "Новый", "Сотрудник", null, moment: enumerationStart + 50));

        using (var completed = await _push.CompleteAsync(snapshotId, 0, 0))
        {
            completed.EnsureSuccessStatusCode();
            Assert.Equal(0, (await PushTestClient.ReadAsync<SnapshotCountsBody>(completed)).MarkedAbsent);
        }

        Assert.True((await LookupAsync(factory, created)).Known[created].IsActive);
    }

    // US-4 "An absence yields to a newer push" — and not to an older one.
    [Fact]
    public async Task Absence_YieldsToANewerPushButNotToAnOlderOne()
    {
        const long enumerationStart = BaseMoment + 1_000;
        var id = NewId();
        await _push.PostChangesAsync(Push(id, "Пропавший", "Пользователь", null));
        await _push.ReconcileAsync(enumerationStart, []);

        var older = await _push.PostChangesAsync(Push(id, "Пропавший", "Пользователь", null, moment: enumerationStart - 1));

        Assert.Equal(1, older.Unchanged);
        Assert.False((await LookupAsync(factory, id)).Known[id].IsActive);

        var newer = await _push.PostChangesAsync(Push(id, "Вернувшийся", "Пользователь", null, moment: enumerationStart + 1));

        Assert.Equal(1, newer.Changed);
        Assert.Equal(new DirectoryUser(id, "Вернувшийся Пользователь", string.Empty, true), (await LookupAsync(factory, id)).Known[id]);
    }

    // US-4 "An interrupted snapshot marks nobody absent" — pages without a completion.
    [Fact]
    public async Task InterruptedReconciliation_PagesWithoutCompletion_MarksNobody()
    {
        var bystander = NewId();
        await _push.PostChangesAsync(Push(bystander, "Пётр", "Волков", null));
        var snapshotId = Guid.NewGuid();
        using (var begun = await _push.BeginAsync(snapshotId, BaseMoment + 1_000))
        {
            begun.EnsureSuccessStatusCode();
        }

        using (var page = await _push.PageAsync(snapshotId, 0, Push(NewId(), "Елена", "Лебедева", null, moment: BaseMoment + 1_001)))
        {
            page.EnsureSuccessStatusCode();
        }

        // The provider restarts: a newer snapshot supersedes the interrupted one, which never completes.
        using (var restarted = await _push.BeginAsync(Guid.NewGuid(), BaseMoment + 2_000))
        {
            restarted.EnsureSuccessStatusCode();
        }

        Assert.True((await LookupAsync(factory, bystander)).Known[bystander].IsActive);
        Assert.Equal(ProjectedUserLifecycle.Present, (await FindAsync(factory, bystander))!.Lifecycle);
    }

    // US-3 "A reported deletion is final" — a later state push and a complete reconciliation leave it deleted.
    [Fact]
    public async Task Deletion_SurvivesALaterPushAndACompleteReconciliationThatCarriesTheUser()
    {
        var id = NewId();
        await _push.PostChangesAsync(Push(id, "Удалённый", "Пользователь", "deleted@example.test", moment: BaseMoment, deleted: true, enabled: false));

        var later = await _push.PostChangesAsync(Push(id, "Удалённый", "Пользователь", "deleted@example.test", moment: BaseMoment + 10));
        var reconciled = await _push.ReconcileAsync(BaseMoment + 1_000, [Push(id, "Удалённый", "Пользователь", "deleted@example.test", moment: BaseMoment + 1_001)]);

        Assert.Equal(1, later.HeldAsDeleted);
        Assert.Equal(1, reconciled.HeldAsDeleted);
        Assert.Equal(0, reconciled.MarkedAbsent);
        Assert.Equal(new DirectoryUser(id, "Удалённый Пользователь", "deleted@example.test", false), (await LookupAsync(factory, id)).Known[id]);
        Assert.Equal(ProjectedUserLifecycle.Deleted, (await FindAsync(factory, id))!.Lifecycle);
    }

    // ADR-0025 § 9, api-contract.md A.5 "A search answer is already in this order" (code review F5): after the API
    // host's ICU changes, every stored key is of the old collation version. The reconciliation's pages rewrite the
    // present users; deleted and absent ones are never pushed again, so the completion recomputes their keys — and
    // nothing else of them. The ids run against the names, so keys left stale would order the answer by id.
    [Fact]
    public async Task Reconcile_RecomputesStaleSortKeys_OfDeletedAndAbsentUsersToo()
    {
        var deleted = "aa-" + NewId();
        var absent = "mm-" + NewId();
        var present = "zz-" + NewId();
        await _push.PostChangesAsync(
            Push(deleted, "Ярослав", "Тестов", null, enabled: false, deleted: true),
            Push(absent, "Борис", "Тестов", null),
            Push(present, "Анна", "Тестова", null));
        await _push.ReconcileAsync(BaseMoment + 1_000, [Push(present, "Анна", "Тестова", null, moment: BaseMoment + 1_001)]);
        await WriteSortKeysOfAnotherCollationAsync();
        var before = await FindAsync(factory, deleted);

        await _push.ReconcileAsync(BaseMoment + 2_000, [Push(present, "Анна", "Тестова", null, moment: BaseMoment + 2_001)]);

        foreach (var id in new[] { deleted, absent, present })
        {
            var row = (await FindAsync(factory, id))!;
            Assert.Equal(DirectoryCollation.Version, row.SortKeyVersion);
            Assert.Equal(DirectoryCollation.SortKey(row.DisplayName), row.SortKey);
        }

        var after = (await FindAsync(factory, deleted))!;
        Assert.Equal(ProjectedUserLifecycle.Deleted, after.Lifecycle);
        Assert.Equal(before!.Moment, after.Moment);
        Assert.Equal(before.ChangedAt, after.ChangedAt);
        Assert.Equal(ProjectedUserLifecycle.Absent, (await FindAsync(factory, absent))!.Lifecycle);

        var answer = await SearchAsync(factory, new UserSearch("тест", IncludeInactive: true));
        string[] inDirectoryUserOrder = ["Анна Тестова", "Борис Тестов", "Ярослав Тестов"];
        Assert.Equal(inDirectoryUserOrder, answer.Users.Select(user => user.DisplayName));
    }

    /// <summary>As if every row had been written by an API host with another ICU: another version, a key that sorts first.</summary>
    private async Task WriteSortKeysOfAnotherCollationAsync()
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<MustaskDbContext>();

        await context.Database.ExecuteSqlRawAsync(
            "UPDATE directory_users SET sort_key_version = 'ru/another-icu', sort_key = '\\x00'::bytea");
    }

    // US-5 "Statuses stay as last received while the feed is interrupted": nothing changes without a push.
    [Fact]
    public async Task Statuses_StayAsLastReceived_UntilThePushesResume()
    {
        var disabled = NewId();
        var enabled = NewId();
        await _push.PostChangesAsync(
            Push(disabled, "Отключённый", "Пользователь", null, enabled: false),
            Push(enabled, "Включённый", "Пользователь", null));

        // No push and no reconciliation arrives: the projection answers as last received.
        var lookup = await LookupAsync(factory, disabled, enabled);

        Assert.False(lookup.Known[disabled].IsActive);
        Assert.True(lookup.Known[enabled].IsActive);
    }
}
