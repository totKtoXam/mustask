using System.Net;
using System.Text.Json;
using Mustask.Api.IntegrationTests.Features.Organizations;
using Mustask.Api.IntegrationTests.Features.WorkItems;

namespace Mustask.Api.IntegrationTests.Features.Boards;

/// <summary>
/// <c>GET /api/organizations/{organizationId}/projects/{projectId}/teams/{teamId}/backlogs/{level}</c>
/// with feat-001's tree (its api-contract § 4): every row's «Родитель» and the
/// rows nested beneath it — the same team's backlog one level below, in that
/// backlog's order — read from the links at each read (US-4), with the mapping
/// written through the parent write (US-5) and a deleted parent's children
/// unparented (US-7).
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class BacklogTreeEndpointTests(MustaskApiFactory factory)
{
    private const string Epics = "epics";
    private const string Features = "features";
    private const string Stories = "stories";

    private Task<string> ShapeAsync(BoardsWorld world, Guid teamId, string level) => BacklogTreeTestData.ShapeAsync(factory, world, teamId, level);

    private Task<JsonElement[]> RowsAsync(BoardsWorld world, Guid teamId, string level) => BacklogTreeTestData.TopRowsAsync(factory, world, teamId, level);

    private Task<Answer> MapAsync(BoardsWorld world, Item child, Item? parent, Item? seen) =>
        LinksTestData.SetParentAsync(factory, world.Items, child.Identifier, parent?.Id, seen?.Id);

    // --- US-4 -------------------------------------------------------------------------------------------------------

    // US-4 · "Rows start collapsed, an expand control only where children show", "Expanding shows the children of the
    // level below in their order" and "A nested row expands one tier further": «Эпики» of «Платформа» is MSK-EPIC-1 with
    // MSK-FEAT-8 then MSK-FEAT-6 («Фичи»'s order), MSK-FEAT-6 with MSK-US-2 and MSK-BUG-4; MSK-EPIC-20 has none.
    [Fact]
    public async Task Get_Epics_ThreeTiers_ChildrenInTheirOwnBacklogsOrder()
    {
        var (world, _) = await BacklogTreeTestData.CreateWorldAsync(factory);

        Assert.Equal("MSK-EPIC-1[MSK-FEAT-8 MSK-FEAT-6[MSK-US-2 MSK-BUG-4]] MSK-EPIC-20", await ShapeAsync(world, world.PlatformId, Epics));
    }

    // US-4 · a nested row has no «Порядок» (BR-H-11) and its «Родитель» cell reads the row above it (BR-H-13); a top-level
    // row keeps mod-006's «Порядок» 1..n.
    [Fact]
    public async Task Get_NestedRows_HaveNoOrder_TheirParentIsTheRowAbove()
    {
        var (world, items) = await BacklogTreeTestData.CreateWorldAsync(factory);

        var rows = await RowsAsync(world, world.PlatformId, Epics);

        Assert.Equal([1, 2], rows.Select(row => row.GetProperty("order").GetInt32()));
        Assert.Equal(JsonValueKind.Null, rows[0].GetProperty("parent").ValueKind);
        var feature = BacklogTreeTestData.Row(rows, "MSK-FEAT-6");
        var story = BacklogTreeTestData.Row(rows, "MSK-US-2");
        Assert.False(feature.TryGetProperty("order", out _));
        Assert.False(story.TryGetProperty("order", out _));
        Assert.Equal(("MSK-EPIC-1", "MSK-FEAT-6"), (BacklogTreeTestData.ParentOf(feature), BacklogTreeTestData.ParentOf(story)));
        Assert.Equal(items["MSK-EPIC-1"].Id, feature.GetProperty("parent").GetProperty("id").GetGuid());
        Assert.Equal(("Новая", "Вход по ссылке"), (feature.GetProperty("state").GetProperty("name").GetString(), feature.GetProperty("title").GetString()));
    }

    // US-4 · "A link that does not nest leaves the child a top-level row elsewhere": MSK-FEAT-21 (another team's
    // backlog), MSK-US-22 (parent two levels up), MSK-FEAT-23 (parent on its own level) are beneath no row of
    // «Платформа», and each is a top-level row of its own backlog, its «Родитель» reading its parent.
    [Fact]
    public async Task Get_ALinkThatDoesNotNest_LeavesTheChildATopLevelRowOfItsOwnBacklog()
    {
        var (world, _) = await BacklogTreeTestData.CreateWorldAsync(factory);

        var epics = await ShapeAsync(world, world.PlatformId, Epics);
        var features = await RowsAsync(world, world.PlatformId, Features);
        var stories = await RowsAsync(world, world.PlatformId, Stories);
        var firstTeamFeatures = await RowsAsync(world, world.FirstTeamId, Features);

        foreach (var child in new[] { "MSK-FEAT-21", "MSK-US-22", "MSK-FEAT-23" })
        {
            Assert.DoesNotContain(child, epics);
        }

        Assert.Equal("MSK-FEAT-8 MSK-FEAT-6[MSK-US-2 MSK-BUG-4] MSK-FEAT-23", BacklogTreeTestData.Shape(features));
        Assert.Equal(("MSK-FEAT-21", "MSK-EPIC-1"), (firstTeamFeatures.Single().GetProperty("identifier").GetString(), BacklogTreeTestData.ParentOf(firstTeamFeatures.Single())));
        Assert.Equal("MSK-EPIC-1", BacklogTreeTestData.ParentOf(stories.Single(row => row.GetProperty("identifier").GetString() == "MSK-US-22")));
        Assert.Equal("MSK-FEAT-8", BacklogTreeTestData.ParentOf(features.Single(row => row.GetProperty("identifier").GetString() == "MSK-FEAT-23")));
    }

    // US-4 · "Unparented rows are normal": on «Фичи», MSK-FEAT-23's «Родитель» reads MSK-FEAT-8 and MSK-FEAT-6's
    // MSK-EPIC-1; a row with no parent reads «Нет» (null).
    [Fact]
    public async Task Get_Features_EveryRowCarriesItsParentCell()
    {
        var (world, items) = await BacklogTreeTestData.CreateWorldAsync(factory);
        await MapAsync(world, items["MSK-FEAT-23"], null, items["MSK-FEAT-8"]);

        var features = await RowsAsync(world, world.PlatformId, Features);

        Assert.Equal(
            [("MSK-FEAT-8", "MSK-EPIC-1"), ("MSK-FEAT-6", "MSK-EPIC-1"), ("MSK-FEAT-23", null)],
            features.Select(row => (row.GetProperty("identifier").GetString(), BacklogTreeTestData.ParentOf(row))));
    }

    // US-4 · "«Истории» rows do not expand in this cut" (OQ-5): MSK-TASK-3 under MSK-US-2 nests nowhere.
    [Fact]
    public async Task Get_Stories_NoRowHasChildren()
    {
        var (world, items) = await BacklogTreeTestData.CreateWorldAsync(factory);
        await LinksTestData.LinkAsync(factory, world.Items, items["MSK-TASK-3"], items["MSK-US-2"]);

        var stories = await RowsAsync(world, world.PlatformId, Stories);

        Assert.All(stories, row => Assert.Empty(row.GetProperty("children").EnumerateArray()));
    }

    // US-4 · "A reorder moves a row with its shown children" and "Collapsing … changes nothing": a reorder of the epics
    // keeps MSK-EPIC-1's subtree beneath it and changes no link; reading the tree changes no «Изменён» time.
    [Fact]
    public async Task Get_AfterAReorder_TheSubtreeMovesWithItsRow_AndReadsChangeNothing()
    {
        var (world, items) = await BacklogTreeTestData.CreateWorldAsync(factory);
        var before = await WorkItemsTestData.StoredAsync(factory, world.ProjectId);

        await BoardsTestData.MoveAsync(factory, world, world.PlatformId, BoardsTestData.Top(items["MSK-EPIC-20"]), Epics);

        Assert.Equal("MSK-EPIC-20 MSK-EPIC-1[MSK-FEAT-8 MSK-FEAT-6[MSK-US-2 MSK-BUG-4]]", await ShapeAsync(world, world.PlatformId, Epics));
        Assert.Equal(before, await WorkItemsTestData.StoredAsync(factory, world.ProjectId));
    }

    // BR-H-11: the children follow their own backlog's order, so a reorder of «Фичи» reorders them beneath MSK-EPIC-1.
    [Fact]
    public async Task Get_AReorderOfTheLevelBelow_ReordersTheChildren()
    {
        var (world, items) = await BacklogTreeTestData.CreateWorldAsync(factory);

        await BoardsTestData.MoveAsync(factory, world, world.PlatformId, BoardsTestData.Top(items["MSK-FEAT-6"]), Features);

        Assert.Equal("MSK-EPIC-1[MSK-FEAT-6[MSK-US-2 MSK-BUG-4] MSK-FEAT-8] MSK-EPIC-20", await ShapeAsync(world, world.PlatformId, Epics));
    }

    // US-4 · "A link changed elsewhere shows at the next read" (BR-H-19): MSK-FEAT-6's parent cleared on its form.
    [Fact]
    public async Task Get_ALinkChangedElsewhere_ShowsAtTheNextRead()
    {
        var (world, items) = await BacklogTreeTestData.CreateWorldAsync(factory);

        var cleared = await WorkItemsTestData.SaveAsync(
            factory, world.Items, "MSK-FEAT-6", new { parent = new { parentId = (string?)null, seenParentId = items["MSK-EPIC-1"].Id.ToString() } });

        Assert.Equal(HttpStatusCode.OK, cleared.Status);
        Assert.Equal("MSK-EPIC-1[MSK-FEAT-8] MSK-EPIC-20", await ShapeAsync(world, world.PlatformId, Epics));
    }

    // BR-H-17: the board does not change — a card carries no parent and no children.
    [Fact]
    public async Task GetBoard_CardsCarryNoLinks()
    {
        var (world, _) = await BacklogTreeTestData.CreateWorldAsync(factory);

        var board = await BoardsTestData.GetBoardAsync(factory, world, world.PlatformId, Epics);

        Assert.Equal(HttpStatusCode.OK, board.Status);
        var cards = board.Body.GetProperty("columns").EnumerateArray().SelectMany(column => column.GetProperty("items").EnumerateArray()).ToList();
        Assert.NotEmpty(cards);
        Assert.All(cards, card => Assert.False(card.TryGetProperty("parent", out _) || card.TryGetProperty("children", out _)));
    }

    // --- US-5 -------------------------------------------------------------------------------------------------------

    // US-5 · "A story gets a feature as its parent": the dialog's picker offers only «Фича» items; the write replaces
    // MSK-EPIC-1 with MSK-FEAT-8; the row reads MSK-FEAT-8, and on «Фичи» MSK-US-22 is beneath MSK-FEAT-8.
    [Fact]
    public async Task Map_AStoryToAFeature_TheRowAndTheLevelAboveShowIt()
    {
        var (world, items) = await BacklogTreeTestData.CreateWorldAsync(factory);
        var offered = await LinksTestData.OfferedAsync(factory, world.Items, "MSK-US-22", levelAbove: "true");

        var answer = await MapAsync(world, items["MSK-US-22"], items["MSK-FEAT-8"], items["MSK-EPIC-1"]);

        Assert.All(offered, identifier => Assert.StartsWith("MSK-FEAT-", identifier, StringComparison.Ordinal));
        Assert.Contains("MSK-FEAT-8", offered);
        Assert.Equal(HttpStatusCode.OK, answer.Status);
        var stories = await RowsAsync(world, world.PlatformId, Stories);
        Assert.Equal("MSK-FEAT-8", BacklogTreeTestData.ParentOf(BacklogTreeTestData.Row(stories, "MSK-US-22")));
        Assert.Equal("MSK-FEAT-8[MSK-US-22] MSK-FEAT-6[MSK-US-2 MSK-BUG-4] MSK-FEAT-23", await ShapeAsync(world, world.PlatformId, Features));
    }

    // US-5 · "A feature gets an epic, replacing its old one in one write": MSK-FEAT-6 under MSK-EPIC-20, its stories
    // still beneath it.
    [Fact]
    public async Task Map_AFeatureToAnotherEpic_ItsSubtreeMovesWithIt()
    {
        var (world, items) = await BacklogTreeTestData.CreateWorldAsync(factory);

        var answer = await MapAsync(world, items["MSK-FEAT-6"], items["MSK-EPIC-20"], items["MSK-EPIC-1"]);

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Equal(items["MSK-EPIC-20"].Id, await LinksTestData.StoredParentAsync(factory, items["MSK-FEAT-6"]));
        Assert.Equal("MSK-EPIC-1[MSK-FEAT-8] MSK-EPIC-20[MSK-FEAT-6[MSK-US-2 MSK-BUG-4]]", await ShapeAsync(world, world.PlatformId, Epics));
    }

    // US-5 · "«Убрать родителя» clears the parent": MSK-FEAT-6 keeps its row and its «Порядок» on «Фичи», reads «Нет»,
    // and is beneath no row of «Эпики».
    [Fact]
    public async Task Map_RemoveParent_TheRowKeepsItsPlace_AndLeavesTheTree()
    {
        var (world, items) = await BacklogTreeTestData.CreateWorldAsync(factory);
        var before = await BoardsTestData.RowsAsync(factory, world, world.PlatformId, Features);

        var answer = await MapAsync(world, items["MSK-FEAT-6"], null, items["MSK-EPIC-1"]);

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Equal(before, await BoardsTestData.RowsAsync(factory, world, world.PlatformId, Features));
        Assert.Null(BacklogTreeTestData.ParentOf(BacklogTreeTestData.Row(await RowsAsync(world, world.PlatformId, Features), "MSK-FEAT-6")));
        Assert.Equal("MSK-EPIC-1[MSK-FEAT-8] MSK-EPIC-20", await ShapeAsync(world, world.PlatformId, Epics));
    }

    // US-5 · "The mapping changes no order and no other field": MSK-US-2 to MSK-FEAT-8 keeps its place on «Истории», its
    // Статус, Исполнитель and Область; among MSK-FEAT-8's children it shows in the «Истории» order.
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
        foreach (var field in new[] { "state", "assignee", "area" })
        {
            Assert.Equal(before.GetProperty(field).GetRawText(), after.GetProperty(field).GetRawText());
        }

        Assert.Equal("MSK-FEAT-8[MSK-US-22 MSK-US-2] MSK-FEAT-6[MSK-BUG-4] MSK-FEAT-23", await ShapeAsync(world, world.PlatformId, Features));
    }

    // US-5 · "A parent changed meanwhile refuses the mapping" and "A refused clear puts the cell back": Борис's backlog
    // showed MSK-US-2 under MSK-FEAT-6; Анна has since mapped it to MSK-FEAT-8. Борис's mapping and his clear are refused
    // with the code's text, and the backlog read again shows MSK-FEAT-8.
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

    // spec § NFR, "a parent write and a reorder of the same item both take effect": MSK-FEAT-6's mapping to MSK-EPIC-20
    // holds at its commit while «Фичи» is reordered with MSK-FEAT-6 to the bottom; both are kept.
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
        Assert.Equal(["MSK-FEAT-8", "MSK-FEAT-23", "MSK-FEAT-6"], await BoardsTestData.RowsAsync(factory, world, world.PlatformId, Features));
        Assert.Equal("MSK-EPIC-1[MSK-FEAT-8] MSK-EPIC-20[MSK-FEAT-6[MSK-US-2 MSK-BUG-4]]", await ShapeAsync(world, world.PlatformId, Epics));
    }

    // --- US-7 -------------------------------------------------------------------------------------------------------

    // US-7 · "A child of a deleted parent is unparented on the backlog": MSK-EPIC-1 in «Удалена» — MSK-FEAT-6's and
    // MSK-FEAT-8's «Родитель» read «Нет», MSK-EPIC-1 is not on «Эпики», and they nest under no row.
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
        Assert.Equal(items["MSK-EPIC-1"].Id, await LinksTestData.StoredParentAsync(factory, items["MSK-FEAT-6"]));
    }

    // US-7 · "The mapping replaces the link to a deleted parent": the row showed «Нет», so the write saw no parent; it
    // is accepted although MSK-EPIC-1 is stored, and MSK-FEAT-6's only parent is MSK-EPIC-20.
    [Fact]
    public async Task Map_AChildOfADeletedParent_FromARowThatReadNone_ReplacesTheLink()
    {
        var (world, items) = await BacklogTreeTestData.CreateWorldAsync(factory);
        await LinksTestData.DeleteAsync(factory, world.Items, items["MSK-EPIC-1"]);

        var answer = await MapAsync(world, items["MSK-FEAT-6"], items["MSK-EPIC-20"], null);

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Equal(items["MSK-EPIC-20"].Id, await LinksTestData.StoredParentAsync(factory, items["MSK-FEAT-6"]));
        Assert.Equal("MSK-EPIC-20[MSK-FEAT-6[MSK-US-2 MSK-BUG-4]]", await ShapeAsync(world, world.PlatformId, Epics));
    }
}
