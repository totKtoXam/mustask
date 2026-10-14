namespace Mustask.Domain.WorkItems;

/// <summary>«Вид работ» (feat-022 spec § The field table): stored and sent by its member name.</summary>
public enum WorkItemActivity
{
    Deployment,
    Design,
    Development,
    Documentation,
    Requirements,
    Testing,
}

/// <summary>«Вид ценности»: «Бизнес» or «Архитектура». Stored and sent by its member name.</summary>
public enum WorkItemValueArea
{
    Business,
    Architectural,
}

/// <summary>
/// The values the fields of <see cref="WorkItemField"/> take (BR-M3-31..BR-M3-37):
/// the fixed lists' keys, the number range and precision, the date range, the
/// text length and the defaults. None changes the business without a release,
/// so they are constants next to the concept that owns them. Each check answers
/// the field reason, or <c>null</c>; one reason, the first that applies.
/// </summary>
public static class WorkItemFieldRules
{
    /// <summary>«Приоритет» of a new item, and of one whose priority a save empties (BR-M3-31, BR-M3-37).</summary>
    public const int DefaultPriority = 2;

    /// <summary>«Серьёзность» «3 — Средняя» of a new bug, and of one whose severity a save empties.</summary>
    public const int DefaultSeverity = 3;

    /// <summary>«Вид ценности» «Бизнес» of a new epic, feature or story, and of one whose value area a save empties.</summary>
    public const WorkItemValueArea DefaultValueArea = WorkItemValueArea.Business;

    /// <summary>«Приоритет» runs from 1, the highest, to 4.</summary>
    public const int HighestPriority = 1;

    public const int LowestPriority = 4;

    /// <summary>«Риск»: 1 «Высокий», 2 «Средний», 3 «Низкий».</summary>
    public const int HighestRisk = 1;

    public const int LowestRisk = 3;

    /// <summary>«Серьёзность»: 1 «Критическая» to 4 «Низкая».</summary>
    public const int HighestSeverity = 1;

    public const int LowestSeverity = 4;

    /// <summary>The largest value of a number field; the smallest is 0 (BR-M3-32).</summary>
    public const int NumberMax = 9_999;

    /// <summary>The digits a decimal field holds after the point (BR-M3-32).</summary>
    public const int NumberDecimals = 2;

    /// <summary>The longest type text field, in UTF-16 code units, as Описание (BR-M3-36).</summary>
    public const int TextMaxLength = 100_000;

    /// <summary>The earliest date a date field takes (BR-M3-35).</summary>
    public static readonly DateOnly EarliestDate = new(2000, 1, 1);

    /// <summary>The latest date a date field takes (BR-M3-35).</summary>
    public static readonly DateOnly LatestDate = new(2099, 12, 31);

    public static string? ValidatePriority(int value) => InRange(value, HighestPriority, LowestPriority);

    public static string? ValidateRisk(int value) => InRange(value, HighestRisk, LowestRisk);

    public static string? ValidateSeverity(int value) => InRange(value, HighestSeverity, LowestSeverity);

    /// <summary>A decimal field: in range, then at most two digits after the point. Never rounded.</summary>
    public static string? ValidateDecimal(decimal value)
    {
        if (value < 0 || value > NumberMax)
        {
            return WorkItemErrorCodes.NumberOutOfRange;
        }

        return decimal.Round(value, NumberDecimals) != value ? WorkItemErrorCodes.NumberTooPrecise : null;
    }

    /// <summary>«Бизнес-ценность»: in range, then an integer. Never rounded.</summary>
    public static string? ValidateInteger(decimal value)
    {
        if (value < 0 || value > NumberMax)
        {
            return WorkItemErrorCodes.NumberOutOfRange;
        }

        return decimal.Truncate(value) != value ? WorkItemErrorCodes.IntegerRequired : null;
    }

    public static string? ValidateDate(DateOnly value) =>
        value < EarliestDate || value > LatestDate ? WorkItemErrorCodes.DateOutOfRange : null;

    public static string? ValidateText(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        return value.Length > TextMaxLength ? WorkItemErrorCodes.TextTooLong : null;
    }

    /// <summary>Validates each scenario in the acceptance criteria list (R1).</summary>
    public static string? ValidateAcceptanceCriteria(IReadOnlyList<AcceptanceCriterion> criteria)
    {
        ArgumentNullException.ThrowIfNull(criteria);

        foreach (var criterion in criteria)
        {
            if (criterion is null)
            {
                continue;
            }

            if (ValidateText(criterion.Given) is { } givenCode)
            {
                return givenCode;
            }

            if (ValidateText(criterion.When) is { } whenCode)
            {
                return whenCode;
            }

            if (ValidateText(criterion.Then) is { } thenCode)
            {
                return thenCode;
            }
        }

        return null;
    }

    /// <summary>A «Вид работ» key, exactly as § The field table spells it, or <c>null</c>.</summary>
    public static WorkItemActivity? ParseActivity(string key) => Parse<WorkItemActivity>(key);

    /// <summary>A «Вид ценности» key, exactly as § The field table spells it, or <c>null</c>.</summary>
    public static WorkItemValueArea? ParseValueArea(string key) => Parse<WorkItemValueArea>(key);

    /// <summary>Whether a target date earlier than the start date stands once both are set (BR-M3-35).</summary>
    public static bool TargetBeforeStart(DateOnly? startDate, DateOnly? targetDate) =>
        startDate is { } start && targetDate is { } target && target < start;

    private static string? InRange(int value, int first, int last) =>
        value < first || value > last ? WorkItemErrorCodes.ValueNotInList : null;

    /// <summary>
    /// A member name, case-sensitive. <see cref="Enum.TryParse{TEnum}(string, out TEnum)"/> would also take
    /// a number or a comma list, which are not keys of the list.
    /// </summary>
    private static T? Parse<T>(string key)
        where T : struct, Enum
    {
        ArgumentNullException.ThrowIfNull(key);

        return Enum.GetNames<T>().Contains(key, StringComparer.Ordinal) ? Enum.Parse<T>(key) : null;
    }
}
