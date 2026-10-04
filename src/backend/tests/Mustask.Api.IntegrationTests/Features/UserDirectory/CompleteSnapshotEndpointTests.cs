using System.Net;
using Mustask.Domain.UserDirectory;
using static Mustask.Api.IntegrationTests.Features.UserDirectory.UserDirectoryTestData;

namespace Mustask.Api.IntegrationTests.Features.UserDirectory;

/// <summary>
/// mod-014 api-contract.md B-4, <c>PUT .../snapshots/{snapshotId}/completion</c>:
/// accepted only when MUSTASK can prove the snapshot whole (ADR-0025 § 5);
/// idempotent once completed; anything less marks nobody absent.
/// </summary>
[Collection(MustaskApiCollection.Name)]
public sealed class CompleteSnapshotEndpointTests(MustaskApiFactory factory) : IAsyncLifetime
{
    private const long EnumerationStart = BaseMoment + 1_000;

    private readonly PushTestClient _push = new(factory);

    private Guid _snapshotId;

    public async Task InitializeAsync()
    {
        await ResetAsync(factory);
        _snapshotId = Guid.NewGuid();
        using var begun = await _push.BeginAsync(_snapshotId, EnumerationStart);
        begun.EnsureSuccessStatusCode();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task PageAsync(int pageNumber, params UserPush[] entries)
    {
        using var response = await _push.PageAsync(_snapshotId, pageNumber, entries);
        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Complete_WholeSnapshot_AnswersThePagesSumsAndTheAbsences()
    {
        var lost = NewId();
        await _push.PostChangesAsync(Push(lost, "Пётр", "Волков", null));
        var kept = NewId();
        await PageAsync(0, Push(kept, "Елена", "Лебедева", null, moment: EnumerationStart + 1));
        await PageAsync(1, Push(NewId(), "Дмитрий", "Орлов", null, moment: EnumerationStart + 2));

        using var response = await _push.CompleteAsync(_snapshotId, 2, 2);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(new SnapshotCountsBody(2, 2, 0, 0, 0, 1, 2, 2), await PushTestClient.ReadAsync<SnapshotCountsBody>(response));
        Assert.False((await LookupAsync(factory, lost)).Known[lost].IsActive);
        Assert.True((await LookupAsync(factory, kept)).Known[kept].IsActive);
    }

    [Fact]
    public async Task Complete_Again_AnswersTheStoredCountsAndMarksNothingAgain()
    {
        await PageAsync(0, Push(NewId(), "Елена", "Лебедева", null, moment: EnumerationStart + 1));
        using (var first = await _push.CompleteAsync(_snapshotId, 1, 1))
        {
            first.EnsureSuccessStatusCode();
        }

        // A user pushed after the completion, older than the snapshot's start, is not marked by a repeat.
        var later = NewId();
        await _push.PostChangesAsync(Push(later, "Сергей", "Новиков", null));

        using var again = await _push.CompleteAsync(_snapshotId, 1, 1);

        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
        Assert.Equal(new SnapshotCountsBody(1, 1, 0, 0, 0, 0, 1, 1), await PushTestClient.ReadAsync<SnapshotCountsBody>(again));
        Assert.True((await LookupAsync(factory, later)).Known[later].IsActive);
    }

    // A realm with no users completes with 0, 0.
    [Fact]
    public async Task Complete_EmptyRealm_CompletesWithZeroCounts()
    {
        using var response = await _push.CompleteAsync(_snapshotId, 0, 0);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(new SnapshotCountsBody(0, 0, 0, 0, 0, 0, 0, 0), await PushTestClient.ReadAsync<SnapshotCountsBody>(response));
    }

    // US-4 "An interrupted snapshot marks nobody absent" — counts that do not match what arrived.
    [Theory]
    [InlineData(2, 2)] // a page missing
    [InlineData(1, 2)] // an entry missing
    [InlineData(1, 0)] // more arrived than declared
    public async Task Complete_CountsNotMatchingWhatArrived_IsIncompleteAndMarksNobody(int pageCount, int entryCount)
    {
        var bystander = NewId();
        await _push.PostChangesAsync(Push(bystander, "Пётр", "Волков", null));
        await PageAsync(0, Push(NewId(), "Елена", "Лебедева", null, moment: EnumerationStart + 1));

        using var response = await _push.CompleteAsync(_snapshotId, pageCount, entryCount);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await PushTestClient.ProblemAsync(response);
        Assert.Equal(UserDirectoryErrorCodes.SnapshotIncomplete, problem.GetProperty("errorCode").GetString());
        Assert.Equal(entryCount, problem.GetProperty("params").GetProperty("expected").GetProperty("value").GetInt32());
        Assert.Equal(1, problem.GetProperty("params").GetProperty("received").GetProperty("value").GetInt32());
        Assert.True((await LookupAsync(factory, bystander)).Known[bystander].IsActive);

        // It stays open: the whole snapshot still completes.
        using var whole = await _push.CompleteAsync(_snapshotId, 1, 1);
        Assert.Equal(HttpStatusCode.OK, whole.StatusCode);
    }

    [Fact]
    public async Task Complete_PageNumbersNotFromZero_IsIncomplete()
    {
        await PageAsync(1, Push(NewId(), "Елена", "Лебедева", null, moment: EnumerationStart + 1));

        using var response = await _push.CompleteAsync(_snapshotId, 1, 1);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(UserDirectoryErrorCodes.SnapshotIncomplete, await PushTestClient.ErrorCodeAsync(response));
    }

    [Fact]
    public async Task Complete_SupersededSnapshot_IsNotOpenAndMarksNobody()
    {
        var bystander = NewId();
        await _push.PostChangesAsync(Push(bystander, "Пётр", "Волков", null));
        using (var newer = await _push.BeginAsync(Guid.NewGuid(), EnumerationStart + 1))
        {
            newer.EnsureSuccessStatusCode();
        }

        using var response = await _push.CompleteAsync(_snapshotId, 0, 0);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal(UserDirectoryErrorCodes.SnapshotNotOpen, await PushTestClient.ErrorCodeAsync(response));
        Assert.True((await LookupAsync(factory, bystander)).Known[bystander].IsActive);
    }

    [Fact]
    public async Task Complete_UnknownSnapshot_IsNotFound()
    {
        using var response = await _push.CompleteAsync(Guid.NewGuid(), 0, 0);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(UserDirectoryErrorCodes.SnapshotNotFound, await PushTestClient.ErrorCodeAsync(response));
    }

    [Fact]
    public async Task Complete_CountsMissingOrNegative_AreRefusedTogether()
    {
        using var response = await _push.SendAsync(
            PushTestClient.Build(HttpMethod.Put, $"snapshots/{_snapshotId}/completion", """{"entryCount":-1}"""));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await PushTestClient.ProblemAsync(response);
        Assert.Equal(UserDirectoryErrorCodes.ValidationFailed, problem.GetProperty("errorCode").GetString());
        var errors = problem.GetProperty("errors");
        Assert.Equal(UserDirectoryErrorCodes.ValueRequired, errors.GetProperty("pageCount")[0].GetProperty("code").GetString());
        Assert.Equal(UserDirectoryErrorCodes.ValueInvalid, errors.GetProperty("entryCount")[0].GetProperty("code").GetString());
    }
}
