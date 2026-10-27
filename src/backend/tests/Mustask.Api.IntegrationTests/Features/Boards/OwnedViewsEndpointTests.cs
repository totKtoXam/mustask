using System.Net;
using System.Text.Json;
using Mustask.Api.IntegrationTests.Features.Organizations;
using Mustask.Api.IntegrationTests.Features.WorkItems;
using Mustask.Domain.Process;
using static Mustask.Api.IntegrationTests.Features.WorkItems.OwningTeamTestData;

namespace Mustask.Api.IntegrationTests.Features.Boards;

/// <summary>
/// The views of the owning team (mod-017 US-4, BR-OT-5, BR-OT-6, BR-OT-7): a team's backlog and board hold the items it owns, a
/// project's all of its items; an area, its owner and its moves decide nothing; every row and card carries the team. Every test
/// builds its own world (<see cref="OwningTeamTestData"/>): the database is shared and never emptied.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class OwnedViewsEndpointTests(MustaskApiFactory factory)
{
    private async Task<Item> ItemAsync(OwningTeamWorld world, string typeKey, string title, Guid? team)
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
            },
            OrganizationsTokens.SignedIn());
        Assert.Equal(HttpStatusCode.Created, answer.Status);

        return new Item(answer.Body.GetProperty("id").GetGuid(), answer.Body.GetProperty("identifier").GetString()!);
    }

    private Task<Answer> SaveAsync(OwningTeamWorld world, Item item, object body) =>
        WorkItemsTestData.SaveAsync(factory, world.Items, item.Identifier, body);

    // US-4 · "each team holds its own; the project all": MARS holds its story, VENUS its own, the team «Альфа» came with none;
    // the project's backlog holds every story once, whoever owns it or none, and «Без команды» is on no team's backlog.
    [Fact]
    public async Task Views_TeamsAndProject_HoldTheirItems_EachOnce_NoTeamItemOnNoTeamView()
    {
        var world = await CreateWorldAsync(factory);
        var boards = BoardsViewOf(world);
        var marsStory = await ItemAsync(world, "US", "Марс", world.Mars);
        var venusStory = await ItemAsync(world, "US", "Венера", world.Venus);
        var unowned = await ItemAsync(world, "US", "Ничья", null);

        Assert.Equal([marsStory.Identifier], await BoardsTestData.RowsAsync(factory, boards, world.Mars));
        Assert.Equal([venusStory.Identifier], await BoardsTestData.RowsAsync(factory, boards, world.Venus));
        Assert.Empty(await BoardsTestData.RowsAsync(factory, boards, world.OwnTeam));
        Assert.Equal(
            [marsStory.Identifier, venusStory.Identifier, unowned.Identifier],
            await ProjectBoardsTestData.RowsAsync(factory, boards));

        foreach (var team in new[] { world.Mars, world.Venus, world.OwnTeam })
        {
            var board = (await BoardsTestData.GetBoardAsync(factory, boards, team)).Body;
            Assert.DoesNotContain(unowned.Identifier, ProjectBoardsTestData.AllCards(board));
        }

        Assert.Contains(unowned.Identifier, ProjectBoardsTestData.AllCards((await ProjectBoardsTestData.GetBoardAsync(factory, boards)).Body));
    }

    // US-4 · "membership no longer follows areas": renaming and moving an area and saving an item's Область move no item
    // between teams (BR-OT-5, BR-OT-10).
    [Fact]
    public async Task Views_AreaRenameAndMove_MoveNoItem()
    {
        var world = await CreateWorldAsync(factory);
        var boards = BoardsViewOf(world);
        var story = await ItemAsync(world, "US", "Марс", world.Mars);
        var reports = await OrganizationsTestData.AddAreaAsync(factory, world.OrganizationId, null, "Отчёты");
        var archive = await OrganizationsTestData.AddAreaAsync(factory, world.OrganizationId, null, "Архив");
        var renamed = await OrganizationsApi.SendAsync(
            factory,
            HttpMethod.Put,
            $"{OrganizationsApi.Areas(world.OrganizationId)}/{reports.Id}/name",
            new { name = "Отчёты 2" },
            OrganizationsTokens.SignedIn());
        var moved = await OrganizationsApi.SendAsync(
            factory,
            HttpMethod.Put,
            $"{OrganizationsApi.Areas(world.OrganizationId)}/{reports.Id}/parent",
            new { parentId = archive.Id.ToString() },
            OrganizationsTokens.SignedIn());

        Assert.Equal((HttpStatusCode.OK, HttpStatusCode.OK), (renamed.Status, moved.Status));
        Assert.Equal([story.Identifier], await BoardsTestData.RowsAsync(factory, boards, world.Mars));
        Assert.Empty(await BoardsTestData.RowsAsync(factory, boards, world.Venus));
    }

    // US-4 · "shown on every row and card" (BR-OT-7): a team's backlog and board and a project's backlog and board carry the
    // team's id and current name, or null for «Без команды»; a checklist entry carries none.
    [Fact]
    public async Task Rows_AndCards_CarryTheTeam_OrNone()
    {
        var world = await CreateWorldAsync(factory);
        var boards = BoardsViewOf(world);
        var owned = await ItemAsync(world, "US", "Марс", world.Mars);
        var unowned = await ItemAsync(world, "US", "Ничья", null);
        var task = await OrganizationsApi.SendAsync(
            factory,
            HttpMethod.Post,
            WorkItemsTestData.List(world.Items),
            new { requestId = Guid.NewGuid().ToString(), typeId = world.Items.Types["TASK"].Id.ToString(), title = "Задача", parentId = owned.Id.ToString() },
            OrganizationsTokens.SignedIn());
        Assert.Equal(HttpStatusCode.Created, task.Status);

        var teamBacklog = (await BoardsTestData.GetBacklogAsync(factory, boards, world.Mars)).Body.GetProperty("items");
        var projectBacklog = (await ProjectBoardsTestData.GetBacklogAsync(factory, boards)).Body.GetProperty("items");
        var teamBoard = (await BoardsTestData.GetBoardAsync(factory, boards, world.Mars)).Body;
        var projectBoard = (await ProjectBoardsTestData.GetBoardAsync(factory, boards)).Body;

        Assert.Equal((world.Mars, "MARS"), TeamOf(Assert.Single(teamBacklog.EnumerateArray())));
        Assert.Equal(
            [(world.Mars, "MARS"), ((Guid, string)?)null],
            projectBacklog.EnumerateArray().Select(row => TeamOf(row)).ToArray());
        Assert.Equal((world.Mars, "MARS"), TeamOf(Assert.Single(Cards(teamBoard))));
        Assert.Equal(
            [(world.Mars, "MARS"), ((Guid, string)?)null],
            Cards(projectBoard).Select(card => TeamOf(card)).ToArray());

        // The task is the story's child on the story's row: it carries the team too (it is a nested row), and on the board it is a
        // checklist entry, which carries none.
        var nested = Assert.Single(teamBacklog.EnumerateArray()).GetProperty("children").EnumerateArray().Single();
        Assert.Equal(task.Body.GetProperty("identifier").GetString(), nested.GetProperty("identifier").GetString());
        Assert.False(Cards(teamBoard).Single().GetProperty("checklist").EnumerateArray().Single().TryGetProperty("team", out _));
        Assert.Equal(unowned.Identifier, Cards(projectBoard).Last().GetProperty("identifier").GetString());
    }

    private static IEnumerable<JsonElement> Cards(JsonElement board) =>
        board.GetProperty("columns").EnumerateArray().SelectMany(column => column.GetProperty("items").EnumerateArray());

    // US-4 · "every state but Removed": the views hold the items in every state outside the Removed category, per level.
    [Fact]
    public async Task Views_HoldEveryStateButRemoved_PerLevel()
    {
        var world = await CreateWorldAsync(factory);
        var boards = BoardsViewOf(world);
        var proposed = await ItemAsync(world, "US", "Новая", world.Mars);
        var done = await ItemAsync(world, "US", "Закрытая", world.Mars);
        var removed = await ItemAsync(world, "US", "Удалённая", world.Mars);
        var epic = await ItemAsync(world, "EPIC", "Эпик", world.Mars);
        var type = world.Items.Types["US"];
        Assert.Equal(HttpStatusCode.OK, (await SaveAsync(world, done, new { stateId = type.StateId(StateCategory.Completed).ToString() })).Status);
        await LinksTestData.DeleteAsync(factory, world.Items, removed);

        Assert.Equal([proposed.Identifier, done.Identifier], await BoardsTestData.RowsAsync(factory, boards, world.Mars));
        Assert.Equal([proposed.Identifier, done.Identifier], await ProjectBoardsTestData.RowsAsync(factory, boards));
        Assert.Equal([epic.Identifier], await BoardsTestData.RowsAsync(factory, boards, world.Mars, "epics"));
    }

    // US-4 · "children follow the view": a team's view shows only its own children beneath a row; a child of another team
    // leaves the row, and a same-level child of another team's parent is a top-level row on its own team's backlog.
    [Fact]
    public async Task Backlog_Children_AreOnlyTheViewsOwn_ParentOfAnotherTeamLeavesATopLevelRow()
    {
        var world = await CreateWorldAsync(factory);
        var boards = BoardsViewOf(world);
        var parent = await ItemAsync(world, "FEAT", "Родитель", world.Mars);
        var marsChild = await OrganizationsApi.SendAsync(
            factory,
            HttpMethod.Post,
            WorkItemsTestData.List(world.Items),
            new { requestId = Guid.NewGuid().ToString(), typeId = world.Items.Types["FEAT"].Id.ToString(), title = "Свой", parentId = parent.Id.ToString() },
            OrganizationsTokens.SignedIn());
        var venusChild = await OrganizationsApi.SendAsync(
            factory,
            HttpMethod.Post,
            WorkItemsTestData.List(world.Items),
            new { requestId = Guid.NewGuid().ToString(), typeId = world.Items.Types["FEAT"].Id.ToString(), title = "Чужой", parentId = parent.Id.ToString() },
            OrganizationsTokens.SignedIn());
        var marsId = marsChild.Body.GetProperty("identifier").GetString()!;
        var venusId = venusChild.Body.GetProperty("identifier").GetString()!;
        await SaveAsync(world, new Item(venusChild.Body.GetProperty("id").GetGuid(), venusId), new { team = new { teamId = world.Venus.ToString() } });

        var mars = await BacklogTreeTestData.ShapeAsync(factory, boards, world.Mars, "features");
        var venus = await BacklogTreeTestData.ShapeAsync(factory, boards, world.Venus, "features");
        var project = await ProjectBoardsTestData.ShapeAsync(factory, boards, "features");

        Assert.Equal($"{parent.Identifier}[{marsId}]", mars);
        Assert.Equal(venusId, venus);
        Assert.Equal($"{parent.Identifier}[{marsId} {venusId}]", project);
    }

    // US-4 · "changing the team moves the item": it leaves the old team's backlog and lands last, unplaced, on the new one's,
    // though its number is the lowest; the project's place is kept (BR-OT-6).
    [Fact]
    public async Task Save_TeamChange_LeavesTheTeamViewAndLandsLastUnplaced_ProjectPlaceKept()
    {
        var world = await CreateWorldAsync(factory);
        var boards = BoardsViewOf(world);
        var moving = await ItemAsync(world, "US", "Меняет команду", world.Mars);
        var venusFirst = await ItemAsync(world, "US", "Венера 1", world.Venus);
        var venusSecond = await ItemAsync(world, "US", "Венера 2", world.Venus);
        await BoardsTestData.ReorderToAsync(factory, boards, world.Venus, venusSecond, venusFirst);
        await BoardsTestData.ReorderToAsync(factory, boards, world.Mars, moving);
        await ProjectBoardsTestData.ReorderToAsync(factory, boards, venusFirst, moving);

        var answer = await SaveAsync(world, moving, new { team = new { teamId = world.Venus.ToString() } });

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Empty(await BoardsTestData.RowsAsync(factory, boards, world.Mars));
        Assert.Equal([venusSecond.Identifier, venusFirst.Identifier, moving.Identifier], await BoardsTestData.RowsAsync(factory, boards, world.Venus));
        Assert.Equal(0, await TeamPlacesOfAsync(factory, moving.Id));
        Assert.Equal(1, await ProjectPlacesOfAsync(factory, moving.Id));
        Assert.Equal(
            [venusSecond.Identifier, venusFirst.Identifier, moving.Identifier],
            await ProjectBoardsTestData.RowsAsync(factory, boards));
    }

    // US-4 · "comes back unplaced": an item given back to its old team has no memory of its former place.
    [Fact]
    public async Task Save_TeamBack_ComesBackUnplaced_NoFormerPlace()
    {
        var world = await CreateWorldAsync(factory);
        var boards = BoardsViewOf(world);
        var first = await ItemAsync(world, "US", "Первая", world.Mars);
        var second = await ItemAsync(world, "US", "Вторая", world.Mars);
        var third = await ItemAsync(world, "US", "Третья", world.Mars);
        await BoardsTestData.ReorderToAsync(factory, boards, world.Mars, third, first, second);
        Assert.Equal([third.Identifier, first.Identifier, second.Identifier], await BoardsTestData.RowsAsync(factory, boards, world.Mars));

        await SaveAsync(world, third, new { team = new { teamId = world.Venus.ToString() } });
        await SaveAsync(world, third, new { team = new { teamId = world.Mars.ToString() } });

        Assert.Equal([first.Identifier, second.Identifier, third.Identifier], await BoardsTestData.RowsAsync(factory, boards, world.Mars));
    }

    // US-4 · "a card move writes only the state": moving a card to another column changes the item's state and nothing else —
    // the owning team, the team place and the project's place stay, and the card is on each view once.
    [Fact]
    public async Task CardMove_WritesOnlyTheState_BothPlacesKept_ShownOnceOnEachView()
    {
        var world = await CreateWorldAsync(factory);
        var boards = BoardsViewOf(world);
        var story = await ItemAsync(world, "US", "Карточка", world.Mars);
        var other = await ItemAsync(world, "US", "Соседняя", world.Mars);
        await BoardsTestData.ReorderToAsync(factory, boards, world.Mars, story, other);
        await ProjectBoardsTestData.ReorderToAsync(factory, boards, story, other);
        var before = await StoredAsync(factory, world.ProjectId, story.Identifier);

        var moved = await SaveAsync(world, story, new { stateId = world.Items.Types["US"].StateId(StateCategory.InProgress).ToString() });

        Assert.Equal(HttpStatusCode.OK, moved.Status);
        var after = await StoredAsync(factory, world.ProjectId, story.Identifier);
        Assert.Equal((before.TeamId, before.AssigneeId), (after.TeamId, after.AssigneeId));
        Assert.Equal((1, 1), (await TeamPlacesOfAsync(factory, story.Id), await ProjectPlacesOfAsync(factory, story.Id)));
        Assert.Single(ProjectBoardsTestData.AllCards((await BoardsTestData.GetBoardAsync(factory, boards, world.Mars)).Body), story.Identifier);
        Assert.Single(ProjectBoardsTestData.AllCards((await ProjectBoardsTestData.GetBoardAsync(factory, boards)).Body), story.Identifier);
        Assert.Equal([story.Identifier, other.Identifier], await BoardsTestData.RowsAsync(factory, boards, world.Mars));
    }
}
