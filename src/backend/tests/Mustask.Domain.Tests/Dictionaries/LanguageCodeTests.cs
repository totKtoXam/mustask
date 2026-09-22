using Mustask.Domain.Dictionaries;
using Mustask.Shared.Exceptions;

namespace Mustask.Domain.Tests.Dictionaries;

public sealed class LanguageCodeTests
{
    [Theory]
    [InlineData("ru", "ru")]
    [InlineData(" RU ", "ru")]
    [InlineData("kk-KZ", "kk-kz")]
    public void Create_ValidInput_NormalizesTrimAndCase(string raw, string expected)
    {
        var code = LanguageCode.Create(raw);

        Assert.Equal(expected, code.Value);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    [InlineData("r")]
    [InlineData("a-very-long-code-over-8")]
    [InlineData("123")]
    [InlineData("ru_RU")]
    public void Create_InvalidInput_Throws(string? raw)
    {
        var ex = Assert.Throws<ValidationException>(() => LanguageCode.Create(raw));

        Assert.Equal(DictionaryErrorCodes.ValidationFailed, ex.Code);
        Assert.True(ex.Errors.ContainsKey("code"));
        Assert.Equal(DictionaryErrorCodes.LanguagesInvalidCode, ex.Errors["code"][0].Code);
    }

    /// <summary>
    /// "2–8 characters": the boundaries of the
    /// length window themselves — two and eight characters are accepted,
    /// nine is not. The existing theories only sampled well inside and well
    /// outside the window ("r", "a-very-long-code-over-8"), so an off-by-one
    /// in either bound would have gone unnoticed.
    /// </summary>
    [Theory]
    [InlineData("ru")]
    [InlineData("kk-kz-tr")]
    public void Create_LengthAtTheBoundary_IsAccepted(string raw)
    {
        var code = LanguageCode.Create(raw);

        Assert.Equal(raw, code.Value);
    }

    /// <summary>
    /// Nine characters is one over the ceiling; `ru-Cyrl-RU` documents
    /// the accepted consequence — a well-formed BCP-47 tag longer than eight
    /// characters is refused by this dictionary's own length limit, so a
    /// script subtag plus a region cannot be a language code here
    /// ("код языка — 2–8 символов").
    /// </summary>
    [Theory]
    [InlineData("kk-kz-tra")]
    [InlineData("ru-Cyrl-RU")]
    public void Create_LengthOverTheCeiling_Throws(string raw)
    {
        var ex = Assert.Throws<ValidationException>(() => LanguageCode.Create(raw));

        Assert.Equal(DictionaryErrorCodes.LanguagesInvalidCode, ex.Errors["code"][0].Code);
    }

    [Fact]
    public void Create_UsesGivenFieldName()
    {
        var ex = Assert.Throws<ValidationException>(() => LanguageCode.Create(null, "language"));

        Assert.True(ex.Errors.ContainsKey("language"));
    }
}
