namespace Mustask.Api.Features.Session;

/// <summary>
/// Serializes refresh-grant renewal per session (ADR-0022 Decision 5): the
/// only point of serialization for concurrent renewal is an in-process
/// semaphore, correct as long as <c>Mustask.Api</c> is one process. A
/// second instance needs a shared lock and key ring together — that trigger
/// is named in ADR-0022 as grounds to supersede this module, not to patch it.
/// </summary>
/// <remarks>
/// A <c>ConcurrentDictionary&lt;string, SemaphoreSlim&gt;</c>
/// keyed by session grew one entry per sign-in, forever — nothing ever
/// removed one (a semaphore cannot be safely removed the moment its own
/// waiter releases it without a separate reference count). A fixed-size
/// array of semaphores, indexed by a hash of the key, bounds memory
/// unconditionally instead: two unrelated sessions landing in the same
/// bucket only serialize a bit more than strictly necessary, which is a
/// performance trade-off, not a correctness one — renewal is already rare
/// and already serialized per session by design.
/// </remarks>
public sealed class SessionRenewalLock
{
    private const int BucketCount = 256;

    private readonly SemaphoreSlim[] _buckets =
        Enumerable.Range(0, BucketCount).Select(static _ => new SemaphoreSlim(1, 1)).ToArray();

    /// <summary>Test hook only — proves the array never grows no matter how many distinct session keys are used.</summary>
    internal int Capacity => _buckets.Length;

    public async Task<IAsyncDisposable> AcquireAsync(string sessionKey, CancellationToken cancellationToken)
    {
        var semaphore = _buckets[Bucket(sessionKey)];
        await semaphore.WaitAsync(cancellationToken);
        return new Releaser(semaphore);
    }

    private static int Bucket(string sessionKey) =>
        (int)((uint)StringComparer.Ordinal.GetHashCode(sessionKey) % BucketCount);

    private sealed class Releaser(SemaphoreSlim semaphore) : IAsyncDisposable
    {
        public ValueTask DisposeAsync()
        {
            semaphore.Release();
            return ValueTask.CompletedTask;
        }
    }
}
