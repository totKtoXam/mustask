using Mustask.Domain.Process;
using Mustask.Domain.WorkItems;

namespace Mustask.Domain.Imports;

/// <summary>How a field's file value becomes a MUSTASK value through a recoding table (BR-IE-7).</summary>
public enum RecodingKind
{
    /// <summary>No table: the value is the field's own.</summary>
    None,

    /// <summary>A project (target: its id).</summary>
    Project,

    /// <summary>A work item type of the process (target: its id).</summary>
    Type,

    /// <summary>A state of the row's type (target: its id; an entry may name one type).</summary>
    State,

    /// <summary>A priority (target: 1..4).</summary>
    Priority,

    /// <summary>An organization member (target: the user id).</summary>
    Assignee,

    /// <summary>A team linked to the row's project (target: its id).</summary>
    Team,
}

/// <summary>What a field's value is once converted.</summary>
public enum FieldValueKind
{
    Text,
    Integer,
    Decimal,
    Date,
    Reference,
    Key,
    Identifier,
}

/// <summary>One field the mapping offers (BR-IE-5). <see cref="TypeKinds"/> is <c>null</c> for a field every type carries.</summary>
public sealed record ImportFieldDefinition(
    string Id,
    string Name,
    string Group,
    bool Required,
    bool MultiValued,
    RecodingKind Recoding,
    bool AllowsEmptyPolicy,
    bool SupportsClear,
    IReadOnlySet<AppearanceToken>? TypeKinds,
    FieldValueKind ValueKind,
    WorkItemField? TypedField);

/// <summary>
/// The fields an import template can feed (doc-017 BR-IE-5): project, type, title, description, state, assignee, priority, owning team,
/// the parent by a MUSTASK identifier, the identifier, and every field of feat-022's table on the types that carry it. Tags, comments,
/// participants and external links join with their lines.
/// </summary>
public static class ImportFieldCatalogue
{
    public const string Project = "project";
    public const string Type = "type";
    public const string Title = "title";
    public const string Description = "description";
    public const string State = "state";
    public const string Assignee = "assignee";
    public const string Priority = "priority";
    public const string OwningTeam = "owningTeam";
    public const string Parent = "parent";
    public const string Identifier = "identifier";
    public const string Risk = "risk";
    public const string StoryPoints = "storyPoints";
    public const string Effort = "effort";
    public const string BusinessValue = "businessValue";
    public const string TimeCriticality = "timeCriticality";
    public const string StartDate = "startDate";
    public const string TargetDate = "targetDate";
    public const string Severity = "severity";
    public const string Activity = "activity";
    public const string OriginalEstimate = "originalEstimate";
    public const string RemainingWork = "remainingWork";
    public const string CompletedWork = "completedWork";
    public const string ValueArea = "valueArea";
    public const string ReproSteps = "reproSteps";
    public const string SystemInfo = "systemInfo";

    private const string Main = "main";
    private const string Typed = "typed";

    /// <summary>The header the export adds for the identifier (BR-IE-10, BR-IE-19).</summary>
    public const string ExportIdentifierHeader = "Идентификатор";

    private static readonly AppearanceToken[] AllKinds =
        [AppearanceToken.Epic, AppearanceToken.Feature, AppearanceToken.Story, AppearanceToken.Task, AppearanceToken.Bug];

    public static IReadOnlyList<ImportFieldDefinition> All { get; } =
    [
        Main_(Project, "Проект", required: true, recoding: RecodingKind.Project, emptyPolicy: false, clear: false, FieldValueKind.Reference),
        Main_(Type, "Тип", required: true, recoding: RecodingKind.Type, emptyPolicy: false, clear: false, FieldValueKind.Reference),
        Main_(Title, "Заголовок", required: true, recoding: RecodingKind.None, emptyPolicy: false, clear: false, FieldValueKind.Text),
        Main_(Description, "Описание", required: false, recoding: RecodingKind.None, emptyPolicy: false, clear: true, FieldValueKind.Text),
        Main_(State, "Статус", required: false, recoding: RecodingKind.State, emptyPolicy: true, clear: false, FieldValueKind.Reference),
        Main_(Assignee, "Исполнитель", required: false, recoding: RecodingKind.Assignee, emptyPolicy: true, clear: true, FieldValueKind.Reference),
        Main_(Priority, "Приоритет", required: false, recoding: RecodingKind.Priority, emptyPolicy: false, clear: true, FieldValueKind.Integer, WorkItemField.Priority),
        Main_(OwningTeam, "Команда-владелец", required: false, recoding: RecodingKind.Team, emptyPolicy: true, clear: true, FieldValueKind.Reference),
        Main_(Parent, "Родитель", required: false, recoding: RecodingKind.None, emptyPolicy: false, clear: true, FieldValueKind.Identifier),
        Main_(Identifier, "Идентификатор MUSTASK", required: false, recoding: RecodingKind.None, emptyPolicy: false, clear: false, FieldValueKind.Identifier),
        Typed_(Risk, "Риск", FieldValueKind.Integer, WorkItemField.Risk),
        Typed_(StoryPoints, "Оценка в пунктах", FieldValueKind.Decimal, WorkItemField.StoryPoints),
        Typed_(Effort, "Трудоёмкость", FieldValueKind.Decimal, WorkItemField.Effort),
        Typed_(BusinessValue, "Бизнес-ценность", FieldValueKind.Integer, WorkItemField.BusinessValue),
        Typed_(TimeCriticality, "Критичность по времени", FieldValueKind.Decimal, WorkItemField.TimeCriticality),
        Typed_(StartDate, "Дата начала", FieldValueKind.Date, WorkItemField.StartDate),
        Typed_(TargetDate, "Целевая дата", FieldValueKind.Date, WorkItemField.TargetDate),
        Typed_(Severity, "Серьёзность", FieldValueKind.Integer, WorkItemField.Severity),
        Typed_(Activity, "Вид работ", FieldValueKind.Key, WorkItemField.Activity),
        Typed_(OriginalEstimate, "Исходная оценка", FieldValueKind.Decimal, WorkItemField.OriginalEstimate),
        Typed_(RemainingWork, "Оставшаяся работа", FieldValueKind.Decimal, WorkItemField.RemainingWork),
        Typed_(CompletedWork, "Выполненная работа", FieldValueKind.Decimal, WorkItemField.CompletedWork),
        Typed_(ValueArea, "Вид ценности", FieldValueKind.Key, WorkItemField.ValueArea),
        Typed_(ReproSteps, "Шаги воспроизведения", FieldValueKind.Text, WorkItemField.ReproSteps),
        Typed_(SystemInfo, "Сведения о системе", FieldValueKind.Text, WorkItemField.SystemInfo),
    ];

    /// <summary>The definition of <paramref name="id"/>, or <c>null</c> when the catalogue has no such field.</summary>
    public static ImportFieldDefinition? Find(string? id) => All.FirstOrDefault(field => string.Equals(field.Id, id, StringComparison.Ordinal));

    /// <summary>Whether a type of kind <paramref name="kind"/> carries the field.</summary>
    public static bool IsOnType(ImportFieldDefinition field, AppearanceToken kind) => field.TypeKinds is null || field.TypeKinds.Contains(kind);

    private static ImportFieldDefinition Main_(
        string id, string name, bool required, RecodingKind recoding, bool emptyPolicy, bool clear, FieldValueKind kind, WorkItemField? typed = null) =>
        new(id, name, Main, required, MultiValued: false, recoding, emptyPolicy, clear, TypeKinds: null, kind, typed);

    private static ImportFieldDefinition Typed_(string id, string name, FieldValueKind kind, WorkItemField field) =>
        new(
            id,
            name,
            Typed,
            Required: false,
            MultiValued: false,
            RecodingKind.None,
            AllowsEmptyPolicy: false,
            SupportsClear: true,
            AllKinds.Where(token => token.Has(field)).ToHashSet(),
            kind,
            field);
}
