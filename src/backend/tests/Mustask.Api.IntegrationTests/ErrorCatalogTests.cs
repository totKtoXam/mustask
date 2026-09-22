using Mustask.Api.ErrorHandling.Catalog;
using Mustask.Shared.Errors;

namespace Mustask.Api.IntegrationTests;

/// <summary>
/// Неизменяемость <see cref="ErrorCatalog"/>
/// должна быть обеспечена типом (<c>FrozenDictionary</c>), а не
/// договорённостью не мутировать источник после конструктора.
/// </summary>
[Trait("Category", "Integration")]
public sealed class ErrorCatalogTests
{
    private static ErrorCatalogEntry Entry(string code) => new(
        code,
        "orders",
        ErrorCatalogOrigin.Slice,
        [409],
        new Dictionary<string, ErrorParamType>(),
        "Квота исчерпана.",
        "Квота таусылды.",
        Deprecated: false,
        KkNeedsReview: false);

    [Fact]
    public void Constructor_GivenSourceDictionaryMutatedAfterConstruction_DoesNotSeeTheMutation()
    {
        var source = new Dictionary<string, ErrorCatalogEntry>(StringComparer.Ordinal)
        {
            ["orders.limit_exhausted"] = Entry("orders.limit_exhausted"),
        };

        var catalog = new ErrorCatalog(source);

        // Мутация словаря-источника после конструктора — ровно то, что делал
        // бы вызывающий код, полагаясь на возвращаемый ссылочный тип.
        source["orders.leaked_after_construction"] = Entry("orders.leaked_after_construction");
        source.Remove("orders.limit_exhausted");

        Assert.True(catalog.TryGet("orders.limit_exhausted", out _));
        Assert.False(catalog.TryGet("orders.leaked_after_construction", out _));
    }

    [Fact]
    public void TryGet_GivenCodeInDifferentCase_ReturnsFalse()
    {
        // Компаратор ключей — явно Ordinal, а не унаследован от того,
        // с чем построили источник (например, OrdinalIgnoreCase).
        var source = new Dictionary<string, ErrorCatalogEntry>(StringComparer.Ordinal)
        {
            ["orders.limit_exhausted"] = Entry("orders.limit_exhausted"),
        };

        var catalog = new ErrorCatalog(source);

        Assert.False(catalog.TryGet("ORDERS.LIMIT_EXHAUSTED", out _));
    }
}
