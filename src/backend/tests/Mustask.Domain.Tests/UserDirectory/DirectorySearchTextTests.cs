using System.Text;
using Mustask.Domain.UserDirectory;

namespace Mustask.Domain.Tests.UserDirectory;

/// <summary>mod-014 US-1: how a search text is classified and normalised (BR-UP-14; api-contract.md A.3).</summary>
public sealed class DirectorySearchTextTests
{
    // US-1 "ё and е count as the same letter".
    [Theory]
    [InlineData("Семён", "семен")]
    [InlineData("СЕМЁН", "семен")]
    [InlineData("Семён", "семен")] // "е" + combining diaeresis: NFC composes it to "ё" first
    [InlineData("Ёлкин", "елкин")]
    public void Normalize_FoldsYoAndCase(string value, string expected)
    {
        Assert.Equal(expected, DirectorySearchText.Normalize(value));
    }

    [Fact]
    public void Normalize_RemovesControlCharacters()
    {
        Assert.Equal("anna", DirectorySearchText.Normalize("an\u0000n\u001Fa"));
    }

    // US-1 "A text shorter than 2 characters returns nobody".
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(" а ")]
    [InlineData("\u0001\u0002")] // two characters, none left after normalisation
    public void Parse_FewerThanTwoCharacters_IsTooShort(string text)
    {
        Assert.Equal(DirectorySearchTextKind.TooShort, DirectorySearchText.Parse(text).Kind);
    }

    [Fact]
    public void Parse_TwoCharacters_IsAPattern()
    {
        var parsed = DirectorySearchText.Parse(" ан ");

        Assert.Equal(DirectorySearchTextKind.Pattern, parsed.Kind);
        Assert.Equal("%ан%", parsed.LikePattern);
    }

    // US-1 "A text longer than 100 characters is refused" (and exactly 100 is accepted).
    [Fact]
    public void Parse_OneHundredAndOneCharactersAfterTrimming_IsTooLong()
    {
        var text = "  " + new string('я', UserDirectoryLimits.SearchTextMaxLength + 1) + "  ";

        Assert.Equal(DirectorySearchTextKind.TooLong, DirectorySearchText.Parse(text).Kind);
    }

    [Fact]
    public void Parse_ExactlyOneHundredCharacters_IsAPattern()
    {
        var text = "  " + new string('я', UserDirectoryLimits.SearchTextMaxLength) + "  ";

        Assert.Equal(DirectorySearchTextKind.Pattern, DirectorySearchText.Parse(text).Kind);
    }

    [Fact]
    public void Parse_CountsCodePointsNotUtf16Units()
    {
        // 100 emoji are 200 UTF-16 units but 100 code points: accepted.
        var text = string.Concat(Enumerable.Repeat("\U0001F600", UserDirectoryLimits.SearchTextMaxLength));

        Assert.Equal(DirectorySearchTextKind.Pattern, DirectorySearchText.Parse(text).Kind);
    }

    [Fact]
    public void Parse_EscapesLikeWildcards()
    {
        var parsed = DirectorySearchText.Parse(@"a_b%c\d");

        Assert.Equal(@"%a\_b\%c\\d%", parsed.LikePattern);
    }

    [Fact]
    public void Parse_NullText_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => DirectorySearchText.Parse(null!));
    }

    // D-5 (qa.md, re-check 3): the noncharacter set, the way the standard defines it — sixty-six values, written out here
    // from the definition rather than from the implementation's mask: U+FDD0..U+FDEF and the last two of each of 17 planes.
    [Fact]
    public void IsNoncharacter_IsExactlyTheStandardsSixtySixValues()
    {
        var expected = new HashSet<int>(Enumerable.Range(0xFDD0, 0x20));

        for (var plane = 0; plane <= 16; plane++)
        {
            expected.Add((plane * 0x10000) + 0xFFFE);
            expected.Add((plane * 0x10000) + 0xFFFF);
        }

        var actual = new HashSet<int>();

        for (var codePoint = 0; codePoint <= 0x10FFFF; codePoint++)
        {
            if (codePoint is >= 0xD800 and <= 0xDFFF)
            {
                continue;
            }

            if (DirectorySearchText.IsNoncharacter(new Rune(codePoint)))
            {
                actual.Add(codePoint);
            }
        }

        Assert.Equal(66, expected.Count);
        Assert.True(
            expected.SetEquals(actual),
            $"{actual.Count} values; missing {string.Join(",", expected.Except(actual).Take(5))}, extra {string.Join(",", actual.Except(expected).Take(5))}");
    }

    // D-5: Normalize never throws, for any scalar value in a text — .NET's NFC throws on U+FFFE.
    [Fact]
    public void Normalize_IsTotal_OverEveryScalarValue()
    {
        var failures = new List<string>();

        for (var codePoint = 0; codePoint <= 0x10FFFF; codePoint++)
        {
            if (codePoint is >= 0xD800 and <= 0xDFFF)
            {
                continue;
            }

            try
            {
                _ = DirectorySearchText.Normalize($"A{char.ConvertFromUtf32(codePoint)}B");
            }
            catch (Exception ex)
            {
                failures.Add($"U+{codePoint:X4}: {ex.GetType().Name}");
            }
        }

        Assert.True(failures.Count == 0, $"{failures.Count} scalar values throw: {string.Join("; ", failures.Take(10))}");
    }

    [Fact]
    public void Normalize_TakesALoneSurrogate_AsTheReplacementCharacter()
    {
        Assert.Equal("a�b", DirectorySearchText.Normalize("a\uD800b"));
    }

    // D-5: a noncharacter is read as U+FFFD — what the provider writes for it in a name — and does not shorten the text.
    [Theory]
    [InlineData(0xFFFE)]
    [InlineData(0xFFFF)]
    [InlineData(0xFDD0)]
    [InlineData(0xFDEF)]
    [InlineData(0x1FFFE)]
    [InlineData(0x10FFFF)]
    public void Normalize_ReadsANoncharacterAsTheReplacementCharacter(int codePoint)
    {
        Assert.Equal("ан�на", DirectorySearchText.Normalize($"Ан{char.ConvertFromUtf32(codePoint)}на"));
    }

    // D-5, defence in depth: whichever other value a runtime's NFC refuses, the text still normalises — without the
    // composition step, with everything else (controls out, lower case, ё → е) applied.
    [Fact]
    public void Normalize_WhenTheCompositionStepThrows_GoesOnWithoutIt()
    {
        var steps = 0;

        var result = DirectorySearchText.Normalize("Ё\u0001Л", _ =>
        {
            steps++;
            throw new ArgumentException("String contains invalid Unicode code points.");
        });

        Assert.Equal(1, steps);
        Assert.Equal("ел", result);
    }

    [Fact]
    public void Normalize_SwallowsNothingButTheCompositionStepsArgumentException()
    {
        Assert.Throws<InvalidOperationException>(() => DirectorySearchText.Normalize("ab", _ => throw new InvalidOperationException()));
    }

    // A.3 (D-5): a search text with a noncharacter is a pattern like any other — never a throw — and matches the
    // user whose name the provider cleaned.
    [Fact]
    public void Parse_ATextWithU_FFFE_IsAPatternWithTheReplacementCharacter()
    {
        var parsed = DirectorySearchText.Parse("ab￾cd");

        Assert.Equal(DirectorySearchTextKind.Pattern, parsed.Kind);
        Assert.Equal("%ab�cd%", parsed.LikePattern);
    }

    [Fact]
    public void Parse_TwoNoncharacters_AreTwoCharacters_NotTooShort()
    {
        Assert.Equal(DirectorySearchTextKind.Pattern, DirectorySearchText.Parse("￾￿").Kind);
    }
}
