using System.Net;
using System.Text.Json;
using Mustask.Api.IntegrationTests.Features.Organizations;
using Mustask.Api.IntegrationTests.Features.WorkItems;

namespace Mustask.Api.IntegrationTests.Features.Boards;

/// <summary>
/// <c>GET /api/organizations/{organizationId}/projects/{projectId}/teams/{teamId}/backlogs/{level}</c>
/// with its tree at full depth (feat-027 api-contract § 1, § 2): every row's
/// «Родитель» and the whole tree of the team's live children beneath it, tasks
/// included, read from the links at each read (feat-027 US-2); the re-parenting a
/// drop or the dialog writes through feat-001's parent write (feat-027 US-5,
/// US-6; feat-001 US-5) and a deleted parent's children unparented (feat-001
/// US-7). feat-001 US-4's one-level-below tree is superseded here (feat-027 D-2).
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class BacklogTreeEndpointTests(MustaskApiFactory factory)
{
    private const string Epics = "epics";
    private const string Features = "features";
    private const string Stories = "stories";

    /// <summary>«Эпики» of «Платформа» with US-1's data, every row expanded.</summary>
    private const string EpicsTree = "MSK-EPIC-1[MSK-FEAT-8[MSK-FEAT-23] MSK-FEAT-6[MSK-US-2[MSK-BUG-5 MSK-TASK-3] MSK-BUG-4] MSK-US-22] MSK-EPIC-20";

    private Task<string> ShapeAsync(BoardsWorld world, Guid teamId, string level) => BacklogTreeTestData.ShapeAsync(factory, world, teamId, level);

    private Task<JsonElement[]> RowsAsync(BoardsWorld world, Guid teamId, string level) => BacklogTreeTestData.TopRowsAsync(factory, world, teamId, level);

    private Task<Answer> MapAsync(BoardsWorld world, Item child, Item? parent, Item? seen) =>
        LinksTestData.SetParentAsync(factory, world.Items, child.Identifier, parent?.Id, seen?.Id);

    private static string[] Identifiers(IEnumerable<JsonElement> rows) => rows.Select(row => row.GetProperty("identifier").GetString()!).ToArray();

    /// <summary>
    /// The team's whole backlog of the level, nested items included, in its order: the board's cards by their `order`,
    /// which is the item's position in the whole backlog (api-contract § 2.3) and runs 1..n without a gap.
    /// </summary>
    private async Task<string[]> WholeBacklogOrderAsync(BoardsWorld world, string level)
    {
        var board = await BoardsTestData.GetBoardAsync(factory, world, world.PlatformId, level);
        Assert.Equal(HttpStatusCode.OK, board.Status);

        var cards = board.Body.GetProperty("columns").EnumerateArray()
            .SelectMany(column => column.GetProperty("items").EnumerateArray())
            .OrderBy(card => card.GetProperty("order").GetInt32())
            .ToList();
        Assert.Equal(Enumerable.Range(1, cards.Count), cards.Select(card => card.GetProperty("order").GetInt32()));

        return Identifiers(cards);
    }

    // --- feat-027 US-2: the whole tree under a row ------------------------------------------------------------------

    // US-2 · "The «Эпики» tree reaches the tasks" and "Children stand by level, then in their backlog's order": beneath
    // MSK-EPIC-1 the features in the «Фичи» order (MSK-FEAT-23 beneath MSK-FEAT-8, its own level), then MSK-US-22 (a
    // story directly under the epic); beneath MSK-US-2 its bug MSK-BUG-5 before its task MSK-TASK-3.
    [Fact]
    public async Task Get_Epics_TheTreeReachesTheTasks_ByLevelThenEachBacklogsOrder()
    {
        var (world, _) = await BacklogTreeTestData.CreateWorldAsync(factory);

        Assert.Equal(EpicsTree, await ShapeAsync(world, world.PlatformId, Epics));
    }

    // US-1 · "Only top-level rows are numbered" (BR-T-2): MSK-EPIC-1 «1», MSK-EPIC-20 «2»; a nested row has no `order`
    // at all, and its «Родитель» is the row above it (feat-001 BR-H-13).
    [Fact]
    public async Task Get_NestedRows_HaveNoOrder_TheirParentIsTheRowAbove()
    {
        var (world, items) = await BacklogTreeTestData.CreateWorldAsync(factory);

        var rows = await RowsAsync(world, world.PlatformId, Epics);

        Assert.Equal([1, 2], rows.Select(row => row.GetProperty("order").GetInt32()));
        Assert.Equal(JsonValueKind.Null, rows[0].GetProperty("parent").ValueKind);
        var nested = rows.SelectMany(row => BacklogTreeTestData.AllRows(row.GetProperty("children").EnumerateArray())).ToList();
        Assert.Equal(8, nested.Count);
        Assert.All(nested, row => Assert.False(row.TryGetProperty("order", out _)));
        var feature = BacklogTreeTestData.Row(rows, "MSK-FEAT-6");
        var task = BacklogTreeTestData.Row(rows, "MSK-TASK-3");
        Assert.Equal(("MSK-EPIC-1", "MSK-US-2"), (BacklogTreeTestData.ParentOf(feature), BacklogTreeTestData.ParentOf(task)));
        Assert.Equal(items["MSK-EPIC-1"].Id, feature.GetProperty("parent").GetProperty("id").GetGuid());
        Assert.Equal(("Новая", "Вход по ссылке"), (feature.GetProperty("state").GetProperty("name").GetString(), feature.GetProperty("title").GetString()));
        Assert.Equal("Пользовательская история", task.GetProperty("parent").GetProperty("type").GetProperty("name").GetString());
    }

    // US-2 · "Another team's children stand beneath no row": MSK-FEAT-21 and MSK-TASK-7, in «Мастаск»'s area, show
    // nowhere on «Платформа»'s pages; MSK-FEAT-21 is a top-level row of «Мастаск»'s «Фичи», with MSK-TASK-7 beneath it on
    // no row (its parent MSK-US-2 is «Платформа»'s).
    [Fact]
    public async Task Get_AnotherTeamsChildren_StandBeneathNoRow()
    {
        var (world, _) = await BacklogTreeTestData.CreateWorldAsync(factory);

        foreach (var level in new[] { Epics, Features, Stories })
        {
            var shape = await ShapeAsync(world, world.PlatformId, level);
            Assert.DoesNotContain("MSK-FEAT-21", shape);
            Assert.DoesNotContain("MSK-TASK-7", shape);
        }

        var firstTeamFeatures = await RowsAsync(world, world.FirstTeamId, Features);
        Assert.Equal(["MSK-FEAT-21"], Identifiers(firstTeamFeatures));
        Assert.Equal("MSK-EPIC-1", BacklogTreeTestData.ParentOf(firstTeamFeatures.Single()));
        Assert.Empty(firstTeamFeatures.Single().GetProperty("children").EnumerateArray());
        Assert.Equal(string.Empty, await ShapeAsync(world, world.FirstTeamId, Stories));
    }

    // US-2 · "A same-level child stands beneath its parent only" (OQ-5, BR-T-5): «Фичи» is MSK-FEAT-8 «1», MSK-FEAT-6 «2»;
    // MSK-FEAT-23 only beneath MSK-FEAT-8, with no «Порядок».
    [Fact]
    public async Task Get_Features_ASameLevelChild_StandsBeneathItsParentOnly()
    {
        var (world, _) = await BacklogTreeTestData.CreateWorldAsync(factory);

        var rows = await RowsAsync(world, world.PlatformId, Features);

        Assert.Equal(["MSK-FEAT-8", "MSK-FEAT-6"], Identifiers(rows));
        Assert.Equal([1, 2], rows.Select(row => row.GetProperty("order").GetInt32()));
        Assert.Equal("MSK-FEAT-8[MSK-FEAT-23] MSK-FEAT-6[MSK-US-2[MSK-BUG-5 MSK-TASK-3] MSK-BUG-4]", BacklogTreeTestData.Shape(rows));
        Assert.False(BacklogTreeTestData.Row(rows, "MSK-FEAT-23").TryGetProperty("order", out _));
        Assert.Equal("MSK-FEAT-8", BacklogTreeTestData.ParentOf(BacklogTreeTestData.Row(rows, "MSK-FEAT-23")));
    }

    // US-2 · "«Истории» rows open to their bugs and tasks": MSK-US-2, MSK-BUG-4, MSK-US-22 numbered 1..3; MSK-US-2 holds
    // MSK-BUG-5 and MSK-TASK-3; MSK-BUG-4 and MSK-US-22 have none.
    [Fact]
    public async Task Get_Stories_OpenToTheirBugsAndTasks()
    {
        var (world, _) = await BacklogTreeTestData.CreateWorldAsync(factory);

        var rows = await RowsAsync(world, world.PlatformId, Stories);

        Assert.Equal("MSK-US-2[MSK-BUG-5 MSK-TASK-3] MSK-BUG-4 MSK-US-22", BacklogTreeTestData.Shape(rows));
        Assert.Equal([1, 2, 3], rows.Select(row => row.GetProperty("order").GetInt32()));
    }

    // US-2 · "A child in «Удалена» frees its parent's place": MSK-BUG-5 in the Removed category shows nowhere, and MSK-US-2
    // holds MSK-TASK-3 only.
    [Fact]
    public async Task Get_AChildInRemoved_ShowsNowhere()
    {
        var (world, items) = await BacklogTreeTestData.CreateWorldAsync(factory);

        await LinksTestData.DeleteAsync(factory, world.Items, items["MSK-BUG-5"]);

        Assert.Equal("MSK-US-2[MSK-TASK-3] MSK-BUG-4 MSK-US-22", await ShapeAsync(world, world.PlatformId, Stories));
        Assert.DoesNotContain("MSK-BUG-5", await ShapeAsync(world, world.PlatformId, Epics));
    }

    // US-2 · "A link made elsewhere shows at the next read" (BR-T-15): MSK-US-22's parent set to MSK-FEAT-8 on its form.
    [Fact]
    public async Task Get_ALinkMadeElsewhere_ShowsAtTheNextRead()
    {
        var (world, items) = await BacklogTreeTestData.CreateWorldAsync(factory);

        var saved = await WorkItemsTestData.SaveAsync(
            factory, world.Items, "MSK-US-22", new { parent = new { parentId = items["MSK-FEAT-8"].Id.ToString(), seenParentId = items["MSK-EPIC-1"].Id.ToString() } });

        Assert.Equal(HttpStatusCode.OK, saved.Status);
        Assert.Equal(
            "MSK-EPIC-1[MSK-FEAT-8[MSK-FEAT-23 MSK-US-22] MSK-FEAT-6[MSK-US-2[MSK-BUG-5 MSK-TASK-3] MSK-BUG-4]] MSK-EPIC-20",
            await ShapeAsync(world, world.PlatformId, Epics));
    }

    // US-2 · "Expanding asks the server nothing": the tree comes whole in one read, and reading it changes no order, link
    // or «Изменён»; a reorder of the epics moves MSK-EPIC-1 with its whole subtree.
    [Fact]
    public async Task Get_AfterAReorder_TheSubtreeMovesWithItsRow_AndReadsChangeNothing()
    {
        var (world, items) = await BacklogTreeTestData.CreateWorldAsync(factory);
        var before = await WorkItemsTestData.StoredAsync(factory, world.ProjectId);

        await BoardsTestData.MoveAsync(factory, world, world.PlatformId, BoardsTestData.Top(items["MSK-EPIC-20"]), Epics);

        Assert.Equal(
            "MSK-EPIC-20 MSK-EPIC-1[MSK-FEAT-8[MSK-FEAT-23] MSK-FEAT-6[MSK-US-2[MSK-BUG-5 MSK-TASK-3] MSK-BUG-4] MSK-US-22]",
            await ShapeAsync(world, world.PlatformId, Epics));
        Assert.Equal(before, await WorkItemsTestData.StoredAsync(factory, world.ProjectId));
    }

    // BR-T-6: children of a level follow that level's backlog order — a reorder of «Фичи» reorders them beneath MSK-EPIC-1,
    // and a reorder of «Истории» those beneath MSK-FEAT-6; tasks stand by number.
    [Fact]
    public async Task Get_AReorderOfALevelBelow_ReordersItsChildren()
    {
        var (world, items) = await BacklogTreeTestData.CreateWorldAsync(factory);

        await BoardsTestData.MoveAsync(factory, world, world.PlatformId, BoardsTestData.Top(items["MSK-FEAT-6"]), Features);
        await BoardsTestData.MoveAsync(factory, world, world.PlatformId, BoardsTestData.Top(items["MSK-BUG-4"]), Stories);

        Assert.Equal(
            "MSK-EPIC-1[MSK-FEAT-6[MSK-BUG-4 MSK-US-2[MSK-BUG-5 MSK-TASK-3]] MSK-FEAT-8[MSK-FEAT-23] MSK-US-22] MSK-EPIC-20",
            await ShapeAsync(world, world.PlatformId, Epics));
    }

    // BR-T-4 with D-9's bound: the tree is as deep as a chain of parents may be, 64 items — here MSK-EPIC-1 → MSK-FEAT-6 →
    // MSK-US-2 → MSK-TASK-3 and sixty more tasks, each beneath the one before — and it is answered whole on every page the
    // chain shows on, although 64 items nest the answer far deeper than the platform's JSON writer allows by default (64
    // levels, thirty links). Read with a deeper reader than the tests' helper, and walked by a loop.
    [Fact]
    public async Task Get_AChainOfTheBoundsLength_IsAnsweredWhole()
    {
        var (world, items) = await BacklogTreeTestData.CreateWorldAsync(factory);
        await BulkItemsTestData.InsertChainAsync(factory, world.Items, world.PlatformAreaId, length: 60, firstNumber: 24, beneath: items["MSK-TASK-3"].Id);

        using var epics = await ReadDeepAsync(world, Epics);
        using var stories = await ReadDeepAsync(world, Stories);

        Assert.Equal((64, "MSK-TASK-83"), Deepest(epics.RootElement.GetProperty("items")));
        Assert.Equal((62, "MSK-TASK-83"), Deepest(stories.RootElement.GetProperty("items")));
        Assert.Equal(["MSK-EPIC-1", "MSK-EPIC-20"], Identifiers(epics.RootElement.GetProperty("items").EnumerateArray()));
    }

    /// <summary>A backlog read with a JSON reader deep enough for a chain at the bound; fails the test unless it answers 200.</summary>
    private async Task<JsonDocument> ReadDeepAsync(BoardsWorld world, string level)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, BoardsTestData.Backlog(world, world.PlatformId, level));
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

    // api-contract § 2.3: the board does not change — a card carries no links and none of the backlog's new values.
    [Fact]
    public async Task GetBoard_CardsCarryNoLinksAndNoPlanningValues()
    {
        var (world, _) = await BacklogTreeTestData.CreateWorldAsync(factory);

        var board = await BoardsTestData.GetBoardAsync(factory, world, world.PlatformId, Epics);

        Assert.Equal(HttpStatusCode.OK, board.Status);
        var cards = board.Body.GetProperty("columns").EnumerateArray().SelectMany(column => column.GetProperty("items").EnumerateArray()).ToList();
        Assert.Equal(["MSK-EPIC-1", "MSK-EPIC-20"], Identifiers(cards));
        Assert.All(cards, card => Assert.DoesNotContain(
            card.EnumerateObject().Select(member => member.Name),
            name => name is "parent" or "children" or "effort" or "businessValue" or "valueArea"));
    }

    // --- feat-027 US-5, US-6 and feat-001 US-5: re-parenting through the parent write -------------------------------

    // feat-027 US-5 · "A story dropped onto a feature becomes its child": the write names MSK-FEAT-8, MSK-EPIC-1 seen;
    // MSK-US-22 then stands beneath MSK-FEAT-8, after MSK-FEAT-23 (by level), and no longer beneath MSK-EPIC-1.
    [Fact]
    public async Task Drop_AStoryOntoAFeature_ItStandsBeneathIt()
    {
        var (world, items) = await BacklogTreeTestData.CreateWorldAsync(factory);

        var answer = await MapAsync(world, items["MSK-US-22"], items["MSK-FEAT-8"], items["MSK-EPIC-1"]);

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Equal(
            "MSK-EPIC-1[MSK-FEAT-8[MSK-FEAT-23 MSK-US-22] MSK-FEAT-6[MSK-US-2[MSK-BUG-5 MSK-TASK-3] MSK-BUG-4]] MSK-EPIC-20",
            await ShapeAsync(world, world.PlatformId, Epics));
        Assert.Equal("MSK-FEAT-8", BacklogTreeTestData.ParentOf(BacklogTreeTestData.Row(await RowsAsync(world, world.PlatformId, Stories), "MSK-US-22")));
    }

    // feat-027 US-5 · "A bug dropped onto a story leaves the top level": on «Истории» MSK-BUG-4 onto MSK-US-22 — MSK-US-2
    // reads «1», MSK-US-22 «2», and MSK-BUG-4 shows only beneath MSK-US-22, with no «Порядок».
    [Fact]
    public async Task Drop_ABugOntoAStory_LeavesTheTopLevel_OrderStaysDense()
    {
        var (world, items) = await BacklogTreeTestData.CreateWorldAsync(factory);

        var answer = await MapAsync(world, items["MSK-BUG-4"], items["MSK-US-22"], items["MSK-FEAT-6"]);

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        var rows = await RowsAsync(world, world.PlatformId, Stories);
        Assert.Equal("MSK-US-2[MSK-BUG-5 MSK-TASK-3] MSK-US-22[MSK-BUG-4]", BacklogTreeTestData.Shape(rows));
        Assert.Equal([1, 2], rows.Select(row => row.GetProperty("order").GetInt32()));
        Assert.False(BacklogTreeTestData.Row(rows, "MSK-BUG-4").TryGetProperty("order", out _));
    }

    // feat-027 US-5 · "A nested row can be dragged onto another row": MSK-BUG-4, nested under MSK-FEAT-6, onto MSK-EPIC-20.
    [Fact]
    public async Task Drop_ANestedRowOntoAnotherTopLevelRow_MovesItThere()
    {
        var (world, items) = await BacklogTreeTestData.CreateWorldAsync(factory);

        var answer = await MapAsync(world, items["MSK-BUG-4"], items["MSK-EPIC-20"], items["MSK-FEAT-6"]);

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Equal(
            "MSK-EPIC-1[MSK-FEAT-8[MSK-FEAT-23] MSK-FEAT-6[MSK-US-2[MSK-BUG-5 MSK-TASK-3]] MSK-US-22] MSK-EPIC-20[MSK-BUG-4]",
            await ShapeAsync(world, world.PlatformId, Epics));
    }

    // feat-027 US-5 · "A descendant on another team is refused by the server": MSK-FEAT-8 → MSK-FEAT-21 («Мастаск») →
    // a feature of «Платформа», which is a top-level row of «Фичи» since its parent is another team's; dropping MSK-FEAT-8
    // onto it is refused with the cycle's path through the row the page does not show, and nothing changes.
    [Fact]
    public async Task Drop_OntoADescendantThroughAnotherTeam_Returns409CycleWithItsPath()
    {
        var (world, items) = await BacklogTreeTestData.CreateWorldAsync(factory);
        await MapAsync(world, items["MSK-FEAT-21"], items["MSK-FEAT-8"], items["MSK-EPIC-1"]);
        var created = await BacklogTreeTestData.CreateChildAsync(factory, world, "FEAT", "Отчёт о входах", items["MSK-FEAT-21"].Id.ToString());
        Assert.Equal(HttpStatusCode.Created, created.Status);
        var report = new Item(created.Body.GetProperty("id").GetGuid(), created.Body.GetProperty("identifier").GetString()!);
        await BoardsTestData.MoveToAreaAsync(factory, world, report, world.PlatformAreaId);
        var before = await ShapeAsync(world, world.PlatformId, Features);

        var answer = await MapAsync(world, items["MSK-FEAT-8"], report, items["MSK-EPIC-1"]);

        Assert.Equal($"MSK-FEAT-8[MSK-FEAT-23] MSK-FEAT-6[MSK-US-2[MSK-BUG-5 MSK-TASK-3] MSK-BUG-4] {report.Identifier}", before);
        BoardsTestData.AssertRefused(answer, HttpStatusCode.Conflict, "workitems.link.cycle");
        Assert.Equal($"Нельзя выбрать родителем {report.Identifier}: он вложен в MSK-FEAT-8 (MSK-FEAT-8 → MSK-FEAT-21 → {report.Identifier}).", answer.Detail);
        Assert.Equal(before, await ShapeAsync(world, world.PlatformId, Features));
    }

    // feat-001 US-5 · "A feature gets an epic, replacing its old one in one write": MSK-FEAT-6 under MSK-EPIC-20, its whole
    // subtree with it.
    [Fact]
    public async Task Map_AFeatureToAnotherEpic_ItsSubtreeMovesWithIt()
    {
        var (world, items) = await BacklogTreeTestData.CreateWorldAsync(factory);

        var answer = await MapAsync(world, items["MSK-FEAT-6"], items["MSK-EPIC-20"], items["MSK-EPIC-1"]);

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Equal(items["MSK-EPIC-20"].Id, await LinksTestData.StoredParentAsync(factory, items["MSK-FEAT-6"]));
        Assert.Equal(
            "MSK-EPIC-1[MSK-FEAT-8[MSK-FEAT-23] MSK-US-22] MSK-EPIC-20[MSK-FEAT-6[MSK-US-2[MSK-BUG-5 MSK-TASK-3] MSK-BUG-4]]",
            await ShapeAsync(world, world.PlatformId, Epics));
    }

    // feat-027 US-6 and feat-001 US-5 · "«Убрать родителя» clears the parent": MSK-FEAT-6 keeps its place on «Фичи», reads
    // «Нет», and is beneath no row of «Эпики»; a same-level child cleared joins the top level («Порядок» 3).
    [Fact]
    public async Task Map_RemoveParent_TheRowKeepsItsPlace_AndLeavesTheTree()
    {
        var (world, items) = await BacklogTreeTestData.CreateWorldAsync(factory);
        var before = await BoardsTestData.RowsAsync(factory, world, world.PlatformId, Features);

        var cleared = await MapAsync(world, items["MSK-FEAT-6"], null, items["MSK-EPIC-1"]);
        var clearedSameLevel = await MapAsync(world, items["MSK-FEAT-23"], null, items["MSK-FEAT-8"]);

        Assert.Equal((HttpStatusCode.OK, HttpStatusCode.OK), (cleared.Status, clearedSameLevel.Status));
        Assert.Equal(["MSK-FEAT-8", "MSK-FEAT-6"], before);
        Assert.Equal(["MSK-FEAT-8", "MSK-FEAT-6", "MSK-FEAT-23"], await BoardsTestData.RowsAsync(factory, world, world.PlatformId, Features));
        Assert.Null(BacklogTreeTestData.ParentOf(BacklogTreeTestData.Row(await RowsAsync(world, world.PlatformId, Features), "MSK-FEAT-6")));
        Assert.Equal("MSK-EPIC-1[MSK-FEAT-8 MSK-US-22] MSK-EPIC-20", await ShapeAsync(world, world.PlatformId, Epics));
    }

    // feat-027 US-5 · "A drop changes no place and no other field": MSK-US-2 to MSK-FEAT-8 keeps its place on «Истории»,
    // its Статус, Исполнитель and Область; among MSK-FEAT-8's children it stands by level, then in the «Истории» order.
    [Fact]
    public async Task Map_ChangesNoOrderAndNoOtherField_ChildrenFollowTheirBacklogsOrder()
    {
        var (world, items) = await BacklogTreeTestData.CreateWorldAsync(factory);
        await MapAsync(world, items["MSK-US-22"], items["MSK-FEAT-8"], items["MSK-EPIC-1"]);
        await BoardsTestData.MoveAsync(factory, world, world.PlatformId, BoardsTestData.Top(items["MSK-US-22"]), Stories);
        var order = await BoardsTestData.RowsAsync(factory, world, world.PlatformId, Stories);
        var before = await WorkItemsTestData.ReadAsync(factory, world.Items, "MSK-US-2");

        var answer = await MapAsync(world, items["MSK-US-2"], items["MSK-FEAT-8"], items["MSK-FEAT-6"]);

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Equal(order, await BoardsTestData.RowsAsync(factory, world, world.PlatformId, Stories));
        var after = await WorkItemsTestData.ReadAsync(factory, world.Items, "MSK-US-2");
        foreach (var field in new[] { "state", "assignee", "area", "priority", "valueArea" })
        {
            Assert.Equal(before.GetProperty(field).GetRawText(), after.GetProperty(field).GetRawText());
        }

        Assert.Equal(
            "MSK-FEAT-8[MSK-FEAT-23 MSK-US-22 MSK-US-2[MSK-BUG-5 MSK-TASK-3]] MSK-FEAT-6[MSK-BUG-4]",
            await ShapeAsync(world, world.PlatformId, Features));
    }

    // feat-027 US-5 · "A parent changed meanwhile refuses the drop", feat-001 US-5 · "A refused clear puts the cell back":
    // Борис's backlog showed MSK-US-2 under MSK-FEAT-6; Анна has since mapped it to MSK-FEAT-8. Борис's drop and his clear
    // are refused with the code's text, and the backlog read again shows MSK-FEAT-8.
    [Fact]
    public async Task Map_FromAStaleBacklog_Returns409ParentChanged_TheReadShowsTheStoredParent()
    {
        var (world, items) = await BacklogTreeTestData.CreateWorldAsync(factory);
        var borisSaw = items["MSK-FEAT-6"];

        var anna = await MapAsync(world, items["MSK-US-2"], items["MSK-FEAT-8"], borisSaw);
        var boris = await MapAsync(world, items["MSK-US-2"], items["MSK-FEAT-23"], borisSaw);
        var borisClears = await MapAsync(world, items["MSK-US-2"], null, borisSaw);

        Assert.Equal(HttpStatusCode.OK, anna.Status);
        BoardsTestData.AssertRefused(boris, HttpStatusCode.Conflict, "workitems.link.parent_changed");
        Assert.Equal("Родитель уже изменён другим участником. Данные обновлены — проверьте и повторите.", boris.Detail);
        BoardsTestData.AssertRefused(borisClears, HttpStatusCode.Conflict, "workitems.link.parent_changed");
        Assert.Equal("MSK-FEAT-8", BacklogTreeTestData.ParentOf(BacklogTreeTestData.Row(await RowsAsync(world, world.PlatformId, Stories), "MSK-US-2")));
    }

    // feat-001 spec § NFR, "a parent write and a reorder of the same item both take effect": MSK-FEAT-6's mapping to
    // MSK-EPIC-20 holds at its commit while «Фичи» is reordered with MSK-FEAT-6 to the bottom; both are kept. The
    // reorder is read from the whole backlog's order — the board's cards carry `order` over the whole backlog (api-contract
    // § 2.3) — because the page's top-level rows cannot show it: MSK-FEAT-23 stands beneath MSK-FEAT-8, so the rows read
    // [MSK-FEAT-8, MSK-FEAT-6] whether or not MSK-FEAT-6 was moved below MSK-FEAT-23 (review B-4).
    [Fact]
    public async Task Map_AndAReorderOfTheSameItemAtOnce_BothTakeEffect()
    {
        var (world, items) = await BacklogTreeTestData.CreateWorldAsync(factory);

        using var gate = factory.CommitGates.Open();
        var map = LinksTestData.SetParentAsync(factory, world.Items, "MSK-FEAT-6", items["MSK-EPIC-20"].Id, items["MSK-EPIC-1"].Id, gate);
        await Task.WhenAny(gate.Reached, map);
        Assert.False(map.IsCompleted, "The mapping finished before reaching its commit gate.");

        var reorder = BoardsTestData.ReorderAsync(factory, world, world.PlatformId, BoardsTestData.Bottom(items["MSK-FEAT-6"]), Features);
        await PgLocks.WaitUntilBlockedAsync(factory.ConnectionString, reorder);

        gate.Release();
        var (mapped, reordered) = (await map, await reorder);

        Assert.Equal((HttpStatusCode.OK, HttpStatusCode.NoContent), (mapped.Status, reordered.Status));
        Assert.Equal(["MSK-FEAT-8", "MSK-FEAT-23", "MSK-FEAT-6"], await WholeBacklogOrderAsync(world, Features));
        Assert.Equal(["MSK-FEAT-8", "MSK-FEAT-6"], await BoardsTestData.RowsAsync(factory, world, world.PlatformId, Features));
        Assert.Equal(
            "MSK-EPIC-1[MSK-FEAT-8[MSK-FEAT-23] MSK-US-22] MSK-EPIC-20[MSK-FEAT-6[MSK-US-2[MSK-BUG-5 MSK-TASK-3] MSK-BUG-4]]",
            await ShapeAsync(world, world.PlatformId, Epics));
    }

    // --- feat-001 US-7 --------------------------------------------------------------------------------------------

    // feat-001 US-7 · "A child of a deleted parent is unparented on the backlog": MSK-EPIC-1 in «Удалена» — MSK-FEAT-6's
    // and MSK-FEAT-8's «Родитель» read «Нет», MSK-EPIC-1 is not on «Эпики», and nothing of its tree shows there.
    [Fact]
    public async Task Get_ChildrenOfADeletedParent_ReadNoParent_AndNestNowhere()
    {
        var (world, items) = await BacklogTreeTestData.CreateWorldAsync(factory);

        await LinksTestData.DeleteAsync(factory, world.Items, items["MSK-EPIC-1"]);

        Assert.Equal("MSK-EPIC-20", await ShapeAsync(world, world.PlatformId, Epics));
        var features = await RowsAsync(world, world.PlatformId, Features);
        Assert.Null(BacklogTreeTestData.ParentOf(BacklogTreeTestData.Row(features, "MSK-FEAT-6")));
        Assert.Null(BacklogTreeTestData.ParentOf(BacklogTreeTestData.Row(features, "MSK-FEAT-8")));
        Assert.Null(BacklogTreeTestData.ParentOf((await RowsAsync(world, world.FirstTeamId, Features)).Single()));
        Assert.Null(BacklogTreeTestData.ParentOf(BacklogTreeTestData.Row(await RowsAsync(world, world.PlatformId, Stories), "MSK-US-22")));
        Assert.Equal(items["MSK-EPIC-1"].Id, await LinksTestData.StoredParentAsync(factory, items["MSK-FEAT-6"]));
    }

    // feat-001 US-7 · "The mapping replaces the link to a deleted parent": the row showed «Нет», so the write saw no
    // parent; it is accepted although MSK-EPIC-1 is stored, and MSK-FEAT-6's only parent is MSK-EPIC-20.
    [Fact]
    public async Task Map_AChildOfADeletedParent_FromARowThatReadNone_ReplacesTheLink()
    {
        var (world, items) = await BacklogTreeTestData.CreateWorldAsync(factory);
        await LinksTestData.DeleteAsync(factory, world.Items, items["MSK-EPIC-1"]);

        var answer = await MapAsync(world, items["MSK-FEAT-6"], items["MSK-EPIC-20"], null);

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Equal(items["MSK-EPIC-20"].Id, await LinksTestData.StoredParentAsync(factory, items["MSK-FEAT-6"]));
        Assert.Equal("MSK-EPIC-20[MSK-FEAT-6[MSK-US-2[MSK-BUG-5 MSK-TASK-3] MSK-BUG-4]]", await ShapeAsync(world, world.PlatformId, Epics));
    }
}
