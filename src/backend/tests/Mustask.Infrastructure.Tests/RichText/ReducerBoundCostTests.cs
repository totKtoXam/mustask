using System.Diagnostics;
using System.Text;
using Xunit.Abstractions;
using static Mustask.Infrastructure.Tests.RichText.RichTextTestSupport;

namespace Mustask.Infrastructure.Tests.RichText;

/// <summary>
/// What the worst input each deterministic bound still lets through costs (QA R-1: the bounds must keep it inside the
/// NFR, and legitimate documents far from them). The assertion is the 2 s of the NFR "a hostile 2 000 000-unit input
/// is answered within 2 s", with the measured figure in the test output.
/// </summary>
public sealed class ReducerBoundCostTests(ITestOutputHelper output)
{
    private static string Chain(int depth, string leaf) =>
        string.Concat(Enumerable.Repeat("<div>", depth)) + leaf + string.Concat(Enumerable.Repeat("</div>", depth));

    public static IEnumerable<object[]> WorstCases()
    {
        // Deep chain (the nesting bound) with as many flat elements under it as the element bound allows.
        yield return ["depth-150-with-142000-paragraphs", Chain(150, string.Concat(Enumerable.Repeat("<p></p>", 142_000)))];
        yield return ["depth-150-with-399000-br", "<p>" + Chain(0, "") + string.Concat(Enumerable.Repeat("<div>", 150))
            + string.Concat(Enumerable.Repeat("<br>", 399_000))];
        yield return ["depth-150-unclosed-b-with-flat-p", string.Concat(Enumerable.Repeat("<b>", 150)) + string.Concat(Enumerable.Repeat("<p>x", 150_000))];
        yield return ["depth-150-unclosed-div-with-flat-li", string.Concat(Enumerable.Repeat("<div>", 150)) + string.Concat(Enumerable.Repeat("<li>x", 280_000))];
        yield return ["depth-150-misnested-formatting", string.Concat(Enumerable.Repeat("<b><i>", 75)) + string.Concat(Enumerable.Repeat("<p>x</b>y</i>z", 80_000))];

        // The attribute bound: as many 64-attribute tags as the squares bound allows (1 953), and 2M units of 63.
        var tag = "<span " + string.Concat(Enumerable.Range(0, 64).Select(i => $"a{i}=v ")) + ">x</span>";
        yield return ["attributes-64-x-1900", string.Concat(Enumerable.Repeat(tag, 1_900))];
        var narrow = "<span " + string.Concat(Enumerable.Range(0, 16).Select(i => $"a{i}=v ")) + ">x</span>";
        yield return ["attributes-16-to-2M-units", string.Concat(Enumerable.Repeat(narrow, 2_000_000 / narrow.Length))];

        // Tags: the tag bound at 2M units of the cheapest tags, and one huge table.
        yield return ["tags-350000-b", string.Concat(Enumerable.Repeat("<b></b>", 175_000))];
        yield return ["table-100000-cells", "<table>" + string.Concat(Enumerable.Repeat("<tr><td>x</td></tr>", 100_000)) + "</table>"];
        yield return ["table-span-bomb", "<table><tr><td colspan='1000' rowspan='65534'>x</td></tr>" + string.Concat(Enumerable.Repeat("<tr><td>y</td></tr>", 65_533)) + "</table>"];

        // Tree-builder markup at the length bound: 8 000 units of the worst the parser has (attributes, nesting).
        yield return ["short-svg-nest", "<svg>" + string.Concat(Enumerable.Repeat("<g>", 1_500))];
        yield return ["short-template-nest", string.Concat(Enumerable.Repeat("<template>", 790))];
        yield return ["short-select-options", "<select>" + string.Concat(Enumerable.Repeat("<option>", 990))];
    }

    [Theory]
    [MemberData(nameof(WorstCases))]
    public void Reduce_WorstCaseUnderTheBounds_IsAnsweredWithinTheNfr(string name, string input)
    {
        Reduce("<p>warm</p>"); // the process's first reduction pays the JIT

        var watch = Stopwatch.StartNew();
        var reduction = Reduce(input);
        watch.Stop();

        output.WriteLine($"{name}: units={input.Length} elapsed={watch.ElapsedMilliseconds} ms exceeded={reduction.Exceeded} markup={reduction.MarkupLength}");
        Assert.True(watch.Elapsed < RichTextTestSupport.Budget(2), $"{name} took {watch.Elapsed}.");
    }

    // The cost of the legitimate documents the spec insists are storable, for the record (same process, warm).
    public static IEnumerable<object[]> Legitimate()
    {
        yield return ["legacy-100001-empty-paragraphs", string.Concat(Enumerable.Repeat("<p></p>", 100_001))];
        yield return ["legacy-100000-ampersands", "<p>" + string.Concat(Enumerable.Repeat("&amp;", 100_000)) + "</p>"];
        yield return ["table-10col-1M", Table(10, 1_000_000)];
        yield return ["table-1col-1M", Table(1, 1_000_000)];
        yield return ["paragraphs-with-marks-1M", string.Concat(Enumerable.Repeat("<p><strong>ab</strong><em>c</em></p>", 27_000))];
    }

    [Theory]
    [MemberData(nameof(Legitimate))]
    public void Reduce_LegitimateDocument_IsAcceptedWithinTheNfr(string name, string input)
    {
        Reduce("<p>warm</p>");

        var watch = Stopwatch.StartNew();
        var reduction = Reduce(input);
        watch.Stop();

        output.WriteLine($"{name}: units={input.Length} elapsed={watch.ElapsedMilliseconds} ms exceeded={reduction.Exceeded} markup={reduction.MarkupLength}");
        Assert.False(reduction.Exceeded);
        Assert.True(watch.Elapsed < RichTextTestSupport.Budget(2), $"{name} took {watch.Elapsed}.");
    }

    // The cold-start figure: the first reduction of a process, a ceiling-sized document, JIT included. Run alone in a
    // fresh test host (--filter on this test) to get the figure the NFR's "within 2 s" is read against.
    [Fact]
    public void Reduce_FirstCeilingSizedDocumentOfTheProcess_Figure()
    {
        var input = Table(10, 1_000_000);
        var reducer = new Mustask.Infrastructure.RichText.HtmlRichTextReducer(Vocabulary);

        var watch = Stopwatch.StartNew();
        var first = reducer.Reduce(input, new(Ceiling), CancellationToken.None);
        var coldMs = watch.ElapsedMilliseconds;
        watch.Restart();
        reducer.Reduce(input, new(Ceiling), CancellationToken.None);
        var warmMs = watch.ElapsedMilliseconds;

        output.WriteLine($"cold={coldMs} ms warm={warmMs} ms units={input.Length} exceeded={first.Exceeded}");
        Assert.False(first.Exceeded);
        Assert.True(coldMs < 5_000, $"Cold start took {coldMs} ms.");
    }

    private static string Table(int columns, int units)
    {
        var builder = new StringBuilder("<table><tbody>");
        var row = "<tr>" + string.Concat(Enumerable.Repeat("<td><p>cell</p></td>", columns)) + "</tr>";

        while (builder.Length + row.Length + 16 < units)
        {
            builder.Append(row);
        }

        return builder.Append("</tbody></table>").ToString();
    }
}
