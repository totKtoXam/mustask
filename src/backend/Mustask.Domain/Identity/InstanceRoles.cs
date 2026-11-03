namespace Mustask.Domain.Identity;

/// <summary>One client role of <c>mustask-api</c>: its name, its description (the realm import carries it) and the instance codes it holds.</summary>
public sealed record InstanceRoleDefinition(string Name, string Description, IReadOnlySet<string> Codes);

/// <summary>
/// The one table of the client roles of <c>mustask-api</c> and the instance codes each holds (tech-024, ADR-0033). The
/// company-wide Keycloak realm lets MUSTASK own only clients and client roles, so the token carries these four role names
/// and nothing else of ours: <c>Mustask.Api</c> maps them to <c>perm</c> claims from this table, and the realm import
/// declares exactly these names. Role names are not permission codes. The panel keeps a hand-mirrored copy in
/// <c>src/frontend/src/shared/api/tokenClaims.ts</c>; change both together.
/// </summary>
/// <remarks>
/// <see cref="SystemAdmin"/> holds every instance code, so a later instance code reaches it by being added here (a test fails
/// for an instance code that no role reaches). Later tasks use the role for cross-organization administration.
/// </remarks>
public static class InstanceRoles
{
    /// <summary>Sign-in and reading the dictionaries. A principal without it has no session and no permissions.</summary>
    public const string User = "user";

    /// <summary>Creating organizations.</summary>
    public const string OrganizationCreator = "organization-creator";

    /// <summary>Reading and changing the dictionaries.</summary>
    public const string DictionariesAdmin = "dictionaries-admin";

    /// <summary>Every instance code.</summary>
    public const string SystemAdmin = "system-admin";

    /// <summary>The roles, in a stable order.</summary>
    public static IReadOnlyList<InstanceRoleDefinition> All { get; } =
    [
        new(User, "Роль: пользователь — вход в систему и просмотр справочников", Set(PermissionCatalog.DictionariesLanguagesRead)),
        new(OrganizationCreator, "Роль: создатель организаций — только право создавать организации", Set(PermissionCatalog.OrganizationsOrganizationCreate)),
        new(DictionariesAdmin, "Роль: администратор справочников — полное управление языками", Set(PermissionCatalog.DictionariesLanguagesRead, PermissionCatalog.DictionariesLanguagesWrite)),
        new(SystemAdmin, "Роль: администратор системы — все права уровня экземпляра", Set([.. PermissionCatalog.InstanceCodes])),
    ];

    /// <summary>The role names, in the order of <see cref="All"/>.</summary>
    public static IReadOnlyList<string> Names { get; } = All.Select(role => role.Name).ToList();

    /// <summary>True when <paramref name="roleName"/> is one of the four; any other name in a token is ignored.</summary>
    public static bool IsKnown(string roleName) => Names.Contains(roleName, StringComparer.Ordinal);

    /// <summary>
    /// The instance codes held through <paramref name="roleNames"/>: the union over the known names, unknown names ignored.
    /// Without <see cref="User"/> among them the answer is empty, whatever else is there.
    /// </summary>
    public static IReadOnlySet<string> CodesOf(IEnumerable<string> roleNames)
    {
        ArgumentNullException.ThrowIfNull(roleNames);

        var names = roleNames.ToHashSet(StringComparer.Ordinal);
        var codes = new HashSet<string>(StringComparer.Ordinal);

        if (!names.Contains(User))
        {
            return codes;
        }

        foreach (var role in All.Where(role => names.Contains(role.Name)))
        {
            codes.UnionWith(role.Codes);
        }

        return codes;
    }

    private static HashSet<string> Set(params string[] codes) => new(codes, StringComparer.Ordinal);
}
