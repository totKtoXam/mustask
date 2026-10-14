namespace Mustask.Domain.Identity;

/// <summary>
/// The codes each organization role holds (ADR-0027 rule 2, spec § Permission
/// matrix, status.md D-8). The bundles are explicit sets, not derived from a
/// code's suffix: a new organization code is added to the catalog and, in the
/// same change, to the bundles that hold it — and the matrix test fails for a
/// code that has no decision. Team membership grants no code (doc-006).
/// </summary>
public static class OrganizationRoleBundles
{
    private static readonly IReadOnlySet<string> ReadCodes = new HashSet<string>(StringComparer.Ordinal)
    {
        PermissionCatalog.OrganizationsOrganizationRead,
        PermissionCatalog.OrganizationsMembershipRead,
        PermissionCatalog.OrganizationsProjectRead,
        PermissionCatalog.OrganizationsTeamRead,
        PermissionCatalog.OrganizationsAreaRead,
        PermissionCatalog.ProcessDefinitionRead,
        PermissionCatalog.WorkItemsWorkItemRead,
    };

    private static readonly IReadOnlySet<string> Admin =
        new HashSet<string>(PermissionCatalog.OrganizationCodes, StringComparer.Ordinal);

    private static readonly IReadOnlySet<string> Member = new HashSet<string>(ReadCodes, StringComparer.Ordinal)
    {
        PermissionCatalog.WorkItemsWorkItemWrite,
        PermissionCatalog.WorkItemsLinkWrite,
        PermissionCatalog.BoardsBacklogReorder,
    };

    private static readonly IReadOnlySet<string> Viewer = ReadCodes;

    /// <summary>The organization codes the role holds. Never an instance code.</summary>
    public static IReadOnlySet<string> CodesOf(OrganizationRole role) => role switch
    {
        OrganizationRole.Admin => Admin,
        OrganizationRole.Member => Member,
        OrganizationRole.Viewer => Viewer,
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, "Unknown organization role."),
    };

    public static bool Holds(OrganizationRole role, string code) => CodesOf(role).Contains(code);

    /// <summary>The roles whose bundle holds <paramref name="code"/>, strongest first (BR-M1-35 derives from it).</summary>
    public static IReadOnlyList<OrganizationRole> RolesHolding(string code) =>
        OrganizationRoles.All.Where(role => Holds(role, code)).ToList();
}
