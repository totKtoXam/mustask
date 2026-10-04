namespace Mustask.Domain.Process;

/// <summary>
/// What a well-formed organization identifier is (status.md D-10). The seed and
/// the read share it, so "malformed" has one definition: a UUID that is not
/// all zeros, written in the canonical hyphenated form when it arrives as text.
/// The identifier is an opaque key to this module (doc-007 R-5): nothing here
/// knows an organization.
/// </summary>
public static class OrganizationKey
{
    /// <summary>The canonical form <c>xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx</c>, the "D" format of <see cref="Guid"/>.</summary>
    private const string CanonicalFormat = "D";

    /// <summary>Characters in the canonical form: 32 hex digits and 4 hyphens.</summary>
    private const int CanonicalLength = 36;

    public static bool IsWellFormed(Guid organizationId) => organizationId != Guid.Empty;

    /// <summary>
    /// Accepts only the hyphenated 8-4-4-4-12 form, in either letter case, that
    /// is not all zeros. Braces, parentheses, the 32-digit form, padding and
    /// any other spelling are refused.
    /// </summary>
    public static bool TryParse(string? value, out Guid organizationId)
    {
        organizationId = Guid.Empty;

        // The length check is not redundant: Guid.TryParseExact trims surrounding
        // white space, and a padded value is not the canonical form.
        if (value is not { Length: CanonicalLength }
            || !Guid.TryParseExact(value, CanonicalFormat, out var parsed)
            || !IsWellFormed(parsed))
        {
            return false;
        }

        organizationId = parsed;
        return true;
    }
}
