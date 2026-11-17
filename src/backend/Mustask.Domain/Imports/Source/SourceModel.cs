namespace Mustask.Domain.Imports.Source;

/// <summary>The kinds of source file (BR-IE-2).</summary>
public enum SourceFormat
{
    /// <summary>A workbook.</summary>
    Xlsx,

    /// <summary>A delimited text file.</summary>
    Csv,
}

/// <summary>What a cell holds (BR-IE-3): a .csv cell is always <see cref="Text"/> or <see cref="Empty"/>.</summary>
public enum SourceCellKind
{
    /// <summary>Nothing, or text that is empty.</summary>
    Empty,

    /// <summary>Text.</summary>
    Text,

    /// <summary>A number.</summary>
    Number,

    /// <summary>A number the cell's number format shows as a date.</summary>
    Date,

    /// <summary>A boolean.</summary>
    Boolean,
}

/// <summary>
/// One cell as the reader hands it on (BR-IE-3). <see cref="Text"/> is the cell as a text (a number as invariant digits, a date as
/// <c>dd.MM.yyyy</c>, a boolean as <c>TRUE</c>/<c>FALSE</c>); <see cref="Number"/> is the raw number of a number or date cell (an
/// Excel serial for a date); <see cref="Date"/> is the date a date cell shows; <see cref="Hyperlink"/> is the target of the cell's
/// link, if any.
/// </summary>
public sealed record SourceCell(
    SourceCellKind Kind,
    string Text,
    double? Number,
    DateTime? Date,
    string? Hyperlink,
    string Address)
{
    /// <summary>An empty cell at <paramref name="address"/>.</summary>
    public static SourceCell Blank(string address) => new(SourceCellKind.Empty, string.Empty, null, null, null, address);

    /// <summary>A text cell (an empty text is an empty cell).</summary>
    public static SourceCell OfText(string text, string address) =>
        text.Length == 0 ? Blank(address) : new SourceCell(SourceCellKind.Text, text, null, null, null, address);

    /// <summary>Whether the cell holds nothing.</summary>
    public bool IsEmpty => Kind == SourceCellKind.Empty;
}

/// <summary>A header of the header row: the column's 0-based index among the cells, its text and its address («B2»).</summary>
public sealed record SourceHeader(int Index, string Text, string Address);

/// <summary>A data row: its 1-based number in the file and one cell per column index, empty cells included.</summary>
public sealed record SourceRow(int Number, IReadOnlyList<SourceCell> Cells)
{
    /// <summary>Whether every cell is empty.</summary>
    public bool IsEmpty => Cells.All(cell => cell.IsEmpty);
}

/// <summary>How a file is read: the template's source settings (BR-IE-3) and, for a .csv, its encoding and delimiter (BR-IE-2).</summary>
/// <param name="Sheet">The sheet name; <c>null</c> reads the first.</param>
/// <param name="HeaderRow">1..50.</param>
/// <param name="FirstDataRow">Below the header row; <c>null</c> reads <paramref name="HeaderRow"/> + 1.</param>
/// <param name="Encoding"><c>utf-8</c> or <c>windows-1251</c>; <c>null</c> detects.</param>
/// <param name="Delimiter"><c>;</c>, <c>,</c> or a tab; <c>null</c> detects.</param>
/// <param name="MaxRows">The most data rows to hand back; <c>null</c> hands them all. The limits are applied on every row regardless.</param>
public sealed record SourceReadOptions(
    string? Sheet = null,
    int HeaderRow = 1,
    int? FirstDataRow = null,
    string? Encoding = null,
    string? Delimiter = null,
    int? MaxRows = null);

/// <summary>What the reader found in a file (BR-IE-3, US-1).</summary>
public sealed record SourceTable(
    SourceFormat Format,
    IReadOnlyList<string> Sheets,
    string? Sheet,
    int HeaderRow,
    int FirstDataRow,
    string? Encoding,
    string? Delimiter,
    bool EncodingDetected,
    bool DelimiterDetected,
    int? DateSystem,
    IReadOnlyList<SourceHeader> Headers,
    IReadOnlyList<SourceRow> Rows,
    int DataRowCount);

/// <summary>The names of the encodings and delimiters the template and the API use.</summary>
public static class SourceNames
{
    public const string Utf8 = "utf-8";

    public const string Windows1251 = "windows-1251";

    public const string Semicolon = ";";

    public const string Comma = ",";

    public const string Tab = "\t";
}

/// <summary>A cell address in A1 notation.</summary>
public static class CellAddress
{
    /// <summary>The address of a 0-based column and a 1-based row: <c>(1, 12)</c> is <c>B12</c>.</summary>
    public static string Of(int columnIndex, int row) => ColumnLetters(columnIndex) + row.ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>The letters of a 0-based column index: 0 is <c>A</c>, 26 is <c>AA</c>.</summary>
    public static string ColumnLetters(int columnIndex)
    {
        var letters = new System.Text.StringBuilder();
        var n = columnIndex + 1;

        while (n > 0)
        {
            n--;
            letters.Insert(0, (char)('A' + (n % 26)));
            n /= 26;
        }

        return letters.ToString();
    }

    /// <summary>Parses <c>B12</c> (or <c>$B$12</c>) into a 0-based column and a 1-based row.</summary>
    public static bool TryParse(string? reference, out int columnIndex, out int row)
    {
        columnIndex = -1;
        row = 0;

        if (string.IsNullOrEmpty(reference))
        {
            return false;
        }

        var i = 0;
        var column = 0;

        while (i < reference.Length && reference[i] == '$')
        {
            i++;
        }

        var lettersStart = i;

        while (i < reference.Length && reference[i] is >= 'A' and <= 'Z' or >= 'a' and <= 'z')
        {
            column = (column * 26) + (char.ToUpperInvariant(reference[i]) - 'A' + 1);

            if (column > 20_000)
            {
                return false;
            }

            i++;
        }

        if (i == lettersStart)
        {
            return false;
        }

        while (i < reference.Length && reference[i] == '$')
        {
            i++;
        }

        if (i >= reference.Length || !int.TryParse(reference.AsSpan(i), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out row) || row < 1)
        {
            return false;
        }

        columnIndex = column - 1;
        return true;
    }
}
