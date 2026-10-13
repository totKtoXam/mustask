namespace Mustask.Domain.WorkItems;

/// <summary>
/// The rules of a work item's two texts (spec § Data model): Заголовок is a
/// trimmed single line of 1..255 characters (BR-M3-3), Описание plain text of
/// 0..100 000 characters stored as entered (BR-M3-19). Lengths count UTF-16
/// code units, as the database's <c>char_length</c> checks and the browser's
/// <c>maxLength</c> do for every character outside the astral planes.
/// </summary>
public static class WorkItemText
{
    /// <summary>
    /// The characters that break a line (Unicode's mandatory breaks): a title
    /// holding any of them is not single-line.
    /// </summary>
    private static readonly char[] LineBreaks = ['\n', '\r', '\u000B', '\u000C', '\u0085', '\u2028', '\u2029'];

    /// <summary>
    /// Trims <paramref name="raw"/> and checks it. Returns the field reason, or
    /// <c>null</c> with the stored value in <paramref name="title"/>. One reason,
    /// the first that applies: required, then too long, then multiline.
    /// </summary>
    public static string? ValidateTitle(string? raw, out string title)
    {
        title = raw?.Trim() ?? string.Empty;

        if (title.Length == 0)
        {
            return WorkItemErrorCodes.TitleRequired;
        }

        if (title.Length > WorkItemLimits.TitleMaxLength)
        {
            return WorkItemErrorCodes.TitleTooLong;
        }

        return title.IndexOfAny(LineBreaks) >= 0 ? WorkItemErrorCodes.TitleMultiline : null;
    }

    /// <summary>The field reason of a plain description, or <c>null</c>: it is stored exactly as sent.</summary>
    public static string? ValidateDescription(string description) => ValidatePlain(description);

    /// <summary>
    /// feat-018 BR-RT-10: the declared format of a write. <c>null</c> is "not declared" (<paramref name="format"/>
    /// <c>null</c>, no reason); anything but exactly <c>plain</c> or <c>html</c> is
    /// <see cref="WorkItemErrorCodes.DescriptionFormatUnknown"/>, whether or not a description came with it.
    /// </summary>
    public static string? ValidateFormat(string? raw, out DescriptionFormat? format)
    {
        format = null;

        if (raw is null)
        {
            return null;
        }

        if (!DescriptionFormats.TryParse(raw, out var parsed))
        {
            return WorkItemErrorCodes.DescriptionFormatUnknown;
        }

        format = parsed;

        return null;
    }

    /// <summary>A plain description: at most 100 000 UTF-16 code units, stored as sent (BR-M3-19).</summary>
    public static string? ValidatePlain(string description)
    {
        ArgumentNullException.ThrowIfNull(description);

        return description.Length > WorkItemLimits.DescriptionMaxLength ? WorkItemErrorCodes.DescriptionTooLong : null;
    }

    /// <summary>
    /// The pre-parse cap of an html description (BR-RT-7): more than 2 000 000 UTF-16 units is refused with the markup
    /// reason before anything reads it.
    /// </summary>
    public static string? CheckRequestSize(string raw)
    {
        ArgumentNullException.ThrowIfNull(raw);

        return raw.Length > WorkItemLimits.DescriptionRequestMaxLength ? WorkItemErrorCodes.DescriptionMarkupTooLarge : null;
    }

    /// <summary>
    /// The limits of a reduced html description in BR-RT-7's order, one reason: the markup ceiling first, then the
    /// visible text, so a value over both gets the markup reason. Both counts are Unicode code points.
    /// </summary>
    public static string? ValidateReduced(int markupCodePoints, int visibleCodePoints)
    {
        if (markupCodePoints > WorkItemLimits.DescriptionMarkupMaxLength)
        {
            return WorkItemErrorCodes.DescriptionMarkupTooLarge;
        }

        return visibleCodePoints > WorkItemLimits.DescriptionVisibleMaxLength ? WorkItemErrorCodes.DescriptionTooLong : null;
    }

    /// <summary>
    /// What a reduced html description is stored as (BR-RT-6): the value as html, or <c>""</c> plain when its visible
    /// text has no non-whitespace character. The reducer decides <paramref name="hasContent"/> from the text it read.
    /// </summary>
    public static WorkItemDescription Describe(string reducedHtml, bool hasContent)
    {
        ArgumentNullException.ThrowIfNull(reducedHtml);

        return hasContent ? WorkItemDescription.Of(reducedHtml, DescriptionFormat.Html) : WorkItemDescription.Empty;
    }

    /// <summary>The number of Unicode code points of <paramref name="text"/> (a lone surrogate counts one).</summary>
    public static int CodePoints(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var count = 0;

        foreach (var _ in text.EnumerateRunes())
        {
            count++;
        }

        return count;
    }
}
