using Mustask.Shared.Errors;
using Mustask.Shared.Exceptions;

namespace Mustask.Shared.Tests;

/// <summary>
/// Контракт каждого наследника <see cref="MustaskException"/>: конструктор
/// корректно проставляет <see cref="MustaskException.Code"/>,
/// <see cref="MustaskException.StatusCode"/> и <see cref="MustaskException.Params"/>.
/// <see cref="Exception.Message"/> больше не текст для показа (ADR-0006) —
/// это диагностический след, проверяется отдельно
/// (<see cref="Message_ContainsCodeAndParamsForDiagnostics"/>).
/// </summary>
public sealed class MustaskExceptionSubclassesTests
{
    [Fact]
    public void NotFoundException_Constructed_SetsCodeAndStatus404()
    {
        var exception = new NotFoundException("orders.not_found");

        Assert.Equal("orders.not_found", exception.Code);
        Assert.Equal(404, exception.StatusCode);
        Assert.Equal(0, exception.Params.Count);
    }

    [Fact]
    public void ConflictException_Constructed_SetsCodeAndStatus409()
    {
        var exception = new ConflictException("orders.already_submitted");

        Assert.Equal("orders.already_submitted", exception.Code);
        Assert.Equal(409, exception.StatusCode);
    }

    [Fact]
    public void ConflictException_ConstructedWithParams_ExposesThem()
    {
        var exception = new ConflictException("orders.limit_exhausted", new ErrorParams
        {
            ["used"] = ErrorParam.Number(50m),
            ["limit"] = ErrorParam.Number(50m),
        });

        Assert.Equal(ErrorParam.Number(50m), exception.Params["used"]);
        Assert.Equal(ErrorParam.Number(50m), exception.Params["limit"]);
    }

    [Fact]
    public void ForbiddenException_Constructed_SetsCodeAndStatus403()
    {
        var exception = new ForbiddenException("orders.forbidden");

        Assert.Equal("orders.forbidden", exception.Code);
        Assert.Equal(403, exception.StatusCode);
    }

    [Fact]
    public void UnauthorizedException_Constructed_SetsCodeAndStatus401()
    {
        var exception = new UnauthorizedException("auth.unauthorized");

        Assert.Equal("auth.unauthorized", exception.Code);
        Assert.Equal(401, exception.StatusCode);
    }

    [Fact]
    public void ValidationException_Constructed_SetsCodeAndStatus400()
    {
        var errors = new Dictionary<string, IReadOnlyList<FieldError>>
        {
            ["name"] = [new FieldError("orders.required")],
        };

        var exception = new ValidationException("orders.validation_failed", errors);

        Assert.Equal("orders.validation_failed", exception.Code);
        Assert.Equal(400, exception.StatusCode);
    }

    [Fact]
    public void Constructor_GivenInnerException_SetsInnerException()
    {
        var inner = new InvalidOperationException("исходная причина");

        var exception = new ConflictException("orders.conflict", innerException: inner);

        Assert.Same(inner, exception.InnerException);
    }

    /// <summary>
    /// <c>Message</c> — диагностический след (код + параметры,
    /// инвариантная культура), не текст для показа. Он печатается везде, где
    /// исключение всплывает вне нашего обработчика, поэтому не должен быть
    /// пустым или голым GUID.
    /// </summary>
    [Fact]
    public void Message_ContainsCodeAndParamsForDiagnostics()
    {
        var exception = new ConflictException("orders.limit_exhausted", new ErrorParams
        {
            ["used"] = ErrorParam.Number(50m),
        });

        Assert.Contains("orders.limit_exhausted", exception.Message, StringComparison.Ordinal);
        Assert.Contains("used=50", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Message_GivenNoParams_IsJustTheCode()
    {
        var exception = new NotFoundException("orders.not_found");

        Assert.Equal("orders.not_found", exception.Message);
    }
}
