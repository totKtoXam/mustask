using Mustask.Domain.Process;
using Mustask.Shared.Exceptions;

namespace Mustask.Domain.WorkItems;

/// <summary>
/// A work item of one project (spec § Data model): one of the five types of its
/// organization's process, a number from the project's one sequence, a title, a
/// plain-text description, a state of its type, an optional assignee from the
/// user directory and an area of its project. Its type, its project and its
/// number never change (BR-M3-1, BR-M3-2, BR-M3-5), so neither does its
/// identifier. It records who created it and when, and the time of its latest
/// save that changed a value (BR-M3-28). feat-022 adds the planning,
/// classification, estimation and text fields of <see cref="WorkItemField"/>:
/// an item holds a value only in the fields its type has
/// (<see cref="WorkItemFieldSet"/>), and <c>null</c> in every other.
/// </summary>
public sealed class WorkItem
{
    private WorkItem()
    {
        // EF materialization.
    }

    private WorkItem(
        Guid id,
        Guid projectId,
        int number,
        Guid typeId,
        Guid stateId,
        Guid areaId,
        string title,
        Guid creationRequestId,
        DateTimeOffset createdAt,
        string createdBy)
    {
        Id = id;
        ProjectId = projectId;
        Number = number;
        TypeId = typeId;
        StateId = stateId;
        AreaId = areaId;
        Title = title;
        Description = string.Empty;
        CreationRequestId = creationRequestId;
        CreatedAt = createdAt;
        CreatedBy = createdBy;
        ChangedAt = createdAt;
    }

    public Guid Id { get; private set; }

    public Guid ProjectId { get; private set; }

    /// <summary>The number in the project's sequence, 1 or more (BR-M3-5).</summary>
    public int Number { get; private set; }

    public Guid TypeId { get; private set; }

    public Guid StateId { get; private set; }

    /// <summary>The assignee's Keycloak user id, or <c>null</c> for «Не назначен» (BR-M3-22).</summary>
    public string? AssigneeId { get; private set; }

    public Guid AreaId { get; private set; }

    public string Title { get; private set; } = null!;

    public string Description { get; private set; } = null!;

    /// <summary>The client's key of the submission that created the item (BR-M3-26): a replay of it creates nothing.</summary>
    public Guid CreationRequestId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>The creator's Keycloak subject (<c>sub</c>).</summary>
    public string CreatedBy { get; private set; } = null!;

    /// <summary>The time of the latest save that changed a value; the creation time until then (BR-M3-28).</summary>
    public DateTimeOffset ChangedAt { get; private set; }

    /// <summary>
    /// The item's one parent, an item of the same project, or <c>null</c> (feat-001 BR-H-1). Written only by
    /// <see cref="WorkItemHierarchy.Write"/>, which holds the rules; a parent's deletion leaves it as it is (BR-H-8).
    /// </summary>
    public Guid? ParentId { get; private set; }

    // --- feat-022: the fields per type (its spec § The field table) -------------------------------------------------

    /// <summary>«Приоритет», 1 the highest to 4, on every type; never empty (BR-M3-31).</summary>
    public int Priority { get; private set; }

    /// <summary>«Риск», 1..3, or <c>null</c> for «Не указан».</summary>
    public int? Risk { get; private set; }

    /// <summary>«Оценка в пунктах», points, or <c>null</c> for «Не оценено» (never 0, BR-M3-32).</summary>
    public decimal? StoryPoints { get; private set; }

    /// <summary>«Трудоёмкость», no unit.</summary>
    public decimal? Effort { get; private set; }

    /// <summary>«Бизнес-ценность», an integer with no unit.</summary>
    public int? BusinessValue { get; private set; }

    /// <summary>«Критичность по времени», no unit.</summary>
    public decimal? TimeCriticality { get; private set; }

    /// <summary>«Дата начала», a calendar date with no time (BR-M3-35).</summary>
    public DateOnly? StartDate { get; private set; }

    /// <summary>«Целевая дата», never earlier than <see cref="StartDate"/> when both are set (BR-M3-35).</summary>
    public DateOnly? TargetDate { get; private set; }

    /// <summary>«Серьёзность», 1..4, set on every bug (BR-M3-31).</summary>
    public int? Severity { get; private set; }

    /// <summary>«Вид работ».</summary>
    public WorkItemActivity? Activity { get; private set; }

    /// <summary>«Исходная оценка», hours.</summary>
    public decimal? OriginalEstimate { get; private set; }

    /// <summary>«Оставшаяся работа», hours; a state change leaves it as entered (BR-M3-34).</summary>
    public decimal? RemainingWork { get; private set; }

    /// <summary>«Выполненная работа», hours; it reduces nothing (BR-M3-34).</summary>
    public decimal? CompletedWork { get; private set; }

    /// <summary>«Вид ценности», set on every epic, feature and story (BR-M3-31).</summary>
    public WorkItemValueArea? ValueArea { get; private set; }

    /// <summary>«Критерии приёмки», a story's structured scenarios, <c>[]</c> when empty, <c>null</c> on other types (R1, BR-M3-36).</summary>
    public IReadOnlyList<AcceptanceCriterion>? AcceptanceCriteria { get; private set; }

    /// <summary>«Шаги воспроизведения», a bug's plain text, <c>""</c> when empty.</summary>
    public string? ReproSteps { get; private set; }

    /// <summary>«Сведения о системе», a bug's plain text, <c>""</c> when empty (status.md D-6).</summary>
    public string? SystemInfo { get; private set; }

    /// <summary>
    /// A new item (US-1, BR-M3-20): its type's initial state, no assignee, an
    /// empty description and the project's root area; of feat-022's fields,
    /// «Приоритет» 2, on a bug «Серьёзность» 3, on an epic, a feature or a story
    /// «Вид ценности» «Бизнес», the type's texts <c>""</c> and every other field
    /// empty (BR-M3-37). The caller has validated
    /// the title (<see cref="WorkItemText.ValidateTitle"/>), resolved the type in
    /// the organization's process and taken <paramref name="number"/> from the
    /// project's sequence.
    /// </summary>
    public static WorkItem Create(
        Guid projectId,
        int number,
        WorkItemTypeStates type,
        Guid rootAreaId,
        string title,
        Guid creationRequestId,
        DateTimeOffset createdAt,
        string createdBy)
    {
        ArgumentNullException.ThrowIfNull(type);
        ArgumentOutOfRangeException.ThrowIfLessThan(number, 1);
        ArgumentException.ThrowIfNullOrWhiteSpace(createdBy);

        if (projectId == Guid.Empty || rootAreaId == Guid.Empty || creationRequestId == Guid.Empty)
        {
            throw new ArgumentException("A work item has a project, an area and the key of its creation.");
        }

        if (WorkItemText.ValidateTitle(title, out var trimmed) is not null || trimmed != title)
        {
            throw new ArgumentException("The title is validated and trimmed before the item is built.", nameof(title));
        }

        var item = new WorkItem(
            Guid.CreateVersion7(),
            projectId,
            number,
            type.TypeId,
            type.InitialStateId,
            rootAreaId,
            title,
            creationRequestId,
            createdAt,
            createdBy);

        item.TakeDefaults(type.Appearance);

        return item;
    }

    /// <summary>
    /// The reasons <paramref name="fields"/> would be refused for on this item
    /// (feat-022 api-contract § 3.2, BR-M3-30..BR-M3-36), every field at once,
    /// one reason a field: a field the type does not have; then its value; then
    /// «Целевая дата» earlier than «Дата начала» once the save is applied to the
    /// stored dates, unless a date has a reason of its own. Empty when the save
    /// may go ahead. The caller holds the item's lock, so the stored dates are
    /// the ones the save will meet.
    /// </summary>
    /// <param name="type">The item's own type, as the organization's process holds it.</param>
    public IReadOnlyList<WorkItemFieldReason> CheckFields(WorkItemFieldsEdit fields, WorkItemTypeStates type)
    {
        ArgumentNullException.ThrowIfNull(fields);
        RequireOwnType(type);

        var kind = type.Appearance;
        var reasons = new List<WorkItemFieldReason>();

        Check(reasons, kind, WorkItemField.Priority, fields.Priority, WorkItemFieldRules.ValidatePriority);
        Check(reasons, kind, WorkItemField.Risk, fields.Risk, WorkItemFieldRules.ValidateRisk);
        Check(reasons, kind, WorkItemField.StoryPoints, fields.StoryPoints, WorkItemFieldRules.ValidateDecimal);
        Check(reasons, kind, WorkItemField.Effort, fields.Effort, WorkItemFieldRules.ValidateDecimal);
        Check(reasons, kind, WorkItemField.BusinessValue, fields.BusinessValue, WorkItemFieldRules.ValidateInteger);
        Check(reasons, kind, WorkItemField.TimeCriticality, fields.TimeCriticality, WorkItemFieldRules.ValidateDecimal);
        Check(reasons, kind, WorkItemField.StartDate, fields.StartDate, WorkItemFieldRules.ValidateDate);
        Check(reasons, kind, WorkItemField.TargetDate, fields.TargetDate, WorkItemFieldRules.ValidateDate);
        Check(reasons, kind, WorkItemField.Severity, fields.Severity, WorkItemFieldRules.ValidateSeverity);
        CheckKey(reasons, kind, WorkItemField.Activity, fields.Activity, key => WorkItemFieldRules.ParseActivity(key) is not null);
        Check(reasons, kind, WorkItemField.OriginalEstimate, fields.OriginalEstimate, WorkItemFieldRules.ValidateDecimal);
        Check(reasons, kind, WorkItemField.RemainingWork, fields.RemainingWork, WorkItemFieldRules.ValidateDecimal);
        Check(reasons, kind, WorkItemField.CompletedWork, fields.CompletedWork, WorkItemFieldRules.ValidateDecimal);
        CheckKey(reasons, kind, WorkItemField.ValueArea, fields.ValueArea, key => WorkItemFieldRules.ParseValueArea(key) is not null);
        CheckAcceptanceCriteria(reasons, kind, fields.AcceptanceCriteria);
        CheckText(reasons, kind, WorkItemField.ReproSteps, fields.ReproSteps);
        CheckText(reasons, kind, WorkItemField.SystemInfo, fields.SystemInfo);

        var datesSent = fields.StartDate is not null || fields.TargetDate is not null;
        var datesHaveReasons = reasons.Any(reason => reason.Field is WorkItemField.StartDate or WorkItemField.TargetDate);

        if (datesSent && !datesHaveReasons)
        {
            var start = fields.StartDate is { } sentStart ? sentStart.Value : StartDate;
            var target = fields.TargetDate is { } sentTarget ? sentTarget.Value : TargetDate;

            if (WorkItemFieldRules.TargetBeforeStart(start, target))
            {
                reasons.Add(new WorkItemFieldReason(WorkItemField.TargetDate, WorkItemErrorCodes.TargetBeforeStart));
            }
        }

        return reasons;
    }

    /// <summary>
    /// One save of the form (US-3, US-4, US-5): exactly the fields of
    /// <paramref name="edit"/> that are set, all of them or none (BR-M3-25). The
    /// state is checked before the assignee, and a refusal throws before anything
    /// changes. A value equal to the stored one changes nothing; when nothing
    /// changes, <see cref="ChangedAt"/> stays. Returns whether anything changed.
    /// </summary>
    /// <param name="type">The item's own type, as the organization's process holds it.</param>
    public bool Save(WorkItemEdit edit, WorkItemTypeStates type, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(edit);
        ArgumentNullException.ThrowIfNull(type);

        RequireOwnType(type);

        // feat-022's fields are checked by the caller (CheckFields), with the other field reasons, before any 404 or 409.
        if (edit.Fields is { } fields && CheckFields(fields, type).Count > 0)
        {
            throw new ArgumentException("The fields are checked before the save.", nameof(edit));
        }

        // The checks first, so a refusal leaves the item as it was.
        if (edit.StateId is { } stateId && stateId != StateId && !type.Allows(stateId))
        {
            throw new ConflictException(WorkItemErrorCodes.StateNotAllowed);
        }

        if (edit.Assignee is { UserId: { } userId, IsAvailable: false } && userId != AssigneeId)
        {
            throw new ConflictException(WorkItemErrorCodes.AssigneeUnavailable);
        }

        var changed = false;

        if (edit.Title is { } title && title != Title)
        {
            Title = title;
            changed = true;
        }

        if (edit.Description is { } description && description != Description)
        {
            Description = description;
            changed = true;
        }

        if (edit.StateId is { } newStateId && newStateId != StateId)
        {
            StateId = newStateId;
            changed = true;
        }

        if (edit.Assignee is { } assignee && assignee.UserId != AssigneeId)
        {
            AssigneeId = assignee.UserId;
            changed = true;
        }

        if (edit.AreaId is { } areaId && areaId != AreaId)
        {
            AreaId = areaId;
            changed = true;
        }

        if (edit.Fields is { } sent)
        {
            changed |= Apply(sent);
        }

        if (changed)
        {
            ChangedAt = now;
        }

        return changed;
    }

    /// <summary>BR-M3-37: the values a new item of kind <paramref name="kind"/> takes; every other field stays empty.</summary>
    private void TakeDefaults(AppearanceToken kind)
    {
        Priority = WorkItemFieldRules.DefaultPriority;
        Severity = kind.Has(WorkItemField.Severity) ? WorkItemFieldRules.DefaultSeverity : null;
        ValueArea = kind.Has(WorkItemField.ValueArea) ? WorkItemFieldRules.DefaultValueArea : null;
        AcceptanceCriteria = kind.Has(WorkItemField.AcceptanceCriteria) ? [] : null;
        ReproSteps = kind.Has(WorkItemField.ReproSteps) ? string.Empty : null;
        SystemInfo = kind.Has(WorkItemField.SystemInfo) ? string.Empty : null;
    }

    /// <summary>
    /// The checked fields of one save (BR-M3-38): each sent field takes its value, an emptied always-set field its
    /// default (BR-M3-31). A value equal to the stored one changes nothing, numbers compared as numbers. No field
    /// changes another (BR-M3-34). Returns whether anything changed.
    /// </summary>
    private bool Apply(WorkItemFieldsEdit fields)
    {
        var changed = false;

        if (fields.Priority is { } priority)
        {
            changed |= Set(Priority, priority.Value ?? WorkItemFieldRules.DefaultPriority, value => Priority = value);
        }

        if (fields.Risk is { } risk)
        {
            changed |= Set(Risk, risk.Value, value => Risk = value);
        }

        if (fields.StoryPoints is { } storyPoints)
        {
            changed |= Set(StoryPoints, storyPoints.Value, value => StoryPoints = value);
        }

        if (fields.Effort is { } effort)
        {
            changed |= Set(Effort, effort.Value, value => Effort = value);
        }

        if (fields.BusinessValue is { } businessValue)
        {
            int? sent = businessValue.Value is { } number ? decimal.ToInt32(number) : null;
            changed |= Set(BusinessValue, sent, value => BusinessValue = value);
        }

        if (fields.TimeCriticality is { } timeCriticality)
        {
            changed |= Set(TimeCriticality, timeCriticality.Value, value => TimeCriticality = value);
        }

        if (fields.StartDate is { } startDate)
        {
            changed |= Set(StartDate, startDate.Value, value => StartDate = value);
        }

        if (fields.TargetDate is { } targetDate)
        {
            changed |= Set(TargetDate, targetDate.Value, value => TargetDate = value);
        }

        if (fields.Severity is { } severity)
        {
            changed |= Set(Severity, severity.Value ?? WorkItemFieldRules.DefaultSeverity, value => Severity = value);
        }

        if (fields.Activity is { } activity)
        {
            var sent = activity.Value is { } key ? WorkItemFieldRules.ParseActivity(key) : null;
            changed |= Set(Activity, sent, value => Activity = value);
        }

        if (fields.OriginalEstimate is { } originalEstimate)
        {
            changed |= Set(OriginalEstimate, originalEstimate.Value, value => OriginalEstimate = value);
        }

        if (fields.RemainingWork is { } remainingWork)
        {
            changed |= Set(RemainingWork, remainingWork.Value, value => RemainingWork = value);
        }

        if (fields.CompletedWork is { } completedWork)
        {
            changed |= Set(CompletedWork, completedWork.Value, value => CompletedWork = value);
        }

        if (fields.ValueArea is { } valueArea)
        {
            var sent = valueArea.Value is { } key ? WorkItemFieldRules.ParseValueArea(key) : WorkItemFieldRules.DefaultValueArea;
            changed |= Set(ValueArea, sent, value => ValueArea = value);
        }

        if (fields.AcceptanceCriteria is { } acceptanceCriteria)
        {
            if (!SequenceEqual(AcceptanceCriteria, acceptanceCriteria))
            {
                AcceptanceCriteria = acceptanceCriteria;
                changed = true;
            }
        }

        if (fields.ReproSteps is { } reproSteps)
        {
            changed |= Set(ReproSteps, reproSteps, value => ReproSteps = value);
        }

        if (fields.SystemInfo is { } systemInfo)
        {
            changed |= Set(SystemInfo, systemInfo, value => SystemInfo = value);
        }

        return changed;
    }

    /// <summary>Assigns <paramref name="value"/> when it differs from <paramref name="current"/>; whether it did.</summary>
    private static bool Set<T>(T current, T value, Action<T> assign)
    {
        if (EqualityComparer<T>.Default.Equals(current, value))
        {
            return false;
        }

        assign(value);

        return true;
    }

    /// <summary>A sent number, date or integer key: not on the type, or its value's reason.</summary>
    private static void Check<T>(
        List<WorkItemFieldReason> reasons, AppearanceToken kind, WorkItemField field, FieldEdit<T?>? sent, Func<T, string?> rule)
        where T : struct
    {
        if (sent is null)
        {
            return;
        }

        if (!kind.Has(field))
        {
            reasons.Add(new WorkItemFieldReason(field, WorkItemErrorCodes.FieldNotOnType));
        }
        else if (sent.Value is { } value && rule(value) is { } code)
        {
            reasons.Add(new WorkItemFieldReason(field, code));
        }
    }

    /// <summary>A sent list key: not on the type, or not a key of the list (BR-M3-31).</summary>
    private static void CheckKey(
        List<WorkItemFieldReason> reasons, AppearanceToken kind, WorkItemField field, FieldEdit<string?>? sent, Func<string, bool> isKey)
    {
        if (sent is null)
        {
            return;
        }

        if (!kind.Has(field))
        {
            reasons.Add(new WorkItemFieldReason(field, WorkItemErrorCodes.FieldNotOnType));
        }
        else if (sent.Value is { } key && !isKey(key))
        {
            reasons.Add(new WorkItemFieldReason(field, WorkItemErrorCodes.ValueNotInList));
        }
    }

    /// <summary>A sent text: not on the type, or too long (BR-M3-36).</summary>
    private static void CheckText(List<WorkItemFieldReason> reasons, AppearanceToken kind, WorkItemField field, string? sent)
    {
        if (sent is null)
        {
            return;
        }

        if (!kind.Has(field))
        {
            reasons.Add(new WorkItemFieldReason(field, WorkItemErrorCodes.FieldNotOnType));
        }
        else if (WorkItemFieldRules.ValidateText(sent) is { } code)
        {
            reasons.Add(new WorkItemFieldReason(field, code));
        }
    }

    /// <summary>Sent acceptance criteria scenarios: not on the type, or scenario text length reason.</summary>
    private static void CheckAcceptanceCriteria(
        List<WorkItemFieldReason> reasons, AppearanceToken kind, IReadOnlyList<AcceptanceCriterion>? sent)
    {
        if (sent is null)
        {
            return;
        }

        if (!kind.Has(WorkItemField.AcceptanceCriteria))
        {
            reasons.Add(new WorkItemFieldReason(WorkItemField.AcceptanceCriteria, WorkItemErrorCodes.FieldNotOnType));
        }
        else if (WorkItemFieldRules.ValidateAcceptanceCriteria(sent) is { } code)
        {
            reasons.Add(new WorkItemFieldReason(WorkItemField.AcceptanceCriteria, code));
        }
    }

    private static bool SequenceEqual(IReadOnlyList<AcceptanceCriterion>? first, IReadOnlyList<AcceptanceCriterion>? second)
    {
        if (ReferenceEquals(first, second))
        {
            return true;
        }

        if (first is null || second is null)
        {
            return false;
        }

        return first.SequenceEqual(second);
    }

    private void RequireOwnType(WorkItemTypeStates type)
    {
        ArgumentNullException.ThrowIfNull(type);

        if (type.TypeId != TypeId)
        {
            throw new ArgumentException("The item is checked against its own type.", nameof(type));
        }
    }

    /// <summary>The link's half on the child (BR-H-3): the new parent, or none, and the write's time.</summary>
    internal void LinkTo(Guid? parentId, DateTimeOffset now)
    {
        ParentId = parentId;
        ChangedAt = now;
    }

    /// <summary>The link's half on a live parent it adds or removes (BR-H-5): only «Изменён» moves.</summary>
    internal void MarkLinkChanged(DateTimeOffset now) => ChangedAt = now;
}

/// <summary>
/// The fields one save sends (BR-M3-25); <c>null</c> means "not sent, keep the
/// stored value". The caller has validated the texts, has resolved the area as
/// an area of the item's project (BR-M3-23), and has checked
/// <paramref name="Fields"/> with <see cref="WorkItem.CheckFields"/> (feat-022).
/// </summary>
public sealed record WorkItemEdit(
    string? Title = null,
    string? Description = null,
    Guid? StateId = null,
    AssigneeEdit? Assignee = null,
    Guid? AreaId = null,
    WorkItemFieldsEdit? Fields = null);

/// <summary>
/// The sent assignee: a user id, or <c>null</c> for «Не назначен» (BR-M3-22).
/// <paramref name="IsAvailable"/> says whether the directory knows the user as
/// active; it matters only when the user is newly set, so an assignee who has
/// become inactive since stays on every save.
/// </summary>
public sealed record AssigneeEdit(string? UserId, bool IsAvailable);
