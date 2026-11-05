using System.Diagnostics;
using System.Text.Json;
using Mustask.Infrastructure.RichText;
using Xunit.Abstractions;
using static Mustask.Infrastructure.Tests.RichText.RichTextTestSupport;

namespace Mustask.Infrastructure.Tests.RichText;

/// <summary>
/// QA D-10 (third round): a systematic sweep, not another point fix. Every tag name the tree builder treats specially,
/// crossed with the ways text meets it (before, after, open, close, self-closing, in a table, in a cell, nested,
/// repeated), at a fixed size. The primary assertions are on counted work (the guard's two counters, which are
/// functions of the input alone, so the same on every host); the wall-time assertions are generous and only there to
/// show the counters did not miss anything (a quadratic shape the counters accepted would take tens of seconds).
/// </summary>
public sealed class ReducerTextWorkSweepTests(ITestOutputHelper output)
{
    // Every name the HTML tree builder gives a rule of its own, plus ordinary and unknown ones.
    private static readonly string[] Names =
    [
        "table", "thead", "tbody", "tfoot", "tr", "td", "th", "caption", "col", "colgroup",
        "select", "option", "optgroup", "template", "frameset", "frame", "head", "body", "html", "form", "plaintext",
        "noscript", "svg", "math", "a", "b", "i", "nobr", "big", "code", "em", "font", "s", "small", "strike", "strong",
        "tt", "u", "li", "dd", "dt", "p", "button", "div", "span", "ul", "ol", "dl", "h1", "h3", "h6", "pre", "listing",
        "hr", "br", "img", "input", "textarea", "iframe", "xmp", "object", "applet", "marquee", "address", "center",
        "details", "fieldset", "figure", "main", "menu", "nav", "section", "summary", "title", "style", "script", "base",
        "link", "meta", "rp", "rt", "rb", "rtc", "image", "isindex", "keygen", "param", "source", "track", "wbr",
        "bgsound", "embed", "area", "mark", "label", "foo", "a-b", "dir", "noframes", "noembed", "blockquote", "h4",
    ];

    // {0} is the tag name. Text before, after, open, close, self-closing, doubled, and inside a table, a row, a cell.
    private static readonly string[] Forms =
    [
        "<{0}>x", "x<{0}>", "<{0}/>x", "</{0}>x", "<{0}>x</{0}>y", "<{0}>x</{0}>", "<{0}><{0}>x", "<table><{0}>x",
        "<table>x<{0}>", "<table><tr><td><{0}>x", "<table><tr><td></{0}>x", "<p><{0}>x", "<table><tr><td>x</td></tr><{0}>x",
        "<table><{0}>x</table>y", "</{0}></{0}>x", "x</{0}>y</{0}>",
    ];

    public static IEnumerable<object[]> Shapes() =>
        Names.SelectMany(name => Forms.Select(form => string.Format(form, name))).Distinct().Select(piece => new object[] { piece });

    private const int SweepUnits = 400_000;

    private static string Repeat(string piece, int units) => string.Concat(Enumerable.Repeat(piece, units / piece.Length));

    [Theory]
    [MemberData(nameof(Shapes))]
    public void Sweep_EveryShapeIsRefusedByCountedWork_OrAnsweredWithinTheNfr(string piece)
    {
        var input = Repeat(piece, SweepUnits);

        // Counted work: what the guard measured (the limits are generous so the scan reads the whole input).
        var open = new RichTextGuardLimits
        {
            MaxLengthWithTreeBuilderMarkup = int.MaxValue,
            MaxTags = int.MaxValue,
            MaxTextAppendWork = long.MaxValue,
            MaxFosterTokenWork = long.MaxValue,
        };
        RichTextInputGuard.Scan(input, open, out var full);
        var guarded = RichTextInputGuard.Scan(input, RichTextGuardLimits.Default, out _);

        var limits = RichTextGuardLimits.Default;

        if (full.TextAppendWork > limits.MaxTextAppendWork || full.FosterTokenWork > limits.MaxFosterTokenWork)
        {
            Assert.False(guarded, $"{piece}: counted work {full.TextAppendWork}/{full.FosterTokenWork} passed the bound but the guard accepted it.");

            return;
        }

        var watch = Stopwatch.StartNew();
        var reduction = Reduce(input);
        watch.Stop();

        output.WriteLine($"{piece}: append={full.TextAppendWork} foster={full.FosterTokenWork} guard={guarded} exceeded={reduction.Exceeded} {watch.ElapsedMilliseconds} ms");

        // A shape whose counted work is within the bounds is answered quickly (a refusal by another bound counts too).
        // Generous: the figure on a quiet machine is under 1.5 s, a quadratic shape the counters missed is 10 s and more.
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(9), $"{piece} took {watch.Elapsed} with append={full.TextAppendWork} foster={full.FosterTokenWork}.");
    }

    // The four shapes of the QA probe, at the size that took 3 to 11 s: the counters see them (not the clock).
    [Theory]
    [InlineData("<td>x")]
    [InlineData("<tr>x")]
    [InlineData("<tbody>x")]
    [InlineData("<th>x")]
    [InlineData("<col>x")]
    [InlineData("<html>x")]
    [InlineData("<body>x")]
    [InlineData("</b>x")]
    [InlineData("</p></div>x")]
    public void TextSplitByIgnoredTokens_IsRefusedByTheTextAppendBound(string piece)
    {
        var input = Repeat(piece, 600_000);

        Assert.False(RichTextInputGuard.Scan(input, RichTextGuardLimits.Default, out var stats));
        Assert.True(stats.TextAppendWork > RichTextGuardLimits.Default.MaxTextAppendWork, $"append={stats.TextAppendWork}");
        Assert.True(Reduce(input).Exceeded);
    }

    // The table form: text after a closed table, found by the tree watcher's blind spot (D-10), counted from the tokens.
    [Theory]
    [InlineData("<table>x</table>y")]
    [InlineData("<table>x</table>")]
    [InlineData("<table><tr>x</tr></table>y")]
    [InlineData("<table><tbody>x<tr><td></td></tr></tbody></table><p>y</p>")]
    public void TextInATableOutsideItsCells_IsRefusedByTheFosterBound(string piece)
    {
        var input = Repeat(piece, 600_000);

        Assert.False(RichTextInputGuard.Scan(input, RichTextGuardLimits.Default, out var stats));
        Assert.True(stats.FosterTokenWork > RichTextGuardLimits.Default.MaxFosterTokenWork, $"foster={stats.FosterTokenWork}");
        Assert.True(Reduce(input).Exceeded);
    }

    // What the counters must not refuse: the shared fixtures' own inputs (canonical and legacy: the editor's output and
    // what was stored before it) count next to nothing, and a ceiling-sized canonical document stays far inside.
    [Theory]
    [InlineData("canonical.json")]
    [InlineData("legacy.json")]
    [InlineData("visible-count.json")]
    public void FixtureInputs_CountNextToNothing(string file)
    {
        var seen = 0;

        foreach (var fixture in Cases(file))
        {
            foreach (var property in new[] { "input", "canonical", "editorOutput", "html" })
            {
                if (!fixture.TryGetProperty(property, out var element) || element.ValueKind != JsonValueKind.String)
                {
                    continue;
                }

                seen++;
                var id = $"{fixture.GetProperty("id").GetString()}.{property}";

                Assert.True(RichTextInputGuard.Scan(element.GetString()!, RichTextGuardLimits.Default, out var stats), id);
                Assert.True(stats.TextAppendWork <= 50_000, $"{id}: append={stats.TextAppendWork}");
                Assert.True(property == "input" || stats.FosterTokenWork == 0, $"{id}: foster={stats.FosterTokenWork}");
            }
        }

        Assert.True(seen > 0);
    }

    [Theory]
    [InlineData("<p><strong>ab</strong><em>c</em><u>d</u> plain <a href=\"https://e.org\" rel=\"noopener noreferrer\">link</a> tail</p>", 27_000)]
    [InlineData("<p>a</p><ul><li><p>b</p><ul><li><p>c</p></li></ul></li></ul><pre><code>d</code></pre>", 11_000)]
    [InlineData("<table><tbody><tr><th><p>h</p></th><td><p>cell <strong>b</strong></p></td></tr></tbody></table><p>t</p>", 9_000)]
    [InlineData("<table>\n  <tbody>\n    <tr>\n      <td>cell</td>\n      <td>cell</td>\n    </tr>\n  </tbody>\n</table>\n", 9_000)]
    [InlineData("<h3>title</h3><p>text <mark>m</mark> <span class=\"rt-color-red\">r</span><br>next</p>", 12_000)]
    public void CeilingSizedEditorOutput_CountsFarInsideTheBounds(string piece, int repeats)
    {
        var input = string.Concat(Enumerable.Repeat(piece, repeats));

        Assert.True(RichTextInputGuard.Scan(input, RichTextGuardLimits.Default, out var stats));
        Assert.True(stats.TextAppendWork < 5_000_000, $"append={stats.TextAppendWork}");
        Assert.Equal(0, stats.FosterTokenWork);
    }

    // Case and attribute spelling do not hide a tag from the counters.
    [Theory]
    [InlineData("<TD>x")]
    [InlineData("<td class=\"a\" id=b>x")]
    [InlineData("<Td\n>x")]
    [InlineData("<TABLE>x</TABLE>y")]
    public void SpellingDoesNotHideAShape(string piece)
    {
        Assert.False(RichTextInputGuard.Scan(Repeat(piece, 800_000), RichTextGuardLimits.Default, out _));
    }
}
