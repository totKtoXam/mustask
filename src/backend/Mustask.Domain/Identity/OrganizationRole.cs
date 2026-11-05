namespace Mustask.Domain.Identity;

/// <summary>
/// The role of a membership in one organization (doc-001 § Roles, status.md
/// D-8). The role is a column of the membership; its codes are a fixed bundle
/// (<see cref="OrganizationRoleBundles"/>). A user is <c>admin</c> in one
/// organization and <c>viewer</c> in another without conflict.
/// </summary>
public enum OrganizationRole
{
    /// <summary>Every organization code.</summary>
    Admin,

    /// <summary>The read codes and the writes that are not admin-only.</summary>
    Member,

    /// <summary>The read codes.</summary>
    Viewer,
}

/// <summary>
/// The role's spelling on the wire and in the database: <c>admin</c>,
/// <c>member</c>, <c>viewer</c> — ordinal and case-sensitive, so a value that is
/// not exactly one of the three is not a role (api-contract § 1.4).
/// </summary>
public static class OrganizationRoles
{
    public const string AdminCode = "admin";

    public const string MemberCode = "member";

    public const string ViewerCode = "viewer";

    /// <summary>All roles, strongest first.</summary>
    public static IReadOnlyList<OrganizationRole> All { get; } = [OrganizationRole.Admin, OrganizationRole.Member, OrganizationRole.Viewer];

    public static string ToCode(OrganizationRole role) => role switch
    {
        OrganizationRole.Admin => AdminCode,
        OrganizationRole.Member => MemberCode,
        OrganizationRole.Viewer => ViewerCode,
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Unknown organization role."),
    };

    /// <summary>Parses exactly <c>admin</c>, <c>member</c> or <c>viewer</c>; anything else (letter case included) is not a role.</summary>
    public static bool TryParse(string? value, out OrganizationRole role)
    {
        switch (value)
        {
            case AdminCode:
                role = OrganizationRole.Admin;
                return true;
            case MemberCode:
                role = OrganizationRole.Member;
                return true;
            case ViewerCode:
                role = OrganizationRole.Viewer;
                return true;
            default:
                role = default;
                return false;
        }
    }
}
