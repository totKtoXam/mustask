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
    /// The nesting of the parsed tree (html and body included): hostile depth makes every scope check linear, and it
    /// is what caps the tree builder's recursion on a run of <c>pre</c>, <c>listing</c> or <c>template</c> start tags
    /// (each of a run is the child of the one before; QA D-1, D-9). The parse also runs on a 32 MB dedicated stack.
    /// </summary>
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

    /// <summary>
    /// Foster parenting: text and inline content that meets a table is put before it, and the DOM finds the table's
    /// index, linear in the parent's children, for each node inserted and each character appended to the text
    /// already there (QA D-8). Each is charged at the parent's child count, read from the tree (child counts, last
    /// child and the text before it: never sibling links, which are linear searches). Canonical output never needs
    /// it; about 4 000 such insertions into one parent reach the bound, which costs under 0.1 s.
    /// </summary>
    public long MaxInsertionWork { get; init; } = 16_000_000;

    /// <summary>
    /// QA D-10. Text that meets a token the tree builder ignores (a <c>td</c>, <c>tr</c>, <c>tbody</c>, <c>html</c> or
    /// <c>body</c> start tag outside a table, an end tag with nothing to close) is appended to the text node already
    /// there, and the DOM copies that node's whole text for each piece: the copying is the sum, over text pieces, of the
    /// length of the text run they join (a run ends at a start tag that makes an element). Counted in characters
    /// copied; 200 million take well under 0.2 s. Canonical output has runs of one piece, or a few for nested marks:
    /// its sum is under the size of its text (a million at the most).
    /// </summary>
    public long MaxTextAppendWork { get; init; } = 200_000_000;

    /// <summary>
    /// QA D-10. Foster parenting seen in the markup itself: text or a tag that is not a table tag, met while a table
    /// (not one of its cells) is the open context, lands before the table and the DOM finds the table's index, linear
    /// in the parent's children, for each. The sum, over such characters and tags, of the tokens read so far (an upper
    /// bound of any parent's children). The tree watcher counts the same from the tree, but loses sight of a table once
    /// text follows it. Canonical output has none.
    /// </summary>
    public long MaxFosterTokenWork { get; init; } = 32_000_000;
}

/// <summary>What the guard's scan saw: tags, the most attributes in one tag, their squares, preformatted start tags.</summary>
internal readonly record struct GuardStats(
    int Tags, int MaxAttributes, long AttributeSquares, int Preformatted, bool TreeBuilderMarkup, long TextAppendWork = 0, long FosterTokenWork = 0);

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
        var pending = 0;
        var cost = new MarkupCost();

        GuardStats Snapshot() => new(tags, maxAttributes, squares, preformatted, treeBuilderMarkup, cost.TextAppend, cost.Foster);

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
                cost.Text(text, pending, open);
                cost.Boundary();
                pending = open + 2;
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
                    cost.Text(text, pending, open);
                    cost.Boundary();
                    pending = index;
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

            // Counted for the record only: the tree builder recurses once per pre/listing start tag in a run, and
            // each of a run is the child of the one before, so the nesting bound of the tree watcher (160) is what
            // caps that recursion, whatever the number of closed blocks (QA D-9).
            if (!isEnd && (name.Equals("pre", StringComparison.OrdinalIgnoreCase) || name.Equals("listing", StringComparison.OrdinalIgnoreCase)))
            {
                preformatted++;
            }

            index = SkipAttributes(text, nameEnd, out var attributes);
            maxAttributes = Math.Max(maxAttributes, attributes);
            squares += (long)attributes * attributes;

            if (attributes > limits.MaxAttributesPerTag || squares > limits.MaxAttributeSquares)
            {
                stats = Snapshot();

                return false;
            }

            cost.Text(text, pending, open);
            cost.Tag(name, isEnd);
            pending = index;

            if (cost.TextAppend > limits.MaxTextAppendWork || cost.Foster > limits.MaxFosterTokenWork)
            {
                stats = Snapshot();

                return false;
            }
        }

        cost.Text(text, pending, length);
        stats = Snapshot();

        if (cost.TextAppend > limits.MaxTextAppendWork || cost.Foster > limits.MaxFosterTokenWork)
        {
            return false;
        }

        return !treeBuilderMarkup || length <= limits.MaxLengthWithTreeBuilderMarkup;
    }

    /// <summary>
    /// The two costs the tree builder pays that the element hook cannot see (QA D-10), counted from the tokens alone.
    /// <b>Text append</b>: pieces of text with no element made between them join one text node, and each join copies
    /// the node; so the sum, over pieces, of the length of the run they join. A run ends at a start tag that makes an
    /// element; every end tag, and every start tag the tree builder may ignore, continues it (an overcount that
    /// canonical output, which is p/li/cell blocks with a few marks, never reaches). <b>Foster parenting</b>: a small
    /// stack of the table contexts (a table, or one of its cells and captions) follows the table tags the way the
    /// tree builder does; text with something in it and any other tag met while a table is the open context lands
    /// before the table, at a cost linear in the parent's children, bounded here by the tokens read so far.
    /// </summary>
    private sealed class MarkupCost
    {
        private bool[] cells = new bool[16];
        private int depth;
        private long run;
        private long tokens;

        public long TextAppend { get; private set; }

        public long Foster { get; private set; }

        /// <summary>A node that is not text (a comment) between two pieces: the run ends.</summary>
        public void Boundary() => run = 0;

        private bool InTable => depth > 0 && !cells[depth - 1];

        public void Text(string text, int start, int end)
        {
            if (end <= start)
            {
                return;
            }

            tokens++;
            TextAppend += run;
            run += end - start;

            if (InTable)
            {
                var content = 0;

                for (var index = start; index < end; index++)
                {
                    if (!IsAsciiWhitespace(text[index]))
                    {
                        content++;
                    }
                }

                Foster += content * tokens;
            }
        }

        public void Tag(ReadOnlySpan<char> name, bool isEnd)
        {
            tokens++;
            var kind = Classify(name);

            if (isEnd)
            {
                switch (kind)
                {
                    case TagKind.Table:
                        PopThroughTable();
                        break;
                    case TagKind.Cell or TagKind.Section or TagKind.Row:
                        if (depth > 0 && cells[depth - 1])
                        {
                            depth--;
                        }

                        break;
                    case TagKind.Column or TagKind.Ignored:
                        break;
                    default:
                        if (InTable)
                        {
                            Foster += tokens;
                        }

                        break;
                }

                return;
            }

            switch (kind)
            {
                case TagKind.Table:
                    if (InTable)
                    {
                        depth--;
                    }

                    Push(false);
                    run = 0;
                    break;
                case TagKind.Section or TagKind.Row:
                    if (depth > 0)
                    {
                        if (cells[depth - 1])
                        {
                            depth--;
                        }

                        run = 0;
                    }

                    break;
                case TagKind.Cell:
                    if (depth > 0)
                    {
                        if (cells[depth - 1])
                        {
                            depth--;
                        }

                        Push(true);
                        run = 0;
                    }

                    break;
                case TagKind.Column:
                    if (InTable)
                    {
                        run = 0;
                    }

                    break;
                case TagKind.Ignored:
                    break;
                default:
                    if (InTable)
                    {
                        Foster += tokens;
                    }

                    run = 0;
                    break;
            }
        }

        private void Push(bool isCell)
        {
            if (depth == cells.Length)
            {
                Array.Resize(ref cells, depth * 2);
            }

            cells[depth++] = isCell;
        }

        private void PopThroughTable()
        {
            for (var index = depth - 1; index >= 0; index--)
            {
                if (!cells[index])
                {
                    depth = index;

                    return;
                }
            }
        }
    }

    private enum TagKind
    {
        Other,
        Table,
        Section,
        Row,
        Cell,
        Column,
        Ignored,
    }

    /// <summary>
    /// The table tags, and the tags that the tree builder may ignore wherever they are met (a second form, html, body,
    /// head and the frame tags): the rest make an element.
    /// </summary>
    private static TagKind Classify(ReadOnlySpan<char> name)
    {
        if (name.Length > 8)
        {
            return TagKind.Other;
        }

        Span<char> lower = stackalloc char[8];
        var length = name.ToLowerInvariant(lower);

        return lower[..length] switch
        {
            "table" => TagKind.Table,
            "tbody" or "thead" or "tfoot" => TagKind.Section,
            "tr" => TagKind.Row,
            "td" or "th" or "caption" => TagKind.Cell,
            "col" or "colgroup" => TagKind.Column,
            "html" or "body" or "head" or "form" or "frame" or "frameset" or "isindex" => TagKind.Ignored,
            _ => TagKind.Other,
        };
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
