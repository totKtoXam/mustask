using System.Diagnostics;
using Mustask.Infrastructure.RichText;
using Xunit.Abstractions;
using static Mustask.Infrastructure.Tests.RichText.RichTextTestSupport;

namespace Mustask.Infrastructure.Tests.RichText;

/// <summary>
/// QA re-check 3 of D-10: shapes outside the developer's sweep, at the 1 000 000-unit ceiling. An accepted shape must
/// scale about linearly (double the size, compare) and stay in the NFR; legitimate editor output must be accepted.
/// </summary>
public sealed class ReducerTextWorkRecheck3QaTests(ITestOutputHelper output)
{
    private static string Repeat(string piece, int units) => string.Concat(Enumerable.Repeat(piece, units / piece.Length));

    public static IEnumerable<object[]> Attacks() =>
        new[]
        {
            "<td>x<tr>y<tbody>z", "<TD>x&amp;", "x<br>y", "x<br>", "<td>x&amp;<tr>&lt;", "<td>x<!--c-->", "<td>x<![CDATA[y]]>",
            "<b><i>x</b>", "<b>x<p>y", "<p>x<p>y", "<li>x<li>y", "<a>x<a>y", "<b>x", "<font color=a>x</p>", "<p><b>x</p>",
            "<p><b id=1>x</p>", "<table><tr><td><table><tr><td>x</td></tr></table>y</td></tr></table>", "</td>x", "</tr>x</td>y",
            "<table><tr><td>x</table>y", "<table><td>x</td>y", "<table><caption>x</caption>y", "<table></td>x", "<table><tr><td>x</tr>y",
            "<table><tr><td><div>x</td>y", "<table><tr><td><table>x</td>y", "<table>x<table>y", "<table><tr><td>x</td></tr></table>y<td>z",
            "<td> x </td> y", "x</table>y", "<table><tr> ", "<td> ", "<tbody>x\u0000y", "<table><col>x", "<colgroup>x",
            "<html a=1>x<body b=2>y<head>z", "<form>x</form>y", "<form><table><form>x", "<table><form>x</form>y", "<table><input type=hidden>x",
            "<table><tr><td><caption>x", "<caption>x", "<table><thead>x", "<th>x</th>y", "</body>x</html>y", "<frame>x", "<isindex>x",
            "<table><tr><td>x</td><td>y</td></tr></table><p>z",
            "<td>x</td><td>y", "x</b></i>y", "<br>x<td>y", "<hr>x<td>y", "<img src=a>x<td>", "<p></p>x<td>", "<table></table>x<td>",
            "<table><tr><td><p>x</td></tr><p>", "<table><tr><td><td>x", "<table><tr><tr>x", "<table><tbody><tbody>x",
            "<TABLE><TBODY>x", "<table  >x</table  >y", "<td\n>x", "<td/>x", "<td a=\">\">x", "<td a='>'>x",
        }.Select(s => new object[] { s });

    [Theory]
    [MemberData(nameof(Attacks))]
    public void AcceptedAtTheCeiling_ScalesLinearlyAndStaysInTheNfr(string piece)
    {
        var big = Repeat(piece, 1_000_000);
        var guarded = RichTextInputGuard.Scan(big, RichTextGuardLimits.Default, out var stats);

        if (!guarded)
        {
            output.WriteLine($"[refused] {piece} append={stats.TextAppendWork} foster={stats.FosterTokenWork}");

            return;
        }

        var half = Repeat(piece, 500_000);
        var w = Stopwatch.StartNew();
        var rHalf = Reduce(half);
        var tHalf = w.ElapsedMilliseconds;
        w.Restart();
        var rBig = Reduce(big);
        var tBig = w.ElapsedMilliseconds;

        output.WriteLine($"[accepted] {piece} append={stats.TextAppendWork} foster={stats.FosterTokenWork} half={tHalf} ms big={tBig} ms exceeded={rHalf.Exceeded}/{rBig.Exceeded}");
        Assert.True(tBig < 9_000 || rBig.Exceeded, $"{piece}: {tBig} ms (half {tHalf} ms), accepted by the guard.");
    }

    public static IEnumerable<object[]> Legit() =>
        new[]
        {
            "<p>" + string.Concat(Enumerable.Repeat("<strong>a</strong><em>b</em><u>c</u><s>d</s><mark>e</mark><code>f</code> t ", 1)) + "</p>",
            "<ul><li><p>item</p></li></ul>",
            "<table><tbody><tr><td><p>c</p></td></tr></tbody></table>",
            "<pre><code>line</code></pre>",
            "<p>w <a href=\"https://example.org/x\" rel=\"noopener noreferrer\">link</a></p>",
            "<p>a<br>b<br>c</p>",
        }.Select(s => new object[] { s });

    [Theory]
    [MemberData(nameof(Legit))]
    public void LegitimateShapeAtTheCeiling_IsAccepted(string piece)
    {
        var input = Repeat(piece, 1_000_000);
        Assert.True(RichTextInputGuard.Scan(input, RichTextGuardLimits.Default, out var stats), $"append={stats.TextAppendWork} foster={stats.FosterTokenWork} tags={stats.Tags}");
        output.WriteLine($"{piece.Length} append={stats.TextAppendWork} foster={stats.FosterTokenWork} tags={stats.Tags}");
    }

    [Fact]
    public void OneVeryLongParagraphWithManyMarks_IsAccepted()
    {
        var input = "<p>" + string.Concat(Enumerable.Repeat("a <strong>b</strong><em>c</em> ", 31_000)) + "</p>";
        Assert.True(RichTextInputGuard.Scan(input, RichTextGuardLimits.Default, out var stats), $"append={stats.TextAppendWork}");
        Assert.False(Reduce(input).Exceeded);
    }

    [Fact]
    public void NestedMarksAroundLongText_AreAccepted()
    {
        // Each nested mark closes and the text after it joins nothing: but the end tags continue the run in the count.
        var input = "<p>" + string.Concat(Enumerable.Repeat("<strong><em><u>word</u></em></strong>", 25_000)) + "</p>";
        Assert.True(RichTextInputGuard.Scan(input, RichTextGuardLimits.Default, out var stats), $"append={stats.TextAppendWork}");
        output.WriteLine($"append={stats.TextAppendWork}");
    }

    [Fact]
    public void HundredByHundredTableAndManyCodeBlocksAndLists_AreAccepted()
    {
        var row = "<tr>" + string.Concat(Enumerable.Repeat("<td><p>cell text</p></td>", 100)) + "</tr>";
        var table = "<table><tbody>" + string.Concat(Enumerable.Repeat(row, 100)) + "</tbody></table>";
        Assert.True(RichTextInputGuard.Scan(table, RichTextGuardLimits.Default, out _));

        var pretty = table.Replace("<tr>", "\n  <tr>\n").Replace("<td>", "    <td>").Replace("</tr>", "\n  </tr>\n");
        Assert.True(RichTextInputGuard.Scan(pretty, RichTextGuardLimits.Default, out var s2), $"foster={s2.FosterTokenWork}");

        var wide = "<table><tbody><tr>" + string.Concat(Enumerable.Repeat("<td>c</td>", 80_000)) + "</tr></tbody></table>";
        Assert.True(RichTextInputGuard.Scan(wide, RichTextGuardLimits.Default, out _));

        var code = string.Concat(Enumerable.Repeat("<pre><code>x = 1;\ny = 2;</code></pre>", 20_000));
        Assert.True(RichTextInputGuard.Scan(code, RichTextGuardLimits.Default, out _));

        var li = "<ul>" + string.Concat(Enumerable.Repeat("<li><p>item</p></li>", 30_000)) + "</ul>";
        Assert.True(RichTextInputGuard.Scan(li, RichTextGuardLimits.Default, out _));
    }
}
