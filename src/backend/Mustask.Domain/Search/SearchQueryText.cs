using System.Text;

namespace Mustask.Domain.Search;

/// <summary>The outcomes of <see cref="SearchQueryText.Judge"/>.</summary>
public enum SearchQueryKind
{
    /// <summary>A usable query.</summary>
    Valid,

    /// <summary>Fewer than <see cref="SearchQueryText.MinLength"/> characters (also empty or missing).</summary>
    TooShort,

    /// <summary>More than <see cref="SearchQueryText.MaxLength"/> characters.</summary>
    TooLong,
}

/// <summary>
/// The typed text of the quick search (BR-GS-3, BR-GS-10): trimmed, 2 to 100 Unicode code points, and folded the way the stored side
/// is folded — NFC, lower case, <c>ё</c> → <c>е</c>, control characters dropped, a noncharacter read as U+FFFD (the user projection's
/// own normalisation) — with the escapes the match forms need, so every character is literal («%», «_», «\» find only themselves).
/// Never logged: only <see cref="Length"/> is.
/// </summary>
public sealed class SearchQueryText
{
    public const int MinLength = 2;

    public const int MaxLength = 100;

    /// <summary>The escape character of the <c>LIKE</c> patterns.</summary>
    public const char LikeEscape = '\\';

    private SearchQueryText(string folded)
    {
        Folded = folded;
        Length = CodePointCount(folded);
    }

    /// <summary>The folded text; what every tier compares with.</summary>
    public string Folded { get; }

    /// <summary>The length of <see cref="Folded"/> in code points (what the similarity rule counts).</summary>
    public int Length { get; }

    /// <summary><c>escaped%</c>: the prefix pattern.</summary>
    public string PrefixPattern => EscapeLike(Folded) + "%";

    /// <summary><c>%escaped%</c>: the contains pattern.</summary>
    public string ContainsPattern => "%" + EscapeLike(Folded) + "%";

    /// <summary>The regular expression of "a word starts with the query": the start of the text or a non-alphanumeric, then the text.</summary>
    public string WordPrefixPattern => "(^|[^[:alnum:]])" + EscapeRegex(Folded);

    /// <summary>Whether the typo tier applies to this query (BR-GS-17).</summary>
    public bool AllowsSimilar => SimilarityRule.Applies(Length);

    /// <summary>Judges the typed text; <paramref name="text"/> is <c>null</c> when the body had none.</summary>
    public static SearchQueryKind Judge(string? text, out SearchQueryText? query)
    {
        query = null;
        var trimmed = (text ?? string.Empty).Trim();
        var length = CodePointCount(trimmed);

        if (length > MaxLength)
        {
            return SearchQueryKind.TooLong;
        }

        var folded = Fold(trimmed);

        if (length < MinLength || CodePointCount(folded) < MinLength)
        {
            return SearchQueryKind.TooShort;
        }

        query = new SearchQueryText(folded);

        return SearchQueryKind.Valid;
    }

    /// <summary>The fold of both sides: NFC, lower case, <c>ё</c> → <c>е</c>; total, no text makes it throw.</summary>
    public static string Fold(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var builder = new StringBuilder(value.Length);

        foreach (var rune in value.EnumerateRunes())
        {
            if (rune.Value is >= 0xFDD0 and <= 0xFDEF || (rune.Value & 0xFFFE) == 0xFFFE)
            {
                builder.Append((char)0xFFFD);
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
            composed = assembled.Normalize(NormalizationForm.FormC);
        }
        catch (ArgumentException)
        {
            composed = assembled;
        }

        return composed.ToLowerInvariant().Replace('ё', 'е');
    }

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

    private static string EscapeRegex(string value)
    {
        var builder = new StringBuilder(value.Length + 8);

        foreach (var ch in value)
        {
            if ("\\^$.|?*+()[]{}".Contains(ch, StringComparison.Ordinal))
            {
                builder.Append('\\');
            }

            builder.Append(ch);
        }

        return builder.ToString();
    }
}
