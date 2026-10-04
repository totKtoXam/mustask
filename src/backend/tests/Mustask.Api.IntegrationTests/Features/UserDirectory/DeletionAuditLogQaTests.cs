using System.Net;
using Mustask.Domain.UserDirectory;
using static Mustask.Api.IntegrationTests.Features.UserDirectory.UserDirectoryTestData;

namespace Mustask.Api.IntegrationTests.Features.UserDirectory;

/// <summary>
/// QA re-check 3 (qa.md, RC3-30..RC3-34): the security pass's S5 fix — "deletions applied N" and a line with the ids —
/// past the cases <c>DeletionAuditLogTests</c> picked. What counts as an applied deletion: every id newly deleted, once,
/// whichever way it got there (a present user, an absent one, an id never held, an older moment than the state held,
/// the entries in either order); and what does not: a deletion that finds the user deleted already even when it carries
/// other values and a newer moment, a state entry for a deleted id, a request that was refused whole.
/// </summary>
[Collection(MustaskApiCollection.Name)]
public sealed class DeletionAuditLogQaTests(MustaskApiFactory factory) : IAsyncLifetime
{
    private const string IdsLine = "deletions applied for user ids";

    private readonly PushTestClient _push = new(factory);

    public Task InitializeAsync() => ResetAsync(factory);

    public Task DisposeAsync() => ResetAsync(factory);

    private static UserPush Deletion(string id, long moment, string surname = "Удалённый", string email = "deleted@example.test") =>
        Push(id, "Игорь", surname, email, enabled: false, username: $"deleted.{surname}", moment: moment, deleted: true);

    private static string[] InfoLines(string output) =>
        [.. output.Split('\n').Where(line => line.Contains("INF]", StringComparison.Ordinal))];

    private static string? TheIdsLine(string output) =>
        InfoLines(output).SingleOrDefault(line => line.Contains(IdsLine, StringComparison.Ordinal));

    private static string TheApplyLine(string output) =>
        InfoLines(output).Single(line => line.Contains("changes applied", StringComparison.Ordinal));

    private Task<string> SendChangesAsync(HttpStatusCode expected, params UserPush[] changes) =>
        ConsoleCapture.RunAsync(factory, async push =>
        {
            using var response = await push.SendAsync(PushTestClient.Build(HttpMethod.Post, "changes", PushTestClient.ChangesBody(changes)));

            Assert.Equal(expected, response.StatusCode);
        });

    // RC3-30: a held present user, a held absent one, an id never held (twice, two moments), a held user deleted with a
    // moment older than the state held, and a state entry reviving an absent user: three ids newly deleted (the unknown one
    // once), the revival is no deletion.
    [Fact]
    public async Task EveryIdNewlyDeleted_IsCountedAndListedOnce_WhateverItsPreviousState()
    {
        var present = NewId();
        var absentThenDeleted = NewId();
        var absentThenRevived = NewId();
        var unknown = NewId();
        await SeedAsync(factory, [
            Push(present, "А", "Б", null, moment: BaseMoment + 500),
            Push(absentThenDeleted, "В", "Г", null, moment: BaseMoment),
            Push(absentThenRevived, "Д", "Е", null, moment: BaseMoment)]);

        // A complete snapshot that holds only `present` marks the two others absent.
        var counts = await _push.ReconcileAsync(BaseMoment + 1_000, [Push(present, "А", "Б", null, moment: BaseMoment + 500)]);
        Assert.Equal(2, counts.MarkedAbsent);

        var output = await SendChangesAsync(
            HttpStatusCode.OK,
            Deletion(absentThenDeleted, BaseMoment + 2_000),
            Deletion(unknown, BaseMoment + 2_010),
            Deletion(unknown, BaseMoment + 2_020),
            Deletion(present, BaseMoment + 1),
            Push(absentThenRevived, "Д", "Е", null, moment: BaseMoment + 3_000));

        Assert.Contains("deletions applied 3.", TheApplyLine(output), StringComparison.Ordinal);

        var idsLine = TheIdsLine(output);
        Assert.NotNull(idsLine);

        foreach (var id in new[] { absentThenDeleted, unknown, present })
        {
            Assert.Equal(1, CountOf(idsLine, id));
        }

        Assert.DoesNotContain(absentThenRevived, idsLine, StringComparison.Ordinal);
        Assert.Equal(ProjectedUserLifecycle.Deleted, (await FindAsync(factory, present))!.Lifecycle);
        Assert.False((await FindAsync(factory, absentThenRevived))!.Lifecycle == ProjectedUserLifecycle.Deleted);
    }

    // RC3-31: a deletion that finds the user deleted already is nothing applied, though it carries other values and a
    // newer moment; a state entry for a deleted id is `heldAsDeleted` and no deletion either.
    [Fact]
    public async Task ARepeatedDeletion_WithOtherValuesAndANewerMoment_IsNotApplied_AndNamesNoId()
    {
        var id = NewId();
        await SeedAsync(factory, [Deletion(id, BaseMoment)]);

        var output = await SendChangesAsync(HttpStatusCode.OK, Deletion(id, BaseMoment + 100, surname: "Другой", email: "other@example.test"));

        Assert.Contains("unchanged 1", TheApplyLine(output), StringComparison.Ordinal);
        Assert.Contains("deletions applied 0.", TheApplyLine(output), StringComparison.Ordinal);
        Assert.Null(TheIdsLine(output));
        Assert.Equal("Удалённый", (await FindAsync(factory, id))!.LastName);
    }

    [Fact]
    public async Task AStateEntryForADeletedId_IsHeldAsDeleted_NotADeletion()
    {
        var id = NewId();
        await SeedAsync(factory, [Deletion(id, BaseMoment)]);

        var output = await SendChangesAsync(HttpStatusCode.OK, Push(id, "Игорь", "Удалённый", null, moment: BaseMoment + 100));

        Assert.Contains("held as deleted 1", TheApplyLine(output), StringComparison.Ordinal);
        Assert.Contains("deletions applied 0.", TheApplyLine(output), StringComparison.Ordinal);
        Assert.Null(TheIdsLine(output));
    }

    // RC3-32: for one held user a newer state and an older deletion, in either order: the deletion wins, the id is
    // listed once, and a state entry after a deletion in the same request changes neither N nor the list.
    [Fact]
    public async Task ADeletionAndANewerState_InEitherOrder_ListTheIdOnce_AndTheDeletionWins()
    {
        var first = NewId();
        var second = NewId();
        await SeedAsync(factory, [Push(first, "А", "Б", null, moment: BaseMoment), Push(second, "В", "Г", null, moment: BaseMoment)]);

        var output = await SendChangesAsync(
            HttpStatusCode.OK,
            Push(first, "А2", "Б2", null, moment: BaseMoment + 50),
            Deletion(first, BaseMoment + 10),
            Deletion(second, BaseMoment + 10),
            Push(second, "В2", "Г2", null, moment: BaseMoment + 50));

        Assert.Contains("deletions applied 2.", TheApplyLine(output), StringComparison.Ordinal);
        var idsLine = TheIdsLine(output)!;
        Assert.Equal(1, CountOf(idsLine, first));
        Assert.Equal(1, CountOf(idsLine, second));

        Assert.Equal(ProjectedUserLifecycle.Deleted, (await FindAsync(factory, first))!.Lifecycle);
        Assert.Equal(ProjectedUserLifecycle.Deleted, (await FindAsync(factory, second))!.Lifecycle);
    }

    // RC3-34: a request refused whole writes nothing and logs no apply line and no ids line, whatever it carried.
    [Fact]
    public async Task ARefusedBatch_LogsNeitherTheCountsNorTheIds_AndDeletesNobody()
    {
        var held = NewId();
        await SeedAsync(factory, [Push(held, "А", "Б", null)]);

        var output = await SendChangesAsync(HttpStatusCode.BadRequest, Deletion(held, BaseMoment + 10), Push(NewId(), "В", "Г", null, moment: 0));

        Assert.DoesNotContain("changes applied", output, StringComparison.Ordinal);
        Assert.DoesNotContain(IdsLine, output, StringComparison.Ordinal);
        Assert.NotEqual(ProjectedUserLifecycle.Deleted, (await FindAsync(factory, held))!.Lifecycle);
    }

    // RC3-34: the largest batch of deletions — 500 — is one ids line holding all 500 ids, each once, and no name.
    [Fact]
    public async Task FiveHundredDeletions_AreOneIdsLine_WithEveryIdOnce()
    {
        var ids = Enumerable.Range(0, UserDirectoryLimits.EntriesPerPush).Select(_ => NewId()).ToList();

        var output = await SendChangesAsync(HttpStatusCode.OK, [.. ids.Select(id => Deletion(id, BaseMoment + 10, surname: "Пятьсотый"))]);

        Assert.Contains("deletions applied 500.", TheApplyLine(output), StringComparison.Ordinal);

        var idsLine = TheIdsLine(output)!;
        Assert.All(ids, id => Assert.Equal(1, CountOf(idsLine, id)));
        Assert.DoesNotContain("Пятьсотый", output, StringComparison.Ordinal);
        Assert.DoesNotContain("deleted.Пятьсотый", output, StringComparison.Ordinal);
    }

    private static int CountOf(string text, string value)
    {
        var count = 0;

        for (var at = text.IndexOf(value, StringComparison.Ordinal); at >= 0; at = text.IndexOf(value, at + value.Length, StringComparison.Ordinal))
        {
            count++;
        }

        return count;
    }
}
