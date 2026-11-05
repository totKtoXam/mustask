namespace Mustask.Domain.WorkItems;

/// <summary>
/// How a work item's Описание is to be read (feat-018 BR-RT-10): exactly the characters it holds
/// (<see cref="Plain"/>, mod-003 BR-M3-19) or the server-reduced safe html subset (<see cref="Html"/>, ADR-0037).
/// Stored by name (conventions, "Named values" rule 2); the wire key is <see cref="DescriptionFormats.Key"/>.
/// </summary>
public enum DescriptionFormat
{
    Plain,
    Html,
}

/// <summary>The wire keys of <see cref="DescriptionFormat"/> (feat-018 api-contract § 1): lower case, exact.</summary>
public static class DescriptionFormats
{
    public const string PlainKey = "plain";

    public const string HtmlKey = "html";

    /// <summary>The key the API writes for <paramref name="format"/>.</summary>
    public static string Key(DescriptionFormat format) => format switch
    {
        DescriptionFormat.Plain => PlainKey,
        DescriptionFormat.Html => HtmlKey,
        _ => throw new ArgumentOutOfRangeException(nameof(format), format, "Unknown description format."),
    };

    /// <summary>
    /// The format a request key names. Exact and case-sensitive, no trimming: <c>"HTML"</c>, <c>" html"</c> and
    /// <c>""</c> are not formats.
    /// </summary>
    public static bool TryParse(string? key, out DescriptionFormat format)
    {
        switch (key)
        {
            case PlainKey:
                format = DescriptionFormat.Plain;
                return true;
            case HtmlKey:
                format = DescriptionFormat.Html;
                return true;
            default:
                format = DescriptionFormat.Plain;
                return false;
        }
    }
}

/// <summary>
/// A description with the format it is in: the unit BR-RT-10 compares ("(value, format)"). An empty value is always
/// <see cref="DescriptionFormat.Plain"/>, so a cleared description and a never-written one are the same pair
/// (api-contract § 1.1).
/// </summary>
public readonly record struct WorkItemDescription
{
    private WorkItemDescription(string value, DescriptionFormat format)
    {
        Value = value;
        Format = format;
    }

    public static WorkItemDescription Empty { get; } = new(string.Empty, DescriptionFormat.Plain);

    public string Value { get; }

    public DescriptionFormat Format { get; }

    /// <summary>The pair; an empty <paramref name="value"/> is plain whatever <paramref name="format"/> says.</summary>
    public static WorkItemDescription Of(string value, DescriptionFormat format)
    {
        ArgumentNullException.ThrowIfNull(value);

        return value.Length == 0 ? Empty : new WorkItemDescription(value, format);
    }
}
