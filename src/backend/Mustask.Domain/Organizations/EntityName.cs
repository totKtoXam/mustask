using Mustask.Shared.Errors;

namespace Mustask.Domain.Organizations;

/// <summary>
/// The name of an organization, project, team or area (BR-M1-1): trimmed,
/// 1 to 100 UTF-16 code units, and compared ignoring letter case and
/// surrounding spaces. <see cref="Normalized"/> is the comparison key the
/// unique indexes hold (ADR-0026 rule 6). <see cref="string.ToUpperInvariant()"/>
/// maps one code unit to one, so the key is as wide as the name, and it does
/// not depend on the database's locale (plan-backend § 3).
/// </summary>
public readonly record struct EntityName
{
    private EntityName(string value)
    {
        Value = value;
        Normalized = Normalize(value);
    }

    /// <summary>The name as stored and shown: trimmed.</summary>
    public string Value { get; }

    /// <summary>The comparison key: the trimmed name, upper-cased with the invariant culture.</summary>
    public string Normalized { get; }

    /// <summary>The field reason of a name that breaks the length rule.</summary>
    public static FieldError LengthReason => new(OrganizationErrorCodes.NameLength);

    /// <summary>
    /// Trims <paramref name="raw"/> and checks its length. A null, an empty
    /// value and spaces only all fail, as does a value over
    /// <see cref="OrganizationLimits.NameMaxLength"/> after trimming.
    /// </summary>
    public static bool TryCreate(string? raw, out EntityName name)
    {
        var trimmed = raw?.Trim();

        if (trimmed is not { Length: > 0 and <= OrganizationLimits.NameMaxLength })
        {
            name = default;
            return false;
        }

        name = new EntityName(trimmed);
        return true;
    }

    /// <summary>The comparison key of a raw value, trimmed first. The same rule as <see cref="Normalized"/>.</summary>
    public static string Normalize(string value) => value.Trim().ToUpperInvariant();

    public override string ToString() => Value;
}
