using Mustask.Domain.Dictionaries;
using Mustask.Infrastructure.Dictionaries;

namespace Mustask.Infrastructure.Tests.Dictionaries;

/// <summary>
/// `RequestLanguageResolver`'s decision had only three Docker-gated
/// end-to-end cases with single-tag headers -- the quality-order walk over a
/// multi-tag header, the inactive-language skip and a malformed/empty tag list were unexercised.
/// Exercises <see cref="RequestLanguageResolver.Resolve"/> directly: once
/// the candidate rows are in memory the whole decision is pure and needs no
/// database (the `internal` visibility is for exactly this).
/// </summary>
public sealed class RequestLanguageResolverTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 9, 12, 0, 0, TimeSpan.Zero);

    private static Language Seed(string code, bool isDefault, bool isActive, int sortOrder = 10) =>
        Language.Seed(Guid.CreateVersion7(), code, endonym: code, isDefault, isActive, sortOrder, Now, "seed");

    /// <summary>Tags are walked in the order given (the caller already sorted them by quality) -- the first one that matches wins, later ones are never consulted.</summary>
    [Fact]
    public void Resolve_MultipleTags_WalksInGivenOrder_FirstMatchWins()
    {
        var ru = Seed("ru", isDefault: true, isActive: true);
        var kk = Seed("kk", isDefault: false, isActive: true);

        // "fr" (unknown) then "kk" (matches) then "ru" (would also match,
        // but never reached) -- mirrors a real `Accept-Language:
        // fr,kk;q=0.9,ru;q=0.8` already reduced to quality order.
        var result = RequestLanguageResolver.Resolve([ru, kk], ["fr", "kk", "ru"]);

        Assert.Equal("kk", result.Code);
    }

    /// <summary>A full-tag match wins over a primary-subtag match found later in the same walk, even when the full tag comes from a lower-quality position.</summary>
    [Fact]
    public void Resolve_FullTagMatch_StopsBeforeConsideringLaterPrimarySubtagCandidates()
    {
        var ru = Seed("ru", isDefault: true, isActive: true);
        var kk = Seed("kk", isDefault: false, isActive: true);
        var kkKz = Seed("kk-kz", isDefault: false, isActive: true, sortOrder: 20);

        // First tag "kk-KZ" matches the full tag "kk-kz" exactly -- the
        // primary-subtag fallback to "kk" (also a real language here) must
        // never be reached for this tag, and "ru" is never reached either.
        var result = RequestLanguageResolver.Resolve([ru, kk, kkKz], ["kk-KZ", "ru"]);

        Assert.Equal("kk-kz", result.Code);
    }

    /// <summary>Within one tag, the primary subtag is tried only once the full tag misses.</summary>
    [Fact]
    public void Resolve_NoFullTagMatch_FallsBackToPrimarySubtagOfTheSameTag()
    {
        var ru = Seed("ru", isDefault: true, isActive: true);
        var kk = Seed("kk", isDefault: false, isActive: true);

        var result = RequestLanguageResolver.Resolve([ru, kk], ["kk-KZ"]);

        Assert.Equal("kk", result.Code);
    }

    /// <summary>"Только среди активных языков" -- a deactivated language must never win, even as an exact full-tag match; the resolver keeps walking to the next tag instead.</summary>
    [Fact]
    public void Resolve_MatchingLanguageIsInactive_IsSkipped_FallsThroughToNextTag()
    {
        var ru = Seed("ru", isDefault: true, isActive: true);
        var kk = Seed("kk", isDefault: false, isActive: false);

        var result = RequestLanguageResolver.Resolve([ru, kk], ["kk", "ru"]);

        Assert.Equal("ru", result.Code);
    }

    /// <summary>"Только среди активных языков" -- an inactive language never satisfies the primary-subtag fallback either.</summary>
    [Fact]
    public void Resolve_PrimarySubtagLanguageIsInactive_IsSkipped_FallsBackToDefault()
    {
        var ru = Seed("ru", isDefault: true, isActive: true);
        var kk = Seed("kk", isDefault: false, isActive: false);

        var result = RequestLanguageResolver.Resolve([ru, kk], ["kk-KZ"]);

        Assert.Equal("ru", result.Code);
    }

    /// <summary>"Если ничего не совпало ... — язык по умолчанию": an empty tag list (no `Accept-Language` header at all) resolves to the default.</summary>
    [Fact]
    public void Resolve_NoTags_FallsBackToDefault()
    {
        var ru = Seed("ru", isDefault: true, isActive: true);
        var kk = Seed("kk", isDefault: false, isActive: true);

        var result = RequestLanguageResolver.Resolve([ru, kk], []);

        Assert.Equal("ru", result.Code);
        Assert.Equal(ru.Id, result.DefaultLanguageId);
    }

    /// <summary>A malformed tag (blank/whitespace-only, as `StringWithQualityHeaderValue.TryParseList` failure or an `Accept-Language: *` would reduce to) is skipped, not thrown on.</summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Resolve_BlankTag_IsSkipped_FallsBackToDefault(string blankTag)
    {
        var ru = Seed("ru", isDefault: true, isActive: true);
        var kk = Seed("kk", isDefault: false, isActive: true);

        var result = RequestLanguageResolver.Resolve([ru, kk], [blankTag]);

        Assert.Equal("ru", result.Code);
    }

    /// <summary>"Сравнивая ... регистронезависимо": an upper-case tag matches a lower-cased stored code.</summary>
    [Fact]
    public void Resolve_TagIsCaseInsensitive()
    {
        var ru = Seed("ru", isDefault: true, isActive: true);
        var kk = Seed("kk", isDefault: false, isActive: true);

        var result = RequestLanguageResolver.Resolve([ru, kk], ["KK"]);

        Assert.Equal("kk", result.Code);
    }

    /// <summary>No default language at all is an application defect (the seed has not run), not a silently wrong answer.</summary>
    [Fact]
    public void Resolve_NoDefaultLanguageInTheDictionary_Throws()
    {
        var kk = Seed("kk", isDefault: false, isActive: true);

        Assert.Throws<InvalidOperationException>(() => RequestLanguageResolver.Resolve([kk], ["kk"]));
    }
}
