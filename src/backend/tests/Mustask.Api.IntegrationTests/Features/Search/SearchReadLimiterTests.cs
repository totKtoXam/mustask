using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http;
using Mustask.Api.Features.Boards;
using Mustask.Api.Features.Search;
using Mustask.Application.Abstractions;

namespace Mustask.Api.IntegrationTests.Features.Search;

/// <summary>
/// feat-071 (ADR-0044 § 4, feat-065 design D-7 and BR-GS-15): the quick search's limiter — 3 reads in flight a user with a queue of 3,
/// 24 in all with a queue of 12, a wait of 1 second by the injected clock, 429 with <c>Retry-After: 1</c> past that, and permits of its
/// own: a user holding every group-read permit is not delayed in search. No host and no database.
/// </summary>
public sealed class SearchReadLimiterTests
{
    private static readonly CancellationToken NoCancel = CancellationToken.None;

    [Fact]
    public async Task OneUser_HoldsThreeReads_AndTheFourthWaitsForARelease()
    {
        using var limiter = new SearchReadLimiter(new ManualClock());
        var held = await HoldAsync(limiter, "u", SearchReadLimiter.PerUserPermits);

        var waiting = limiter.TryEnterAsync("u", NoCancel).AsTask();
        Assert.False(waiting.IsCompleted);

        held[0].Dispose();

        Assert.NotNull(await waiting.WaitAsync(TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public async Task TheSeventhRead_FindsTheQueueFullAndIsRefusedAtOnce()
    {
        using var limiter = new SearchReadLimiter(new ManualClock());
        await HoldAsync(limiter, "u", SearchReadLimiter.PerUserPermits);
        var queued = Enumerable.Range(0, SearchReadLimiter.PerUserQueue).Select(_ => limiter.TryEnterAsync("u", NoCancel).AsTask()).ToList();

        var refused = await limiter.TryEnterAsync("u", NoCancel);

        Assert.Null(refused);
        Assert.All(queued, task => Assert.False(task.IsCompleted));
    }

    [Fact]
    public async Task AReadThatWaitsOneSecond_IsRefused_AndNotBefore()
    {
        var clock = new ManualClock();
        using var limiter = new SearchReadLimiter(clock);
        await HoldAsync(limiter, "u", SearchReadLimiter.PerUserPermits);

        var waiting = limiter.TryEnterAsync("u", NoCancel).AsTask();
        clock.Advance(SearchReadLimiter.MaxWait - TimeSpan.FromMilliseconds(1));
        Assert.False(waiting.IsCompleted);

        clock.Advance(TimeSpan.FromMilliseconds(1));

        Assert.Null(await waiting.WaitAsync(TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public async Task OneUser_CannotTakeThePermitsOfTheProcess_AndAnotherUserIsNotHeld()
    {
        using var limiter = new SearchReadLimiter(new ManualClock());
        await HoldAsync(limiter, "u", SearchReadLimiter.PerUserPermits);

        Assert.NotNull(await limiter.TryEnterAsync("v", NoCancel));
    }

    [Fact]
    public async Task TheProcess_HoldsTwentyFourReads_The25thQueues_AndPastTheQueueIsRefused()
    {
        using var limiter = new SearchReadLimiter(new ManualClock());
        var held = new List<RateLimitLease>();

        for (var user = 0; held.Count < SearchReadLimiter.GlobalPermits; user++)
        {
            held.AddRange(await HoldAsync(limiter, $"user-{user}", SearchReadLimiter.PerUserPermits));
        }

        Assert.Equal(SearchReadLimiter.GlobalPermits, held.Count);

        // The 25th..36th reads of different users queue (12 places); the 37th is refused.
        var queued = Enumerable.Range(0, SearchReadLimiter.GlobalQueue).Select(index => limiter.TryEnterAsync($"queued-{index}", NoCancel).AsTask()).ToList();
        Assert.All(queued, task => Assert.False(task.IsCompleted));

        Assert.Null(await limiter.TryEnterAsync("one-too-many", NoCancel));

        held[0].Dispose();

        Assert.NotNull(await queued[0].WaitAsync(TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public async Task ACancelledRequest_LeavesTheQueue_AndFreesItsPlace()
    {
        using var limiter = new SearchReadLimiter(new ManualClock());
        await HoldAsync(limiter, "u", SearchReadLimiter.PerUserPermits);
        using var aborted = new CancellationTokenSource();

        var waiting = limiter.TryEnterAsync("u", aborted.Token).AsTask();
        await aborted.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiting.WaitAsync(TimeSpan.FromSeconds(5)));

        // The queue has its three places again: three more wait, the fourth is refused.
        var queued = Enumerable.Range(0, SearchReadLimiter.PerUserQueue).Select(_ => limiter.TryEnterAsync("u", NoCancel).AsTask()).ToList();
        Assert.Null(await limiter.TryEnterAsync("u", NoCancel));
        Assert.All(queued, task => Assert.False(task.IsCompleted));
    }

    [Fact]
    public async Task TheFilter_Refuses_With429_AndRetryAfterOne_AndNoBody()
    {
        using var limiter = new SearchReadLimiter(new ManualClock());
        await HoldAsync(limiter, "u", SearchReadLimiter.PerUserPermits);
        _ = Enumerable.Range(0, SearchReadLimiter.PerUserQueue).Select(_ => limiter.TryEnterAsync("u", NoCancel).AsTask()).ToList();
        var http = new DefaultHttpContext();
        var reached = false;

        var result = await new SearchReadLimitFilter(limiter, new Subject("u")).InvokeAsync(
            new DefaultEndpointFilterInvocationContext(http),
            _ =>
            {
                reached = true;

                return ValueTask.FromResult<object?>(Results.Ok());
            });

        Assert.False(reached);
        Assert.Equal(StatusCodes.Status429TooManyRequests, Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.StatusCodeHttpResult>(result).StatusCode);
        Assert.Equal("1", http.Response.Headers.RetryAfter.ToString());
    }

    [Fact]
    public async Task TheFilter_LetsAReadThrough_AndReleasesItsPlaceAtTheEnd()
    {
        using var limiter = new SearchReadLimiter(new ManualClock());
        var filter = new SearchReadLimitFilter(limiter, new Subject("u"));

        for (var round = 0; round < SearchReadLimiter.PerUserPermits * 2; round++)
        {
            var result = await filter.InvokeAsync(new DefaultEndpointFilterInvocationContext(new DefaultHttpContext()), _ => ValueTask.FromResult<object?>("done"));

            Assert.Equal("done", result);
        }
    }

    // ADR-0044 § 4 "independent of ADR-0036's limiter": a user holding every group-read permit and filling its queue is not delayed in search.
    [Fact]
    public async Task TheTwoLimiters_ShareNoPermits()
    {
        var clock = new ManualClock();
        using var aggregate = new AggregateReadLimiter(clock);
        using var search = new SearchReadLimiter(clock);
        var aggregateLeases = new List<RateLimitLease>();

        for (var index = 0; index < AggregateReadLimiter.PerUserPermits; index++)
        {
            aggregateLeases.Add((await aggregate.TryEnterAsync("u", NoCancel))!);
        }

        var lease = await search.TryEnterAsync("u", NoCancel).AsTask().WaitAsync(TimeSpan.FromSeconds(5));

        Assert.NotNull(lease);
        Assert.Equal(AggregateReadLimiter.PerUserPermits, aggregateLeases.Count);
    }

    private static async Task<List<RateLimitLease>> HoldAsync(SearchReadLimiter limiter, string subject, int count)
    {
        var leases = new List<RateLimitLease>();

        for (var index = 0; index < count; index++)
        {
            leases.Add((await limiter.TryEnterAsync(subject, NoCancel))!);
        }

        return leases;
    }

    private sealed class Subject(string subject) : ICurrentUser
    {
        public string DisplayName => subject;

        public string SubjectId => subject;
    }

    /// <summary>A clock whose timers fire only when the test advances it.</summary>
    private sealed class ManualClock : TimeProvider
    {
        private readonly List<ManualTimer> _timers = [];

        private DateTimeOffset _now = new(2026, 11, 17, 0, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => _now;

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            var timer = new ManualTimer(callback, state, _now + dueTime);

            lock (_timers)
            {
                _timers.Add(timer);
            }

            return timer;
        }

        public void Advance(TimeSpan by)
        {
            _now += by;
            List<ManualTimer> due;

            lock (_timers)
            {
                due = _timers.Where(timer => !timer.Disposed && timer.Due <= _now).ToList();
            }

            foreach (var timer in due)
            {
                timer.Fire();
            }
        }
    }

    private sealed class ManualTimer(TimerCallback callback, object? state, DateTimeOffset due) : ITimer
    {
        public DateTimeOffset Due { get; } = due;

        public bool Disposed { get; private set; }

        public void Fire()
        {
            Disposed = true;
            callback(state);
        }

        public bool Change(TimeSpan dueTime, TimeSpan period) => true;

        public void Dispose() => Disposed = true;

        public ValueTask DisposeAsync()
        {
            Disposed = true;

            return ValueTask.CompletedTask;
        }
    }
}
