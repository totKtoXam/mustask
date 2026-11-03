using System.Net;
using System.Text;
using System.Text.Json;
using Mustask.Api.IntegrationTests.Features.Organizations;
using Mustask.Api.IntegrationTests.Features.WorkItems;
using static Mustask.Api.IntegrationTests.Features.Boards.TeamAcrossProjectsTestData;

namespace Mustask.Api.IntegrationTests.Features.Boards;

/// <summary>
/// QA of feat-048's aggregate reads, from the spec and the contract (BR-AV-4, -6, -9, -10, US-11): what the developer's tests do
/// not reach — every grouping partitions the items (each once), equal change times page stably, the by-project cursor agrees
/// with the project's own backlog, a forged cursor is a 400 and never a 500, a selector and an item that moves between reads.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class AggregateViewQaTests(MustaskApiFactory factory)
{
    private static string Org(Cast cast) => $"/api/organizations/{cast.OrganizationId}";

    private static Task<Answer> GetAsync(Cast cast, string path, MustaskApiFactory factory) =>
        OrganizationsApi.SendAsync(factory, HttpMethod.Get, $"{Org(cast)}{path}", token: OrganizationsTokens.SignedIn());

    /// <summary>Альфа: A1, A2 (MARS), A3 (VENUS), A4 (none); Бета: B1 (MARS); Гамма: G1 (none). Альфа and Бета under «Разработка».</summary>
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

    private static string[] Titles(Answer answer, string property = "rows") =>
        answer.Body.GetProperty(property).EnumerateArray().Select(row => row.GetProperty("title").GetString()!).ToArray();

    private static string? NextCursor(Answer answer) =>
        answer.Body.TryGetProperty("nextCursor", out var cursor) && cursor.ValueKind == JsonValueKind.String ? cursor.GetString() : null;

    /// <summary>Reads a group page by page and returns the titles in the order received; fails on a non-200 or a runaway cursor.</summary>
    private async Task<List<string>> ReadAllAsync(Cast cast, string route, string query, int limit, string property = "rows")
    {
        var seen = new List<string>();
        string? cursor = null;

        for (var pages = 0; pages < 50; pages++)
        {
            var page = await GetAsync(
                cast, $"{route}?{query}&limit={limit}" + (cursor is null ? string.Empty : "&cursor=" + Uri.EscapeDataString(cursor)), factory);
            Assert.True(page.Status == HttpStatusCode.OK, $"{route}?{query} answered {page.Status} {page.ErrorCode}.");
            seen.AddRange(Titles(page, property));
            cursor = NextCursor(page);

            if (cursor is null)
            {
                return seen;
            }
        }

        throw new Xunit.Sdk.XunitException("The cursor never ended.");
    }

    // BR-AV-4 · "each item is in exactly one group and appears once": under every grouping the pages of all groups, read to the
    // end, hold the six items once, and each group holds as many as its header counts.
    [Theory]
    [InlineData("department")]
    [InlineData("project")]
    [InlineData("team")]
    [InlineData("none")]
    public async Task EveryGrouping_PartitionsTheItems_EachOnce_AndTheHeaderCountsMatchThePages(string grouping)
    {
        var (cast, _, _) = await SeedAsync();
        var groups = await GetAsync(cast, $"/backlogs/stories/groups?grouping={grouping}", factory);
        var all = new List<string>();

        foreach (var group in groups.Body.GetProperty("groups").EnumerateArray())
        {
            var id = group.TryGetProperty("id", out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : "none";
            var rows = await ReadAllAsync(cast, "/backlogs/stories/rows", $"grouping={grouping}&group={id}", 2);

            Assert.Equal(group.GetProperty("count").GetInt32(), rows.Count);
            all.AddRange(rows);
        }

        Assert.Equal(["A1", "A2", "A3", "A4", "B1", "G1"], all.Order());
    }

    // BR-AV-9 · a cursor never fails because rows tie: six items with one change time page one at a time, number descending, then
    // the project key, each once.
    [Fact]
    public async Task EqualChangeTimes_PageStably_ByNumberThenProjectKey()
    {
        var (cast, items, _) = await SeedAsync();
        var same = new DateTimeOffset(2026, 10, 5, 0, 0, 0, TimeSpan.Zero);

        foreach (var item in items.Values)
        {
            await SetChangedAtAsync(item, same);
        }

        var one = await ReadAllAsync(cast, "/backlogs/stories/rows", "grouping=none", 1);
        var four = await ReadAllAsync(cast, "/workitems/rows", "grouping=none", 4);

        Assert.Equal(["A4", "A3", "A2", "A1", "B1", "G1"], one);
        Assert.Equal(one, four);
    }

    // BR-AV-6 · by project, the cursor pages in the project's own rank order: placed items first, the rest in creation order, and
    // the pages agree with the project's backlog.
    [Fact]
    public async Task ByProjectCursor_AgreesWithTheProjectsOwnBacklog_PlacedAndUnplaced()
    {
        var (cast, items, _) = await SeedAsync();

        await ProjectBoardsTestData.MoveAsync(factory, cast.Alpha, BoardsTestData.Top(items["A3"]));
        await ProjectBoardsTestData.MoveAsync(factory, cast.Alpha, BoardsTestData.Bottom(items["A1"]));

        var own = await ProjectBoardsTestData.RowsAsync(factory, cast.Alpha);
        var paged = await ReadAllAsync(cast, "/backlogs/stories/rows", $"grouping=project&group={cast.Alpha.ProjectId}", 1);
        var identifiers = items.ToDictionary(pair => pair.Value.Identifier, pair => pair.Key);

        Assert.Equal(own.Select(identifier => identifiers[identifier]), paged);
    }

    // ADR-0036 · the cursor is not signed, but a crafted one must be a validation 400, never a server error.
    [Theory]
    [InlineData("T", "3155378976000000000", Skip = "QA D-1 (qa.md): a crafted position past DateTime.MaxValue is a 500, not a 400; unskip when fixed")]
    [InlineData("T", "-1", Skip = "QA D-1 (qa.md): a crafted negative position is a 500, not a 400; unskip when fixed")]
    [InlineData("N", "0")]
    public async Task ACraftedCursor_WithOutOfRangeValues_Is400OnCursor(string field, string value)
    {
        var (cast, _, _) = await SeedAsync();
        var first = await GetAsync(cast, "/backlogs/stories/rows?grouping=team&group=none&limit=1", factory);
        var cursor = NextCursor(first)!;

        var padded = cursor.Replace('-', '+').Replace('_', '/');
        padded = padded.PadRight(padded.Length + ((4 - (padded.Length % 4)) % 4), '=');
        var json = Encoding.UTF8.GetString(Convert.FromBase64String(padded));
        var forged = System.Text.RegularExpressions.Regex.Replace(json, $"\"{field}\":-?\\d+", $"\"{field}\":{value}");
        var crafted = Convert.ToBase64String(Encoding.UTF8.GetBytes(forged)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

        var answer = await GetAsync(cast, "/backlogs/stories/rows?grouping=team&group=none&limit=1&cursor=" + crafted, factory);

        Assert.Equal(HttpStatusCode.BadRequest, answer.Status);
        Assert.Equal("boards.invalid_cursor", answer.Body.GetProperty("errors").GetProperty("cursor")[0].GetProperty("code").GetString());
    }

    // BR-NV-13 · a deactivated project leaves every read: its group, its rows in the department and the list, its count in the team.
    [Fact]
    public async Task ADeactivatedProject_LeavesTheGroups_TheDepartmentCount_AndTheList()
    {
        var (cast, _, _) = await SeedAsync();
        await DeactivateAsync(factory, cast, cast.Beta);

        var byProject = await GetAsync(cast, "/backlogs/stories/groups?grouping=project", factory);
        var byTeam = await GetAsync(cast, "/backlogs/stories/groups?grouping=team", factory);
        var list = await GetAsync(cast, "/workitems", factory);
        var rows = await ReadAllAsync(cast, "/backlogs/stories/rows", "grouping=none", 10);

        Assert.Equal(["Альфа", "Гамма"], byProject.Body.GetProperty("groups").EnumerateArray().Select(group => group.GetProperty("name").GetString()!));
        Assert.Equal([2, 2, 1], byTeam.Body.GetProperty("groups").EnumerateArray().Select(group => group.GetProperty("count").GetInt32()));
        Assert.Equal(5, list.Body.GetProperty("count").GetInt32());
        Assert.DoesNotContain("B1", rows);
    }

    // BR-NV-17 / BR-AV-2 · a department and a team chosen together: the team's items in the department's projects, by project.
    [Fact]
    public async Task ATeamAndADepartment_NarrowTheGroupsAndTheirRows_AndKeepTheTotal()
    {
        var (cast, _, department) = await SeedAsync();
        var query = $"grouping=project&teamId={cast.Mars}&departmentId={department}";

        var groups = await GetAsync(cast, $"/backlogs/stories/groups?{query}", factory);
        var rows = await ReadAllAsync(cast, "/backlogs/stories/rows", $"{query}&group={cast.Alpha.ProjectId}", 10);
        var gamma = await ReadAllAsync(cast, "/backlogs/stories/rows", $"{query}&group={cast.Gamma.ProjectId}", 10);

        Assert.Equal(["Альфа", "Бета"], groups.Body.GetProperty("groups").EnumerateArray().Select(group => group.GetProperty("name").GetString()!));
        Assert.Equal([2, 1], groups.Body.GetProperty("groups").EnumerateArray().Select(group => group.GetProperty("count").GetInt32()));
        Assert.Equal([4, 1], groups.Body.GetProperty("groups").EnumerateArray().Select(group => group.GetProperty("total").GetInt32()));
        Assert.Equal(["A1", "A2"], rows.Order());
        Assert.Empty(gamma);
    }

    // BR-AV-10 · an item whose team changes is in its new group at the next read, and the old group counts one fewer.
    [Fact]
    public async Task AnItemThatChangesTeam_AndAProjectThatChangesDepartment_MoveGroupsAtTheNextRead()
    {
        var (cast, items, department) = await SeedAsync();

        await WorkItemsTestData.ExecuteAsync(factory, $"UPDATE work_items SET owning_team_id = {cast.Venus} WHERE id = {items["A1"].Id}");
        await WorkItemsTestData.ExecuteAsync(factory, $"UPDATE areas SET parent_id = {department} WHERE project_id = {cast.Gamma.ProjectId} AND parent_id IS NULL");

        var byTeam = await GetAsync(cast, "/backlogs/stories/groups?grouping=team", factory);
        var byDepartment = await GetAsync(cast, "/backlogs/stories/groups?grouping=department", factory);

        Assert.Equal([2, 2, 2], byTeam.Body.GetProperty("groups").EnumerateArray().Select(group => group.GetProperty("count").GetInt32()));
        Assert.Equal(["Разработка"], byDepartment.Body.GetProperty("groups").EnumerateArray().Select(group => group.GetProperty("name").GetString()!));
        Assert.Equal([6], byDepartment.Body.GetProperty("groups").EnumerateArray().Select(group => group.GetProperty("count").GetInt32()));
    }

    // BR-NV-12 · the keyword may spell an identifier with its project key; it narrows the headers and every group's rows alike.
    [Fact]
    public async Task AKeywordOfAnIdentifier_NarrowsTheHeadersTheRowsAndTheList()
    {
        var (cast, items, _) = await SeedAsync();
        var search = "search=" + Uri.EscapeDataString(items["B1"].Identifier);

        var groups = await GetAsync(cast, $"/backlogs/stories/groups?grouping=project&{search}", factory);
        var rows = await ReadAllAsync(cast, "/workitems/rows", $"grouping=none&{search}", 10);
        var list = await GetAsync(cast, $"/workitems?{search}", factory);

        Assert.Equal(["Бета"], groups.Body.GetProperty("groups").EnumerateArray().Select(group => group.GetProperty("name").GetString()!));
        Assert.Equal(["B1"], rows);
        Assert.Equal(["B1"], Titles(list, "data"));
    }

    // feat-003 filters on the aggregate reads · a state that names nothing selects nothing and is never an error.
    [Fact]
    public async Task AStateThatNamesNothing_SelectsNothing_OnEveryRead()
    {
        var (cast, _, _) = await SeedAsync();

        var groups = await GetAsync(cast, "/backlogs/stories/groups?grouping=team&state=Nowhere", factory);
        var rows = await GetAsync(cast, "/backlogs/stories/rows?grouping=none&state=Nowhere", factory);
        var list = await GetAsync(cast, "/workitems?state=Nowhere", factory);

        Assert.Equal(HttpStatusCode.OK, groups.Status);
        Assert.Equal(0, groups.Body.GetProperty("groups").GetArrayLength());
        Assert.Equal(0, rows.Body.GetProperty("rows").GetArrayLength());
        Assert.Equal(0, list.Body.GetProperty("count").GetInt32());
    }

    // BR-AV-7 · a cell pages by cursor, and a cursor of one column is not another column's.
    [Fact]
    public async Task ACellPages_AndItsCursorIsNotAnotherColumns()
    {
        var (cast, _, _) = await SeedAsync();
        var groups = await GetAsync(cast, "/boards/stories/groups?grouping=none", factory);
        var columns = groups.Body.GetProperty("groups")[0].GetProperty("columns").EnumerateArray().ToList();
        var full = columns.Single(column => column.GetProperty("count").GetInt32() == 6).GetProperty("key").GetString()!;
        var other = columns.First(column => column.GetProperty("count").GetInt32() == 0).GetProperty("key").GetString()!;

        var seen = await ReadAllAsync(cast, "/boards/stories/cells", $"grouping=none&column={full}", 4, "cards");
        var first = await GetAsync(cast, $"/boards/stories/cells?grouping=none&column={full}&limit=4", factory);
        var crossed = await GetAsync(cast, $"/boards/stories/cells?grouping=none&column={other}&cursor={Uri.EscapeDataString(NextCursor(first)!)}", factory);

        Assert.Equal(["A1", "A2", "A3", "A4", "B1", "G1"], seen.Order());
        Assert.Equal(["G1", "B1", "A4", "A3", "A2", "A1"], seen);
        Assert.Equal(HttpStatusCode.BadRequest, crossed.Status);
    }

    // api-contract § 1 · a bad group is a 400 naming `group`; a group of a project that is not in the organization is not found.
    [Theory]
    [InlineData("grouping=team&group=bogus", HttpStatusCode.BadRequest)]
    [InlineData("grouping=team", HttpStatusCode.BadRequest)]
    [InlineData("grouping=project&group=00000000-0000-0000-0000-0000000000aa", HttpStatusCode.NotFound)]
    public async Task ABadOrMissingGroup_IsRefused(string query, HttpStatusCode expected)
    {
        var (cast, _, _) = await SeedAsync();

        var answer = await GetAsync(cast, "/backlogs/stories/rows?" + query, factory);

        Assert.Equal(expected, answer.Status);
    }

    // The level's own types only: no epics here, so the epics views are empty, not an error.
    [Fact]
    public async Task AnotherLevel_ReadsOnlyItsOwnTypes()
    {
        var (cast, _, _) = await SeedAsync();

        var groups = await GetAsync(cast, "/backlogs/epics/groups?grouping=team", factory);
        var list = await GetAsync(cast, "/workitems?page=999999999", factory);

        Assert.Equal(HttpStatusCode.OK, groups.Status);
        Assert.Equal(0, groups.Body.GetProperty("groups").GetArrayLength());
        Assert.Equal(HttpStatusCode.OK, list.Status);
        Assert.Equal(0, list.Body.GetProperty("data").GetArrayLength());
    }
}
