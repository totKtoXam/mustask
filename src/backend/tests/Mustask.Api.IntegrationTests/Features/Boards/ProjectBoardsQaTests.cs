using System.Net;
using System.Text.Json;
using Mustask.Api.IntegrationTests.Features.Organizations;
using Mustask.Api.IntegrationTests.Features.WorkItems;

namespace Mustask.Api.IntegrationTests.Features.Boards;

/// <summary>
/// QA of feat-039 (stage 05 QA, qa.md Backend): the cases the developer's endpoint tests do not pin — written from the spec and
/// the api-contract before the implementation was read. Each carries the clause it checks.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class ProjectBoardsQaTests(MustaskApiFactory factory)
{
    private async Task<(BoardsWorld World, Item S1, Item S2, Item S3)> ProjectAsync()
    {
        var world = await BoardsTestData.CreateWorldAsync(factory);
        var s1 = await BoardsTestData.CreateAsync(factory, world, "US", "Первая", world.RootAreaId);
        var s2 = await BoardsTestData.CreateAsync(factory, world, "US", "Вторая", world.RootAreaId);
        var s3 = await BoardsTestData.CreateAsync(factory, world, "US", "Третья", world.PlatformAreaId);

        return (world, s1, s2, s3);
    }

    private static string[] Ids(params Item[] items) => items.Select(item => item.Identifier).ToArray();

    // api-contract § 3.1 "written with the backlog's deeper JSON limit": a chain at the bound (epic, feature, story and sixty tasks,
    // each beneath the one before) nests the answer past the platform's default writer depth, and the project's route answers it
    // whole, as the team's does (feat-027 D-9).
    [Fact]
    public async Task Get_AChainOfTheBoundsLength_IsAnsweredWhole_OnTheProjectsRoute()
    {
        var (world, s1, _, _) = await ProjectAsync();
        var epic = await BoardsTestData.CreateAsync(factory, world, "EPIC", "Эпик");
        var feature = await BoardsTestData.CreateAsync(factory, world, "FEAT", "Фича", world.PlatformAreaId);
        await LinksTestData.LinkAsync(factory, world.Items, feature, epic);
        await LinksTestData.LinkAsync(factory, world.Items, s1, feature);
        var chain = await BulkItemsTestData.InsertChainAsync(factory, world.Items, length: 60, firstNumber: 100, beneath: s1.Id);

        using var epics = await ReadDeepAsync(world, "epics");
        using var stories = await ReadDeepAsync(world, "stories");

        Assert.Equal((63, chain[^1].Identifier), Deepest(epics.RootElement.GetProperty("items")));
        Assert.Equal((61, chain[^1].Identifier), Deepest(stories.RootElement.GetProperty("items")));
    }

    // api-contract § 0.2: an id in a path is accepted in either letter case, and an all-zero one names nothing.
    [Fact]
    public async Task Get_ProjectIdInUpperCase_IsFound_AndAllZerosNamesNothing()
    {
        var (world, s1, _, _) = await ProjectAsync();
        var upper = $"/api/organizations/{world.OrganizationId}/projects/{world.ProjectId.ToString().ToUpperInvariant()}";

        var found = await OrganizationsApi.SendAsync(factory, HttpMethod.Get, $"{upper}/backlogs/stories", token: OrganizationsTokens.SignedIn());
        var board = await OrganizationsApi.SendAsync(factory, HttpMethod.Get, $"{upper}/boards/stories", token: OrganizationsTokens.SignedIn());
        var zeros = await OrganizationsApi.SendAsync(
            factory,
            HttpMethod.Get,
            $"/api/organizations/{world.OrganizationId}/projects/{Guid.Empty}/backlogs/stories",
            token: OrganizationsTokens.SignedIn());

        Assert.Equal(HttpStatusCode.OK, found.Status);
        Assert.Equal(s1.Identifier, found.Body.GetProperty("items")[0].GetProperty("identifier").GetString());
        Assert.Equal(world.ProjectId, found.Body.GetProperty("project").GetProperty("id").GetGuid());
        Assert.Equal(HttpStatusCode.OK, board.Status);
        BoardsTestData.AssertRefused(zeros, HttpStatusCode.NotFound, "organizations.project.not_found");
    }

    // api-contract § 2.2: on a board card `order` is the item's position in the whole project order of the level, so the columns
    // hold gaps; a story in the Removed category is on no card.
    [Fact]
    public async Task Board_CardOrder_IsThePositionInTheWholeProjectOrder_AndRemovedItemsAreOnNoCard()
    {
        var (world, s1, s2, s3) = await ProjectAsync();
        var removed = await BoardsTestData.CreateAsync(factory, world, "US", "Удалённая", world.PlatformAreaId);
        Assert.Equal(HttpStatusCode.OK, (await BoardsTestData.SetStateAsync(factory, world, s2, "US", "Активна")).Status);
        await LinksTestData.DeleteAsync(factory, world.Items, removed);
        await ProjectBoardsTestData.MoveAsync(factory, world, BoardsTestData.Top(s3));

        var board = await ProjectBoardsTestData.GetBoardAsync(factory, world);

        Assert.Equal(HttpStatusCode.OK, board.Status);
        var orders = board.Body.GetProperty("columns").EnumerateArray()
            .SelectMany(column => column.GetProperty("items").EnumerateArray())
            .ToDictionary(card => card.GetProperty("identifier").GetString()!, card => card.GetProperty("order").GetInt32());
        Assert.Equal(new Dictionary<string, int> { [s3.Identifier] = 1, [s1.Identifier] = 2, [s2.Identifier] = 3 }, orders);
        Assert.DoesNotContain(removed.Identifier, ProjectBoardsTestData.AllCards(board.Body));
    }

    // mod-006 BR-M4-26 on the project, api-contract § 3.2 step 3: the «Задачи» level has no project order, so a task is not an item
    // of any project backlog — moving one, or going after one, is stale and nothing is placed.
    [Fact]
    public async Task Reorder_ATask_IsNotAnItemOfThisBacklog_Returns409()
    {
        var (world, s1, _, _) = await ProjectAsync();
        var task = await BoardsTestData.CreateAsync(factory, world, "TASK", "Задача", world.PlatformAreaId);

        var moved = await ProjectBoardsTestData.ReorderAsync(factory, world, BoardsTestData.Top(task));
        var anchored = await ProjectBoardsTestData.ReorderAsync(factory, world, BoardsTestData.After(s1, task));

        BoardsTestData.AssertRefused(moved, HttpStatusCode.Conflict, "boards.backlog.reorder_stale");
        BoardsTestData.AssertRefused(anchored, HttpStatusCode.Conflict, "boards.backlog.reorder_stale");
        Assert.Empty(await ProjectBoardsTestData.PlacesAsync(factory, world));
    }

    // BR-MX-1: an item of another ORGANIZATION named in the body answers like an id that names nothing — stale — and the other
    // organization's item gets no place on anything.
    [Fact]
    public async Task Reorder_AnItemOfAnotherOrganization_IsStale_AndLearnsNothing()
    {
        var (world, s1, _, _) = await ProjectAsync();
        var foreignWorld = await BoardsTestData.CreateWorldAsync(factory);
        var foreign = await BoardsTestData.CreateAsync(factory, foreignWorld, "US", "Чужая", foreignWorld.RootAreaId);

        var moved = await ProjectBoardsTestData.ReorderAsync(factory, world, BoardsTestData.Top(foreign));
        var anchored = await ProjectBoardsTestData.ReorderAsync(factory, world, BoardsTestData.After(s1, foreign));

        BoardsTestData.AssertRefused(moved, HttpStatusCode.Conflict, "boards.backlog.reorder_stale");
        BoardsTestData.AssertRefused(anchored, HttpStatusCode.Conflict, "boards.backlog.reorder_stale");
        Assert.Empty(await ProjectBoardsTestData.PlacesAsync(factory, world));
        Assert.Empty(await ProjectBoardsTestData.PlacesAsync(factory, foreignWorld));
    }

    // mod-006 BR-M4-24: the same reorder twice is the same order and the same places — dense, no duplicate, nothing drifts.
    [Fact]
    public async Task Reorder_TheSameMoveTwice_LeavesTheSameDensePlaces()
    {
        var (world, s1, s2, s3) = await ProjectAsync();

        await ProjectBoardsTestData.MoveAsync(factory, world, BoardsTestData.Top(s3));
        var once = await ProjectBoardsTestData.PlacesAsync(factory, world);
        await ProjectBoardsTestData.MoveAsync(factory, world, BoardsTestData.Top(s3));
        var twice = await ProjectBoardsTestData.PlacesAsync(factory, world);

        Assert.Equal(once, twice);
        Assert.Equal([1, 2, 3], twice.Select(place => place.Position).Order());
        Assert.Equal(Ids(s3, s1, s2), await ProjectBoardsTestData.RowsAsync(factory, world));
    }

    // mod-006 § 3.2: `afterItemId` means something only with "after"; with "top" or "bottom" it is ignored, not an error — even when
    // it names nothing.
    [Theory]
    [InlineData("top")]
    [InlineData("bottom")]
    public async Task Reorder_ATopOrBottomMove_IgnoresAStrayAnchor(string position)
    {
        var (world, s1, s2, s3) = await ProjectAsync();

        var answer = await ProjectBoardsTestData.ReorderAsync(
            factory, world, new { itemId = s2.Id.ToString(), position, afterItemId = "not-an-id" });

        Assert.Equal(HttpStatusCode.NoContent, answer.Status);
        Assert.Equal(position == "top" ? Ids(s2, s1, s3) : Ids(s1, s3, s2), await ProjectBoardsTestData.RowsAsync(factory, world));
    }

    // US-2 · "Two users reorder at once" at a larger scale than the gate tests: eight reorders of eight items at the same moment all
    // answer 204 (none starves, none deadlocks, none is told it is stale), every item is on the backlog once, and the places are
    // exactly 1..n.
    [Fact]
    public async Task Reorder_EightAtOnce_AllAnswer204_AndThePlacesStayDense()
    {
        var world = await BoardsTestData.CreateWorldAsync(factory);
        var items = new List<Item>();
        for (var index = 0; index < 8; index++)
        {
            items.Add(await BoardsTestData.CreateAsync(factory, world, "US", $"История {index}", index % 2 == 0 ? world.RootAreaId : world.PlatformAreaId));
        }

        var answers = await Task.WhenAll(items.Select(item => ProjectBoardsTestData.ReorderAsync(factory, world, BoardsTestData.Top(item))));

        Assert.All(answers, answer => Assert.True(answer.Status == HttpStatusCode.NoContent, $"A concurrent reorder answered {answer.Status} {answer.ErrorCode}."));
        var rows = await ProjectBoardsTestData.RowsAsync(factory, world);
        Assert.Equal(items.Select(item => item.Identifier).Order(), rows.Order());
        var places = await ProjectBoardsTestData.PlacesAsync(factory, world);
        Assert.Equal(Enumerable.Range(1, 8), places.Select(place => place.Position).Order());
    }

    // US-3 · "A move offers only the states of the card's own type" (BR-MX-19, mod-006 BR-M4-18): a direct write of a story's state
    // on a bug is 409 `workitems.workitem.state_not_allowed` and changes nothing — no state, no place of the project's order.
    [Fact]
    public async Task CardMove_AStateOfAnotherType_Returns409_AndChangesNothing()
    {
        var (world, s1, _, _) = await ProjectAsync();
        var bug = await BoardsTestData.CreateAsync(factory, world, "BUG", "Баг", world.PlatformAreaId);
        await ProjectBoardsTestData.MoveAsync(factory, world, BoardsTestData.Top(s1));
        var placesBefore = await ProjectBoardsTestData.PlacesAsync(factory, world);
        var storedBefore = await WorkItemsTestData.StoredAsync(factory, world.ProjectId);

        var answer = await OrganizationsApi.SendAsync(
            factory,
            HttpMethod.Patch,
            WorkItemsTestData.Item(world.Items, bug.Identifier),
            new { stateId = world.Items.Types["US"].StateId("Активна").ToString() },
            OrganizationsTokens.SignedIn());

        BoardsTestData.AssertRefused(answer, HttpStatusCode.Conflict, "workitems.workitem.state_not_allowed");
        Assert.Equal(storedBefore, await WorkItemsTestData.StoredAsync(factory, world.ProjectId));
        Assert.Equal(placesBefore, await ProjectBoardsTestData.PlacesAsync(factory, world));
        var board = await ProjectBoardsTestData.GetBoardAsync(factory, world);
        Assert.Contains(bug.Identifier, BoardsTestData.Cards(BoardsTestData.Column(board.Body, "Новая")));
    }

    // US-1 · row "+" (BR-PB-3): an item created beneath a story with no team in the request stands last beneath it, is on the
    // project backlog once, and the creation writes no place on either order.
    [Fact]
    public async Task CreateChild_BeneathARow_TakesNoPlace_AndStandsBeneathItsParent()
    {
        var (world, s1, s2, s3) = await ProjectAsync();
        await ProjectBoardsTestData.MoveAsync(factory, world, BoardsTestData.Top(s3));
        var projectPlaces = await ProjectBoardsTestData.PlacesAsync(factory, world);
        var teamPlaces = await ProjectBoardsTestData.TeamPlacesAsync(factory, world);

        var created = await BacklogTreeTestData.CreateChildAsync(factory, world, "TASK", "Дочерняя", s1.Id.ToString());

        Assert.Equal(HttpStatusCode.Created, created.Status);
        var identifier = created.Body.GetProperty("identifier").GetString()!;
        Assert.Equal($"{s3.Identifier} {s1.Identifier}[{identifier}] {s2.Identifier}", await ProjectBoardsTestData.ShapeAsync(factory, world));
        Assert.Equal(projectPlaces, await ProjectBoardsTestData.PlacesAsync(factory, world));
        Assert.Equal(teamPlaces, await ProjectBoardsTestData.TeamPlacesAsync(factory, world));
    }

    /// <summary>The project's backlog read with a JSON reader deep enough for a chain at the bound; fails the test unless it answers 200.</summary>
    private async Task<JsonDocument> ReadDeepAsync(BoardsWorld world, string level)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, ProjectBoardsTestData.Backlog(world, level));
        request.WithBearer(OrganizationsTokens.SignedIn());
        using var response = await factory.CreateClient().SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        return JsonDocument.Parse(await response.Content.ReadAsStringAsync(), new JsonDocumentOptions { MaxDepth = 256 });
    }

    /// <summary>The depth of the deepest row (a top-level row is 1) and that row's identifier, walked by a loop.</summary>
    private static (int Depth, string Identifier) Deepest(JsonElement rows)
    {
        var pending = new Stack<(JsonElement Row, int Depth)>(rows.EnumerateArray().Select(row => (row, 1)));
        var deepest = (Depth: 0, Identifier: string.Empty);

        while (pending.TryPop(out var next))
        {
            if (next.Depth > deepest.Depth)
            {
                deepest = (next.Depth, next.Row.GetProperty("identifier").GetString()!);
            }

            foreach (var child in next.Row.GetProperty("children").EnumerateArray())
            {
                pending.Push((child, next.Depth + 1));
            }
        }

        return deepest;
    }
}
