using Mustask.Application.Abstractions.UserDirectory;

namespace Mustask.Application.Abstractions.Organizations;

/// <summary>
/// Reads of memberships with the people named, from the user projection and never
/// from Keycloak (mod-014 BR-UP-9; mod-016 plan-backend § 3.4, § 3.7). Every list
/// runs a fixed number of queries: the memberships (one), then the projection
/// lookups, split above 500 ids into <c>ceil(ids / 500)</c> statements
/// (<see cref="UserDirectoryBatches"/>). The views are shared records, like
/// <c>WorkItemView</c>: slices share Abstractions, never each other.
/// </summary>
public interface IMemberReadModel
{
    /// <summary>
    /// The organization's members in the order of api-contract § 0.5, each with the people named in «Добавлен» and
    /// «Роль изменена» (who may no longer be members).
    /// </summary>
    Task<IReadOnlyList<MemberView>> ListAsync(Guid organizationId, CancellationToken cancellationToken);

    /// <summary>One member as <see cref="ListAsync"/> shows them; <c>null</c> when the user has no membership.</summary>
    Task<MemberView?> FindAsync(Guid organizationId, string userId, CancellationToken cancellationToken);

    /// <summary>The team's members in the order of api-contract § 0.5.</summary>
    Task<IReadOnlyList<UserRefView>> ListTeamMembersAsync(Guid teamId, CancellationToken cancellationToken);

    /// <summary>The ids of every member of the organization (any role); the restriction of a member-only search.</summary>
    Task<IReadOnlyList<string>> ListMemberIdsAsync(Guid organizationId, CancellationToken cancellationToken);

    /// <summary>Whether the user has a membership in the organization (any role). Tracks nothing.</summary>
    Task<bool> IsMemberAsync(Guid organizationId, string userId, CancellationToken cancellationToken);

    /// <summary>
    /// How many administrators other than <paramref name="exceptUserId"/> are <b>known and active</b> in the user projection
    /// (BR-M1-4, BR-M1-32): one that is inactive or unknown does not count. The one definition of that count.
    /// </summary>
    Task<int> CountOtherKnownActiveAdministratorsAsync(Guid organizationId, string exceptUserId, CancellationToken cancellationToken);
}

/// <summary>The three values of <see cref="UserRefView.State"/> (api-contract § 0.4).</summary>
public static class UserRefStates
{
    public const string Active = "active";

    /// <summary>Disabled or deleted in Keycloak (mod-014 BR-UP-11); the projection still holds them.</summary>
    public const string Inactive = "inactive";

    /// <summary>The projection does not hold the id (mod-014 BR-UP-17).</summary>
    public const string Unknown = "unknown";
}

/// <summary>
/// <c>UserRef</c> of api-contract § 0.4: the one shape that names a person.
/// <paramref name="DisplayName"/> and <paramref name="Email"/> are <c>null</c> only
/// when <paramref name="State"/> is <c>unknown</c>; a known user with no e-mail has <c>""</c>.
/// </summary>
public sealed record UserRefView(string Id, string? DisplayName, string? Email, string State)
{
    public static UserRefView From(DirectoryUser user) =>
        new(user.Id, user.DisplayName, user.Email, user.IsActive ? UserRefStates.Active : UserRefStates.Inactive);

    public static UserRefView Unknown(string id) => new(id, null, null, UserRefStates.Unknown);

    /// <summary>The reference for <paramref name="id"/>: the projected user when the lookup knows them, else the unknown one.</summary>
    public static UserRefView Of(string id, IReadOnlyDictionary<string, DirectoryUser> known) =>
        known.TryGetValue(id, out var user) ? From(user) : Unknown(id);
}

/// <summary><c>Member</c> of api-contract § 1.2.</summary>
/// <param name="Role"><c>admin</c>, <c>member</c> or <c>viewer</c>.</param>
/// <param name="RoleChangedBy"><c>null</c> while the role never changed.</param>
public sealed record MemberView(
    UserRefView User,
    string Role,
    UserRefView AddedBy,
    DateTimeOffset AddedAt,
    UserRefView? RoleChangedBy,
    DateTimeOffset? RoleChangedAt);
