using Mustask.Shared.Errors;

namespace Mustask.Shared.Tests;

public sealed class ErrorParamsTests
{
    [Fact]
    public void CollectionInitializer_GivenIndexerAssignments_PopulatesValues()
    {
        var parameters = new ErrorParams
        {
            ["used"] = ErrorParam.Number(50m),
            ["limit"] = ErrorParam.Number(50m),
            ["country"] = ErrorParam.Text("CN"),
        };

        Assert.Equal(3, parameters.Count);
        Assert.Equal(ErrorParam.Number(50m), parameters["used"]);
        Assert.Equal(ErrorParam.Text("CN"), parameters["country"]);
    }

    [Fact]
    public void None_ReturnsEmptySet()
    {
        Assert.Equal(0, ErrorParams.None.Count);
    }

    [Fact]
    public void None_CalledTwice_ReturnsIndependentInstances()
    {
        // Ergo: None не статический синглтон — мутация одного вызова не
        // должна просачиваться в другой.
        var first = ErrorParams.None;
        first["leaked"] = ErrorParam.Text("оops");

        var second = ErrorParams.None;

        Assert.Equal(0, second.Count);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Indexer_GivenNullOrWhitespaceName_Throws(string? name)
    {
        var parameters = new ErrorParams();

        Assert.ThrowsAny<ArgumentException>(() => parameters[name!] = ErrorParam.Number(1m));
    }

    /// <summary>
    /// Рендерер (<c>ErrorTextRenderer</c>)
    /// подставляет только имена вида <c>[A-Za-z0-9_]+</c> — имя вне этого
    /// набора отклоняется на границе, а не молча остаётся неподставленным
    /// плейсхолдером у пользователя.
    /// </summary>
    [Theory]
    [InlineData("with-dash")]
    [InlineData("with space")]
    [InlineData("кириллица")]
    [InlineData("with.dot")]
    public void Indexer_GivenNameWithCharactersOutsidePlaceholderSet_Throws(string name)
    {
        var parameters = new ErrorParams();

        Assert.Throws<ArgumentException>(() => parameters[name] = ErrorParam.Number(1m));
    }

    [Fact]
    public void GetEnumerator_YieldsAllEntries()
    {
        var parameters = new ErrorParams
        {
            ["a"] = ErrorParam.Number(1m),
            ["b"] = ErrorParam.Number(2m),
        };

        var names = parameters.Select(kvp => kvp.Key).OrderBy(name => name, StringComparer.Ordinal).ToList();

        Assert.Equal(["a", "b"], names);
    }

    [Fact]
    public void TryGetValue_GivenMissingName_ReturnsFalse()
    {
        var parameters = new ErrorParams();

        Assert.False(parameters.TryGetValue("missing", out _));
    }
}
