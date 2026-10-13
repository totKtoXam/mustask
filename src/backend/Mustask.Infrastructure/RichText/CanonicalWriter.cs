using System.Text;

namespace Mustask.Infrastructure.RichText;

/// <summary>
/// Writes the canonical model in the WHATWG fragment serialisation form (api-contract § 3): attributes <c>href</c>
/// then <c>rel</c> on <c>a</c>, <c>class</c> alone elsewhere, double quotes, <c>&amp;</c> <c>&lt;</c> <c>&gt;</c>
/// escaped in text and <c>&amp;</c> <c>"</c> in attributes, U+00A0 as <c>&amp;nbsp;</c>, void <c>br</c>. It counts
/// code points as it appends and stops the moment the count passes the bound, and accumulates the visible text
/// length (BR-RT-8) and "has content" (BR-RT-6) in the same pass.
/// </summary>
internal sealed class CanonicalWriter
{
    private const string Rel = "noopener noreferrer";

    private readonly RichTextVocabulary vocabulary;
    private readonly long limit;
    private readonly StringBuilder builder = new();
    private readonly List<OpenMark> open = [];
    private long markup;
    private long visible;
    private bool leafSeen;

    public CanonicalWriter(RichTextVocabulary vocabulary, long limit, long alreadyCounted)
    {
        this.vocabulary = vocabulary;
        this.limit = limit;
        markup = alreadyCounted;
    }

    public long VisibleLength => visible;

    public bool HasContent { get; private set; }

    /// <summary>The written html, and its length in code points (without what was only read and dropped).</summary>
    public string Html => builder.ToString();

    public long MarkupLength { get; private set; }

    public void Write(List<Block> blocks)
    {
        foreach (var block in blocks)
        {
            WriteBlock(block);
        }
    }

    private void WriteBlock(Block block)
    {
        switch (block)
        {
            case Leaf leaf:
                WriteLeaf(leaf);
                break;
            case CodeBlock code:
                WriteCode(code);
                break;
            case ListBlock list:
                WriteList(list);
                break;
            case RectangularTable table:
                WriteTable(table);
                break;
        }
    }

    private void Separate()
    {
        if (leafSeen)
        {
            visible++;
        }

        leafSeen = true;
    }

    private void WriteLeaf(Leaf leaf)
    {
        Separate();

        var tag = leaf.Kind switch
        {
            LeafKind.Heading3 => "h3",
            LeafKind.Heading4 => "h4",
            _ => "p",
        };

        Markup("<" + tag + ">");

        foreach (var item in leaf.Items)
        {
            if (item.IsBreak)
            {
                Markup("<br>");
                visible++;
            }
            else
            {
                SetMarks(item.Marks);
                Text(item.Text!);
            }
        }

        CloseAll();
        Markup("</" + tag + ">");
    }

    private void WriteCode(CodeBlock code)
    {
        Separate();

        var cls = code.Language >= 0 ? vocabulary.CodeLanguage.Tokens[code.Language].Class : null;
        Markup(cls is null ? "<pre><code>" : "<pre><code class=\"" + cls + "\">");

        // <pre><code> has the code element between the pre and its text, so the parser's "drop a line feed right after
        // <pre>" does not apply and the text is written as it is.
        Text(code.Text);
        Markup("</code></pre>");
    }

    private void WriteList(ListBlock list)
    {
        if (list.Items.Count == 0)
        {
            return;
        }

        var tag = list.Ordered ? "ol" : "ul";
        Markup("<" + tag + ">");

        foreach (var item in list.Items)
        {
            Markup("<li>");

            // An item holds paragraphs and nested lists; one with nothing to write (a nested list with no item) is
            // an empty paragraph, never an empty li (that would not survive a re-parse unchanged).
            if (!item.Blocks.Any(block => block is not ListBlock { Items.Count: 0 }))
            {
                WriteLeaf(new Leaf(LeafKind.Paragraph));
            }

            foreach (var block in item.Blocks)
            {
                WriteBlock(block);
            }

            Markup("</li>");
        }

        Markup("</" + tag + ">");
    }

    private void WriteTable(RectangularTable table)
    {
        Markup("<table><tbody>");

        foreach (var row in table.Rows)
        {
            Markup("<tr>");

            foreach (var cell in row)
            {
                var tag = cell.Header ? "th" : "td";
                Markup("<" + tag + ">");

                if (cell.Paragraphs.Count == 0)
                {
                    WriteLeaf(new Leaf(LeafKind.Paragraph));
                }

                foreach (var paragraph in cell.Paragraphs)
                {
                    WriteLeaf(paragraph);
                }

                Markup("</" + tag + ">");
            }

            Markup("</tr>");
        }

        Markup("</tbody></table>");
    }

    // --- marks ---------------------------------------------------------------------------------------------------

    /// <summary>
    /// Brings the open elements to <paramref name="marks"/>: the common prefix of the canonical order stays open (so
    /// adjacent equal marks merge), the rest is closed and the new ones opened outermost first (BR-RT-15).
    /// </summary>
    private void SetMarks(Marks marks)
    {
        var wanted = new List<OpenMark>(9);

        foreach (var kind in vocabulary.MarkOrder)
        {
            switch (kind)
            {
                case MarkKind.Link when marks.Href is not null:
                    wanted.Add(new OpenMark(kind, 0, marks.Href));
                    break;
                case MarkKind.Bold when marks.Bold:
                case MarkKind.Italic when marks.Italic:
                case MarkKind.Underline when marks.Underline:
                case MarkKind.Strike when marks.Strike:
                    wanted.Add(new OpenMark(kind, 0, null));
                    break;
                case MarkKind.Highlight when marks.Highlight >= 0:
                    wanted.Add(new OpenMark(kind, marks.Highlight, null));
                    break;
                case MarkKind.TextColor when marks.Color >= 0:
                    wanted.Add(new OpenMark(kind, marks.Color, null));
                    break;
                case MarkKind.FontSize when marks.Size >= 0:
                    wanted.Add(new OpenMark(kind, marks.Size, null));
                    break;
            }
        }

        if (marks.Code)
        {
            wanted.Add(OpenMark.Code);
        }

        var common = 0;

        while (common < open.Count && common < wanted.Count && open[common] == wanted[common])
        {
            common++;
        }

        for (var index = open.Count - 1; index >= common; index--)
        {
            Markup(Close(open[index]));
        }

        open.RemoveRange(common, open.Count - common);

        for (var index = common; index < wanted.Count; index++)
        {
            Open(wanted[index]);
            open.Add(wanted[index]);
        }
    }

    private void CloseAll()
    {
        for (var index = open.Count - 1; index >= 0; index--)
        {
            Markup(Close(open[index]));
        }

        open.Clear();
    }

    private void Open(OpenMark mark)
    {
        switch (mark.Kind)
        {
            case MarkKind.Link when !mark.IsCode:
                Markup("<a href=\"");
                Attribute(mark.Href!);
                Markup("\" rel=\"" + Rel + "\">");
                break;
            case MarkKind.Bold when !mark.IsCode:
                Markup("<strong>");
                break;
            case MarkKind.Italic when !mark.IsCode:
                Markup("<em>");
                break;
            case MarkKind.Underline when !mark.IsCode:
                Markup("<u>");
                break;
            case MarkKind.Strike when !mark.IsCode:
                Markup("<s>");
                break;
            case MarkKind.Highlight when !mark.IsCode:
                Markup("<mark class=\"" + vocabulary.Highlight.Tokens[mark.Token].Class + "\">");
                break;
            case MarkKind.TextColor when !mark.IsCode:
                Markup("<span class=\"" + vocabulary.TextColor.Tokens[mark.Token].Class + "\">");
                break;
            case MarkKind.FontSize when !mark.IsCode:
                Markup("<span class=\"" + vocabulary.FontSize.Tokens[mark.Token].Class + "\">");
                break;
            default:
                Markup("<code>");
                break;
        }
    }

    private static string Close(OpenMark mark) =>
        mark.IsCode
            ? "</code>"
            : mark.Kind switch
            {
                MarkKind.Link => "</a>",
                MarkKind.Bold => "</strong>",
                MarkKind.Italic => "</em>",
                MarkKind.Underline => "</u>",
                MarkKind.Strike => "</s>",
                MarkKind.Highlight => "</mark>",
                _ => "</span>",
            };

    // --- output --------------------------------------------------------------------------------------------------

    /// <summary>Fixed ASCII-free-of-escaping markup.</summary>
    private void Markup(string text)
    {
        builder.Append(text);
        Count(text.Length);
    }

    private void Attribute(string value)
    {
        foreach (var rune in value.EnumerateRunes())
        {
            switch (rune.Value)
            {
                case 0:
                    break;
                case '&':
                    builder.Append("&amp;");
                    Count(5);
                    break;
                case '"':
                    builder.Append("&quot;");
                    Count(6);
                    break;
                case 0xA0:
                    builder.Append("&nbsp;");
                    Count(6);
                    break;
                default:
                    Append(rune);
                    Count(1);
                    break;
            }
        }
    }

    /// <summary>Text: escaped, counted for the markup, the visible length and "has content".</summary>
    private void Text(string text)
    {
        foreach (var rune in text.EnumerateRunes())
        {
            switch (rune.Value)
            {
                case 0:
                    continue;
                case '&':
                    builder.Append("&amp;");
                    Count(5);
                    break;
                case '<':
                    builder.Append("&lt;");
                    Count(4);
                    break;
                case '>':
                    builder.Append("&gt;");
                    Count(4);
                    break;
                case 0xA0:
                    builder.Append("&nbsp;");
                    Count(6);
                    break;
                default:
                    Append(rune);
                    Count(1);
                    break;
            }

            visible++;

            if (!HasContent && !Rune.IsWhiteSpace(rune))
            {
                HasContent = true;
            }
        }
    }

    private void Append(Rune rune)
    {
        if (rune.IsBmp)
        {
            builder.Append((char)rune.Value);
        }
        else
        {
            builder.Append(rune.ToString());
        }
    }

    private void Count(int codePoints)
    {
        markup += codePoints;
        MarkupLength += codePoints;

        if (markup > limit)
        {
            throw new RichTextBudgetExceededException();
        }
    }

    private readonly record struct OpenMark(MarkKind Kind, int Token, string? Href, bool IsCode = false)
    {
        public static OpenMark Code { get; } = new(MarkKind.Link, 0, null, IsCode: true);
    }
}
