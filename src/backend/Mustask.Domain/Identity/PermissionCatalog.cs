namespace Mustask.Domain.Identity;

/// <summary>
/// One code of the catalog: its name, where it comes from and a human-readable
/// description (diagnostics, logs).
/// </summary>
public sealed record PermissionDefinition(string Code, PermissionScope Scope, string Description);

/// <summary>
/// The one catalog of permission codes, fixed in code: the source of truth,
/// changed only by a release (ADR-0027, status.md D-8). Every code carries its
/// <see cref="PermissionScope"/>. <see cref="PermissionScope.Instance"/> codes
/// are the realm import's client roles of <c>mustask-api</c> and are compared
/// with it by the contract tests; <see cref="PermissionScope.Organization"/>
/// codes never reach the realm or the token — an organization membership's role
/// bundle (<see cref="OrganizationRoleBundles"/>) is their only source. A code is
/// added here together with its scope and its bundles, in the change that first
/// checks it.
/// </summary>
public static class PermissionCatalog
{
    // ----- Instance codes (the token) -----

    /// <summary>Reading the languages dictionary (every signed-in user, through the realm's default roles).</summary>
    public const string DictionariesLanguagesRead = "dictionaries.languages.read";

    /// <summary>Changing the languages dictionary (create, edit, set the default).</summary>
    public const string DictionariesLanguagesWrite = "dictionaries.languages.write";

    /// <summary>
    /// Creating an organization (mod-002 BR-M1-2, D-i). Instance-level: it is
    /// held in the token, not granted by a membership.
    /// </summary>
    public const string OrganizationsOrganizationCreate = "organizations.organization.create";

    // ----- Organization codes (the membership) -----

    /// <summary>Reading an organization and its served access.</summary>
    public const string OrganizationsOrganizationRead = "organizations.organization.read";

    /// <summary>Renaming an organization.</summary>
    public const string OrganizationsOrganizationWrite = "organizations.organization.write";

    /// <summary>Deactivating and restoring an organization, and its deactivated list (BR-M1-35).</summary>
    public const string OrganizationsOrganizationDeactivate = "organizations.organization.deactivate";

    /// <summary>Reading an organization's members.</summary>
    public const string OrganizationsMembershipRead = "organizations.membership.read";

    /// <summary>Adding a member, changing a role, removing a member, and the member candidate search.</summary>
    public const string OrganizationsMembershipWrite = "organizations.membership.write";

    /// <summary>Reading an organization's projects.</summary>
    public const string OrganizationsProjectRead = "organizations.project.read";

    /// <summary>Creating and renaming a project.</summary>
    public const string OrganizationsProjectWrite = "organizations.project.write";

    /// <summary>Deactivating and restoring a project, and the deactivated list (BR-M1-35).</summary>
    public const string OrganizationsProjectDeactivate = "organizations.project.deactivate";

    /// <summary>Reading teams and their members.</summary>
    public const string OrganizationsTeamRead = "organizations.team.read";

    /// <summary>Creating, renaming and deleting a team, the project's default team, a team's members and the team candidate search.</summary>
    public const string OrganizationsTeamWrite = "organizations.team.write";

    /// <summary>Reading a project's area tree.</summary>
    public const string OrganizationsAreaRead = "organizations.area.read";

    /// <summary>Creating, renaming, moving, re-owning and deleting an area.</summary>
    public const string OrganizationsAreaWrite = "organizations.area.write";

    /// <summary>Reading an organization's process definition (mod-001 BR-M2-10).</summary>
    public const string ProcessDefinitionRead = "process.definition.read";

    /// <summary>Reading a project's work items: the list and one item (mod-003 BR-M3-27).</summary>
    public const string WorkItemsWorkItemRead = "workitems.workitem.read";

    /// <summary>Creating and saving work items, and the assignee candidates a save needs (mod-003 BR-M3-27).</summary>
    public const string WorkItemsWorkItemWrite = "workitems.workitem.write";

    /// <summary>Reordering a team's backlog (mod-006 BR-M4-27).</summary>
    public const string BoardsBacklogReorder = "boards.backlog.reorder";

    /// <summary>Setting, replacing and clearing a work item's parent (feat-001 BR-H-18).</summary>
    public const string WorkItemsLinkWrite = "workitems.link.write";

    /// <summary>Every code of the catalog, in a stable order: instance codes first, then organization codes.</summary>
    public static IReadOnlyList<PermissionDefinition> All { get; } =
    [
        new(DictionariesLanguagesRead, PermissionScope.Instance, "Просмотр справочника языков"),
        new(DictionariesLanguagesWrite, PermissionScope.Instance, "Изменение справочника языков"),
        new(OrganizationsOrganizationCreate, PermissionScope.Instance, "Создание организаций"),
        new(OrganizationsOrganizationRead, PermissionScope.Organization, "Просмотр организации"),
        new(OrganizationsOrganizationWrite, PermissionScope.Organization, "Переименование организации"),
        new(OrganizationsOrganizationDeactivate, PermissionScope.Organization, "Деактивация и восстановление организации"),
        new(OrganizationsMembershipRead, PermissionScope.Organization, "Просмотр участников организации"),
        new(OrganizationsMembershipWrite, PermissionScope.Organization, "Добавление, смена роли и удаление участников организации"),
        new(OrganizationsProjectRead, PermissionScope.Organization, "Просмотр проектов организации"),
        new(OrganizationsProjectWrite, PermissionScope.Organization, "Создание и переименование проектов"),
        new(OrganizationsProjectDeactivate, PermissionScope.Organization, "Деактивация и восстановление проектов"),
        new(OrganizationsTeamRead, PermissionScope.Organization, "Просмотр команд и их участников"),
        new(OrganizationsTeamWrite, PermissionScope.Organization, "Управление командами и их участниками"),
        new(OrganizationsAreaRead, PermissionScope.Organization, "Просмотр областей проекта"),
        new(OrganizationsAreaWrite, PermissionScope.Organization, "Управление областями проекта"),
        new(ProcessDefinitionRead, PermissionScope.Organization, "Просмотр определения процесса организации"),
        new(WorkItemsWorkItemRead, PermissionScope.Organization, "Просмотр рабочих элементов проекта"),
        new(WorkItemsWorkItemWrite, PermissionScope.Organization, "Создание и изменение рабочих элементов проекта"),
        new(WorkItemsLinkWrite, PermissionScope.Organization, "Изменение связей рабочих элементов проекта"),
        new(BoardsBacklogReorder, PermissionScope.Organization, "Изменение порядка бэклога команды"),
    ];

    /// <summary>The descriptions by code (diagnostics, logs).</summary>
    public static IReadOnlyDictionary<string, string> Descriptions { get; } =
        All.ToDictionary(definition => definition.Code, definition => definition.Description, StringComparer.Ordinal);

    /// <summary>All codes, instance and organization. The realm import holds <see cref="InstanceCodes"/> only.</summary>
    public static IReadOnlyCollection<string> AllCodes { get; } = All.Select(definition => definition.Code).ToList();

    /// <summary>The codes the token carries: the realm import's client roles of <c>mustask-api</c> (ADR-0018).</summary>
    public static IReadOnlyCollection<string> InstanceCodes { get; } = CodesOf(PermissionScope.Instance);

    /// <summary>The codes a membership's role bundle grants; never in the realm import or the token (ADR-0027).</summary>
    public static IReadOnlyCollection<string> OrganizationCodes { get; } = CodesOf(PermissionScope.Organization);

    /// <summary>The scope of a code of the catalog.</summary>
    /// <exception cref="ArgumentException">The code is not in the catalog.</exception>
    public static PermissionScope ScopeOf(string code)
    {
        foreach (var definition in All)
        {
            if (string.Equals(definition.Code, code, StringComparison.Ordinal))
            {
                return definition.Scope;
            }
        }

        throw new ArgumentException($"'{code}' is not a code of the permission catalog.", nameof(code));
    }

    private static List<string> CodesOf(PermissionScope scope) =>
        All.Where(definition => definition.Scope == scope).Select(definition => definition.Code).ToList();
}
