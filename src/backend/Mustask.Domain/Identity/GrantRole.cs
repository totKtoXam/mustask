namespace Mustask.Domain.Identity;

/// <summary>
/// The role of a grant on a department, a project or a team (mod-019 OQ-10, ADR-0038 § 1): the same three in every scope, so
/// the table decides the scope and not the role. UI names «Читатель», «Участник», «Менеджер». Bundles:
/// <see cref="ScopedRoleBundles"/>.
/// </summary>
public enum GrantRole
{
    /// <summary>«Читатель»: reads.</summary>
    Reader,

    /// <summary>«Участник»: changes what the scope's work consists of.</summary>
    Member,

    /// <summary>«Менеджер»: manages the object, its grants and its links.</summary>
    Manager,
}

/// <summary>
/// The role's spelling on the wire and in the database: <c>reader</c>, <c>member</c>, <c>manager</c> — ordinal and
/// case-sensitive, as <see cref="OrganizationRoles"/>.
/// </summary>
public static class GrantRoles
{
    public const string ReaderCode = "reader";

    public const string MemberCode = "member";

    public const string ManagerCode = "manager";

    /// <summary>All roles, strongest first.</summary>
    public static IReadOnlyList<GrantRole> All { get; } = [GrantRole.Manager, GrantRole.Member, GrantRole.Reader];

    public static string ToCode(GrantRole role) => role switch
    {
        GrantRole.Reader => ReaderCode,
        GrantRole.Member => MemberCode,
        GrantRole.Manager => ManagerCode,
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Unknown grant role."),
    };

    /// <summary>Parses exactly <c>reader</c>, <c>member</c> or <c>manager</c>; anything else is not a role.</summary>
    public static bool TryParse(string? value, out GrantRole role)
    {
        switch (value)
        {
            case ReaderCode:
                role = GrantRole.Reader;
                return true;
            case MemberCode:
                role = GrantRole.Member;
                return true;
            case ManagerCode:
                role = GrantRole.Manager;
                return true;
            default:
                role = default;
                return false;
        }
    }
}
