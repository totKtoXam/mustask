using Mustask.Api.ErrorHandling.Catalog;
using Mustask.Shared.Errors;

namespace Mustask.Api.IntegrationTests;

/// <summary>
/// Разбор каталога из сырого JSON (без Docker, без хоста); подключение
/// каталога к обработчикам проверяется отдельно.
/// </summary>
[Trait("Category", "Integration")]
public sealed class ErrorCatalogLoaderTests
{
    private static ErrorCatalogSourceFile SingleCodeFile(
        string module = "orders",
        string code = "orders.limit_exhausted",
        string statuses = "[409]",
        string paramsJson = """{ "used": "number", "limit": "number" }""",
        string ru = "Квота исчерпана: {used} из {limit}.",
        string kk = "Квота таусылды: {limit} ішінен {used}.")
    {
        var json = $$"""
            {
              "module": "{{module}}",
              "origin": "slice",
              "codes": {
                "{{code}}": {
                  "statuses": {{statuses}},
                  "params": {{paramsJson}},
                  "ru": "{{ru}}",
                  "kk": "{{kk}}"
                }
              }
            }
            """;

        return new ErrorCatalogSourceFile("test.json", json);
    }

    [Fact]
    public void Load_GivenValidFile_ParsesEntry()
    {
        var catalog = ErrorCatalogLoader.Load([SingleCodeFile()]);

        var entry = catalog.Get("orders.limit_exhausted");

        Assert.Equal("orders", entry.Module);
        Assert.Equal(ErrorCatalogOrigin.Slice, entry.Origin);
        Assert.Equal([409], entry.Statuses);
        Assert.Equal(ErrorParamType.Number, entry.Params["used"]);
        Assert.False(entry.Deprecated);
        Assert.False(entry.KkNeedsReview);
    }

    [Fact]
    public void Load_GivenSameCodeInTwoFiles_Throws()
    {
        var first = SingleCodeFile();
        var second = SingleCodeFile();

        var exception = Assert.Throws<InvalidOperationException>(() => ErrorCatalogLoader.Load([first, second]));

        Assert.Contains("orders.limit_exhausted", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Load_GivenUnknownParamTypeHint_Throws()
    {
        var file = SingleCodeFile(paramsJson: """{ "used": "percentage" }""");

        var exception = Assert.Throws<InvalidOperationException>(() => ErrorCatalogLoader.Load([file]));

        Assert.Contains("percentage", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>Раньше — голый InvalidOperationException от JsonElement.GetString() без имени файла и кода.</summary>
    [Fact]
    public void Load_GivenNonStringParamTypeHint_ThrowsWithFileNameAndCodeInMessage()
    {
        var file = SingleCodeFile(paramsJson: """{ "used": 5 }""");

        var exception = Assert.Throws<InvalidOperationException>(() => ErrorCatalogLoader.Load([file]));

        Assert.Contains("test.json", exception.Message, StringComparison.Ordinal);
        Assert.Contains("orders.limit_exhausted", exception.Message, StringComparison.Ordinal);
        Assert.Contains("used", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Load_GivenEmptyRuText_Throws(string ru)
    {
        var file = SingleCodeFile(ru: ru);

        Assert.Throws<InvalidOperationException>(() => ErrorCatalogLoader.Load([file]));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Load_GivenEmptyKkText_Throws(string kk)
    {
        var file = SingleCodeFile(kk: kk);

        Assert.Throws<InvalidOperationException>(() => ErrorCatalogLoader.Load([file]));
    }

    [Fact]
    public void Load_GivenNoFiles_Throws()
    {
        Assert.Throws<InvalidOperationException>(() => ErrorCatalogLoader.Load([]));
    }

    [Fact]
    public void Load_GivenUnknownOrigin_Throws()
    {
        var json = """
            {
              "module": "orders",
              "origin": "unknown",
              "codes": {}
            }
            """;

        Assert.Throws<InvalidOperationException>(
            () => ErrorCatalogLoader.Load([new ErrorCatalogSourceFile("bad.json", json)]));
    }

    [Fact]
    public void Get_GivenUnknownCode_Throws()
    {
        var catalog = ErrorCatalogLoader.Load([SingleCodeFile()]);

        Assert.Throws<KeyNotFoundException>(() => catalog.Get("orders.no_such_code"));
    }

    [Fact]
    public void TryGet_GivenUnknownCode_ReturnsFalse()
    {
        var catalog = ErrorCatalogLoader.Load([SingleCodeFile()]);

        Assert.False(catalog.TryGet("orders.no_such_code", out _));
    }

    /// <summary>Битый JSON раньше давал исключение только с LineNumber/BytePositionInLine, без имени файла.</summary>
    [Fact]
    public void Load_GivenMalformedJson_ThrowsWithFileNameInMessage()
    {
        var json = "{ this is not valid json ";

        var exception = Assert.Throws<InvalidOperationException>(
            () => ErrorCatalogLoader.Load([new ErrorCatalogSourceFile("broken.json", json)]));

        Assert.Contains("broken.json", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>Раньше — голый InvalidOperationException от JsonElement.GetInt32() без имени файла и кода.</summary>
    [Fact]
    public void Load_GivenNonNumericStatus_ThrowsWithFileNameAndCodeInMessage()
    {
        var file = SingleCodeFile(statuses: """["409"]""");

        var exception = Assert.Throws<InvalidOperationException>(() => ErrorCatalogLoader.Load([file]));

        Assert.Contains("test.json", exception.Message, StringComparison.Ordinal);
        Assert.Contains("orders.limit_exhausted", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>Раньше — голый InvalidOperationException от JsonElement.GetBoolean() без имени файла и кода.</summary>
    [Fact]
    public void Load_GivenNonBooleanDeprecated_ThrowsWithFileNameAndCodeInMessage()
    {
        var json = """
            {
              "module": "orders",
              "origin": "slice",
              "codes": {
                "orders.limit_exhausted": {
                  "statuses": [409],
                  "params": {},
                  "ru": "Квота исчерпана.",
                  "kk": "Квота таусылды.",
                  "deprecated": "yes"
                }
              }
            }
            """;

        var exception = Assert.Throws<InvalidOperationException>(
            () => ErrorCatalogLoader.Load([new ErrorCatalogSourceFile("deprecated-bad.json", json)]));

        Assert.Contains("deprecated-bad.json", exception.Message, StringComparison.Ordinal);
        Assert.Contains("orders.limit_exhausted", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>Та же безымянная ветка, что и у "deprecated", но для "kkNeedsReview".</summary>
    [Fact]
    public void Load_GivenNonBooleanKkNeedsReview_ThrowsWithFileNameAndCodeInMessage()
    {
        var json = """
            {
              "module": "orders",
              "origin": "slice",
              "codes": {
                "orders.limit_exhausted": {
                  "statuses": [409],
                  "params": {},
                  "ru": "Квота исчерпана.",
                  "kk": "Квота таусылды.",
                  "kkNeedsReview": "yes"
                }
              }
            }
            """;

        var exception = Assert.Throws<InvalidOperationException>(
            () => ErrorCatalogLoader.Load([new ErrorCatalogSourceFile("kkreview-bad.json", json)]));

        Assert.Contains("kkreview-bad.json", exception.Message, StringComparison.Ordinal);
        Assert.Contains("orders.limit_exhausted", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>Дубль ключа внутри одной записи раньше проглатывался молча — побеждала последняя.</summary>
    [Fact]
    public void Load_GivenDuplicateKeyInsideEntry_ThrowsWithFileNameInMessage()
    {
        var json = """
            {
              "module": "orders",
              "origin": "slice",
              "codes": {
                "orders.limit_exhausted": {
                  "statuses": [409],
                  "params": {},
                  "ru": "Первый текст.",
                  "ru": "Второй текст.",
                  "kk": "Квота таусылды."
                }
              }
            }
            """;

        var exception = Assert.Throws<InvalidOperationException>(
            () => ErrorCatalogLoader.Load([new ErrorCatalogSourceFile("dup-key.json", json)]));

        Assert.Contains("dup-key.json", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// `additionalProperties: false` из
    /// `schema.json` ничем не проверялся — опечатка вроде `"depricated"`
    /// принималась молча, и код оставался «живым».
    /// </summary>
    [Fact]
    public void Load_GivenUnknownFieldInEntry_ThrowsWithFileNameAndCodeInMessage()
    {
        var json = """
            {
              "module": "orders",
              "origin": "slice",
              "codes": {
                "orders.limit_exhausted": {
                  "statuses": [409],
                  "params": {},
                  "ru": "Квота исчерпана.",
                  "kk": "Квота таусылды.",
                  "depricated": true
                }
              }
            }
            """;

        var exception = Assert.Throws<InvalidOperationException>(
            () => ErrorCatalogLoader.Load([new ErrorCatalogSourceFile("typo.json", json)]));

        Assert.Contains("typo.json", exception.Message, StringComparison.Ordinal);
        Assert.Contains("orders.limit_exhausted", exception.Message, StringComparison.Ordinal);
        Assert.Contains("depricated", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>Тот же белый список полей действует и на верхнем уровне файла, не только внутри записи кода.</summary>
    [Fact]
    public void Load_GivenUnknownRootField_ThrowsWithFileNameInMessage()
    {
        var json = """
            {
              "module": "orders",
              "origin": "slice",
              "codes": {},
              "extra": true
            }
            """;

        var exception = Assert.Throws<InvalidOperationException>(
            () => ErrorCatalogLoader.Load([new ErrorCatalogSourceFile("root-typo.json", json)]));

        Assert.Contains("root-typo.json", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Entries_ExposesDeprecatedAndKkNeedsReviewFlags()
    {
        var json = """
            {
              "module": "http",
              "origin": "platform",
              "codes": {
                "http.legacy": {
                  "statuses": [410],
                  "params": {},
                  "ru": "Устаревший код.",
                  "kk": "Ескірген код.",
                  "deprecated": true,
                  "kkNeedsReview": true
                }
              }
            }
            """;

        var catalog = ErrorCatalogLoader.Load([new ErrorCatalogSourceFile("http.json", json)]);
        var entry = catalog.Get("http.legacy");

        Assert.True(entry.Deprecated);
        Assert.True(entry.KkNeedsReview);
    }
}
