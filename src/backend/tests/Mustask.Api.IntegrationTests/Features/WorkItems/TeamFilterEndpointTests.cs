using System.Net;
using System.Text.Json;
using Mustask.Api.IntegrationTests.Features.Boards;
using Mustask.Api.IntegrationTests.Features.Organizations;
using Mustask.Domain.Identity;
using static Mustask.Api.IntegrationTests.Features.WorkItems.OwningTeamTestData;

namespace Mustask.Api.IntegrationTests.Features.WorkItems;

/// <summary>
/// The «Команда» filter (mod-017 US-5, BR-OT-8, api-contract § 3): the repeated <c>teamId</c> — a team's id or <c>none</c> — on the
/// project's list, backlog and board, one meaning on all three; a value that names no team of the project selects nothing and is
/// never an error. Every test builds its own world (<see cref="OwningTeamTestData"/>): the database is shared and never emptied.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class TeamFilterEndpointTests(MustaskApiFactory factory)
{
    private async Task<Item> ItemAsync(OwningTeamWorld world, string typeKey, string title, Guid? team, string? parentId = null)
    {
        var answer = await OrganizationsApi.SendAsync(
            factory,
            HttpMethod.Post,
            WorkItemsTestData.List(world.Items),
            new
            {
                requestId = Guid.NewGuid().ToString(),
                typeId = world.Items.Types[typeKey].Id.ToString(),
                title,
                team = new { teamId = team?.ToString() },
                parentId,
            },
            OrganizationsTokens.SignedIn());
        Assert.Equal(HttpStatusCode.Created, answer.Status);

        return new Item(answer.Body.GetProperty("id").GetGuid(), answer.Body.GetProperty("identifier").GetString()!);
    }

    private static string Query(params string[] values) => values.Length == 0 ? string.Empty : "teamId=" + string.Join("&teamId=", values.Select(Uri.EscapeDataString));

    private Task<Answer> ListAsync(OwningTeamWorld world, string query, string? token = null, int? page = null) =>
        OrganizationsApi.SendAsync(
            factory,
            HttpMethod.Get,
            WorkItemsTestData.List(world.Items) + "?" + (page is null ? string.Empty : $"page={page}&") + query,
            token: token ?? OrganizationsTokens.SignedIn());

    private Task<Answer> BacklogAsync(OwningTeamWorld world, string query, string level = "stories", string? token = null) =>
        OrganizationsApi.SendAsync(
            factory,
            HttpMethod.Get,
            $"{OrganizationsApi.Project(world.OrganizationId, world.ProjectId)}/backlogs/{level}?{query}",
            token: token ?? OrganizationsTokens.SignedIn());

    private Task<Answer> BoardAsync(OwningTeamWorld world, string query, string level = "stories", string? token = null) =>
        OrganizationsApi.SendAsync(
            factory,
            HttpMethod.Get,
            $"{OrganizationsApi.Project(world.OrganizationId, world.ProjectId)}/boards/{level}?{query}",
            token: token ?? OrganizationsTokens.SignedIn());

    private static string[] ListIdentifiers(Answer answer) =>
        answer.Body.GetProperty("data").EnumerateArray().Select(row => row.GetProperty("identifier").GetString()!).ToArray();

    private static string[] Rows(Answer answer) =>
        answer.Body.GetProperty("items").EnumerateArray().Select(row => row.GetProperty("identifier").GetString()!).ToArray();

    private static string[] Cards(Answer answer) => ProjectBoardsTestData.AllCards(answer.Body);

    /// <summary>MARS, VENUS and no team each own two stories; one of MARS's is closed, one removed.</summary>
    private async Task<(OwningTeamWorld World, Dictionary<string, Item> Items)> ArrangeAsync()
    {
        var world = await CreateWorldAsync(factory);
        var items = new Dictionary<string, Item>
        {
            ["m1"] = await ItemAsync(world, "US", "Марс 1", world.Mars),
            ["v1"] = await ItemAsync(world, "US", "Венера 1", world.Venus),
            ["n1"] = await ItemAsync(world, "US", "Ничья 1", null),
            ["m2"] = await ItemAsync(world, "US", "Марс 2", world.Mars),
            ["v2"] = await ItemAsync(world, "US", "Венера 2", world.Venus),
            ["n2"] = await ItemAsync(world, "US", "Ничья 2", null),
        };
        var type = world.Items.Types["US"];
        await WorkItemsTestData.SaveAsync(
            factory, world.Items, items["m2"].Identifier, new { stateId = type.StateId(Mustask.Domain.Process.StateCategory.Completed).ToString() });

        return (world, items);
    }

    // US-5 · "the list shows the team": every row carries the team's id and name, or null.
    [Fact]
    public async Task List_RowsCarryTheTeam()
    {
        var (world, items) = await ArrangeAsync();

        var answer = await ListAsync(world, string.Empty);

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        var teams = answer.Body.GetProperty("data").EnumerateArray()
            .ToDictionary(row => row.GetProperty("identifier").GetString()!, row => TeamOf(row));
        Assert.Equal((world.Mars, "MARS"), teams[items["m1"].Identifier]);
        Assert.Equal((world.Venus, "VENUS"), teams[items["v2"].Identifier]);
        Assert.Null(teams[items["n1"].Identifier]);
    }

    // US-5 · "filtered by one, several, none": an item matches when it is owned by any chosen team, or by none when `none` is
    // chosen; every type and state the list shows, and the count follows.
    [Theory]
    [InlineData("m", "mars")]
    [InlineData("v", "venus")]
    [InlineData("n", "none")]
    [InlineData("mv", "mars,venus")]
    [InlineData("mn", "mars,none")]
    [InlineData("vn", "venus,none")]
    [InlineData("mvn", "mars,venus,none")]
    public async Task List_TeamFilter_OneSeveralAndNone_MatchAnyValue_AllTypesAndStates(string expectedOwners, string chosen)
    {
        var (world, items) = await ArrangeAsync();
        var named = chosen.Split(',')
            .Select(value => value switch { "mars" => world.Mars.ToString(), "venus" => world.Venus.ToString(), _ => "none" })
            .ToArray();
        var expected = items
            .Where(pair => expectedOwners.Contains(pair.Key[0], StringComparison.Ordinal))
            .Select(pair => pair.Value.Identifier)
            .Order(StringComparer.Ordinal)
            .ToArray();

        var answer = await ListAsync(world, Query(named));

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Equal(expected, ListIdentifiers(answer).Order(StringComparer.Ordinal));
        Assert.Equal(expected.Length, answer.Body.GetProperty("count").GetInt32());
    }

    // US-5 · "pages from the first page": the count is the number of matching items; the last page holds the rest.
    [Fact]
    public async Task List_FilterPaging_CountAndLastPage()
    {
        var world = await CreateWorldAsync(factory);
        var task = world.Items.Types["TASK"];
        await WorkItemsTestData.ExecuteAsync(factory, $"""
            INSERT INTO work_items (id, project_id, number, type_id, state_id, area_id, title, description, creation_request_id, created_at, created_by, changed_at, owning_team_id)
            SELECT gen_random_uuid(), {world.ProjectId}, n, {task.Id}, {task.StateId("Новая")}, {world.Items.RootAreaId},
                   'Задача ' || n, '', gen_random_uuid(), now(), 'filter-test', now() + n * interval '1 second',
                   CASE WHEN n <= 52 THEN {world.Mars} ELSE {world.Venus} END
            FROM generate_series(1, 55) AS n
            """);

        var first = await ListAsync(world, Query(world.Mars.ToString()), page: 1);
        var last = await ListAsync(world, Query(world.Mars.ToString()), page: 2);
        var past = await ListAsync(world, Query(world.Mars.ToString()), page: 3);
        var other = await ListAsync(world, Query(world.Venus.ToString()));

        Assert.Equal((52, 50), (first.Body.GetProperty("count").GetInt32(), first.Body.GetProperty("data").GetArrayLength()));
        Assert.Equal((52, 2), (last.Body.GetProperty("count").GetInt32(), last.Body.GetProperty("data").GetArrayLength()));
        Assert.Equal((52, 0), (past.Body.GetProperty("count").GetInt32(), past.Body.GetProperty("data").GetArrayLength()));
        Assert.Equal((3, 3), (other.Body.GetProperty("count").GetInt32(), other.Body.GetProperty("data").GetArrayLength()));
    }

    // US-5 · "matches nothing": a team with no item answers an empty page and a count of zero.
    [Fact]
    public async Task List_FilterWithNoMatch_IsEmpty_CountZero()
    {
        var (world, _) = await ArrangeAsync();

        var answer = await ListAsync(world, Query(world.OwnTeam.ToString()));

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Equal((0, 0), (answer.Body.GetProperty("count").GetInt32(), answer.Body.GetProperty("data").GetArrayLength()));
    }

    // US-5 · "project backlog filtered": the tree is built over the matching items only — a child whose parent does not match
    // is a top-level row, «Порядок» numbers the visible top-level rows 1..n in the project's order.
    [Fact]
    public async Task ProjectBacklog_Filter_TreeOverMatchingOnly_OrderNumbersVisibleRows()
    {
        var world = await CreateWorldAsync(factory);
        var parent = await ItemAsync(world, "FEAT", "Родитель", world.Mars);
        var marsChild = await ItemAsync(world, "FEAT", "Свой", world.Mars, parent.Id.ToString());
        var venusChild = await ItemAsync(world, "FEAT", "Чужой", world.Venus, parent.Id.ToString());
        await SetOwningTeamAsync(factory, venusChild.Id, world.Venus);
        var venusTop = await ItemAsync(world, "FEAT", "Венера сама", world.Venus);

        var venus = await BacklogAsync(world, Query(world.Venus.ToString()), "features");
        var mars = await BacklogAsync(world, Query(world.Mars.ToString()), "features");
        var all = await BacklogAsync(world, string.Empty, "features");

        Assert.Equal(HttpStatusCode.OK, venus.Status);
        Assert.Equal([venusChild.Identifier, venusTop.Identifier], Rows(venus));
        Assert.Equal([1, 2], venus.Body.GetProperty("items").EnumerateArray().Select(row => row.GetProperty("order").GetInt32()));
        Assert.Equal(
            $"{parent.Identifier}[{marsChild.Identifier}]",
            BacklogTreeTestData.Shape(mars.Body.GetProperty("items").EnumerateArray()));
        Assert.Equal(
            $"{parent.Identifier}[{marsChild.Identifier} {venusChild.Identifier}] {venusTop.Identifier}",
            BacklogTreeTestData.Shape(all.Body.GetProperty("items").EnumerateArray()));
    }

    // US-5 · "board filtered; the checklist not narrowed": the columns and their cards follow the filter; a card's checklist
    // lists its children whatever their team.
    [Fact]
    public async Task ProjectBoard_Filter_ColumnsFollow_ChecklistNotNarrowed()
    {
        var world = await CreateWorldAsync(factory);
        var story = await ItemAsync(world, "US", "Историй Марса", world.Mars);
        var venusStory = await ItemAsync(world, "US", "Историй Венеры", world.Venus);
        var task = await ItemAsync(world, "TASK", "Задача Венеры", world.Venus, story.Id.ToString());
        await SetOwningTeamAsync(factory, task.Id, world.Venus);

        var board = await BoardAsync(world, Query(world.Mars.ToString()));

        Assert.Equal(HttpStatusCode.OK, board.Status);
        Assert.Equal([story.Identifier], Cards(board));
        Assert.DoesNotContain(venusStory.Identifier, Cards(board));
        var card = board.Body.GetProperty("columns").EnumerateArray().SelectMany(column => column.GetProperty("items").EnumerateArray()).Single();
        Assert.Equal([task.Identifier], card.GetProperty("checklist").EnumerateArray().Select(entry => entry.GetProperty("identifier").GetString()));
    }

    // US-5 · "the three surfaces agree": for every combination of MARS, VENUS and «Без команды» the list, the backlog and the
    // board hold the same items.
    [Theory]
    [InlineData("m")]
    [InlineData("v")]
    [InlineData("n")]
    [InlineData("mv")]
    [InlineData("mn")]
    [InlineData("vn")]
    [InlineData("mvn")]
    public async Task ThreeSurfaces_AgreeForEveryCombination(string combination)
    {
        var (world, _) = await ArrangeAsync();
        var values = combination.Select(code => code switch { 'm' => world.Mars.ToString(), 'v' => world.Venus.ToString(), _ => "none" }).ToArray();
        var query = Query(values);

        var list = ListIdentifiers(await ListAsync(world, query)).Order(StringComparer.Ordinal).ToArray();
        var backlog = Rows(await BacklogAsync(world, query)).Order(StringComparer.Ordinal).ToArray();
        var board = Cards(await BoardAsync(world, query)).Order(StringComparer.Ordinal).ToArray();

        Assert.Equal(list, backlog);
        Assert.Equal(list, board);
        Assert.Equal(combination.Length * 2, list.Length);
    }

    // US-5 · "a filtered backlog reorders against the full list": the reorder takes no filter, so the items a filter hid keep
    // their relative order.
    [Fact]
    public async Task ProjectReorder_WhileFiltered_HiddenItemsKeepTheirRelativeOrder()
    {
        var world = await CreateWorldAsync(factory);
        var boards = BoardsViewOf(world);
        var a = await ItemAsync(world, "US", "A", world.Mars);
        var b = await ItemAsync(world, "US", "B", world.Venus);
        var c = await ItemAsync(world, "US", "C", world.Mars);
        var d = await ItemAsync(world, "US", "D", world.Venus);
        await ProjectBoardsTestData.ReorderToAsync(factory, boards, a, b, c, d);
        Assert.Equal([a.Identifier, c.Identifier], Rows(await BacklogAsync(world, Query(world.Mars.ToString()))));

        await ProjectBoardsTestData.MoveAsync(factory, boards, BoardsTestData.Top(c));

        Assert.Equal([c.Identifier, a.Identifier], Rows(await BacklogAsync(world, Query(world.Mars.ToString()))));
        Assert.Equal([b.Identifier, d.Identifier], Rows(await BacklogAsync(world, Query(world.Venus.ToString()))));
        Assert.Equal([c.Identifier, a.Identifier, b.Identifier, d.Identifier], await ProjectBoardsTestData.RowsAsync(factory, boards));
    }

    // US-5 · "a value that is not a team of the project": a malformed value, an unknown id, a deleted team's, another project's
    // and another organization's all answer alike — status and body identical — with no item and a total of zero, on all three
    // surfaces; nothing is validated and nothing is disclosed (BR-OT-8, BR-OT-16).
    [Fact]
    public async Task Filter_NonTeamValues_FourIdenticalAnswers_TotalZero()
    {
        var (world, _) = await ArrangeAsync();
        var values = new[]
        {
            "not-an-id",
            Guid.CreateVersion7().ToString(),
            world.DeletedTeam.ToString(),
            world.OtherProjectTeam.ToString(),
            world.OtherOrganizationTeam.ToString(),
            string.Empty,
            "NONE",
        };

        var lists = new List<Answer>();
        var backlogs = new List<Answer>();
        var boards = new List<Answer>();

        foreach (var value in values)
        {
            lists.Add(await ListAsync(world, Query(value)));
            backlogs.Add(await BacklogAsync(world, Query(value)));
            boards.Add(await BoardAsync(world, Query(value)));
        }

        foreach (var group in new[] { lists, backlogs, boards })
        {
            Assert.All(group, answer => Assert.Equal(HttpStatusCode.OK, answer.Status));
            Assert.All(group, answer => Assert.Equal(group[0].Body.GetRawText(), answer.Body.GetRawText()));
        }

        Assert.Equal((0, 0), (lists[0].Body.GetProperty("count").GetInt32(), lists[0].Body.GetProperty("data").GetArrayLength()));
        Assert.Empty(Rows(backlogs[0]));
        Assert.Empty(Cards(boards[0]));
    }

    // US-5 · "the choice lives with the visit": a filtered read changes nothing stored — the teams, the places, «Изменён».
    [Fact]
    public async Task Filter_ChangesNothingStored()
    {
        var (world, items) = await ArrangeAsync();
        var before = await WorkItemsTestData.StoredAsync(factory, world.ProjectId);
        var teamsBefore = new List<Guid?>();

        foreach (var item in items.Values)
        {
            teamsBefore.Add((await StoredAsync(factory, world.ProjectId, item.Identifier)).TeamId);
        }

        await ListAsync(world, Query(world.Mars.ToString()));
        await BacklogAsync(world, Query("none"));
        await BoardAsync(world, Query(world.Venus.ToString(), "none"));

        Assert.Equal(before, await WorkItemsTestData.StoredAsync(factory, world.ProjectId));
        var teamsAfter = new List<Guid?>();

        foreach (var item in items.Values)
        {
            teamsAfter.Add((await StoredAsync(factory, world.ProjectId, item.Identifier)).TeamId);
        }

        Assert.Equal(teamsBefore, teamsAfter);
    }

    // US-5 · "everyone who reads may filter": «Читатель» gets the same answers as a member.
    [Fact]
    public async Task Filter_Reader_SameAsMember()
    {
        var (world, _) = await ArrangeAsync();
        var reader = await MembershipTestData.NewMemberAsync(factory, world.OrganizationId, OrganizationRole.Viewer);
        var query = Query(world.Mars.ToString(), "none");

        var member = await ListAsync(world, query);
        var asReader = await ListAsync(world, query, reader.Token);
        var backlog = await BacklogAsync(world, query, token: reader.Token);
        var board = await BoardAsync(world, query, token: reader.Token);

        Assert.Equal(HttpStatusCode.OK, asReader.Status);
        Assert.Equal(member.Body.GetRawText(), asReader.Body.GetRawText());
        Assert.Equal((HttpStatusCode.OK, HttpStatusCode.OK), (backlog.Status, board.Status));
    }

    // US-5 · the filter on an address that is not found answers the address's own code first.
    [Fact]
    public async Task Filter_OnAnUnknownProject_AnswersTheProjectNotFound()
    {
        var (world, _) = await ArrangeAsync();

        var answer = await OrganizationsApi.SendAsync(
            factory,
            HttpMethod.Get,
            $"{OrganizationsApi.Organization(world.OrganizationId)}/projects/{Guid.CreateVersion7()}/workitems?teamId=none",
            token: OrganizationsTokens.SignedIn());

        Assert.Equal((HttpStatusCode.NotFound, "organizations.project.not_found"), (answer.Status, answer.ErrorCode));
        Assert.Equal(JsonValueKind.Object, answer.Body.ValueKind);
    }
}
