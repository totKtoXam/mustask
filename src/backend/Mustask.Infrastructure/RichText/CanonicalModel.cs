namespace Mustask.Infrastructure.RichText;

/// <summary>
/// The marks of one run of text. Tokens are indexes into the vocabulary's families, <c>-1</c> for none; the stored
/// class is written from the table by the writer, never carried from the input.
/// </summary>
internal readonly record struct Marks(
    string? Href, bool Bold, bool Italic, bool Underline, bool Strike, int Highlight, int Color, int Size, bool Code)
{
    public static Marks None { get; } = new(null, false, false, false, false, -1, -1, -1, false);

    /// <summary>
    /// US-7: inline code wins over every mark but a link; a heading drops the size (BR-RT-15).
    /// </summary>
    public Marks Canonical(bool inHeading)
    {
        if (Code)
        {
            return new Marks(Href, false, false, false, false, -1, -1, -1, true);
        }

        return inHeading ? this with { Size = -1 } : this;
    }
}

/// <summary>A run of text with its marks, or a line break when <see cref="Text"/> is <c>null</c>.</summary>
internal readonly record struct Inline(string? Text, Marks Marks)
{
    public static Inline Break { get; } = new(null, Marks.None);

    public bool IsBreak => Text is null;
}

internal abstract class Block;

internal enum LeafKind
{
    Paragraph,
    Heading3,
    Heading4,
}

/// <summary>A block that holds text directly: paragraph or heading (BR-RT-8's "leaf blocks").</summary>
internal sealed class Leaf(LeafKind kind) : Block
{
    public LeafKind Kind { get; } = kind;

    public List<Inline> Items { get; } = [];
}

/// <summary>A code block: language index into the vocabulary's family (<c>-1</c> plain) and its text.</summary>
internal sealed class CodeBlock(int language, string text) : Block
{
    public int Language { get; } = language;

    public string Text { get; } = text;
}

internal sealed class ListBlock(bool ordered) : Block
{
    public bool Ordered { get; } = ordered;

    public List<ItemBlock> Items { get; } = [];
}

/// <summary>A list item: paragraphs and nested lists only.</summary>
internal sealed class ItemBlock
{
    public List<Block> Blocks { get; } = [];
}

internal sealed class CellBlock(bool header, int columnSpan, int rowSpan)
{
    public bool Header { get; } = header;

    public int ColumnSpan { get; } = columnSpan;

    public int RowSpan { get; } = rowSpan;

    public List<Leaf> Paragraphs { get; } = [];
}

internal sealed class RowBlock(int group)
{
    public int Group { get; } = group;

    public List<CellBlock> Cells { get; } = [];
}

/// <summary>A table as read from the document; <see cref="RectangularTable"/> is what is written.</summary>
internal sealed class TableBlock : Block
{
    public List<RowBlock> Rows { get; } = [];

    public int CurrentGroup { get; set; }
}

/// <summary>A rectangular table: every row has the same number of cells, none spanning (BR-RT-17).</summary>
internal sealed class RectangularTable(List<List<CellBlock>> rows) : Block
{
    public List<List<CellBlock>> Rows { get; } = rows;
}
