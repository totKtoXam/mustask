using Mustask.Domain.WorkItems;

namespace Mustask.Domain.Tests.WorkItems;

/// <summary>
/// The keyword of the list's «Поиск» (feat-003 BR-WS-9, api-contract § 3.3) as the domain reads it: what is searched for, as
/// the title, the identifier or the number. Whether a title contains the text is the database's, tested through the endpoint.
/// </summary>
public sealed class WorkItemKeywordTests
{
    private const string ProjectKey = "MSK";

    private static WorkItemKeyword? Of(params string?[] values) => WorkItemKeyword.Of(values, ProjectKey);

    // US-3 · "Spaces around the keyword do not count, and a blank keyword is no filter".
    [Theory]
    [InlineData("   оплат   ", "оплат")]
    [InlineData("\t оплат \n", "оплат")]
    [InlineData(" оплат ", "оплат")]
    public void Of_IsTrimmedAtBothEnds_ByEveryUnicodeWhiteSpace(string raw, string expected)
    {
        Assert.Equal(expected, Of(raw)!.Text);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t ")]
    public void Of_BlankKeyword_IsNoKeyword(string raw)
    {
        Assert.Null(Of(raw));
        Assert.Null(WorkItemKeyword.Of([null], ProjectKey));
        Assert.Null(WorkItemKeyword.Of(null, ProjectKey));
        Assert.Null(WorkItemKeyword.Of([], ProjectKey));
    }

    // BR-WS-9 · "Given more than once in the address, the first value counts" — the first of the non-blank ones (BR-WS-11).
    [Fact]
    public void Of_GivenMoreThanOnce_TheFirstNonBlankCounts()
    {
        Assert.Equal("первый", Of("первый", "второй")!.Text);
        Assert.Equal("второй", Of("  ", "второй", "третий")!.Text);
    }

    // US-3 · "The field takes at most 255 characters": 255 after trimming is searched, 256 matches nothing — and is no error.
    [Fact]
    public void Of_At255AfterTrimming_IsSearched_At256_MatchesNothing()
    {
        var at255 = Of("  " + new string('я', 255) + "  ")!;
        var at256 = Of(new string('я', 256))!;

        Assert.False(at255.MatchesNothing);
        Assert.Equal(255, at255.Text.Length);
        Assert.True(at256.MatchesNothing);
    }

    // R-6 · "a character no title can hold (a line break, U+0000)": matches nothing, never an error.
    [Theory]
    [InlineData("оплата\nкартой")]
    [InlineData("оплата\rкартой")]
    [InlineData("оплата\vкартой")]
    [InlineData("оплата\fкартой")]
    [InlineData("оплата\u0085картой")]
    [InlineData("оплата\x2028картой")]
    [InlineData("оплата\x2029картой")]
    [InlineData("опл\0ата")]
    public void Of_ALineBreakOrNul_MatchesNothing(string raw)
    {
        var keyword = Of(raw)!;

        Assert.True(keyword.MatchesNothing);
        Assert.Null(keyword.Number);
        Assert.Null(keyword.Identifier);
    }

    // BR-WS-9 (b) · "the keyword is its identifier, ignoring letter case".
    [Theory]
    [InlineData("MSK-BUG-42", "BUG", 42)]
    [InlineData("msk-bug-42", "BUG", 42)]
    [InlineData("Msk-Us-3", "US", 3)]
    [InlineData("  msk-task-2147483647  ", "TASK", int.MaxValue)]
    public void Of_TheAddressedProjectsIdentifier_IsAnIdentifier_AnyLetterCase(string raw, string typeKey, int number)
    {
        var keyword = Of(raw)!;

        Assert.Equal(new KeywordIdentifier(typeKey, number), keyword.Identifier);
        Assert.False(keyword.MatchesNothing);
    }

    // BR-WS-9 · "A part of an identifier, an identifier with another type's key, and another project's identifier match no
    // identifier; they still match a title that contains them": no identifier, the text kept for the title.
    [Theory]
    [InlineData("MSK-BUG-")]
    [InlineData("BUG-42")]
    [InlineData("MSK-42")]
    [InlineData("-BUG-42")]
    [InlineData("MSK-BUG-042")]
    [InlineData("MSK-BUG-0")]
    [InlineData("MSK-BUG-42-1")]
    [InlineData("MSK BUG 42")]
    [InlineData("WRH-US-1")]
    [InlineData("OTH-US-1")]
    [InlineData("MSKX-BUG-1")]
    [InlineData("MS-BUG-1")]
    [InlineData("MSK-BUG-2147483648")]
    [InlineData("MSK-BUG-99999999999")]
    public void Of_APartOrAnotherProjectsIdentifier_IsNoIdentifier_OnlyText(string raw)
    {
        var keyword = Of(raw)!;

        Assert.Null(keyword.Identifier);
        Assert.False(keyword.MatchesNothing);
        Assert.Equal(raw, keyword.Text);
    }

    // BR-WS-9 · letters outside the Latin alphabet are never folded onto it: a dotless «ı» and a long «ſ» do not spell EPIC or US.
    [Theory]
    [InlineData("msk-epıc-1")]
    [InlineData("msk-ſ-1")]
    [InlineData("MSK-BUG-４２")]
    public void Of_ALookalikeOutsideAscii_IsNoIdentifier(string raw)
    {
        Assert.Null(Of(raw)!.Identifier);
    }

    // BR-WS-9 (c) · "the keyword is its number, in digits as the identifier writes it".
    [Theory]
    [InlineData("1", 1)]
    [InlineData("42", 42)]
    [InlineData("  42  ", 42)]
    [InlineData("2147483647", int.MaxValue)]
    public void Of_DigitsAsAnIdentifierWritesThem_AreANumber(string raw, int expected)
    {
        Assert.Equal(expected, Of(raw)!.Number);
    }

    // R-6 · "an overflowing number": text for the title, no number, no error; and not the number as the identifier writes it.
    [Theory]
    [InlineData("0")]
    [InlineData("042")]
    [InlineData("00")]
    [InlineData("2147483648")]
    [InlineData("99999999999")]
    [InlineData("-1")]
    [InlineData("+1")]
    [InlineData("4 2")]
    [InlineData("4,2")]
    [InlineData("١٢")]
    [InlineData("４２")]
    public void Of_NotANumberAsAnIdentifierWritesIt_IsNoNumber_OnlyText(string raw)
    {
        var keyword = Of(raw)!;

        Assert.Null(keyword.Number);
        Assert.False(keyword.MatchesNothing);
    }

    // A word is neither: it is only text.
    [Fact]
    public void Of_AWord_IsOnlyText()
    {
        var keyword = Of("Оплата")!;

        Assert.Null(keyword.Number);
        Assert.Null(keyword.Identifier);
        Assert.Equal("Оплата", keyword.Text);
    }
}
