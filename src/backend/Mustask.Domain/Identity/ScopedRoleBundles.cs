namespace Mustask.Domain.Identity;

/// <summary>
/// The codes each grant role holds, per the scope it is granted on (ADR-0029 § 4 as amended by ADR-0038, spec § Roles).
/// The bundles are explicit sets over <b>registered</b> codes; a code joins them in the change that first checks it
/// (<c>organizations.visibility.write</c> is not registered yet). A bundle holds
/// codes of several resource scopes: a code applies to an object only where its resource scope is the object's, and where
/// the grant reaches it (<see cref="EffectivePermissions"/>). Only <see cref="ResourceScope.Area"/> (a department),
/// <see cref="ResourceScope.Project"/> and <see cref="ResourceScope.Team"/> are grant scopes. No bundle holds an
/// organization-scope code or an instance code.
/// </summary>
public static class ScopedRoleBundles
{
    private static readonly IReadOnlySet<string> Read = Set(
        PermissionCatalog.OrganizationsProjectRead,
        PermissionCatalog.WorkItemsWorkItemRead);

    private static readonly IReadOnlySet<string> Change = Set(
        PermissionCatalog.OrganizationsProjectRead,
        PermissionCatalog.WorkItemsWorkItemRead,
        PermissionCatalog.WorkItemsWorkItemWrite,
        PermissionCatalog.WorkItemsLinkWrite,
        PermissionCatalog.WorkItemsWorkItemDelete);

    // The project's «Участник» also orders the project's backlog (BR-SA-10); a department's does not.
    private static readonly IReadOnlySet<string> ProjectChange = Set(
        PermissionCatalog.OrganizationsProjectRead,
        PermissionCatalog.WorkItemsWorkItemRead,
        PermissionCatalog.WorkItemsWorkItemWrite,
        PermissionCatalog.WorkItemsLinkWrite,
        PermissionCatalog.WorkItemsWorkItemDelete,
        PermissionCatalog.BoardsProjectBacklogReorder);

    // The department's «Менеджер»: its own area codes, and on every project it reaches the project's settings, links and grants (OQ-8 A).
    private static readonly IReadOnlySet<string> DepartmentManager = Set(
        PermissionCatalog.OrganizationsProjectRead,
        PermissionCatalog.WorkItemsWorkItemRead,
        PermissionCatalog.WorkItemsWorkItemWrite,
        PermissionCatalog.WorkItemsLinkWrite,
        PermissionCatalog.WorkItemsWorkItemDelete,
        PermissionCatalog.OrganizationsAreaWrite,
        PermissionCatalog.OrganizationsAreaLink,
        PermissionCatalog.OrganizationsAreaGrant,
        PermissionCatalog.OrganizationsProjectWrite,
        PermissionCatalog.OrganizationsProjectLink,
        PermissionCatalog.OrganizationsProjectGrant);

    // The project's «Менеджер»: the project's own codes, and the area code on its node and sub-areas (OQ-3 B; the reach rule keeps it there).
    private static readonly IReadOnlySet<string> ProjectManager = Set(
        PermissionCatalog.OrganizationsProjectRead,
        PermissionCatalog.WorkItemsWorkItemRead,
        PermissionCatalog.WorkItemsWorkItemWrite,
        PermissionCatalog.WorkItemsLinkWrite,
        PermissionCatalog.WorkItemsWorkItemDelete,
        PermissionCatalog.OrganizationsProjectWrite,
        PermissionCatalog.OrganizationsProjectLink,
        PermissionCatalog.OrganizationsProjectGrant,
        PermissionCatalog.BoardsProjectBacklogReorder,
        PermissionCatalog.OrganizationsAreaWrite);

    private static readonly IReadOnlySet<string> None = Set();

    private static readonly IReadOnlySet<string> TeamMember = Set(
        PermissionCatalog.BoardsBacklogReorder,
        PermissionCatalog.IterationsSprintManage);

    private static readonly IReadOnlySet<string> TeamManager = Set(
        PermissionCatalog.BoardsBacklogReorder,
        PermissionCatalog.IterationsSprintManage,
        PermissionCatalog.OrganizationsTeamWrite,
        PermissionCatalog.OrganizationsTeamLink,
        PermissionCatalog.OrganizationsTeamGrant);

    /// <summary>The scopes a grant can be held on.</summary>
    public static IReadOnlyList<ResourceScope> GrantScopes { get; } = [ResourceScope.Area, ResourceScope.Project, ResourceScope.Team];

    /// <summary>The codes <paramref name="role"/> holds when granted on an object of <paramref name="grantScope"/>.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The scope is not one a grant is held on, or the role is unknown.</exception>
    public static IReadOnlySet<string> CodesOf(ResourceScope grantScope, GrantRole role) => (grantScope, role) switch
    {
        (ResourceScope.Area, GrantRole.Reader) => Read,
        (ResourceScope.Area, GrantRole.Member) => Change,
        (ResourceScope.Area, GrantRole.Manager) => DepartmentManager,
        (ResourceScope.Project, GrantRole.Reader) => Read,
        (ResourceScope.Project, GrantRole.Member) => ProjectChange,
        (ResourceScope.Project, GrantRole.Manager) => ProjectManager,
        (ResourceScope.Team, GrantRole.Reader) => None,
        (ResourceScope.Team, GrantRole.Member) => TeamMember,
        (ResourceScope.Team, GrantRole.Manager) => TeamManager,
        _ => throw new ArgumentOutOfRangeException(nameof(grantScope), (grantScope, role), "Not a grant scope and role."),
    };

    private static HashSet<string> Set(params string[] codes) => new(codes, StringComparer.Ordinal);
}
