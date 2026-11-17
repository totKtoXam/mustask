using Mustask.Domain.Search;

namespace Mustask.Domain.Tests.Search;

/// <summary>feat-071: the pure rules of the quick search (feat-065 BR-GS-3, -8, -10, -17).</summary>
public sealed class SearchRulesTests
{
    // US-2 "The query is trimmed and bounded": trimmed, 2..100 code points.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("а")]
    [InlineData(" а ")]
    public void Judge_ShorterThanTwo_IsTooShort(string? text)
    {
        Assert.Equal(SearchQueryKind.TooShort, SearchQueryText.Judge(text, out var query));
        Assert.Null(query);
    }

    // US-2 "The query is trimmed and bounded": the boundaries 2 and 100 are valid, 101 is not.
    [Fact]
    public void Judge_Boundaries_TwoAndHundredAreValidHundredOneIsTooLong()
    {
        Assert.Equal(SearchQueryKind.Valid, SearchQueryText.Judge("ал", out _));
        Assert.Equal(SearchQueryKind.Valid, SearchQueryText.Judge(new string('а', 100), out _));
        Assert.Equal(SearchQueryKind.TooLong, SearchQueryText.Judge(new string('а', 101), out _));
    }

    // Length counts Unicode code points, not UTF-16 units: 50 surrogate pairs are 50 characters.
    [Fact]
    public void Judge_CountsCodePoints()
    {
        var fifty = string.Concat(Enumerable.Repeat("\U0001F600", 50));
        var hundredAndOne = string.Concat(Enumerable.Repeat("\U0001F600", 101));

        Assert.Equal(SearchQueryKind.Valid, SearchQueryText.Judge(fifty, out _));
        Assert.Equal(SearchQueryKind.TooLong, SearchQueryText.Judge(hundredAndOne, out _));
    }

    // US-2 "The query is trimmed": « ал » searches «ал».
    [Fact]
    public void Judge_Trims()
    {
        SearchQueryText.Judge("  ал  ", out var query);

        Assert.Equal("ал", query!.Folded);
        Assert.Equal(2, query.Length);
    }

    // US-2 "Case and «ё»": the fold ignores case and reads «ё» as «е».
    [Theory]
    [InlineData("ЗВЁЗДЫ", "звезды")]
    [InlineData("Звёзды", "звезды")]
    [InlineData("MSK-Bug", "msk-bug")]
    [InlineData("Alpha Альфа", "alpha альфа")]
    public void Fold_IgnoresCaseAndYo(string text, string expected) =>
        Assert.Equal(expected, SearchQueryText.Fold(text));

    // US-2 "Literal characters": «%», «_» and «\» are escaped for LIKE and for the regular expression.
    [Fact]
    public void Patterns_EscapeEveryWildcard()
    {
        SearchQueryText.Judge("a%_\\b", out var query);

        Assert.Equal("a\\%\\_\\\\b%", query!.PrefixPattern);
        Assert.Equal("%a\\%\\_\\\\b%", query.ContainsPattern);
        Assert.Equal("(^|[^[:alnum:]])a%_\\\\b", query.WordPrefixPattern);
    }

    [Fact]
    public void WordPrefixPattern_EscapesRegexMetacharacters()
    {
        SearchQueryText.Judge("a.b(c)", out var query);

        Assert.Equal("(^|[^[:alnum:]])a\\.b\\(c\\)", query!.WordPrefixPattern);
    }

    // US-2 "Typo tolerance needs 4 characters": 3 characters add no similar rows.
    [Theory]
    [InlineData("лог", false)]
    [InlineData("логи", true)]
    [InlineData("логстика", true)]
    public void AllowsSimilar_NeedsFourCharacters(string text, bool expected)
    {
        SearchQueryText.Judge(text, out var query);

        Assert.Equal(expected, query!.AllowsSimilar);
    }

    [Fact]
    public void Tiers_AreOrderedAndSimilarIsLast()
    {
        Assert.True(MatchTier.Exact < MatchTier.Prefix);
        Assert.True(MatchTier.Prefix < MatchTier.WordPrefix);
        Assert.True(MatchTier.WordPrefix < MatchTier.Contains);
        Assert.True(MatchTier.Contains < MatchTier.Similar);
    }

    // US-2 "Typo tolerance as the last tier": a group short of its limit is filled with similar rows, after every other row.
    [Fact]
    public void Fill_BelowLimit_KeepsSimilarRowsAfterTheOthers()
    {
        int[] tiers = [2, 4, 5, 5];

        var (rows, hasMore) = SimilarityRule.Fill(tiers, tier => tier, 5);

        Assert.Equal(tiers, rows);
        Assert.False(hasMore);
    }

    // US-2 "Typo tolerance needs 4 characters and a short group": at the limit no similar row is added and never displaces one.
    [Fact]
    public void Fill_AtLimit_DropsSimilarRows()
    {
        int[] tiers = [1, 2, 3, 4, 4, 5];

        var (rows, hasMore) = SimilarityRule.Fill(tiers, tier => tier, 5);

        Assert.Equal([1, 2, 3, 4, 4], rows);
        Assert.False(hasMore);
    }

    // US-4 "More never reflects a hidden row": more than the limit of visible rows is the only source of hasMore.
    [Fact]
    public void Fill_MoreExactRowsThanLimit_HasMore()
    {
        int[] tiers = [4, 4, 4, 4, 4, 4];

        var (rows, hasMore) = SimilarityRule.Fill(tiers, tier => tier, 5);

        Assert.Equal(5, rows.Count);
        Assert.True(hasMore);
    }

    [Fact]
    public void Fill_SimilarRowsPastTheLimit_ArePartOfMore()
    {
        int[] tiers = [4, 5, 5, 5, 5, 5];

        var (rows, hasMore) = SimilarityRule.Fill(tiers, tier => tier, 5);

        Assert.Equal(5, rows.Count);
        Assert.True(hasMore);
    }

    [Theory]
    [InlineData("projects", SearchGroupKind.Projects)]
    [InlineData("teams", SearchGroupKind.Teams)]
    [InlineData("departments", SearchGroupKind.Departments)]
    [InlineData("areas", SearchGroupKind.Areas)]
    [InlineData("users", SearchGroupKind.Users)]
    public void GroupKinds_ParseTheirWireName(string name, SearchGroupKind expected)
    {
        Assert.True(SearchGroupKinds.TryParse(name, out var kind));
        Assert.Equal(expected, kind);
        Assert.Equal(name, kind.ToWireName());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Projects")]
    [InlineData("workitems")]
    public void GroupKinds_RefuseAnythingElse(string? name) => Assert.False(SearchGroupKinds.TryParse(name, out _));
}
