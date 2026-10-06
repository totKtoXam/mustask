using System.Net;
using System.Text.Json;
using Mustask.Api.IntegrationTests.Features.Organizations;
using Mustask.Api.IntegrationTests.Features.WorkItems;

namespace Mustask.Api.IntegrationTests.Features.Boards;

/// <summary>
/// The boards tests' data and requests (mod-006). Every test builds its own
/// world of the stories' data (US-1): organization, project «Мастаск» keyed MSK
/// with its root «Мастаск» and first team «Мастаск»; team «Платформа» owning its
/// default area «Платформа» under the root; «Платформа / API» below it with no
/// owner of its own; «Отчёты» under the root with no owner. Items are created
/// through mod-003's endpoints and moved between areas with its PATCH, so what a
/// test reads is what the API wrote; areas and teams change through mod-002's
/// endpoints, as "elsewhere" (US-7).
/// </summary>
internal static class BoardsTestData
{
    public static async Task<BoardsWorld> CreateWorldAsync(MustaskApiFactory factory)
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var project = await OrganizationsTestData.CreateProjectAsync(factory, organization.Id, "Мастаск", "MSK");
        var types = await WorkItemsTestData.TypesAsync(factory, organization.Id);
        var items = new WorkItemsWorld(organization.Id, project.Project.Id, project.Root.Id, "MSK", types);

        var platform = await OrganizationsTestData.AddTeamAsync(factory, project.Project.Id, "Платформа");
        var api = await OrganizationsTestData.AddAreaAsync(factory, project.Project.Id, platform.Area.Id, "API");
        var reports = await OrganizationsTestData.AddAreaAsync(factory, project.Project.Id, project.Root.Id, "Отчёты");

        return new BoardsWorld(items, project.FirstTeam.Id, platform.Team.Id, platform.Area.Id, api.Id, reports.Id);
    }

    /// <summary>A further team with its own default area under the root (US-1 «Аналитика»).</summary>
    public static async Task<(Guid TeamId, Guid AreaId)> AddTeamAsync(MustaskApiFactory factory, BoardsWorld world, string name)
    {
        var team = await OrganizationsTestData.AddTeamAsync(factory, world.ProjectId, name);

        return (team.Team.Id, team.Area.Id);
    }

    public static string Team(BoardsWorld world, Guid teamId) =>
        $"/api/organizations/{world.OrganizationId}/projects/{world.ProjectId}/teams/{teamId}";

    public static string Backlog(BoardsWorld world, Guid teamId, string level) => $"{Team(world, teamId)}/backlogs/{level}";

    public static string Board(BoardsWorld world, Guid teamId, string level) => $"{Team(world, teamId)}/boards/{level}";

    /// <summary>
    /// An item of <paramref name="typeKey"/> created through mod-003's create (in the root), then moved into
    /// <paramref name="areaId"/> with its PATCH when that is another area.
    /// </summary>
    public static async Task<Item> CreateAsync(MustaskApiFactory factory, BoardsWorld world, string typeKey, string title, Guid? areaId = null)
    {
        var answer = await WorkItemsTestData.CreateAsync(factory, world.Items, typeKey, title);
        Assert.Equal(HttpStatusCode.Created, answer.Status);
        var item = new Item(answer.Body.GetProperty("id").GetGuid(), answer.Body.GetProperty("identifier").GetString()!);

        if (areaId is { } area && area != world.RootAreaId)
        {
            await MoveToAreaAsync(factory, world, item, area);
        }

        return item;
    }

    /// <summary>The create request as the dialog on a team's surface sends it: with the team's id (api-contract § 7).</summary>
    public static Task<Answer> CreateFromTeamAsync(
        MustaskApiFactory factory, BoardsWorld world, string typeKey, string title, string teamId, Guid? requestId = null, CommitGate? gate = null) =>
        OrganizationsApi.SendAsync(
            factory,
            HttpMethod.Post,
            WorkItemsTestData.List(world.Items),
            new { requestId = (requestId ?? Guid.NewGuid()).ToString(), typeId = world.Items.Types[typeKey].Id.ToString(), title, teamId },
            OrganizationsTokens.SignedIn(),
            gate);

    /// <summary>Sets the item's Область on its form (mod-003's PATCH); fails the test unless it answers 200.</summary>
    public static async Task MoveToAreaAsync(MustaskApiFactory factory, BoardsWorld world, Item item, Guid areaId)
    {
        var answer = await WorkItemsTestData.SaveAsync(factory, world.Items, item.Identifier, new { areaId = areaId.ToString() });
        Assert.Equal(HttpStatusCode.OK, answer.Status);
    }

    /// <summary>Sets the item's Статус to the state of that name of its own type (mod-003's PATCH, a card move or the form).</summary>
    public static async Task<Answer> SetStateAsync(MustaskApiFactory factory, BoardsWorld world, Item item, string typeKey, string stateName) =>
        await WorkItemsTestData.SaveAsync(factory, world.Items, item.Identifier, new { stateId = world.Items.Types[typeKey].StateId(stateName).ToString() });

    public static Task<Answer> GetBacklogAsync(MustaskApiFactory factory, BoardsWorld world, Guid teamId, string level = "stories") =>
        OrganizationsApi.SendAsync(factory, HttpMethod.Get, Backlog(world, teamId, level), token: OrganizationsTokens.SignedIn());

    public static Task<Answer> GetBoardAsync(MustaskApiFactory factory, BoardsWorld world, Guid teamId, string level = "stories") =>
        OrganizationsApi.SendAsync(factory, HttpMethod.Get, Board(world, teamId, level), token: OrganizationsTokens.SignedIn());

    /// <summary>The backlog's rows by identifier, in order; fails the test unless the read answers 200 with «Порядок» 1..n.</summary>
    public static async Task<string[]> RowsAsync(MustaskApiFactory factory, BoardsWorld world, Guid teamId, string level = "stories")
    {
        var answer = await GetBacklogAsync(factory, world, teamId, level);
        Assert.Equal(HttpStatusCode.OK, answer.Status);

        var items = answer.Body.GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(Enumerable.Range(1, items.Count), items.Select(item => item.GetProperty("order").GetInt32()));

        return items.Select(item => item.GetProperty("identifier").GetString()!).ToArray();
    }

    /// <summary>A reorder as the panel sends it.</summary>
    public static Task<Answer> ReorderAsync(
        MustaskApiFactory factory, BoardsWorld world, Guid teamId, object body, string level = "stories", CommitGate? gate = null) =>
        OrganizationsApi.SendAsync(
            factory, HttpMethod.Post, $"{Backlog(world, teamId, level)}/reorder", body, OrganizationsTokens.SignedIn(), gate);

    public static object Top(Item item) => new { itemId = item.Id.ToString(), position = "top" };

    public static object Bottom(Item item) => new { itemId = item.Id.ToString(), position = "bottom" };

    public static object After(Item item, Item anchor) => new { itemId = item.Id.ToString(), position = "after", afterItemId = anchor.Id.ToString() };

    /// <summary>Reorders and fails the test unless the reorder answers 204.</summary>
    public static async Task MoveAsync(MustaskApiFactory factory, BoardsWorld world, Guid teamId, object body, string level = "stories")
    {
        var answer = await ReorderAsync(factory, world, teamId, body, level);
        Assert.True(answer.Status == HttpStatusCode.NoContent, $"The reorder answered {answer.Status} {answer.ErrorCode}.");
    }

    /// <summary>Puts the backlog in exactly <paramref name="order"/> by moving each item to the bottom in turn.</summary>
    public static async Task ReorderToAsync(MustaskApiFactory factory, BoardsWorld world, Guid teamId, params Item[] order)
    {
        foreach (var item in order)
        {
            await MoveAsync(factory, world, teamId, Bottom(item));
        }
    }

    /// <summary>The board's columns in order, each as «header: identifier identifier …» («header:» when empty).</summary>
    public static string[] Columns(JsonElement board) =>
        board.GetProperty("columns").EnumerateArray()
            .Select(column => string.Join(' ', [column.GetProperty("header").GetString() + ":", .. Cards(column)]))
            .ToArray();

    /// <summary>The identifiers of a column's cards, in order.</summary>
    public static string[] Cards(JsonElement column) =>
        column.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("identifier").GetString()!).ToArray();

    /// <summary>The column of that header.</summary>
    public static JsonElement Column(JsonElement board, string header) =>
        board.GetProperty("columns").EnumerateArray().Single(column => column.GetProperty("header").GetString() == header);

    /// <summary>The state a drop of a card of <paramref name="typeId"/> in the column writes, or <c>null</c>.</summary>
    public static string? Target(JsonElement column, Guid typeId)
    {
        var target = column.GetProperty("targets").EnumerateArray().Single(candidate => candidate.GetProperty("typeId").GetGuid() == typeId);
        var state = target.GetProperty("stateId");

        return state.ValueKind == JsonValueKind.Null ? null : state.GetString();
    }

    public static void AssertRefused(Answer answer, HttpStatusCode status, string code)
    {
        Assert.True(answer.Status == status, $"Expected {status} {code}, got {answer.Status} {answer.ErrorCode}.");
        Assert.Equal(code, answer.ErrorCode);
    }
}

/// <summary>US-1's world: the work items' world and the teams and areas of the stories.</summary>
internal sealed record BoardsWorld(
    WorkItemsWorld Items,
    Guid FirstTeamId,
    Guid PlatformId,
    Guid PlatformAreaId,
    Guid ApiAreaId,
    Guid ReportsAreaId)
{
    public Guid OrganizationId => Items.OrganizationId;

    public Guid ProjectId => Items.ProjectId;

    public Guid RootAreaId => Items.RootAreaId;

    public Guid TypeId(string key) => Items.Types[key].Id;
}

/// <summary>A created work item: its id and identifier.</summary>
internal sealed record Item(Guid Id, string Identifier);
