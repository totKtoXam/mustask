using System.Globalization;
using System.Threading.RateLimiting;
using Mustask.Application.Abstractions;

namespace Mustask.Api.Features.Search;

/// <summary>
/// The read limiter of the quick search (ADR-0044 § 4, feat-065 design D-7): a structural copy of ADR-0036's
/// <c>AggregateReadLimiter</c> with the search's numbers, and an instance of its own — no permits are shared with the group reads,
/// so neither read class starves the other. One user (the Keycloak subject, across organizations and tabs) holds 3 reads in flight
/// with a queue of 3; the process holds 24 with a queue of 12; a read waits at most 1 second by the injected clock; a read that
/// finds the queue full or waits out the time is refused with 429 and <c>Retry-After: 1</c>. The wait is cancelled with the request.
/// </summary>
public sealed class SearchReadLimiter : IDisposable
{
    public const int PerUserPermits = 3;

    public const int PerUserQueue = 3;

    public const int GlobalPermits = 24;

    public const int GlobalQueue = 12;

    public static readonly TimeSpan MaxWait = TimeSpan.FromSeconds(1);

    private readonly PartitionedRateLimiter<string> _limiter;

    private readonly TimeProvider _clock;

    public SearchReadLimiter(TimeProvider clock)
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

/// <summary>The endpoint filter that holds a search to <see cref="SearchReadLimiter"/>: after authentication and the organization gate, so the subject is known.</summary>
public sealed class SearchReadLimitFilter(SearchReadLimiter limiter, ICurrentUser currentUser) : IEndpointFilter
{
    public const int RetryAfterSeconds = 1;

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
