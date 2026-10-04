using System.Net;
using Mustask.Domain.UserDirectory;
using static Mustask.Api.IntegrationTests.Features.UserDirectory.UserDirectoryTestData;

namespace Mustask.Api.IntegrationTests.Features.UserDirectory;

/// <summary>
/// mod-014 US-5, the concurrent scenarios (BR-UP-6; ADR-0025 § 6): two
/// requests for one user, released together, 50 times — the per-user lock
/// makes the outcome that of some sequential order, so the newest moment wins
/// and a deletion wins over any state.
/// </summary>
[Collection(MustaskApiCollection.Name)]
public sealed class PushConcurrencyTests(MustaskApiFactory factory) : IAsyncLifetime
{
    private const int Repetitions = 50;

    private readonly PushTestClient _push = new(factory);

    public Task InitializeAsync() => ResetAsync(factory);

    public Task DisposeAsync() => Task.CompletedTask;

    private async Task SendTogetherAsync(UserPush first, UserPush second)
    {
        var a = PushTestClient.Build(HttpMethod.Post, "changes", PushTestClient.ChangesBody(first));
        var b = PushTestClient.Build(HttpMethod.Post, "changes", PushTestClient.ChangesBody(second));

        using var gate = new SemaphoreSlim(0);

        async Task<HttpStatusCode> SendAsync(HttpRequestMessage request)
        {
            await gate.WaitAsync();
            using var response = await _push.SendAsync(request);
            return response.StatusCode;
        }

        var sending = new[] { Task.Run(() => SendAsync(a)), Task.Run(() => SendAsync(b)) };
        gate.Release(2);

        var statuses = await Task.WhenAll(sending);
        Assert.All(statuses, status => Assert.Equal(HttpStatusCode.OK, status));
    }

    // US-5 "Concurrent pushes for one user end with the newest state".
    [Fact]
    public async Task ConcurrentPushes_FiftyTimes_TheNewestStateWins()
    {
        for (var i = 0; i < Repetitions; i++)
        {
            var id = NewId();
            var older = Push(id, "Анна", "Смирнова", null, moment: BaseMoment + 1);
            var newer = Push(id, "Анна", "Кузнецова", null, moment: BaseMoment + 2);

            await SendTogetherAsync(i % 2 == 0 ? older : newer, i % 2 == 0 ? newer : older);

            Assert.Equal("Анна Кузнецова", (await LookupAsync(factory, id)).Known[id].DisplayName);
        }

        Assert.Equal(Repetitions, await CountAsync(factory));
    }

    // US-5 "A deletion applied concurrently with an 'enabled' push wins" — either moment the later.
    [Fact]
    public async Task ConcurrentDeletionAndEnabled_FiftyTimes_TheUserStaysDeleted()
    {
        for (var i = 0; i < Repetitions; i++)
        {
            var id = NewId();
            var deletionLater = i % 2 == 0;
            var deletion = Push(id, "Ольга", "Белова", null, moment: BaseMoment + (deletionLater ? 2 : 1), deleted: true);
            var enabled = Push(id, "Ольга", "Белова", null, enabled: true, moment: BaseMoment + (deletionLater ? 1 : 2));

            await SendTogetherAsync(i % 4 < 2 ? deletion : enabled, i % 4 < 2 ? enabled : deletion);

            Assert.False((await LookupAsync(factory, id)).Known[id].IsActive);
            Assert.Equal(ProjectedUserLifecycle.Deleted, (await FindAsync(factory, id))!.Lifecycle);

            var later = await _push.PostChangesAsync(Push(id, "Ольга", "Белова", null, enabled: true, moment: BaseMoment + 10));
            Assert.Equal(1, later.HeldAsDeleted);
        }
    }
}
