using System.IO.Compression;
using System.Text;
using Mustask.Domain.Imports;
using Mustask.Domain.Imports.Source;
using Mustask.Infrastructure.Imports.Reading;
using Mustask.Shared.Exceptions;

namespace Mustask.Infrastructure.Tests.Imports.Reading;

/// <summary>
/// The .xlsx reader on generated, made-up workbooks (mod-024 plan-backend § 3). Stories: doc-017 US-1 (sheet, rows, formulas, empty
/// rows, refusals), BR-IE-3, BR-IE-21 and the file-safety NFR.
/// </summary>
public class XlsxSourceReaderTests
{
    private static readonly SourceFileReader Reader = new();

    internal static string RefusalOf(Func<object> read)
    {
        var exception = Assert.Throws<ValidationException>(() => read());
        Assert.Equal(ImportErrorCodes.FileRefused, exception.Code);
        return exception.Errors[SourceRefusal.Field][0].Code;
    }

    private static byte[] Register(Action<XlsxBuilder.SheetBuilder> fill, string sheet = "Задачи") =>
        new XlsxBuilder().Sheet(sheet, fill).Build();

    private static void Fill(XlsxBuilder.SheetBuilder s, int dataRows)
    {
        s.Text("A1", "Номер").Text("B1", "Заголовок");

        for (var row = 2; row < 2 + dataRows; row++)
        {
            s.Inline($"A{row}", $"PLT-{row}").Inline($"B{row}", $"Задача {row}");
        }
    }

    // US-1 "The template reads the chosen sheet from the chosen rows"
    [Fact]
    public void Read_ChosenSheetHeaderAndDataRow_ReturnsNineHeadersAndSampleFromRowThree()
    {
        string[] headers = ["Номер", "Проект", "Заголовок", "Вид", "Статус", "Ответственный", "Важность", "Срок", "Ссылка"];
        var bytes = new XlsxBuilder()
            .Sheet("Справка", s => s.Text("A1", "не то"))
            .Sheet("Задачи", s =>
            {
                s.Text("A1", "Реестр задач (выдуманный)");

                for (var i = 0; i < headers.Length; i++)
                {
                    s.Text($"{(char)('A' + i)}2", headers[i]);
                }

                s.Inline("A3", "PLT-1").Inline("C3", "Первая");
                s.Inline("A4", "PLT-2").Inline("C4", "Вторая");
            })
            .Build();

        var table = Reader.Read(bytes, "Реестр.xlsx", new SourceReadOptions("Задачи", 2, 3));

        Assert.Equal(SourceFormat.Xlsx, table.Format);
        Assert.Equal(["Справка", "Задачи"], table.Sheets);
        Assert.Equal("Задачи", table.Sheet);
        Assert.Equal(headers, table.Headers.Select(h => h.Text));
        Assert.Equal("C2", table.Headers[2].Address);
        Assert.Equal(2, table.DataRowCount);
        Assert.Equal(3, table.Rows[0].Number);
        Assert.Equal("PLT-1", table.Rows[0].Cells[0].Text);
        Assert.Equal("Первая", table.Rows[0].Cells[2].Text);
        Assert.True(table.Rows[0].Cells[1].IsEmpty);
        Assert.Equal(1900, table.DateSystem);
    }

    [Fact]
    public void Read_NoSheetNamed_ReadsTheFirstSheet()
    {
        var table = Reader.Read(Register(s => Fill(s, 2)), "a.xlsx", new SourceReadOptions());

        Assert.Equal("Задачи", table.Sheet);
        Assert.Equal(2, table.DataRowCount);
    }

    [Fact]
    public void Read_SheetMissing_RefusesNamingTheSheet()
    {
        var exception = Assert.Throws<ValidationException>(() =>
            Reader.Read(Register(s => Fill(s, 1)), "a.xlsx", new SourceReadOptions("Нет такого")));

        var reason = exception.Errors[SourceRefusal.Field][0];
        Assert.Equal(ImportErrorCodes.FileSheetMissing, reason.Code);
        Assert.Equal("Нет такого", reason.Params["sheet"].Value);
    }

    // US-1 "A formula's last value is read and never evaluated" · BR-IE-3
    [Fact]
    public void Read_Formula_GivesTheStoredValueNotTheFormulaResult()
    {
        var bytes = Register(s =>
        {
            s.Text("A1", "Проект").Text("B1", "Сумма").Text("C1", "Без значения");
            s.FormulaText("A2", "\"Пла\"&\"тформа\"", "Платформа");
            s.FormulaNumber("B2", "1+1", 5);
            s.FormulaWithoutValue("C2", "SUM(A1:A9)");
        });

        var row = Reader.Read(bytes, "a.xlsx", new SourceReadOptions()).Rows[0];

        Assert.Equal("Платформа", row.Cells[0].Text);
        Assert.Equal(5d, row.Cells[1].Number);
        Assert.True(row.Cells[2].IsEmpty);
    }

    // BR-IE-3 "A merged cell gives its value to its top-left position only"
    [Fact]
    public void Read_MergedCell_ValueOnTopLeftOnly()
    {
        var bytes = Register(s =>
        {
            s.Text("A1", "Группа").Text("B1", "Заголовок").Text("C1", "Срок");
            s.Inline("A2", "Платформа").Inline("A3", "эхо из скрытой ячейки").Inline("B2", "Один").Inline("B3", "Два");
            s.Merge("A2:A3");
        });

        var table = Reader.Read(bytes, "a.xlsx", new SourceReadOptions());

        Assert.Equal("Платформа", table.Rows[0].Cells[0].Text);
        Assert.True(table.Rows[1].Cells[0].IsEmpty);
        Assert.Equal("Два", table.Rows[1].Cells[1].Text);
    }

    // BR-IE-3 "A cell gives its typed value: text, number, date"
    [Fact]
    public void Read_CellsByType_AreTextNumberDateBoolean()
    {
        var bytes = Register(s =>
        {
            s.Text("A1", "Текст").Text("B1", "Число").Text("C1", "Дата").Text("D1", "Дата и время").Text("E1", "Время").Text("F1", "Флаг").Text("G1", "Ошибка");
            s.Text("A2", "Платформа").Number("B2", 1200.5).Number("C2", 46023, XlsxBuilder.DateStyle).Number("D2", 46023.5, XlsxBuilder.DateTimeStyle);
            s.Number("E2", 0.25, XlsxBuilder.TimeStyle).Bool("F2", true).Error("G2", "#N/A");
        });

        var cells = Reader.Read(bytes, "a.xlsx", new SourceReadOptions()).Rows[0].Cells;

        Assert.Equal(SourceCellKind.Text, cells[0].Kind);
        Assert.Equal(SourceCellKind.Number, cells[1].Kind);
        Assert.Equal("1200.5", cells[1].Text);
        Assert.Equal(SourceCellKind.Date, cells[2].Kind);
        Assert.Equal(new DateTime(2026, 1, 1), cells[2].Date);
        Assert.Equal(46023d, cells[2].Number);
        Assert.Equal("01.01.2026", cells[2].Text);
        Assert.Equal(new DateTime(2026, 1, 1, 12, 0, 0), cells[3].Date);
        Assert.Equal(SourceCellKind.Number, cells[4].Kind);
        Assert.Equal(SourceCellKind.Boolean, cells[5].Kind);
        Assert.Equal("#N/A", cells[6].Text);
    }

    // BR-IE-3 / the converter "Дата из числа Excel": the 1904 system is reported and applied
    [Fact]
    public void Read_Workbook1904_DateUsesTheOtherEpoch()
    {
        var builder = new XlsxBuilder { Date1904 = true };
        builder.Sheet("Л", s => s.Text("A1", "Дата").Number("A2", 1000, XlsxBuilder.DateStyle));

        var table = Reader.Read(builder.Build(), "a.xlsx", new SourceReadOptions());

        Assert.Equal(1904, table.DateSystem);
        Assert.Equal(new DateTime(1904, 1, 1).AddDays(1000), table.Rows[0].Cells[0].Date);
    }

    // BR-IE-3 "A cell's hyperlink stays available to «URL из гиперссылки»"
    [Fact]
    public void Read_CellWithHyperlink_KeepsTheTarget()
    {
        var bytes = Register(s =>
        {
            s.Text("A1", "Ссылка");
            s.Text("A2", "Документ").Link("A2", "https://docs.example.com/page");
        });

        var cell = Reader.Read(bytes, "a.xlsx", new SourceReadOptions()).Rows[0].Cells[0];

        Assert.Equal("Документ", cell.Text);
        Assert.Equal("https://docs.example.com/page", cell.Hyperlink);
    }

    // US-1 "A header matches ignoring case and surrounding spaces" (the reader hands the header trimmed)
    [Fact]
    public void Read_HeaderWithSurroundingSpaces_IsTrimmed()
    {
        var table = Reader.Read(Register(s => s.Inline("A1", " заголовок ").Inline("A2", "x")), "a.xlsx", new SourceReadOptions());

        Assert.Equal("заголовок", table.Headers[0].Text);
    }

    // US-1 "Empty rows are skipped and counted" (the reader keeps them in place, the dry run reports them)
    [Fact]
    public void Read_EmptyRowsInside_AreKeptAsEmptyRowsAndCounted()
    {
        var bytes = Register(s =>
        {
            s.Text("A1", "Заголовок");
            s.Inline("A2", "Один");
            s.Inline("A6", "Два");
        });

        var table = Reader.Read(bytes, "a.xlsx", new SourceReadOptions());

        Assert.Equal(5, table.DataRowCount);
        Assert.Equal([2, 3, 4, 5, 6], table.Rows.Select(r => r.Number));
        Assert.True(table.Rows[1].IsEmpty);
        Assert.False(table.Rows[4].IsEmpty);
    }

    [Fact]
    public void Read_MaxRows_HandsBackThatManyButCountsAll()
    {
        var table = Reader.Read(Register(s => Fill(s, 30)), "a.xlsx", new SourceReadOptions(MaxRows: 5));

        Assert.Equal(5, table.Rows.Count);
        Assert.Equal(30, table.DataRowCount);
    }

    [Fact]
    public void Read_HeaderRowEmpty_ReportsNoHeaders()
    {
        var exception = Assert.Throws<ValidationException>(() =>
            Reader.Read(Register(s => Fill(s, 2)), "a.xlsx", new SourceReadOptions(HeaderRow: 7)));

        Assert.Equal(ImportErrorCodes.ValidationFailed, exception.Code);
        Assert.Equal(ImportErrorCodes.HeaderEmpty, exception.Errors["headerRow"][0].Code);
    }

    [Theory]
    [InlineData(0, null, "headerRow", ImportErrorCodes.HeaderRowInvalid)]
    [InlineData(51, null, "headerRow", ImportErrorCodes.HeaderRowInvalid)]
    [InlineData(2, 2, "firstDataRow", ImportErrorCodes.FirstDataRowInvalid)]
    [InlineData(3, 1, "firstDataRow", ImportErrorCodes.FirstDataRowInvalid)]
    public void Read_BadRowOptions_AreFieldErrors(int header, int? first, string field, string code)
    {
        var exception = Assert.Throws<ValidationException>(() =>
            Reader.Read(Register(s => Fill(s, 2)), "a.xlsx", new SourceReadOptions(HeaderRow: header, FirstDataRow: first)));

        Assert.Equal(code, exception.Errors[field][0].Code);
    }

    // ----- BR-IE-21 limits -----

    // US-1 "An unsupported or oversized file is refused": 5 001 data rows refused whole, 5 000 accepted
    [Fact]
    public void Read_5000DataRows_Accepted()
    {
        var table = Reader.Read(Register(s => Fill(s, ImportLimits.MaxDataRows)), "a.xlsx", new SourceReadOptions());

        Assert.Equal(ImportLimits.MaxDataRows, table.DataRowCount);
    }

    [Fact]
    public void Read_5001DataRows_RefusedWholeNothingRead()
    {
        var bytes = Register(s => Fill(s, ImportLimits.MaxDataRows + 1));

        Assert.Equal(ImportErrorCodes.FileTooManyRows, RefusalOf(() => Reader.Read(bytes, "a.xlsx", new SourceReadOptions())));
    }

    [Fact]
    public void Read_5001DataRowsEvenWhenOnlyAFewAreAsked_StillRefused()
    {
        var bytes = Register(s => Fill(s, ImportLimits.MaxDataRows + 1));

        Assert.Equal(ImportErrorCodes.FileTooManyRows, RefusalOf(() => Reader.Read(bytes, "a.xlsx", new SourceReadOptions(MaxRows: 3))));
    }

    [Fact]
    public void Read_FirstDataRowLower_CountsFromThere()
    {
        // 5 001 rows in the sheet, but the data starts at row 3: 4 999 data rows are within the limit.
        var table = Reader.Read(Register(s => Fill(s, ImportLimits.MaxDataRows)), "a.xlsx", new SourceReadOptions(FirstDataRow: 3));

        Assert.Equal(ImportLimits.MaxDataRows - 1, table.DataRowCount);
    }

    [Fact]
    public void Read_200Columns_AcceptedAnd201Refused()
    {
        string Letters(int index) => CellAddress.ColumnLetters(index);

        var ok = Register(s =>
        {
            for (var c = 0; c < 200; c++)
            {
                s.Inline($"{Letters(c)}1", $"К{c}");
            }
        });
        var tooMany = Register(s =>
        {
            for (var c = 0; c < 201; c++)
            {
                s.Inline($"{Letters(c)}1", $"К{c}");
            }
        });

        Assert.Equal(200, Reader.Read(ok, "a.xlsx", new SourceReadOptions()).Headers.Count);
        Assert.Equal(ImportErrorCodes.FileTooManyColumns, RefusalOf(() => Reader.Read(tooMany, "a.xlsx", new SourceReadOptions())));
    }

    [Fact]
    public void Read_50SheetsAccepted_51Refused()
    {
        static byte[] Book(int count)
        {
            var builder = new XlsxBuilder();

            for (var i = 0; i < count; i++)
            {
                builder.Sheet($"Лист{i}", s => s.Text("A1", "Заголовок"));
            }

            return builder.Build();
        }

        Assert.Equal(50, Reader.Read(Book(50), "a.xlsx", new SourceReadOptions()).Sheets.Count);
        Assert.Equal(ImportErrorCodes.FileTooManySheets, RefusalOf(() => Reader.Read(Book(51), "a.xlsx", new SourceReadOptions())));
    }

    [Fact]
    public void Read_FileOver10MB_Refused()
    {
        var bytes = new byte[ImportLimits.MaxFileBytes + 1];
        bytes[0] = 0x50;

        Assert.Equal(ImportErrorCodes.FileTooLarge, RefusalOf(() => Reader.Read(bytes, "a.xlsx", new SourceReadOptions())));
    }

    // ----- BR-IE-2 formats -----

    // US-1 "An unsupported or oversized file is refused": «Реестр.xls»
    [Theory]
    [InlineData("Реестр.xls")]
    [InlineData("Реестр.xlsm")]
    [InlineData("Реестр.ods")]
    [InlineData("Реестр.txt")]
    [InlineData("Реестр")]
    public void Read_OtherExtensions_AreUnsupported(string fileName)
    {
        Assert.Equal(ImportErrorCodes.FileUnsupportedFormat, RefusalOf(() => Reader.Read(Register(s => Fill(s, 1)), fileName, new SourceReadOptions())));
    }

    [Fact]
    public void Read_BinaryXlsRenamedXlsx_IsUnsupported()
    {
        byte[] ole = [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1, 0, 0, 0, 0];

        Assert.Equal(ImportErrorCodes.FileUnsupportedFormat, RefusalOf(() => Reader.Read(ole, "a.xlsx", new SourceReadOptions())));
    }

    // ----- File safety (NFR) -----

    [Fact]
    public void Read_NotAZip_IsUnreadable()
    {
        Assert.Equal(ImportErrorCodes.FileUnreadable, RefusalOf(() => Reader.Read(Encoding.UTF8.GetBytes("это не книга"), "a.xlsx", new SourceReadOptions())));
    }

    [Fact]
    public void Read_TruncatedWorkbook_IsUnreadable()
    {
        var bytes = Register(s => Fill(s, 50));
        var cut = bytes[..(bytes.Length / 2)];

        Assert.Equal(ImportErrorCodes.FileUnreadable, RefusalOf(() => Reader.Read(cut, "a.xlsx", new SourceReadOptions())));
    }

    [Fact]
    public void Read_ZipWithoutAWorkbook_IsUnreadable()
    {
        using var stream = new MemoryStream();

        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            using var writer = new StreamWriter(zip.CreateEntry("hello.txt").Open());
            writer.Write("привет");
        }

        Assert.Equal(ImportErrorCodes.FileUnreadable, RefusalOf(() => Reader.Read(stream.ToArray(), "a.xlsx", new SourceReadOptions())));
    }

    // NFR file safety "a workbook that would expand past 100 MB … is refused whole"
    [Fact]
    public void Read_ZipBomb_RefusedWithoutReadingRows()
    {
        var builder = new XlsxBuilder();
        builder.Sheet("Л", s => s.Text("A1", "Заголовок"));
        builder.ExtraParts.Add(("xl/media/bomb.bin", new byte[ImportLimits.MaxExpandedBytes + 1_048_576]));
        var bytes = builder.Build();

        Assert.True(bytes.Length < ImportLimits.MaxFileBytes, $"the made-up bomb must fit the 10 MB limit; it is {bytes.Length}");
        Assert.Equal(ImportErrorCodes.FileExpandsTooMuch, RefusalOf(() => Reader.Read(bytes, "a.xlsx", new SourceReadOptions())));
    }

    [Fact]
    public void Read_ManyTinyEntries_RefusedAsExpansion()
    {
        var builder = new XlsxBuilder();
        builder.Sheet("Л", s => s.Text("A1", "Заголовок"));

        for (var i = 0; i <= ImportLimits.MaxZipEntries; i++)
        {
            builder.ExtraParts.Add(($"xl/media/{i}.bin", [1]));
        }

        Assert.Equal(ImportErrorCodes.FileExpandsTooMuch, RefusalOf(() => Reader.Read(builder.Build(), "a.xlsx", new SourceReadOptions())));
    }

    // NFR file safety "… holds a macro is refused whole"
    [Fact]
    public void Read_VbaProjectPart_RefusedAsMacros()
    {
        var builder = new XlsxBuilder();
        builder.Sheet("Л", s => s.Text("A1", "Заголовок"));
        builder.ExtraParts.Add(("xl/vbaProject.bin", [1, 2, 3]));

        Assert.Equal(ImportErrorCodes.FileHasMacros, RefusalOf(() => Reader.Read(builder.Build(), "a.xlsx", new SourceReadOptions())));
    }

    [Fact]
    public void Read_MacroEnabledContentType_RefusedAsMacros()
    {
        var builder = new XlsxBuilder
        {
            ExtraContentTypes = "<Override PartName=\"/xl/other.xml\" ContentType=\"application/vnd.ms-excel.sheet.macroEnabled.main+xml\"/>",
        };
        builder.Sheet("Л", s => s.Text("A1", "Заголовок"));

        Assert.Equal(ImportErrorCodes.FileHasMacros, RefusalOf(() => Reader.Read(builder.Build(), "a.xlsx", new SourceReadOptions())));
    }

    // NFR file safety "… references an external file is refused whole"
    [Fact]
    public void Read_ExternalLinkPart_RefusedAsExternalReference()
    {
        var builder = new XlsxBuilder();
        builder.Sheet("Л", s => s.Text("A1", "Заголовок"));
        builder.ExtraParts.Add(("xl/externalLinks/externalLink1.xml", Encoding.UTF8.GetBytes("<externalLink/>")));

        Assert.Equal(ImportErrorCodes.FileExternalReference, RefusalOf(() => Reader.Read(builder.Build(), "a.xlsx", new SourceReadOptions())));
    }

    [Fact]
    public void Read_ExternalNonHyperlinkRelationship_RefusedAsExternalReference()
    {
        var builder = new XlsxBuilder
        {
            ExtraWorkbookRelationships = "<Relationship Id=\"rIdX\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/oleObject\" Target=\"file:///C:/other/book.xlsx\" TargetMode=\"External\"/>",
        };
        builder.Sheet("Л", s => s.Text("A1", "Заголовок"));

        Assert.Equal(ImportErrorCodes.FileExternalReference, RefusalOf(() => Reader.Read(builder.Build(), "a.xlsx", new SourceReadOptions())));
    }

    [Fact]
    public void Read_ExternalHyperlinkRelationship_IsNotAReference()
    {
        var bytes = Register(s => s.Text("A1", "Ссылка").Text("A2", "Док").Link("A2", "https://example.com/x"));

        Assert.Single(Reader.Read(bytes, "a.xlsx", new SourceReadOptions()).Rows);
    }

    // XXE: a DOCTYPE with an entity is refused, never expanded, never fetched
    [Fact]
    public void Read_SheetWithDoctype_IsUnreadable()
    {
        var builder = new XlsxBuilder();
        builder.Sheet("Л", s =>
        {
            s.RawSheetXmlPrefix = "<!DOCTYPE worksheet [<!ENTITY x SYSTEM \"file:///etc/hostname\">]>";
            s.Text("A1", "Заголовок");
        });

        Assert.Equal(ImportErrorCodes.FileUnreadable, RefusalOf(() => Reader.Read(builder.Build(), "a.xlsx", new SourceReadOptions())));
    }

    [Fact]
    public void Read_ContentTypesWithDoctype_IsUnreadable()
    {
        var builder = new XlsxBuilder { ExtraContentTypes = "<!-- x -->" };
        builder.Sheet("Л", s => s.Text("A1", "Заголовок"));
        var bytes = builder.Build();

        // Rewrite [Content_Types].xml with a DOCTYPE.
        using var stream = new MemoryStream();
        stream.Write(bytes);
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Update, leaveOpen: true))
        {
            zip.GetEntry("[Content_Types].xml")!.Delete();
            using var writer = new StreamWriter(zip.CreateEntry("[Content_Types].xml").Open());
            writer.Write("<?xml version=\"1.0\"?><!DOCTYPE Types [<!ENTITY a \"aaaa\">]><Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">&a;</Types>");
        }

        Assert.Equal(ImportErrorCodes.FileUnreadable, RefusalOf(() => Reader.Read(stream.ToArray(), "a.xlsx", new SourceReadOptions())));
    }
}
