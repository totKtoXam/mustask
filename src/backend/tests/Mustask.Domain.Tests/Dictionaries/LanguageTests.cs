using Mustask.Domain.Dictionaries;
using Mustask.Shared.Exceptions;

namespace Mustask.Domain.Tests.Dictionaries;

/// <summary>Правила справочника языков.</summary>
public sealed class LanguageTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 9, 12, 0, 0, TimeSpan.Zero);

    private static Language NewInactiveLanguage() =>
        Language.Create(Guid.CreateVersion7(), "tr", endonym: null, sortOrder: 40, Now, "admin");

    [Fact]
    public void Create_NormalizesCodeAndEndonym_AndStartsInactive()
    {
        var language = Language.Create(Guid.CreateVersion7(), " TR ", " Türkçe ", 40, Now, "admin");

        Assert.Equal("tr", language.Code);
        Assert.Equal("Türkçe", language.Endonym);
        Assert.False(language.IsActive);
        Assert.False(language.IsDefault);
    }

    [Fact]
    public void Replace_ActivatingWithoutEndonym_Throws()
    {
        var language = NewInactiveLanguage();

        var ex = Assert.Throws<ValidationException>(
            () => language.Replace(endonym: null, sortOrder: 40, isActive: true, Now, "admin"));

        Assert.True(ex.Errors.ContainsKey("endonym"));
        Assert.Equal(DictionaryErrorCodes.LanguagesEndonymRequired, ex.Errors["endonym"][0].Code);
    }

    [Fact]
    public void Replace_ActivatingWithEndonym_Succeeds()
    {
        var language = NewInactiveLanguage();

        language.Replace(endonym: "Türkçe", sortOrder: 40, isActive: true, Now, "admin");

        Assert.True(language.IsActive);
        Assert.Equal("Türkçe", language.Endonym);
    }

    [Fact]
    public void Replace_ClearingEndonymOfActiveLanguage_Throws()
    {
        var language = NewInactiveLanguage();
        language.Replace("Türkçe", 40, isActive: true, Now, "admin");

        var ex = Assert.Throws<ValidationException>(
            () => language.Replace(endonym: "", sortOrder: 40, isActive: true, Now, "admin"));

        Assert.True(ex.Errors.ContainsKey("endonym"));
    }

    [Fact]
    public void Replace_DeactivatingDefaultLanguage_Throws()
    {
        var language = Language.Seed(Guid.CreateVersion7(), "ru", "Русский", isDefault: true, isActive: true, 10, Now, "seed");

        var ex = Assert.Throws<ConflictException>(
            () => language.Replace("Русский", 10, isActive: false, Now, "admin"));

        Assert.Equal(DictionaryErrorCodes.LanguagesDefaultCannotBeDeactivated, ex.Code);
    }

    [Fact]
    public void Replace_DeactivatingNonDefaultLanguage_Succeeds()
    {
        var language = Language.Seed(Guid.CreateVersion7(), "kk", "Қазақша", isDefault: false, isActive: true, 20, Now, "seed");

        language.Replace("Қазақша", 20, isActive: false, Now, "admin");

        Assert.False(language.IsActive);
    }

    [Fact]
    public void AssertCanBecomeDefault_InactiveLanguage_Throws()
    {
        var language = NewInactiveLanguage();

        var ex = Assert.Throws<ConflictException>(language.AssertCanBecomeDefault);

        Assert.Equal(DictionaryErrorCodes.LanguagesDefaultMustBeActive, ex.Code);
    }

    [Fact]
    public void AssertCanBecomeDefault_ActiveLanguage_DoesNotThrow()
    {
        var language = NewInactiveLanguage();
        language.Replace("Türkçe", 40, isActive: true, Now, "admin");

        language.AssertCanBecomeDefault();
    }

    [Fact]
    public void MakeDefault_ThenClearDefault_TogglesFlag()
    {
        var language = NewInactiveLanguage();

        language.MakeDefault(Now, "admin");
        Assert.True(language.IsDefault);

        language.ClearDefault(Now, "admin");
        Assert.False(language.IsDefault);
    }

    // --- A missing default is one descriptive failure, not a NullReference ---

    [Fact]
    public void GetDefault_OneLanguageIsDefault_ReturnsIt()
    {
        var ru = Language.Seed(Guid.CreateVersion7(), "ru", "Русский", isDefault: true, isActive: true, 10, Now, "seed");
        var kk = Language.Seed(Guid.CreateVersion7(), "kk", "Қазақша", isDefault: false, isActive: true, 20, Now, "seed");

        var result = Language.GetDefault([kk, ru]);

        Assert.Same(ru, result);
    }

    [Fact]
    public void GetDefault_NoLanguageIsDefault_ThrowsInvalidOperationException_NotNullReference()
    {
        var kk = Language.Seed(Guid.CreateVersion7(), "kk", "Қазақша", isDefault: false, isActive: true, 20, Now, "seed");

        var ex = Assert.Throws<InvalidOperationException>(() => Language.GetDefault([kk]));

        Assert.False(string.IsNullOrWhiteSpace(ex.Message));
    }

    [Fact]
    public void GetDefault_EmptyDictionary_ThrowsInvalidOperationException()
    {
        var ex = Assert.Throws<InvalidOperationException>(() => Language.GetDefault([]));

        Assert.False(string.IsNullOrWhiteSpace(ex.Message));
    }
}
