using System.Net;
using System.Text.Json;
using Mustask.Api.IntegrationTests.Features.Organizations;
using Mustask.Api.IntegrationTests.Features.UserDirectory;
using Mustask.Api.IntegrationTests.Features.WorkItems;
using Mustask.Domain.Process;

namespace Mustask.Api.IntegrationTests.Features.Boards;

/// <summary>
/// <c>GET …/teams/{teamId}/boards/{level}</c> with each card's checklist (feat-032 api-contract § 1, § 2): the card's
/// direct, live task, bug and test-case children, whatever their Область, in the order of their numbers (US-1); and the
/// existing writes a checkbox and «Добавить задачу» reuse (US-2, US-3), seen through the next read of the board
/// (BR-C-10). The data is US-1's: MSK-US-2 «Повторная отправка письма» with MSK-TASK-3, -4, -6, -7 and MSK-BUG-5, -8,
/// MSK-US-9 with none; the numbers differ from the stories' (the world numbers items as it creates them), the
/// order of creation is theirs.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class BoardChecklistsEndpointTests(MustaskApiFactory factory)
{
    /// <summary>US-1's data: the story, the children created in the stories' order, and the people.</summary>
    private sealed record Data(
        BoardsWorld World,
        Item Story,
        Item EmptyStory,
        Item TaskBe,
        Item TaskFe,
        Item BugCounter,
        Item TaskQa,
        Item TaskRelease,
        Item BugDuplicate,
        string Anna,
        string Boris);

    private async Task<Data> CreateDataAsync()
    {
        var world = await BoardsTestData.CreateWorldAsync(factory);
        var anna = await WorkItemsTestData.AddMemberAsync(factory, world.Items, "Анна", "Смирнова");
        var boris = await WorkItemsTestData.AddMemberAsync(factory, world.Items, "Борис", "Петров");

        var story = await BoardsTestData.CreateAsync(factory, world, "US", "Повторная отправка письма", world.PlatformAreaId);
        var taskBe = await BoardsTestData.CreateAsync(factory, world, "TASK", "be");
        var taskFe = await BoardsTestData.CreateAsync(factory, world, "TASK", "fe");
        var bugCounter = await BoardsTestData.CreateAsync(factory, world, "BUG", "Неверный счётчик", world.PlatformAreaId);
        var taskQa = await BoardsTestData.CreateAsync(factory, world, "TASK", "qa");
        var taskRelease = await BoardsTestData.CreateAsync(factory, world, "TASK", "release");
        var bugDuplicate = await BoardsTestData.CreateAsync(factory, world, "BUG", "Дубль", world.PlatformAreaId);
        var emptyStory = await BoardsTestData.CreateAsync(factory, world, "US", "Без задач", world.PlatformAreaId);

        foreach (var child in new[] { taskBe, taskFe, bugCounter, taskQa, taskRelease, bugDuplicate })
        {
            await LinksTestData.LinkAsync(factory, world.Items, child, story);
        }

        await AssignAsync(world, story, anna);
        await AssignAsync(world, taskBe, anna);
        await AssignAsync(world, taskQa, boris);
        await AssignAsync(world, taskRelease, boris);
        await AssignAsync(world, bugCounter, boris);
        await AssignAsync(world, bugDuplicate, anna);
        await BoardsTestData.SetStateAsync(factory, world, story, "US", "Активна");
        await BoardsTestData.SetStateAsync(factory, world, taskBe, "TASK", "Закрыта");
        await BoardsTestData.SetStateAsync(factory, world, taskQa, "TASK", "Активна");
        await BoardsTestData.SetStateAsync(factory, world, bugCounter, "BUG", "Закрыта");
        await BoardsTestData.SetStateAsync(factory, world, bugDuplicate, "BUG", "Разрешена");
        await LinksTestData.DeleteAsync(factory, world.Items, taskRelease);

        return new Data(world, story, emptyStory, taskBe, taskFe, bugCounter, taskQa, taskRelease, bugDuplicate, anna, boris);
    }

    private async Task AssignAsync(BoardsWorld world, Item item, string userId)
    {
        var answer = await WorkItemsTestData.SaveAsync(factory, world.Items, item.Identifier, new { assignee = new { userId } });
        Assert.Equal(HttpStatusCode.OK, answer.Status);
    }

    private async Task<JsonElement> BoardAsync(BoardsWorld world, string level = "stories", Guid? teamId = null)
    {
        var answer = await BoardsTestData.GetBoardAsync(factory, world, teamId ?? world.PlatformId, level);
        Assert.Equal(HttpStatusCode.OK, answer.Status);

        return answer.Body;
    }

    /// <summary>The card of that identifier, in whichever column it stands.</summary>
    private static JsonElement Card(JsonElement board, string identifier) =>
        board.GetProperty("columns").EnumerateArray()
            .SelectMany(column => column.GetProperty("items").EnumerateArray())
            .Single(card => card.GetProperty("identifier").GetString() == identifier);

    private static JsonElement[] ChildrenOf(JsonElement board, Item card) =>
        Card(board, card.Identifier).GetProperty("checklist").EnumerateArray().ToArray();

    private static string[] Identifiers(IEnumerable<JsonElement> children) =>
        children.Select(child => child.GetProperty("identifier").GetString()!).ToArray();

    private static string StateName(JsonElement child) => child.GetProperty("state").GetProperty("name").GetString()!;

    private static string CategoryOf(JsonElement child) => child.GetProperty("state").GetProperty("category").GetProperty("code").GetString()!;

    // --- US-1 ------------------------------------------------------------------------------------------------------

    // US-1 · "A card shows its counters under its assignee" + "Rows are in the order of the child's number, for every
    // kind" (BR-C-1, BR-C-5): the story's children are its tasks and bugs by number — a bug before a later task, and
    // the bug the bug backlog puts first after the earlier one, since the order is the number's and not the backlog's;
    // the child in «Удалена» is in none (BR-C-3).
    [Fact]
    public async Task Get_Card_CarriesItsTasksAndBugs_ByNumber_NotTheRemoved()
    {
        var data = await CreateDataAsync();
        await BoardsTestData.MoveAsync(factory, data.World, data.World.PlatformId, BoardsTestData.Top(data.BugDuplicate));

        var board = await BoardAsync(data.World);

        Assert.Equal(
            [data.TaskBe.Identifier, data.TaskFe.Identifier, data.BugCounter.Identifier, data.TaskQa.Identifier, data.BugDuplicate.Identifier],
            Identifiers(ChildrenOf(board, data.Story)));
        Assert.DoesNotContain(data.TaskRelease.Identifier, Identifiers(ChildrenOf(board, data.Story)));
        Assert.All(ChildrenOf(board, data.Story), child => Assert.NotEqual("Removed", CategoryOf(child)));
    }

    // US-1 · "A «Разрешена» bug is not done": it is in the answer (the bug total), in the Resolved category — not in
    // Completed, which is what the panel counts as done (BR-C-3); the closed ones are Completed.
    [Fact]
    public async Task Get_Children_CarryTheirStateAndCategory_AResolvedBugIsNotCompleted()
    {
        var data = await CreateDataAsync();

        var children = ChildrenOf(await BoardAsync(data.World), data.Story).ToDictionary(child => child.GetProperty("identifier").GetString()!);

        Assert.Equal(("Закрыта", "Completed"), (StateName(children[data.TaskBe.Identifier]), CategoryOf(children[data.TaskBe.Identifier])));
        Assert.Equal(("Новая", "Proposed"), (StateName(children[data.TaskFe.Identifier]), CategoryOf(children[data.TaskFe.Identifier])));
        Assert.Equal(("Активна", "InProgress"), (StateName(children[data.TaskQa.Identifier]), CategoryOf(children[data.TaskQa.Identifier])));
        Assert.Equal(("Закрыта", "Completed"), (StateName(children[data.BugCounter.Identifier]), CategoryOf(children[data.BugCounter.Identifier])));
        Assert.Equal(("Разрешена", "Resolved"), (StateName(children[data.BugDuplicate.Identifier]), CategoryOf(children[data.BugDuplicate.Identifier])));
        Assert.Equal(
            ("Resolved", "Решённые"),
            (CategoryOf(children[data.BugDuplicate.Identifier]), children[data.BugDuplicate.Identifier].GetProperty("state").GetProperty("category").GetProperty("label").GetString()));
    }

    // US-1 · "A row shows what the reference shows" (BR-C-5, BR-C-6): each child carries its type with the appearance the
    // icon is chosen by, its title, its state and its assignee — the person, «Не назначен» as null; its state's id and the
    // type's id are the ones a tick writes through (api-contract § 3).
    [Fact]
    public async Task Get_Child_CarriesWhatARowShows()
    {
        var data = await CreateDataAsync();

        var children = ChildrenOf(await BoardAsync(data.World), data.Story).ToDictionary(child => child.GetProperty("identifier").GetString()!);
        var be = children[data.TaskBe.Identifier];
        var fe = children[data.TaskFe.Identifier];
        var bug = children[data.BugCounter.Identifier];

        Assert.Equal(("be", data.TaskBe.Id), (be.GetProperty("title").GetString(), be.GetProperty("id").GetGuid()));
        Assert.Equal(
            (data.World.TypeId("TASK"), "Задача", "task"),
            (be.GetProperty("type").GetProperty("id").GetGuid(), be.GetProperty("type").GetProperty("name").GetString(), be.GetProperty("type").GetProperty("appearance").GetString()));
        Assert.Equal(("Баг", "bug"), (bug.GetProperty("type").GetProperty("name").GetString(), bug.GetProperty("type").GetProperty("appearance").GetString()));
        Assert.Equal(
            (data.Anna, "Анна Смирнова"),
            (be.GetProperty("assignee").GetProperty("id").GetString(), be.GetProperty("assignee").GetProperty("displayName").GetString()));
        Assert.Equal("Борис Петров", bug.GetProperty("assignee").GetProperty("displayName").GetString());
        Assert.Equal(JsonValueKind.Null, fe.GetProperty("assignee").ValueKind);
        Assert.Equal(data.World.Items.Types["TASK"].StateId("Закрыта"), Guid.Parse(be.GetProperty("state").GetProperty("id").GetString()!));
        Assert.Equal(
            ["id", "identifier", "number", "type", "title", "assignee", "state"],
            be.EnumerateObject().Select(property => property.Name));
    }

    // US-1 · "An assignee the directory does not know" (BR-C-6): the id stays, the names are null — the panel reads
    // «Неизвестный пользователь».
    [Fact]
    public async Task Get_Child_AnAssigneeTheDirectoryDoesNotKnow_KeepsItsIdWithoutNames()
    {
        var data = await CreateDataAsync();
        var ghost = $"unknown-user-{Guid.NewGuid()}";
        await WorkItemsTestData.ExecuteAsync(factory, $"UPDATE work_items SET assignee_id = {ghost} WHERE id = {data.TaskQa.Id}");

        var qa = ChildrenOf(await BoardAsync(data.World), data.Story).Single(child => child.GetProperty("identifier").GetString() == data.TaskQa.Identifier);

        var assignee = qa.GetProperty("assignee");
        Assert.Equal(ghost, assignee.GetProperty("id").GetString());
        Assert.Equal((JsonValueKind.Null, JsonValueKind.Null), (assignee.GetProperty("displayName").ValueKind, assignee.GetProperty("email").ValueKind));
    }

    // US-1 · "The assignee is on the card": the card keeps its own assignee, unchanged (BR-C-6, mod-006 BR-M4-14).
    [Fact]
    public async Task Get_Card_KeepsItsAssignee()
    {
        var data = await CreateDataAsync();

        var board = await BoardAsync(data.World);

        Assert.Equal("Анна Смирнова", Card(board, data.Story.Identifier).GetProperty("assignee").GetProperty("displayName").GetString());
        Assert.Equal(JsonValueKind.Null, Card(board, data.EmptyStory.Identifier).GetProperty("assignee").ValueKind);
    }

    // US-1 · "A story with no children still has its task counter": the answer for a card with none is an empty list,
    // never an absent field (api-contract § 2.1); the counter «0/0» is the panel's reading of it.
    [Fact]
    public async Task Get_CardWithoutChildren_AnswersAnEmptyList()
    {
        var data = await CreateDataAsync();

        var card = Card(await BoardAsync(data.World), data.EmptyStory.Identifier);

        Assert.Equal(JsonValueKind.Array, card.GetProperty("checklist").ValueKind);
        Assert.Equal(0, card.GetProperty("checklist").GetArrayLength());
    }

    // US-1 · "Without a test-case type there is no flask": the seeded process has no type of the `testcase` appearance,
    // so no child of any card is one.
    [Fact]
    public async Task Get_WithoutATestCaseType_NoChildIsATestCase()
    {
        var data = await CreateDataAsync();

        var board = await BoardAsync(data.World);

        Assert.DoesNotContain(
            board.GetProperty("columns").EnumerateArray().SelectMany(column => column.GetProperty("items").EnumerateArray()).SelectMany(card => card.GetProperty("checklist").EnumerateArray()),
            child => child.GetProperty("type").GetProperty("appearance").GetString() == "testcase");
        Assert.DoesNotContain(
            board.GetProperty("level").GetProperty("types").EnumerateArray(),
            type => type.GetProperty("appearance").GetString() == "testcase");
    }

    // US-1 · "Test cases are a count only, whatever their state" (BR-C-1, BR-C-5, BR-C-11): in a fixture process with a
    // type of the appearance `testcase` on the «Задачи» level, two test cases beneath the story — one «Готов», one
    // «Закрыта» — are both in its checklist, after the tasks and bugs by their numbers, with the token as the type's
    // appearance.
    [Fact]
    public async Task Get_TestCases_AreChildren_WhateverTheirState()
    {
        var data = await CreateDataAsync();
        var (typeId, ready, closed) = await AddTestCaseTypeAsync(data.World);
        var first = await BoardsTestData.CreateAsync(factory, data.World, "TASK", "Письмо приходит");
        var second = await BoardsTestData.CreateAsync(factory, data.World, "TASK", "Письмо не дублируется");
        await AssignAsync(data.World, first, data.Anna);
        foreach (var testCase in new[] { first, second })
        {
            await LinksTestData.LinkAsync(factory, data.World.Items, testCase, data.Story);
        }

        await WorkItemsTestData.ExecuteAsync(factory, $"UPDATE work_items SET type_id = {typeId}, state_id = {ready} WHERE id = {first.Id}");
        await WorkItemsTestData.ExecuteAsync(factory, $"UPDATE work_items SET type_id = {typeId}, state_id = {closed} WHERE id = {second.Id}");

        var children = ChildrenOf(await BoardAsync(data.World), data.Story);

        var testCases = children.Where(child => child.GetProperty("type").GetProperty("appearance").GetString() == "testcase").ToArray();
        Assert.Equal(["MSK-TC-9", "MSK-TC-10"], Identifiers(testCases));
        Assert.Equal(("Готов", "Закрыта"), (StateName(testCases[0]), StateName(testCases[1])));
        Assert.Equal(("Completed", "Тест-кейс"), (CategoryOf(testCases[1]), testCases[1].GetProperty("type").GetProperty("name").GetString()));
        Assert.Equal(
            [data.TaskBe.Identifier, data.TaskFe.Identifier, data.BugCounter.Identifier, data.TaskQa.Identifier, data.BugDuplicate.Identifier, "MSK-TC-9", "MSK-TC-10"],
            Identifiers(children));
        Assert.Equal("Анна Смирнова", testCases[0].GetProperty("assignee").GetProperty("displayName").GetString());
    }

    // US-1 · "Only task, bug and test-case children are listed" (BR-C-1): on the «Фичи» board a feature's story child is in
    // no list, its bug child is; the epic's feature children are in none either.
    [Fact]
    public async Task Get_FeatureCard_HoldsItsBugsAndNotItsStories()
    {
        var data = await CreateDataAsync();
        var feature = await BoardsTestData.CreateAsync(factory, data.World, "FEAT", "Вход по ссылке", data.World.PlatformAreaId);
        var epic = await BoardsTestData.CreateAsync(factory, data.World, "EPIC", "Вход и регистрация", data.World.PlatformAreaId);
        var featureBug = await BoardsTestData.CreateAsync(factory, data.World, "BUG", "Ссылка истекает", data.World.PlatformAreaId);
        await LinksTestData.LinkAsync(factory, data.World.Items, data.Story, feature);
        await LinksTestData.LinkAsync(factory, data.World.Items, featureBug, feature);
        await LinksTestData.LinkAsync(factory, data.World.Items, feature, epic);

        Assert.Equal([featureBug.Identifier], Identifiers(ChildrenOf(await BoardAsync(data.World, "features"), feature)));
        Assert.Empty(ChildrenOf(await BoardAsync(data.World, "epics"), epic));
    }

    // US-1 · "Children of another team count" (BR-C-1, D-4 OQ-4): a task whose Область resolves to another team is in its
    // parent's checklist, while the backlog's tree beneath the story shows no row for it (feat-027 BR-T-4).
    [Fact]
    public async Task Get_ChildOfAnotherTeam_IsInTheChecklist_NotInTheBacklogsTree()
    {
        var data = await CreateDataAsync();
        Assert.Equal(data.World.RootAreaId, (await WorkItemsTestData.ReadAsync(factory, data.World.Items, data.TaskFe.Identifier)).GetProperty("area").GetProperty("id").GetGuid());

        var checklist = Identifiers(ChildrenOf(await BoardAsync(data.World), data.Story));
        var tree = await BacklogTreeTestData.TopRowsAsync(factory, data.World, data.World.PlatformId, "stories");

        Assert.Contains(data.TaskFe.Identifier, checklist);
        Assert.DoesNotContain(data.TaskFe.Identifier, Identifiers(tree.Single(row => row.GetProperty("identifier").GetString() == data.Story.Identifier).GetProperty("children").EnumerateArray()));
    }

    // US-1 · "A child bug is still its own card": the bug is in its story's checklist and a card of the board, in its own
    // column; the card's own children are its own.
    [Fact]
    public async Task Get_ChildBug_IsAlsoACardOfItsOwn()
    {
        var data = await CreateDataAsync();

        var board = await BoardAsync(data.World);

        Assert.Contains(data.BugCounter.Identifier, Identifiers(ChildrenOf(board, data.Story)));
        Assert.Contains(data.BugCounter.Identifier, BoardsTestData.Cards(BoardsTestData.Column(board, "Закрыта")));
        Assert.Contains(data.BugDuplicate.Identifier, BoardsTestData.Cards(BoardsTestData.Column(board, "Разрешена")));
        Assert.Empty(ChildrenOf(board, data.BugCounter));
    }

    // US-1 · "Only task, bug and test-case children are listed" (BR-C-1): the grandchildren are not in it — a task beneath a
    // bug is in the bug card's checklist and not in its story's.
    [Fact]
    public async Task Get_GrandchildIsNotInTheStorysChecklist()
    {
        var data = await CreateDataAsync();
        var grandchild = await BoardsTestData.CreateAsync(factory, data.World, "TASK", "Внучатая");
        await LinksTestData.LinkAsync(factory, data.World.Items, grandchild, data.BugCounter);

        var board = await BoardAsync(data.World);

        Assert.DoesNotContain(grandchild.Identifier, Identifiers(ChildrenOf(board, data.Story)));
        Assert.Equal([grandchild.Identifier], Identifiers(ChildrenOf(board, data.BugCounter)));
    }

    // US-1 · "The board's other behaviour is unchanged" (BR-C-10): the columns, the cards and their order are what they were,
    // and a card is the backlog item's fields plus `checklist`, last.
    [Fact]
    public async Task Get_ColumnsAndCards_AreUnchanged_ACardGainsOnlyChildren()
    {
        var data = await CreateDataAsync();

        var board = await BoardAsync(data.World);

        Assert.Equal(
            ["Новая: " + data.EmptyStory.Identifier, "Активна: " + data.Story.Identifier, "Разрешена: " + data.BugDuplicate.Identifier, "Закрыта: " + data.BugCounter.Identifier],
            BoardsTestData.Columns(board));
        Assert.Equal(
            ["order", "id", "identifier", "number", "type", "title", "assignee", "state", "priority", "team", "checklist"],
            Card(board, data.Story.Identifier).EnumerateObject().Select(property => property.Name));
    }

    // US-1 · "The checklists come with the board's answer" (BR-C-10): one read answers every card's children; a read
    // changes nothing stored.
    [Fact]
    public async Task Get_Board_ChangesNothingStored()
    {
        var data = await CreateDataAsync();
        var before = await WorkItemsTestData.StoredAsync(factory, data.World.ProjectId);

        await BoardAsync(data.World);

        Assert.Equal(before, await WorkItemsTestData.StoredAsync(factory, data.World.ProjectId));
    }

    // review B-3's shape on the checklists: a story with more distinct assignees among its tasks than the directory takes
    // in one read (500) is answered whole, every name resolved, an unknown one keeping its id.
    [Fact]
    public async Task Get_MoreThan500DistinctAssigneesAmongChildren_IsAnsweredWhole()
    {
        const int distinct = 520;
        var world = await BoardsTestData.CreateWorldAsync(factory);
        var story = await BoardsTestData.CreateAsync(factory, world, "US", "История", world.PlatformAreaId);
        var ids = Enumerable.Range(0, distinct).Select(_ => UserDirectoryTestData.NewId()).ToArray();
        await UserDirectoryTestData.SeedAsync(
            factory, ids.Select((id, index) => UserDirectoryTestData.Push(id, $"Имя{index + 1}", $"Фамилия{index + 1}", $"user{index + 1}-{id}@example.test")));
        var unknown = $"unknown-user-{Guid.NewGuid()}";
        await BulkItemsTestData.InsertAssignedAsync(factory, world.Items, world.PlatformAreaId, [.. ids, unknown], firstNumber: 2, beneath: story.Id);

        var children = ChildrenOf(await BoardAsync(world), story);

        Assert.Equal(distinct + 1, children.Length);
        Assert.Equal(
            Enumerable.Range(1, distinct).Select(index => $"Имя{index} Фамилия{index}"),
            children.Take(distinct).Select(child => child.GetProperty("assignee").GetProperty("displayName").GetString()));
        Assert.Equal(JsonValueKind.Null, children[distinct].GetProperty("assignee").GetProperty("displayName").ValueKind);
    }

    // --- US-2: the writes a checkbox reuses, seen at the next read -----------------------------------------------------

    private Task<Answer> SetStateAsync(Data data, Item child, string typeKey, string state) =>
        BoardsTestData.SetStateAsync(factory, data.World, child, typeKey, state);

    // US-2 · "Ticking closes the child" (BR-C-7): mod-003's PATCH with the type's first Completed state — «Изменён»
    // moves, title, assignee and Область stay — and the next read of the board shows the child closed.
    [Fact]
    public async Task Tick_ATask_ClosesIt_NothingElseChanges_TheNextReadShowsIt()
    {
        var data = await CreateDataAsync();
        var before = await WorkItemsTestData.ReadAsync(factory, data.World.Items, data.TaskQa.Identifier);
        var completed = data.World.Items.Types["TASK"].StateId(StateCategory.Completed);

        var answer = await WorkItemsTestData.SaveAsync(factory, data.World.Items, data.TaskQa.Identifier, new { stateId = completed });

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.True(answer.Body.GetProperty("changedAt").GetDateTimeOffset() > before.GetProperty("changedAt").GetDateTimeOffset());
        Assert.Equal(
            (before.GetProperty("title").GetString(), before.GetProperty("assignee").GetProperty("id").GetString(), before.GetProperty("area").GetProperty("id").GetGuid()),
            (answer.Body.GetProperty("title").GetString(), answer.Body.GetProperty("assignee").GetProperty("id").GetString(), answer.Body.GetProperty("area").GetProperty("id").GetGuid()));
        var qa = ChildrenOf(await BoardAsync(data.World), data.Story).Single(child => child.GetProperty("identifier").GetString() == data.TaskQa.Identifier);
        Assert.Equal(("Закрыта", "Completed"), (StateName(qa), CategoryOf(qa)));
    }

    // US-2 · "Unticking reopens the child in its initial state": the closed task goes back to «Новая», the first state of the
    // Proposed category, and the counter's done count falls.
    [Fact]
    public async Task Untick_ATask_ReopensItInItsInitialState()
    {
        var data = await CreateDataAsync();
        var initial = data.World.Items.Types["TASK"].States.Single(state => state.Position == StateDefinition.InitialPosition).Id;

        var answer = await WorkItemsTestData.SaveAsync(factory, data.World.Items, data.TaskBe.Identifier, new { stateId = initial });

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        var children = ChildrenOf(await BoardAsync(data.World), data.Story);
        var be = children.Single(child => child.GetProperty("identifier").GetString() == data.TaskBe.Identifier);
        Assert.Equal(("Новая", "Proposed"), (StateName(be), CategoryOf(be)));
        Assert.Equal(0, children.Count(child => child.GetProperty("type").GetProperty("appearance").GetString() == "task" && CategoryOf(child) == "Completed"));
    }

    // US-2 · "A closed child that is also a card moves column at the read" and "A ticked then unticked «Разрешена» bug ends
    // in «Новая»": the bug's own card stays in «Разрешена» until the board is read again, where it is in «Закрыта»; an
    // untick then writes «Новая» and the card is in «Новая».
    [Fact]
    public async Task Tick_ABugThatIsAlsoACard_MovesItsCardAtTheNextRead()
    {
        var data = await CreateDataAsync();
        Assert.Contains(data.BugDuplicate.Identifier, BoardsTestData.Cards(BoardsTestData.Column(await BoardAsync(data.World), "Разрешена")));

        var ticked = await SetStateAsync(data, data.BugDuplicate, "BUG", "Закрыта");

        Assert.Equal(HttpStatusCode.OK, ticked.Status);
        var closed = await BoardAsync(data.World);
        Assert.Contains(data.BugDuplicate.Identifier, BoardsTestData.Cards(BoardsTestData.Column(closed, "Закрыта")));
        Assert.Equal("Completed", CategoryOf(ChildrenOf(closed, data.Story).Single(child => child.GetProperty("identifier").GetString() == data.BugDuplicate.Identifier)));

        var unticked = await SetStateAsync(data, data.BugDuplicate, "BUG", "Новая");

        Assert.Equal(HttpStatusCode.OK, unticked.Status);
        Assert.Contains(data.BugDuplicate.Identifier, BoardsTestData.Cards(BoardsTestData.Column(await BoardAsync(data.World), "Новая")));
    }

    // US-2 · "The later of two writes decides": no check, no notice — Анна's «Активна» on the form is overwritten by Борис's
    // untick, and the board reads «Новая».
    [Fact]
    public async Task Untick_AfterAnotherUsersWrite_TheLaterDecides()
    {
        var data = await CreateDataAsync();

        var anna = await SetStateAsync(data, data.TaskBe, "TASK", "Активна");
        var boris = await SetStateAsync(data, data.TaskBe, "TASK", "Новая");

        Assert.Equal((HttpStatusCode.OK, HttpStatusCode.OK), (anna.Status, boris.Status));
        Assert.Equal(
            "Новая",
            StateName(ChildrenOf(await BoardAsync(data.World), data.Story).Single(child => child.GetProperty("identifier").GetString() == data.TaskBe.Identifier)));
    }

    // --- US-3: the create beneath a card, seen at the next read ----------------------------------------------------------

    // US-3 · "A task is added with a title" and "The new task takes the parent's area and defaults" (BR-C-9): feat-027's
    // create with the card's item as `parentId` — the task is the last of the list, «Новая», nobody assigned, in the
    // parent's Область; the parent's «Изменён» moved. A replay with the same `requestId` makes no second task.
    [Fact]
    public async Task Create_ATaskBeneathACard_IsTheListsLastRow_InTheParentsArea()
    {
        var data = await CreateDataAsync();
        var parentBefore = await WorkItemsTestData.ReadAsync(factory, data.World.Items, data.Story.Identifier);
        var requestId = Guid.NewGuid();

        var created = await BacklogTreeTestData.CreateChildAsync(factory, data.World, "TASK", "Проверить ссылку", data.Story.Id.ToString(), requestId);
        var replay = await BacklogTreeTestData.CreateChildAsync(factory, data.World, "TASK", "Проверить ссылку", data.Story.Id.ToString(), requestId);

        Assert.Equal(HttpStatusCode.Created, created.Status);
        Assert.Equal(created.Body.GetProperty("identifier").GetString(), replay.Body.GetProperty("identifier").GetString());
        var children = ChildrenOf(await BoardAsync(data.World), data.Story);
        var last = children[^1];
        Assert.Equal((created.Body.GetProperty("identifier").GetString(), "Проверить ссылку", "Новая"), (last.GetProperty("identifier").GetString(), last.GetProperty("title").GetString(), StateName(last)));
        Assert.Equal(JsonValueKind.Null, last.GetProperty("assignee").ValueKind);
        Assert.Equal(6, children.Length);
        var stored = await WorkItemsTestData.ReadAsync(factory, data.World.Items, last.GetProperty("identifier").GetString()!);
        Assert.Equal(
            (data.World.PlatformAreaId, data.Story.Id),
            (stored.GetProperty("area").GetProperty("id").GetGuid(), stored.GetProperty("parent").GetProperty("id").GetGuid()));
        Assert.True((await WorkItemsTestData.ReadAsync(factory, data.World.Items, data.Story.Identifier)).GetProperty("changedAt").GetDateTimeOffset() > parentBefore.GetProperty("changedAt").GetDateTimeOffset());
    }

    // US-3 · "A story with no task can still get one": the story with no children gets its first, and the list holds it.
    [Fact]
    public async Task Create_ATaskBeneathAStoryWithNone_IsItsOnlyChild()
    {
        var data = await CreateDataAsync();

        var created = await BacklogTreeTestData.CreateChildAsync(factory, data.World, "TASK", "Первая задача", data.EmptyStory.Id.ToString());

        Assert.Equal(HttpStatusCode.Created, created.Status);
        Assert.Equal(
            [created.Body.GetProperty("identifier").GetString()!],
            Identifiers(ChildrenOf(await BoardAsync(data.World), data.EmptyStory)));
    }

    // US-3 · "A refusal shows at the field": the card's item removed after the board was read — feat-027's refusal, nothing
    // created, and the card's checklist is unchanged.
    [Fact]
    public async Task Create_BeneathARemovedCard_IsRefused_NothingIsCreated()
    {
        var data = await CreateDataAsync();
        var before = (await WorkItemsTestData.StoredAsync(factory, data.World.ProjectId)).Count;
        await LinksTestData.DeleteAsync(factory, data.World.Items, data.EmptyStory);

        var refused = await BacklogTreeTestData.CreateChildAsync(factory, data.World, "TASK", "Первая задача", data.EmptyStory.Id.ToString());

        BoardsTestData.AssertRefused(refused, HttpStatusCode.Conflict, "workitems.link.item_deleted");
        Assert.Equal(before, (await WorkItemsTestData.StoredAsync(factory, data.World.ProjectId)).Count);
    }

    // --- the fixture process of BR-C-11 ----------------------------------------------------------------------------------

    /// <summary>
    /// A type of the appearance `testcase` on the «Задачи» level of the world's process, keyed TC, with the states «Готов»
    /// (Proposed), «Закрыта» (Completed) and «Удалена» (Removed): feat-031 adds the real one, no process has it before.
    /// </summary>
    private async Task<(Guid TypeId, Guid Ready, Guid Closed)> AddTestCaseTypeAsync(BoardsWorld world)
    {
        var (typeId, ready, closed, removed) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        await WorkItemsTestData.ExecuteAsync(factory, $"""
            INSERT INTO work_item_types (id, process_id, backlog_level_id, key, name, appearance, position)
            SELECT {typeId}, task.process_id, task.backlog_level_id, 'TC', 'Тест-кейс', 'testcase', 99
            FROM work_item_types task
            WHERE task.key = 'TASK' AND task.process_id = (SELECT id FROM processes WHERE organization_id = {world.OrganizationId})
            """);
        await WorkItemsTestData.ExecuteAsync(factory, $"""
            INSERT INTO state_definitions (id, work_item_type_id, name, category, position) VALUES
                ({ready}, {typeId}, 'Готов', 'Proposed', 1),
                ({closed}, {typeId}, 'Закрыта', 'Completed', 2),
                ({removed}, {typeId}, 'Удалена', 'Removed', 3)
            """);

        return (typeId, ready, closed);
    }
}
