namespace Mustask.Infrastructure.RichText;

/// <summary>
/// The deterministic structural bounds of the reduction (QA R-1, D-1, D-2): computed from the input string alone, so
/// the same input gets the same answer on every host. They keep the parser's superlinear and recursive paths away
/// from hostile input; a refusal is the existing markup reason. Legitimate (canonical) documents sit far inside
/// them: the panel sends allow-list markup only.
/// </summary>
internal sealed record RichTextGuardLimits
{
    public static RichTextGuardLimits Default { get; } = new();

    /// <summary>
    /// A document holding any construct whose parse depends on the tree builder (comments, declarations, raw-text,
    /// foreign-content, select, template and frameset markup) is read by a tokenizer this guard cannot follow
    /// exactly, so it is only accepted when it is this short: the parse of a short input is cheap whatever it holds.
    /// </summary>
    public int MaxLengthWithTreeBuilderMarkup { get; init; } = 8_000;

    /// <summary>Start and end tag tokens. A ceiling-sized canonical document is under 290 000 (an empty p is 7 units, 2 tags).</summary>
    public int MaxTags { get; init; } = 350_000;

    /// <summary>
    /// Attributes in one tag: the tokenizer's duplicate check is quadratic in them (5 000 take about 1 s). Pasted
    /// spreadsheet and word-processor markup carries at most about ten.
    /// </summary>
    public int MaxAttributesPerTag { get; init; } = 64;

    /// <summary>The sum, over tags, of the square of their attribute count: the duplicate check's total work.</summary>
    public long MaxAttributeSquares { get; init; } = 8_000_000;

    /// <summary>
    /// <c>pre</c> and <c>listing</c> start tags: the tree builder processes the token after one inside the call that
    /// handled it, so a run of them recurses once per tag (QA D-1). The parse also runs on a large dedicated stack.
    /// </summary>
    public int MaxPreformattedStartTags { get; init; } = 1_000;

    /// <summary>The nesting of the parsed tree (html and body included): hostile depth makes every scope check linear.</summary>
    public int MaxDomDepth { get; init; } = 160;

    /// <summary>
    /// Elements the parser creates through its element factory. A ceiling-sized canonical document is under 260 000
    /// (a br is 4 units, and at most 100 000 of them are visible text).
    /// </summary>
    public int MaxElements { get; init; } = 300_000;

    /// <summary>
    /// The tree builder's scope checks walk the open elements, so its work is the sum over elements of the nesting at
    /// their creation: this bounds that sum (read from the tree itself). A canonical document is under 3 million (15
    /// deep at the most, 200 000 elements; measured: the 1 M-unit table is 0.4 million); 150 levels with 100 000
    /// elements under them is not a document.
    /// </summary>
    public long MaxTreeWork { get; init; } = 6_000_000;
}

/// <summary>What the guard's scan saw: tags, the most attributes in one tag, their squares, preformatted start tags.</summary>
internal readonly record struct GuardStats(int Tags, int MaxAttributes, long AttributeSquares, int Preformatted, bool TreeBuilderMarkup);

/// <summary>
/// One linear pass over the (normalised) input in the tokenizer's data state, exact for the tag syntax of the
/// HTML tokenizer (tag names, attribute names, the three value forms, self-closing, bogus end tags), that decides
/// whether the structural bounds hold. See <see cref="RichTextGuardLimits"/>.
/// </summary>
internal static class RichTextInputGuard
{
    // The names whose handling depends on the tree builder (it switches the tokenizer, changes the content model or
    // ignores later tags), as a prefix: any spelling that starts with one is treated as such. `<!` and `<?` too.
    private static readonly string[] TreeBuilderNames =
    [
        "script", "style", "title", "textarea", "xmp", "iframe", "noembed", "noframes", "noscript", "plaintext", "svg", "math",
        "select", "option", "optgroup", "frameset", "frame", "template",
    ];

    public static bool IsWithinBounds(string text, RichTextGuardLimits limits) => Scan(text, limits, out _);

    /// <summary>
    /// The scan: <c>true</c> when every bound holds. <paramref name="stats"/> is what was seen up to the end or to the
    /// first bound passed (all of it when the limits are generous, which is how the tests compare it with the parser).
    /// </summary>
    internal static bool Scan(string text, RichTextGuardLimits limits, out GuardStats stats)
    {
        var length = text.Length;
        var index = 0;
        var tags = 0;
        var preformatted = 0;
        var maxAttributes = 0;
        long squares = 0;
        var treeBuilderMarkup = false;

        GuardStats Snapshot() => new(tags, maxAttributes, squares, preformatted, treeBuilderMarkup);

        while (index < length)
        {
            var open = text.IndexOf('<', index);

            if (open < 0 || open + 1 >= length)
            {
                break;
            }

            var next = text[open + 1];

            if (next is '!' or '?')
            {
                // A comment, a declaration, CDATA or a processing instruction.
                if (length > limits.MaxLengthWithTreeBuilderMarkup)
                {
                    stats = Snapshot();

                    return false;
                }

                treeBuilderMarkup = true;
                index = open + 2;

                continue;
            }

            var isEnd = next == '/';
            var nameStart = open + (isEnd ? 2 : 1);

            if (nameStart >= length)
            {
                break;
            }

            if (!IsAsciiAlpha(text[nameStart]))
            {
                if (isEnd && text[nameStart] != '>')
                {
                    // A bogus comment: everything up to the next '>' (no quoting).
                    var close = text.IndexOf('>', nameStart);
                    index = close < 0 ? length : close + 1;
                }
                else
                {
                    index = isEnd ? nameStart + 1 : nameStart;
                }

                continue;
            }

            var nameEnd = nameStart;

            while (nameEnd < length && !IsTagNameEnd(text[nameEnd]))
            {
                nameEnd++;
            }

            var name = text.AsSpan(nameStart, nameEnd - nameStart);

            if (++tags > limits.MaxTags)
            {
                stats = Snapshot();

                return false;
            }

            if (IsTreeBuilderName(name))
            {
                treeBuilderMarkup = true;

                if (length > limits.MaxLengthWithTreeBuilderMarkup)
                {
                    stats = Snapshot();

                    return false;
                }
            }

            if (!isEnd && (name.Equals("pre", StringComparison.OrdinalIgnoreCase) || name.Equals("listing", StringComparison.OrdinalIgnoreCase))
                && ++preformatted > limits.MaxPreformattedStartTags)
            {
                stats = Snapshot();

                return false;
            }

            index = SkipAttributes(text, nameEnd, out var attributes);
            maxAttributes = Math.Max(maxAttributes, attributes);
            squares += (long)attributes * attributes;

            if (attributes > limits.MaxAttributesPerTag || squares > limits.MaxAttributeSquares)
            {
                stats = Snapshot();

                return false;
            }
        }

        stats = Snapshot();

        return !treeBuilderMarkup || length <= limits.MaxLengthWithTreeBuilderMarkup;
    }

    /// <summary>
    /// The tokenizer's states from "before attribute name" to the end of the tag: returns the index after the closing
    /// <c>&gt;</c> (or the end of the input) and how many attributes (duplicates included) the tag held.
    /// </summary>
    private static int SkipAttributes(string text, int index, out int attributes)
    {
        var length = text.Length;
        attributes = 0;

        while (true)
        {
            while (index < length && IsAsciiWhitespace(text[index]))
            {
                index++;
            }

            if (index >= length)
            {
                return length;
            }

            var c = text[index];

            if (c == '>')
            {
                return index + 1;
            }

            if (c == '/')
            {
                index++;

                continue;
            }

            // An attribute name: its first character is taken whatever it is ('=' included).
            attributes++;
            index++;

            while (index < length && text[index] is not (' ' or '\t' or '\n' or '\f' or '\r' or '/' or '>' or '='))
            {
                index++;
            }

            while (index < length && IsAsciiWhitespace(text[index]))
            {
                index++;
            }

            if (index >= length)
            {
                return length;
            }

            if (text[index] != '=')
            {
                continue;
            }

            index++;

            while (index < length && IsAsciiWhitespace(text[index]))
            {
                index++;
            }

            if (index >= length)
            {
                return length;
            }

            var quote = text[index];

            if (quote is '"' or '\'')
            {
                var close = text.IndexOf(quote, index + 1);

                if (close < 0)
                {
                    return length;
                }

                index = close + 1;
            }
            else if (quote == '>')
            {
                return index + 1;
            }
            else
            {
                while (index < length && !IsAsciiWhitespace(text[index]) && text[index] != '>')
                {
                    index++;
                }
            }
        }
    }

    private static bool IsTreeBuilderName(ReadOnlySpan<char> name)
    {
        foreach (var prefix in TreeBuilderNames)
        {
            if (name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsAsciiAlpha(char c) => c is >= 'a' and <= 'z' or >= 'A' and <= 'Z';

    private static bool IsAsciiWhitespace(char c) => c is ' ' or '\t' or '\n' or '\f' or '\r';

    private static bool IsTagNameEnd(char c) => IsAsciiWhitespace(c) || c is '/' or '>';
}
