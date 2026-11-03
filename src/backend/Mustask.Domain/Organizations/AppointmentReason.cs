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
        var trimmed = raw?.Trim();

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
}
