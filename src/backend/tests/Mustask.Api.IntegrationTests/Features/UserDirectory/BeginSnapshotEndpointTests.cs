using System.Net;
using Mustask.Domain.UserDirectory;
using Mustask.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static Mustask.Api.IntegrationTests.Features.UserDirectory.UserDirectoryTestData;

namespace Mustask.Api.IntegrationTests.Features.UserDirectory;

/// <summary>
/// mod-014 api-contract.md B-2, <c>PUT .../snapshots/{snapshotId}</c>: a new
/// snapshot must start after every one begun so far and supersedes the open
/// ones; the same id again is idempotent with the same start and a conflict
/// with another (ADR-0025 § 3, "Replay": an old "begin" never supersedes a
/// newer snapshot).
/// </summary>
[Collection(MustaskApiCollection.Name)]
public sealed class BeginSnapshotEndpointTests(MustaskApiFactory factory) : IAsyncLifetime
{
    private readonly PushTestClient _push = new(factory);

    public Task InitializeAsync() => ResetAsync(factory);

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task<(HttpStatusCode Status, SnapshotStateBody? Body, string? ErrorCode)> BeginAsync(Guid id, long start)
    {
        using var response = await _push.BeginAsync(id, start);

        return response.IsSuccessStatusCode
            ? (response.StatusCode, await PushTestClient.ReadAsync<SnapshotStateBody>(response), null)
            : (response.StatusCode, null, await PushTestClient.ErrorCodeAsync(response));
    }

    [Fact]
    public async Task Begin_NewSnapshot_IsOpen()
    {
        var id = Guid.NewGuid();

        var (status, body, _) = await BeginAsync(id, BaseMoment);

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal(new SnapshotStateBody(id, BaseMoment, "open"), body);
    }

    [Fact]
    public async Task Begin_SameIdWithTheSameStart_AnswersItsCurrentState()
    {
        var id = Guid.NewGuid();
        await BeginAsync(id, BaseMoment);

        var (status, body, _) = await BeginAsync(id, BaseMoment);

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal(new SnapshotStateBody(id, BaseMoment, "open"), body);
    }

    [Fact]
    public async Task Begin_SameIdWithAnotherStart_IsAConflict()
    {
        var id = Guid.NewGuid();
        await BeginAsync(id, BaseMoment);

        var (status, _, code) = await BeginAsync(id, BaseMoment + 10);

        Assert.Equal(HttpStatusCode.Conflict, status);
        Assert.Equal(UserDirectoryErrorCodes.SnapshotConflict, code);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public async Task Begin_NewIdNotNewerThanTheNewestBegun_IsAConflictAndSupersedesNothing(long offset)
    {
        var newest = Guid.NewGuid();
        await BeginAsync(newest, BaseMoment);

        var (status, _, code) = await BeginAsync(Guid.NewGuid(), BaseMoment + offset);

        Assert.Equal(HttpStatusCode.Conflict, status);
        Assert.Equal(UserDirectoryErrorCodes.SnapshotConflict, code);
        Assert.Equal("open", (await BeginAsync(newest, BaseMoment)).Body!.State);
    }

    [Fact]
    public async Task Begin_NewerSnapshot_SupersedesTheOpenOnes()
    {
        var older = Guid.NewGuid();
        var newer = Guid.NewGuid();
        await BeginAsync(older, BaseMoment);

        var (status, body, _) = await BeginAsync(newer, BaseMoment + 1);

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal("open", body!.State);
        Assert.Equal("superseded", (await BeginAsync(older, BaseMoment)).Body!.State);
    }

    [Fact]
    public async Task Begin_CompletedSnapshotAgain_AnswersCompleted()
    {
        var id = Guid.NewGuid();
        await BeginAsync(id, BaseMoment);
        using (var completed = await _push.CompleteAsync(id, 0, 0))
        {
            completed.EnsureSuccessStatusCode();
        }

        Assert.Equal("completed", (await BeginAsync(id, BaseMoment)).Body!.State);
    }

    [Theory]
    [InlineData("""{}""", "user_directory.value_required")]
    [InlineData("""{"enumerationStart":null}""", "user_directory.value_required")]
    [InlineData("""{"enumerationStart":0}""", "user_directory.value_invalid")]
    [InlineData("""{"enumerationStart":-1}""", "user_directory.value_invalid")]
    public async Task Begin_EnumerationStartMissingOrNotPositive_IsRefused(string body, string reason)
    {
        using var response = await _push.SendAsync(PushTestClient.Build(HttpMethod.Put, $"snapshots/{Guid.NewGuid()}", body));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await PushTestClient.ProblemAsync(response);
        Assert.Equal(UserDirectoryErrorCodes.ValidationFailed, problem.GetProperty("errorCode").GetString());
        Assert.Equal(reason, problem.GetProperty("errors").GetProperty("enumerationStart")[0].GetProperty("code").GetString());
    }

    [Fact]
    public async Task Begin_EnumerationStartMoreThanAnHourAhead_IsRefused()
    {
        var farFuture = UserPushValidation.MicrosecondsSinceEpoch(DateTimeOffset.UtcNow.AddHours(2));

        using var response = await _push.BeginAsync(Guid.NewGuid(), farFuture);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await PushTestClient.ProblemAsync(response);
        Assert.Equal(UserDirectoryErrorCodes.ValueInvalid, problem.GetProperty("errors").GetProperty("enumerationStart")[0].GetProperty("code").GetString());
    }

    [Fact]
    public async Task Begin_SnapshotIdNotAGuid_MatchesNoRoute()
    {
        using var response = await _push.SendAsync(PushTestClient.Build(HttpMethod.Put, "snapshots/not-a-guid", """{"enumerationStart":1}"""));

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    // plan § 4: snapshot rows are pruned at each begin to the newest few, their pages with them.
    [Fact]
    public async Task Begin_ManySnapshots_KeepsOnlyTheNewestTwenty()
    {
        var first = Guid.NewGuid();
        await BeginAsync(first, BaseMoment);
        using (var page = await _push.PageAsync(first, 0, Push(NewId(), "Анна", "Первая", null)))
        {
            page.EnsureSuccessStatusCode();
        }

        for (var i = 1; i <= UserDirectoryLimits.SnapshotsKept; i++)
        {
            Assert.Equal(HttpStatusCode.OK, (await BeginAsync(Guid.NewGuid(), BaseMoment + i)).Status);
        }

        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<MustaskDbContext>();

        Assert.Equal(UserDirectoryLimits.SnapshotsKept, await context.DirectorySnapshots.CountAsync());
        Assert.False(await context.DirectorySnapshots.AnyAsync(snapshot => snapshot.Id == first));
        Assert.False(await context.DirectorySnapshotPages.AnyAsync(page => page.SnapshotId == first));
    }
}
