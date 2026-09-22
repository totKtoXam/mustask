using Mustask.Domain.Dictionaries;
using Mustask.Infrastructure.Dictionaries.Seeding;

namespace Mustask.Infrastructure.Tests.Dictionaries;

/// <summary>
/// The shipped seed files themselves. The composition of
/// `contracts/dictionaries/*.json` is otherwise asserted only by
/// <c>DictionarySeederTests</c>, which needs Docker: a seed file broken in a
/// way that survives JSON parsing (a language dropped, two defaults, an
/// active language without an endonym) would pass every runnable test and
/// only surface at startup on a machine with a daemon. These checks read the
/// embedded resources through the production loader, so they cover the
/// loader's own contract too, and need no database at all.
/// </summary>
public sealed class DictionarySeedFilesTests
{
    private static readonly System.Reflection.Assembly InfrastructureAssembly = typeof(DictionarySeeder).Assembly;

    /// <summary>ru, kk, en; exactly one default and it is `ru`; all three active.</summary>
    [Fact]
    public void LoadLanguages_ShipsRuKkEn_WithRuTheOnlyDefault()
    {
        var languages = DictionarySeedFiles.LoadLanguages(InfrastructureAssembly);

        Assert.Equal(
            ["en", "kk", "ru"],
            languages.Select(l => l.Code).Order(StringComparer.Ordinal).ToList());

        var ru = Assert.Single(languages, l => l.Code == "ru");
        Assert.True(ru.IsDefault);
        Assert.True(ru.IsActive);
        Assert.Equal("ru", Assert.Single(languages, l => l.IsDefault).Code);
        Assert.All(languages, l => Assert.True(l.IsActive));
    }

    /// <summary>An active language needs a non-empty endonym — the seed must not ship a state the API itself would refuse.</summary>
    [Fact]
    public void LoadLanguages_EveryActiveLanguage_HasAnEndonym()
    {
        var languages = DictionarySeedFiles.LoadLanguages(InfrastructureAssembly);

        Assert.All(languages.Where(l => l.IsActive), l => Assert.False(string.IsNullOrWhiteSpace(l.Endonym)));
    }

    /// <summary>Every seeded code is a valid language code by the same rule the API applies to an administrator's input.</summary>
    [Fact]
    public void LoadLanguages_EveryCode_IsAValidLanguageCode()
    {
        var languages = DictionarySeedFiles.LoadLanguages(InfrastructureAssembly);

        Assert.All(languages, l => Assert.Equal(l.Code, LanguageCode.Create(l.Code).Value));
    }
}
