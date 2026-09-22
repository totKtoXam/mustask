using Mustask.Shared.Errors;

namespace Mustask.Shared.Tests;

/// <summary>Причина ошибки поля — обычный код реестра, а не текст.</summary>
public sealed class FieldErrorTests
{
    [Fact]
    public void Constructor_GivenCodeAndParams_ExposesThem()
    {
        var parameters = new ErrorParams { ["date"] = ErrorParam.Date(new DateOnly(2026, 1, 1)) };

        var fieldError = new FieldError("orders.date_in_past", parameters);

        Assert.Equal("orders.date_in_past", fieldError.Code);
        Assert.Same(parameters, fieldError.Params);
    }

    [Fact]
    public void Constructor_GivenOnlyCode_DefaultsToEmptyParams()
    {
        var fieldError = new FieldError("orders.required");

        Assert.Equal(0, fieldError.Params.Count);
    }
}
