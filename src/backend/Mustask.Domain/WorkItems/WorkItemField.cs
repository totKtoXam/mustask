using Mustask.Domain.Process;

namespace Mustask.Domain.WorkItems;

/// <summary>
/// The planning, classification, estimation and text fields a work item carries
/// beyond mod-003's (feat-022 spec § The field table). Which of them an item
/// has depends on its type (<see cref="WorkItemFieldSet"/>).
/// </summary>
public enum WorkItemField
{
    /// <summary>«Приоритет», every type, always set (BR-M3-31).</summary>
    Priority,

    /// <summary>«Риск».</summary>
    Risk,

    /// <summary>«Оценка в пунктах».</summary>
    StoryPoints,

    /// <summary>«Трудоёмкость».</summary>
    Effort,

    /// <summary>«Бизнес-ценность», an integer.</summary>
    BusinessValue,

    /// <summary>«Критичность по времени».</summary>
    TimeCriticality,

    /// <summary>«Дата начала».</summary>
    StartDate,

    /// <summary>«Целевая дата», not earlier than «Дата начала» (BR-M3-35).</summary>
    TargetDate,

    /// <summary>«Серьёзность», a bug's, always set (BR-M3-31).</summary>
    Severity,

    /// <summary>«Вид работ».</summary>
    Activity,

    /// <summary>«Исходная оценка», hours.</summary>
    OriginalEstimate,

    /// <summary>«Оставшаяся работа», hours.</summary>
    RemainingWork,

    /// <summary>«Выполненная работа», hours.</summary>
    CompletedWork,

    /// <summary>«Вид ценности», always set (BR-M3-31).</summary>
    ValueArea,

    /// <summary>«Критерии приёмки», a story's plain text.</summary>
    AcceptanceCriteria,

    /// <summary>«Шаги воспроизведения», a bug's plain text.</summary>
    ReproSteps,

    /// <summary>«Сведения о системе», a bug's plain text (status.md D-6, OQ-4 (b)).</summary>
    SystemInfo,
}

/// <summary>
/// The fields each type has (feat-022 spec § The field table, BR-M3-30). A type
/// is recognised by its kind — the appearance token the organization's process
/// holds fixed and unique per type (mod-001) — never by its name or its key.
/// </summary>
public static class WorkItemFieldSet
{
    private static readonly IReadOnlySet<WorkItemField> PortfolioFields = new HashSet<WorkItemField>
    {
        WorkItemField.Priority,
        WorkItemField.Risk,
        WorkItemField.Effort,
        WorkItemField.BusinessValue,
        WorkItemField.TimeCriticality,
        WorkItemField.StartDate,
        WorkItemField.TargetDate,
        WorkItemField.ValueArea,
    };

    private static readonly IReadOnlySet<WorkItemField> StoryFields = new HashSet<WorkItemField>
    {
        WorkItemField.StoryPoints,
        WorkItemField.Priority,
        WorkItemField.Risk,
        WorkItemField.ValueArea,
        WorkItemField.AcceptanceCriteria,
    };

    private static readonly IReadOnlySet<WorkItemField> TaskFields = new HashSet<WorkItemField>
    {
        WorkItemField.Priority,
        WorkItemField.Activity,
        WorkItemField.OriginalEstimate,
        WorkItemField.RemainingWork,
        WorkItemField.CompletedWork,
    };

    private static readonly IReadOnlySet<WorkItemField> BugFields = new HashSet<WorkItemField>
    {
        WorkItemField.StoryPoints,
        WorkItemField.Priority,
        WorkItemField.Severity,
        WorkItemField.Activity,
        WorkItemField.OriginalEstimate,
        WorkItemField.RemainingWork,
        WorkItemField.CompletedWork,
        WorkItemField.ReproSteps,
        WorkItemField.SystemInfo,
    };

    /// <summary>The fields of a type of kind <paramref name="kind"/>: «Эпик» and «Фича» 8, a story 5, «Задача» 5, «Баг» 9.</summary>
    public static IReadOnlySet<WorkItemField> Of(AppearanceToken kind) => kind switch
    {
        AppearanceToken.Epic or AppearanceToken.Feature => PortfolioFields,
        AppearanceToken.Story => StoryFields,
        AppearanceToken.Task => TaskFields,
        AppearanceToken.Bug => BugFields,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown kind of work item type."),
    };

    /// <summary>Whether a type of kind <paramref name="kind"/> has <paramref name="field"/>.</summary>
    public static bool Has(this AppearanceToken kind, WorkItemField field) => Of(kind).Contains(field);
}
