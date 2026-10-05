using System.Text.RegularExpressions;

namespace Mustask.Domain.Organizations;

/// <summary>
/// A project's key (BR-M1-1, design § 4.4): 2 to 10 Latin capital letters and
/// digits, starting with a letter, judged exactly as sent: never trimmed and
/// never upper-cased. It is unique in its organization among projects of any
/// status and never changes.
/// </summary>
public static partial class ProjectKey
{
    // \z, not $: in .NET "$" also matches before a final line feed, and "PRM\n" is not a key.
    [GeneratedRegex(@"^[A-Z][A-Z0-9]{1,9}\z", RegexOptions.CultureInvariant)]
    private static partial Regex Pattern { get; }

    /// <summary>
    /// <c>null</c> when <paramref name="key"/> is a well-formed key; otherwise the
    /// field reason: <see cref="OrganizationErrorCodes.ProjectKeyRequired"/> for a
    /// null or empty key, <see cref="OrganizationErrorCodes.ProjectKeyInvalid"/> for
    /// anything else, «РМК» and «prm» included.
    /// </summary>
    public static string? Validate(string? key)
    {
        if (string.IsNullOrEmpty(key))
        {
            return OrganizationErrorCodes.ProjectKeyRequired;
        }

        return Pattern.IsMatch(key) ? null : OrganizationErrorCodes.ProjectKeyInvalid;
    }

    public static bool IsWellFormed(string? key) => Validate(key) is null;
}
