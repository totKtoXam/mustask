using Mustask.Api.Features.Session;

namespace Mustask.Api.IntegrationTests.Features.Session;

/// <summary>
/// Unit-level (no host) coverage of
/// <see cref="SessionAccessTokenCache"/>'s bound growth — before this fix,
/// nothing ever removed an entry (not logout, not <c>invalid_grant</c>, not
/// expiry), so the cache grew by one entry per sign-in for as long as the
/// process ran.
/// </summary>
public sealed class SessionAccessTokenCacheTests
{
    [Fact]
    public void Remove_DeletesTheEntry()
    {
        var clock = new ManualTimeProvider();
        var cache = new SessionAccessTokenCache(clock);
        cache.Set("session-1", "token-1", clock.GetUtcNow().AddMinutes(5));

        cache.Remove("session-1");

        Assert.Equal(0, cache.Count);
        Assert.False(cache.TryGetFresh("session-1", TimeSpan.Zero, out _));
    }

    /// <summary>Every <see cref="SessionAccessTokenCache.Set"/> also sweeps whatever has already expired — the cheapest place to bound growth without a background timer.</summary>
    [Fact]
    public void Set_SweepsAlreadyExpiredEntries()
    {
        var clock = new ManualTimeProvider();
        var cache = new SessionAccessTokenCache(clock);

        cache.Set("session-old", "token-old", clock.GetUtcNow().AddSeconds(1));
        clock.Advance(TimeSpan.FromSeconds(2));
        Assert.Equal(1, cache.Count);

        cache.Set("session-new", "token-new", clock.GetUtcNow().AddMinutes(5));

        Assert.Equal(1, cache.Count);
        Assert.False(cache.TryGetFresh("session-old", TimeSpan.Zero, out _));
        Assert.True(cache.TryGetFresh("session-new", TimeSpan.Zero, out _));
    }

    [Fact]
    public void TryGetFresh_EntryWithinThreshold_ReturnsFalse()
    {
        var clock = new ManualTimeProvider();
        var cache = new SessionAccessTokenCache(clock);
        cache.Set("session-1", "token-1", clock.GetUtcNow().AddSeconds(30));

        Assert.False(cache.TryGetFresh("session-1", TimeSpan.FromSeconds(60), out _));
    }

    [Fact]
    public void TryGetFresh_UnknownSession_ReturnsFalse()
    {
        var cache = new SessionAccessTokenCache(new ManualTimeProvider());

        Assert.False(cache.TryGetFresh("unknown", TimeSpan.Zero, out _));
    }

    /// <summary>
    /// A sweep must remove an entry only if it
    /// is still the exact value the sweep observed. This reproduces the race
    /// without real threads: build a snapshot holding the stale entry a
    /// sweep would have captured, then — before sweeping it — overwrite the
    /// same sid with a fresh entry the way a concurrent renewal's
    /// <see cref="SessionAccessTokenCache.Set"/> would. Before the fix,
    /// <c>SweepExpired</c> removed by key alone
    /// (<c>_entries.TryRemove(sid, out _)</c>) and this fresh entry would be
    /// deleted regardless — reopening the race where a stale refresh
    /// token reaches Keycloak and tears down the whole session.
    /// </summary>
    [Fact]
    public void SweepSnapshot_DoesNotRemoveAnEntryThatChangedSinceTheSnapshot()
    {
        var clock = new ManualTimeProvider();
        var cache = new SessionAccessTokenCache(clock);

        cache.Set("session-1", "token-old", clock.GetUtcNow().AddSeconds(1));
        var staleSnapshot = new[]
        {
            new KeyValuePair<string, SessionAccessTokenCache.Entry>(
                "session-1", new SessionAccessTokenCache.Entry("token-old", clock.GetUtcNow().AddSeconds(1))),
        };

        clock.Advance(TimeSpan.FromSeconds(2));
        // A concurrent renewal wrote a fresh entry for the same sid after the
        // snapshot above was taken, but before the sweep below runs.
        cache.Set("session-1", "token-new", clock.GetUtcNow().AddMinutes(5));

        cache.SweepSnapshot(staleSnapshot);

        Assert.True(cache.TryGetFresh("session-1", TimeSpan.Zero, out var entry));
        Assert.Equal("token-new", entry.AccessToken);
    }

    /// <summary>Minimal hand-rolled fake — no test-only TimeProvider package is referenced by this project.</summary>
    private sealed class ManualTimeProvider : TimeProvider
    {
        private DateTimeOffset _now = DateTimeOffset.UtcNow;

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan delta) => _now += delta;
    }
}
