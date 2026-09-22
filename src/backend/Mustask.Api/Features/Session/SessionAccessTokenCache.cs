using System.Collections.Concurrent;

namespace Mustask.Api.Features.Session;

/// <summary>
/// In-process cache of the access token handed out by
/// <c>POST /api/session/token</c>, keyed by <c>sid</c> (the id token's
/// session id): it survives only the process, not a
/// restart. A restart is not a lost session: the next
/// <c>POST /api/session/token</c> simply misses the cache and renews from
/// the refresh token already inside the cookie.
/// </summary>
public sealed class SessionAccessTokenCache(TimeProvider clock)
{
    public readonly record struct Entry(string AccessToken, DateTimeOffset ExpiresAtUtc);

    private readonly ConcurrentDictionary<string, Entry> _entries = new(StringComparer.Ordinal);

    /// <summary>Test hook only — proves entries are actually evicted, not merely made unreachable by <see cref="TryGetFresh"/>.</summary>
    internal int Count => _entries.Count;

    /// <summary>
    /// Every write is also the cheapest place to bound
    /// growth — a fixed-size cache/eviction policy is more machinery than a
    /// per-process token cache needs, but never sweeping at all let a dead
    /// entry (one <c>sid</c> per sign-in, never removed before this fix)
    /// accumulate for as long as the process runs.
    /// </summary>
    public void Set(string sid, string accessToken, DateTimeOffset expiresAtUtc)
    {
        _entries[sid] = new Entry(accessToken, expiresAtUtc);
        SweepExpired();
    }

    /// <summary>Called on logout and on <c>invalid_grant</c> — a token must not outlive the session that owned it.</summary>
    public void Remove(string sid) => _entries.TryRemove(sid, out _);

    /// <summary>
    /// <see langword="true"/> only if a cached token exists for <paramref name="sid"/>
    /// AND still has more than <paramref name="threshold"/> left before it expires
    /// (renewal triggers under 60 s remaining).
    /// </summary>
    public bool TryGetFresh(string sid, TimeSpan threshold, out Entry entry)
    {
        if (_entries.TryGetValue(sid, out entry) && entry.ExpiresAtUtc - clock.GetUtcNow() > threshold)
        {
            return true;
        }

        entry = default;
        return false;
    }

    /// <summary>
    /// Removes only the exact snapshot this
    /// sweep observed — <c>TryRemove(KeyValuePair)</c> is a compare-and-swap
    /// against both key and value. A plain <c>TryRemove(sid, out _)</c> would
    /// delete whatever is stored under <paramref name="sid"/> at the moment
    /// of removal, including a fresh entry a concurrent <see cref="Set"/>
    /// wrote after the snapshot was taken — reopening the renewal race (a
    /// stale refresh token reaching Keycloak, and refresh-token reuse
    /// detection tearing down the whole session).
    /// <see cref="Remove"/>, by contrast, is an unconditional invalidation
    /// (logout, <c>invalid_grant</c>, a rejected renewed id token) — the
    /// caller means "whatever is cached for this session is no longer valid,
    /// no matter what it is", so the plain by-key removal there is correct
    /// and is not changed.
    /// </summary>
    private void SweepExpired() => SweepSnapshot(_entries);

    /// <summary>
    /// Test hook only — the actual sweep in
    /// <see cref="SweepExpired"/> always passes its own live snapshot of
    /// <see cref="_entries"/> (<c>foreach</c> over a <see cref="ConcurrentDictionary{TKey,TValue}"/>
    /// enumerates a point-in-time snapshot already). Letting a test supply a
    /// snapshot it built itself is what makes the compare-and-swap
    /// deterministic to prove: build a snapshot that says "sid X held this
    /// stale entry", then, before sweeping it, overwrite sid X with a fresh
    /// entry the way a concurrent <see cref="Set"/> would — the correct
    /// removal call must see the mismatch and leave the fresh entry alone.
    /// </summary>
    internal void SweepSnapshot(IEnumerable<KeyValuePair<string, Entry>> snapshot)
    {
        var now = clock.GetUtcNow();

        foreach (var (sid, entry) in snapshot)
        {
            if (entry.ExpiresAtUtc <= now)
            {
                _entries.TryRemove(KeyValuePair.Create(sid, entry));
            }
        }
    }
}
