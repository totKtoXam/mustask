using System.Net;
using System.Text.Json;
using Mustask.Api.IntegrationTests.Features.Organizations;
using Mustask.Api.IntegrationTests.Features.WorkItems;
using Mustask.Domain.Identity;
using static Mustask.Api.IntegrationTests.Features.Boards.TeamAcrossProjectsTestData;

namespace Mustask.Api.IntegrationTests.Features.Boards;

/// <summary>
/// feat-062 · doc-016 BR-TD-17 on the multi-project work item views (feat-048's aggregates at the organization and department level
/// and their counts, and a team's backlog and board of feat-040): only items of projects the viewer sees, filtered before paging, so no
/// total, page, cursor or count reveals a hidden project. Each surface is read by a member who cannot see «Гамма» (GAM) and by the
/// same kind of member of a twin organization whose data holds no «Гамма» item; the two answers are equal in shape
/// (<see cref="SightTwin"/>), totals and counts included. The administrator, who sees everything, is unchanged.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class SightInWorkViewsTests(MustaskApiFactory factory)
{
    private sealed record World(Cast Cast, string Viewer, Dictionary<string, Item> Items);

    /// <summary>
    /// Альфа (A1, A2 of MARS, A3 of VENUS, A4 of no team), Бета (B1 of MARS), Гамма (hidden from a member): G1, G2 of MARS and G3 of
    /// no team when <paramref name="withHidden"/>, each assigned to a person only the hidden items have; a member with no grant.
    /// </summary>
    private async Task<World> BuildAsync(bool withHidden)
    {
        var cast = await CreateAsync(factory);
        var items = new Dictionary<string, Item>
        {
            ["A1"] = await StoryAsync(factory, cast.Alpha, "A1", cast.Mars),
            ["A2"] = await StoryAsync(factory, cast.Alpha, "A2", cast.Mars),
            ["A3"] = await StoryAsync(factory, cast.Alpha, "A3", cast.Venus),
            ["A4"] = await StoryAsync(factory, cast.Alpha, "A4", null),
            ["B1"] = await StoryAsync(factory, cast.Beta, "B1", cast.Mars),
        };

        if (withHidden)
        {
            items["G1"] = await StoryAsync(factory, cast.Gamma, "G1", cast.Mars);
            items["G2"] = await StoryAsync(factory, cast.Gamma, "G2", cast.Mars);
            items["G3"] = await StoryAsync(factory, cast.Gamma, "G3", null);
        }

        await WorkItemsTestData.ExecuteAsync(factory, $"UPDATE work_items SET assignee_id = 'visible-person' WHERE id = {items["A1"].Id}");

        foreach (var key in items.Keys.Where(key => key.StartsWith('G')))
        {
            await WorkItemsTestData.ExecuteAsync(factory, $"UPDATE work_items SET assignee_id = 'hidden-person' WHERE id = {items[key].Id}");
        }

        await WorkItemsTestData.ExecuteAsync(factory, $"UPDATE projects SET visible_to_organization = false WHERE id = {cast.Gamma.ProjectId}");
        var viewer = await MembershipTestData.NewMemberAsync(factory, cast.OrganizationId, OrganizationRole.Member);

        return new World(cast, viewer.Token, items);
    }

    private Task<Answer> GetAsync(World world, string path, string? token = null) =>
        OrganizationsApi.SendAsync(factory, HttpMethod.Get, $"/api/organizations/{world.Cast.OrganizationId}{path}", token: token ?? world.Viewer);

    private static string? NextCursor(Answer answer) =>
        answer.Body.TryGetProperty("nextCursor", out var next) && next.ValueKind == JsonValueKind.String ? next.GetString() : null;

    /// <summary>Every page of a group, followed by its own cursors, as shapes.</summary>
    private async Task<string> PagesAsync(World world, string path, string token)
    {
        var shapes = new List<string>();
        string? cursor = null;

        do
        {
            var answer = await GetAsync(world, path + (cursor is null ? string.Empty : $"&cursor={Uri.EscapeDataString(cursor)}"), token);
            Assert.Equal(HttpStatusCode.OK, answer.Status);
            shapes.Add(SightTwin.Shape(answer));
            cursor = NextCursor(answer);
        }
        while (cursor is not null);

        return string.Join('\n', shapes);
    }

    /// <summary>Every read of the aggregate views and of the team's views, as one string per surface.</summary>
    private async Task<Dictionary<string, string>> ReadAllAsync(World world, string token)
    {
        var reads = new Dictionary<string, string>();
        var mars = world.Cast.Mars;

        foreach (var grouping in new[] { "none", "team", "project", "department" })
        {
            reads[$"backlog groups {grouping}"] = SightTwin.Shape(await GetAsync(world, $"/backlogs/stories/groups?grouping={grouping}", token));
            reads[$"board groups {grouping}"] = SightTwin.Shape(await GetAsync(world, $"/boards/stories/groups?grouping={grouping}", token));
            reads[$"workitem groups {grouping}"] = SightTwin.Shape(await GetAsync(world, $"/workitems/groups?grouping={grouping}", token));
        }

        reads["backlog rows of MARS, 1 a page"] = await PagesAsync(world, $"/backlogs/stories/rows?grouping=team&group={mars}&limit=1", token);
        reads["backlog rows of no team, 1 a page"] = await PagesAsync(world, "/backlogs/stories/rows?grouping=team&group=none&limit=1", token);
        reads["backlog rows of ALP project, 2 a page"] = await PagesAsync(world, $"/backlogs/stories/rows?grouping=project&group={world.Cast.Alpha.ProjectId}&limit=2", token);
        reads["workitem rows of MARS, 1 a page"] = await PagesAsync(world, $"/workitems/rows?grouping=team&group={mars}&limit=1", token);

        var groups = await GetAsync(world, "/boards/stories/groups?grouping=team", token);
        var marsGroup = groups.Body.GetProperty("groups").EnumerateArray().Single(group => group.GetProperty("name").GetString() == "MARS");

        foreach (var column in marsGroup.GetProperty("columns").EnumerateArray())
        {
            var key = column.GetProperty("key").GetString();
            reads[$"board cell {key}"] = await PagesAsync(world, $"/boards/stories/cells?grouping=team&group={marsGroup.GetProperty("id").GetGuid()}&column={key}&limit=1", token);
        }

        reads["workitems"] = SightTwin.Shape(await GetAsync(world, "/workitems", token));
        reads["workitems page 2"] = SightTwin.Shape(await GetAsync(world, "/workitems?page=2", token));
        reads["workitems by keyword"] = SightTwin.Shape(await GetAsync(world, "/workitems?search=G1", token));
        reads["workitems of MARS"] = SightTwin.Shape(await GetAsync(world, $"/workitems?teamId={mars}", token));
        reads["assignee options"] = SightTwin.Shape(await GetAsync(world, "/workitems/assignee-options", token));

        foreach (var level in new[] { "stories" })
        {
            reads[$"team backlog {level}"] = SightTwin.Shape(await GetBacklogAsync(factory, world.Cast, mars, level, null, token));
            reads[$"team board {level}"] = SightTwin.Shape(await OrganizationsApi.SendAsync(
                factory, HttpMethod.Get, $"{Team(world.Cast, mars)}/boards/{level}", token: token));
        }

        return reads;
    }

    // BR-TD-17 · every surface answers a viewer who cannot see Гамма as it answers the same viewer of the data without Гамма.
    [Fact]
    public async Task EveryMultiProjectView_ForAViewerWhoCannotSeeAProject_IsIdenticalToTheTwinWithoutIt_TotalsPagesAndCursorsIncluded()
    {
        var real = await BuildAsync(withHidden: true);
        var twin = await BuildAsync(withHidden: false);

        var seen = await ReadAllAsync(real, real.Viewer);
        var expected = await ReadAllAsync(twin, twin.Viewer);

        Assert.Equal(expected.Keys.Order(), seen.Keys.Order());

        foreach (var (surface, shape) in expected)
        {
            Assert.True(shape == seen[surface], $"{surface}:\nthe twin   {shape}\nthe viewer {seen[surface]}");
        }

        // Not vacuous: the viewer sees the visible items, and nothing of Гамма is in any answer.
        Assert.Contains("A1", seen["workitems"]);
        Assert.Contains("\"count\":5", seen["workitems"]);

        foreach (var shape in seen.Values)
        {
            Assert.DoesNotContain("Гамма", shape);
            Assert.DoesNotContain("GAM", shape);
            Assert.DoesNotContain("hidden-person", shape);
            Assert.DoesNotContain(real.Cast.Gamma.ProjectId.ToString(), shape);
        }
    }

    // BR-TD-17 · the administrator sees everything: the narrowing is the viewer's, not the data's.
    [Fact]
    public async Task TheAdministrator_StillSeesTheHiddenProjectsItems_InEveryView()
    {
        var real = await BuildAsync(withHidden: true);
        var admin = OrganizationsTokens.SignedIn();

        var all = await GetAsync(real, "/workitems", admin);
        var viewers = await GetAsync(real, "/workitems");
        var byProject = await GetAsync(real, "/backlogs/stories/groups?grouping=project", admin);
        var marsBacklog = await GetBacklogAsync(factory, real.Cast, real.Cast.Mars, "stories", null, admin);

        Assert.Equal(8, all.Body.GetProperty("count").GetInt32());
        Assert.Equal(5, viewers.Body.GetProperty("count").GetInt32());
        Assert.Contains("GAM", byProject.Body.GetRawText());
        Assert.Contains("G1", marsBacklog.Body.GetRawText());
    }

    // BR-TD-17 · a group page that names the hidden project answers as one that names nothing.
    [Fact]
    public async Task AGroupNamingTheHiddenProject_AnswersAsOneThatNamesNothing()
    {
        var real = await BuildAsync(withHidden: true);
        var gamma = real.Cast.Gamma.ProjectId;
        var unknown = Guid.CreateVersion7();

        foreach (var template in new[]
        {
            "/backlogs/stories/rows?grouping=project&group={0}",
            "/workitems/rows?grouping=project&group={0}",
        })
        {
            var hidden = await GetAsync(real, string.Format(template, gamma));
            var missing = await GetAsync(real, string.Format(template, unknown));

            Assert.Equal(HttpStatusCode.NotFound, missing.Status);
            Assert.Equal(SightTwin.Refusal(missing), SightTwin.Refusal(hidden));
        }
    }

    // feat-041 debt R-5 remainder · a reorder naming an item of a hidden project (moved or anchor) answers as an id that names nothing.
    [Fact]
    public async Task ATeamBacklogReorder_NamingAnItemOfAHiddenProject_AnswersAsAnIdThatNamesNothing_AndTheVisibleOnesStillMove()
    {
        var real = await BuildAsync(withHidden: true);
        var cast = real.Cast;
        var manager = await MembershipTestData.NewMemberAsync(factory, cast.OrganizationId, OrganizationRole.Member);
        await OrganizationsTestData.GrantAsync(factory, ResourceScope.Team, cast.OrganizationId, cast.Mars, manager.Id, GrantRole.Manager);

        Task<Answer> ReorderAs(object body, string token) => OrganizationsApi.SendAsync(
            factory, HttpMethod.Post, $"{Team(cast, cast.Mars)}/backlogs/stories/reorder", body, token);

        var unknown = new Item(Guid.CreateVersion7(), "NONE-1");
        var hidden = real.Items["G1"];
        var a1 = real.Items["A1"];

        var movedHidden = await ReorderAs(BoardsTestData.Top(hidden), manager.Token);
        var movedUnknown = await ReorderAs(BoardsTestData.Top(unknown), manager.Token);
        var afterHidden = await ReorderAs(BoardsTestData.After(a1, hidden), manager.Token);
        var afterUnknown = await ReorderAs(BoardsTestData.After(a1, unknown), manager.Token);

        Assert.Equal(HttpStatusCode.Conflict, movedUnknown.Status);
        Assert.Equal(SightTwin.Shape(movedUnknown), SightTwin.Shape(movedHidden));
        Assert.Equal(HttpStatusCode.Conflict, afterUnknown.Status);
        Assert.Equal(SightTwin.Shape(afterUnknown), SightTwin.Shape(afterHidden));

        // The visible ones still move; the administrator, who sees the hidden item, may move it.
        var moved = await ReorderAs(BoardsTestData.Top(real.Items["B1"]), manager.Token);
        Assert.Equal(HttpStatusCode.NoContent, moved.Status);
        var first = (await GetBacklogAsync(factory, cast, cast.Mars, "stories", null, manager.Token)).Body.GetProperty("items")[0];
        Assert.Equal("B1", first.GetProperty("title").GetString());

        var byAdministrator = await ReorderAs(BoardsTestData.Top(hidden), OrganizationsTokens.SignedIn());
        Assert.Equal(HttpStatusCode.NoContent, byAdministrator.Status);
    }
}
