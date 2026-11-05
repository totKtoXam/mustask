using Microsoft.AspNetCore.Http;
using Mustask.Api.Features.Boards;
using Mustask.Application.Abstractions;

namespace Mustask.Api.IntegrationTests.Features.Boards;

/// <summary>
/// The read limiter of the aggregate views (feat-048 OQ-2, plan-backend): 6 concurrent reads a user and 40 in all, a queue of 6 a
/// user and 20 in all, a wait of at most 3 seconds by the injected clock, and 429 with <c>Retry-After: 2</c> past that. No host and
/// no database: the limiter and its filter are built here and the clock is the test's.
/// </summary>
public sealed class AggregateReadLimiterTests
{
    private static readonly CancellationToken NoCancel = CancellationToken.None;

    [Fact]
    public async Task OneUser_HoldsSixReads_AndTheSeventhWaitsForARelease()
    {
        var clock = new ManualClock();
        using var limiter = new AggregateReadLimiter(clock);
        var held = await HoldAsync(limiter, "u", AggregateReadLimiter.PerUserPermits);

        var waiting = limiter.TryEnterAsync("u", NoCancel).AsTask();
        Assert.False(waiting.IsCompleted);

        held[0].Dispose();
        var lease = await waiting.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.NotNull(lease);
    }

    [Fact]
    public async Task AReadThatWaitsThreeSeconds_IsRefused()
    {
        var clock = new ManualClock();
        using var limiter = new AggregateReadLimiter(clock);
        await HoldAsync(limiter, "u", AggregateReadLimiter.PerUserPermits);

        var waiting = limiter.TryEnterAsync("u", NoCancel).AsTask();
        clock.Advance(AggregateReadLimiter.MaxWait - TimeSpan.FromMilliseconds(1));
        Assert.False(waiting.IsCompleted);

        clock.Advance(TimeSpan.FromMilliseconds(1));

        Assert.Null(await waiting.WaitAsync(TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public async Task AFullQueue_RefusesAtOnce()
    {
        var clock = new ManualClock();
        using var limiter = new AggregateReadLimiter(clock);
        await HoldAsync(limiter, "u", AggregateReadLimiter.PerUserPermits);
        var queued = Enumerable.Range(0, AggregateReadLimiter.PerUserQueue).Select(_ => limiter.TryEnterAsync("u", NoCancel).AsTask()).ToList();

        var refused = await limiter.TryEnterAsync("u", NoCancel);

        Assert.Null(refused);
        Assert.All(queued, task => Assert.False(task.IsCompleted));
    }

    [Fact]
    public async Task AnotherUser_IsNotHeldByTheFirst()
    {
        var clock = new ManualClock();
        using var limiter = new AggregateReadLimiter(clock);
        await HoldAsync(limiter, "u", AggregateReadLimiter.PerUserPermits);

        var other = await limiter.TryEnterAsync("v", NoCancel);

        Assert.NotNull(other);
    }

    [Fact]
    public async Task TheProcess_HoldsFortyReads_AndTheNextWaits()
    {
        var clock = new ManualClock();
        using var limiter = new AggregateReadLimiter(clock);
        var held = new List<System.Threading.RateLimiting.RateLimitLease>();

        for (var user = 0; user < 7; user++)
        {
            held.AddRange(await HoldAsync(limiter, $"user-{user}", user < 6 ? AggregateReadLimiter.PerUserPermits : AggregateReadLimiter.GlobalPermits - 36));
        }

        Assert.Equal(AggregateReadLimiter.GlobalPermits, held.Count);

        var waiting = limiter.TryEnterAsync("user-8", NoCancel).AsTask();
        Assert.False(waiting.IsCompleted);

        held[0].Dispose();

        Assert.NotNull(await waiting.WaitAsync(TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public async Task ARequestThatIsAborted_LeavesTheQueue_AndIsNotRefused()
    {
        var clock = new ManualClock();
        using var limiter = new AggregateReadLimiter(clock);
        await HoldAsync(limiter, "u", AggregateReadLimiter.PerUserPermits);
        using var aborted = new CancellationTokenSource();

        var waiting = limiter.TryEnterAsync("u", aborted.Token).AsTask();
        await aborted.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiting.WaitAsync(TimeSpan.FromSeconds(5)));
    }

    [Fact]
    public async Task TheFilter_Refuses_With429_AndRetryAfterTwo_WhenTheUserIsOverTheLimit()
    {
        var clock = new ManualClock();
        using var limiter = new AggregateReadLimiter(clock);
        await HoldAsync(limiter, "u", AggregateReadLimiter.PerUserPermits);
        _ = Enumerable.Range(0, AggregateReadLimiter.PerUserQueue).Select(_ => limiter.TryEnterAsync("u", NoCancel).AsTask()).ToList();
        var http = new DefaultHttpContext();
        var filter = new AggregateReadLimitFilter(limiter, new Subject("u"));
        var reached = false;

        var result = await filter.InvokeAsync(
            new DefaultEndpointFilterInvocationContext(http),
            _ =>
            {
                reached = true;

                return ValueTask.FromResult<object?>(Results.Ok());
            });

        Assert.False(reached);
        Assert.Equal(StatusCodes.Status429TooManyRequests, Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.StatusCodeHttpResult>(result).StatusCode);
        Assert.Equal("2", http.Response.Headers.RetryAfter.ToString());
    }

    [Fact]
    public async Task TheFilter_LetsTheReadThrough_AndReleasesItsPlaceAtTheEnd()
    {
        var clock = new ManualClock();
        using var limiter = new AggregateReadLimiter(clock);
        var filter = new AggregateReadLimitFilter(limiter, new Subject("u"));

        for (var round = 0; round < AggregateReadLimiter.PerUserPermits * 2; round++)
        {
            var result = await filter.InvokeAsync(
                new DefaultEndpointFilterInvocationContext(new DefaultHttpContext()),
                _ => ValueTask.FromResult<object?>("done"));

            Assert.Equal("done", result);
        }
    }

    private static async Task<List<System.Threading.RateLimiting.RateLimitLease>> HoldAsync(AggregateReadLimiter limiter, string subject, int count)
    {
        var leases = new List<System.Threading.RateLimiting.RateLimitLease>();

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

        private DateTimeOffset _now = new(2026, 11, 3, 0, 0, 0, TimeSpan.Zero);

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
        public DateTimeOffset Due { get; private set; } = due;

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
