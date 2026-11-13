using Mustask.Domain.Imports;
using Mustask.Domain.Imports.Conversion;
using Mustask.Domain.Process;
using Mustask.Domain.WorkItems;

namespace Mustask.Domain.Tests.Imports;

/// <summary>The converter catalogue and chains (doc-017 BR-IE-8, BR-IE-9) on made-up values.</summary>
public class ConverterEngineTests
{
    private static ConversionResult Run(string text, params ConverterStep[] chain) =>
        ConverterEngine.RunChain(ConversionValue.OfText(text), chain);

    [Fact]
    public void Trim_RemovesEdgeSpaces_AndCollapsesInnerWhenAsked()
    {
        Assert.Equal("a  b", Run("  a  b ", new ConverterStep(ConverterIds.Trim)).Value.Display());
        Assert.Equal("a b", Run("  a   b ", new ConverterStep(ConverterIds.Trim, Collapse: true)).Value.Display());
    }

    [Fact]
    public void Truncate_CutsToLength_AndRejectsNoLength()
    {
        Assert.Equal("abc", Run("abcdef", new ConverterStep(ConverterIds.Truncate, Length: 3)).Value.Display());
        Assert.False(Run("abcdef", new ConverterStep(ConverterIds.Truncate)).Ok);
    }

    [Theory]
    [InlineData("1 200,5", "1200,5")]
    [InlineData("3.25", "3,25")]
    [InlineData("-4", "-4")]
    public void Number_ReadsRussianAndInvariantSpelling(string input, string expected) =>
        Assert.Equal(expected, Run(input, new ConverterStep(ConverterIds.Number)).Value.Display());

    [Fact]
    public void Number_OnWords_FailsNamingTheConverter()
    {
        var result = Run("много", new ConverterStep(ConverterIds.Number));

        Assert.False(result.Ok);
        Assert.Equal(ConverterIds.Number, result.Failure!.Converter);
    }

    [Fact]
    public void ExcelDate_ConvertsASerial()
    {
        Assert.Equal("2026-03-15", Run("46096", new ConverterStep(ConverterIds.ExcelDate)).Value.Display());
        Assert.False(Run("0", new ConverterStep(ConverterIds.ExcelDate)).Ok);
    }

    [Theory]
    [InlineData(DateFormatIds.DmyDot, "15.03.2026", "2026-03-15")]
    [InlineData(DateFormatIds.YmdDash, "2026-03-15", "2026-03-15")]
    [InlineData(DateFormatIds.DmySlash, "15/03/2026", "2026-03-15")]
    [InlineData(DateFormatIds.MdySlash, "03/15/2026", "2026-03-15")]
    public void DateFormat_ParsesEachFormat(string format, string input, string expected) =>
        Assert.Equal(expected, Run(input, new ConverterStep(ConverterIds.DateFormat, Format: format)).Value.Display());

    [Fact]
    public void DateFormat_WrongText_Fails() =>
        Assert.False(Run("31.02.2026", new ConverterStep(ConverterIds.DateFormat, Format: DateFormatIds.DmyDot)).Ok);

    [Fact]
    public void Split_MakesATrimmedList_AndDropsEmptyItems()
    {
        var value = Run("a, b,, c", new ConverterStep(ConverterIds.Split, Separator: ",")).Value;

        Assert.Equal(ConversionKind.List, value.Kind);
        Assert.Equal(["a", "b", "c"], value.Items);
    }

    [Fact]
    public void Lookup_ReplacesOnlyTheNamedValue()
    {
        var step = new ConverterStep(ConverterIds.Lookup, From: "Срочно", To: "Высокий");

        Assert.Equal("Высокий", Run(" срочно ", step).Value.Display());
        Assert.Equal("Обычно", Run("Обычно", step).Value.Display());
    }

    [Fact]
    public void Extract_TakesTheGroup_AndGivesEmptyWhenNothingMatches()
    {
        var step = new ConverterStep(ConverterIds.Extract, Pattern: @"№(\d+)", Group: 1);

        Assert.Equal("42", Run("Заявка №42 от марта", step).Value.Display());
        Assert.True(Run("без номера", step).Value.IsEmpty);
    }

    [Fact]
    public void Extract_CatastrophicPattern_StopsOnTheTimeLimit()
    {
        var step = new ConverterStep(ConverterIds.Extract, Pattern: @"^(a+)+$");
        var result = Run(new string('a', 60) + "!", step);

        Assert.False(result.Ok);
        Assert.True(result.Failure!.Timeout);
    }

    [Fact]
    public void Chain_RunsInOrder_AndReportsEachStep()
    {
        var result = Run("  15.03.2026 ", new ConverterStep(ConverterIds.Trim), new ConverterStep(ConverterIds.DateFormat, Format: DateFormatIds.DmyDot));

        Assert.True(result.Ok);
        Assert.Equal(2, result.Steps.Count);
        Assert.Equal("15.03.2026", result.Steps[0].Display());
        Assert.Equal("2026-03-15", result.Value.Display());
    }

    [Fact]
    public void Chain_StopsAtTheFirstFailure()
    {
        var result = Run("abc", new ConverterStep(ConverterIds.Number), new ConverterStep(ConverterIds.Trim));

        Assert.False(result.Ok);
        Assert.Empty(result.Steps);
    }

    [Fact]
    public void EmptyValue_PassesEveryStep() =>
        Assert.True(ConverterEngine.RunChain(ConversionValue.Empty, [new ConverterStep(ConverterIds.Number)]).Value.IsEmpty);

    [Fact]
    public void Merge_FillsPlaceholders_AndTrims()
    {
        var value = ConverterEngine.Merge("{Фамилия} {Имя}", name => name == "Фамилия" ? "Иванов" : string.Empty);

        Assert.Equal("Иванов", value.Display());
        Assert.Equal(["Фамилия", "Имя"], ConverterEngine.MergePlaceholders("{Фамилия} {Имя}"));
    }

    [Fact]
    public void DateInRange_IsTwoThousandToTwoThousandNinetyNine()
    {
        Assert.True(ConverterEngine.DateInRange(new DateOnly(2000, 1, 1)));
        Assert.False(ConverterEngine.DateInRange(new DateOnly(1999, 12, 31)));
        Assert.False(ConverterEngine.DateInRange(new DateOnly(2100, 1, 1)));
    }

    [Fact]
    public void Catalogue_TypedFieldsFollowTheTypeFieldSets()
    {
        var risk = ImportFieldCatalogue.Find(ImportFieldCatalogue.Risk)!;
        var effort = ImportFieldCatalogue.Find(ImportFieldCatalogue.Effort)!;

        Assert.True(ImportFieldCatalogue.IsOnType(risk, AppearanceToken.Epic));
        Assert.Equal(AppearanceToken.Epic.Has(WorkItemField.Effort), ImportFieldCatalogue.IsOnType(effort, AppearanceToken.Epic));
        Assert.Equal(25, ImportFieldCatalogue.All.Count);
        Assert.Equal(["project", "type", "title"], ImportFieldCatalogue.All.Where(f => f.Required).Select(f => f.Id));
    }
}
