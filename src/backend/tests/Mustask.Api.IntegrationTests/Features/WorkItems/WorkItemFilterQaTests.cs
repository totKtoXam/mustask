using System.Net;
using Mustask.Api.IntegrationTests.Features.Organizations;
using static Mustask.Api.IntegrationTests.Features.WorkItems.SearchTestData;

namespace Mustask.Api.IntegrationTests.Features.WorkItems;

/// <summary>
/// feat-003 QA pass: the combinations and edges the endpoint suites of the developer leave open, written from the spec and the
/// contract before the code was read (qa.md, Backend). Same world as the stories (<see cref="SearchTestData"/>).
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class WorkItemFilterQaTests(MustaskApiFactory factory)
{
    private async Task<string[]> RowsAsync(SearchWorld data, string query)
    {
        var answer = await ListAsync(factory, data.World, query);

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Equal(answer.Body.GetProperty("data").GetArrayLength(), Count(answer));

        return Identifiers(answer);
    }

    // BR-WS-2, BR-WS-4 · all four filters at once with «Удалена» among the states: US-5 alone (Ivan's, MARS, «Возврат оплаты»),
    // and each clear widens in the way the intersection says.
    [Fact]
    public async Task AllFour_WithUdalena_ListTheDeletedItemThatMatchesEveryOne_AndEachClearWidens()
    {
        var data = await CreateAsync(factory);
        var mars = data.Teams.Mars.ToString();

        Assert.Equal(
            Of(data, "US5"),
            await RowsAsync(data, Query(states: ["Удалена"], assignees: [data.People.Ivan], search: "возврат", teams: [mars])));

        // Without the team: still US-5 (FEAT-6 is Zoya's); without the keyword: still US-5 (Ivan's other item is live, «Разрешена»).
        Assert.Equal(Of(data, "US5"), await RowsAsync(data, Query(states: ["Удалена"], assignees: [data.People.Ivan], search: "возврат")));
        Assert.Equal(Of(data, "US5"), await RowsAsync(data, Query(states: ["Удалена"], assignees: [data.People.Ivan], teams: [mars])));

        // Without the assignee: the keyword and the team alone still pick US-5 out of the bin.
        Assert.Equal(Of(data, "US5"), await RowsAsync(data, Query(states: ["Удалена"], search: "возврат", teams: [mars])));

        // Without «Удалена» the deleted item is gone, and nothing live matches Ivan, the team MARS and «возврат».
        Assert.Empty(await RowsAsync(data, Query(assignees: [data.People.Ivan], search: "возврат", teams: [mars])));

        // «Удалена» and «Разрешена» with Ivan: US-5 (deleted, changed after every live item) and BUG-2 (his live resolved bug).
        Assert.Equal(Of(data, "US5", "BUG2"), await RowsAsync(data, Query(states: ["Удалена", "Разрешена"], assignees: [data.People.Ivan])));
    }

    // BR-WS-8, BR-OT-8 · «Без команды» and «Не назначен» are two different `none`s, and each combines with the state.
    [Fact]
    public async Task NoTeam_And_NoAssignee_Combine_WithStates_EachItsOwnNone()
    {
        var data = await CreateAsync(factory);

        Assert.Equal(Of(data, "US3"), await RowsAsync(data, Query(states: ["Разрешена"], teams: ["none"])));
        Assert.Equal(Of(data, "US3"), await RowsAsync(data, Query(states: ["Разрешена"], assignees: ["none"])));
        Assert.Equal(Of(data, "US3"), await RowsAsync(data, Query(assignees: ["none"], teams: ["none"])));

        // Both deleted items have a team and an assignee: «Не назначен» and «Без команды» find nothing in the bin.
        Assert.Empty(await RowsAsync(data, Query(states: ["Удалена"], assignees: ["none"])));
        Assert.Empty(await RowsAsync(data, Query(states: ["Удалена"], teams: ["none"])));
    }

    // BR-WS-9 · a number matches a deleted item by its number while «Удалена» is chosen, and the identifier of a live item finds
    // that item only (the state filter narrows the keyword's result as it narrows any other).
    [Fact]
    public async Task Search_ByNumberAndIdentifier_IsNarrowedByTheChosenStates()
    {
        var data = await CreateAsync(factory);

        Assert.Empty(await RowsAsync(data, Query(search: "5")));
        Assert.Equal(Of(data, "US5"), await RowsAsync(data, Query(states: ["Удалена"], search: "5")));
        Assert.Equal(Of(data, "FEAT6"), await RowsAsync(data, Query(states: ["Удалена"], search: data.Id("FEAT6").ToLowerInvariant())));
        Assert.Empty(await RowsAsync(data, Query(states: ["Новая"], search: data.Id("US1"))));
        Assert.Equal(Of(data, "US1"), await RowsAsync(data, Query(states: ["Активна"], search: data.Id("US1"))));
    }

    // api-contract § 2.2 · "Filters other than `state` do not narrow it": a keyword, an assignee or a team in the options' request line
    // changes nothing, and an unknown parameter is ignored.
    [Fact]
    public async Task Options_IgnoreEveryParameterButState()
    {
        var data = await CreateAsync(factory);
        var plain = await OptionsAsync(factory, data.World);

        var noisy = await OptionsAsync(
            factory, data.World, Query(assignees: [data.People.Mia], search: "оплат", teams: [data.Teams.Venus.ToString()]) + "&page=2&unknown=1");

        Assert.Equal(HttpStatusCode.OK, noisy.Status);
        Assert.Equal(plain.Body.GetRawText(), noisy.Body.GetRawText());
    }

    // api-contract § 0, § 2.1 · both reads answer 401 without a token, whatever the filter.
    [Fact]
    public async Task Reads_WithoutAToken_Answer401()
    {
        var data = await CreateAsync(factory);
        var list = WorkItemsTestData.List(data.World);

        var withFilter = await OrganizationsApi.SendAsync(factory, HttpMethod.Get, list + Query(states: ["Удалена"], search: "оплат"));
        var options = await OrganizationsApi.SendAsync(factory, HttpMethod.Get, OptionsPath(data.World) + Query(states: ["Удалена"]));

        Assert.Equal(HttpStatusCode.Unauthorized, withFilter.Status);
        Assert.Equal(HttpStatusCode.Unauthorized, options.Status);
    }

    // spec § Option sizes · about 120 chosen people still fit the request line and answer as one filter: the accepted bound is far above
    // what the panel's own choices reach, and a value that names nobody beside Mia's id adds nothing.
    [Fact]
    public async Task Assignee_AHundredAndTwentyValues_StillFitTheRequestLine()
    {
        var data = await CreateAsync(factory);
        var ids = Enumerable.Range(0, 120).Select(_ => Guid.NewGuid().ToString()).Append(data.People.Mia).ToArray();

        var answer = await ListAsync(factory, data.World, Query(assignees: ids));

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Equal(Of(data, "BUG42", "US1"), Identifiers(answer));
    }

    // BR-WS-9 · «ё» typed as «е» plus a combining diaeresis (the decomposed form some keyboards and pasted texts produce) is still «ё».
    // Open defect qa.md D-B1 (minor): the fold replaces the precomposed letter only, so the decomposed one finds nothing. Remove the
    // Skip when it is fixed (normalise the keyword to NFC before the fold).
    [Fact(Skip = "Open defect qa.md D-B1 (minor): a decomposed «ё» is not folded to «е».")]
    public async Task Search_ADecomposedYo_IsStillYo()
    {
        var world = await WorkItemsTestData.CreateWorldAsync(factory);
        var item = await WorkItemsTestData.CreateItemAsync(factory, world, "TASK", "Новогодняя ёлка");

        var decomposed = "ёлка";
        var answer = await ListAsync(factory, world, Query(search: decomposed));

        Assert.Equal([item], Identifiers(answer));
    }

    // BR-WS-9 · letters outside the BMP and combining sequences are text: no error, and they find the title that holds them.
    [Fact]
    public async Task Search_ASurrogatePair_IsText_AndIsNoError()
    {
        var world = await WorkItemsTestData.CreateWorldAsync(factory);
        var item = await WorkItemsTestData.CreateItemAsync(factory, world, "TASK", "Релиз 🚀 в пятницу");
        await WorkItemsTestData.CreateItemAsync(factory, world, "TASK", "Релиз в понедельник");

        Assert.Equal([item], Identifiers(await ListAsync(factory, world, Query(search: "🚀"))));
        Assert.Equal([item], Identifiers(await ListAsync(factory, world, Query(search: "релиз 🚀"))));
    }

    // BR-WS-9, BR-WS-10 · a request line that is not UTF-8 (a stray byte, half a surrogate pair) is text no title holds: 200, nothing,
    // for the keyword, a state name and an assignee id alike.
    [Theory]
    [InlineData("search=%FF")]
    [InlineData("search=%ED%A0%BD")]
    [InlineData("state=%FF")]
    [InlineData("assigneeId=%ED%A0%BD")]
    public async Task AnInvalidUtf8Value_NamesNothing_AndIsNoError(string parameter)
    {
        var data = await CreateAsync(factory);

        var answer = await OrganizationsApi.SendAsync(
            factory, HttpMethod.Get, WorkItemsTestData.List(data.World) + "?" + parameter, token: OrganizationsTokens.SignedIn());

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Empty(Identifiers(answer));
        Assert.Equal(0, Count(answer));
    }
}
