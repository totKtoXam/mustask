using System.Diagnostics;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using Mustask.Infrastructure.RichText;
using static Mustask.Infrastructure.Tests.RichText.RichTextTestSupport;

namespace Mustask.Infrastructure.Tests.RichText;

/// <summary>
/// QA R-1, D-1, D-2: the deterministic structural bounds in front of the parser. Each answer is a function of the
/// input string alone (no clock, no host), so the same input gives the same answer everywhere.
/// </summary>
public sealed class RichTextInputGuardTests
{
    private static readonly RichTextGuardLimits Unlimited = new()
    {
        MaxLengthWithTreeBuilderMarkup = int.MaxValue,
        MaxTags = int.MaxValue,
        MaxAttributesPerTag = int.MaxValue,
        MaxAttributeSquares = long.MaxValue,
        MaxTextAppendWork = long.MaxValue,
        MaxFosterTokenWork = long.MaxValue,
    };

    private static GuardStats Stats(string text)
    {
        RichTextInputGuard.Scan(text, Unlimited, out var stats);

        return stats;
    }

    // Exactness of the tag syntax the guard counts attributes in: each of these is one tag with that many attributes.
    [Theory]
    [InlineData("<p>", 0)]
    [InlineData("<p a>", 1)]
    [InlineData("<p a b c>", 3)]
    [InlineData("<p a=1 b='2' c=\"3\">", 3)]
    [InlineData("<p a=\"1\"b=\"2\"c=\"3\">", 3)]
    [InlineData("<p a=\">\" b='>' c=>", 3)]
    [InlineData("<p a=\"<\" b=1<c d>", 3)]
    [InlineData("<p/a/b/c>", 3)]
    [InlineData("<p =a =b>", 1)] // the first '=' starts the name "=a"; the second is its value's '='
    [InlineData("<p a = 1 b= 2 c =3>", 3)]
    [InlineData("<p a='1\n2' b>", 2)]
    [InlineData("</p a b>", 2)]
    [InlineData("<p\ta\nb\fc\rd>", 4)]
    public void Scan_CountsAttributesAsTheTokenizerDoes(string html, int attributes)
    {
        Assert.Equal(attributes, Stats(html).MaxAttributes);
    }

    // The quotes cannot be used to hide a second tag's attributes: the value ends at its quote, the tag at its '>'.
    [Fact]
    public void Scan_AQuotedGreaterThan_DoesNotEndTheTag()
    {
        var stats = Stats("<p a=\">\" b c d><p e f>");

        Assert.Equal((2, 4), (stats.Tags, stats.MaxAttributes));
    }

    [Fact]
    public void Scan_BogusEndTag_RunsToTheNextGreaterThan_WithoutQuoting()
    {
        // "</ x \">" is a bogus comment ending at the first '>'; the <p b c d> after it is a real tag.
        var stats = Stats("</ x \"><p b c d>");

        Assert.Equal((1, 3), (stats.Tags, stats.MaxAttributes));
    }

    // Differential: whatever tag soup the generator builds, the guard counts at least the attributes the parser
    // ends up with on any element (the guard counts duplicates, the parser drops them). A guard that undercounted
    // would let a quadratic tag through.
    [Fact]
    public void Scan_NeverCountsFewerAttributesThanTheParserKeeps()
    {
        string[] pieces =
        [
            "<p", "<a", "<div", "<span", "<b", "</p", "</a", "</div", " ", "  ", "\n", "\t", "/", "=", "\"", "'", ">", ">", "<", "a", "b", "c",
            "a=1", "b=\"2\"", "c='3'", "href=\"x\"", "class=\"y\"", "data-x", "a=\">\"", "='", "\"x\"y",
        ];

        var parser = new HtmlParser(new HtmlParserOptions { IsScripting = true });

        foreach (var seed in new[] { 11, 12, 13 })
        {
            var random = new Random(seed);

            for (var round = 0; round < 700; round++)
            {
                var builder = new System.Text.StringBuilder();

                for (var index = random.Next(2, 40); index > 0; index--)
                {
                    builder.Append(pieces[random.Next(pieces.Length)]);
                }

                var html = builder.ToString();
                var stats = Stats(html);
                var document = parser.ParseDocument("<!DOCTYPE html><html><head></head><body>" + html);
                var kept = document.All.Where(element => element.LocalName is not ("html" or "head" or "body"))
                    .Select(element => element.Attributes.Length)
                    .DefaultIfEmpty(0)
                    .Max();

                Assert.True(stats.MaxAttributes >= kept, $"seed {seed} round {round}: guard {stats.MaxAttributes} < parser {kept} for: {html}");
            }
        }
    }

    // D-2: one tag with many distinct attribute names is refused by count, before the parser reads it.
    [Theory]
    [InlineData(64, true)]
    [InlineData(65, false)]
    [InlineData(190_000, false)]
    public void IsWithinBounds_AttributesPerTag(int attributes, bool within)
    {
        var html = "<p " + string.Concat(Enumerable.Range(0, attributes).Select(i => $"a{i}=1 ")) + ">x</p>";

        Assert.Equal(within, RichTextInputGuard.IsWithinBounds(html, RichTextGuardLimits.Default));
    }

    // The attribute work in total: many tags that are each within the per-tag bound.
    [Fact]
    public void IsWithinBounds_AttributeSquares_AreBoundedInTotal()
    {
        var tag = "<p " + string.Concat(Enumerable.Range(0, 64).Select(i => $"a{i} ")) + ">";

        Assert.True(RichTextInputGuard.IsWithinBounds(string.Concat(Enumerable.Repeat(tag, 1_900)), RichTextGuardLimits.Default));
        Assert.False(RichTextInputGuard.IsWithinBounds(string.Concat(Enumerable.Repeat(tag, 2_000)), RichTextGuardLimits.Default));
    }

    // D-1 / D-9: the guard does not count preformatted start tags as a bound (it only reports them): what caps the
    // tree builder's recursion is the nesting bound of the tree watcher, tested on the reducer below.
    [Fact]
    public void Scan_ReportsPreformattedStartTags_ButDoesNotBoundThem()
    {
        var html = string.Concat(Enumerable.Repeat("<pre><code>x</code></pre><LISTING>y</listing>", 2_000));

        Assert.True(RichTextInputGuard.Scan(html, RichTextGuardLimits.Default, out var stats));
        Assert.Equal(4_000, stats.Preformatted);
    }

    [Theory]
    [InlineData(1_001)]
    [InlineData(2_000)]
    [InlineData(10_000)]
    public void Reduce_ManySequentialClosedCodeBlocks_AreAccepted(int count)
    {
        var html = string.Concat(Enumerable.Repeat("<pre><code>x</code></pre>", count));

        var reduction = Reduce(html);

        Assert.False(reduction.Exceeded);
        Assert.Equal(html, reduction.Html);
    }

    // The recursion is capped by nesting: a run of unclosed ones is refused at 160, closed ones are not counted.
    [Theory]
    [InlineData("<pre>", 150, false)]
    [InlineData("<pre>", 161, true)]
    [InlineData("<listing>", 161, true)]
    [InlineData("<template>", 161, true)]
    [InlineData("<pre><listing>", 100_000, true)]
    public void Reduce_RunOfUnclosedPreformattedStartTags_IsCappedByTheNestingBound(string piece, int count, bool refused)
    {
        var html = string.Concat(Enumerable.Repeat(piece, count)) + "x";

        Assert.Equal(refused, Reduce(html).Exceeded);
    }

    // Markup whose handling depends on the tree builder is read only when short; plain allow-list markup is read
    // at any size up to the other bounds.
    [Theory]
    [InlineData("<!-- c -->")]
    [InlineData("<!DOCTYPE html>")]
    [InlineData("<?xml?>")]
    [InlineData("<script>x</script>")]
    [InlineData("<STYLE>x</STYLE>")]
    [InlineData("<svg><g></g></svg>")]
    [InlineData("<math></math>")]
    [InlineData("<textarea></textarea>")]
    [InlineData("<template></template>")]
    [InlineData("<select><option>a</select>")]
    [InlineData("<frameset></frameset>")]
    [InlineData("<noscript></noscript>")]
    [InlineData("<title>x</title>")]
    [InlineData("<plaintext>")]
    [InlineData("<iframe></iframe>")]
    public void IsWithinBounds_TreeBuilderMarkup_IsReadOnlyWhenShort(string markup)
    {
        var padding = new string('x', 8_000);

        Assert.True(RichTextInputGuard.IsWithinBounds("<p>" + markup + "</p>", RichTextGuardLimits.Default));
        Assert.False(RichTextInputGuard.IsWithinBounds("<p>" + markup + padding + "</p>", RichTextGuardLimits.Default));
        Assert.False(RichTextInputGuard.IsWithinBounds("<p>" + padding + "</p>" + markup, RichTextGuardLimits.Default));
    }

    [Fact]
    public void IsWithinBounds_AllowListMarkupAtTheCeiling_IsWithin()
    {
        var html = string.Concat(Enumerable.Repeat("<p><strong>a</strong><em>b</em></p>", 30_000));

        Assert.True(html.Length > 1_000_000);
        Assert.True(RichTextInputGuard.IsWithinBounds(html, RichTextGuardLimits.Default));
    }

    [Fact]
    public void IsWithinBounds_TagCount()
    {
        Assert.True(RichTextInputGuard.IsWithinBounds(string.Concat(Enumerable.Repeat("<b></b>", 175_000)), RichTextGuardLimits.Default));
        Assert.False(RichTextInputGuard.IsWithinBounds(string.Concat(Enumerable.Repeat("<b></b>", 175_001)), RichTextGuardLimits.Default));
    }

    // QA D-8: text and inline content that meets a table is foster-parented before it, and the DOM's insertion
    // before a node is linear in the parent's children. The bound counts that work from the tree, deterministically.
    [Theory]
    [InlineData("<table>x")]
    [InlineData("<table><b>")]
    [InlineData("<table><a>")]
    [InlineData("<b><table>")]
    [InlineData("<a><table>")]
    [InlineData("<p><table>")]
    [InlineData("<table><tr>y<td>")]
    [InlineData("x<table>")]
    public void Reduce_FosterParentedContentBetweenTables_IsBoundedAndDeterministic(string piece)
    {
        // 2 000 000 units, the request cap: the QA repros ran 1 000 000 of these into the 20 s safety net.
        var input = string.Concat(Enumerable.Repeat(piece, 2_000_000 / piece.Length));

        var watch = Stopwatch.StartNew();
        var first = Reduce(input);
        watch.Stop();

        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(2.5), $"{piece} took {watch.Elapsed}.");
        Assert.Equal(first.Exceeded, Reduce(input).Exceeded);
    }

    // Foster text that merges into the text node already before a table adds no child, so growth does not see it; the
    // cost of it must still be small however many blocks precede the table.
    [Theory]
    [InlineData("<tr>y")]
    [InlineData("<td>y")]
    [InlineData("y")]
    [InlineData("<b>y</b>")]
    public void Reduce_ManyFosterTokensIntoOneTable_AfterManyBlocks_IsBounded(string piece)
    {
        var input = string.Concat(Enumerable.Repeat("<p>a</p>", 40_000)) + "<table>" + string.Concat(Enumerable.Repeat(piece, 300_000 / piece.Length));

        var watch = Stopwatch.StartNew();
        var reduction = Reduce(input);
        watch.Stop();

        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(2.5), $"{piece} took {watch.Elapsed}.");
        Assert.Equal(reduction.Exceeded, Reduce(input).Exceeded);
    }

    // Many tables as canonical output has them are not foster parenting and are not charged.
    [Theory]
    [InlineData(17_000)]
    [InlineData(17_800)] // 996 800 units, just under the ceiling
    public void Reduce_ManySmallCanonicalTables_AreAccepted(int count)
    {
        var html = string.Concat(Enumerable.Repeat("<table><tbody><tr><td><p>x</p></td></tr></tbody></table>", count));

        Assert.False(Reduce(html).Exceeded);
    }

    // Tables between paragraphs, the same pretty-printed, and a few thousand loose-text tables: all read.
    [Fact]
    public void Reduce_TablesBetweenParagraphs_AndModestLooseText_AreAccepted()
    {
        var between = string.Concat(Enumerable.Repeat("<p>a</p>\n<table><tr><td>x</td></tr></table>\n", 8_000));
        var loose = string.Concat(Enumerable.Repeat("text<table><tr><td>x</td></tr></table>", 2_000));

        Assert.False(Reduce(between).Exceeded);
        Assert.False(Reduce(loose).Exceeded);
    }

    // The tree watcher: the element count is read from the parser's own factory, deterministically. The most a
    // ceiling-sized document can hold is 142 857 elements (7 units the cheapest); 142 000 empty paragraphs read fine.
    [Fact]
    public void Reduce_ElementCountBound_IsDeterministic()
    {
        var atCeiling = Reduce(string.Concat(Enumerable.Repeat("<p></p>", 142_000)));

        Assert.False(atCeiling.Exceeded);
        Assert.Equal(994_000, atCeiling.MarkupLength);

        // 300 001 void elements: 1.2 million units, 300 002 tags (within the tag bound), one element too many.
        Assert.True(Reduce("<p>" + string.Concat(Enumerable.Repeat("<br>", 300_001)) + "</p>").Exceeded);
    }

    // The work bound: the sum of nesting over elements. 100 000 elements under 100 levels is refused; the same
    // elements at the nesting of a real document are read.
    [Fact]
    public void Reduce_TreeWorkBound_IsDeterministic()
    {
        var deep = string.Concat(Enumerable.Repeat("<div>", 100)) + string.Concat(Enumerable.Repeat("<p>x</p>", 100_000));
        var shallow = string.Concat(Enumerable.Repeat("<div>", 10)) + string.Concat(Enumerable.Repeat("<p>x</p>", 100_000));

        Assert.True(Reduce(deep).Exceeded);
        Assert.False(Reduce(shallow).Exceeded);
    }

    // D-1 as a class: every HTML element name, flat and repeated, goes through the reducer without the process
    // dying and in bounded time; whatever recursion the tree builder has for one is ended by the nesting bound.
    public static IEnumerable<object[]> TagNames() =>
        ("a abbr acronym address applet area article aside audio b base basefont bdi bdo bgsound big blink blockquote body br button canvas caption "
        + "center cite code col colgroup content data datalist dd del details dfn dialog dir div dl dt element em embed fieldset figcaption figure "
        + "font footer form frame frameset h1 h2 h3 h4 h5 h6 head header hgroup hr html i iframe image img input ins isindex kbd keygen label legend "
        + "li link listing main map mark marquee math menu menuitem meta meter multicol nav nextid nobr noembed noframes noscript object ol optgroup "
        + "option output p param picture plaintext pre progress q rb rp rt rtc ruby s samp script search section select slot small source span strike "
        + "strong style sub summary sup svg table tbody td template textarea tfoot th thead time title tr track tt u ul var video wbr xmp")
            .Split(' ')
            .Select(name => new object[] { name });

    [Theory]
    [MemberData(nameof(TagNames))]
    public void Reduce_EveryElementName_FloodedFlat_IsAnsweredWithoutACrash(string name)
    {
        foreach (var html in new[]
        {
            string.Concat(Enumerable.Repeat($"<{name}>", 3_000)) + "x",
            string.Concat(Enumerable.Repeat($"<{name}>\n", 3_000)) + "x",
            "<pre>" + string.Concat(Enumerable.Repeat($"<{name}>", 3_000)),
            string.Concat(Enumerable.Repeat($"<p><{name}>", 3_000)),
            string.Concat(Enumerable.Repeat($"<b><{name}></b>", 3_000)),
            string.Concat(Enumerable.Repeat($"</{name}>", 3_000)),
        })
        {
            var watch = Stopwatch.StartNew();
            var reduction = Reduce(html);
            watch.Stop();

            Assert.NotNull(reduction);
            Assert.True(watch.Elapsed < TimeSpan.FromSeconds(5), $"<{name}> took {watch.Elapsed}.");
        }
    }
}
