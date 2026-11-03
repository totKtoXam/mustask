using System.Net;
using System.Text.Json;
using Mustask.Api.IntegrationTests.Features.Organizations;
using Mustask.Api.IntegrationTests.Features.WorkItems;
using static Mustask.Api.IntegrationTests.Features.Boards.TeamAcrossProjectsTestData;

namespace Mustask.Api.IntegrationTests.Features.Boards;

/// <summary>
/// The aggregate views' reads (feat-048 api-contract § 2; BR-AV-1..12, US-11): the grouped counts in the order of BR-AV-4, a group's
/// pages by cursor (ADR-0036) that neither repeat nor lose a row when the data moves between pages, the cells of a board, the
/// organization-wide list, and the 400/404 answers. Every test builds its own organization (<see cref="CreateAsync"/>): MARS in
/// Альфа, Бета and Гамма, VENUS in Альфа alone; here Альфа and Бета sit under the department «Разработка», Гамма at the top level.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class AggregateViewEndpointTests(MustaskApiFactory factory)
{
    private static string Org(Cast cast) => $"/api/organizations/{cast.OrganizationId}";

    private static Task<Answer> GetAsync(Cast cast, string path, MustaskApiFactory factory) =>
        OrganizationsApi.SendAsync(factory, HttpMethod.Get, $"{Org(cast)}{path}", token: OrganizationsTokens.SignedIn());

    /// <summary>Альфа: A1, A2 (MARS), A3 (VENUS), A4 (no team); Бета: B1 (MARS); Гамма: G1 (no team); the departments set.</summary>
    private async Task<(Cast Cast, Dictionary<string, Item> Items, Guid Department)> SeedAsync()
    {
        var cast = await CreateAsync(factory);
        var items = new Dictionary<string, Item>
        {
            ["A1"] = await StoryAsync(factory, cast.Alpha, "A1", cast.Mars),
            ["A2"] = await StoryAsync(factory, cast.Alpha, "A2", cast.Mars),
            ["A3"] = await StoryAsync(factory, cast.Alpha, "A3", cast.Venus),
            ["A4"] = await StoryAsync(factory, cast.Alpha, "A4", null),
            ["B1"] = await StoryAsync(factory, cast.Beta, "B1", cast.Mars),
            ["G1"] = await StoryAsync(factory, cast.Gamma, "G1", null),
        };

        var department = await OrganizationsTestData.AddDepartmentAsync(factory, cast.OrganizationId, "Разработка");

        foreach (var view in new[] { cast.Alpha, cast.Beta })
        {
            await WorkItemsTestData.ExecuteAsync(
                factory, $"UPDATE areas SET parent_id = {department.Id} WHERE project_id = {view.ProjectId} AND parent_id IS NULL");
        }

        // A known change order, newest first: G1, B1, A4, A3, A2, A1.
        var at = new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);
        var order = new[] { "A1", "A2", "A3", "A4", "B1", "G1" };

        for (var index = 0; index < order.Length; index++)
        {
            await SetChangedAtAsync(items[order[index]], at.AddMinutes(index));
        }

        return (cast, items, department.Id);
    }

    private Task SetChangedAtAsync(Item item, DateTimeOffset at) =>
        WorkItemsTestData.ExecuteAsync(factory, $"UPDATE work_items SET changed_at = {at} WHERE id = {item.Id}");

    private static string[] Names(Answer answer) =>
        answer.Body.GetProperty("groups").EnumerateArray().Select(group => group.TryGetProperty("name", out var name) && name.ValueKind == JsonValueKind.String ? name.GetString()! : "-").ToArray();

    private static int[] Counts(Answer answer, string property = "count") =>
        answer.Body.GetProperty("groups").EnumerateArray().Select(group => group.GetProperty(property).GetInt32()).ToArray();

    private static string[] Identifiers(Answer answer, string property = "rows") =>
        answer.Body.GetProperty(property).EnumerateArray().Select(row => row.GetProperty("identifier").GetString()!).ToArray();

    private static string? NextCursor(Answer answer) =>
        answer.Body.TryGetProperty("nextCursor", out var cursor) && cursor.ValueKind == JsonValueKind.String ? cursor.GetString() : null;

    private static string Reason(Answer answer, string field) =>
        answer.Body.GetProperty("errors").GetProperty(field)[0].GetProperty("code").GetString()!;

    // BR-AV-4 · by team, «Без команды» first, then by name; the count is of the narrowed view, the total of the whole.
    [Fact]
    public async Task Groups_ByTeam_PutNoTeamFirst_ThenByName_AndCountTheItems()
    {
        var (cast, _, _) = await SeedAsync();

        var answer = await GetAsync(cast, "/backlogs/stories/groups?grouping=team", factory);

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Equal("team", answer.Body.GetProperty("grouping").GetString());
        Assert.Equal(["-", "MARS", "VENUS"], Names(answer));
        Assert.Equal([2, 3, 1], Counts(answer));
        Assert.Equal([2, 3, 1], Counts(answer, "total"));
    }

    // BR-AV-5 · a filter narrows the count, the total stays («3 из 5»).
    [Fact]
    public async Task Groups_WithAFilter_NarrowTheCount_AndKeepTheTotal()
    {
        var (cast, _, _) = await SeedAsync();

        var answer = await GetAsync(cast, "/backlogs/stories/groups?grouping=team&projectId=" + cast.Beta.ProjectId, factory);

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Equal(["MARS"], Names(answer));
        Assert.Equal([1], Counts(answer));
        Assert.Equal([3], Counts(answer, "total"));
    }

    // BR-AV-4 · by department, «Без департамента» (a project at the top level) first.
    [Fact]
    public async Task Groups_ByDepartment_PutNoDepartmentFirst()
    {
        var (cast, _, _) = await SeedAsync();

        var answer = await GetAsync(cast, "/backlogs/stories/groups?grouping=department", factory);

        Assert.Equal(["-", "Разработка"], Names(answer));
        Assert.Equal([1, 5], Counts(answer));
    }

    // BR-AV-4 · by project, by name; an empty project (Дельта) is absent.
    [Fact]
    public async Task Groups_ByProject_AreByName_AndTheEmptyOnesAbsent()
    {
        var (cast, _, _) = await SeedAsync();

        var answer = await GetAsync(cast, "/backlogs/stories/groups?grouping=project", factory);

        Assert.Equal(["Альфа", "Бета", "Гамма"], Names(answer));
        Assert.Equal([4, 1, 1], Counts(answer));
        Assert.Equal(["ALP", "BET", "GAM"], answer.Body.GetProperty("groups").EnumerateArray().Select(group => group.GetProperty("key").GetString()!));
    }

    // BR-AV-1 · without a grouping there is one group.
    [Fact]
    public async Task Groups_WithoutAGrouping_AreOneGroup()
    {
        var (cast, _, _) = await SeedAsync();

        var answer = await GetAsync(cast, "/backlogs/stories/groups", factory);

        Assert.Equal([6], Counts(answer));
        Assert.Equal("none", answer.Body.GetProperty("grouping").GetString());
    }

    // BR-AV-9 · the board adds the count of every column to each group.
    [Fact]
    public async Task BoardGroups_CarryTheCountOfEveryColumn()
    {
        var (cast, _, _) = await SeedAsync();

        var answer = await GetAsync(cast, "/boards/stories/groups?grouping=team", factory);

        Assert.Equal(HttpStatusCode.OK, answer.Status);

        foreach (var group in answer.Body.GetProperty("groups").EnumerateArray())
        {
            var columns = group.GetProperty("columns").EnumerateArray().ToList();
            Assert.NotEmpty(columns);
            Assert.Equal(group.GetProperty("count").GetInt32(), columns.Sum(column => column.GetProperty("count").GetInt32()));
        }
    }

    // BR-AV-3 · a grouping the level does not offer, or one that is not one, is a 400 naming the field.
    [Theory]
    [InlineData("grouping=bogus")]
    [InlineData("grouping=team&teamId=none")]
    [InlineData("grouping=project&projectId=x&teamId=y")]
    public async Task Groups_WithAGroupingTheSelectorsDoNotOffer_Are400OnGrouping(string query)
    {
        var (cast, _, _) = await SeedAsync();

        var answer = await GetAsync(cast, "/backlogs/stories/groups?" + query, factory);

        Assert.Equal(HttpStatusCode.BadRequest, answer.Status);
        Assert.Equal("boards.validation_failed", answer.ErrorCode);
        Assert.Equal("boards.invalid_grouping", Reason(answer, "grouping"));
    }

    [Fact]
    public async Task Groups_OfAnUnknownLevel_Are404()
    {
        var (cast, _, _) = await SeedAsync();

        var answer = await GetAsync(cast, "/backlogs/nothing/groups", factory);

        Assert.Equal(HttpStatusCode.NotFound, answer.Status);
        Assert.Equal("boards.level.not_found", answer.ErrorCode);
    }

    // ADR-0036 · the pages follow the cursor in change-time order, newest first, and a row that moves between the pages is neither
    // repeated nor — when it moves behind the cursor — lost: A4 is changed after the first page was read.
    [Fact]
    public async Task Rows_ByCursor_FollowChangeTimeDescending_AndDoNotRepeatARowThatMoves()
    {
        var (cast, items, _) = await SeedAsync();
        var path = "/backlogs/stories/rows?grouping=team&group=none&limit=1";

        var first = await GetAsync(cast, path, factory);
        Assert.Equal(["G1"], first.Body.GetProperty("rows").EnumerateArray().Select(row => row.GetProperty("title").GetString()!));
        var cursor = NextCursor(first);
        Assert.NotNull(cursor);

        // The row already read is changed again and so moves to the front: the next page must not return it.
        await SetChangedAtAsync(items["G1"], new DateTimeOffset(2026, 10, 2, 0, 0, 0, TimeSpan.Zero));
        var second = await GetAsync(cast, path + "&cursor=" + Uri.EscapeDataString(cursor!), factory);

        Assert.Equal(["A4"], second.Body.GetProperty("rows").EnumerateArray().Select(row => row.GetProperty("title").GetString()!));
        Assert.Null(NextCursor(second));
    }

    [Fact]
    public async Task Rows_OfAGroup_ArePagedUntilTheEnd_WithoutATotal()
    {
        var (cast, _, department) = await SeedAsync();
        var seen = new List<string>();
        string? cursor = null;
        var pages = 0;

        do
        {
            var query = "/backlogs/stories/rows?grouping=department&group=none&limit=2" + (cursor is null ? string.Empty : "&cursor=" + Uri.EscapeDataString(cursor));
            var page = await GetAsync(cast, query, factory);
            Assert.Equal(HttpStatusCode.OK, page.Status);
            Assert.False(page.Body.TryGetProperty("total", out _));
            seen.AddRange(page.Body.GetProperty("rows").EnumerateArray().Select(row => row.GetProperty("title").GetString()!));
            cursor = NextCursor(page);
            pages++;
        }
        while (cursor is not null && pages < 10);

        Assert.Equal(["G1"], seen);

        var all = await GetAsync(cast, "/backlogs/stories/rows?grouping=department&group=" + department + "&limit=2", factory);
        Assert.Equal(2, all.Body.GetProperty("rows").GetArrayLength());
        Assert.NotNull(NextCursor(all));
    }

    // BR-AV-6 · a by-project group is in the project's rank order; with no place set, the creation order.
    [Fact]
    public async Task Rows_OfAProjectGroup_AreInTheProjectsRankOrder()
    {
        var (cast, _, _) = await SeedAsync();

        var answer = await GetAsync(cast, $"/backlogs/stories/rows?grouping=project&group={cast.Alpha.ProjectId}", factory);

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Equal(["A1", "A2", "A3", "A4"], answer.Body.GetProperty("rows").EnumerateArray().Select(row => row.GetProperty("title").GetString()!));
        Assert.Null(NextCursor(answer));
    }

    // OQ-4 · a by-project group of a project that is not active is the project's not-found.
    [Fact]
    public async Task Rows_OfADeactivatedProjectGroup_AreTheProjectsNotFound()
    {
        var (cast, _, _) = await SeedAsync();
        await DeactivateAsync(factory, cast, cast.Gamma);

        var answer = await GetAsync(cast, $"/backlogs/stories/rows?grouping=project&group={cast.Gamma.ProjectId}", factory);

        Assert.Equal(HttpStatusCode.NotFound, answer.Status);
        Assert.Equal("organizations.project.not_found", answer.ErrorCode);
    }

    // ADR-0036 · a malformed cursor, and one issued for another request, are the same 400 naming `cursor`.
    [Theory]
    [InlineData("not-a-cursor")]
    [InlineData("e30")]
    public async Task Rows_WithAMalformedCursor_Are400OnCursor(string cursor)
    {
        var (cast, _, _) = await SeedAsync();

        var answer = await GetAsync(cast, "/backlogs/stories/rows?grouping=team&group=none&cursor=" + cursor, factory);

        Assert.Equal(HttpStatusCode.BadRequest, answer.Status);
        Assert.Equal("boards.validation_failed", answer.ErrorCode);
        Assert.Equal("boards.invalid_cursor", Reason(answer, "cursor"));
    }

    [Fact]
    public async Task Rows_WithACursorOfAnotherGrouping_Are400OnCursor()
    {
        var (cast, _, _) = await SeedAsync();
        var first = await GetAsync(cast, "/backlogs/stories/rows?grouping=team&group=none&limit=1", factory);
        var cursor = NextCursor(first)!;

        var answer = await GetAsync(cast, "/backlogs/stories/rows?grouping=department&group=none&limit=1&cursor=" + Uri.EscapeDataString(cursor), factory);

        Assert.Equal(HttpStatusCode.BadRequest, answer.Status);
        Assert.Equal("boards.invalid_cursor", Reason(answer, "cursor"));
    }

    [Theory]
    [InlineData("limit=0")]
    [InlineData("limit=51")]
    [InlineData("limit=abc")]
    public async Task Rows_WithABadLimit_Are400OnLimit(string query)
    {
        var (cast, _, _) = await SeedAsync();

        var answer = await GetAsync(cast, "/backlogs/stories/rows?grouping=team&group=none&" + query, factory);

        Assert.Equal(HttpStatusCode.BadRequest, answer.Status);
        Assert.Equal("boards.invalid_parameter", Reason(answer, "limit"));
    }

    // BR-AV-7 · a cell is the cards of a lane in a column; the column is a key of the groups read.
    [Fact]
    public async Task Cells_HoldTheCardsOfALaneInAColumn_AndAnUnknownColumnIs400()
    {
        var (cast, _, _) = await SeedAsync();
        var groups = await GetAsync(cast, "/boards/stories/groups?grouping=team", factory);
        var mars = groups.Body.GetProperty("groups").EnumerateArray().Single(group => group.GetProperty("name").GetString() == "MARS");
        var column = mars.GetProperty("columns").EnumerateArray().Single(candidate => candidate.GetProperty("count").GetInt32() == 3).GetProperty("key").GetString();

        var cell = await GetAsync(cast, $"/boards/stories/cells?grouping=team&group={mars.GetProperty("id").GetGuid()}&column={column}", factory);
        var unknown = await GetAsync(cast, $"/boards/stories/cells?grouping=team&group={mars.GetProperty("id").GetGuid()}&column=Nowhere-9", factory);

        Assert.Equal(HttpStatusCode.OK, cell.Status);
        Assert.Equal(["B1", "A2", "A1"], cell.Body.GetProperty("cards").EnumerateArray().Select(card => card.GetProperty("title").GetString()!));
        Assert.Null(NextCursor(cell));
        Assert.Equal(HttpStatusCode.BadRequest, unknown.Status);
        Assert.Equal("boards.invalid_parameter", Reason(unknown, "column"));
    }

    // BR-NV-12 · the organization-wide list: every project's items, newest change first, the project and the team on each row.
    [Fact]
    public async Task List_HoldsTheItemsOfEveryProject_NewestFirst_WithTheirProjectAndTeam()
    {
        var (cast, _, _) = await SeedAsync();

        var answer = await GetAsync(cast, "/workitems", factory);

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Equal(6, answer.Body.GetProperty("count").GetInt32());
        Assert.Equal(["G1", "B1", "A4", "A3", "A2", "A1"], answer.Body.GetProperty("data").EnumerateArray().Select(row => row.GetProperty("title").GetString()!));
        var b1 = answer.Body.GetProperty("data").EnumerateArray().Single(row => row.GetProperty("title").GetString() == "B1");
        Assert.Equal("Бета", b1.GetProperty("project").GetProperty("name").GetString());
        Assert.True(b1.TryGetProperty("team", out var teamOfB1), b1.ToString());
        Assert.Equal("MARS", teamOfB1.GetProperty("name").GetString());
    }

    [Fact]
    public async Task List_NarrowedByTheKeyword_AndByATeamSelector()
    {
        var (cast, _, _) = await SeedAsync();

        var byTitle = await GetAsync(cast, "/workitems?search=A3", factory);
        var byTeam = await GetAsync(cast, $"/workitems?teamId={cast.Venus}", factory);
        var byNone = await GetAsync(cast, "/workitems?teamId=none", factory);

        Assert.Equal(["A3"], byTitle.Body.GetProperty("data").EnumerateArray().Select(row => row.GetProperty("title").GetString()!));
        Assert.Equal(["A3"], byTeam.Body.GetProperty("data").EnumerateArray().Select(row => row.GetProperty("title").GetString()!));
        Assert.Equal(2, byNone.Body.GetProperty("count").GetInt32());
    }

    [Fact]
    public async Task List_WithPageZero_Is400OnPage()
    {
        var (cast, _, _) = await SeedAsync();

        var answer = await GetAsync(cast, "/workitems?page=0", factory);

        Assert.Equal(HttpStatusCode.BadRequest, answer.Status);
        Assert.Equal("workitems.invalid_page", Reason(answer, "page"));
    }

    [Fact]
    public async Task AssigneeOptions_OfAnOrganizationWithNoAssignees_AreEmpty()
    {
        var (cast, _, _) = await SeedAsync();

        var answer = await GetAsync(cast, "/workitems/assignee-options", factory);

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Equal(0, answer.Body.GetArrayLength());
    }

    // The grouped «Рабочие элементы» pages like the others, by change time even for a by-project group (BR-AV-8).
    [Fact]
    public async Task WorkItemRows_OfAProjectGroup_AreByChangeTime_NotByRank()
    {
        var (cast, _, _) = await SeedAsync();

        var answer = await GetAsync(cast, $"/workitems/rows?grouping=project&group={cast.Alpha.ProjectId}", factory);

        Assert.Equal(["A4", "A3", "A2", "A1"], answer.Body.GetProperty("rows").EnumerateArray().Select(row => row.GetProperty("title").GetString()!));
    }

    [Fact]
    public async Task WorkItemGroups_AreCountedLikeTheOtherViews()
    {
        var (cast, _, _) = await SeedAsync();

        var answer = await GetAsync(cast, "/workitems/groups?grouping=team", factory);

        Assert.Equal(["-", "MARS", "VENUS"], Names(answer));
        Assert.Equal([2, 3, 1], Counts(answer));
    }
}
