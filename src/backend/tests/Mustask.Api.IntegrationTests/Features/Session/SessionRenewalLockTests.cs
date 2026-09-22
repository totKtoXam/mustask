using Mustask.Api.Features.Session;

namespace Mustask.Api.IntegrationTests.Features.Session;

/// <summary>
/// Unit-level (no host) coverage of
/// <see cref="SessionRenewalLock"/>'s bound memory — before this fix, a
/// <c>ConcurrentDictionary&lt;string, SemaphoreSlim&gt;</c> grew one entry per
/// sign-in, forever, because a semaphore cannot be safely removed the moment
/// its own waiter releases it.
/// </summary>
public sealed class SessionRenewalLockTests
{
    [Fact]
    public async Task AcquireAsync_ManyDistinctSessionKeys_CapacityDoesNotGrow()
    {
        var subject = new SessionRenewalLock();
        var capacityBefore = subject.Capacity;

        for (var i = 0; i < 1000; i++)
        {
            await using var release = await subject.AcquireAsync($"session-{i}", CancellationToken.None);
        }

        Assert.Equal(capacityBefore, subject.Capacity);
    }

    /// <summary>Correctness must survive the switch from one semaphore per key to a fixed bucket array: the same key is still mutually exclusive.</summary>
    [Fact]
    public async Task AcquireAsync_SameSessionKey_SerializesConcurrentCallers()
    {
        var subject = new SessionRenewalLock();
        var concurrentHolders = 0;
        var maxObservedConcurrentHolders = 0;
        var gate = new object();

        async Task WorkAsync()
        {
            await using var release = await subject.AcquireAsync("same-session", CancellationToken.None);

            lock (gate)
            {
                concurrentHolders++;
                maxObservedConcurrentHolders = Math.Max(maxObservedConcurrentHolders, concurrentHolders);
            }

            await Task.Delay(10);

            lock (gate)
            {
                concurrentHolders--;
            }
        }

        await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => WorkAsync()));

        Assert.Equal(1, maxObservedConcurrentHolders);
    }
}
