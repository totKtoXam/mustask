namespace Mustask.Domain.Identity;

/// <summary>
/// Permission codes that are organization-scoped (status.md D-8, BR-M2-10).
/// They sit beside <see cref="PermissionCatalog"/>, not in it, and never reach
/// the Keycloak realm import: the realm declares only the codes of
/// <see cref="PermissionCatalog.AllCodes"/>, and a code that a membership, not
/// a token, will grant has no place there. No role or group grants these codes
/// and no token carries them. <c>mod-016</c> folds them into its scoped catalog
/// together with its amendment of ADR-0018.
/// </summary>
public static class OrganizationPermissionCatalog
{
    /// <summary>Reading an organization's process definition. Declared on the read endpoint; nothing checks it before <c>mod-016</c>.</summary>
    public const string ProcessDefinitionRead = "process.definition.read";

    /// <summary>Reading a project's work items: the list and one item (mod-003 BR-M3-27). Nothing checks it before <c>mod-016</c>.</summary>
    public const string WorkItemsWorkItemRead = "workitems.workitem.read";

    /// <summary>Creating and saving work items, and the assignee candidates a save needs (mod-003 BR-M3-27). Nothing checks it before <c>mod-016</c>.</summary>
    public const string WorkItemsWorkItemWrite = "workitems.workitem.write";

    /// <summary>Human-readable descriptions of the codes (diagnostics, logs).</summary>
    public static readonly IReadOnlyDictionary<string, string> Descriptions = new Dictionary<string, string>
    {
        [ProcessDefinitionRead] = "Просмотр определения процесса организации",
        [WorkItemsWorkItemRead] = "Просмотр рабочих элементов проекта",
        [WorkItemsWorkItemWrite] = "Создание и изменение рабочих элементов проекта",
    };

    /// <summary>All organization-scoped codes.</summary>
    public static IReadOnlyCollection<string> AllCodes { get; } = Descriptions.Keys.ToList();
}
