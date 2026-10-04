using System.Text;

namespace Mustask.Domain.UserDirectory;

/// <summary>
/// How a search text matches (BR-UP-14; api-contract.md A.3): a substring of
/// the display name or of the e-mail, both sides normalised the same way —
/// Unicode NFC, lower case, <c>ё</c> → <c>е</c>, control characters removed, a
/// noncharacter read as U+FFFD (<see cref="Normalize(string)"/>).
/// The stored side is normalised once, on write (<see cref="ProjectedUser"/>),
/// so a search is two <c>LIKE</c>s over plain columns.
/// </summary>
public sealed class DirectorySearchText
{
    /// <summary>The escape character of <see cref="LikePattern"/>.</summary>
    public const char LikeEscape = '\\';

    /// <summary>What a noncharacter becomes in <see cref="Normalize(string)"/>: U+FFFD, as the provider writes it.</summary>
    public const char ReplacementCharacter = '�';

    private DirectorySearchText(DirectorySearchTextKind kind, string likePattern)
    {
        Kind = kind;
        LikePattern = likePattern;
    }

    public DirectorySearchTextKind Kind { get; }

    /// <summary>
    /// For <see cref="DirectorySearchTextKind.Pattern"/>: <c>%text%</c> with
    /// <c>%</c>, <c>_</c> and <c>\</c> escaped by <see cref="LikeEscape"/>;
    /// empty otherwise.
    /// </summary>
    public string LikePattern { get; }

    /// <summary>
    /// Trims the text and classifies it. Fewer than
    /// <see cref="UserDirectoryLimits.SearchTextMinLength"/> code points after
    /// trimming — or after normalisation, so a text of control characters can
    /// never become a pattern that matches everyone — is too short; more than
    /// <see cref="UserDirectoryLimits.SearchTextMaxLength"/> is too long.
    /// </summary>
    public static DirectorySearchText Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var trimmed = text.Trim();
        var length = CodePointCount(trimmed);

        if (length > UserDirectoryLimits.SearchTextMaxLength)
        {
            return new DirectorySearchText(DirectorySearchTextKind.TooLong, string.Empty);
        }

        var normalized = Normalize(trimmed);

        if (length < UserDirectoryLimits.SearchTextMinLength
            || CodePointCount(normalized) < UserDirectoryLimits.SearchTextMinLength)
        {
            return new DirectorySearchText(DirectorySearchTextKind.TooShort, string.Empty);
        }

        return new DirectorySearchText(DirectorySearchTextKind.Pattern, string.Concat("%", EscapeLike(normalized), "%"));
    }

    /// <summary>
    /// The normalisation both sides of a match go through. It is total — no
    /// text makes it throw, so no text is ever a 500 on the push channel or in
    /// a search (D-5): a control character is removed, a noncharacter
    /// (<see cref="IsNoncharacter"/>) becomes U+FFFD — the replacement the
    /// provider makes in a name, so a text copied from Keycloak finds the user
    /// as MUSTASK holds it — and a text the platform's NFC cannot take
    /// (.NET throws on U+FFFE; the throwing set belongs to the runtime and its
    /// ICU, not to this code) goes on without the composition step.
    /// </summary>
    public static string Normalize(string value) => Normalize(value, ComposeNfc);

    /// <summary><see cref="Normalize(string)"/> with the composition step given, so a test can make it throw.</summary>
    internal static string Normalize(string value, Func<string, string> compose)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentNullException.ThrowIfNull(compose);

        var builder = new StringBuilder(value.Length);

        foreach (var rune in value.EnumerateRunes())
        {
            if (IsNoncharacter(rune))
            {
                builder.Append(ReplacementCharacter);
            }
            else if (!Rune.IsControl(rune))
            {
                builder.Append(rune.ToString());
            }
        }

        var assembled = builder.ToString();
        string composed;

        try
        {
            composed = compose(assembled);
        }
        catch (ArgumentException)
        {
            composed = assembled;
        }

        // After NFC a decomposed "е" + U+0308 is the single "ё", and after
        // lower-casing "Ё" is "ё" too — one replacement covers all four.
        return composed.ToLowerInvariant().Replace('ё', 'е');
    }

    /// <summary>
    /// A Unicode noncharacter: U+FDD0..U+FDEF and the last two code points of
    /// every plane (U+FFFE, U+FFFF, U+1FFFE, … U+10FFFF) — sixty-six values the
    /// standard reserves for a program's internal use, never for text a person
    /// reads. A name or e-mail holding one is refused (<c>value_invalid</c>,
    /// api-contract.md B.3) and a search text holding one is read as U+FFFD.
    /// The set is fixed by the standard, so the rule does not move with the
    /// runtime; U+FFFE is the one value .NET's NFC throws on (D-5).
    /// </summary>
    public static bool IsNoncharacter(Rune rune) =>
        rune.Value is >= 0xFDD0 and <= 0xFDEF || (rune.Value & 0xFFFE) == 0xFFFE;

    private static string ComposeNfc(string value) => value.Normalize(NormalizationForm.FormC);

    /// <summary>The length the contract counts in: Unicode code points.</summary>
    public static int CodePointCount(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var count = 0;

        foreach (var _ in value.EnumerateRunes())
        {
            count++;
        }

        return count;
    }

    private static string EscapeLike(string value)
    {
        var builder = new StringBuilder(value.Length + 8);

        foreach (var ch in value)
        {
            if (ch is '%' or '_' or LikeEscape)
            {
                builder.Append(LikeEscape);
            }

            builder.Append(ch);
        }

        return builder.ToString();
    }
}

/// <summary>The three outcomes of <see cref="DirectorySearchText.Parse"/>.</summary>
public enum DirectorySearchTextKind
{
    /// <summary>Fewer than two characters: answers nobody, without reading anything.</summary>
    TooShort,

    /// <summary>Over one hundred characters: refused.</summary>
    TooLong,

    /// <summary>A pattern to match.</summary>
    Pattern,
}
