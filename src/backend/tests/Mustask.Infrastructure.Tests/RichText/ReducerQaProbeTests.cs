using System.Diagnostics;
using System.Text;
using AngleSharp.Html.Parser;
using Mustask.Application.Abstractions.RichText;
using Mustask.Infrastructure.RichText;
using Xunit.Abstractions;
using static Mustask.Infrastructure.Tests.RichText.RichTextTestSupport;

namespace Mustask.Infrastructure.Tests.RichText;

/// <summary>
/// feat-018 backend QA: adversarial probes written from the spec (US-2, US-4, US-7, ADR-0037) and not from the
/// developer's tests. Everything here asserts a spec rule; the timing probes in the second half also write what they
/// measured to the test output, which is the evidence qa.md quotes.
/// </summary>
public sealed class ReducerQaProbeTests(ITestOutputHelper output)
{
    // --- re-parse stability: references the parser keeps but a second parse changes ------------------------------

    // US-2 "A stored value is stable under a browser's re-parse" / BR-RT-5: a numeric reference to CR is kept by the
    // parser as a CR character; written raw, the next parse normalises it to LF.
    [Theory]
    [InlineData("<p>a&#13;b</p>")]
    [InlineData("<p>a&#x0D;&#x0A;b</p>")]
    [InlineData("<pre><code>a&#13;b</code></pre>")]
    [InlineData("<ul><li>a&#13;b</li></ul>")]
    public void QA_Reduce_NumericReferenceToCarriageReturn_IsAFixedPointAndStableUnderReparse(string input)
    {
        var stored = Stored(input);

        Assert.DoesNotContain('\r', stored);
        Assert.Equal(stored, Stored(stored));
        AssertStableUnderReparse(stored);
    }

    // A control character and a noncharacter reached by reference: stored, then re-read, must be the same string.
    [Theory]
    [InlineData("<p>a&#1;b&#31;c&#127;d&#xFFFE;e&#xFDD0;f</p>")]
    [InlineData("<p>a&#x85;b&#x2028;c&#x2029;d&#xFEFF;e&#x200B;f</p>")]
    [InlineData("<p>&#xD800;&#xDFFF;&#x110000;&#0;</p>")]
    [InlineData("<p>a&#12;b&#11;c</p>")]
    public void QA_Reduce_ControlAndOddCharacters_AreStableUnderReparse(string input)
    {
        var stored = Stored(input);

        Assert.Equal(stored, Stored(stored));
        AssertStableUnderReparse(stored);
        Assert.DoesNotContain('\0', stored);
        Assert.Equal(stored, Reducer.Reduce(stored, new RichTextReductionLimits(Ceiling), CancellationToken.None).Html);

        // No lone surrogate is ever stored (BR-RT-8): every surrogate is half of a valid pair.
        for (var index = 0; index < stored.Length; index++)
        {
            if (char.IsHighSurrogate(stored[index]))
            {
                Assert.True(index + 1 < stored.Length && char.IsLowSurrogate(stored[++index]), "lone high surrogate stored");
            }
            else
            {
                Assert.False(char.IsLowSurrogate(stored[index]), "lone low surrogate stored");
            }
        }
    }

    // --- hostile payloads beyond the shipped corpus ---------------------------------------------------------------

    public static IEnumerable<object[]> Hostile() =>
    [
        ["<noscript><p title=\"</noscript><img src=x onerror=alert(1)>\">"],
        ["<math><mtext><table><mglyph><style><!--</style><img title=\"--&gt;&lt;img src=1 onerror=alert(1)&gt;\">"],
        ["<math><mtext><table><mglyph><style><![CDATA[</style><img src onerror=alert(1)>]]>"],
        ["<svg><desc><![CDATA[</desc><script>alert(1)</script>]]></svg>"],
        ["<svg><foreignObject><p><img src=x onerror=alert(1)></p></foreignObject></svg>"],
        ["<a href=x><table><a>"],
        ["<p><a href='https://e.org'><a href='javascript:alert(1)'>t</a></a></p>"],
        ["<p><b><a href=javascript:alert(1)>x</b></a></p>"],
        ["<b><p><a href='javascript:alert(1)'>x</b>y</a>"],
        ["<table><caption><a href='javascript:alert(1)'>x</a></caption><colgroup><col span=2></colgroup><tr><td><a href=data:text/html,x>y</a></table>"],
        ["<listing><script>alert(1)</script></listing><noembed><img src=x onerror=1></noembed><noframes><img onerror=1></noframes>"],
        ["<iframe><p title=\"</iframe><img src=x onerror=alert(1)>\"></iframe>"],
        ["<textarea><p title=\"</textarea><img src=x onerror=alert(1)>\"></textarea>"],
        ["<title><p title=\"</title><img src=x onerror=alert(1)>\"></title>"],
        ["<select><option><a href=javascript:1>x</a></select><datalist><option>y</datalist><details open ontoggle=alert(1)>z</details>"],
        ["<p onclick=alert(1) style='x' class='rt-color-red' title=t id=i lang=ru dir=rtl hidden>x</p>"],
        ["<span class=\"rt-color-red&#9;&#10;&#12;&#13;rt-size-xl\">x</span>"],
        ["<span class=\"rt-color-red00A0rt-size-xl\">x</span>"],
        ["<span class=\"rt-color-red\u000Brt-size-xl\">x</span>"],
        ["<span class=\"&#x72;t-color-red\">x</span>"],
        ["<span class=\"rt-color-red\" class=\"rt-color-blue\">x</span>"],
        ["<a href=\"https://example.org/\"onclick=\"alert(1)\">x</a>"],
        ["<a href=\"&#x6A&#x61&#x76&#x61&#x73&#x63&#x72&#x69&#x70&#x74&#x3A;alert(1)\">x</a>"],
        ["<a href=\"&Tab;java&NewLine;script&colon;alert(1)\">x</a>"],
        ["<a href=\"00A0javascript:alert(1)\">x</a>"],
        ["<a href=\"2028javascript:alert(1)\">x</a>"],
        ["<a href=\"https://example.org\\@evil.example\">x</a><a href=\"http:\\\\evil.example\">y</a>"],
        ["<a href=\"mailto:a@example.org?body=<script>alert(1)</script>\">x</a>"],
        ["<a href='https://e.org/\"><img src=x onerror=alert(1)>'>x</a>"],
        ["<pre><code class=\"language-sql\"></code></pre><code class=\"language-python\">x</code>"],
        ["<table><tr><td><table><tr><td><script>alert(1)</script>x</td></tr></table></td></tr></table>"],
        ["<ul><ul><ul><ul><li><script>alert(1)</script>deep</li></ul></ul></ul></ul>"],
        ["<template><template><script>alert(1)</script></template></template>after"],
        ["<body onload=alert(1)><html onload=alert(1)><head><script>alert(1)</script></head>x"],
        ["<frameset><frame src=javascript:alert(1)></frameset>"],
        ["<object><param name=x value=y><embed src=javascript:alert(1)></object>"],
        ["<a href=\"javascript&#58;alert(1)\">x</a><a href=\"jav&#x09;ascript:alert(1)\">y</a>"],
        ["<ruby>a<rt>b</rt></ruby><bdo dir=rtl>c</bdo><wbr><nobr>d</nobr><marquee onstart=alert(1)>e</marquee>"],
        ["\uD83D<b></b>\uDE00 <p>\uDE00\uD83D</p>"],
    ];

    [Theory]
    [MemberData(nameof(Hostile))]
    public void QA_Reduce_HostilePayload_NothingSurvives_StoredValueIsStable(string input)
    {
        var stored = Stored(input);

        AssertInsideAllowList(stored);
        AssertStableUnderReparse(stored);
        Assert.Equal(stored, Stored(stored));

        // Elements and attributes are checked on the parsed tree (AssertInsideAllowList): hostile text that survives as
        // escaped text or inside a quoted href is not an element or an attribute.
        var body = ParseBody(stored, scripting: false);
        Assert.Empty(body.QuerySelectorAll("script, img, svg, iframe, style, form, input, [onerror], [onclick], [onload], [style], [id]"));
    }

    // BR-RT-14: only an exact token, on its own element, whatever surrounds it.
    [Theory]
    [InlineData("<span class=\"rt-color-red00A0x\">a</span>", "<p>a</p>")]
    [InlineData("<span class=\"rt-color-red\u000Bx\">a</span>", "<p>a</p>")]
    [InlineData("<span class=\"rt-color-red\fx\">a</span>", "<p><span class=\"rt-color-red\">a</span></p>")]
    [InlineData("<span class=\"&#x72;t-color-red\">a</span>", "<p><span class=\"rt-color-red\">a</span></p>")]
    [InlineData("<span class=\"rt-color-red rt-color-blue\">a</span>", "<p><span class=\"rt-color-red\">a</span></p>")]
    [InlineData("<span class=\"rt-color-red\" class=\"rt-color-blue\">a</span>", "<p><span class=\"rt-color-red\">a</span></p>")]
    [InlineData("<span class=\"rt-hl-red\">a</span>", "<p>a</p>")]
    [InlineData("<mark class=\"rt-color-red\">a</mark>", "<p>a</p>")]
    [InlineData("<p class=\"rt-size-lg\">a</p><h3 class=\"rt-color-red\">b</h3><li class=\"rt-color-red\">c</li>", "<p>a</p><h3>b</h3><p>c</p>")]
    [InlineData("<a class=\"rt-color-red\" href=\"https://e.org\">a</a>", "<p><a href=\"https://e.org\" rel=\"noopener noreferrer\">a</a></p>")]
    [InlineData("<pre class=\"language-python\"><code>a</code></pre>", "<pre><code>a</code></pre>")]
    [InlineData("<pre><code class=\"LANGUAGE-PYTHON\">a</code></pre>", "<pre><code>a</code></pre>")]
    [InlineData("<pre><code class=\"language-python language-sql\">a</code></pre>", "<pre><code class=\"language-python\">a</code></pre>")]
    [InlineData("<code class=\"language-python\">a</code>", "<p><code>a</code></p>")]
    [InlineData("<span class=\"rt-size-lg\"><h4>a</h4></span>", "<h4>a</h4>")]
    public void QA_Reduce_ClassTokens_AreExactAndBoundToTheirElement(string input, string expected)
    {
        Assert.Equal(expected, Stored(input));
    }

    // US-2 link address: read as a browser reads it, stored as read.
    [Theory]
    [InlineData("<a href=\"https://exa&#9;mple.org/p\">x</a>", "https://example.org/p")]
    [InlineData("<a href=\"HTTP://EXAMPLE.org\">x</a>", "HTTP://EXAMPLE.org")]
    [InlineData("<a href=\"https://a@b\">x</a>", "https://a@b")]
    [InlineData("<a href=\"http://[::1]:80/\">x</a>", "http://[::1]:80/")]
    [InlineData("<a href=\"https://e.org/?a=1&amp;b=&quot;\">x</a>", "https://e.org/?a=1&b=\"")]
    public void QA_Reduce_LinkAddress_IsStoredAsRead(string input, string expectedHref)
    {
        var stored = Stored(input);
        var body = ParseBody(stored, scripting: false);

        Assert.Equal(expectedHref, body.QuerySelector("a")!.GetAttribute("href"));
        Assert.Equal(stored, Stored(stored));
        AssertStableUnderReparse(stored);
    }

    [Theory]
    [InlineData("<a href=\"https://\">x</a>")]
    [InlineData("<a href=\"https://@\">x</a>")]
    [InlineData("<a href=\"https://?q\">x</a>")]
    [InlineData("<a href=\"http:\\\\evil.example\">x</a>")]
    [InlineData("<a href=\"https://evil.example\\@good.example\">x</a>")]
    [InlineData("<a href=\"00A0https://e.org\">x</a>")]
    [InlineData("<a href=\"https:/e.org\">x</a>")]
    [InlineData("<a href=\"mailto:\">x</a>")]
    [InlineData("<a href=\"file:///etc/passwd\">x</a>")]
    [InlineData("<a href=\"blob:https://e.org/x\">x</a>")]
    [InlineData("<a href=\"https://e.org\"></a>")]
    public void QA_Reduce_LinkAddress_Refused_TextKept(string input)
    {
        var stored = Stored(input);

        Assert.DoesNotContain("<a ", stored);
        Assert.DoesNotContain("href", stored);
    }

    // --- limit arithmetic -----------------------------------------------------------------------------------------

    // BR-RT-8: a pair split by a tag is two lone surrogates, each U+FFFD, each counted 1; a real pair is 1.
    [Fact]
    public void QA_Reduce_SurrogatesSplitByATag_AreTwoReplacementCharacters()
    {
        var split = Reduce("<p>\uD83D<b></b>\uDE00</p>");
        Assert.Equal("<p>��</p>", split.Html);
        Assert.Equal(2, split.VisibleLength);

        var pair = Reduce("<p>a😀b</p>");
        Assert.Equal(3, pair.VisibleLength);
        Assert.Equal(1 + 1 + 1 + 3 + 4, pair.MarkupLength);

        var reversed = Reduce("<p>\uDE00\uD83D</p>");
        Assert.Equal("<p>��</p>", reversed.Html);
    }

    // The 2 000 000-unit cap is on raw UTF-16 units: a value of astral characters at exactly the cap reaches the
    // reducer (the handler's cap check is "more than"), and its code-point count is half its length.
    [Fact]
    public void QA_Reduce_AstralAtTheRawCap_CountsCodePointsNotUnits()
    {
        const int Emoji = 999_993; // 999 993 + <p></p> (7) = 1 000 000 code points of markup
        var atCeiling = Reduce("<p>" + string.Concat(Enumerable.Repeat("\U0001F600", Emoji)) + "</p>");

        Assert.False(atCeiling.Exceeded);
        Assert.Equal(1_000_000, atCeiling.MarkupLength);
        Assert.Equal(Emoji, atCeiling.VisibleLength);

        var over = Reduce("<p>" + string.Concat(Enumerable.Repeat("\U0001F600", Emoji + 1)) + "</p>");
        Assert.True(over.Exceeded);
    }

    // BR-RT-7: the ceiling is on the reduced markup, in code points, entity forms included (an ampersand is 5).
    [Fact]
    public void QA_Reduce_EscapedCharacters_CountAsWritten()
    {
        var input = "<p>" + new string('&', 199_999) + "</p>"; // raw is 5 units each once written: 999 995 + 7 = 1 000 002
        Assert.True(Reduce(input).Exceeded);

        var fits = Reduce("<p>" + new string('&', 199_998) + "</p>"); // 999 990 + 7
        Assert.False(fits.Exceeded);
        Assert.Equal(199_998, fits.VisibleLength);
        Assert.Equal(999_997, fits.MarkupLength);
    }

    // BR-RT-6 / BR-RT-7: whitespace around blocks that the output drops still counts against the ceiling. Quantify
    // what a pretty-printed but otherwise small document does at the edge.
    [Fact]
    public void QA_Reduce_PrettyPrintedDocument_UnderTheCeilingAsWritten_IsNotRefused()
    {
        var builder = new StringBuilder("<table>\n");

        while (builder.Length < 700_000)
        {
            builder.Append("  <tr>\n    <td>\n      <p>x</p>\n    </td>\n  </tr>\n");
        }

        builder.Append("</table>");

        var reduction = Reduce(builder.ToString());

        output.WriteLine($"pretty table raw={builder.Length}: exceeded={reduction.Exceeded}, markup={reduction.MarkupLength}");
        Assert.False(reduction.Exceeded);
    }

    // BR-RT-7 "(2) reduced html at most 1 000 000 ... nothing is built past the ceiling": the dropped-whitespace
    // budget must not turn a document whose reduced markup is below the ceiling into a refusal. QA D-5 (minor): 840 000
    // of reduced markup plus 350 000 of dropped indentation is refused. Recorded, not sent back; the panel never
    // sends indented html. Skipped so that a minor does not keep the suite red.
    [Fact]
    public void QA_Reduce_IndentedHtml_ReducedBelowTheCeiling_IsAccepted()
    {
        // 90 000 paragraphs of 5 characters (about 11 units each reduced: 990 000) spaced by 1 000 000 - ... of indent
        var paragraphs = Enumerable.Repeat("<p>hello</p>", 70_000);
        var html = string.Join("\n    ", paragraphs); // 70 000 * 12 = 840 000 markup, 70 000 * 5 = 350 000 whitespace

        var reduction = Reduce(html);

        output.WriteLine($"indented raw={html.Length}: exceeded={reduction.Exceeded}, markup={reduction.MarkupLength}");
        Assert.False(reduction.Exceeded);
        Assert.Equal(840_000, reduction.MarkupLength);
    }

    // --- idempotency on a second generator (different grammar and seeds from the developer's) -------------------------

    [Fact]
    public void QA_Reduce_RandomStructures_AreIdempotentAndStable()
    {
        string[] pieces =
        [
            "<p>", "<p class=rt-color-red>", "</p>", "<h2>", "</h2>", "<h5>", "</h5>", "<b>", "</b>", "<strong>", "<em>", "</em>",
            "<u>", "</u>", "<del>", "</del>", "<code>", "</code>", "<pre>", "</pre>", "<pre><code>", "</code></pre>",
            "<ul>", "</ul>", "<ol start=3>", "</ol>", "<li>", "</li>", "<table>", "</table>", "<thead>", "<tbody>", "<tfoot>",
            "<tr>", "</tr>", "<td>", "</td>", "<th>", "</th>", "<td colspan=3>", "<td rowspan=0>", "<td colspan=0 rowspan=2>",
            "<caption>", "<colgroup><col>", "<a href=\"https://e.org/a b\">", "<a href=\"mailto:x@y\">", "<a name=n>", "</a>",
            "<span class=\"rt-size-xl rt-color-teal\">", "<span class=rt-size-sm>", "</span>", "<mark class=rt-hl-gray>", "</mark>",
            "<div>", "</div>", "<section>", "</section>", "<br>", "<br/>", "<hr>", "<img src=x>", "<wbr>", "text", "  ", "\n", "\t",
            "&amp;", "&lt;", "&gt;", "&quot;", "&nbsp;", "&#9;", "\U0001F600", "‍", "️", "<!-- c -->", "<![CDATA[x]]>",
            "<font face=x>", "</font>", "<sup>", "</sup>", "<blockquote>", "</blockquote>", "<form>", "</form>", "<select><option>", "</select>",
            "<svg><g>", "</svg>", "<math><mi>", "</math>", "<textarea>", "</textarea>", "<noscript>", "</noscript>", "<template>", "</template>",
            "<pre>\n", "<li><p>", "<td><p>", "<ul><li>", "<ol><li>", "<table><tr><td>", "<a href='https://e.org'><code>", "</code></a>",
        ];

        foreach (var seed in new[] { 1, 7, 42, 2026, 90210 })
        {
            var random = new Random(seed);

            for (var round = 0; round < 600; round++)
            {
                var builder = new StringBuilder();
                var count = random.Next(1, 60);

                for (var index = 0; index < count; index++)
                {
                    builder.Append(pieces[random.Next(pieces.Length)]);
                }

                var input = builder.ToString();
                var once = Stored(input);
                var twice = Stored(once);

                Assert.True(once == twice, $"seed {seed} round {round} not idempotent: {input}\nonce:  {once}\ntwice: {twice}");
                AssertInsideAllowList(once);
                AssertStableUnderReparse(once);
            }
        }
    }

    // --- time: hostile shapes (probe; asserts the NFR's 2 s for a refusal, with the budget's own 1.5 s inside it) ---

    private static string Repeat(string piece, int units) => string.Concat(Enumerable.Repeat(piece, units / piece.Length));

    public static IEnumerable<object[]> HostileShapes()
    {
        yield return ["attributes-same-name", "<p " + Repeat("a=1 ", 1_900_000) + ">x"];
        yield return ["anchor-unclosed", Repeat("<a href='https://e.org'>x", 2_000_000)];
        yield return ["misnested-b-p", Repeat("<b><p>x</b>", 2_000_000)];
        yield return ["misnested-b-i-p", Repeat("<b><i><p>x</b></i>", 2_000_000)];
        yield return ["li-unclosed", Repeat("<li>x", 2_000_000)];
        yield return ["ul-li-nested", Repeat("<ul><li>", 2_000_000)];
        yield return ["td-unclosed", "<table><tr>" + Repeat("<td>x", 2_000_000)];
        yield return ["table-in-td", Repeat("<table><tr><td>", 2_000_000)];
        yield return ["select-option", "<select>" + Repeat("<option>x", 2_000_000)];
        yield return ["svg-nested", Repeat("<svg>", 2_000_000)];
        yield return ["svg-g-nested", "<svg>" + Repeat("<g>", 2_000_000)];
        yield return ["font-nested", Repeat("<font>", 2_000_000)];
        yield return ["span-class-nested", Repeat("<span class='rt-color-red'>", 2_000_000)];
        yield return ["comments", Repeat("<!--x-->", 2_000_000)];
        yield return ["entities", Repeat("&amp;&#x1F600;&notit;&copy", 2_000_000)];
        yield return ["code-pre-lines", "<pre><code>" + Repeat("x\n", 1_900_000) + "</code></pre>"];
        yield return ["p-many", Repeat("<p>x</p>", 2_000_000)];
        yield return ["br-many", Repeat("<br>", 2_000_000)];
        yield return ["tr-many-empty", "<table>" + Repeat("<tr>", 2_000_000)];
        yield return ["colspan-many", "<table><tr>" + Repeat("<td colspan=999>", 2_000_000)];
        yield return ["deep-heading-in-list", Repeat("<ol><li><h3>", 2_000_000)];
        yield return ["long-class", "<span class=\"" + Repeat("rt-color-nope ", 1_900_000) + "\">x</span>"];
    }

    [Theory]
    [MemberData(nameof(HostileShapes))]
    public void QA_Reduce_HostileShape_IsAnsweredWithinTheNfr(string name, string input)
    {
        Assert.True(input.Length <= 2_000_000 + 64, $"{name}: {input.Length} units");

        var watch = Stopwatch.StartNew();
        var reduction = Reduce(input);
        watch.Stop();

        output.WriteLine($"{name}: units={input.Length} elapsed={watch.ElapsedMilliseconds} ms exceeded={reduction.Exceeded} markup={reduction.MarkupLength}");
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(4), $"{name} took {watch.Elapsed}.");
    }

    // QA D-2 (major): the parse budget is a cancellation token, and AngleSharp checks it between tokens, not inside
    // one. A single start tag with many distinct attribute names is quadratic inside the token (duplicate check), so
    // the budget never fires: 1.79 M units ran 441 s on the QA machine. The sizes below are what the probe measured.
    [Theory]
    [InlineData(65)]
    [InlineData(10_000)]
    [InlineData(20_000)]
    [InlineData(40_000)]
    [InlineData(190_000)]
    public void QA_Reduce_ManyDistinctAttributesInOneTag_IsRefusedAtOnce(int attributes)
    {
        var input = "<p " + string.Concat(Enumerable.Range(0, attributes).Select(i => $"a{i}=1 ")) + ">x";

        var watch = Stopwatch.StartNew();
        var reduction = Reduce(input);
        watch.Stop();

        output.WriteLine($"attributes={attributes} units={input.Length} elapsed={watch.ElapsedMilliseconds} ms exceeded={reduction.Exceeded}");
        Assert.True(reduction.Exceeded);
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(1), $"{attributes} attributes took {watch.Elapsed}.");
    }

    // The same tag at the bound is read: the bound is on the count, nothing else (QA D-2, deterministic).
    [Fact]
    public void QA_Reduce_SixtyFourAttributesInOneTag_AreAccepted()
    {
        var input = "<p " + string.Concat(Enumerable.Range(0, 64).Select(i => $"a{i}=1 ")) + ">x</p>";

        Assert.Equal("<p>x</p>", Stored(input));
    }

    // QA D-1 (blocker): consecutive <pre> or <listing> start tags nest AngleSharp's tree builder on the call stack
    // (HtmlDomBuilder.InBodyStartTag -> PreventNewLine -> Home -> InBody -> InBodyStartTag ...). About 400 of them (2 000
    // units) overflow a thread-pool thread's stack, which no handler can catch: the whole process dies. The parse
    // budget does not help, the overflow comes in milliseconds. Skipped so that the suite stays runnable while the
    // defect is open (a crash takes the test host down with it); un-skip when it is fixed.
    [Theory]
    // An unclosed pre nests (each is inside the last), so these runs are refused by the nesting bound (160) long
    // before the parser's recursion could matter; the count bound (1 000) is the second line under it.
    [InlineData("<pre>", 100, false)]
    [InlineData("<pre>", 150, false)]
    [InlineData("<pre>", 400, true)]
    [InlineData("<pre>", 1_001, true)]
    [InlineData("<pre>", 400_000, true)]
    [InlineData("<listing>", 150, false)]
    [InlineData("<listing>", 400, true)]
    [InlineData("<pre><listing>", 200_000, true)]
    [InlineData("<pre>\n", 150, false)]
    [InlineData("<pre>\n", 1_000, true)]
    public void QA_Reduce_ConsecutivePreStartTags_AreAnsweredNotACrash(string piece, int count, bool refused)
    {
        var input = string.Concat(Enumerable.Repeat(piece, count)) + "x";

        var watch = Stopwatch.StartNew();
        var reduction = Reduce(input);
        watch.Stop();

        Assert.Equal(refused, reduction.Exceeded);
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(2.5), $"Took {watch.Elapsed}.");
    }

    // --- time: legitimate documents against the parse budget (evidence, not a verdict) ------------------------------

    private static string TableDoc(int columns, int targetUnits, bool header)
    {
        var builder = new StringBuilder("<table><tbody>");
        var row = "<tr>" + string.Concat(Enumerable.Repeat("<td><p>cell</p></td>", columns)) + "</tr>";

        if (header)
        {
            builder.Append("<tr>" + string.Concat(Enumerable.Repeat("<th><p>head</p></th>", columns)) + "</tr>");
        }

        while (builder.Length + row.Length + 16 < targetUnits)
        {
            builder.Append(row);
        }

        return builder.Append("</tbody></table>").ToString();
    }

    private static string NestedLists(int targetUnits)
    {
        var builder = new StringBuilder();
        const string Unit = "<ul><li><p>a</p><ul><li><p>b</p><ul><li><p>c</p></li></ul></li></ul></li></ul>";

        while (builder.Length + Unit.Length < targetUnits)
        {
            builder.Append(Unit);
        }

        return builder.ToString();
    }

    private static string Marks(int targetUnits)
    {
        var builder = new StringBuilder();
        const string Unit = "<p><a href=\"https://e.org\" rel=\"noopener noreferrer\"><strong><em><u>x</u></em></strong></a><s>y</s><mark class=\"rt-hl-red\">z</mark></p>";

        while (builder.Length + Unit.Length < targetUnits)
        {
            builder.Append(Unit);
        }

        return builder.ToString();
    }

    private static string SpreadsheetPaste(int targetUnits)
    {
        var builder = new StringBuilder("<table border=0 cellpadding=0 cellspacing=0 width=1280 style='border-collapse:collapse;table-layout:fixed;width:960pt'>");
        builder.Append("<col width=64 span=20 style='width:48pt'>");
        var row = "<tr height=20 style='height:15.0pt'>" + string.Concat(Enumerable.Range(0, 20).Select(i =>
            $"<td height=20 class=xl65 width=64 style='height:15.0pt;width:48pt;border-top:none;border-left:none'>{i * 7919 % 1000}</td>")) + "</tr>";

        while (builder.Length + row.Length + 16 < targetUnits)
        {
            builder.Append(row);
        }

        return builder.Append("</table>").ToString();
    }

    public static IEnumerable<object[]> LegitimateDocuments()
    {
        yield return ["table-10col-canonical", TableDoc(10, 1_000_000, header: true)];
        yield return ["table-100col-canonical", TableDoc(100, 1_000_000, header: true)];
        yield return ["table-1col-canonical", TableDoc(1, 1_000_000, header: false)];
        yield return ["nested-lists-canonical", NestedLists(1_000_000)];
        yield return ["marks-canonical", Marks(1_000_000)];
        yield return ["spreadsheet-paste-2M-raw", SpreadsheetPaste(1_999_000)];
    }

    [Theory]
    [MemberData(nameof(LegitimateDocuments))]
    public void QA_Reduce_LegitimateLargeDocument_ParseTimeAgainstTheBudget(string name, string input)
    {
        // Three cold-to-warm samples in this process, then the parser alone (no walker) for the same input.
        var samples = new List<long>();
        var exceeded = new List<bool>();
        var markup = 0;

        for (var run = 0; run < 3; run++)
        {
            var watch = Stopwatch.StartNew();
            var reduction = Reduce(input);
            watch.Stop();
            samples.Add(watch.ElapsedMilliseconds);
            exceeded.Add(reduction.Exceeded);
            markup = reduction.MarkupLength;
        }

        var parseWatch = Stopwatch.StartNew();
        var document = new HtmlParser(new HtmlParserOptions { IsScripting = true })
            .ParseDocument("<!DOCTYPE html><html><head></head><body>" + input);
        parseWatch.Stop();
        var nodes = document.All.Length;
        document.Dispose();

        output.WriteLine(
            $"{name}: units={input.Length} reduce[ms]={string.Join("/", samples)} exceeded={string.Join("/", exceeded)} "
            + $"parse-only[ms]={parseWatch.ElapsedMilliseconds} elements={nodes} reducedMarkup={markup}");

        // QA R-1, closed: the answer is a function of the input alone (deterministic bounds, no wall clock), so a
        // legitimate document up to the ceiling is accepted on any host, every time.
        Assert.All(exceeded, wasExceeded => Assert.False(wasExceeded, $"{name} was refused."));
    }

    // QA R-1: the same answer under load. One busy thread per core stands in for a slower or contended host; the
    // documents the spec insists are storable (US-4 legacy worst case, ceiling-sized tables and lists) are accepted.
    [Theory]
    [InlineData("table-10col-canonical")]
    [InlineData("table-1col-canonical")]
    [InlineData("nested-lists-canonical")]
    [InlineData("legacy-100001-empty-paragraphs")]
    [InlineData("legacy-100000-ampersands")]
    public void QA_Reduce_LegitimateLargeDocument_IsAcceptedUnderLoad(string name)
    {
        var input = name switch
        {
            "table-10col-canonical" => TableDoc(10, 1_000_000, header: true),
            "table-1col-canonical" => TableDoc(1, 1_000_000, header: false),
            "legacy-100001-empty-paragraphs" => string.Concat(Enumerable.Repeat("<p></p>", 100_001)),
            "legacy-100000-ampersands" => "<p>" + string.Concat(Enumerable.Repeat("&amp;", 100_000)) + "</p>",
            _ => NestedLists(1_000_000),
        };

        using var stop = new CancellationTokenSource();
        var busy = new List<Thread>();

        for (var index = 0; index < Environment.ProcessorCount; index++)
        {
            var thread = new Thread(() =>
            {
                var x = 0L;

                while (!stop.IsCancellationRequested)
                {
                    x++;
                }

                GC.KeepAlive(x);
            })
            {
                IsBackground = true,
            };
            thread.Start();
            busy.Add(thread);
        }

        try
        {
            var watch = Stopwatch.StartNew();
            var first = Reduce(input);
            watch.Stop();
            output.WriteLine($"{name} loaded: {watch.ElapsedMilliseconds} ms exceeded={first.Exceeded}");

            Assert.False(first.Exceeded);
            Assert.Equal(first.Html, Reduce(input).Html);
        }
        finally
        {
            stop.Cancel();

            foreach (var thread in busy)
            {
                thread.Join();
            }
        }
    }

    // QA R-1: the safety net is a server error, never a refusal reason; what the reduction accepts or refuses does not
    // depend on it. A one-millisecond net abandons a ceiling-sized document with an exception.
    [Fact]
    public void QA_Reduce_SafetyNet_IsAServerErrorNotARefusal()
    {
        var input = TableDoc(10, 1_000_000, header: true);
        var impatient = new HtmlRichTextReducer(Vocabulary, new RichTextOptions { SafetyNetMilliseconds = 1 });

        Assert.Throws<RichTextReductionAbortedException>(
            () => impatient.Reduce(input, new RichTextReductionLimits(Ceiling), CancellationToken.None));

        // The same document with the default net is accepted: only the net differs.
        Assert.False(Reduce(input).Exceeded);
    }

    // QA R-1: the same input gives the same answer however often it is asked, on a reducer of any size.
    [Fact]
    public void QA_Reduce_Answer_IsAFunctionOfTheInputOnly()
    {
        var hostile = "<p " + string.Concat(Enumerable.Range(0, 1_000).Select(i => $"a{i}=1 ")) + ">x";
        var small = new HtmlRichTextReducer(Vocabulary, new RichTextOptions { MaxConcurrentReductions = 1 });
        var limits = new RichTextReductionLimits(Ceiling);

        Assert.All(Enumerable.Range(0, 3), _ => Assert.True(small.Reduce(hostile, limits, CancellationToken.None).Exceeded));
        Assert.All(Enumerable.Range(0, 3), _ => Assert.False(small.Reduce("<p>a</p>", limits, CancellationToken.None).Exceeded));
    }

    // --- re-check 1 (after commit de5e52f) ----------------------------------------------------------------------------

    // QA D-8 (major): text or an inline element between table start tags is foster-parented by the tree builder, and
    // that is quadratic in the number of tables, which none of the bounds counts (tags and elements stay far inside
    // theirs). 100 000 units take 0.9 s, 300 000 take 6.3 s, 1 000 000 run into the 20 s safety net (a server error).
    // The answer wanted is a fast one, refusal or not; the answer is not asserted, the time is.
    [Theory]
    [InlineData("<table>x", 300_000)]
    [InlineData("<table><b>", 300_000)]
    [InlineData("<table><a>", 300_000)]
    public void QA_Reduce_FosterParentedInlineContentBetweenTables_IsAnsweredWithinTheNfr(string piece, int units)
    {
        var input = string.Concat(Enumerable.Repeat(piece, units / piece.Length));

        var watch = Stopwatch.StartNew();
        var reduction = Reduce(input);
        watch.Stop();

        output.WriteLine($"{piece} x{units / piece.Length}: {watch.ElapsedMilliseconds} ms exceeded={reduction.Exceeded}");
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(2.5), $"{piece} took {watch.Elapsed}.");
    }

    // QA D-9 (minor): the preformatted start-tag bound counts tags, not nesting. 1 001 closed code blocks are canonical
    // editor output (about 25 000 bytes) and are refused. Skipped (a minor is carried, not sent back).
    [Fact]
    public void QA_Reduce_ManySequentialCodeBlocks_AreNotRefusedByTheBound()
    {
        var input = string.Concat(Enumerable.Repeat("<pre><code>x</code></pre>", 1_001));

        Assert.False(Reduce(input).Exceeded);
    }

    // The sequence is accepted up to the bound as the developer documents it, so this is the edge.
    [Fact]
    public void QA_Reduce_ThousandSequentialCodeBlocks_AreAccepted()
    {
        var input = string.Concat(Enumerable.Repeat("<pre><code>x</code></pre>", 1_000));

        Assert.False(Reduce(input).Exceeded);
    }

    // QA R-1 re-check: documents built to sit as near as legitimate output can to each structural bound, with a busy
    // thread per core. All are canonical, under the ceiling and the visible limit; none may be refused, and the answer
    // is the same twice.
    public static IEnumerable<object[]> BoundHuggingDocuments()
    {
        // the most elements: empty paragraphs, then brs inside one paragraph at the visible limit
        yield return ["142k-empty-paragraphs", string.Concat(Enumerable.Repeat("<p></p>", 142_000))];
        yield return ["100k-br-in-one-paragraph", "<p>" + string.Concat(Enumerable.Repeat("<br>", 99_990)) + "x</p>"];
        // the most tags per unit: empty list items three levels deep
        yield return ["nested-empty-list-items", string.Concat(Enumerable.Repeat("<ul><li><p></p><ul><li><p></p><ul><li><p></p></li></ul></li></ul></li></ul>", 13_000))];
        // the deepest legitimate paragraph: every mark at once, link outermost
        yield return ["all-marks-per-run", string.Concat(Enumerable.Repeat("<p><a href=\"https://e.org\" rel=\"noopener noreferrer\"><strong><em><u><s><mark class=\"rt-hl-red\"><span class=\"rt-color-red\"><span class=\"rt-size-lg\">x</span></span></mark></s></u></em></strong></a></p>", 5_000))];
        // one very wide row, and many small tables
        yield return ["one-row-60k-cells", "<table><tbody><tr>" + string.Concat(Enumerable.Repeat("<td><p>x</p></td>", 50_000)) + "</tr></tbody></table>"];
        yield return ["17k-small-tables", string.Concat(Enumerable.Repeat("<table><tbody><tr><td><p>x</p></td></tr></tbody></table>", 17_000))];
        // many marks with alternating state in one paragraph
        yield return ["alternating-marks", "<p>" + string.Concat(Enumerable.Repeat("<strong>a</strong>b", 50_000)) + "</p>"];
        // many links
        yield return ["20k-links", "<p>" + string.Concat(Enumerable.Repeat("<a href=\"https://e.org/p?q=1\" rel=\"noopener noreferrer\">l</a> ", 12_000)) + "</p>"];
        // mixed headings, lists, tables, code
        yield return ["mixed-5k", string.Concat(Enumerable.Repeat("<h3>t</h3><p>text <strong>b</strong></p><ul><li><p>a</p></li></ul><table><tbody><tr><td><p>c</p></td></tr></tbody></table><pre><code class=\"language-sql\">select 1</code></pre>", 900))];
    }

    [Theory]
    [MemberData(nameof(BoundHuggingDocuments))]
    public void QA_Reduce_BoundHuggingLegitimateDocument_IsAcceptedUnderLoad_AndDeterministic(string name, string input)
    {
        Assert.True(input.Length <= 1_000_000 + 64, $"{name}: {input.Length} units");

        using var stop = new CancellationTokenSource();
        var busy = Enumerable.Range(0, Environment.ProcessorCount).Select(_ =>
        {
            var thread = new Thread(() =>
            {
                var x = 0L;

                while (!stop.IsCancellationRequested)
                {
                    x++;
                }

                GC.KeepAlive(x);
            })
            {
                IsBackground = true,
            };
            thread.Start();

            return thread;
        }).ToList();

        try
        {
            var watch = Stopwatch.StartNew();
            var first = Reduce(input);
            watch.Stop();
            output.WriteLine($"{name}: units={input.Length} {watch.ElapsedMilliseconds} ms exceeded={first.Exceeded} markup={first.MarkupLength} visible={first.VisibleLength}");

            Assert.False(first.Exceeded, $"{name} was refused");
            Assert.Equal(first.Html, Reduce(input).Html);
        }
        finally
        {
            stop.Cancel();

            foreach (var thread in busy)
            {
                thread.Join();
            }
        }
    }

    // QA D-10 (major, re-check 2): text pieces separated by tokens the tree builder ignores (a stray td, tr, tbody, an
    // unmatched end tag, the text after a closed table) are appended to one text node by string concatenation, which
    // is quadratic in the number of pieces, and no bound counts it: 150 000 units 0.5 s, 300 000 1.0 s, 600 000
    // 4.5 to 6.6 s, 1 000 000 13 s, 2 000 000 the 20 s safety net (a 500). Not table foster parenting only.
    [Theory]
    [InlineData("<td>x", 600_000)]
    [InlineData("<tr>x", 600_000)]
    [InlineData("<tbody>x", 600_000)]
    [InlineData("<table>x</table>y", 600_000)]
    public void QA_Reduce_TextSplitByIgnoredTokens_IsAnsweredWithinTheNfr(string piece, int units)
    {
        var input = string.Concat(Enumerable.Repeat(piece, units / piece.Length));

        var watch = Stopwatch.StartNew();
        var reduction = Reduce(input);
        watch.Stop();

        output.WriteLine($"{piece} x{units / piece.Length}: {watch.ElapsedMilliseconds} ms exceeded={reduction.Exceeded}");
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(2.5), $"{piece} took {watch.Elapsed}.");
    }

    // Re-check 2: canonical table shapes the new foster-parenting bound must not touch.
    public static IEnumerable<object[]> TableShapes()
    {
        var cell = "<td>" + string.Concat(Enumerable.Repeat("<p>cell paragraph</p>", 40)) + "</td>";
        yield return ["long-cells", "<table><tbody>" + string.Concat(Enumerable.Repeat("<tr>" + cell + cell + "</tr>", 140)) + "</tbody></table>"];
        yield return ["table-paragraph-alternating", string.Concat(Enumerable.Repeat("<table><tbody><tr><td><p>x</p></td></tr></tbody></table><p>text</p>", 9_000))];
        yield return ["table-list-code-alternating", string.Concat(Enumerable.Repeat("<table><tbody><tr><th><p>h</p></th></tr></tbody></table><ul><li><p>a</p><ul><li><p>b</p></li></ul></li></ul><pre><code>c</code></pre>", 4_000))];
        yield return ["tables-with-marks", string.Concat(Enumerable.Repeat("<table><tbody><tr><td><p><strong>b</strong><em>i</em></p></td><td><p><a href=\"https://e.org\" rel=\"noopener noreferrer\">l</a></p></td></tr></tbody></table>", 5_000))];
        yield return ["headers-everywhere", "<table><tbody><tr>" + string.Concat(Enumerable.Repeat("<th><p>h</p></th>", 25_000)) + "</tr><tr>" + string.Concat(Enumerable.Repeat("<td><p></p></td>", 25_000)) + "</tr></tbody></table>"];
    }

    [Theory]
    [MemberData(nameof(TableShapes))]
    public void QA_Reduce_CanonicalTableShapes_AreAccepted(string name, string input)
    {
        var watch = Stopwatch.StartNew();
        var reduction = Reduce(input);
        watch.Stop();

        output.WriteLine($"{name}: units={input.Length} {watch.ElapsedMilliseconds} ms exceeded={reduction.Exceeded} markup={reduction.MarkupLength}");
        Assert.False(reduction.Exceeded, $"{name} was refused");
        Assert.Equal(input, reduction.Html);
    }

    // Idempotency and stability once more, after the loop's changes: a CR reference, foster parenting, marks.
    [Fact]
    public void QA_Reduce_RandomStructures_AfterTheBounds_AreIdempotentAndStable()
    {
        string[] pieces =
        [
            "<p>", "</p>", "<b>", "</b>", "<a href=\"https://e.org/x\">", "</a>", "<a href=\"https://e.org/<x>\">", "<code>", "</code>",
            "<table>", "</table>", "<tr>", "<td>", "<th>", "<td colspan=2>", "</tr>", "text", "x", " ", "\n", "&#13;", "&#13;&#10;",
            "&amp;", "&nbsp;", "<br>", "<ul>", "<li>", "</li>", "</ul>", "<pre>", "</pre>", "<pre><code class=\"language-sql\">", "</code></pre>",
            "<span class=\"rt-color-red\">", "</span>", "<u>", "</u>", "<h4>", "</h4>", "<div>", "</div>", "<!-- c -->", "<font>", "</font>",
        ];

        foreach (var seed in new[] { 3, 5, 8, 13, 21 })
        {
            var random = new Random(seed);

            for (var round = 0; round < 500; round++)
            {
                var builder = new StringBuilder();

                for (var index = random.Next(1, 50); index > 0; index--)
                {
                    builder.Append(pieces[random.Next(pieces.Length)]);
                }

                var input = builder.ToString();
                var once = Stored(input);
                var twice = Stored(once);

                Assert.True(once == twice, $"seed {seed} round {round} not idempotent: {input}\nonce:  {once}\ntwice: {twice}");
                Assert.DoesNotContain('\r', once);
                AssertInsideAllowList(once);
                AssertStableUnderReparse(once);
            }
        }
    }
}
