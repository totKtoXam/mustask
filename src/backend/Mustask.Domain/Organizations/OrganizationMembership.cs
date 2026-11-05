using Mustask.Domain.Identity;

namespace Mustask.Domain.Organizations;

/// <summary>
/// A user's membership in one organization, with a role (spec § Entities,
/// BR-M1-16): who added it and when, and who last changed the role and when.
/// One per user per organization (BR-M1-5); a user has any number of them. The
/// user is the Keycloak <c>sub</c>, an opaque string, and the membership
/// outlives a disabled or deleted account (BR-M1-17): there is deliberately no
/// link to the user projection. The role's codes are a bundle
/// (<see cref="OrganizationRoleBundles"/>); this entity only holds the role.
/// </summary>
public sealed class OrganizationMembership
{
    private OrganizationMembership()
    {
        // EF materialization.
    }

    private OrganizationMembership(Guid organizationId, string userId, OrganizationRole role, string addedBy, DateTimeOffset addedAt)
    {
        OrganizationId = organizationId;
        UserId = userId;
        Role = role;
        AddedBy = addedBy;
        AddedAt = addedAt;
    }

    public Guid OrganizationId { get; private set; }

    /// <summary>The member's Keycloak subject.</summary>
    public string UserId { get; private set; } = null!;

    public OrganizationRole Role { get; private set; }

    /// <summary>The adder's Keycloak subject; the member's own for the founding membership (BR-M1-25).</summary>
    public string AddedBy { get; private set; } = null!;

    public DateTimeOffset AddedAt { get; private set; }

    /// <summary><c>null</c> while the role never changed.</summary>
    public string? RoleChangedBy { get; private set; }

    public DateTimeOffset? RoleChangedAt { get; private set; }

    /// <summary>A new membership added by <paramref name="addedBy"/> (US-1). No role change is recorded.</summary>
    public static OrganizationMembership Add(Guid organizationId, string userId, OrganizationRole role, string addedBy, DateTimeOffset now)
    {
        if (organizationId == Guid.Empty)
        {
            throw new ArgumentException("An organization's id is never all zeros.", nameof(organizationId));
        }

        EnsureUser(userId, nameof(userId));
        EnsureUser(addedBy, nameof(addedBy));

        return new OrganizationMembership(organizationId, userId, role, addedBy, now);
    }

    /// <summary>
    /// The founding membership of a new organization: its creator, as <c>admin</c>, added by themselves at the
    /// organization's creation time, whatever the user projection says of them (BR-M1-25, BR-M1-32).
    /// </summary>
    public static OrganizationMembership Founder(Guid organizationId, string creatorId, DateTimeOffset createdAt) =>
        Add(organizationId, creatorId, OrganizationRole.Admin, addedBy: creatorId, createdAt);

    /// <summary>
    /// Sets the role and records who and when. Setting the role the member already has changes nothing — not even the
    /// two «changed» fields (api-contract § 1.5): answers whether anything changed.
    /// </summary>
    public bool ChangeRole(OrganizationRole role, string by, DateTimeOffset now)
    {
        EnsureUser(by, nameof(by));

        if (role == Role)
        {
            return false;
        }

        Role = role;
        RoleChangedBy = by;
        RoleChangedAt = now;

        return true;
    }

    /// <summary>Whether the membership holds the administrator role.</summary>
    public bool IsAdmin => Role == OrganizationRole.Admin;

    /// <summary>
    /// Whether <paramref name="value"/> can be a user id of a membership: a Keycloak subject of 1 to 255 characters, not
    /// blank (api-contract § 0.3). The one definition, for a body field (a 400), a path segment (a not-found) and the
    /// entities alike. The value is taken as given, never trimmed.
    /// </summary>
    public static bool IsValidUserId([System.Diagnostics.CodeAnalysis.NotNullWhen(true)] string? value) =>
        !string.IsNullOrWhiteSpace(value) && value.Length <= OrganizationLimits.UserIdMaxLength;

    private static void EnsureUser(string value, string parameterName)
    {
        if (!IsValidUserId(value))
        {
            throw new ArgumentException(
                $"A user is a Keycloak subject: 1 to {OrganizationLimits.UserIdMaxLength} characters, not blank.",
                parameterName);
        }
    }
}
