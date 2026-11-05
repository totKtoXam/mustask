using System.Text;
using Mustask.Domain.UserDirectory;
using Mustask.Shared.Errors;

namespace Mustask.Domain.Organizations;

/// <summary>
/// The «Причина» of an administrator appointment (feat-033 BR-RS-5): trimmed, 1 to
/// <see cref="OrganizationLimits.AppointmentReasonMaxLength"/> Unicode code points (an emoji is one, as the panel counts with
/// <c>Array.from(text.trim()).length</c>), and free of U+0000 and Unicode noncharacters, which PostgreSQL cannot store or the
/// platform's text rules refuse.
/// </summary>
public readonly record struct AppointmentReason
{
    private AppointmentReason(string value) => Value = value;

    /// <summary>The reason as stored and shown: trimmed.</summary>
    public string Value { get; }

    /// <summary>Checks <paramref name="raw"/>; on a refusal <paramref name="error"/> is the field reason of <c>reason</c>.</summary>
    public static bool TryCreate(string? raw, out AppointmentReason reason, out FieldError? error)
    {
        var trimmed = raw is null ? null : TrimLikeThePanel(raw);

        if (string.IsNullOrEmpty(trimmed))
        {
            reason = default;
            error = new FieldError(OrganizationErrorCodes.RescueReasonRequired);
            return false;
        }

        if (DirectorySearchText.CodePointCount(trimmed) > OrganizationLimits.AppointmentReasonMaxLength)
        {
            reason = default;
            error = new FieldError(OrganizationErrorCodes.RescueReasonTooLong);
            return false;
        }

        foreach (var rune in trimmed.EnumerateRunes())
        {
            if (rune.Value == 0 || DirectorySearchText.IsNoncharacter(rune))
            {
                reason = default;
                error = new FieldError(OrganizationErrorCodes.RescueReasonInvalid);
                return false;
            }
        }

        reason = new AppointmentReason(trimmed);
        error = null;
        return true;
    }

    /// <summary>
    /// <c>String.prototype.trim</c> of the panel (ECMAScript WhiteSpace + LineTerminator): tab, VT, FF, space, U+00A0, U+FEFF, every
    /// Unicode space separator (Zs), U+2028 and U+2029, LF, CR. .NET's <c>Trim()</c> differs in two places: it keeps U+FEFF and it
    /// removes U+0085 (and the other C0/C1 separators), which the panel keeps. BR-RS-5: both ends count the same way.
    /// </summary>
    private static string TrimLikeThePanel(string text)
    {
        var start = 0;
        var end = text.Length;

        while (start < end && IsPanelWhiteSpace(text[start]))
        {
            start++;
        }

        while (end > start && IsPanelWhiteSpace(text[end - 1]))
        {
            end--;
        }

        return text[start..end];
    }

    private static bool IsPanelWhiteSpace(char c) =>
        c is (char)0x09 or (char)0x0A or (char)0x0B or (char)0x0C or (char)0x0D or (char)0x2028 or (char)0x2029 or (char)0xFEFF
        || System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) == System.Globalization.UnicodeCategory.SpaceSeparator;
}
