using System.Globalization;
using System.Threading.RateLimiting;
using Mustask.Application.Abstractions;

namespace Mustask.Api.Features.Boards;

/// <summary>
/// The read limiter of the aggregate views (feat-048 OQ-2, plan-backend): the group and cell reads of one API process share 40
/// concurrent reads, and one user (the Keycloak subject) holds 6 of them. Past the permits a read waits in a queue (6 a user, 20 in
/// all) for at most 3 seconds by the injected clock; a read that finds the queue full or waits out the time is refused with 429
/// and <c>Retry-After: 2</c>. The wait is cancelled with the request too, never left to hold a place.
/// </summary>
public sealed class AggregateReadLimiter : IDisposable
{
    public const int PerUserPermits = 6;

    public const int PerUserQueue = 6;

    public const int GlobalPermits = 40;

    public const int GlobalQueue = 20;

    public static readonly TimeSpan MaxWait = TimeSpan.FromSeconds(3);

    private readonly PartitionedRateLimiter<string> _limiter;

    private readonly TimeProvider _clock;

    public AggregateReadLimiter(TimeProvider clock)
    {
        _clock = clock;
        _limiter = PartitionedRateLimiter.CreateChained(
            PartitionedRateLimiter.Create<string, string>(subject => RateLimitPartition.GetConcurrencyLimiter(
                subject,
                _ => new ConcurrencyLimiterOptions
                {
                    PermitLimit = PerUserPermits,
                    QueueLimit = PerUserQueue,
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                })),
            PartitionedRateLimiter.Create<string, string>(_ => RateLimitPartition.GetConcurrencyLimiter(
                "all",
                _ => new ConcurrencyLimiterOptions
                {
                    PermitLimit = GlobalPermits,
                    QueueLimit = GlobalQueue,
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                })));
    }

    /// <summary>The lease to dispose when the read ends, or <c>null</c> when the read is refused.</summary>
    public async ValueTask<RateLimitLease?> TryEnterAsync(string subject, CancellationToken requestAborted)
    {
        using var wait = new CancellationTokenSource(MaxWait, _clock);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(requestAborted, wait.Token);

        try
        {
            var lease = await _limiter.AcquireAsync(subject, 1, linked.Token);

            return lease.IsAcquired ? lease : null;
        }
        catch (OperationCanceledException) when (!requestAborted.IsCancellationRequested)
        {
            return null;
        }
    }

    public void Dispose() => _limiter.Dispose();
}

/// <summary>The endpoint filter that holds a read to <see cref="AggregateReadLimiter"/>: after authentication, so the subject is known.</summary>
public sealed class AggregateReadLimitFilter(AggregateReadLimiter limiter, ICurrentUser currentUser) : IEndpointFilter
{
    public const int RetryAfterSeconds = 2;

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        using var lease = await limiter.TryEnterAsync(currentUser.SubjectId, context.HttpContext.RequestAborted);

        if (lease is null)
        {
            // An empty body: UseStatusCodePages turns it into the problem+json with http.too_many_requests.
            context.HttpContext.Response.Headers.RetryAfter = RetryAfterSeconds.ToString(CultureInfo.InvariantCulture);

            return Results.StatusCode(StatusCodes.Status429TooManyRequests);
        }

        return await next(context);
    }
}
