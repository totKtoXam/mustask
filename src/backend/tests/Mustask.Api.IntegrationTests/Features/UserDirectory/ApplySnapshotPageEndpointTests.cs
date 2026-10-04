using System.Net;
using Mustask.Domain.UserDirectory;
using static Mustask.Api.IntegrationTests.Features.UserDirectory.UserDirectoryTestData;

namespace Mustask.Api.IntegrationTests.Features.UserDirectory;

/// <summary>
/// mod-014 api-contract.md B-3, <c>PUT .../snapshots/{snapshotId}/pages/{pageNumber}</c>:
/// entries applied as pushes and stamped with the snapshot; a page sent again
/// answers the stored counts and is not applied again; only an open snapshot
/// takes pages.
/// </summary>
[Collection(MustaskApiCollection.Name)]
public sealed class ApplySnapshotPageEndpointTests(MustaskApiFactory factory) : IAsyncLifetime
{
    private readonly PushTestClient _push = new(factory);

    private Guid _snapshotId;

    public async Task InitializeAsync()
    {
        await ResetAsync(factory);
        _snapshotId = Guid.NewGuid();
        using var begun = await _push.BeginAsync(_snapshotId, BaseMoment + 1_000);
        begun.EnsureSuccessStatusCode();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private static UserPush Maria(string id, long moment, string lastName = "Белова") =>
        Push(id, "Мария", lastName, "m.belova@example.test", username: "m.belova", moment: moment);

    [Fact]
    public async Task Page_OfAnOpenSnapshot_IsAppliedAndStampsItsUsers()
    {
        var held = NewId();
        await _push.PostChangesAsync(Maria(held, BaseMoment));
        var fresh = NewId();

        using var response = await _push.PageAsync(_snapshotId, 0, Maria(held, BaseMoment + 900, lastName: "Орлова"), Push(fresh, "Иван", "Новиков", null, moment: BaseMoment + 901));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(new FeedCountsBody(2, 1, 1, 0, 0), await PushTestClient.ReadAsync<FeedCountsBody>(response));
        Assert.Equal("Мария Орлова", (await LookupAsync(factory, held)).Known[held].DisplayName);
        Assert.Equal(_snapshotId, (await FindAsync(factory, held))!.LastSnapshotId);
        Assert.Equal(_snapshotId, (await FindAsync(factory, fresh))!.LastSnapshotId);
    }

    // ADR-0025 § 6, "A snapshot entry": its newer moment is stored even when its values are unchanged.
    [Fact]
    public async Task Page_EntryWithUnchangedValues_CountsUnchangedAndStoresTheNewerMoment()
    {
        var id = NewId();
        await _push.PostChangesAsync(Maria(id, BaseMoment));

        using var response = await _push.PageAsync(_snapshotId, 0, Maria(id, BaseMoment + 900));

        Assert.Equal(new FeedCountsBody(1, 0, 0, 1, 0), await PushTestClient.ReadAsync<FeedCountsBody>(response));
        Assert.Equal(BaseMoment + 900, (await FindAsync(factory, id))!.Moment);
    }

    [Fact]
    public async Task Page_SentAgain_AnswersTheStoredCountsAndIsNotAppliedAgain()
    {
        var id = NewId();
        using (var first = await _push.PageAsync(_snapshotId, 0, Maria(id, BaseMoment)))
        {
            Assert.Equal(new FeedCountsBody(1, 1, 0, 0, 0), await PushTestClient.ReadAsync<FeedCountsBody>(first));
        }

        using var again = await _push.PageAsync(_snapshotId, 0, Maria(id, BaseMoment + 5, lastName: "Другая"));

        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
        Assert.Equal(new FeedCountsBody(1, 1, 0, 0, 0), await PushTestClient.ReadAsync<FeedCountsBody>(again));
        Assert.Equal("Мария Белова", (await LookupAsync(factory, id)).Known[id].DisplayName);
    }

    [Fact]
    public async Task Page_OfAnUnknownSnapshot_IsNotFound()
    {
        using var response = await _push.PageAsync(Guid.NewGuid(), 0, Maria(NewId(), BaseMoment));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(UserDirectoryErrorCodes.SnapshotNotFound, await PushTestClient.ErrorCodeAsync(response));
        Assert.Equal(0, await CountAsync(factory));
    }

    [Fact]
    public async Task Page_OfASupersededSnapshot_IsNotOpen()
    {
        using (var newer = await _push.BeginAsync(Guid.NewGuid(), BaseMoment + 2_000))
        {
            newer.EnsureSuccessStatusCode();
        }

        using var response = await _push.PageAsync(_snapshotId, 0, Maria(NewId(), BaseMoment));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(UserDirectoryErrorCodes.SnapshotNotOpen, await PushTestClient.ErrorCodeAsync(response));
        Assert.Equal(0, await CountAsync(factory));
    }

    [Fact]
    public async Task Page_OfACompletedSnapshot_IsNotOpen()
    {
        using (var completed = await _push.CompleteAsync(_snapshotId, 0, 0))
        {
            completed.EnsureSuccessStatusCode();
        }

        using var response = await _push.PageAsync(_snapshotId, 0, Maria(NewId(), BaseMoment));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(UserDirectoryErrorCodes.SnapshotNotOpen, await PushTestClient.ErrorCodeAsync(response));
    }

    [Fact]
    public async Task Page_NegativeNumber_IsRefused()
    {
        using var response = await _push.PageAsync(_snapshotId, -1, Maria(NewId(), BaseMoment));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await PushTestClient.ProblemAsync(response);
        Assert.Equal(UserDirectoryErrorCodes.ValidationFailed, problem.GetProperty("errorCode").GetString());
        Assert.Equal(UserDirectoryErrorCodes.ValueInvalid, problem.GetProperty("errors").GetProperty("pageNumber")[0].GetProperty("code").GetString());
    }

    [Fact]
    public async Task Page_WithADeletionOrARepeatedId_IsRefusedWithFieldPaths()
    {
        var id = NewId();
        var body = $$"""
            {"entries":[
              {"id":"{{id}}","moment":{{BaseMoment}},"username":"u","enabled":true},
              {"id":"{{id}}","moment":{{BaseMoment + 1}},"username":"u","enabled":true},
              {"id":"{{NewId()}}","moment":{{BaseMoment}},"username":"u","deleted":true}
            ]}
            """;

        using var response = await _push.SendAsync(PushTestClient.Build(HttpMethod.Put, $"snapshots/{_snapshotId}/pages/0", body));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = (await PushTestClient.ProblemAsync(response)).GetProperty("errors");
        Assert.Equal(UserDirectoryErrorCodes.ValueInvalid, errors.GetProperty("entries[1].id")[0].GetProperty("code").GetString());
        Assert.Equal(UserDirectoryErrorCodes.ValueInvalid, errors.GetProperty("entries[2].deleted")[0].GetProperty("code").GetString());
        Assert.Equal(0, await CountAsync(factory));
    }

    [Fact]
    public async Task Page_EmptyOrOverFiveHundredEntries_IsRefused()
    {
        using (var empty = await _push.SendAsync(PushTestClient.Build(HttpMethod.Put, $"snapshots/{_snapshotId}/pages/0", """{"entries":[]}""")))
        {
            Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);
            Assert.Equal(UserDirectoryErrorCodes.ValueRequired, (await PushTestClient.ProblemAsync(empty)).GetProperty("errors").GetProperty("entries")[0].GetProperty("code").GetString());
        }

        var tooMany = Enumerable.Range(0, UserDirectoryLimits.EntriesPerPush + 1).Select(_ => Push(NewId(), "А", "Б", null)).ToArray();
        using var response = await _push.PageAsync(_snapshotId, 0, tooMany);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(UserDirectoryErrorCodes.TooManyEntries, (await PushTestClient.ProblemAsync(response)).GetProperty("errors").GetProperty("entries")[0].GetProperty("code").GetString());
    }

    // US-3 "A reported deletion is final" — a snapshot entry for a deleted user is held as deleted.
    [Fact]
    public async Task Page_EntryForADeletedUser_IsHeldAsDeleted()
    {
        var id = NewId();
        await _push.PostChangesAsync(Maria(id, BaseMoment) with { Deleted = true, Enabled = false });

        using var response = await _push.PageAsync(_snapshotId, 0, Maria(id, BaseMoment + 900));

        Assert.Equal(new FeedCountsBody(1, 0, 0, 0, 1), await PushTestClient.ReadAsync<FeedCountsBody>(response));
        Assert.False((await LookupAsync(factory, id)).Known[id].IsActive);
    }
}
