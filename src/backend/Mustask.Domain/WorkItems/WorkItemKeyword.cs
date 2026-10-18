using System.Globalization;
using System.Text;

namespace Mustask.Domain.WorkItems;

/// <summary>
/// The keyword of the list's «Поиск» (feat-003 BR-WS-9, api-contract § 3.3), parsed once from the <c>search</c> value of the
/// request. An item matches it when (a) its title contains <see cref="Text"/> as one piece of text, ignoring letter case, «ё»
/// equal to «е»; or (b) <see cref="Identifier"/> — the keyword is the item's identifier, in any letter case, of the addressed
/// project; or (c) <see cref="Number"/> — the keyword is the item's number as the identifier writes it. The text side is
/// judged by the database (its collation folds letter case, so Cyrillic folds as Latin does); this type decides only what is
/// searched for. A keyword is never an error: one no title can hold matches nothing.
/// </summary>
public sealed record WorkItemKeyword
{
    /// <summary>The keyword, trimmed at both ends (every Unicode white space), as typed otherwise.</summary>
    public required string Text { get; init; }

    /// <summary>
    /// The keyword is longer than a title can be, or holds a line break or U+0000: no title holds it, no identifier or number
    /// is it, and PostgreSQL text cannot carry the NUL. It selects nothing and is never sent to the database.
    /// </summary>
    public bool MatchesNothing { get; init; }

    /// <summary>(c) The keyword as an item number: digits without a leading zero that fit <see cref="int"/>; <c>null</c> otherwise.</summary>
    public int? Number { get; init; }

    /// <summary>(b) The keyword as the identifier of an item of the addressed project; <c>null</c> when it is not one.</summary>
    public KeywordIdentifier? Identifier { get; init; }

    /// <summary>
    /// The keyword of the repeated <c>search</c> values as they arrived: blank values are left out and the first of the rest
    /// counts (BR-WS-9, BR-WS-11). <c>null</c> when there is none, which sets no filter.
    /// </summary>
    /// <param name="raw">The values of the <c>search</c> parameter; <c>null</c> when absent.</param>
    /// <param name="projectKey">The addressed project's key: another project's identifier is no identifier here.</param>
    public static WorkItemKeyword? Of(IEnumerable<string?>? raw, string projectKey)
    {
        ArgumentException.ThrowIfNullOrEmpty(projectKey);

        var first = raw?.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

        if (first is null)
        {
            return null;
        }

        var text = first.Trim();

        if (text.Length > WorkItemLimits.TitleMaxLength || WorkItemText.HoldsLineBreak(text) || text.Contains('\0', StringComparison.Ordinal))
        {
            return new WorkItemKeyword { Text = text, MatchesNothing = true };
        }

        return new WorkItemKeyword { Text = text, Number = NumberOf(text), Identifier = IdentifierOf(text, projectKey) };
    }

    /// <summary>Digits in the form an identifier writes a number: no sign, no leading zero, ASCII only, within <see cref="int"/>.</summary>
    private static int? NumberOf(string text)
    {
        if (text.Length is 0 or > 10 || text[0] == '0' || !text.All(character => character is >= '0' and <= '9'))
        {
            return null;
        }

        return int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var number) ? number : null;
    }

    /// <summary>
    /// The identifier of an item of the project keyed <paramref name="projectKey"/>, in any letter case. Only ASCII text is
    /// folded to capitals, so no letter outside the Latin alphabet (a dotless «ı», a long «ſ») can stand for one inside it.
    /// </summary>
    private static KeywordIdentifier? IdentifierOf(string text, string projectKey)
    {
        if (!Ascii.IsValid(text) || !WorkItemIdentifier.TryParse(text.ToUpperInvariant(), out var identifier)
            || !string.Equals(identifier.ProjectKey, projectKey, StringComparison.Ordinal))
        {
            return null;
        }

        return new KeywordIdentifier(identifier.TypeKey, identifier.Number);
    }
}

/// <summary>The type key and the number of the identifier a keyword spells; the project is the addressed one.</summary>
public sealed record KeywordIdentifier(string TypeKey, int Number);
