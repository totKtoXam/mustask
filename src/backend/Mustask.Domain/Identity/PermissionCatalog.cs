namespace Mustask.Domain.Identity;

/// <summary>
/// One code of the catalog: its name, where it comes from (<see cref="PermissionSource"/>), the kind of object it is checked on (<see cref="ResourceScope"/>, organization codes only) and a human-readable
/// description (diagnostics, logs).
/// </summary>
public sealed record PermissionDefinition(string Code, PermissionSource Source, string Description, ResourceScope? Resource = null);

/// <summary>
/// The one catalog of permission codes, fixed in code: the source of truth,
/// changed only by a release (ADR-0027, status.md D-8). Every code carries its
/// <see cref="PermissionSource"/>. <see cref="PermissionSource.Instance"/> codes
/// are held through the four client roles of <c>mustask-api</c> (<see cref="InstanceRoles"/>); a test fails for one
/// no role reaches; <see cref="PermissionSource.Organization"/>
/// codes never reach the realm or the token — an organization membership's role
/// bundle (<see cref="OrganizationRoleBundles"/>) is their only source. A code is
/// added here together with its scope and its bundles, in the change that first
/// checks it.
/// </summary>
public static class PermissionCatalog
{
    // ----- Instance codes (the token) -----

    /// <summary>Reading the languages dictionary (every user holding the client role <c>user</c>).</summary>
    public const string DictionariesLanguagesRead = "dictionaries.languages.read";

    /// <summary>Changing the languages dictionary (create, edit, set the default).</summary>
    public const string DictionariesLanguagesWrite = "dictionaries.languages.write";

    /// <summary>
    /// Creating an organization (mod-002 BR-M1-2, D-i). Instance-level: it is
    /// held in the token, not granted by a membership.
    /// </summary>
    public const string OrganizationsOrganizationCreate = "organizations.organization.create";

    /// <summary>
    /// Appointing an administrator in any organization and reading its appointment record (feat-033, ADR-0035 § 2).
    /// Instance-level: held through <c>system-admin</c> only; it reads no organization content (ADR-0029 § 9).
    /// </summary>
    public const string OrganizationsOrganizationRescue = "organizations.organization.rescue";

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

    /// <summary>Creating, renaming and describing a project, and naming its default team.</summary>
    public const string OrganizationsProjectWrite = "organizations.project.write";

    /// <summary>The project's end of a link between a project and a team, and of an unlink (feat-028 BR-OL-5).</summary>
    public const string OrganizationsProjectLink = "organizations.project.link";

    /// <summary>Deactivating and restoring a project, and the deactivated list (BR-M1-35).</summary>
    public const string OrganizationsProjectDeactivate = "organizations.project.deactivate";

    /// <summary>Reading teams and their members.</summary>
    public const string OrganizationsTeamRead = "organizations.team.read";

    /// <summary>A team's name, description and members, and the team candidate search (feat-028 narrowed it: it no longer creates or deletes).</summary>
    public const string OrganizationsTeamWrite = "organizations.team.write";

    /// <summary>Creating a team (feat-028 BR-OL-5).</summary>
    public const string OrganizationsTeamCreate = "organizations.team.create";

    /// <summary>Deleting a team and reading what a deletion would reassign (feat-028 BR-OL-5).</summary>
    public const string OrganizationsTeamDelete = "organizations.team.delete";

    /// <summary>The team's end of a link between a project and a team, and of an unlink (feat-028 BR-OL-5).</summary>
    public const string OrganizationsTeamLink = "organizations.team.link";

    /// <summary>Reading the organization's area tree, an area's page and its links (mod-018, ADR-0029 § 4: scope O).</summary>
    public const string OrganizationsAreaRead = "organizations.area.read";

    /// <summary>
    /// Renaming, describing, moving and deleting an area, and creating a child area (mod-018: scope A). Checked on the organization
    /// role alone until <c>mod-019-scoped-access</c>.
    /// </summary>
    public const string OrganizationsAreaWrite = "organizations.area.write";

    /// <summary>Creating a top-level area, and moving an area to or from the top level (mod-018: scope O).</summary>
    public const string OrganizationsAreaCreate = "organizations.area.create";

    /// <summary>The area's end of a link with a project or a team, and of creating an area with links (mod-018: scope A).</summary>
    public const string OrganizationsAreaLink = "organizations.area.link";

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

    /// <summary>
    /// Deleting a work item into «Удалена» and restoring it (mod-005 BR-DR-17, ADR-0030 rule 9). Checked on the organization
    /// role alone until mod-019; belonging to a team grants nothing.
    /// </summary>
    public const string WorkItemsWorkItemDelete = "workitems.workitem.delete";

    /// <summary>
    /// Removing a deleted work item for good (mod-005 BR-DR-12, BR-DR-17, ADR-0030 rule 9; doc-010 D-13): the organization
    /// administrator's alone. No scoped role holds it, so mod-019 has nothing to scope there.
    /// </summary>
    public const string WorkItemsWorkItemPurge = "workitems.workitem.purge";

    /// <summary>Giving, changing and removing grants on a department (mod-019 BR-SA-2: scope A, the «Менеджер» bundles).</summary>
    public const string OrganizationsAreaGrant = "organizations.area.grant";

    /// <summary>Giving, changing and removing grants on a project (mod-019 BR-SA-2: scope P).</summary>
    public const string OrganizationsProjectGrant = "organizations.project.grant";

    /// <summary>Giving, changing and removing grants on a team (mod-019 BR-SA-2: scope T).</summary>
    public const string OrganizationsTeamGrant = "organizations.team.grant";

    /// <summary>Every code of the catalog, in a stable order: instance codes first, then organization codes.</summary>
    public static IReadOnlyList<PermissionDefinition> All { get; } =
    [
        new(DictionariesLanguagesRead, PermissionSource.Instance, "Просмотр справочника языков"),
        new(DictionariesLanguagesWrite, PermissionSource.Instance, "Изменение справочника языков"),
        new(OrganizationsOrganizationCreate, PermissionSource.Instance, "Создание организаций"),
        new(OrganizationsOrganizationRescue, PermissionSource.Instance, "Назначение администратора в любую организацию"),
        new(OrganizationsOrganizationRead, PermissionSource.Organization, "Просмотр организации", ResourceScope.Organization),
        new(OrganizationsOrganizationWrite, PermissionSource.Organization, "Переименование организации", ResourceScope.Organization),
        new(OrganizationsOrganizationDeactivate, PermissionSource.Organization, "Деактивация и восстановление организации", ResourceScope.Organization),
        new(OrganizationsMembershipRead, PermissionSource.Organization, "Просмотр участников организации", ResourceScope.Organization),
        new(OrganizationsMembershipWrite, PermissionSource.Organization, "Добавление, смена роли и удаление участников организации", ResourceScope.Organization),
        new(OrganizationsProjectRead, PermissionSource.Organization, "Просмотр проектов организации", ResourceScope.Project),
        new(OrganizationsProjectWrite, PermissionSource.Organization, "Создание, переименование и описание проектов, команда по умолчанию", ResourceScope.Project),
        new(OrganizationsProjectLink, PermissionSource.Organization, "Связи проекта с командами", ResourceScope.Project),
        new(OrganizationsProjectDeactivate, PermissionSource.Organization, "Деактивация и восстановление проектов", ResourceScope.Organization),
        new(OrganizationsTeamRead, PermissionSource.Organization, "Просмотр команд и их участников", ResourceScope.Organization),
        new(OrganizationsTeamWrite, PermissionSource.Organization, "Название, описание и участники команды", ResourceScope.Team),
        new(OrganizationsTeamCreate, PermissionSource.Organization, "Создание команд", ResourceScope.Organization),
        new(OrganizationsTeamDelete, PermissionSource.Organization, "Удаление команд", ResourceScope.Organization),
        new(OrganizationsTeamLink, PermissionSource.Organization, "Связи команды с проектами", ResourceScope.Team),
        new(OrganizationsAreaRead, PermissionSource.Organization, "Просмотр областей организации", ResourceScope.Organization),
        new(OrganizationsAreaWrite, PermissionSource.Organization, "Изменение, перемещение и удаление областей, дочерние области", ResourceScope.Area),
        new(OrganizationsAreaCreate, PermissionSource.Organization, "Создание областей верхнего уровня", ResourceScope.Organization),
        new(OrganizationsAreaLink, PermissionSource.Organization, "Связи департаментов с командами", ResourceScope.Area),
        new(ProcessDefinitionRead, PermissionSource.Organization, "Просмотр определения процесса организации", ResourceScope.Organization),
        new(WorkItemsWorkItemRead, PermissionSource.Organization, "Просмотр рабочих элементов проекта", ResourceScope.Project),
        new(WorkItemsWorkItemWrite, PermissionSource.Organization, "Создание и изменение рабочих элементов проекта", ResourceScope.Project),
        new(WorkItemsLinkWrite, PermissionSource.Organization, "Изменение связей рабочих элементов проекта", ResourceScope.Project),
        new(WorkItemsWorkItemDelete, PermissionSource.Organization, "Удаление и восстановление рабочих элементов проекта", ResourceScope.Project),
        new(WorkItemsWorkItemPurge, PermissionSource.Organization, "Окончательное удаление рабочих элементов проекта", ResourceScope.Project),
        new(BoardsBacklogReorder, PermissionSource.Organization, "Изменение порядка бэклога команды", ResourceScope.Team),
        new(OrganizationsAreaGrant, PermissionSource.Organization, "Доступ к департаменту: выдача, смена роли и отзыв", ResourceScope.Area),
        new(OrganizationsProjectGrant, PermissionSource.Organization, "Доступ к проекту: выдача, смена роли и отзыв", ResourceScope.Project),
        new(OrganizationsTeamGrant, PermissionSource.Organization, "Доступ к команде: выдача, смена роли и отзыв", ResourceScope.Team),
    ];

    /// <summary>The descriptions by code (diagnostics, logs).</summary>
    public static IReadOnlyDictionary<string, string> Descriptions { get; } =
        All.ToDictionary(definition => definition.Code, definition => definition.Description, StringComparer.Ordinal);

    /// <summary>All codes, instance and organization. The realm import holds <see cref="InstanceCodes"/> only.</summary>
    public static IReadOnlyCollection<string> AllCodes { get; } = All.Select(definition => definition.Code).ToList();

    /// <summary>The codes the token's client roles give (<see cref="InstanceRoles"/>, ADR-0033).</summary>
    public static IReadOnlyCollection<string> InstanceCodes { get; } = CodesOf(PermissionSource.Instance);

    /// <summary>The codes a membership's role bundle grants; never in the realm import or the token (ADR-0027).</summary>
    public static IReadOnlyCollection<string> OrganizationCodes { get; } = CodesOf(PermissionSource.Organization);

    /// <summary>The source of a code of the catalog: the token or an organization membership.</summary>
    /// <exception cref="ArgumentException">The code is not in the catalog.</exception>
    public static PermissionSource SourceOf(string code) => DefinitionOf(code).Source;

    /// <summary>The kind of object an organization code is checked on (ADR-0029 § 2, ADR-0037).</summary>
    /// <exception cref="ArgumentException">The code is not in the catalog, or is an instance code (no resource scope).</exception>
    public static ResourceScope ResourceScopeOf(string code) =>
        DefinitionOf(code).Resource
        ?? throw new ArgumentException($"'{code}' is an instance code and has no resource scope.", nameof(code));

    private static PermissionDefinition DefinitionOf(string code)
    {
        foreach (var definition in All)
        {
            if (string.Equals(definition.Code, code, StringComparison.Ordinal))
            {
                return definition;
            }
        }

        throw new ArgumentException($"'{code}' is not a code of the permission catalog.", nameof(code));
    }

    private static List<string> CodesOf(PermissionSource source) =>
        All.Where(definition => definition.Source == source).Select(definition => definition.Code).ToList();
}
