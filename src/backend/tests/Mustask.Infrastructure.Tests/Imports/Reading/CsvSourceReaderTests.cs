using System.Text;
using Mustask.Domain.Imports;
using Mustask.Domain.Imports.Source;
using Mustask.Infrastructure.Imports.Reading;
using Mustask.Shared.Exceptions;

namespace Mustask.Infrastructure.Tests.Imports.Reading;

/// <summary>The .csv reader (doc-017 BR-IE-2, US-1 CSV scenarios) on generated, made-up text.</summary>
public class CsvSourceReaderTests
{
    private static readonly SourceFileReader Reader = new();

    private static readonly Encoding Windows1251 = CodePagesEncodingProvider.Instance.GetEncoding(1251)!;

    private static SourceTable Read(string text, SourceReadOptions? options = null, Encoding? encoding = null, bool bom = false)
    {
        var body = (encoding ?? new UTF8Encoding(false)).GetBytes(text);
        var bytes = bom ? [.. new byte[] { 0xEF, 0xBB, 0xBF }, .. body] : body;
        return Reader.Read(bytes, "Реестр.csv", options ?? new SourceReadOptions());
    }

    // US-1 "A CSV in Windows-1251 with «;» is detected and can be overridden"
    [Fact]
    public void Read_Windows1251Semicolon_DetectedAndReadInCyrillic()
    {
        var table = Read("Номер;Заголовок;Срок\r\nPLT-1;Первая задача;01.03.2026\r\n", encoding: Windows1251);

        Assert.Equal(SourceNames.Windows1251, table.Encoding);
        Assert.True(table.EncodingDetected);
        Assert.Equal(";", table.Delimiter);
        Assert.True(table.DelimiterDetected);
        Assert.Equal(["Номер", "Заголовок", "Срок"], table.Headers.Select(h => h.Text));
        Assert.Equal("Первая задача", table.Rows[0].Cells[1].Text);
    }

    [Fact]
    public void Read_DelimiterOverride_ReadsTheColumnsAgain()
    {
        const string text = "Номер;Заголовок\r\nPLT-1;Первая\r\n";

        var detected = Read(text);
        var forced = Read(text, new SourceReadOptions(Delimiter: ","));

        Assert.Equal(2, detected.Headers.Count);
        Assert.Single(forced.Headers);
        Assert.Equal("Номер;Заголовок", forced.Headers[0].Text);
        Assert.False(forced.DelimiterDetected);
    }

    [Fact]
    public void Read_EncodingOverride_IsUsedAndNotMarkedDetected()
    {
        var table = Read("Заголовок\r\nПривет\r\n", new SourceReadOptions(Encoding: SourceNames.Windows1251), Windows1251);

        Assert.Equal("Привет", table.Rows[0].Cells[0].Text);
        Assert.False(table.EncodingDetected);
    }

    [Fact]
    public void Read_Utf8WithBom_BomIsNotInTheFirstHeader()
    {
        var table = Read("Заголовок;Проект\r\nПервая;Платформа\r\n", bom: true);

        Assert.Equal(SourceNames.Utf8, table.Encoding);
        Assert.Equal("Заголовок", table.Headers[0].Text);
    }

    [Theory]
    [InlineData(",", "Номер,Заголовок,Срок\nPLT-1,Первая,01.03.2026\nPLT-2,Вторая,02.03.2026\n")]
    [InlineData("\t", "Номер\tЗаголовок\tСрок\nPLT-1\tПервая\t01.03.2026\n")]
    [InlineData(";", "Номер;Заголовок;Срок\nPLT-1;Первая;01.03.2026\n")]
    public void Read_DelimiterIsDetected(string expected, string text)
    {
        var table = Read(text);

        Assert.Equal(expected, table.Delimiter);
        Assert.Equal(3, table.Headers.Count);
    }

    [Fact]
    public void Read_DecimalCommaInsideSemicolonFile_DoesNotConfuseTheDelimiter()
    {
        var table = Read("Название;Оценка\nПервая;1,5\nВторая;2,5\n");

        Assert.Equal(";", table.Delimiter);
        Assert.Equal("1,5", table.Rows[0].Cells[1].Text);
    }

    // US-1 "A quoted value with a line break stays one value"
    [Fact]
    public void Read_QuotedLineBreaks_StayOneValue()
    {
        var table = Read("Заголовок;Описание;Срок\r\nПервая;\"строка один\r\nстрока два\nстрока три\";01.03.2026\r\nВторая;ровно;02.03.2026\r\n");

        Assert.Equal(2, table.DataRowCount);
        Assert.Equal(3, table.Rows[0].Cells.Count);
        Assert.Equal("строка один\r\nстрока два\nстрока три", table.Rows[0].Cells[1].Text);
        Assert.Equal("01.03.2026", table.Rows[0].Cells[2].Text);
        Assert.Equal("Вторая", table.Rows[1].Cells[0].Text);
    }

    [Fact]
    public void Read_EscapedQuotesAndDelimiterInQuotes_AreValue()
    {
        var table = Read("А;Б\n\"он сказал \"\"да\"\"; потом ушёл\";x\n");

        Assert.Equal("он сказал \"да\"; потом ушёл", table.Rows[0].Cells[0].Text);
        Assert.Equal("x", table.Rows[0].Cells[1].Text);
    }

    // BR-IE-3 "A .csv has one sheet, every value is text, and the header and first data rows apply as for a workbook"
    [Fact]
    public void Read_HeaderRowTwo_UsesRecordNumbersAndEveryValueIsText()
    {
        var table = Read("Реестр (выдуманный)\nНомер;Оценка\nPLT-1;12\nPLT-2;\n", new SourceReadOptions(HeaderRow: 2));

        Assert.Empty(table.Sheets);
        Assert.Equal("A2", table.Headers[0].Address);
        Assert.Equal(3, table.Rows[0].Number);
        Assert.Equal(SourceCellKind.Text, table.Rows[0].Cells[1].Kind);
        Assert.Equal("12", table.Rows[0].Cells[1].Text);
        Assert.True(table.Rows[1].Cells[1].IsEmpty);
        Assert.Null(table.DateSystem);
    }

    [Fact]
    public void Read_FinalLineBreak_DoesNotAddARow()
    {
        Assert.Equal(1, Read("А;Б\n1;2\n").DataRowCount);
        Assert.Equal(1, Read("А;Б\r\n1;2").DataRowCount);
        Assert.Equal(1, Read("А;Б\r\n1;2\r\n\r\n").DataRowCount);
    }

    [Fact]
    public void Read_BlankLineInside_IsAnEmptyRow()
    {
        var table = Read("А;Б\n1;2\n\n3;4\n");

        Assert.Equal(3, table.DataRowCount);
        Assert.True(table.Rows[1].IsEmpty);
    }

    [Fact]
    public void Read_RowShorterThanHeader_IsPaddedWithEmptyCells()
    {
        var table = Read("А;Б;В\n1\n");

        Assert.Equal(3, table.Rows[0].Cells.Count);
        Assert.True(table.Rows[0].Cells[2].IsEmpty);
    }

    // ----- BR-IE-21 -----

    [Fact]
    public void Read_5000Rows_AcceptedAnd5001Refused()
    {
        static string Csv(int rows) => "А;Б\n" + string.Concat(Enumerable.Range(1, rows).Select(i => $"{i};x\n"));

        Assert.Equal(5000, Read(Csv(5000)).DataRowCount);
        Assert.Equal(ImportErrorCodes.FileTooManyRows, XlsxSourceReaderTests.RefusalOf(() => Read(Csv(5001))));
    }

    [Fact]
    public void Read_201Columns_Refused()
    {
        var header = string.Join(';', Enumerable.Range(0, 201).Select(i => $"К{i}"));

        Assert.Equal(ImportErrorCodes.FileTooManyColumns, XlsxSourceReaderTests.RefusalOf(() => Read(header + "\n1\n")));
        Assert.Equal(200, Read(string.Join(';', Enumerable.Range(0, 200).Select(i => $"К{i}")) + "\n1\n").Headers.Count);
    }

    [Fact]
    public void Read_BinaryContent_IsUnreadable()
    {
        byte[] bytes = [.. Encoding.UTF8.GetBytes("А;Б\n"), 0, 0, 1, 2];

        Assert.Equal(ImportErrorCodes.FileUnreadable, XlsxSourceReaderTests.RefusalOf(() => Reader.Read(bytes, "a.csv", new SourceReadOptions())));
    }

    [Fact]
    public void Read_Utf16_IsUnreadable()
    {
        var bytes = Encoding.Unicode.GetPreamble().Concat(Encoding.Unicode.GetBytes("А;Б\n1;2\n")).ToArray();

        // The NUL bytes of UTF-16 are caught as binary; either way the reason is "unreadable", not a garbled read.
        Assert.Equal(ImportErrorCodes.FileUnreadable, XlsxSourceReaderTests.RefusalOf(() => Reader.Read(bytes, "a.csv", new SourceReadOptions())));
    }

    [Fact]
    public void Read_EmptyFile_IsUnreadable()
    {
        Assert.Equal(ImportErrorCodes.FileUnreadable, XlsxSourceReaderTests.RefusalOf(() => Reader.Read([], "a.csv", new SourceReadOptions())));
    }

    [Fact]
    public void Read_HeaderRowEmpty_IsAFieldError()
    {
        var exception = Assert.Throws<ValidationException>(() => Read("\n\nА;Б\n", new SourceReadOptions(HeaderRow: 1)));

        Assert.Equal(ImportErrorCodes.HeaderEmpty, exception.Errors["headerRow"][0].Code);
    }

    [Fact]
    public void Read_UnknownEncodingOrDelimiter_AreFieldErrors()
    {
        var encoding = Assert.Throws<ValidationException>(() => Read("А;Б\n", new SourceReadOptions(Encoding: "koi8-r")));
        var delimiter = Assert.Throws<ValidationException>(() => Read("А;Б\n", new SourceReadOptions(Delimiter: "|")));

        Assert.True(encoding.Errors.ContainsKey("encoding"));
        Assert.True(delimiter.Errors.ContainsKey("delimiter"));
    }
}
