namespace Mustask.Domain.UserDirectory;

/// <summary>
/// One user of realm <c>mustask</c> as last received from Keycloak — the
/// aggregate of the user projection (mod-014; BR-UP-1..8; api-contract.md
/// B.5, the effects table). The provider is its only writer (BR-UP-1): this
/// type is changed only through <see cref="ProjectionBatch"/> and
/// <see cref="MarkAbsent"/>, by the push channel's use cases — and
/// <see cref="RefreshSortKey"/> re-derives a stale key without changing state.
/// </summary>
/// <remarks>
/// Ordering (ADR-0025 § 6): a state is replaced only by a push whose
/// <see cref="Moment"/> is strictly greater; a reported deletion is final
/// whatever its moment; an absence is held with the snapshot's enumeration
/// start as its moment, so any newer push replaces it and any older one is
/// ignored.
/// </remarks>
public sealed class ProjectedUser
{
    private ProjectedUser()
    {
        // EF materialization.
    }

    /// <summary>The Keycloak user id; opaque text, compared ordinally.</summary>
    public string Id { get; private set; } = null!;

    public string Username { get; private set; } = null!;

    /// <summary>Empty when Keycloak holds none.</summary>
    public string FirstName { get; private set; } = string.Empty;

    /// <summary>Empty when Keycloak holds none.</summary>
    public string LastName { get; private set; } = string.Empty;

    /// <summary>As Keycloak holds it, letter case kept; empty when none.</summary>
    public string Email { get; private set; } = string.Empty;

    public bool Enabled { get; private set; }

    public ProjectedUserLifecycle Lifecycle { get; private set; }

    /// <summary>The marker of the state held (ADR-0025 § 6).</summary>
    public long Moment { get; private set; }

    /// <summary>BR-UP-3, derived.</summary>
    public string DisplayName { get; private set; } = null!;

    /// <summary><see cref="DisplayName"/> through <see cref="DirectorySearchText.Normalize"/>, derived.</summary>
    public string SearchName { get; private set; } = null!;

    /// <summary><see cref="Email"/> through <see cref="DirectorySearchText.Normalize"/>, derived.</summary>
    public string SearchEmail { get; private set; } = null!;

    /// <summary><see cref="DirectoryCollation.SortKey"/> of <see cref="DisplayName"/>, derived.</summary>
    public byte[] SortKey { get; private set; } = [];

    /// <summary>The <see cref="DirectoryCollation.Version"/> that produced <see cref="SortKey"/>.</summary>
    public string SortKeyVersion { get; private set; } = null!;

    /// <summary>BR-UP-5: enabled, and neither deleted nor absent. Derived, stored for the search index.</summary>
    public bool IsActive { get; private set; }

    /// <summary>The last snapshot whose page carried this user (ADR-0025 § 5).</summary>
    public Guid? LastSnapshotId { get; private set; }

    /// <summary>When MUSTASK last changed this record; diagnostics only.</summary>
    public DateTimeOffset ChangedAt { get; private set; }

    /// <summary>A push for an id not held: added (a deletion entry adds the user already deleted).</summary>
    public static ProjectedUser Create(UserPush push, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(push);

        var user = new ProjectedUser
        {
            Id = push.Id,
            Lifecycle = push.Deleted ? ProjectedUserLifecycle.Deleted : ProjectedUserLifecycle.Present,
            Moment = push.Moment,
            ChangedAt = now,
        };

        user.TakeProfile(push);
        user.Enabled = push.Enabled && !push.Deleted;
        user.Recompute();

        return user;
    }

    /// <summary>Applies one push to the held state (api-contract.md B.5).</summary>
    public PushOutcome Apply(UserPush push, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(push);

        if (!string.Equals(push.Id, Id, StringComparison.Ordinal))
        {
            throw new ArgumentException($"A push for id '{push.Id}' cannot apply to user '{Id}'.", nameof(push));
        }

        if (Lifecycle == ProjectedUserLifecycle.Deleted)
        {
            // BR-UP-5: final. A second report of the deletion is a duplicate;
            // a state after it is the accepted risk of a restored backup.
            return push.Deleted ? PushOutcome.Unchanged : PushOutcome.HeldAsDeleted;
        }

        if (push.Deleted)
        {
            // A deletion wins over any state, whatever its moment; the
            // carried last state replaces the held one only when newer.
            if (push.Moment > Moment)
            {
                TakeProfile(push);
                Moment = push.Moment;
            }

            Lifecycle = ProjectedUserLifecycle.Deleted;
            Recompute();
            ChangedAt = now;

            return PushOutcome.Changed;
        }

        if (push.Moment <= Moment)
        {
            return PushOutcome.Unchanged;
        }

        var changed = !HasProfileOf(push)
            || Enabled != push.Enabled
            || Lifecycle != ProjectedUserLifecycle.Present;

        TakeProfile(push);
        Enabled = push.Enabled;
        Lifecycle = ProjectedUserLifecycle.Present;
        // Stored even when nothing else changed: otherwise a delayed push
        // from between the old moment and this one could revert the user
        // (ADR-0025 § 6, "A snapshot entry").
        Moment = push.Moment;
        Recompute();

        if (changed)
        {
            ChangedAt = now;
        }

        return changed ? PushOutcome.Changed : PushOutcome.Unchanged;
    }

    /// <summary>Remembers the snapshot whose page carried this user.</summary>
    public void StampSnapshot(Guid snapshotId) => LastSnapshotId = snapshotId;

    /// <summary>
    /// BR-UP-8: a complete snapshot that did not carry this user marks it
    /// absent as of the snapshot's enumeration start — only a present user
    /// whose held state is older than that start. Returns whether it did.
    /// </summary>
    public bool MarkAbsent(long enumerationStart, DateTimeOffset now)
    {
        if (Lifecycle != ProjectedUserLifecycle.Present || Moment >= enumerationStart)
        {
            return false;
        }

        Lifecycle = ProjectedUserLifecycle.Absent;
        Moment = enumerationStart;
        Recompute();
        ChangedAt = now;

        return true;
    }

    /// <summary>
    /// ADR-0025 § 9: recomputes the derived values when <see cref="SortKey"/>
    /// comes from another <see cref="DirectoryCollation.Version"/> — an API
    /// host whose ICU changed. A present user is rewritten by every
    /// reconciliation anyway; a deleted or absent one is never pushed again,
    /// so a snapshot's completion calls this for it. It changes no state: not
    /// the lifecycle, the moment, the profile or <see cref="ChangedAt"/>.
    /// Returns whether the key was stale.
    /// </summary>
    public bool RefreshSortKey()
    {
        if (string.Equals(SortKeyVersion, DirectoryCollation.Version, StringComparison.Ordinal))
        {
            return false;
        }

        Recompute();

        return true;
    }

    private bool HasProfileOf(UserPush push) =>
        string.Equals(Username, push.Username, StringComparison.Ordinal)
        && string.Equals(FirstName, push.FirstName ?? string.Empty, StringComparison.Ordinal)
        && string.Equals(LastName, push.LastName ?? string.Empty, StringComparison.Ordinal)
        && string.Equals(Email, push.Email ?? string.Empty, StringComparison.Ordinal);

    private void TakeProfile(UserPush push)
    {
        Username = push.Username;
        FirstName = push.FirstName ?? string.Empty;
        LastName = push.LastName ?? string.Empty;
        Email = push.Email ?? string.Empty;
    }

    /// <summary>
    /// The derived values. Each is assigned only when it differs, so a write
    /// that changes nothing leaves the row unmodified; a key from another
    /// collation version is recomputed by any write and by
    /// <see cref="RefreshSortKey"/> (ADR-0025 § 9).
    /// </summary>
    private void Recompute()
    {
        var displayName = DisplayNameRule.Compose(FirstName, LastName, Username);

        if (!string.Equals(DisplayName, displayName, StringComparison.Ordinal)
            || !string.Equals(SortKeyVersion, DirectoryCollation.Version, StringComparison.Ordinal))
        {
            DisplayName = displayName;
            SearchName = DirectorySearchText.Normalize(displayName);

            var sortKey = DirectoryCollation.SortKey(displayName);

            if (!SortKey.AsSpan().SequenceEqual(sortKey))
            {
                SortKey = sortKey;
            }

            SortKeyVersion = DirectoryCollation.Version;
        }

        var searchEmail = DirectorySearchText.Normalize(Email);

        if (!string.Equals(SearchEmail, searchEmail, StringComparison.Ordinal))
        {
            SearchEmail = searchEmail;
        }

        IsActive = Enabled && Lifecycle == ProjectedUserLifecycle.Present;
    }
}
