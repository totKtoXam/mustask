using Mustask.Shared.Exceptions;

namespace Mustask.Shared.Tests;

/// <summary>
/// Базовый конструктор <see cref="MustaskException"/> валидирует <c>code</c> и
/// диапазон <c>statusCode</c> (дефект, исправленный ранее).
/// Проверка <c>message</c> из этого файла ушла вместе с параметром — с
/// ADR-0006 текст для показа больше не аргумент конструктора:
/// <c>Message</c> строится диагностически из кода и параметров.
/// </summary>
public sealed class MustaskExceptionConstructorValidationTests
{
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_GivenEmptyOrWhitespaceCode_ThrowsArgumentException(string code)
    {
        Assert.Throws<ArgumentException>(() => new NotFoundException(code));
    }

    [Fact]
    public void Constructor_GivenNullCode_ThrowsArgumentException()
    {
        // ThrowIfNullOrWhiteSpace бросает ArgumentNullException для null —
        // это наследник ArgumentException, поэтому ThrowsAny.
        Assert.ThrowsAny<ArgumentException>(() => new NotFoundException(null!));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(200)]
    [InlineData(399)]
    [InlineData(600)]
    public void Constructor_GivenStatusCodeOutsideHttpErrorRange_ThrowsArgumentOutOfRangeException(int statusCode)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new StatusCodeProbeException("test.code", statusCode));
    }

    [Theory]
    [InlineData(400)]
    [InlineData(599)]
    public void Constructor_GivenStatusCodeAtRangeBoundaries_DoesNotThrow(int statusCode)
    {
        var exception = new StatusCodeProbeException("test.code", statusCode);

        Assert.Equal(statusCode, exception.StatusCode);
    }

    /// <summary>
    /// Минимальный наследник <see cref="MustaskException"/>, который пробрасывает
    /// <c>statusCode</c> напрямую — нужен, чтобы проверить guard по диапазону
    /// без привязки к конкретному фиксированному статусу существующих
    /// наследников.
    /// </summary>
    private sealed class StatusCodeProbeException : MustaskException
    {
        public StatusCodeProbeException(string code, int statusCode)
            : base(code, statusCode)
        {
        }
    }
}
