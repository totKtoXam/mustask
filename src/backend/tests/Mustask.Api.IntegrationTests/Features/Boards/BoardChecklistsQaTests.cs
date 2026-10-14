using System.Net;
using System.Text.Json;
using Mustask.Api.IntegrationTests.Features.Organizations;
using Mustask.Api.IntegrationTests.Features.WorkItems;

namespace Mustask.Api.IntegrationTests.Features.Boards;

/// <summary>
/// QA's additions to <see cref="BoardChecklistsEndpointTests"/> (feat-032): the cases the developer's set leaves open —
/// checklists of two cards never mix, a removed test case is outside its parent's checklist (BR-C-3 for every kind),
/// and a process that has a `testcase` type is readable through the process definition and does not break the
/// «Истории» board that lists it as a child (api-contract § 4).
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class BoardChecklistsQaTests(MustaskApiFactory factory)
{
    private static JsonElement[] Cards(JsonElement board) =>
        board.GetProperty("columns").EnumerateArray()
            .SelectMany(column => column.GetProperty("items").EnumerateArray())
            .ToArray();

    private static string[] ChildIdentifiers(JsonElement card) =>
        card.GetProperty("checklist").EnumerateArray().Select(child => child.GetProperty("identifier").GetString()!).ToArray();

    private async Task<JsonElement> BoardAsync(BoardsWorld world, string level)
    {
        var answer = await BoardsTestData.GetBoardAsync(factory, world, world.PlatformId, level);
        Assert.Equal(HttpStatusCode.OK, answer.Status);

        return answer.Body;
    }

    // US-1 · "Only task, bug and test-case children are listed" + BR-C-1 (a card's list is its own children): two cards
    // that both have children keep each its own, in each one's order of numbers — the single statement for all the
    // cards groups by parent and does not mix them.
    [Fact]
    public async Task Get_TwoCardsWithChildren_KeepEachItsOwn()
    {
        var world = await BoardsTestData.CreateWorldAsync(factory);
        var first = await BoardsTestData.CreateAsync(factory, world, "US", "Первая", world.PlatformAreaId);
        var second = await BoardsTestData.CreateAsync(factory, world, "US", "Вторая", world.PlatformAreaId);
        var a1 = await BoardsTestData.CreateAsync(factory, world, "TASK", "a1");
        var b1 = await BoardsTestData.CreateAsync(factory, world, "TASK", "b1");
        var a2 = await BoardsTestData.CreateAsync(factory, world, "BUG", "a2", world.PlatformAreaId);
        var b2 = await BoardsTestData.CreateAsync(factory, world, "TASK", "b2");
        await LinksTestData.LinkAsync(factory, world.Items, a1, first);
        await LinksTestData.LinkAsync(factory, world.Items, b1, second);
        await LinksTestData.LinkAsync(factory, world.Items, a2, first);
        await LinksTestData.LinkAsync(factory, world.Items, b2, second);

        var cards = Cards(await BoardAsync(world, "stories"));

        Assert.Equal([a1.Identifier, a2.Identifier], ChildIdentifiers(cards.Single(card => card.GetProperty("identifier").GetString() == first.Identifier)));
        Assert.Equal([b1.Identifier, b2.Identifier], ChildIdentifiers(cards.Single(card => card.GetProperty("identifier").GetString() == second.Identifier)));
    }

    // BR-C-3 for every kind · a test case in the Removed category is in neither the list nor (so) a count; a live one is.
    [Fact]
    public async Task Get_ARemovedTestCase_IsNotInTheChecklist()
    {
        var world = await BoardsTestData.CreateWorldAsync(factory);
        var type = await AddTestCaseTypeAsync(world);
        var story = await BoardsTestData.CreateAsync(factory, world, "US", "История", world.PlatformAreaId);
        var live = await BoardsTestData.CreateAsync(factory, world, "TASK", "жив");
        var gone = await BoardsTestData.CreateAsync(factory, world, "TASK", "удалён");
        await LinksTestData.LinkAsync(factory, world.Items, live, story);
        await LinksTestData.LinkAsync(factory, world.Items, gone, story);
        await WorkItemsTestData.ExecuteAsync(factory, $"UPDATE work_items SET type_id = {type.TypeId}, state_id = {type.Ready} WHERE id = {live.Id}");
        await WorkItemsTestData.ExecuteAsync(factory, $"UPDATE work_items SET type_id = {type.TypeId}, state_id = {type.Removed} WHERE id = {gone.Id}");

        var card = Cards(await BoardAsync(world, "stories")).Single(c => c.GetProperty("identifier").GetString() == story.Identifier);

        Assert.Equal([live.Identifier.Replace("TASK", "TC")], ChildIdentifiers(card));
    }

    // api-contract § 4 · the sixth token is readable through the process definition (the type on the «Задачи» level, which
    // hosts no board: test cases are cards nowhere) and in the board's checklist of the story it is a child of, where the
    // «Истории» board's own columns and level types stay as they were.
    [Fact]
    public async Task ProcessWithATestCaseType_IsReadable_AndTheStoriesBoardStillAnswers()
    {
        var world = await BoardsTestData.CreateWorldAsync(factory);
        var type = await AddTestCaseTypeAsync(world);
        var story = await BoardsTestData.CreateAsync(factory, world, "US", "История", world.PlatformAreaId);
        var testCase = await BoardsTestData.CreateAsync(factory, world, "TASK", "Письмо приходит");
        await LinksTestData.LinkAsync(factory, world.Items, testCase, story);
        await WorkItemsTestData.ExecuteAsync(factory, $"UPDATE work_items SET type_id = {type.TypeId}, state_id = {type.Ready} WHERE id = {testCase.Id}");

        var process = await OrganizationsApi.SendAsync(
            factory, HttpMethod.Get, $"/api/organizations/{world.OrganizationId}/process", token: OrganizationsTokens.SignedIn());
        Assert.Equal(HttpStatusCode.OK, process.Status);
        var types = process.Body.GetProperty("workItemTypes").EnumerateArray().ToArray();
        var tc = types.Single(t => t.GetProperty("key").GetString() == "TC");
        Assert.Equal("testcase", tc.GetProperty("appearance").GetString());
        Assert.Equal(
            types.Single(t => t.GetProperty("key").GetString() == "TASK").GetProperty("backlogLevel").GetProperty("id").GetGuid(),
            tc.GetProperty("backlogLevel").GetProperty("id").GetGuid());

        var board = await BoardAsync(world, "stories");
        Assert.DoesNotContain(board.GetProperty("level").GetProperty("types").EnumerateArray(), t => t.GetProperty("appearance").GetString() == "testcase");
        var child = Assert.Single(Cards(board).Single(c => c.GetProperty("identifier").GetString() == story.Identifier).GetProperty("checklist").EnumerateArray());
        Assert.Equal("testcase", child.GetProperty("type").GetProperty("appearance").GetString());
        Assert.DoesNotContain(Cards(board), c => c.GetProperty("type").GetProperty("appearance").GetString() == "testcase");
    }

    /// <summary>The fixture type of BR-C-11 (as in <see cref="BoardChecklistsEndpointTests"/>): `testcase` on the «Задачи» level.</summary>
    private async Task<(Guid TypeId, Guid Ready, Guid Closed, Guid Removed)> AddTestCaseTypeAsync(BoardsWorld world)
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

        return (typeId, ready, closed, removed);
    }
}
