using Mustask.Shared.Errors;

namespace Mustask.Domain.Organizations;

/// <summary>
/// The description of a project or a team (feat-028 BR-OL-3): free text kept <b>exactly as sent</b> — spaces and line breaks
/// included — at most <see cref="OrganizationLimits.DescriptionMaxLength"/> UTF-16 code units, the unit the database's
/// <c>char_length</c> counts. The empty string and <c>null</c> are both "no description" and answer <c>null</c>.
/// </summary>
public readonly record struct EntityDescription
{
    private EntityDescription(string value) => Value = value;

    /// <summary>The text as sent: never empty, never over the limit.</summary>
    public string Value { get; }

    /// <summary>The field reason of a description over the limit.</summary>
    public static FieldError LengthReason => new(OrganizationErrorCodes.DescriptionLength);

    /// <summary>
    /// Judges <paramref name="raw"/>. A <c>null</c> or empty value is no description and succeeds with <c>null</c>; a value over
    /// the limit fails. Nothing is trimmed (BR-OL-3: "kept as typed").
    /// </summary>
    public static bool TryCreate(string? raw, out EntityDescription? description)
    {
        if (string.IsNullOrEmpty(raw))
        {
            description = null;
            return true;
        }

        if (raw.Length > OrganizationLimits.DescriptionMaxLength)
        {
            description = null;
            return false;
        }

        description = new EntityDescription(raw);
        return true;
    }

    public override string ToString() => Value;
}
