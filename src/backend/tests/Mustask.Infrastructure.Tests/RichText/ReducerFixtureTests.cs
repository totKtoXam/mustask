using System.Text;
using Mustask.Infrastructure.RichText;
using static Mustask.Infrastructure.Tests.RichText.RichTextTestSupport;

namespace Mustask.Infrastructure.Tests.RichText;

/// <summary>
/// The shared fixture files of <c>contracts/rich-text/fixtures</c> run against the reducer (feat-018 US-2, US-3, US-4,
/// US-7): the panel runs the same files, so a case added by either side runs on both.
/// </summary>
public sealed class ReducerFixtureTests
{
    public static IEnumerable<object[]> CanonicalCases() => CaseIds("canonical.json");

    public static IEnumerable<object[]> VisibleCases() => CaseIds("visible-count.json");

    public static IEnumerable<object[]> LegacyCases() => CaseIds("legacy.json");

    public static IEnumerable<object[]> HostileCases() => CaseIds("hostile-corpus.json");

    // US-7 · "The editor's canonical form is the server's" and the scenarios each case names (US-2, US-7): reducing
    // the input and the editor's output both give canonical, reducing canonical again returns canonical (idempotent).
    [Theory]
    [MemberData(nameof(CanonicalCases))]
    public void Reduce_CanonicalFixture_InputAndEditorOutputGiveCanonical_CanonicalIsAFixedPoint(string id)
    {
        var fixture = Case("canonical.json", id);
        var canonical = fixture.GetProperty("canonical").GetString()!;

        Assert.Equal(canonical, Stored(fixture.GetProperty("input").GetString()!));

        if (fixture.TryGetProperty("editorOutput", out var editorOutput))
        {
            Assert.Equal(canonical, Stored(editorOutput.GetString()!));
        }

        Assert.Equal(canonical, Stored(canonical));
    }

    // US-2 · "A stored value is stable under a browser's re-parse" for every canonical value.
    [Theory]
    [MemberData(nameof(CanonicalCases))]
    public void Reduce_CanonicalFixture_StoredValueIsStableAndInsideTheAllowList(string id)
    {
        var stored = Stored(Case("canonical.json", id).GetProperty("input").GetString()!);

        AssertInsideAllowList(stored);
        AssertStableUnderReparse(stored);
    }

    // US-4 · "Visible text is counted by BR-RT-8": one shared (html, count) file.
    [Theory]
    [MemberData(nameof(VisibleCases))]
    public void Reduce_VisibleCountFixture_CountsByCodePoints(string id)
    {
        var fixture = Case("visible-count.json", id);
        var html = fixture.GetProperty("html").GetString()!.Replace("{U+D83D}", "\uD83D", StringComparison.Ordinal);
        var reduction = Reduce(html);

        Assert.False(reduction.Exceeded);
        Assert.Equal(fixture.GetProperty("count").GetInt32(), reduction.VisibleLength);

        // A lone surrogate is replaced by U+FFFD in the stored html as well.
        if (fixture.TryGetProperty("stored", out var stored))
        {
            Assert.Equal(stored.GetString(), reduction.Html);
        }
    }

    // US-3 · "Legacy text of any length still saves" and US-4 · "Legacy worst cases fit the ceiling": the client's
    // conversion is a fixed point of the reduction, within the ceiling, and counts as expected.
    [Theory]
    [MemberData(nameof(LegacyCases))]
    public void Reduce_LegacyFixture_ConvertedHtmlIsAFixedPoint_WithinTheCeiling(string id)
    {
        var fixture = Case("legacy.json", id);
        var html = fixture.TryGetProperty("generator", out var generator) ? Generate(generator.GetString()!) : fixture.GetProperty("html").GetString()!;

        var reduction = Reduce(html);

        Assert.False(reduction.Exceeded);
        Assert.Equal(html, reduction.Html);
        Assert.Equal(fixture.GetProperty("count").GetInt32(), reduction.VisibleLength);

        if (fixture.TryGetProperty("markup", out var markup))
        {
            Assert.Equal(markup.GetInt32(), reduction.MarkupLength);
            Assert.Equal(markup.GetInt32(), html.Length);
        }

        Assert.True(reduction.MarkupLength <= Ceiling);
    }

    // US-2 · "A hostile corpus does not survive" and "A stored value is stable under a browser's re-parse".
    [Theory]
    [MemberData(nameof(HostileCases))]
    public void Reduce_HostileCorpus_NothingSurvives_StoredValueIsStable(string id)
    {
        var fixture = Case("hostile-corpus.json", id);
        var stored = Stored(fixture.GetProperty("input").GetString()!);
        var lower = stored.ToLowerInvariant();

        foreach (var pattern in fixture.GetProperty("mustNotContain").EnumerateArray())
        {
            Assert.DoesNotContain(pattern.GetString()!.ToLowerInvariant(), lower, StringComparison.Ordinal);
        }

        AssertInsideAllowList(stored);
        AssertStableUnderReparse(stored);

        // Idempotent: a second save changes nothing.
        Assert.Equal(stored, Stored(stored));
    }

    private static string Generate(string name)
    {
        switch (name)
        {
            case "repeat-amp-100000":
                return "<p>" + string.Concat(Enumerable.Repeat("&amp;", 100_000)) + "</p>";
            case "lf-100000":
                var builder = new StringBuilder();

                for (var index = 0; index <= 100_000; index++)
                {
                    builder.Append("<p></p>");
                }

                return builder.ToString();
            default:
                throw new ArgumentOutOfRangeException(nameof(name), name, "Unknown fixture generator.");
        }
    }
}
