using System.Text;

namespace Mustask.Infrastructure.Imports.Reading;

/// <summary>What a cell's number format makes of its number.</summary>
internal enum NumberFormatKind
{
    /// <summary>A plain number (or text, currency, percentage…).</summary>
    General,

    /// <summary>A date, with or without a time.</summary>
    Date,

    /// <summary>A time of day or a duration only: stays a number.</summary>
    Time,
}

/// <summary>
/// Decides from a style's number format whether a numeric cell shows a date (BR-IE-3: a cell gives its typed value), and turns an Excel
/// serial into a date in the workbook's 1900 or 1904 system.
/// </summary>
internal static class NumberFormats
{
    public static NumberFormatKind Classify(int formatId, string? customCode)
    {
        if (customCode is null)
        {
            if (formatId is (>= 14 and <= 17) or 22 or (>= 27 and <= 36) or (>= 50 and <= 58))
            {
                return NumberFormatKind.Date;
            }

            return formatId is (>= 18 and <= 21) or (>= 45 and <= 47) ? NumberFormatKind.Time : NumberFormatKind.General;
        }

        var hasYearOrDay = false;
        var hasMonthOrMinute = false;
        var hasHourOrSecond = false;
        var inQuote = false;
        var inBracket = false;
        var bracket = new StringBuilder();

        for (var i = 0; i < customCode.Length; i++)
        {
            var c = customCode[i];

            if (inQuote)
            {
                inQuote = c != '"';
                continue;
            }

            if (inBracket)
            {
                if (c == ']')
                {
                    inBracket = false;
                    var content = bracket.ToString().ToLowerInvariant();

                    // [h], [mm], [ss] are elapsed-time parts; every other bracket is a colour, a condition or a locale.
                    if (content.Length > 0 && content.All(ch => ch == 'h'))
                    {
                        hasHourOrSecond = true;
                    }
                    else if (content.Length > 0 && content.All(ch => ch is 'm' or 's'))
                    {
                        hasHourOrSecond = true;
                    }

                    bracket.Clear();
                }
                else
                {
                    bracket.Append(c);
                }

                continue;
            }

            switch (char.ToLowerInvariant(c))
            {
                case '"':
                    inQuote = true;
                    break;
                case '[':
                    inBracket = true;
                    break;
                case '\\':
                case '_':
                case '*':
                    i++;
                    break;
                case 'y':
                case 'd':
                    hasYearOrDay = true;
                    break;
                case 'm':
                    hasMonthOrMinute = true;
                    break;
                case 'h':
                case 's':
                    hasHourOrSecond = true;
                    break;
            }
        }

        if (hasYearOrDay || (hasMonthOrMinute && !hasHourOrSecond))
        {
            return NumberFormatKind.Date;
        }

        return hasHourOrSecond ? NumberFormatKind.Time : NumberFormatKind.General;
    }

    /// <summary>The date of an Excel serial in the 1900 or 1904 system; <c>null</c> when it is out of the range a date can hold.</summary>
    public static DateTime? FromSerial(double serial, bool system1904)
    {
        if (double.IsNaN(serial) || double.IsInfinity(serial) || serial < 0 || serial > 2_958_465)
        {
            return null;
        }

        try
        {
            if (system1904)
            {
                return new DateTime(1904, 1, 1, 0, 0, 0, DateTimeKind.Unspecified).AddDays(serial);
            }

            // The 1900 system counts a 29 February 1900 that never existed: serials below 61 are one day off a plain count.
            if (serial < 1)
            {
                return null;
            }

            if (serial < 60)
            {
                return DateTime.FromOADate(serial + 1);
            }

            if (serial < 61)
            {
                return new DateTime(1900, 3, 1);
            }

            return DateTime.FromOADate(serial);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }
}
