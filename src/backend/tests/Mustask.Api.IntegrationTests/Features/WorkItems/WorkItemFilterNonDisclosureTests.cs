using System.Net;
using Mustask.Api.IntegrationTests.Features.Organizations;
using static Mustask.Api.IntegrationTests.Features.WorkItems.SearchTestData;

namespace Mustask.Api.IntegrationTests.Features.WorkItems;

/// <summary>
/// feat-003 BR-WS-10, BR-WS-15, spec § Security: a filter value never tells what it is. Whatever a value of «Статус», «Исполнитель» or
/// «Поиск» names — nothing at all, another organization's or another project's, an item that is deleted while the bin is not chosen,
/// a character PostgreSQL cannot hold, a value past any column — the answer has the same status and the same body as for any other
/// value that names nothing: <c>200 { "count": 0, "data": [] }</c>, never a 400, never a 500, and never a trace of the object the
/// value would have named. A value beside a valid one adds nothing.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class WorkItemFilterNonDisclosureTests(MustaskApiFactory factory)
{
    private const string Nothing = """{"count":0,"data":[]}""";

    private async Task<(HttpStatusCode Status, string Body)> ReadAsync(SearchWorld data, string rawQuery)
    {
        var answer = await OrganizationsApi.SendAsync(
            factory, HttpMethod.Get, WorkItemsTestData.List(data.World) + rawQuery, token: OrganizationsTokens.SignedIn());

        return (answer.Status, answer.Body.GetRawText());
    }

    private static string Escaped(string name, string value) => $"{name}={Uri.EscapeDataString(value)}";

    // US-1 outline "An address value that names no state selects nothing" / BR-WS-10: each kind of value alike.
    [Fact]
    public async Task State_EveryKindOfValueThatNamesNoState_AnswersTheSameStatusAndBody()
    {
        var data = await CreateAsync(factory);

        // A word no process of the stand has, the stored name in another letter case, with a space, with LIKE characters, SQL, and a
        // name past any column.
        var values = new[]
        {
            "Чужая", "новая", "НОВАЯ", "Новая ", " Новая", "Новая%", "Н_вая", "%", "_", "\\", "'; DROP TABLE work_items; --", new string('я', 5_000),
        };

        foreach (var value in values)
        {
            Assert.Equal((HttpStatusCode.OK, Nothing), await ReadAsync(data, "?" + Escaped("state", value)));
        }

        Assert.Equal((HttpStatusCode.OK, Nothing), await ReadAsync(data, "?state=%00"));
        Assert.Equal((HttpStatusCode.OK, Nothing), await ReadAsync(data, "?state=%D0%9D%00"));
    }

    // US-2 outline / BR-WS-10, BR-WS-15: each kind of value that names nobody assigned alike — malformed, unknown, another organization's
    // user, a member assigned nothing, a person assigned only deleted items while the bin is not chosen, the letter case of `none`.
    [Fact]
    public async Task Assignee_EveryKindOfValueThatNamesNobodyAssigned_AnswersTheSameStatusAndBody()
    {
        var data = await CreateAsync(factory);
        var other = await WorkItemsTestData.CreateWorldAsync(factory, "OTH", "Чужой");
        var oleg = await WorkItemsTestData.AddMemberAsync(factory, other, "Олег", "Сидоров");
        var foreign = await WorkItemsTestData.CreateItemAsync(factory, other, "TASK", "Задача Олега");
        await WorkItemsTestData.SaveAsync(factory, other, foreign, new { assignee = new { userId = oleg } });

        var values = new[]
        {
            "not-a-guid", Guid.NewGuid().ToString(), oleg, data.People.Ann, data.People.Rita, data.People.Zoya, "NONE", "None", "none ", "%", "'; DROP TABLE work_items; --",
            new string('u', 5_000),
        };

        foreach (var value in values)
        {
            Assert.Equal((HttpStatusCode.OK, Nothing), await ReadAsync(data, "?" + Escaped("assigneeId", value)));
        }

        Assert.Equal((HttpStatusCode.OK, Nothing), await ReadAsync(data, "?assigneeId=a%00b"));
        Assert.Equal((HttpStatusCode.OK, Nothing), await ReadAsync(data, "?assigneeId=%00"));
    }

    // US-3 outline / BR-WS-9, BR-WS-15: every keyword that finds no item of this project answers alike — a part of an identifier,
    // another project's or organization's identifier, a number past every item's or past int, a deleted item while the bin is not
    // chosen, a keyword no title can hold (a line break, U+0000, 256 characters).
    [Fact]
    public async Task Search_EveryKeywordThatFindsNothing_AnswersTheSameStatusAndBody()
    {
        var data = await CreateAsync(factory);
        var other = await WorkItemsTestData.CreateWorldAsync(factory, "OTH", "Чужой");
        var foreign = await WorkItemsTestData.CreateItemAsync(factory, other, "US", "Чужая история");
        var sibling = await WorkItemsTestData.CreateItemAsync(factory, data.World with { ProjectId = data.Teams.OtherProjectId, Key = "BET" }, "US", "Соседняя история");

        var values = new[]
        {
            "ALP-BUG-4", "BUG-42", "ALP-US-42", "ALP-BUG-042", "042", "99999999999", "2147483648", "ALP-BUG-99999999999", foreign, sibling,
            "Чужая история", "Соседняя история", "нет такого заголовка", data.Id("US5"), "Возврат", "5", new string('я', 256), new string('я', 5_000),
        };

        foreach (var value in values)
        {
            Assert.Equal((HttpStatusCode.OK, Nothing), await ReadAsync(data, "?" + Escaped("search", value)));
        }

        foreach (var escaped in new[] { "%00", "оплат%00а", "оплат%0Aа", "оплат%0Dа", "оплат%E2%80%A8а", "%0Aоплат%0Aа%0A" })
        {
            Assert.Equal((HttpStatusCode.OK, Nothing), await ReadAsync(data, "?search=" + escaped));
        }
    }

    // BR-WS-10 · "beside a valid value it adds nothing": the answer is the valid value's own, byte for byte, whatever else is listed.
    [Fact]
    public async Task AValueThatNamesNothing_BesideAValidOne_AddsNothing()
    {
        var data = await CreateAsync(factory);

        var state = await ReadAsync(data, "?" + Escaped("state", "Новая"));
        Assert.Equal(state, await ReadAsync(data, "?" + Escaped("state", "Новая") + "&" + Escaped("state", "Чужая") + "&state=%00"));

        var assignee = await ReadAsync(data, "?" + Escaped("assigneeId", data.People.Mia));
        Assert.Equal(assignee, await ReadAsync(data, "?" + Escaped("assigneeId", data.People.Mia) + "&assigneeId=nobody&assigneeId=%00&" + Escaped("assigneeId", data.People.Zoya)));
    }
}
