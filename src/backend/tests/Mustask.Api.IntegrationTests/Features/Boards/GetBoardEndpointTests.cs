using System.Net;
using System.Text.Json;
using Mustask.Api.IntegrationTests.Features.Organizations;
using Mustask.Api.IntegrationTests.Features.WorkItems;
using Mustask.Application.Abstractions.Process;

namespace Mustask.Api.IntegrationTests.Features.Boards;

/// <summary>
/// <c>GET /api/organizations/{organizationId}/projects/{projectId}/teams/{teamId}/boards/{level}</c>
/// (mod-006 api-contract § 3.3): US-4 — the backlog's items as cards in columns
/// derived per (type, state) — and US-5, the card move, which is mod-003's PATCH
/// with the column's target state (api-contract § 6), seen through the board;
/// US-7's board scenario.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class GetBoardEndpointTests(MustaskApiFactory factory)
{
    private async Task<JsonElement> BoardAsync(BoardsWorld world, Guid teamId, string level = "stories")
    {
        var answer = await BoardsTestData.GetBoardAsync(factory, world, teamId, level);
        Assert.Equal(HttpStatusCode.OK, answer.Status);

        return answer.Body;
    }

    /// <summary>A card move as the board sends it: mod-003's PATCH with the state the target column maps for the card's type.</summary>
    private async Task<Answer> DropAsync(BoardsWorld world, Item item, string typeKey, string header)
    {
        var board = await BoardAsync(world, world.PlatformId);
        var stateId = BoardsTestData.Target(BoardsTestData.Column(board, header), world.TypeId(typeKey));
        Assert.NotNull(stateId);

        return await WorkItemsTestData.SaveAsync(factory, world.Items, item.Identifier, new { stateId });
    }

    private static string StateName(JsonElement item) => item.GetProperty("state").GetProperty("name").GetString()!;

    // --- US-4 ------------------------------------------------------------------------------------------------------

    // US-4 · "Each level's board has its own columns" (BR-M4-2): the three levels four, headed by the names of the states
    // they map (bug-004 D-3), with the keys and categories of api-contract § 3.3. The category's label is no header.
    [Theory]
    [InlineData("epics", "Новая,Активна,Разрешена,Закрыта", "Proposed-1,InProgress-1,Resolved-1,Completed-1")]
    [InlineData("features", "Новая,Активна,Разрешена,Закрыта", "Proposed-1,InProgress-1,Resolved-1,Completed-1")]
    [InlineData("stories", "Новая,Активна,Разрешена,Закрыта", "Proposed-1,InProgress-1,Resolved-1,Completed-1")]
    public async Task Get_EachLevel_HasItsColumns(string level, string headers, string keys)
    {
        var world = await BoardsTestData.CreateWorldAsync(factory);

        var columns = (await BoardAsync(world, world.PlatformId, level)).GetProperty("columns").EnumerateArray().ToList();

        Assert.Equal(headers.Split(','), columns.Select(column => column.GetProperty("header").GetString()));
        Assert.Equal(keys.Split(','), columns.Select(column => column.GetProperty("key").GetString()));
        // The column's category stays in the answer (its code and label); only the header is the state's name.
        Assert.Equal(
            ["Proposed:Новые", "InProgress:В работе", "Resolved:Решённые", "Completed:Завершённые"],
            columns.Select(column => $"{column.GetProperty("category").GetProperty("code").GetString()}:{column.GetProperty("category").GetProperty("label").GetString()}"));
        Assert.DoesNotContain(columns, column => column.GetProperty("header").GetString() == column.GetProperty("category").GetProperty("label").GetString());
    }

    // spec § Model's table for «Истории»: what a drop writes per type, "—" being no drop target (BR-M4-18); a story's
    // «Разрешена» and a bug's stand in one column (bug-004), each type's own state under it.
    [Fact]
    public async Task Get_Stories_TargetsAsTheSpecsTable()
    {
        var world = await BoardsTestData.CreateWorldAsync(factory);
        var board = await BoardAsync(world, world.PlatformId);
        var us = world.Items.Types["US"];
        var bug = world.Items.Types["BUG"];

        string Cell(JsonElement column, WorkItemTypeRow type) =>
            BoardsTestData.Target(column, type.Id) is { } state ? type.States.Single(candidate => candidate.Id.ToString() == state).Name : "—";

        Assert.Equal(
            ["Новая: Новая Новая", "Активна: Активна Активна", "Разрешена: Разрешена Разрешена", "Закрыта: Закрыта Закрыта"],
            board.GetProperty("columns").EnumerateArray().Select(column =>
                $"{column.GetProperty("header").GetString()}: {Cell(column, us)} {Cell(column, bug)}"));
        Assert.NotEqual(
            BoardsTestData.Target(BoardsTestData.Column(board, "Разрешена"), us.Id),
            BoardsTestData.Target(BoardsTestData.Column(board, "Разрешена"), bug.Id));
    }

    // bug-004 D-1 · the report: a story in «Разрешена» and a bug in «Разрешена» are cards of one column, in the backlog's order.
    [Fact]
    public async Task Get_AStoryAndABugInRazreshena_AreCardsOfOneColumn()
    {
        var world = await BoardsTestData.CreateWorldAsync(factory);
        var story = await BoardsTestData.CreateAsync(factory, world, "US", "История", world.PlatformAreaId);
        var bug = await BoardsTestData.CreateAsync(factory, world, "BUG", "Баг", world.PlatformAreaId);
        await BoardsTestData.SetStateAsync(factory, world, story, "US", "Разрешена");
        await BoardsTestData.SetStateAsync(factory, world, bug, "BUG", "Разрешена");

        var board = await BoardAsync(world, world.PlatformId);

        Assert.Equal([story.Identifier, bug.Identifier], BoardsTestData.Cards(BoardsTestData.Column(board, "Разрешена")));
        Assert.Equal(
            ["Новая:", "Активна:", $"Разрешена: {story.Identifier} {bug.Identifier}", "Закрыта:"],
            BoardsTestData.Columns(board));
    }

    // US-4 · "A card sits in the column its type and state map to".
    [Theory]
    [InlineData("US", "Новая", "stories", "Новая")]
    [InlineData("BUG", "Активна", "stories", "Активна")]
    [InlineData("US", "Разрешена", "stories", "Разрешена")]
    [InlineData("BUG", "Разрешена", "stories", "Разрешена")]
    [InlineData("BUG", "Закрыта", "stories", "Закрыта")]
    [InlineData("EPIC", "Разрешена", "epics", "Разрешена")]
    public async Task Get_Card_SitsInItsColumn(string typeKey, string state, string level, string header)
    {
        var world = await BoardsTestData.CreateWorldAsync(factory);
        var item = await BoardsTestData.CreateAsync(factory, world, typeKey, "Карточка", world.PlatformAreaId);
        await BoardsTestData.SetStateAsync(factory, world, item, typeKey, state);

        var board = await BoardAsync(world, world.PlatformId, level);

        Assert.Equal([item.Identifier], BoardsTestData.Cards(BoardsTestData.Column(board, header)));
        Assert.Single(board.GetProperty("columns").EnumerateArray(), column => BoardsTestData.Cards(column).Length > 0);
    }

    // US-4 · "The board holds the backlog's items in the backlog's order" (BR-M4-15): «Новая» lists them in the backlog's
    // order, not another team's item; after a reorder, in the new order.
    [Fact]
    public async Task Get_Cards_InTheBacklogsOrder_OnlyTheTeams()
    {
        var world = await BoardsTestData.CreateWorldAsync(factory);
        var us3 = await BoardsTestData.CreateAsync(factory, world, "US", "Третья", world.PlatformAreaId);
        var bug5 = await BoardsTestData.CreateAsync(factory, world, "BUG", "Пятый", world.PlatformAreaId);
        var rootStory = await BoardsTestData.CreateAsync(factory, world, "US", "В корне");
        var us9 = await BoardsTestData.CreateAsync(factory, world, "US", "Девятая", world.PlatformAreaId);

        var board = await BoardAsync(world, world.PlatformId);
        Assert.Equal([us3.Identifier, bug5.Identifier, us9.Identifier], BoardsTestData.Cards(BoardsTestData.Column(board, "Новая")));
        Assert.DoesNotContain(BoardsTestData.Columns(board), column => column.Contains(rootStory.Identifier, StringComparison.Ordinal));

        await BoardsTestData.MoveAsync(factory, world, world.PlatformId, BoardsTestData.Top(us9));

        var after = await BoardAsync(world, world.PlatformId);
        Assert.Equal([us9.Identifier, us3.Identifier, bug5.Identifier], BoardsTestData.Cards(BoardsTestData.Column(after, "Новая")));
    }

    // US-4 · "A card shows the item at a glance and opens it": type, identifier, title, state with its category, the
    // assignee's display data.
    [Fact]
    public async Task Get_Card_CarriesWhatTheCardShows()
    {
        var world = await BoardsTestData.CreateWorldAsync(factory);
        var bug = await BoardsTestData.CreateAsync(factory, world, "BUG", "Неверный счётчик", world.PlatformAreaId);
        await BoardsTestData.SetStateAsync(factory, world, bug, "BUG", "Активна");
        var anna = await WorkItemsTestData.AddMemberAsync(factory, world.Items, "Анна", "Смирнова");
        await WorkItemsTestData.SaveAsync(factory, world.Items, bug.Identifier, new { assignee = new { userId = anna } });

        var card = BoardsTestData.Column(await BoardAsync(world, world.PlatformId), "Активна").GetProperty("items")[0];

        Assert.Equal((bug.Identifier, "Неверный счётчик", "bug"), (
            card.GetProperty("identifier").GetString(), card.GetProperty("title").GetString(), card.GetProperty("type").GetProperty("appearance").GetString()));
        Assert.Equal(("Активна", "В работе"), (StateName(card), card.GetProperty("state").GetProperty("category").GetProperty("label").GetString()));
        Assert.Equal("Анна Смирнова", card.GetProperty("assignee").GetProperty("displayName").GetString());

        // mod-016 BR-M1-26 (api-contract § 3.3, US-5 "The reads say whether the assignee is still a member").
        Assert.True(card.GetProperty("assignee").GetProperty("isMember").GetBoolean());
    }

    // US-4 · "A column with no cards stays on the board": all four columns, three with no card.
    [Fact]
    public async Task Get_ColumnsWithoutCards_StayOnTheBoard()
    {
        var world = await BoardsTestData.CreateWorldAsync(factory);
        var us = await BoardsTestData.CreateAsync(factory, world, "US", "Новая", world.PlatformAreaId);

        Assert.Equal(
            [$"Новая: {us.Identifier}", "Активна:", "Разрешена:", "Закрыта:"],
            BoardsTestData.Columns(await BoardAsync(world, world.PlatformId)));
    }

    // US-4 · "A level with no items shows the empty frame": the columns come, with no card in any (the panel draws the frame).
    [Fact]
    public async Task Get_LevelWithoutItems_ColumnsWithoutCards()
    {
        var world = await BoardsTestData.CreateWorldAsync(factory);

        Assert.Equal(["Новая:", "Активна:", "Разрешена:", "Закрыта:"], BoardsTestData.Columns(await BoardAsync(world, world.PlatformId, "epics")));
    }

    // US-4 · "Failure and not found are the backlog's": each address of US-1's not-found outline, `boards` for `backlogs`.
    [Theory]
    [InlineData("team-malformed", "organizations.team.not_found")]
    [InlineData("team-deleted", "organizations.team.not_found")]
    [InlineData("team-of-another-project", "organizations.team.not_found")]
    [InlineData("project-deactivated", "organizations.project.not_found")]
    [InlineData("organization-unknown", "organizations.organization.not_found")]
    [InlineData("level-tasks", "boards.level.not_found")]
    public async Task Get_ContextNotFound_AnswersItsLevelsCode(string part, string code)
    {
        var world = await BoardsTestData.CreateWorldAsync(factory);

        var answer = await OrganizationsApi.SendAsync(
            factory, HttpMethod.Get, await GetBacklogEndpointTests.NotFoundAddressAsync(factory, world, part, "boards"), token: OrganizationsTokens.SignedIn());

        BoardsTestData.AssertRefused(answer, HttpStatusCode.NotFound, code);
    }

    // --- US-5 ------------------------------------------------------------------------------------------------------

    // US-5 · "A move writes the state its column maps for the card's type": a story and a bug both to «Разрешена» — each
    // takes its own type's state, and both stand in the one column; title, assignee and area unchanged, «Изменён» the
    // move's, the backlog place kept.
    [Fact]
    public async Task Move_WritesTheColumnsStateForTheType_NothingElse()
    {
        var world = await BoardsTestData.CreateWorldAsync(factory);
        var us2 = await BoardsTestData.CreateAsync(factory, world, "US", "Вторая", world.PlatformAreaId);
        var bug5 = await BoardsTestData.CreateAsync(factory, world, "BUG", "Пятый", world.PlatformAreaId);
        await BoardsTestData.SetStateAsync(factory, world, us2, "US", "Активна");
        await BoardsTestData.SetStateAsync(factory, world, bug5, "BUG", "Активна");
        await BoardsTestData.ReorderToAsync(factory, world, world.PlatformId, bug5, us2);
        var before = await WorkItemsTestData.ReadAsync(factory, world.Items, us2.Identifier);

        var story = await DropAsync(world, us2, "US", "Разрешена");
        var bug = await DropAsync(world, bug5, "BUG", "Разрешена");

        Assert.Equal((HttpStatusCode.OK, HttpStatusCode.OK), (story.Status, bug.Status));
        Assert.Equal(("Разрешена", "Разрешена"), (StateName(story.Body), StateName(bug.Body)));
        Assert.Equal(
            ("Resolved", "Resolved"),
            (story.Body.GetProperty("state").GetProperty("category").GetProperty("code").GetString(), bug.Body.GetProperty("state").GetProperty("category").GetProperty("code").GetString()));
        Assert.Equal(
            (before.GetProperty("title").GetString(), before.GetProperty("assignee").ValueKind, before.GetProperty("area").GetProperty("id").GetGuid()),
            (story.Body.GetProperty("title").GetString(), story.Body.GetProperty("assignee").ValueKind, story.Body.GetProperty("area").GetProperty("id").GetGuid()));
        Assert.True(story.Body.GetProperty("changedAt").GetDateTimeOffset() > before.GetProperty("changedAt").GetDateTimeOffset());

        var board = await BoardAsync(world, world.PlatformId);
        Assert.Equal([bug5.Identifier, us2.Identifier], BoardsTestData.Cards(BoardsTestData.Column(board, "Разрешена")));
        Assert.Equal([bug5.Identifier, us2.Identifier], await BoardsTestData.RowsAsync(factory, world, world.PlatformId));
    }

    // US-5 · BR-M4-18 · "A request for a state of another type is refused": «Разрешена» is one column that maps both types,
    // but each type's own state under it; the story's «Разрешена» sent directly for the bug is refused, the bug unchanged.
    [Fact]
    public async Task Move_ToAStateOfAnotherType_IsRefused()
    {
        var world = await BoardsTestData.CreateWorldAsync(factory);
        var bug5 = await BoardsTestData.CreateAsync(factory, world, "BUG", "Пятый", world.PlatformAreaId);
        var board = await BoardAsync(world, world.PlatformId);

        var storysResolved = BoardsTestData.Target(BoardsTestData.Column(board, "Разрешена"), world.TypeId("US"));
        Assert.NotNull(storysResolved);
        Assert.NotNull(BoardsTestData.Target(BoardsTestData.Column(board, "Разрешена"), world.TypeId("BUG")));
        var answer = await WorkItemsTestData.SaveAsync(factory, world.Items, bug5.Identifier, new { stateId = storysResolved });

        BoardsTestData.AssertRefused(answer, HttpStatusCode.Conflict, "workitems.workitem.state_not_allowed");
        Assert.Equal("Этот статус недоступен для рабочего элемента этого типа.", answer.Detail);
        Assert.Equal("Новая", StateName(await WorkItemsTestData.ReadAsync(factory, world.Items, bug5.Identifier)));
    }

    // US-5 · "The card's menu moves it by keyboard": a bug in «Новая» is offered «Активна», «Разрешена», «Закрыта» —
    // the other columns that map a state of its type — and «Разрешена» puts it in «Разрешена».
    [Fact]
    public async Task Move_CardMenu_OffersTheOtherColumnsOfItsType()
    {
        var world = await BoardsTestData.CreateWorldAsync(factory);
        var bug5 = await BoardsTestData.CreateAsync(factory, world, "BUG", "Пятый", world.PlatformAreaId);
        var board = await BoardAsync(world, world.PlatformId);

        var offered = board.GetProperty("columns").EnumerateArray()
            .Where(column => BoardsTestData.Target(column, world.TypeId("BUG")) is not null && !BoardsTestData.Cards(column).Contains(bug5.Identifier))
            .Select(column => column.GetProperty("header").GetString());
        Assert.Equal(["Активна", "Разрешена", "Закрыта"], offered);

        var moved = await DropAsync(world, bug5, "BUG", "Разрешена");

        Assert.Equal("Разрешена", StateName(moved.Body));
        Assert.Equal([bug5.Identifier], BoardsTestData.Cards(BoardsTestData.Column(await BoardAsync(world, world.PlatformId), "Разрешена")));
    }

    // US-5 · "The card lands at its backlog place in the new column".
    [Fact]
    public async Task Move_CardLandsAtItsBacklogPlace()
    {
        var world = await BoardsTestData.CreateWorldAsync(factory);
        var us3 = await BoardsTestData.CreateAsync(factory, world, "US", "Третья", world.PlatformAreaId);
        var us9 = await BoardsTestData.CreateAsync(factory, world, "US", "Девятая", world.PlatformAreaId);
        var us11 = await BoardsTestData.CreateAsync(factory, world, "US", "Одиннадцатая", world.PlatformAreaId);
        await BoardsTestData.SetStateAsync(factory, world, us3, "US", "Активна");
        await BoardsTestData.SetStateAsync(factory, world, us11, "US", "Активна");

        await DropAsync(world, us9, "US", "Активна");

        Assert.Equal(
            [us3.Identifier, us9.Identifier, us11.Identifier],
            BoardsTestData.Cards(BoardsTestData.Column(await BoardAsync(world, world.PlatformId), "Активна")));
    }

    // US-5 · "Two users move the same card, the later decides": no token, no notice (BR-M4-3).
    [Fact]
    public async Task Move_SameCardByTwoUsers_TheLaterDecides()
    {
        var world = await BoardsTestData.CreateWorldAsync(factory);
        var us2 = await BoardsTestData.CreateAsync(factory, world, "US", "Вторая", world.PlatformAreaId);

        var anna = await DropAsync(world, us2, "US", "Закрыта");
        var boris = await DropAsync(world, us2, "US", "Активна");

        Assert.Equal((HttpStatusCode.OK, HttpStatusCode.OK), (anna.Status, boris.Status));
        Assert.Equal("Активна", StateName(await WorkItemsTestData.ReadAsync(factory, world.Items, us2.Identifier)));
    }

    // US-5 · "A form opened before the move keeps the moved state" (mod-003 BR-M3-25): Анна's save of the title alone leaves
    // the state Борис's move wrote; her later save of the state sets it.
    [Fact]
    public async Task Move_ThenAnOlderFormSavesOnlyItsField_TheMovedStateStays()
    {
        var world = await BoardsTestData.CreateWorldAsync(factory);
        var us2 = await BoardsTestData.CreateAsync(factory, world, "US", "Вторая", world.PlatformAreaId);
        // Анна opens the form while it is «Новая»: her later save sends only what she changed (BR-M3-25).
        Assert.Equal("Новая", StateName(await WorkItemsTestData.ReadAsync(factory, world.Items, us2.Identifier)));

        await DropAsync(world, us2, "US", "Разрешена");
        var title = await WorkItemsTestData.SaveAsync(factory, world.Items, us2.Identifier, new { title = "Вторая, переименованная" });

        Assert.Equal("Разрешена", StateName(title.Body));

        var state = await BoardsTestData.SetStateAsync(factory, world, us2, "US", "Активна");
        Assert.Equal("Активна", StateName(state.Body));
    }

    // --- US-7, the board's scenario ------------------------------------------------------------------------------------

    // US-7 · "A state set on the form moves the card": on the next read the card is in «Разрешена», and the backlog row
    // shows «Разрешена» with «Порядок» 1.
    [Fact]
    public async Task Get_StateSetOnTheForm_MovesTheCard()
    {
        var world = await BoardsTestData.CreateWorldAsync(factory);
        var us3 = await BoardsTestData.CreateAsync(factory, world, "US", "Третья", world.PlatformAreaId);
        var us9 = await BoardsTestData.CreateAsync(factory, world, "US", "Девятая", world.PlatformAreaId);
        await BoardsTestData.MoveAsync(factory, world, world.PlatformId, BoardsTestData.Top(us9));

        await BoardsTestData.SetStateAsync(factory, world, us9, "US", "Разрешена");

        Assert.Equal([us9.Identifier], BoardsTestData.Cards(BoardsTestData.Column(await BoardAsync(world, world.PlatformId), "Разрешена")));
        var row = (await BoardsTestData.GetBacklogAsync(factory, world, world.PlatformId)).Body.GetProperty("items")[0];
        Assert.Equal((us9.Identifier, 1, "Разрешена"), (row.GetProperty("identifier").GetString(), row.GetProperty("order").GetInt32(), StateName(row)));
        Assert.Equal([us9.Identifier, us3.Identifier], await BoardsTestData.RowsAsync(factory, world, world.PlatformId));
    }
}
