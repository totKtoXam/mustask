using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using Mustask.Api.IntegrationTests.Features.Organizations;

namespace Mustask.Api.IntegrationTests.Features.WorkItems;

/// <summary>
/// <c>PATCH …/workitems/{identifier}</c> with <c>descriptionFormat</c> (feat-018 api-contract § 1..§ 4): US-2, US-3
/// (server half), US-4 and US-7 through the API. The existing <c>SaveWorkItemEndpointTests</c> stay as they are: they
/// are "a client that declares nothing is unchanged" (BR-RT-10).
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class SaveWorkItemDescriptionEndpointTests(MustaskApiFactory factory)
{
    private const string Description = "workitems.workitem.description_too_long";
    private const string MarkupTooLarge = "workitems.workitem.description_markup_too_large";
    private const string FormatUnknown = "workitems.workitem.description_format_unknown";

    private async Task<(WorkItemsWorld World, string Bug)> BugAsync()
    {
        var world = await WorkItemsTestData.CreateWorldAsync(factory);
        var bug = await WorkItemsTestData.CreateItemAsync(factory, world, "BUG", "Падение при входе");

        return (world, bug);
    }

    private static string? Text(JsonElement body, string name) => body.GetProperty(name).GetString();

    private static string? Reason(Answer answer, string field = "description") =>
        answer.Body.GetProperty("errors").GetProperty(field).EnumerateArray().Single().GetProperty("code").GetString();

    private static string Repeat(string unit, int times) => new StringBuilder(unit.Length * times).Insert(0, unit, times).ToString();

    // --- US-4: the format contract -------------------------------------------------------------------------------

    // US-4 · "A client that declares nothing is unchanged".
    [Fact]
    public async Task Save_NoFormat_StoredExactlyAsSent_ReportedPlain()
    {
        var (world, bug) = await BugAsync();

        var answer = await WorkItemsTestData.SaveAsync(factory, world, bug, new { description = "a < b <b>x</b>" });

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Equal(("a < b <b>x</b>", "plain"), (Text(answer.Body, "description"), Text(answer.Body, "descriptionFormat")));

        var reopened = await WorkItemsTestData.ReadAsync(factory, world, bug);
        Assert.Equal(("a < b <b>x</b>", "plain"), (Text(reopened, "description"), Text(reopened, "descriptionFormat")));
    }

    // US-4 · "a plain description over 100 000 code units is refused with description_too_long as before", declared or not.
    [Theory]
    [InlineData(null)]
    [InlineData("plain")]
    public async Task Save_PlainOver100000_DescriptionTooLong(string? format)
    {
        var (world, bug) = await BugAsync();

        var answer = await WorkItemsTestData.SaveAsync(factory, world, bug, new { description = new string('ж', 100_001), descriptionFormat = format });

        Assert.Equal(HttpStatusCode.BadRequest, answer.Status);
        Assert.Equal(Description, Reason(answer));
    }

    // US-4 · "A declared html description is reduced and reported".
    [Fact]
    public async Task Save_Html_ReducedAndReported()
    {
        var (world, bug) = await BugAsync();

        var answer = await WorkItemsTestData.SaveAsync(
            factory, world, bug, new { description = "<p>a</p><script>x</script>", descriptionFormat = "html" });

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Equal(("<p>a</p>", "html"), (Text(answer.Body, "description"), Text(answer.Body, "descriptionFormat")));

        var reopened = await WorkItemsTestData.ReadAsync(factory, world, bug);
        Assert.Equal(("<p>a</p>", "html"), (Text(reopened, "description"), Text(reopened, "descriptionFormat")));
    }

    // US-4 · "A never-written or cleared description is plain and empty": both return "" with plain, never null.
    [Fact]
    public async Task Get_NeverWrittenOrClearedByHtml_EmptyPlain()
    {
        var (world, bug) = await BugAsync();
        var fresh = await WorkItemsTestData.ReadAsync(factory, world, bug);

        Assert.Equal(("", "plain"), (Text(fresh, "description"), Text(fresh, "descriptionFormat")));

        await WorkItemsTestData.SaveAsync(factory, world, bug, new { description = "<p>a</p>", descriptionFormat = "html" });
        var cleared = await WorkItemsTestData.SaveAsync(factory, world, bug, new { description = "<p> </p>", descriptionFormat = "html" });

        Assert.Equal(("", "plain"), (Text(cleared.Body, "description"), Text(cleared.Body, "descriptionFormat")));
    }

    // US-4 · "An unknown format is refused" / "An unknown format with no description is still refused": nothing stored.
    [Theory]
    [InlineData("markdown", true)]
    [InlineData("HTML", true)]
    [InlineData("", true)]
    [InlineData(" html", true)]
    [InlineData("markdown", false)]
    public async Task Save_UnknownFormat_400DescriptionFormatUnknown_NothingStored(string format, bool withDescription)
    {
        var (world, bug) = await BugAsync();
        var before = await WorkItemsTestData.StoredAsync(factory, world.ProjectId);

        var body = withDescription
            ? (object)new { description = "<p>x</p>", descriptionFormat = format, title = "Другой" }
            : new { descriptionFormat = format, title = "Другой" };
        var answer = await WorkItemsTestData.SaveAsync(factory, world, bug, body);

        Assert.Equal(HttpStatusCode.BadRequest, answer.Status);
        Assert.Equal("workitems.validation_failed", answer.ErrorCode);
        Assert.Equal(FormatUnknown, Reason(answer));
        Assert.Equal(before, await WorkItemsTestData.StoredAsync(factory, world.ProjectId));
    }

    // api-contract § 1: a value of another JSON type fails binding as every other field does.
    [Fact]
    public async Task Save_FormatOfAnotherJsonType_Is400HttpBadRequest()
    {
        var (world, bug) = await BugAsync();

        var answer = await WorkItemsTestData.SaveAsync(factory, world, bug, new { description = "x", descriptionFormat = 7 });

        Assert.Equal(HttpStatusCode.BadRequest, answer.Status);
        Assert.Equal("http.bad_request", answer.ErrorCode);
    }

    // US-4 · "A format with no description changes nothing", also for the stored format.
    [Fact]
    public async Task Save_HtmlFormatWithoutDescription_Unchanged()
    {
        var (world, bug) = await BugAsync();
        await WorkItemsTestData.SaveAsync(factory, world, bug, new { description = "plain text" });
        var before = await WorkItemsTestData.ReadAsync(factory, world, bug);

        var answer = await WorkItemsTestData.SaveAsync(factory, world, bug, new { descriptionFormat = "html" });

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Equal(before.GetRawText(), answer.Body.GetRawText());
    }

    // US-4 · "Equality is of value and format together": the html re-send of the stored value is no change.
    [Fact]
    public async Task Save_HtmlResendOfStored_NoChange_ChangedAtSame()
    {
        var (world, bug) = await BugAsync();
        var first = await WorkItemsTestData.SaveAsync(factory, world, bug, new { description = "<b>a</b>", descriptionFormat = "html" });

        var again = await WorkItemsTestData.SaveAsync(factory, world, bug, new { description = Text(first.Body, "description"), descriptionFormat = "html" });

        Assert.Equal("<p><strong>a</strong></p>", Text(again.Body, "description"));
        Assert.Equal(first.Body.GetProperty("changedAt").GetDateTimeOffset(), again.Body.GetProperty("changedAt").GetDateTimeOffset());
    }

    [Fact]
    public async Task Save_PlainResendOfHtmlValue_IsChange_StoredPlain()
    {
        var (world, bug) = await BugAsync();
        var first = await WorkItemsTestData.SaveAsync(factory, world, bug, new { description = "<p>a</p>", descriptionFormat = "html" });

        var plain = await WorkItemsTestData.SaveAsync(factory, world, bug, new { description = "<p>a</p>" });

        Assert.Equal(("<p>a</p>", "plain"), (Text(plain.Body, "description"), Text(plain.Body, "descriptionFormat")));
        Assert.True(plain.Body.GetProperty("changedAt").GetDateTimeOffset() > first.Body.GetProperty("changedAt").GetDateTimeOffset());
    }

    // US-4 · "A plain write over an html description is honoured".
    [Fact]
    public async Task Save_PlainOverHtml_StoredAsPlain()
    {
        var (world, bug) = await BugAsync();
        await WorkItemsTestData.SaveAsync(factory, world, bug, new { description = "<p>a</p>", descriptionFormat = "html" });

        var answer = await WorkItemsTestData.SaveAsync(factory, world, bug, new { description = "text" });

        Assert.Equal(("text", "plain"), (Text(answer.Body, "description"), Text(answer.Body, "descriptionFormat")));
    }

    // --- US-4: limits ---------------------------------------------------------------------------------------------

    // US-4 · "The visible limit is enforced on the server".
    [Fact]
    public async Task Save_Html_VisibleOver100000_TooLong()
    {
        var (world, bug) = await BugAsync();

        var answer = await WorkItemsTestData.SaveAsync(
            factory, world, bug, new { description = "<p>" + new string('a', 100_001) + "</p>", descriptionFormat = "html" });

        Assert.Equal(HttpStatusCode.BadRequest, answer.Status);
        Assert.Equal(Description, Reason(answer));
    }

    // US-4 · "Exactly the limit is accepted", also 100 000 emoji.
    [Theory]
    [InlineData("a")]
    [InlineData("\U0001F600")]
    public async Task Save_Html_ExactlyTheLimit_Stored(string unit)
    {
        var (world, bug) = await BugAsync();
        var html = "<p>" + Repeat(unit, 100_000) + "</p>";

        var answer = await WorkItemsTestData.SaveAsync(factory, world, bug, new { description = html, descriptionFormat = "html" });

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Equal(html, Text((await WorkItemsTestData.ReadAsync(factory, world, bug)), "description"));
    }

    // US-4 · "Markup does not count against the visible limit": 99 000 visible, markup well over 100 000.
    [Fact]
    public async Task Save_Html_99000VisibleWithMarkup_Stored()
    {
        var (world, bug) = await BugAsync();
        var html = "<p>" + Repeat("<strong>ab</strong><em>c</em>", 33_000) + "</p>";

        var answer = await WorkItemsTestData.SaveAsync(factory, world, bug, new { description = html, descriptionFormat = "html" });

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.True(Text(answer.Body, "description")!.Length > 100_000);
    }

    // US-4 · "The markup ceiling has its own reason": 50 000 visible characters, markup over 1 000 000 code points.
    [Fact]
    public async Task Save_Html_MarkupOverCeiling_MarkupTooLarge()
    {
        var (world, bug) = await BugAsync();
        var html = "<p>" + Repeat("<strong>a</strong><em>b</em>", 25_000) + "</p>" + Repeat("<p><u><s>x</s></u></p>", 5_000);
        var before = await WorkItemsTestData.StoredAsync(factory, world.ProjectId);

        // Each paragraph of the filler is 7 + 20 markup code points; widen it until the ceiling is crossed.
        html += Repeat("<p><strong><em><u><s>y</s></u></em></strong></p>", 10_000);
        var answer = await WorkItemsTestData.SaveAsync(factory, world, bug, new { description = html, descriptionFormat = "html" });

        Assert.Equal(HttpStatusCode.BadRequest, answer.Status);
        Assert.Equal(MarkupTooLarge, Reason(answer));
        Assert.Equal(
            "Слишком много разметки. Упростите форматирование или сократите текст.",
            answer.Body.GetProperty("errors").GetProperty("description")[0].GetProperty("detail").GetString());
        Assert.Equal(before, await WorkItemsTestData.StoredAsync(factory, world.ProjectId));
    }

    // US-4 · "Over both limits, the markup reason wins".
    [Fact]
    public async Task Save_Html_BothOver_MarkupOnly()
    {
        var (world, bug) = await BugAsync();
        var html = "<p>" + Repeat("<strong>a</strong>", 100_001) + "</p>" + Repeat("<p><strong><em><u><s>y</s></u></em></strong></p>", 20_000);

        var answer = await WorkItemsTestData.SaveAsync(factory, world, bug, new { description = html, descriptionFormat = "html" });

        Assert.Equal(MarkupTooLarge, Reason(answer));
        Assert.Single(answer.Body.GetProperty("errors").GetProperty("description").EnumerateArray());
    }

    // US-4 · "An oversized request is refused before parsing".
    [Fact]
    public async Task Save_Html_RequestOver2000000_MarkupTooLarge()
    {
        var (world, bug) = await BugAsync();

        var answer = await WorkItemsTestData.SaveAsync(
            factory, world, bug, new { description = new string('a', 2_000_001), descriptionFormat = "html" });

        Assert.Equal(HttpStatusCode.BadRequest, answer.Status);
        Assert.Equal(MarkupTooLarge, Reason(answer));
    }

    // US-4 · "Limits come before the empty rule": whitespace only, over the ceiling, is refused, not stored as "".
    [Fact]
    public async Task Save_Html_BlankOverCeiling_RefusedNotEmpty()
    {
        var (world, bug) = await BugAsync();
        await WorkItemsTestData.SaveAsync(factory, world, bug, new { description = "<p>was</p>", descriptionFormat = "html" });

        var answer = await WorkItemsTestData.SaveAsync(
            factory, world, bug, new { description = new string(' ', 1_000_001), descriptionFormat = "html" });

        Assert.Equal(MarkupTooLarge, Reason(answer));
        Assert.Equal("<p>was</p>", Text(await WorkItemsTestData.ReadAsync(factory, world, bug), "description"));
    }

    // US-4 · "Amplifying input is refused promptly" / US-2 · "The reduction stops early".
    [Fact]
    public async Task Save_Html_Amplifying_RefusedWithin2s()
    {
        var (world, bug) = await BugAsync();
        var unit = "<a href='https://e.org'>x</a>y";
        var html = "<strong><em><u><s><mark class='rt-hl-red'><span class='rt-color-red'><span class='rt-size-lg'>"
            + Repeat(unit, (2_000_000 - 100) / unit.Length);

        // Warm the host up so the timing is the reduction's, not the first request's.
        await WorkItemsTestData.SaveAsync(factory, world, bug, new { description = "<p>warm</p>", descriptionFormat = "html" });

        var watch = Stopwatch.StartNew();
        var answer = await WorkItemsTestData.SaveAsync(factory, world, bug, new { description = html, descriptionFormat = "html" });
        watch.Stop();

        Assert.Equal(MarkupTooLarge, Reason(answer));
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(2), $"Took {watch.Elapsed}.");
    }

    // US-4 · "Legacy worst cases fit the ceiling": 100 000 '&' and 100 000 line feeds, as the client would send them.
    [Theory]
    [InlineData("amp")]
    [InlineData("lf")]
    public async Task Save_LegacyWorstCases_Stored(string kind)
    {
        var (world, bug) = await BugAsync();
        var html = kind == "amp" ? "<p>" + Repeat("&amp;", 100_000) + "</p>" : Repeat("<p></p>", 100_001);

        var answer = await WorkItemsTestData.SaveAsync(factory, world, bug, new { description = html, descriptionFormat = "html" });

        Assert.Equal(HttpStatusCode.OK, answer.Status);

        // 100 001 empty paragraphs have no visible character: stored as the empty description (BR-RT-6).
        Assert.Equal(kind == "amp" ? html : "", Text(answer.Body, "description"));
    }

    // US-4 · "A lone surrogate counts one on both sides": the stored value holds U+FFFD.
    [Fact]
    public async Task Save_Html_LoneSurrogate_StoredAsReplacementCharacter()
    {
        var (world, bug) = await BugAsync();

        var answer = await WorkItemsTestData.SaveAsync(
            factory, world, bug, new { description = "<p>a\uD83Db</p>", descriptionFormat = "html" });

        Assert.Equal("<p>a�b</p>", Text(answer.Body, "description"));
    }

    // US-4 · "Response time holds": a 100 000-character visible description is read and saved within a second.
    [Fact]
    public async Task Save_And_Get_100000VisibleCharacters_Within1s()
    {
        var (world, bug) = await BugAsync();
        var html = "<p>" + new string('a', 100_000) + "</p>";
        await WorkItemsTestData.SaveAsync(factory, world, bug, new { description = "<p>warm</p>", descriptionFormat = "html" });

        var watch = Stopwatch.StartNew();
        var saved = await WorkItemsTestData.SaveAsync(factory, world, bug, new { description = html, descriptionFormat = "html" });
        var saveTime = watch.Elapsed;
        watch.Restart();
        var read = await WorkItemsTestData.GetAsync(factory, world, bug);
        var readTime = watch.Elapsed;

        Assert.Equal(HttpStatusCode.OK, saved.Status);
        Assert.Equal(HttpStatusCode.OK, read.Status);
        Assert.True(saveTime < TimeSpan.FromSeconds(1), $"Save took {saveTime}.");
        Assert.True(readTime < TimeSpan.FromSeconds(1), $"Read took {readTime}.");
    }

    // --- US-2, US-7 through the API -------------------------------------------------------------------------------

    // US-7 · "The editor's canonical form is the server's", through PATCH and GET, and idempotent on a second save.
    [Fact]
    public async Task Save_FixtureCanonicalCases_RoundTripThroughPatchGet()
    {
        var (world, bug) = await BugAsync();
        var path = Path.Combine(RepositoryRoot(), "contracts", "rich-text", "fixtures", "canonical.json");
        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(path));

        foreach (var fixture in document.RootElement.GetProperty("cases").EnumerateArray())
        {
            var id = fixture.GetProperty("id").GetString();
            var canonical = fixture.GetProperty("canonical").GetString()!;

            var first = await WorkItemsTestData.SaveAsync(
                factory, world, bug, new { description = fixture.GetProperty("input").GetString(), descriptionFormat = "html" });

            Assert.True(first.Status == HttpStatusCode.OK, $"{id}: {first.Status} {first.ErrorCode}");
            Assert.True(canonical == Text(first.Body, "description"), $"{id}: stored value differs from canonical.");
            Assert.Equal(canonical.Length == 0 ? "plain" : "html", Text(first.Body, "descriptionFormat"));

            var read = await WorkItemsTestData.ReadAsync(factory, world, bug);
            Assert.True(canonical == Text(read, "description"), $"{id}: read value differs from canonical.");

            var second = await WorkItemsTestData.SaveAsync(factory, world, bug, new { description = canonical, descriptionFormat = "html" });
            Assert.True(
                first.Body.GetProperty("changedAt").GetDateTimeOffset() == second.Body.GetProperty("changedAt").GetDateTimeOffset(),
                $"{id}: a second save of the canonical value moved changedAt.");
        }
    }

    // US-2 · "A hostile corpus does not survive": through the API.
    [Fact]
    public async Task Save_HostileCorpus_StoredValueClean()
    {
        var (world, bug) = await BugAsync();
        var path = Path.Combine(RepositoryRoot(), "contracts", "rich-text", "fixtures", "hostile-corpus.json");
        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(path));

        foreach (var fixture in document.RootElement.GetProperty("cases").EnumerateArray())
        {
            var id = fixture.GetProperty("id").GetString();
            var answer = await WorkItemsTestData.SaveAsync(
                factory, world, bug, new { description = fixture.GetProperty("input").GetString(), descriptionFormat = "html" });

            Assert.True(answer.Status == HttpStatusCode.OK, $"{id}: {answer.Status} {answer.ErrorCode}");

            var stored = Text(answer.Body, "description")!.ToLowerInvariant();

            foreach (var pattern in fixture.GetProperty("mustNotContain").EnumerateArray())
            {
                Assert.False(stored.Contains(pattern.GetString()!.ToLowerInvariant(), StringComparison.Ordinal), $"{id}: stored value holds '{pattern}'.");
            }
        }
    }

    // --- US-3 server half ----------------------------------------------------------------------------------------

    // US-3 · "Saving another field leaves Описание untouched": a legacy and an html description, byte for byte.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Save_OtherFieldOnly_DescriptionByteForByteUntouched(bool html)
    {
        var (world, bug) = await BugAsync();
        var description = html ? "<p>a</p><table><tbody><tr><td><p>b</p></td></tr></tbody></table>" : "  шаг 1:\r\n\t<b>x</b>   y\n";
        await WorkItemsTestData.SaveAsync(
            factory, world, bug, new { description, descriptionFormat = html ? "html" : (string?)null });

        var answer = await WorkItemsTestData.SaveAsync(
            factory, world, bug, new { stateId = world.Types["BUG"].StateId("Активна").ToString() });

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Equal((description, html ? "html" : "plain"), (Text(answer.Body, "description"), Text(answer.Body, "descriptionFormat")));
    }

    // US-3 · "Other reads are not broken": the list never returns the description or its format.
    [Fact]
    public async Task List_NeverCarriesTheDescriptionOrItsFormat()
    {
        var (world, bug) = await BugAsync();
        await WorkItemsTestData.SaveAsync(factory, world, bug, new { description = "<p>a</p>", descriptionFormat = "html" });

        var row = (await WorkItemsTestData.ListAsync(factory, world)).Body.GetProperty("data")[0];

        Assert.False(row.TryGetProperty("description", out _));
        Assert.False(row.TryGetProperty("descriptionFormat", out _));
    }

    // mod-003 § 0.4: a bad path with an oversized description is the path's 404.
    [Fact]
    public async Task Save_BadPathOversizedDescription_404()
    {
        var (world, _) = await BugAsync();

        var answer = await WorkItemsTestData.SaveAsync(
            factory, world, "MSK-BUG-999", new { description = new string('a', 2_000_001), descriptionFormat = "html" });

        Assert.Equal(HttpStatusCode.NotFound, answer.Status);
    }

    // mod-003 § 0.4: the description's reason joins the title's.
    [Fact]
    public async Task Save_TitleAndDescriptionBothInvalid_BothReasonsAtOnce()
    {
        var (world, bug) = await BugAsync();

        var answer = await WorkItemsTestData.SaveAsync(
            factory, world, bug, new { title = " ", description = "<p>x</p>", descriptionFormat = "markdown" });

        Assert.Equal("workitems.workitem.title_required", Reason(answer, "title"));
        Assert.Equal(FormatUnknown, Reason(answer));
    }

    // Create returns the new field too, plain and empty.
    [Fact]
    public async Task Create_ReturnsDescriptionFormatPlain()
    {
        var world = await WorkItemsTestData.CreateWorldAsync(factory);
        var identifier = await WorkItemsTestData.CreateItemAsync(factory, world, "TASK", "Задача");

        var item = await WorkItemsTestData.ReadAsync(factory, world, identifier);

        Assert.Equal(("", "plain"), (Text(item, "description"), Text(item, "descriptionFormat")));
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Mustask.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Mustask.sln not found above the test binaries.");
    }
}
