using Mustask.Shared.Errors;

namespace Mustask.Shared.Tests;

/// <summary>
/// Закрытый набор подсказок типа, значение и тип связаны фабричным
/// методом и не могут рассинхронизироваться.
/// </summary>
public sealed class ErrorParamTests
{
    [Fact]
    public void Text_GivenValue_SetsValueAndType()
    {
        var param = ErrorParam.Text("CN");

        Assert.Equal("CN", param.Value);
        Assert.Equal(ErrorParamType.Text, param.Type);
    }

    [Fact]
    public void Text_GivenNull_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => ErrorParam.Text(null!));
    }

    /// <summary>Пустой ввод — валидное бизнес-значение, не отказ.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Text_GivenEmptyOrWhitespace_ReturnsValueAsIs(string value)
    {
        var param = ErrorParam.Text(value);

        Assert.Equal(value, param.Value);
        Assert.Equal(ErrorParamType.Text, param.Type);
    }

    /// <summary>CR/LF в значении не должны попасть в лог одной строкой (CWE-117).</summary>
    [Fact]
    public void Text_GivenControlCharacters_StripsThem()
    {
        var param = ErrorParam.Text("CN\r\n[FORGED] extra\tline");

        Assert.Equal("CN[FORGED] extraline", param.Value);
    }

    [Fact]
    public void Number_GivenValue_SetsValueAndType()
    {
        var param = ErrorParam.Number(50m);

        Assert.Equal(50m, param.Value);
        Assert.Equal(ErrorParamType.Number, param.Type);
    }

    [Fact]
    public void Money_GivenValue_SetsCurrencyType()
    {
        var param = ErrorParam.Money(12500m);

        Assert.Equal(12500m, param.Value);
        Assert.Equal(ErrorParamType.Currency, param.Type);
    }

    [Fact]
    public void Date_GivenValue_SetsValueAndType()
    {
        var date = new DateOnly(2026, 1, 12);

        var param = ErrorParam.Date(date);

        Assert.Equal(date, param.Value);
        Assert.Equal(ErrorParamType.Date, param.Type);
    }

    [Fact]
    public void Moment_GivenValue_SetsValueAndType()
    {
        var moment = new DateTimeOffset(2026, 1, 12, 10, 0, 0, TimeSpan.Zero);

        var param = ErrorParam.Moment(moment);

        Assert.Equal(moment, param.Value);
        Assert.Equal(ErrorParamType.DateTime, param.Type);
    }

    [Fact]
    public void Equals_GivenSameValueAndType_ReturnsTrue()
    {
        Assert.Equal(ErrorParam.Number(50m), ErrorParam.Number(50m));
    }

    [Fact]
    public void Equals_GivenSameValueDifferentType_ReturnsFalse()
    {
        Assert.NotEqual(ErrorParam.Number(50m), ErrorParam.Money(50m));
    }
}
