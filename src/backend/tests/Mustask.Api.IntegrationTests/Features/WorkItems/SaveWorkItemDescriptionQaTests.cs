using System.Diagnostics;
using System.Net;
using System.Text;
using System.Text.Json;
using Mustask.Api.IntegrationTests.Features.Organizations;
using Xunit.Abstractions;

namespace Mustask.Api.IntegrationTests.Features.WorkItems;

/// <summary>
/// feat-018 backend QA: what the developer's endpoint tests do not reach (raw JSON on the wire, ordering of 404 and
/// 400, concurrency of two description writes, an end-to-end document at the markup ceiling). Written from the spec
/// and api-contract, not from the implementation.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class SaveWorkItemDescriptionQaTests(MustaskApiFactory factory, ITestOutputHelper output)
{
    private async Task<(WorkItemsWorld World, string Bug)> BugAsync()
    {
        var world = await WorkItemsTestData.CreateWorldAsync(factory);
        var bug = await WorkItemsTestData.CreateItemAsync(factory, world, "BUG", "Падение при входе");

        return (world, bug);
    }

    /// <summary>A PATCH whose body is the raw JSON text given: what a foreign client can put on the wire.</summary>
    private async Task<Answer> PatchRawAsync(WorkItemsWorld world, string identifier, string rawJson)
    {
        using var request = new HttpRequestMessage(HttpMethod.Patch, WorkItemsTestData.Item(world, identifier))
        {
            Content = new StringContent(rawJson, Encoding.UTF8, "application/json"),
        };
        request.WithBearer(OrganizationsTokens.SignedIn());

        using var response = await factory.CreateClient().SendAsync(request);
        var text = await response.Content.ReadAsStringAsync();
        var json = string.IsNullOrEmpty(text) ? default : JsonDocument.Parse(text).RootElement.Clone();

        return new Answer(response.StatusCode, response.Headers.Location, response.Content.Headers.ContentType?.MediaType, json);
    }

    // BR-RT-8 / US-4 "A lone surrogate counts one on both sides", through the wire (QA D-4, minor). The test client's
    // serializer replaces a lone surrogate before it is sent, so the developer's test of this scenario never carries
    // one. Sent as the JSON escape a browser's JSON.stringify writes, the platform's binder refuses the whole request
    // before the reducer sees it: 400 http.bad_request, nothing stored. Characterised here; the spec and the contract
    // say "replaced by U+FFFD, stored" (§ 3) and are silent about the binder.
    [Fact]
    public async Task Save_Html_LoneSurrogateEscapeOnTheWire_IsRefusedByTheBinder_NothingStored()
    {
        var (world, bug) = await BugAsync();
        var before = await WorkItemsTestData.StoredAsync(factory, world.ProjectId);

        var answer = await PatchRawAsync(world, bug, "{\"description\":\"<p>a\\ud83db</p>\",\"descriptionFormat\":\"html\"}");

        Assert.Equal(HttpStatusCode.BadRequest, answer.Status);
        Assert.Equal("http.bad_request", answer.ErrorCode);
        Assert.Equal(before, await WorkItemsTestData.StoredAsync(factory, world.ProjectId));
    }

    // BR-RT-9: U+0000 in new html input is removed.
    [Fact]
    public async Task Save_Html_NulEscapeOnTheWire_IsRemoved()
    {
        var (world, bug) = await BugAsync();

        var answer = await PatchRawAsync(world, bug, "{\"description\":\"<p>a\\u0000b</p>\",\"descriptionFormat\":\"html\"}");

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Equal("<p>ab</p>", answer.Body.GetProperty("description").GetString());
    }

    // api-contract § 1: absent and null are "not declared"; both mean plain, stored as sent.
    [Theory]
    [InlineData("{\"description\":\"<b>x</b>\",\"descriptionFormat\":null}")]
    [InlineData("{\"description\":\"<b>x</b>\"}")]
    public async Task Save_FormatNullOrAbsent_IsPlainAsSent(string rawJson)
    {
        var (world, bug) = await BugAsync();

        var answer = await PatchRawAsync(world, bug, rawJson);

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Equal("<b>x</b>", answer.Body.GetProperty("description").GetString());
        Assert.Equal("plain", answer.Body.GetProperty("descriptionFormat").GetString());
    }

    // api-contract § 1: another JSON type fails binding, with or without a description; nothing stored.
    [Theory]
    [InlineData("{\"descriptionFormat\":true}")]
    [InlineData("{\"descriptionFormat\":[\"html\"]}")]
    [InlineData("{\"descriptionFormat\":{\"v\":\"html\"}}")]
    [InlineData("{\"description\":\"x\",\"descriptionFormat\":1.5}")]
    public async Task Save_FormatOfAnotherJsonType_Is400BadRequest(string rawJson)
    {
        var (world, bug) = await BugAsync();
        var before = await WorkItemsTestData.StoredAsync(factory, world.ProjectId);

        var answer = await PatchRawAsync(world, bug, rawJson);

        Assert.Equal(HttpStatusCode.BadRequest, answer.Status);
        Assert.Equal("http.bad_request", answer.ErrorCode);
        Assert.Equal(before, await WorkItemsTestData.StoredAsync(factory, world.ProjectId));
    }

    // mod-003 § 0.4 / api-contract § 4: the path's 404 comes before the 400 of an unknown format.
    [Fact]
    public async Task Save_UnknownFormat_OnAMissingItem_Is404Not400()
    {
        var (world, _) = await BugAsync();

        var answer = await WorkItemsTestData.SaveAsync(
            factory, world, "MSK-BUG-999", new { description = "<p>x</p>", descriptionFormat = "markdown" });

        Assert.Equal(HttpStatusCode.NotFound, answer.Status);
    }

    // The same for a project of another organization (the address is resolved from the outside in).
    [Fact]
    public async Task Save_UnknownFormat_OnAnotherOrganizationsItem_Is404()
    {
        var (world, bug) = await BugAsync();
        var (other, _) = await BugAsync();

        var answer = await WorkItemsTestData.SaveAsync(
            factory, other with { ProjectId = world.ProjectId }, bug, new { description = "<p>x</p>", descriptionFormat = "markdown" });

        Assert.Equal(HttpStatusCode.NotFound, answer.Status);
    }

    // BR-RT-10 on the stored pair: a plain write of markup over html is honoured and then readable as plain, and an
    // html re-send of the markup comes back reduced (a change), never both formats at once.
    [Fact]
    public async Task Save_PlainThenHtmlOfTheSameText_AlternatesTheFormat()
    {
        var (world, bug) = await BugAsync();

        var plain = await WorkItemsTestData.SaveAsync(factory, world, bug, new { description = "<p>a</p>" });
        var html = await WorkItemsTestData.SaveAsync(factory, world, bug, new { description = "<p>a</p>", descriptionFormat = "html" });
        var again = await WorkItemsTestData.SaveAsync(factory, world, bug, new { description = "<p>a</p>", descriptionFormat = "plain" });

        Assert.Equal("plain", plain.Body.GetProperty("descriptionFormat").GetString());
        Assert.Equal("html", html.Body.GetProperty("descriptionFormat").GetString());
        Assert.Equal("plain", again.Body.GetProperty("descriptionFormat").GetString());
        Assert.True(
            html.Body.GetProperty("changedAt").GetDateTimeOffset() > plain.Body.GetProperty("changedAt").GetDateTimeOffset());
        Assert.True(
            again.Body.GetProperty("changedAt").GetDateTimeOffset() > html.Body.GetProperty("changedAt").GetDateTimeOffset());
    }

    // BR-M3-25 / the lock: two saves of one item at once, one html, one touching another field. Neither is lost, none
    // is a 5xx, and the description is the html as reduced.
    [Fact]
    public async Task Save_ConcurrentDescriptionAndTitle_BothLand()
    {
        var (world, bug) = await BugAsync();

        var results = await Task.WhenAll(
            Enumerable.Range(0, 6).Select(index => index % 2 == 0
                ? WorkItemsTestData.SaveAsync(factory, world, bug, new { description = $"<p>v{index}</p>", descriptionFormat = "html" })
                : WorkItemsTestData.SaveAsync(factory, world, bug, new { title = $"Заголовок {index}" })));

        Assert.All(results, answer => Assert.Equal(HttpStatusCode.OK, answer.Status));

        var final = await WorkItemsTestData.ReadAsync(factory, world, bug);
        Assert.Matches("^<p>v[024]</p>$", final.GetProperty("description").GetString());
        Assert.Equal("html", final.GetProperty("descriptionFormat").GetString());
        Assert.Matches("^Заголовок [135]$", final.GetProperty("title").GetString());
    }

    // The same writer twice at once with the same html: one change, no error, and the pair stays html.
    [Fact]
    public async Task Save_ConcurrentIdenticalHtml_IsOneStableValue()
    {
        var (world, bug) = await BugAsync();
        var body = new { description = "<b>same</b>", descriptionFormat = "html" };

        var results = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => WorkItemsTestData.SaveAsync(factory, world, bug, body)));

        Assert.All(results, answer => Assert.Equal(HttpStatusCode.OK, answer.Status));
        Assert.All(results, answer => Assert.Equal("<p><strong>same</strong></p>", answer.Body.GetProperty("description").GetString()));
        Assert.Single(results.Select(answer => answer.Body.GetProperty("changedAt").GetString()).Distinct());
    }

    // QA R-1 (risk, evidence): a canonical table document of 999 834 units, under both limits (visible about 86 000),
    // end to end. On the QA machine, shared and CPU-saturated, it was refused with description_markup_too_large after
    // about 2 s: the parse budget fired on a legitimate value. The invariant asserted is the one that must hold on any
    // host: a 200 that round-trips, or the markup reason; never a 5xx, never another reason. The figure is output.
    [Fact]
    public async Task Save_TableDocumentAtTheCeiling_IsStoredWhole_OrRefusedWithTheMarkupReason()
    {
        var (world, bug) = await BugAsync();
        var builder = new StringBuilder("<table><tbody>");
        var row = "<tr>" + string.Concat(Enumerable.Repeat("<td><p>x</p></td><td><p></p></td>", 5)) + "</tr>"; // 5 cells of one character, 5 empty: visible stays under 100 000

        while (builder.Length + row.Length + 16 < 1_000_000)
        {
            builder.Append(row);
        }

        var html = builder.Append("</tbody></table>").ToString();
        await WorkItemsTestData.SaveAsync(factory, world, bug, new { description = "<p>warm</p>", descriptionFormat = "html" });

        var watch = Stopwatch.StartNew();
        var saved = await WorkItemsTestData.SaveAsync(factory, world, bug, new { description = html, descriptionFormat = "html" });
        watch.Stop();

        output.WriteLine($"markup={html.Length} save={watch.ElapsedMilliseconds} ms status={(int)saved.Status} body={(saved.Status == HttpStatusCode.OK ? "ok" : saved.Body.GetProperty("errors").GetRawText())}");

        // R-1, closed: the bounds are deterministic, so a legitimate document is stored on any host.
        Assert.Equal(HttpStatusCode.OK, saved.Status);
        Assert.Equal(html, saved.Body.GetProperty("description").GetString());

        var reopened = await WorkItemsTestData.ReadAsync(factory, world, bug);
        Assert.Equal(html, reopened.GetProperty("description").GetString());
    }
}
