using System.Text.Json;
using Mustask.Domain.Imports;
using Mustask.Domain.Imports.Conversion;
using Mustask.Domain.Imports.Templates;
using Xunit;

namespace Mustask.Domain.Tests.Imports;

public sealed class TemplateValidatorTests
{
    private static readonly string[] Headers = ["Проект", "Тип", "Заголовок", "Статус"];

    private static FieldMapping Col(string field, string column, params ChainStepSettings[] chain) =>
        new() { Id = Guid.NewGuid(), Field = field, Source = new MappingSource { Kind = TemplateTokens.SourceColumn, Column = column }, Chain = chain };

    private static TemplateSettings Valid() => new()
    {
        Source = new TemplateSource { HeaderRow = 1, FirstDataRow = 2, Headers = Headers },
        Mappings = [Col("project", "Проект"), Col("type", "Тип"), Col("title", "Заголовок")],
    };

    private static ChainStepSettings Step(string converter, string json = "{}") =>
        new() { Converter = converter, Params = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json) };

    private static bool Has(Dictionary<string, List<Mustask.Shared.Errors.FieldError>> errors, string key, string code) =>
        errors.TryGetValue(key, out var list) && list.Any(e => e.Code == code);

    [Fact]
    public void Validate_ValidTemplate_NoErrors() => Assert.Empty(TemplateValidator.Validate("Реестр", Valid()));

    [Theory]
    [InlineData("", ImportErrorCodes.TemplateNameRequired)]
    [InlineData("   ", ImportErrorCodes.TemplateNameRequired)]
    public void Validate_BadName_Reported(string name, string code) =>
        Assert.True(Has(TemplateValidator.Validate(name, Valid()), "name", code));

    [Fact]
    public void Validate_NameOver100AfterTrim_TooLong() =>
        Assert.True(Has(TemplateValidator.Validate(new string('я', 101), Valid()), "name", ImportErrorCodes.TemplateNameTooLong));

    [Fact]
    public void Validate_FirstDataRowNotBelowHeader_Reported()
    {
        var settings = Valid() with { Source = Valid().Source with { HeaderRow = 3, FirstDataRow = 3 } };
        Assert.True(Has(TemplateValidator.Validate("a", settings), "settings.source", ImportErrorCodes.FirstDataRowInvalid));
    }

    [Fact]
    public void Validate_HeaderRowOutOfRange_Reported()
    {
        var settings = Valid() with { Source = Valid().Source with { HeaderRow = 51, FirstDataRow = 60 } };
        Assert.True(Has(TemplateValidator.Validate("a", settings), "settings.source", ImportErrorCodes.HeaderRowInvalid));
    }

    [Fact]
    public void Validate_MissingRequiredField_SourceRequired()
    {
        var settings = Valid() with { Mappings = [Col("project", "Проект"), Col("type", "Тип")] };
        Assert.True(Has(TemplateValidator.Validate("a", settings), "settings.mappings", ImportErrorCodes.MappingSourceRequired));
    }

    [Fact]
    public void Validate_UnknownColumn_AndHeaderCaseInsensitive()
    {
        var ok = Valid() with { Mappings = [Col("project", " проект "), Col("type", "Тип"), Col("title", "Заголовок")] };
        Assert.Empty(TemplateValidator.Validate("a", ok));

        var bad = Valid() with { Mappings = [Col("project", "Проект"), Col("type", "Тип"), Col("title", "Нет такого")] };
        Assert.True(Has(TemplateValidator.Validate("a", bad), "settings.mappings[2].source", ImportErrorCodes.MappingColumnUnknown));
    }

    [Fact]
    public void Validate_FieldTakenAndUnknown_Reported()
    {
        var settings = Valid() with { Mappings = [.. Valid().Mappings, Col("title", "Статус"), Col("nope", "Статус")] };
        var errors = TemplateValidator.Validate("a", settings);

        Assert.True(Has(errors, "settings.mappings[3].field", ImportErrorCodes.MappingFieldTaken));
        Assert.True(Has(errors, "settings.mappings[4].field", ImportErrorCodes.MappingFieldUnknown));
    }

    [Fact]
    public void Validate_ChainOver10_TooLong()
    {
        var chain = Enumerable.Range(0, 11).Select(_ => Step("trim")).ToArray();
        var settings = Valid() with { Mappings = [Col("project", "Проект"), Col("type", "Тип"), Col("title", "Заголовок", chain)] };
        Assert.True(Has(TemplateValidator.Validate("a", settings), "settings.mappings[2].chain", ImportErrorCodes.MappingChainTooLong));
    }

    [Fact]
    public void Validate_SplitIntoSingleValuedField_ListIntoSingle()
    {
        var settings = Valid() with { Mappings = [Col("project", "Проект"), Col("type", "Тип"), Col("title", "Заголовок", Step("split", "{\"delimiter\":\",\"}"))] };
        Assert.True(Has(TemplateValidator.Validate("a", settings), "settings.mappings[2].chain", ImportErrorCodes.MappingListIntoSingle));
    }

    [Fact]
    public void Validate_BadConverterParams_Reported()
    {
        var chain = new[] { Step("truncate", "{\"length\":0}"), Step("dateFormat", "{\"format\":\"x\"}"), Step("extract", "{\"pattern\":\"(\"}"), Step("bogus") };
        var settings = Valid() with { Mappings = [Col("project", "Проект"), Col("type", "Тип"), Col("title", "Заголовок", chain)] };
        var errors = TemplateValidator.Validate("a", settings)["settings.mappings[2].chain"];

        Assert.Equal(2, errors.Count(e => e.Code == ImportErrorCodes.MappingConverterParamInvalid));
        Assert.Contains(errors, e => e.Code == ImportErrorCodes.MappingPatternInvalid);
        Assert.Contains(errors, e => e.Code == ImportErrorCodes.MappingConverterUnknown);
    }

    [Fact]
    public void Validate_ClearOnFieldWithoutClear_NotAllowed()
    {
        var m = Col("title", "Заголовок") with { EmptyCell = TemplateTokens.EmptyClear };
        var settings = Valid() with { Mappings = [Col("project", "Проект"), Col("type", "Тип"), m] };
        Assert.True(Has(TemplateValidator.Validate("a", settings), "settings.mappings[2].emptyCell", ImportErrorCodes.MappingClearNotAllowed));
    }

    [Fact]
    public void Validate_MergePlaceholderNotInHeaders_ColumnUnknown()
    {
        var merge = new FieldMapping { Id = Guid.NewGuid(), Field = "title", Source = new MappingSource { Kind = TemplateTokens.SourceMerge, Pattern = "{Проект} / {Модуль}" } };
        var settings = Valid() with { Mappings = [Col("project", "Проект"), Col("type", "Тип"), merge] };
        Assert.True(Has(TemplateValidator.Validate("a", settings), "settings.mappings[2].source", ImportErrorCodes.MappingColumnUnknown));
    }

    [Fact]
    public void Validate_Recoding_PolicyRules()
    {
        var state = Col("state", "Статус");
        var project = Col("project", "Проект");
        var settings = Valid() with
        {
            Mappings = [project, Col("type", "Тип"), Col("title", "Заголовок"), state],
            Recoding =
            [
                new RecodingTableSettings { MappingId = project.Id, Policy = TemplateTokens.PolicyEmpty },
                new RecodingTableSettings { MappingId = state.Id, Policy = TemplateTokens.PolicyDefault },
                new RecodingTableSettings { MappingId = state.Id, Policy = TemplateTokens.PolicyError, Entries = [new RecodingEntrySettings { FileValue = "a", Target = "not-a-guid" }] },
            ],
        };
        var errors = TemplateValidator.Validate("a", settings);

        Assert.True(Has(errors, "settings.recoding[0]", ImportErrorCodes.RecodingEmptyNotAllowed));
        Assert.True(Has(errors, "settings.recoding[1]", ImportErrorCodes.RecodingDefaultRequired));
        Assert.True(Has(errors, "settings.recoding[2]", ImportErrorCodes.RecodingTargetUnknown));
    }

    [Fact]
    public void Validate_RecodingOver1000Entries_TooMany()
    {
        var state = Col("state", "Статус");
        var entries = Enumerable.Range(0, 1001).Select(i => new RecodingEntrySettings { FileValue = "v" + i, Target = Guid.NewGuid().ToString() }).ToList();
        var settings = Valid() with { Mappings = [.. Valid().Mappings, state], Recoding = [new RecodingTableSettings { MappingId = state.Id, Entries = entries }] };
        Assert.True(Has(TemplateValidator.Validate("a", settings), "settings.recoding[0]", ImportErrorCodes.RecodingTooManyEntries));
    }

    [Fact]
    public void Settings_RoundTripJson_KeepsIgnoredColumns()
    {
        var settings = Valid() with { IgnoredColumns = ["Идентификатор"] };
        var back = TemplateJson.Deserialize(TemplateJson.Serialize(settings));

        Assert.Equal(["Идентификатор"], back.IgnoredColumns);
        Assert.Equal(3, back.Mappings.Count);
    }

    [Fact]
    public void CopyName_NumbersWhileTaken_AndCutsTo100()
    {
        var taken = new HashSet<string> { ImportTemplate.KeyOf("Копия Реестр"), ImportTemplate.KeyOf("Копия Реестр 2") };
        Assert.Equal("Копия Реестр 3", ImportTemplate.CopyName("Реестр", taken.Contains));
        Assert.Equal(100, ImportTemplate.CopyName(new string('я', 100), _ => false).Length);
    }

    [Fact]
    public void Hyperlink_UsesLinkThenHttpText()
    {
        var chain = new[] { new ConverterStep(ConverterIds.Hyperlink) };
        Assert.Equal("https://x.example/a", ConverterEngine.RunChain(ConversionValue.OfText("Документ"), chain, "https://x.example/a").Value.Display());
        Assert.Equal("https://x.example/b", ConverterEngine.RunChain(ConversionValue.OfText("https://x.example/b"), chain).Value.Display());
        Assert.True(ConverterEngine.RunChain(ConversionValue.OfText("Документ"), chain).Value.IsEmpty);
    }

    [Fact]
    public void LookupTable_MissPolicies()
    {
        KeyValuePair<string, string>[] table = [new("S", "2"), new("M", "8")];
        ConversionValue Run(string v, string policy) =>
            ConverterEngine.RunChain(ConversionValue.OfText(v), [new ConverterStep(ConverterIds.Lookup, Entries: table, Policy: policy, Default: "5")]).Value;

        Assert.Equal("8", Run(" m ", "error").Display());
        Assert.Equal("5", Run("L", "default").Display());
        Assert.True(Run("L", "empty").IsEmpty);
        Assert.False(ConverterEngine.RunChain(ConversionValue.OfText("L"), [new ConverterStep(ConverterIds.Lookup, Entries: table, Policy: "error")]).Ok);
    }
}
