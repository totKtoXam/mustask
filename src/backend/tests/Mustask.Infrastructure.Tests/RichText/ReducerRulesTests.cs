using System.Diagnostics;
using System.Text;
using Mustask.Infrastructure.RichText;
using static Mustask.Infrastructure.Tests.RichText.RichTextTestSupport;

namespace Mustask.Infrastructure.Tests.RichText;

/// <summary>The link rule, the vocabulary and the writer of the reduction (feat-018 US-2, US-7).</summary>
public sealed class ReducerRulesTests
{
    // US-2 · "Links keep only absolute safe addresses": the story's table, as the address rule reads it.
    [Theory]
    [InlineData("https://example.org", true)]
    [InlineData("http://example.org", true)]
    [InlineData("mailto:a@example.org", true)]
    [InlineData("HTTPS://EXAMPLE.ORG/Path", true)]
    [InlineData("  https://example.org  ", true)]
    [InlineData("https://user@example.org:8080/p?q=1#f", true)]
    [InlineData("javascript:alert(1)", false)]
    [InlineData("data:text/html;base64,AAAA", false)]
    [InlineData("vbscript:x", false)]
    [InlineData(" JaVaScRiPt:x", false)]
    [InlineData("java\tscript:x", false)]
    [InlineData("/rel", false)]
    [InlineData("rel", false)]
    [InlineData("//evil.example", false)]
    [InlineData("#frag", false)]
    [InlineData("", false)]
    [InlineData("https://", false)]
    [InlineData("https:///path", false)]
    [InlineData("https://:80/", false)]
    [InlineData("https:example.org", false)]
    [InlineData("https://exa mple.org", false)]
    [InlineData("mailto:", false)]
    [InlineData("mailto:?subject=x", false)]
    [InlineData("ftp://example.org", false)]
    public void LinkAddress_TryRead_FollowsTheStoryTable(string raw, bool kept)
    {
        Assert.Equal(kept, LinkAddress.TryRead(raw, out _));
    }

    // US-2 "Link address": leading/trailing C0 and spaces trimmed, tab/LF/CR removed anywhere, never re-encoded.
    [Fact]
    public void LinkAddress_TryRead_ReturnsTheAddressAsRead()
    {
        Assert.True(LinkAddress.TryRead("\u0001 htt\tps://exa\nmple.org/a b\r ", out var href));
        Assert.Equal("https://example.org/a b", href);
    }

    // US-2 · "Executable and presentational markup is stripped": the content of dropped elements is gone with them.
    [Fact]
    public void Reduce_DroppedElements_TakeTheirContent_UnknownOnesKeepIt()
    {
        Assert.Equal("<p>ab</p>", Stored("<p>a<noscript>x</noscript><textarea>y</textarea><select><option>z</option></select>b</p>"));
        Assert.Equal("<p>abc</p>", Stored("<p>a<sub>b</sub><sup>c</sup></p>"));
    }

    // US-7 · "Tokens are matched exactly": every entry of the file is kept on its element and on no other, is dropped
    // when its case changes, and is written from the table.
    public static IEnumerable<object[]> VocabularyTokens() =>
        Vocabulary.Families.SelectMany(family => family.Tokens.Select(token => new object[] { family.Kind, token.Class }));

    [Theory]
    [MemberData(nameof(VocabularyTokens))]
    public void Vocabulary_EveryToken_KeptOnItsElementOnly_ExactCase(string kind, string cls)
    {
        var element = Vocabulary.Families.Single(family => family.Kind == kind).Element;
        var other = element == "mark" ? "span" : "mark";

        if (kind == "codeLanguage")
        {
            Assert.Equal($"<pre><code class=\"{cls}\">x</code></pre>", Stored($"<pre><code class='{cls}'>x</code></pre>"));
            Assert.Equal("<pre><code>x</code></pre>", Stored($"<pre><code class='{cls.ToUpperInvariant()}'>x</code></pre>"));

            // Not a block's language: inline code carries no class, and a class on pre is not read.
            Assert.Equal("<p><code>x</code></p>", Stored($"<code class='{cls}'>x</code>"));
            Assert.Equal("<pre><code>x</code></pre>", Stored($"<pre class='{cls}'><code>x</code></pre>"));

            return;
        }

        Assert.Equal($"<p><{element} class=\"{cls}\">x</{element}></p>", Stored($"<{element} class='{cls}'>x</{element}>"));
        Assert.Equal("<p>x</p>", Stored($"<{element} class='{cls.ToUpperInvariant()}'>x</{element}>"));
        Assert.Equal("<p>x</p>", Stored($"<{other} class='{cls}'>x</{other}>"));
        Assert.Equal("<p>x</p>", Stored($"<p class='{cls}'>x</p>"));
    }

    // Plan § 5.2 (c): markOrder is the order the pass implements: one text carrying all eight marks.
    [Fact]
    public void Vocabulary_MarkOrder_IsTheOrderTheWriterNests()
    {
        var input = "<span class='rt-size-xl'><span class='rt-color-red'><mark class='rt-hl-red'><s><u><em><strong><a href='https://e.org'>x</a></strong></em></u></s></mark></span></span>";
        var expected =
            "<p><a href=\"https://e.org\" rel=\"noopener noreferrer\"><strong><em><u><s><mark class=\"rt-hl-red\">"
            + "<span class=\"rt-color-red\"><span class=\"rt-size-xl\">x</span></span></mark></s></u></em></strong></a></p>";

        Assert.Equal(expected, Stored(input));

        var names = Vocabulary.MarkOrder.Select(kind => kind.ToString()).ToList();
        Assert.Equal(["Link", "Bold", "Italic", "Underline", "Strike", "Highlight", "TextColor", "FontSize"], names);
    }

    // Plan § 5.2 (a): the embedded bytes are the file on disk.
    [Fact]
    public void Vocabulary_Embedded_EqualsTheFileOnDisk()
    {
        Assert.Equal(File.ReadAllBytes(ContractsFile("vocabularies.json")), RichTextVocabulary.EmbeddedBytes());
    }

    // Plan § 5.2 (d): a file the pass cannot implement fails the load.
    [Theory]
    [InlineData("""{"markOrder":["link","bold","italic","underline","strike","highlight","textColor","fontSize"],"families":[{"kind":"emoji","element":"span","tokens":[]}]}""")]
    [InlineData("""{"markOrder":["link","bold"],"families":[]}""")]
    [InlineData("""{"markOrder":["link","bold","italic","underline","strike","highlight","textColor","sparkle"],"families":[]}""")]
    public void Vocabulary_Parse_UnknownKindOrMark_Fails(string json)
    {
        Assert.Throws<InvalidOperationException>(() => RichTextVocabulary.Parse(json));
    }

    [Fact]
    public void Vocabulary_Parse_DuplicateOrForeignClass_Fails()
    {
        var json = File.ReadAllText(ContractsFile("vocabularies.json"));

        Assert.Throws<InvalidOperationException>(() => RichTextVocabulary.Parse(json.Replace("rt-color-pink", "rt-color-red", StringComparison.Ordinal)));
        Assert.Throws<InvalidOperationException>(() => RichTextVocabulary.Parse(json.Replace("rt-color-pink", "evil", StringComparison.Ordinal)));
    }

    // ADR-0039 decision 6: no token literal in server code; the file is the only source.
    [Fact]
    public void ServerCode_HoldsNoTokenLiteral()
    {
        var source = Path.Combine(RepositoryRoot(), "src", "backend");
        string[] literals = ["rt-color-", "rt-hl-", "rt-size-", "language-"];

        var hits = Directory.EnumerateFiles(source, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}tests{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                && !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                && !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                && !path.Contains($"{Path.DirectorySeparatorChar}Migrations{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(path => literals.Any(literal => File.ReadAllText(path).Contains(literal, StringComparison.Ordinal)))
            .Select(path => Path.GetRelativePath(source, path))
            .ToList();

        Assert.Empty(hits);
    }

    // Plan § 11.1 WriterTests: the WHATWG form of api-contract § 3.
    [Fact]
    public void Writer_EscapesAsTheSerialisationDoes_AndWritesAttributesInOrder()
    {
        Assert.Equal("<p>a &amp; b &lt;i&gt; c&nbsp;d</p>", Stored("<p>a &amp; b &lt;i&gt; c d</p>"));
        Assert.Equal(
            "<p><a href=\"https://e.org/?a=1&amp;b=%22q%22&quot;\" rel=\"noopener noreferrer\">l</a></p>",
            Stored("<p><a rel=\"nofollow\" target=\"_blank\" href='https://e.org/?a=1&amp;b=%22q%22\"'>l</a></p>"));
        Assert.Equal("<p>a<br>b</p>", Stored("<p>a<br/>b</p>"));
    }

    // The writer's output is what AngleSharp's own serialisation of the same tree gives (the oracle of plan § 11.1).
    [Theory]
    [MemberData(nameof(CanonicalIds))]
    public void Writer_Output_EqualsTheParsersOwnSerialisation(string id)
    {
        var stored = Stored(Case("canonical.json", id).GetProperty("input").GetString()!);

        Assert.Equal(stored, ParseBody(stored, scripting: false).InnerHtml);
    }

    public static IEnumerable<object[]> CanonicalIds() => CaseIds("canonical.json");

    // --- budget, amplification and depth -------------------------------------------------------------------------

    // US-4 · "The markup ceiling has its own reason" at its edge: 1 000 000 code points are accepted, 1 000 001 are not.
    [Fact]
    public void Reduce_MarkupExactlyTheCeiling_IsAccepted_OneMoreIsExceeded()
    {
        var atCeiling = "<p>" + new string('a', Ceiling - 7) + "</p>";

        var accepted = Reduce(atCeiling);
        Assert.False(accepted.Exceeded);
        Assert.Equal(Ceiling, accepted.MarkupLength);
        Assert.Equal(Ceiling - 7, accepted.VisibleLength);

        Assert.True(Reduce("<p>" + new string('a', Ceiling - 6) + "</p>").Exceeded);
    }

    // US-4 · "Exactly the limit is accepted": 100 000 emoji are 100 000 code points of visible text.
    [Fact]
    public void Reduce_100000Emoji_CountsByCodePoint()
    {
        var reduction = Reduce("<p>" + string.Concat(Enumerable.Repeat("\U0001F600", 100_000)) + "</p>");

        Assert.False(reduction.Exceeded);
        Assert.Equal(100_000, reduction.VisibleLength);
        Assert.Equal(100_007, reduction.MarkupLength);
    }

    // US-4 · "Limits come before the empty rule": whitespace only, over the ceiling, is exceeded, not ""; at the
    // ceiling it is read and stored as "" (the budget counts text read, plan § 5.1).
    [Fact]
    public void Reduce_WhitespaceOnly_OverTheCeiling_IsExceeded_AtTheCeilingIsEmpty()
    {
        Assert.True(Reduce(new string(' ', Ceiling + 1)).Exceeded);

        var atCeiling = Reduce(new string('\n', Ceiling));
        Assert.False(atCeiling.Exceeded);
        Assert.False(atCeiling.HasContent);
    }

    // US-2 · "The reduction stops early" / US-4 · "Amplifying input is refused promptly": 2 000 000 units that toggle
    // a link per character around eight stacked marks.
    [Fact]
    public void Reduce_Amplifying2000000Units_IsExceededWithinTwoSeconds()
    {
        const string Open = "<strong><em><u><s><mark class='rt-hl-red'><span class='rt-color-red'><span class='rt-size-lg'>";
        var input = new StringBuilder(Open);

        while (input.Length < 2_000_000 - 40)
        {
            // The link toggles on every other character, so the outer mark changes each time and nothing merges.
            input.Append("<a href='https://e.org'>x</a>y");
        }

        var watch = Stopwatch.StartNew();
        var reduction = Reduce(input.ToString());
        watch.Stop();

        Assert.True(reduction.Exceeded);
        Assert.True(watch.Elapsed < RichTextTestSupport.Budget(2), $"Took {watch.Elapsed}.");
    }

    // Plan § 5.1 / § 6.1: hostile nesting costs time, never the process: it is bounded and refused.
    [Theory]
    [InlineData("b", true)]
    [InlineData("div", true)]
    [InlineData("ul", true)]
    [InlineData("table", false)] // a table start tag inside a table closes it: no depth, nothing to refuse
    public void Reduce_HostileDeepNesting_IsBoundedInTime(string tag, bool refused)
    {
        var input = string.Concat(Enumerable.Repeat($"<{tag}>", 2_000_000 / (tag.Length + 2))) + "x";

        var watch = Stopwatch.StartNew();
        var reduction = Reduce(input);
        watch.Stop();

        Assert.Equal(refused, reduction.Exceeded);
        Assert.True(watch.Elapsed < RichTextTestSupport.Budget(4), $"Took {watch.Elapsed}.");
    }

    // Nesting is bounded by DOM depth (160 including html and body), deterministically: 150 levels are read, 3 000
    // are refused, and neither depends on how fast the host is.
    [Fact]
    public void Reduce_NestingBound_IsDeterministic()
    {
        string Nested(int levels) =>
            string.Concat(Enumerable.Repeat("<div>", levels)) + "deep" + string.Concat(Enumerable.Repeat("</div>", levels));

        Assert.Equal("<p>deep</p>", Stored(Nested(150)));
        Assert.True(Reduce(Nested(160)).Exceeded);
        Assert.True(Reduce(Nested(3_000)).Exceeded);
    }

    // A tag count that would write more table positions than the bound allows is refused before it is built.
    [Fact]
    public void Reduce_SpanBomb_IsExceededWithoutBuildingTheGrid()
    {
        // One cell claims 1 000 columns of 65 534 rows: 65 million positions from 600 000 units.
        var input = "<table><tr><td colspan='1000' rowspan='65534'>x</td></tr>"
            + string.Concat(Enumerable.Repeat("<tr><td>y</td></tr>", 65_533)) + "</table>";

        var watch = Stopwatch.StartNew();
        var reduction = Reduce(input);
        watch.Stop();

        Assert.True(reduction.Exceeded);
        Assert.True(watch.Elapsed < RichTextTestSupport.Budget(2));
    }

    // --- property: idempotency over a small grammar --------------------------------------------------------------

    // Plan § 11.1 IdempotencyPropertyTests: a seeded generator, so a failure reproduces; reduce(reduce(x)) = reduce(x),
    // the output is inside the allow-list and stable under re-parse.
    [Fact]
    public void Reduce_RandomMarkup_IsIdempotent_InsideTheAllowList_AndStable()
    {
        string[] fragments =
        [
            "<p>", "</p>", "<b>", "</b>", "<i>", "</i>", "<u>", "</u>", "<s>", "</s>", "<del>", "<strike>", "<code>", "</code>",
            "<pre>", "</pre>", "<ul>", "</ul>", "<ol>", "</ol>", "<li>", "</li>", "<table>", "</table>", "<tr>", "</tr>", "<td>", "</td>",
            "<td colspan='2'>", "<td rowspan='2'>", "<th>", "</th>", "<thead>", "</thead>", "<h1>", "</h1>", "<h4>", "</h4>",
            "<a href='https://e.org'>", "<a href='javascript:x'>", "</a>", "<span class='rt-color-red rt-size-lg'>", "<span class='RT-COLOR-RED'>",
            "</span>", "<mark class='rt-hl-teal'>", "</mark>", "<div>", "</div>", "<br>", "<img src=x>", "<script>x</script>", "<svg><b>",
            "text", " ", "\n", "&amp;", "&lt;", "&nbsp;", "\U0001F600", "<!-- c -->", "<font color=red>", "</font>", "<blockquote>", "<pre><code class='language-sql'>",
        ];

        var random = new Random(20261013);

        for (var round = 0; round < 1_000; round++)
        {
            var builder = new StringBuilder();
            var count = random.Next(1, 40);

            for (var index = 0; index < count; index++)
            {
                builder.Append(fragments[random.Next(fragments.Length)]);
            }

            var input = builder.ToString();
            var once = Stored(input);

            var twice = Stored(once);
            Assert.True(once == twice, $"Not idempotent for seed round {round}: {input}\nonce:  {once}\ntwice: {twice}");
            AssertInsideAllowList(once);
            AssertStableUnderReparse(once);
        }
    }
}
