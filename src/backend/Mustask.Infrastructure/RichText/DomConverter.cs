using System.Text;
using AngleSharp.Dom;

namespace Mustask.Infrastructure.RichText;

/// <summary>
/// The allow-list stage and the canonical pass in one iterative walk of the parsed document (plan-backend § 5.1,
/// US-2, US-7): elements are dropped with their content, unwrapped, or mapped; attributes are read and never kept;
/// the result is an in-memory canonical model whose depth is bounded by construction (lists three deep, marks in a
/// fixed order). Nothing here recurses on the depth of the DOM.
/// </summary>
internal sealed class DomConverter
{
    private const string HtmlNamespace = "http://www.w3.org/1999/xhtml";
    private const int MaxListDepth = 3;

    private static readonly HashSet<string> Dropped = new(StringComparer.Ordinal)
    {
        "script", "style", "iframe", "object", "embed", "template", "svg", "math", "noscript", "head", "title", "link",
        "meta", "base", "form", "input", "textarea", "select", "button", "audio", "video", "canvas", "frame", "frameset",
        "applet", "img", "hr",
    };

    private readonly RichTextVocabulary vocabulary;
    private readonly long maxPositions;
    private readonly long droppedLimit;
    private readonly CancellationToken cancellationToken;

    private readonly List<Container> containers = [];
    private Leaf? currentLeaf;
    private Marks marks = Marks.None;
    private bool inPre;
    private readonly StringBuilder preText = new();
    private int preLanguage = -1;
    private int listDepth;
    private long droppedWhitespace;
    private int visited;

    public DomConverter(RichTextVocabulary vocabulary, long maxPositions, long droppedLimit, CancellationToken cancellationToken)
    {
        this.vocabulary = vocabulary;
        this.maxPositions = maxPositions;
        this.droppedLimit = droppedLimit;
        this.cancellationToken = cancellationToken;
    }

    /// <summary>Whitespace read and dropped between blocks, in code points: it counts against the budget (US-4).</summary>
    public long DroppedWhitespace => droppedWhitespace;

    public List<Block> Convert(IElement body)
    {
        var root = new RootContainer();
        containers.Add(root);

        var stack = new Stack<WalkFrame>();
        stack.Push(new WalkFrame(body));

        while (stack.Count > 0)
        {
            var frame = stack.Peek();

            if ((++visited & 0x1FFF) == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            var children = frame.Element.ChildNodes;

            if (frame.Next >= children.Length)
            {
                Exit(frame);
                stack.Pop();
                continue;
            }

            var child = children[frame.Next++];

            if (child is IText text)
            {
                OnText(text.Data);
            }
            else if (child is IElement element)
            {
                var next = new WalkFrame(element);

                if (Enter(next))
                {
                    stack.Push(next);
                }
                else
                {
                    Exit(next);
                }
            }
        }

        return root.Blocks;
    }

    // --- elements ------------------------------------------------------------------------------------------------

    /// <summary>Applies the element's meaning; <c>true</c> when its children are to be walked.</summary>
    private bool Enter(WalkFrame frame)
    {
        var element = frame.Element;
        var name = element.LocalName;
        frame.SavedMarks = marks;

        if (element.NamespaceUri != HtmlNamespace || Dropped.Contains(name))
        {
            frame.Skipped = true;
            return false;
        }

        if (name == "br")
        {
            OnBreak();
            frame.Skipped = true;
            return false;
        }

        if (inPre)
        {
            return true;
        }

        switch (name)
        {
            case "p":
                StartLeaf(LeafKind.Paragraph);
                frame.CloseLeaf = true;
                break;
            case "h1":
            case "h2":
            case "h3":
                StartHeading(LeafKind.Heading3);
                frame.CloseLeaf = true;
                break;
            case "h4":
            case "h5":
            case "h6":
                StartHeading(LeafKind.Heading4);
                frame.CloseLeaf = true;
                break;
            case "pre":
                CloseLeaf();
                inPre = true;
                preText.Clear();
                preLanguage = LanguageOf(element);
                frame.EndPre = true;
                break;
            case "ul":
            case "ol":
                StartList(name == "ol", frame);
                break;
            case "li":
                StartItem(frame);
                break;
            case "table":
                StartTable(frame);
                break;
            case "thead":
            case "tbody":
            case "tfoot":
                CloseLeaf();

                if (Current is TableContainer groupTable)
                {
                    groupTable.Table.CurrentGroup++;
                }

                break;
            case "tr":
                CloseLeaf();

                if (Current is TableContainer rowTable)
                {
                    var row = new RowBlock(rowTable.Table.CurrentGroup);
                    rowTable.Table.Rows.Add(row);
                    Push(new RowContainer(rowTable, row));
                    frame.Pop = true;
                }

                break;
            case "td":
            case "th":
                CloseLeaf();

                if (Current is RowContainer rowContainer)
                {
                    var cell = new CellBlock(name == "th", SpanOf(element, "colspan"), SpanOf(element, "rowspan"));
                    rowContainer.Row.Cells.Add(cell);
                    Push(new CellContainer(cell));
                    frame.Pop = true;
                }

                break;
            case "strong":
            case "b":
                marks = marks with { Bold = true };
                break;
            case "em":
            case "i":
                marks = marks with { Italic = true };
                break;
            case "u":
                marks = marks with { Underline = true };
                break;
            case "s":
            case "strike":
            case "del":
                marks = marks with { Strike = true };
                break;
            case "code":
                marks = marks with { Code = true };
                break;
            case "a":
                if (LinkAddress.TryRead(element.GetAttribute("href"), out var href))
                {
                    marks = marks with { Href = href };
                }

                break;
            case "mark":
                if (vocabulary.Highlight.FirstMatch(element.GetAttribute("class")) is >= 0 and var highlight)
                {
                    marks = marks with { Highlight = highlight };
                }

                break;
            case "span":
                var color = vocabulary.TextColor.FirstMatch(element.GetAttribute("class"));
                var size = vocabulary.FontSize.FirstMatch(element.GetAttribute("class"));
                marks = marks with
                {
                    Color = color >= 0 ? color : marks.Color,
                    Size = size >= 0 ? size : marks.Size,
                };
                break;
            default:
                // Unknown elements are unwrapped: transparent, their text kept (US-2).
                break;
        }

        return true;
    }

    private void Exit(WalkFrame frame)
    {
        if (frame.Skipped)
        {
            return;
        }

        if (frame.EndPre)
        {
            EndPre();
        }

        if (frame.Pop)
        {
            CloseLeaf();
            Pop();
        }
        else if (frame.CloseLeaf)
        {
            CloseLeaf();
        }

        marks = frame.SavedMarks;
    }

    private int LanguageOf(IElement pre)
    {
        foreach (var child in pre.Children)
        {
            if (child.LocalName == "code" && child.NamespaceUri == HtmlNamespace)
            {
                return vocabulary.CodeLanguage.FirstMatch(child.GetAttribute("class"));
            }
        }

        return -1;
    }

    private static int SpanOf(IElement element, string attribute)
    {
        var value = element.GetAttribute(attribute)?.Trim();

        return value is { Length: > 0 and <= 9 } && int.TryParse(value, System.Globalization.NumberStyles.None, null, out var span) ? span : 1;
    }

    // --- text ----------------------------------------------------------------------------------------------------

    private void OnText(string data)
    {
        if (data.Length == 0)
        {
            return;
        }

        if (inPre)
        {
            preText.Append(data);

            return;
        }

        if (currentLeaf is null)
        {
            if (IsAsciiWhitespace(data))
            {
                // Whitespace between blocks is dropped, but it was read: it counts against the budget.
                droppedWhitespace += CodePoints(data);

                if (droppedWhitespace > droppedLimit)
                {
                    throw new RichTextBudgetExceededException();
                }

                return;
            }

            StartLeaf(LeafKind.Paragraph);
        }

        currentLeaf!.Items.Add(new Inline(data, marks.Canonical(currentLeaf.Kind != LeafKind.Paragraph)));
    }

    private void OnBreak()
    {
        if (inPre)
        {
            preText.Append('\n');

            return;
        }

        if (currentLeaf is null)
        {
            StartLeaf(LeafKind.Paragraph);
        }

        currentLeaf!.Items.Add(Inline.Break);
    }

    private static bool IsAsciiWhitespace(string text)
    {
        foreach (var c in text)
        {
            if (c is not (' ' or '\t' or '\n' or '\f' or '\r'))
            {
                return false;
            }
        }

        return true;
    }

    private static int CodePoints(string text)
    {
        var count = 0;

        foreach (var _ in text.EnumerateRunes())
        {
            count++;
        }

        return count;
    }

    // --- structure -----------------------------------------------------------------------------------------------

    private Container Current => containers[^1];

    private void Push(Container container) => containers.Add(container);

    private void Pop()
    {
        var container = containers[^1];
        containers.RemoveAt(containers.Count - 1);

        switch (container)
        {
            case ListContainer:
                listDepth--;
                break;
            case TableContainer table:
                foreach (var before in table.Before)
                {
                    Current.Add(before);
                }

                if (TableNormaliser.Normalise(table.Table, maxPositions) is { } rectangular)
                {
                    Current.Add(rectangular);
                }

                break;
        }
    }

    private void CloseLeaf() => currentLeaf = null;

    private void StartLeaf(LeafKind kind)
    {
        CloseLeaf();

        // A heading is a top-level block only; in an item or a cell it is a paragraph (US-7).
        var effective = kind != LeafKind.Paragraph && Current is not RootContainer ? LeafKind.Paragraph : kind;
        var leaf = new Leaf(effective);
        Current.Add(leaf);
        currentLeaf = leaf;
    }

    private void StartHeading(LeafKind kind) => StartLeaf(kind);

    private void StartList(bool ordered, WalkFrame frame)
    {
        CloseLeaf();
        frame.CloseLeaf = true;

        if (Current is RootContainer or ItemContainer or ListContainer && listDepth < MaxListDepth)
        {
            var list = new ListBlock(ordered);
            Current.Add(list);
            Push(new ListContainer(list));
            listDepth++;
            frame.CloseLeaf = false;
            frame.Pop = true;
        }
    }

    private void StartItem(WalkFrame frame)
    {
        CloseLeaf();
        frame.CloseLeaf = true;

        if (Current is ListContainer list)
        {
            var item = new ItemBlock();
            list.List.Items.Add(item);
            list.ResetImplicit();
            Push(new ItemContainer(item));
            frame.CloseLeaf = false;
            frame.Pop = true;
        }
    }

    private void StartTable(WalkFrame frame)
    {
        CloseLeaf();
        frame.CloseLeaf = true;

        if (Current is RootContainer)
        {
            Push(new TableContainer(new TableBlock()));
            frame.CloseLeaf = false;
            frame.Pop = true;
        }
    }

    private void EndPre()
    {
        inPre = false;
        var text = preText.ToString();
        preText.Clear();

        if (Current is RootContainer)
        {
            Current.Add(new CodeBlock(preLanguage, text));

            return;
        }

        // In a list item or a table cell a code block is one paragraph per line, empty lines kept (US-7).
        foreach (var line in text.Split('\n'))
        {
            var leaf = new Leaf(LeafKind.Paragraph);

            if (line.Length > 0)
            {
                leaf.Items.Add(new Inline(line, Marks.None));
            }

            Current.Add(leaf);
        }
    }

    // --- frames and containers -----------------------------------------------------------------------------------

    private sealed class WalkFrame(IElement element)
    {
        public IElement Element { get; } = element;

        public int Next { get; set; }

        public Marks SavedMarks { get; set; }

        public bool Skipped { get; set; }

        public bool CloseLeaf { get; set; }

        public bool Pop { get; set; }

        public bool EndPre { get; set; }
    }

    private abstract class Container
    {
        public abstract void Add(Block block);
    }

    private sealed class RootContainer : Container
    {
        public List<Block> Blocks { get; } = [];

        public override void Add(Block block) => Blocks.Add(block);
    }

    private sealed class ListContainer(ListBlock list) : Container
    {
        private ItemBlock? implicitItem;

        public ListBlock List { get; } = list;

        public void ResetImplicit() => implicitItem = null;

        /// <summary>Content directly in a list, outside any item, goes into an item of its own.</summary>
        public override void Add(Block block)
        {
            if (implicitItem is null)
            {
                implicitItem = new ItemBlock();
                List.Items.Add(implicitItem);
            }

            implicitItem.Blocks.Add(block);
        }
    }

    private sealed class ItemContainer(ItemBlock item) : Container
    {
        public override void Add(Block block) => item.Blocks.Add(block);
    }

    private sealed class TableContainer(TableBlock table) : Container
    {
        public TableBlock Table { get; } = table;

        /// <summary>Text and blocks outside any cell are put before the table, where a parser foster-parents them.</summary>
        public List<Block> Before { get; } = [];

        public override void Add(Block block) => Before.Add(block);
    }

    private sealed class RowContainer(TableContainer table, RowBlock row) : Container
    {
        private readonly TableContainer table = table;

        public RowBlock Row { get; } = row;

        public override void Add(Block block) => table.Add(block);
    }

    private sealed class CellContainer : Container
    {
        private readonly CellBlock cell;

        public CellContainer(CellBlock cell) => this.cell = cell;

        public override void Add(Block block)
        {
            if (block is Leaf leaf)
            {
                cell.Paragraphs.Add(leaf);
            }
        }
    }
}
