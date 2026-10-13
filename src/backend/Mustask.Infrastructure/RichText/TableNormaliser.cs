namespace Mustask.Infrastructure.RichText;

/// <summary>The output (or the work to produce it) passed the bound; the reduction stops and answers "exceeded".</summary>
internal sealed class RichTextBudgetExceededException : Exception
{
    public RichTextBudgetExceededException()
        : base("The rich-text reduction passed its bound.")
    {
    }
}

/// <summary>
/// US-7 "Tables are rectangular" (BR-RT-17): a spanning cell keeps its content at its top-left position and every
/// other position it covered becomes an empty cell of the same element; a short row is padded at its end to the
/// widest row with empty cells of the row's last cell's element; a row with no cell and a table with no row are
/// removed. Spans are expanded without materialising more positions than the markup bound could ever hold.
/// </summary>
internal static class TableNormaliser
{
    private const int MaxColumnSpan = 1_000;

    /// <param name="maxPositions">
    /// More positions than this can only write more markup than the bound allows (each is at least a <c>td</c> and a
    /// paragraph), so the table is refused as exceeded before anything is built.
    /// </param>
    public static RectangularTable? Normalise(TableBlock table, long maxPositions)
    {
        var rows = table.Rows.Where(row => row.Cells.Count > 0).ToList();

        if (rows.Count == 0)
        {
            return null;
        }

        // The last row of each group, so a rowspan never crosses a row group.
        var lastOfGroup = new Dictionary<int, int>();

        for (var index = 0; index < rows.Count; index++)
        {
            lastOfGroup[rows[index].Group] = index;
        }

        var occupied = new Dictionary<int, bool>?[rows.Count];
        var placed = new List<(int Column, CellBlock Cell)>[rows.Count];
        var width = 0;
        long positions = 0;

        for (var r = 0; r < rows.Count; r++)
        {
            placed[r] = [];
            var column = 0;

            foreach (var cell in rows[r].Cells)
            {
                while (occupied[r] is { } row && row.ContainsKey(column))
                {
                    column++;
                }

                var columnSpan = Math.Clamp(cell.ColumnSpan, 1, MaxColumnSpan);
                var available = lastOfGroup[rows[r].Group] - r + 1;
                var rowSpan = cell.RowSpan == 0 ? available : Math.Clamp(cell.RowSpan, 1, available);

                placed[r].Add((column, cell));
                positions++;

                for (var rr = r; rr < r + rowSpan; rr++)
                {
                    for (var cc = column; cc < column + columnSpan; cc++)
                    {
                        if (rr == r && cc == column)
                        {
                            continue;
                        }

                        positions++;

                        if (positions > maxPositions)
                        {
                            throw new RichTextBudgetExceededException();
                        }

                        occupied[rr] ??= [];
                        occupied[rr]![cc] = cell.Header;
                    }
                }

                column += columnSpan;
                width = Math.Max(width, column);
            }

            if (occupied[r] is { Count: > 0 } covered)
            {
                width = Math.Max(width, covered.Keys.Max() + 1);
            }

            if (positions > maxPositions)
            {
                throw new RichTextBudgetExceededException();
            }
        }

        if ((long)width * rows.Count > maxPositions)
        {
            throw new RichTextBudgetExceededException();
        }

        var result = new List<List<CellBlock>>(rows.Count);

        for (var r = 0; r < rows.Count; r++)
        {
            var line = new CellBlock?[width];

            foreach (var (column, cell) in placed[r])
            {
                line[column] = cell;
            }

            var lastHeader = rows[r].Cells[^1].Header;
            var cells = new List<CellBlock>(width);

            for (var c = 0; c < width; c++)
            {
                if (line[c] is { } own)
                {
                    cells.Add(own);
                }
                else if (occupied[r] is { } covered && covered.TryGetValue(c, out var header))
                {
                    cells.Add(new CellBlock(header, 1, 1));
                }
                else
                {
                    cells.Add(new CellBlock(lastHeader, 1, 1));
                }
            }

            result.Add(cells);
        }

        return new RectangularTable(result);
    }
}
