using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Mustask.Application.Abstractions.Imports;

namespace Mustask.Infrastructure.Imports;

/// <summary>
/// The dry run's full report as a .xlsx (BR-IE-13): one row per file row. Every text cell is an inline string, and a text that begins with
/// «=», «+», «-», «@», a tab or a carriage return gets a leading apostrophe (BR-IE-22), so a value copied from the file never runs as a formula.
/// </summary>
public sealed class DryRunReportWriter : IDryRunReportWriter
{
    private static readonly string[] Headers = ["Строка", "Результат", "Заголовок", "Идентификатор", "Проект", "Причины и предупреждения"];

    /// <summary>The text as a safe cell value.</summary>
    public static string Guard(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        return text[0] is '=' or '+' or '-' or '@' or '\t' or '\r' ? "'" + text : text;
    }

    public byte[] Write(IReadOnlyList<DryRunReportLine> rows)
    {
        using var stream = new MemoryStream();

        using (var document = SpreadsheetDocument.Create(stream, SpreadsheetDocumentType.Workbook))
        {
            var workbook = document.AddWorkbookPart();
            workbook.Workbook = new Workbook();
            var sheetPart = workbook.AddNewPart<WorksheetPart>();
            var data = new SheetData();
            sheetPart.Worksheet = new Worksheet(data);

            data.Append(RowOf(1, Headers.Select(h => TextCell(h)).ToArray()));

            for (var i = 0; i < rows.Count; i++)
            {
                var line = rows[i];
                data.Append(RowOf(
                    (uint)(i + 2),
                    NumberCell(line.Row),
                    TextCell(Guard(line.Result)),
                    TextCell(Guard(line.Title)),
                    TextCell(Guard(line.Identifier)),
                    TextCell(Guard(line.Project)),
                    TextCell(Guard(string.Join("\n", line.Reasons)))));
            }

            var sheets = workbook.Workbook.AppendChild(new Sheets());
            sheets.Append(new Sheet { Id = workbook.GetIdOfPart(sheetPart), SheetId = 1, Name = "Отчёт" });
            workbook.Workbook.Save();
        }

        return stream.ToArray();
    }

    private static Row RowOf(uint index, params Cell[] cells)
    {
        var row = new Row { RowIndex = index };
        row.Append(cells);
        return row;
    }

    private static Cell TextCell(string text) =>
        new() { DataType = CellValues.InlineString, InlineString = new InlineString(new Text(text) { Space = SpaceProcessingModeValues.Preserve }) };

    private static Cell NumberCell(int value) =>
        new() { DataType = CellValues.Number, CellValue = new CellValue(value.ToString(System.Globalization.CultureInfo.InvariantCulture)) };
}
