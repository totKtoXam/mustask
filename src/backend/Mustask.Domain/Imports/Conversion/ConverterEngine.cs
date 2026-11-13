using System.Globalization;
using System.Text.RegularExpressions;

namespace Mustask.Domain.Imports.Conversion;

public static class ConverterIds
{
    public const string Trim = "trim";
    public const string Truncate = "truncate";
    public const string Number = "number";
    public const string ExcelDate = "excelDate";
    public const string DateFormat = "dateFormat";
    public const string IsoDate = "isoDate";
    public const string Split = "split";
    public const string Hyperlink = "hyperlink";
    public const string Lookup = "lookup";
    public const string Extract = "extract";
    public const string Merge = "merge";
    public const string Constant = "constant";

    public static readonly IReadOnlyList<string> Steps = [Trim, Truncate, Number, ExcelDate, DateFormat, IsoDate, Split, Hyperlink, Lookup, Extract];
}

public static class DateFormatIds
{
    public const string DmyDot = "dmy_dot";
    public const string YmdDash = "ymd_dash";
    public const string DmySlash = "dmy_slash";
    public const string MdySlash = "mdy_slash";

    public static string? ToPattern(string? id) => id switch
    {
        DmyDot => "d.M.yyyy",
        YmdDash => "yyyy-M-d",
        DmySlash => "d/M/yyyy",
        MdySlash => "M/d/yyyy",
        _ => null,
    };
}

/// <summary>One chain step with its flat parameters (unused ones stay <c>null</c>).</summary>
public sealed record ConverterStep(
    string Converter,
    bool Collapse = false,
    int? Length = null,
    string? Format = null,
    string? Separator = null,
    string? Pattern = null,
    int? Group = null,
    string? From = null,
    string? To = null);

/// <summary>A converter failure: the converter that failed, and whether it was the regex time limit.</summary>
public sealed record ConversionFailure(string Converter, bool Timeout);

public readonly record struct ConversionResult(ConversionValue Value, ConversionFailure? Failure, IReadOnlyList<ConversionValue> Steps)
{
    public bool Ok => Failure is null;
}

/// <summary>Runs converter chains (doc-017 BR-IE-8, BR-IE-9). Pure: no clock, no I/O; a regex runs under <see cref="ImportLimits.PatternTimeout"/>.</summary>
public static class ConverterEngine
{
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
    private static readonly DateOnly Min = new(2000, 1, 1);
    private static readonly DateOnly Max = new(2099, 12, 31);

    public static ConversionResult RunChain(ConversionValue start, IReadOnlyList<ConverterStep> chain)
    {
        var value = start;
        var steps = new List<ConversionValue>();

        foreach (var step in chain)
        {
            ConversionValue? next;

            try
            {
                next = Apply(value, step);
            }
            catch (RegexMatchTimeoutException)
            {
                return new(value, new ConversionFailure(step.Converter, true), steps);
            }

            if (next is null)
            {
                return new(value, new ConversionFailure(step.Converter, false), steps);
            }

            steps.Add(next);
            value = next;
        }

        return new(value, null, steps);
    }

    /// <summary>Whether <paramref name="pattern"/> compiles; used by the template validator.</summary>
    public static bool IsValidPattern(string pattern)
    {
        try
        {
            _ = new Regex(pattern, RegexOptions.CultureInvariant, ImportLimits.PatternTimeout);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    public static bool DateInRange(DateOnly date) => date >= Min && date <= Max;

    /// <summary>The «{column}» placeholders of a merge template, in order of appearance.</summary>
    public static IReadOnlyList<string> MergePlaceholders(string template) =>
        Regex.Matches(template, @"\{([^{}]+)\}").Select(m => m.Groups[1].Value).ToList();

    /// <summary>Fills a merge template; <paramref name="cell"/> returns a column's text ("" when empty).</summary>
    public static ConversionValue Merge(string template, Func<string, string> cell) =>
        ConversionValue.OfText(Regex.Replace(template, @"\{([^{}]+)\}", m => cell(m.Groups[1].Value)).Trim());

    /// <summary>Text of a number or date for a text field: a comma decimal, <c>dd.MM.yyyy</c>.</summary>
    public static string ToFieldText(ConversionValue value) =>
        value.Kind == ConversionKind.Date ? value.Date.ToString("dd.MM.yyyy", Invariant) : value.Display();

    /// <summary>A value after a step, or <c>null</c> when the step cannot convert it. An empty value passes every step unchanged.</summary>
    private static ConversionValue? Apply(ConversionValue value, ConverterStep step)
    {
        if (value.IsEmpty)
        {
            return value;
        }

        return step.Converter switch
        {
            ConverterIds.Trim => Trim(value, step),
            ConverterIds.Truncate => Truncate(value, step),
            ConverterIds.Number => ToNumber(value),
            ConverterIds.ExcelDate => ExcelDate(value),
            ConverterIds.DateFormat => ParseDate(value, step.Format),
            ConverterIds.IsoDate => ParseDate(value, DateFormatIds.YmdDash),
            ConverterIds.Split => Split(value, step),
            ConverterIds.Hyperlink => value, // the reader already put the link target into the cell text where there is one
            ConverterIds.Lookup => Lookup(value, step),
            ConverterIds.Extract => Extract(value, step),
            _ => null,
        };
    }

    private static ConversionValue Trim(ConversionValue value, ConverterStep step)
    {
        var text = value.Display().Trim();

        if (step.Collapse)
        {
            text = Regex.Replace(text, @"\s+", " ", RegexOptions.CultureInvariant);
        }

        return ConversionValue.OfText(text);
    }

    private static ConversionValue? Truncate(ConversionValue value, ConverterStep step)
    {
        if (step.Length is not > 0)
        {
            return null;
        }

        var text = value.Display();
        return ConversionValue.OfText(text.Length <= step.Length ? text : text[..step.Length.Value]);
    }

    private static ConversionValue? ToNumber(ConversionValue value)
    {
        if (value.Kind == ConversionKind.Number)
        {
            return value;
        }

        if (value.Kind is ConversionKind.Date or ConversionKind.List)
        {
            return null;
        }

        var text = new string(value.Text.Where(c => !char.IsWhiteSpace(c) && c != ' ').ToArray()).Replace(',', '.');
        return decimal.TryParse(text, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, Invariant, out var number)
            ? ConversionValue.OfNumber(number)
            : null;
    }

    private static ConversionValue? ExcelDate(ConversionValue value)
    {
        if (value.Kind == ConversionKind.Date)
        {
            return value;
        }

        var number = ToNumber(value);

        if (number is null)
        {
            return null;
        }

        var serial = Math.Floor(number.Number);

        if (serial is < 1 or > 2958465)
        {
            return null;
        }

        return ConversionValue.OfDate(DateOnly.FromDateTime(new DateTime(1899, 12, 30).AddDays((double)serial)));
    }

    private static ConversionValue? ParseDate(ConversionValue value, string? format)
    {
        if (value.Kind == ConversionKind.Date)
        {
            return value;
        }

        var pattern = DateFormatIds.ToPattern(format);

        if (pattern is null || value.Kind != ConversionKind.Text)
        {
            return null;
        }

        return DateOnly.TryParseExact(value.Text.Trim(), pattern, Invariant, DateTimeStyles.None, out var date)
            ? ConversionValue.OfDate(date)
            : null;
    }

    private static ConversionValue? Split(ConversionValue value, ConverterStep step)
    {
        if (string.IsNullOrEmpty(step.Separator) || value.Kind == ConversionKind.Date)
        {
            return null;
        }

        var items = value.Display()
            .Split(step.Separator, StringSplitOptions.None)
            .Select(item => item.Trim())
            .Where(item => item.Length > 0)
            .ToList();

        return ConversionValue.OfList(items);
    }

    private static ConversionValue? Lookup(ConversionValue value, ConverterStep step)
    {
        // A one-pair replacement: a value equal (ignoring case and edge spaces) to From becomes To; any other value stays.
        if (step.From is null)
        {
            return null;
        }

        return string.Equals(value.Display().Trim(), step.From.Trim(), StringComparison.OrdinalIgnoreCase)
            ? ConversionValue.OfText(step.To ?? string.Empty)
            : value;
    }

    private static ConversionValue? Extract(ConversionValue value, ConverterStep step)
    {
        if (string.IsNullOrEmpty(step.Pattern) || step.Pattern.Length > ImportLimits.MaxPatternLength)
        {
            return null;
        }

        Regex regex;

        try
        {
            regex = new Regex(step.Pattern, RegexOptions.CultureInvariant, ImportLimits.PatternTimeout);
        }
        catch (ArgumentException)
        {
            return null;
        }

        var match = regex.Match(value.Display());

        if (!match.Success)
        {
            return ConversionValue.Empty;
        }

        var group = step.Group ?? 0;
        return group < match.Groups.Count ? ConversionValue.OfText(match.Groups[group].Value.Trim()) : null;
    }
}
