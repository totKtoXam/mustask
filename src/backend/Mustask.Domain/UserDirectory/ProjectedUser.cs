namespace Mustask.Domain.UserDirectory;

/// <summary>
/// One person who has signed in to MUSTASK, as the identity provider's id token last described them — the aggregate of
/// the user projection (mod-014). Since tech-025 (ADR-0034) it is written only by a sign-in, through
/// <see cref="SignIn"/> and <see cref="RecordSignIn"/>. Rows written by the retired push channel (ADR-0025) are still
/// read: they may be disabled, absent or deleted, and a later sign-in makes the person present and enabled again,
/// because the identity provider let them in.
/// </summary>
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

    /// <summary>Retired with the push channel (ADR-0025 § 6): a sign-in writes 0. Kept because the column is not dropped (ADR-0034).</summary>
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

    /// <summary>Retired with the push channel (ADR-0025 § 5): never written now. Kept because the column is not dropped (ADR-0034).</summary>
    public Guid? LastSnapshotId { get; private set; }

    /// <summary>When MUSTASK last changed this record; diagnostics only.</summary>
    public DateTimeOffset ChangedAt { get; private set; }

    /// <summary>The first sign-in of a person: the profile is stored, enabled and present. The profile must be valid.</summary>
    public static ProjectedUser SignIn(SignInProfile profile, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(profile);
        SignInProfileValidation.EnsureValid(profile);

        var user = new ProjectedUser
        {
            Id = profile.Id!,
            Lifecycle = ProjectedUserLifecycle.Present,
            Enabled = true,
            Moment = 0,
            ChangedAt = now,
        };

        user.TakeProfile(profile);
        user.Recompute();

        return user;
    }

    /// <summary>
    /// A later sign-in: the stored profile takes the claims, and the person is present and enabled (the identity
    /// provider let them in, whatever an old push said). A derived value from another collation version is recomputed
    /// here too. The profile must be valid and must be this user's.
    /// </summary>
    public SignInOutcome RecordSignIn(SignInProfile profile, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(profile);
        SignInProfileValidation.EnsureValid(profile);

        if (!string.Equals(profile.Id, Id, StringComparison.Ordinal))
        {
            throw new ArgumentException($"A sign-in of '{profile.Id}' cannot apply to user '{Id}'.", nameof(profile));
        }

        var changed = !HasProfileOf(profile)
            || !Enabled
            || Lifecycle != ProjectedUserLifecycle.Present;

        TakeProfile(profile);
        Enabled = true;
        Lifecycle = ProjectedUserLifecycle.Present;
        Recompute();

        if (changed)
        {
            ChangedAt = now;
        }

        return changed ? SignInOutcome.Updated : SignInOutcome.Unchanged;
    }

    private bool HasProfileOf(SignInProfile profile) =>
        string.Equals(Username, profile.Username, StringComparison.Ordinal)
        && string.Equals(FirstName, profile.FirstName ?? string.Empty, StringComparison.Ordinal)
        && string.Equals(LastName, profile.LastName ?? string.Empty, StringComparison.Ordinal)
        && string.Equals(Email, profile.Email ?? string.Empty, StringComparison.Ordinal);

    private void TakeProfile(SignInProfile profile)
    {
        Username = profile.Username!;
        FirstName = profile.FirstName ?? string.Empty;
        LastName = profile.LastName ?? string.Empty;
        Email = profile.Email ?? string.Empty;
    }

    /// <summary>
    /// The derived values. Each is assigned only when it differs, so a write that changes nothing leaves the row
    /// unmodified; a key from another collation version is recomputed by any write (ADR-0025 § 9).
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
