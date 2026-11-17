using System.IO.Compression;
using Mustask.Application.Abstractions.Imports;
using Mustask.Domain.Imports.Source;
using Mustask.Infrastructure.Imports;
using Mustask.Infrastructure.Imports.Reading;
using Xunit;

namespace Mustask.Infrastructure.Tests.Imports;

public sealed class DryRunReportWriterTests
{
    [Theory]
    [InlineData("=1+1", "'=1+1")]
    [InlineData("+7", "'+7")]
    [InlineData("-x", "'-x")]
    [InlineData("@cmd", "'@cmd")]
    [InlineData("\tтаб", "'\tтаб")]
    [InlineData("обычный", "обычный")]
    [InlineData("", "")]
    public void Guard_FormulaStarts_GetAnApostrophe(string text, string expected) =>
        Assert.Equal(expected, DryRunReportWriter.Guard(text));

    [Fact]
    public void Write_OneRowPerFileRow_ReadableByTheSourceReader_FormulaTextEscaped()
    {
        var bytes = new DryRunReportWriter().Write(
        [
            new DryRunReportLine(2, "Будет создан", "=HYPERLINK(\"http://x\")", null, "Платформа", []),
            new DryRunReportLine(3, "Ошибка", "Отчёт", "PLT-TASK-1", "Платформа", ["Элемент не найден.", "Значение «Баг» не сопоставлено"]),
        ]);

        var table = new SourceFileReader().Read(bytes, "report.xlsx", new SourceReadOptions());

        Assert.Equal(["Строка", "Результат", "Заголовок", "Идентификатор", "Проект", "Причины и предупреждения"], table.Headers.Select(h => h.Text));
        Assert.Equal(2, table.DataRowCount);
        Assert.Equal("'=HYPERLINK(\"http://x\")", table.Rows[0].Cells[2].Text);
        Assert.Equal("Ошибка", table.Rows[1].Cells[1].Text);
        Assert.Contains("Элемент не найден.", table.Rows[1].Cells[5].Text);
        Assert.Equal(SourceCellKind.Number, table.Rows[0].Cells[0].Kind);

        using var zip = new ZipArchive(new MemoryStream(bytes));
        Assert.DoesNotContain(zip.Entries, e => e.FullName.EndsWith("vbaProject.bin", StringComparison.Ordinal));
    }
}
