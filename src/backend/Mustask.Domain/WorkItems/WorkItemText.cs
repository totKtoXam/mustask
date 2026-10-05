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

    /// <summary>The field reason of a description, or <c>null</c>: it is stored exactly as sent.</summary>
    public static string? ValidateDescription(string description)
    {
        ArgumentNullException.ThrowIfNull(description);

        return description.Length > WorkItemLimits.DescriptionMaxLength ? WorkItemErrorCodes.DescriptionTooLong : null;
    }
}
