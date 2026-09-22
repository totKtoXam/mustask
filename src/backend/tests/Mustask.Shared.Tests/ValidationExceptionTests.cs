using Mustask.Shared.Errors;
using Mustask.Shared.Exceptions;

namespace Mustask.Shared.Tests;

public sealed class ValidationExceptionTests
{
    [Fact]
    public void Errors_GivenFieldErrors_ExposesThem()
    {
        var errors = new Dictionary<string, IReadOnlyList<FieldError>>
        {
            ["name"] = [new FieldError("orders.required"), new FieldError("orders.too_long")],
            ["email"] = [new FieldError("orders.invalid_format")],
        };

        var exception = new ValidationException("orders.validation_failed", errors);

        Assert.Equal(["orders.required", "orders.too_long"], exception.Errors["name"].Select(e => e.Code));
        Assert.Equal(["orders.invalid_format"], exception.Errors["email"].Select(e => e.Code));
    }

    [Fact]
    public void Errors_GivenFieldErrorWithParams_ExposesThem()
    {
        var errors = new Dictionary<string, IReadOnlyList<FieldError>>
        {
            ["startDate"] = [new FieldError(
                "orders.date_in_past",
                new ErrorParams { ["date"] = ErrorParam.Date(new DateOnly(2026, 1, 1)) })],
        };

        var exception = new ValidationException("orders.validation_failed", errors);

        Assert.Equal(
            ErrorParam.Date(new DateOnly(2026, 1, 1)),
            exception.Errors["startDate"][0].Params["date"]);
    }

    [Fact]
    public void Errors_SourceDictionaryMutatedAfterConstruction_DoesNotAffectException()
    {
        var source = new Dictionary<string, IReadOnlyList<FieldError>>
        {
            ["name"] = [new FieldError("orders.required")],
        };

        var exception = new ValidationException("orders.validation_failed", source);
        source["name"] = [new FieldError("orders.substituted")];
        source["extra"] = [new FieldError("orders.added_after")];

        Assert.Equal(["orders.required"], exception.Errors["name"].Select(e => e.Code));
        Assert.False(exception.Errors.ContainsKey("extra"));
    }

    [Fact]
    public void Errors_MutatingReturnedDictionary_DoesNotAffectException()
    {
        var exception = new ValidationException(
            "orders.validation_failed",
            new Dictionary<string, IReadOnlyList<FieldError>> { ["name"] = [new FieldError("orders.required")] });

        var firstAccess = exception.Errors;
        var secondAccess = exception.Errors;

        Assert.NotSame(firstAccess, secondAccess);

        if (firstAccess is IDictionary<string, IReadOnlyList<FieldError>> mutable)
        {
            mutable["name"] = [new FieldError("orders.substituted")];
            mutable["extra"] = [new FieldError("orders.added")];
        }

        Assert.Equal(["orders.required"], exception.Errors["name"].Select(e => e.Code));
        Assert.False(exception.Errors.ContainsKey("extra"));
    }

    [Fact]
    public void Constructor_GivenEmptyErrorsDictionary_ExposesEmptyErrors()
    {
        var exception = new ValidationException(
            "orders.validation_failed",
            new Dictionary<string, IReadOnlyList<FieldError>>());

        Assert.Empty(exception.Errors);
    }

    [Fact]
    public void Constructor_GivenNullErrorsArgument_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => new ValidationException(
            "orders.validation_failed",
            null!));
    }

    /// <summary>
    /// Тот же дефект, что был исправлен для старой формы
    /// (<c>string[]</c>): <c>null</c> вместо списка причин не должен молча
    /// сохраняться — отвергается на границе.
    /// </summary>
    [Fact]
    public void Constructor_GivenFieldWithNullFieldErrorsList_Throws()
    {
        var errors = new Dictionary<string, IReadOnlyList<FieldError>>
        {
            ["name"] = null!,
        };

        var exception = Assert.Throws<ArgumentException>(() => new ValidationException(
            "orders.validation_failed", errors));
        Assert.Equal("errors", exception.ParamName);
    }

    /// <summary>
    /// Тот же дефект, что был исправлен для старой формы:
    /// раньше копировался только словарь, а массив-значение оставался общим
    /// с внутренним состоянием исключения. Для новой формы — тот же тест,
    /// но с массивом <see cref="FieldError"/> вместо <c>string[]</c>.
    /// </summary>
    [Fact]
    public void Errors_MutatingArrayReturnedByAccess_DoesNotAffectException()
    {
        var exception = new ValidationException(
            "orders.validation_failed",
            new Dictionary<string, IReadOnlyList<FieldError>> { ["name"] = [new FieldError("orders.original")] });

        var array = (FieldError[])exception.Errors["name"];
        array[0] = new FieldError("orders.tampered");

        Assert.Equal("orders.original", exception.Errors["name"][0].Code);
    }

    [Fact]
    public void Constructor_GivenSourceListMutatedAfterConstruction_DoesNotAffectException()
    {
        var fieldErrors = new List<FieldError> { new("orders.original") };
        var errors = new Dictionary<string, IReadOnlyList<FieldError>> { ["name"] = fieldErrors };

        var exception = new ValidationException("orders.validation_failed", errors);
        fieldErrors[0] = new FieldError("orders.tampered");

        Assert.Equal("orders.original", exception.Errors["name"][0].Code);
    }
}
