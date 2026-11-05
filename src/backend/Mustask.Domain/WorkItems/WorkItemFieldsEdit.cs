namespace Mustask.Domain.WorkItems;

/// <summary>
/// The fields of <see cref="WorkItemField"/> one save sends (feat-022 api-contract
/// § 3.2). A member that is <c>null</c> was not sent and is not changed. A sent
/// field other than a text is a <see cref="FieldEdit{T}"/> whose
/// <see cref="FieldEdit{T}.Value"/> is <c>null</c> to empty it — on «Приоритет»,
/// «Серьёзность» and «Вид ценности» that stores the default (BR-M3-31); a sent
/// text is the text, <c>""</c> to empty it. The values are as the request
/// carried them: <see cref="WorkItem.CheckFields"/> checks them.
/// </summary>
public sealed record WorkItemFieldsEdit
{
    public FieldEdit<int?>? Priority { get; init; }

    public FieldEdit<int?>? Risk { get; init; }

    public FieldEdit<decimal?>? StoryPoints { get; init; }

    public FieldEdit<decimal?>? Effort { get; init; }

    /// <summary>A number, so that a fraction is a reason of its own (<see cref="WorkItemErrorCodes.IntegerRequired"/>).</summary>
    public FieldEdit<decimal?>? BusinessValue { get; init; }

    public FieldEdit<decimal?>? TimeCriticality { get; init; }

    public FieldEdit<DateOnly?>? StartDate { get; init; }

    public FieldEdit<DateOnly?>? TargetDate { get; init; }

    public FieldEdit<int?>? Severity { get; init; }

    /// <summary>The list key as sent; anything but a member name of <see cref="WorkItemActivity"/> is not in the list.</summary>
    public FieldEdit<string?>? Activity { get; init; }

    public FieldEdit<decimal?>? OriginalEstimate { get; init; }

    public FieldEdit<decimal?>? RemainingWork { get; init; }

    public FieldEdit<decimal?>? CompletedWork { get; init; }

    /// <summary>The list key as sent; anything but a member name of <see cref="WorkItemValueArea"/> is not in the list.</summary>
    public FieldEdit<string?>? ValueArea { get; init; }

    public IReadOnlyList<AcceptanceCriterion>? AcceptanceCriteria { get; init; }

    public string? ReproSteps { get; init; }

    public string? SystemInfo { get; init; }
}

/// <summary>A sent field: its value, or <c>null</c> to empty it.</summary>
public sealed record FieldEdit<T>(T Value);

/// <summary>One field reason of a save's fields (feat-022 api-contract § 3.2): the field and its code.</summary>
public sealed record WorkItemFieldReason(WorkItemField Field, string Code);
