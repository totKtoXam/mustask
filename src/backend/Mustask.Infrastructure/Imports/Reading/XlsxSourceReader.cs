using System.Globalization;
using System.Text;
using System.Xml;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Mustask.Domain.Imports;
using Mustask.Domain.Imports.Source;
using Mustask.Shared.Errors;

namespace Mustask.Infrastructure.Imports.Reading;

/// <summary>
/// Reads one sheet of a .xlsx (doc-017 BR-IE-3) on the Open XML SDK's streaming reader. The zip container has already passed
/// <see cref="ZipSafetyGate"/>. A cell gives its typed value — text, number, date, or the last value a formula computed (formulas are
/// never evaluated, macros never run); a merged cell gives its value at its top-left position only; a cell's hyperlink stays on the
/// cell. The row, column and sheet limits are enforced while reading and refuse the whole file.
/// </summary>
internal static class XlsxSourceReader
{
    private const int MaxMergedRegions = 50_000;

    private const int MaxStyleFormats = 64_000;

    public static SourceTable Read(byte[] content, SourceReadOptions options)
    {
        ZipSafetyGate.Check(content);

        try
        {
            return ReadChecked(content, options);
        }
        catch (Exception exception) when (exception is OpenXmlPackageException or InvalidDataException or XmlException or IOException
            or FileFormatException or ArgumentException or InvalidOperationException or NullReferenceException
            or KeyNotFoundException or FormatException or OverflowException)
        {
            throw SourceRefusal.Because(ImportErrorCodes.FileUnreadable, inner: exception);
        }
    }

    private static SourceTable ReadChecked(byte[] content, SourceReadOptions options)
    {
        using var stream = new MemoryStream(content, writable: false);
        using var document = SpreadsheetDocument.Open(stream, false, new OpenSettings { MaxCharactersInPart = ImportLimits.MaxExpandedBytes });

        var workbookPart = document.WorkbookPart ?? throw SourceRefusal.Because(ImportErrorCodes.FileUnreadable);
        var sheets = workbookPart.Workbook?.Sheets?.Elements<Sheet>().ToList() ?? [];

        if (sheets.Count > ImportLimits.MaxSheets)
        {
            throw SourceRefusal.Because(ImportErrorCodes.FileTooManySheets);
        }

        var worksheets = sheets
            .Where(sheet => sheet.Id?.Value is { } id && workbookPart.GetPartById(id) is WorksheetPart)
            .ToList();
        var names = worksheets.Select(sheet => sheet.Name?.Value ?? string.Empty).ToList();

        Sheet chosen;

        if (options.Sheet is null)
        {
            chosen = worksheets.FirstOrDefault() ?? throw SourceRefusal.Because(ImportErrorCodes.FileUnreadable);
        }
        else
        {
            chosen = worksheets.FirstOrDefault(sheet => string.Equals(sheet.Name?.Value, options.Sheet, StringComparison.Ordinal))
                ?? worksheets.FirstOrDefault(sheet => string.Equals(sheet.Name?.Value?.Trim(), options.Sheet.Trim(), StringComparison.OrdinalIgnoreCase))
                ?? throw SourceRefusal.Because(ImportErrorCodes.FileSheetMissing, new ErrorParams { ["sheet"] = ErrorParam.Text(options.Sheet) });
        }

        var sheetPart = (WorksheetPart)workbookPart.GetPartById(chosen.Id!.Value!);
        var is1904 = workbookPart.Workbook?.WorkbookProperties?.Date1904?.Value == true;
        var firstDataRow = options.FirstDataRow ?? options.HeaderRow + 1;

        var shared = ReadSharedStrings(workbookPart);
        var kinds = ReadStyleKinds(workbookPart);

        var (retained, lastDataRow) = ReadCells(sheetPart, options, firstDataRow, shared, kinds, is1904);

        var headerCells = retained.GetValueOrDefault(options.HeaderRow) ?? [];
        var headers = headerCells
            .Where(pair => pair.Value.Text.Trim().Length > 0)
            .OrderBy(pair => pair.Key)
            .Select(pair => new SourceHeader(pair.Key, pair.Value.Text.Trim(), pair.Value.Address))
            .ToList();

        if (headers.Count == 0)
        {
            throw SourceRefusal.BadOption("headerRow", ImportErrorCodes.HeaderEmpty);
        }

        var width = headers[^1].Index + 1;
        var dataRowCount = Math.Max(0, lastDataRow - firstDataRow + 1);
        var handBack = Math.Min(dataRowCount, options.MaxRows ?? int.MaxValue);
        var rows = new List<SourceRow>(handBack);

        for (var number = firstDataRow; number < firstDataRow + handBack; number++)
        {
            var present = retained.GetValueOrDefault(number);
            var cells = new SourceCell[width];

            for (var column = 0; column < width; column++)
            {
                cells[column] = present is not null && present.TryGetValue(column, out var cell)
                    ? cell
                    : SourceCell.Blank(CellAddress.Of(column, number));
            }

            rows.Add(new SourceRow(number, cells));
        }

        return new SourceTable(
            SourceFormat.Xlsx,
            names,
            chosen.Name?.Value,
            options.HeaderRow,
            firstDataRow,
            Encoding: null,
            Delimiter: null,
            EncodingDetected: false,
            DelimiterDetected: false,
            is1904 ? 1904 : 1900,
            headers,
            rows,
            dataRowCount);
    }

    private static (Dictionary<int, Dictionary<int, SourceCell>> Retained, int LastDataRow) ReadCells(
        WorksheetPart sheetPart,
        SourceReadOptions options,
        int firstDataRow,
        IReadOnlyList<string> shared,
        IReadOnlyList<NumberFormatKind> kinds,
        bool is1904)
    {
        var retained = new Dictionary<int, Dictionary<int, SourceCell>>();
        var lastDataRow = 0;
        var maxDataRow = firstDataRow + ImportLimits.MaxDataRows - 1;
        var merges = new List<string>();
        var links = new List<(string Reference, string Target)>();
        var relationships = sheetPart.HyperlinkRelationships.ToDictionary(r => r.Id, r => r.Uri.OriginalString);

        var row = 0;
        var column = -1;

        using var reader = OpenXmlReader.Create(sheetPart);

        while (reader.Read())
        {
            if (!reader.IsStartElement)
            {
                continue;
            }

            if (reader.ElementType == typeof(Row))
            {
                var attribute = reader.Attributes.FirstOrDefault(a => a.LocalName == "r");
                row = int.TryParse(attribute.Value, NumberStyles.None, CultureInfo.InvariantCulture, out var declared) ? declared : row + 1;
                column = -1;
            }
            else if (reader.ElementType == typeof(Cell))
            {
                var cell = (Cell)reader.LoadCurrentElement()!;

                if (cell.CellReference?.Value is { } reference && CellAddress.TryParse(reference, out var parsedColumn, out var parsedRow))
                {
                    column = parsedColumn;
                    row = parsedRow;
                }
                else
                {
                    column++;
                }

                var inHeader = row == options.HeaderRow;
                var inData = row >= firstDataRow;

                if (!inHeader && !inData)
                {
                    continue;
                }

                var converted = Convert(cell, column, row, shared, kinds, is1904);

                if (converted.IsEmpty)
                {
                    continue;
                }

                if (column >= ImportLimits.MaxColumns)
                {
                    throw SourceRefusal.Because(ImportErrorCodes.FileTooManyColumns);
                }

                if (inData)
                {
                    if (row > maxDataRow)
                    {
                        throw SourceRefusal.Because(ImportErrorCodes.FileTooManyRows);
                    }

                    lastDataRow = Math.Max(lastDataRow, row);
                }

                // Rows beyond what the caller asked for are counted, not kept.
                if (inHeader || options.MaxRows is null || row - firstDataRow < options.MaxRows)
                {
                    if (!retained.TryGetValue(row, out var cells))
                    {
                        cells = [];
                        retained[row] = cells;
                    }

                    cells[column] = converted;
                }
            }
            else if (reader.ElementType == typeof(MergeCell))
            {
                var merge = (MergeCell)reader.LoadCurrentElement()!;

                if (merge.Reference?.Value is { } region)
                {
                    merges.Add(region);

                    if (merges.Count > MaxMergedRegions)
                    {
                        throw SourceRefusal.Because(ImportErrorCodes.FileUnreadable);
                    }
                }
            }
            else if (reader.ElementType == typeof(Hyperlink))
            {
                var link = (Hyperlink)reader.LoadCurrentElement()!;

                if (link.Reference?.Value is { } reference && link.Id?.Value is { } id && relationships.TryGetValue(id, out var target))
                {
                    links.Add((reference, target));
                }
            }
        }

        ApplyMerges(retained, merges);
        ApplyLinks(retained, links);
        return (retained, lastDataRow);
    }

    /// <summary>A merged cell gives its value to its top-left position only (BR-IE-3): the other cells of the region are emptied.</summary>
    private static void ApplyMerges(Dictionary<int, Dictionary<int, SourceCell>> retained, List<string> merges)
    {
        foreach (var region in merges)
        {
            if (!TryParseRegion(region, out var firstColumn, out var firstRow, out var lastColumn, out var lastRow))
            {
                continue;
            }

            foreach (var (rowNumber, cells) in retained)
            {
                if (rowNumber < firstRow || rowNumber > lastRow)
                {
                    continue;
                }

                foreach (var column in cells.Keys.Where(c => c >= firstColumn && c <= lastColumn && !(c == firstColumn && rowNumber == firstRow)).ToList())
                {
                    cells.Remove(column);
                }
            }
        }
    }

    private static void ApplyLinks(Dictionary<int, Dictionary<int, SourceCell>> retained, List<(string Reference, string Target)> links)
    {
        foreach (var (reference, target) in links)
        {
            if (!TryParseRegion(reference, out var firstColumn, out var firstRow, out var lastColumn, out var lastRow))
            {
                continue;
            }

            foreach (var (rowNumber, cells) in retained)
            {
                if (rowNumber < firstRow || rowNumber > lastRow)
                {
                    continue;
                }

                foreach (var column in cells.Keys.Where(c => c >= firstColumn && c <= lastColumn).ToList())
                {
                    cells[column] = cells[column] with { Hyperlink = target };
                }
            }
        }
    }

    private static bool TryParseRegion(string region, out int firstColumn, out int firstRow, out int lastColumn, out int lastRow)
    {
        var parts = region.Split(':');
        firstColumn = firstRow = lastColumn = lastRow = 0;

        if (!CellAddress.TryParse(parts[0], out firstColumn, out firstRow))
        {
            return false;
        }

        if (parts.Length == 1)
        {
            lastColumn = firstColumn;
            lastRow = firstRow;
            return true;
        }

        return parts.Length == 2 && CellAddress.TryParse(parts[1], out lastColumn, out lastRow);
    }

    private static SourceCell Convert(Cell cell, int column, int row, IReadOnlyList<string> shared, IReadOnlyList<NumberFormatKind> kinds, bool is1904)
    {
        var address = CellAddress.Of(column, row);
        var type = cell.DataType?.Value;
        var raw = cell.CellValue?.Text;

        if (type == CellValues.InlineString)
        {
            return SourceCell.OfText(TextOf(cell.InlineString), address);
        }

        if (type == CellValues.SharedString)
        {
            return int.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out var index) && index < shared.Count
                ? SourceCell.OfText(shared[index], address)
                : SourceCell.Blank(address);
        }

        if (type == CellValues.String || type == CellValues.Error)
        {
            return SourceCell.OfText(raw ?? string.Empty, address);
        }

        if (string.IsNullOrEmpty(raw))
        {
            return SourceCell.Blank(address);
        }

        if (type == CellValues.Boolean)
        {
            return new SourceCell(SourceCellKind.Boolean, raw == "1" ? "TRUE" : "FALSE", null, null, null, address);
        }

        if (type == CellValues.Date)
        {
            return DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var iso)
                ? DateCell(iso, null, address)
                : SourceCell.OfText(raw, address);
        }

        if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var number) || double.IsNaN(number) || double.IsInfinity(number))
        {
            return SourceCell.OfText(raw, address);
        }

        var style = cell.StyleIndex?.Value is { } styleIndex && styleIndex < kinds.Count ? kinds[(int)styleIndex] : NumberFormatKind.General;

        if (style == NumberFormatKind.Date && NumberFormats.FromSerial(number, is1904) is { } date)
        {
            return DateCell(date, number, address);
        }

        return new SourceCell(SourceCellKind.Number, number.ToString("G15", CultureInfo.InvariantCulture), number, null, null, address);
    }

    private static SourceCell DateCell(DateTime date, double? serial, string address)
    {
        var text = date.TimeOfDay == TimeSpan.Zero
            ? date.ToString("dd.MM.yyyy", CultureInfo.InvariantCulture)
            : date.ToString("dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture);

        return new SourceCell(SourceCellKind.Date, text, serial, date, null, address);
    }

    private static string TextOf(OpenXmlElement? item)
    {
        if (item is null)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();

        foreach (var child in item.ChildElements)
        {
            switch (child)
            {
                case Text text:
                    builder.Append(text.Text);
                    break;
                case Run run:
                    builder.Append(run.Text?.Text);
                    break;
            }
        }

        return builder.ToString();
    }

    private static List<string> ReadSharedStrings(WorkbookPart workbookPart)
    {
        var strings = new List<string>();

        if (workbookPart.SharedStringTablePart is not { } part)
        {
            return strings;
        }

        using var reader = OpenXmlReader.Create(part);

        while (reader.Read())
        {
            if (reader.ElementType == typeof(SharedStringItem) && reader.IsStartElement)
            {
                strings.Add(TextOf(reader.LoadCurrentElement()));
            }
        }

        return strings;
    }

    private static List<NumberFormatKind> ReadStyleKinds(WorkbookPart workbookPart)
    {
        var kinds = new List<NumberFormatKind>();

        if (workbookPart.WorkbookStylesPart is not { } part)
        {
            return kinds;
        }

        var custom = new Dictionary<int, string>();
        var formatIds = new List<int>();
        var inCellFormats = false;

        using var reader = OpenXmlReader.Create(part);

        while (reader.Read())
        {
            if (reader.ElementType == typeof(CellFormats))
            {
                inCellFormats = reader.IsStartElement;
            }
            else if (reader.ElementType == typeof(NumberingFormat) && reader.IsStartElement)
            {
                var format = (NumberingFormat)reader.LoadCurrentElement()!;

                if (format.NumberFormatId?.Value is { } id && format.FormatCode?.Value is { } code)
                {
                    custom[(int)id] = code;
                }
            }
            else if (inCellFormats && reader.ElementType == typeof(CellFormat) && reader.IsStartElement)
            {
                var format = (CellFormat)reader.LoadCurrentElement()!;
                formatIds.Add(format.NumberFormatId?.Value is { } id ? (int)id : 0);

                if (formatIds.Count > MaxStyleFormats)
                {
                    throw SourceRefusal.Because(ImportErrorCodes.FileUnreadable);
                }
            }
        }

        foreach (var id in formatIds)
        {
            kinds.Add(NumberFormats.Classify(id, custom.GetValueOrDefault(id)));
        }

        return kinds;
    }
}
