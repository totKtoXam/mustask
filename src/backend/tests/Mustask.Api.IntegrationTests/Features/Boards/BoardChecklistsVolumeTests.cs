using System.Diagnostics;
using System.Net;
using Mustask.Api.IntegrationTests.Features.Organizations;
using Mustask.Api.IntegrationTests.Features.WorkItems;
using Xunit.Abstractions;

namespace Mustask.Api.IntegrationTests.Features.Boards;

/// <summary>
/// feat-032 spec § Non-functional requirements, "Volumes and speed" (BR-C-10): the board of one team and level with
/// 5 000 cards, each with five children, is read within 2 s — the checklists come with the answer, in the board's read
/// and not in one per card. Measured as <see cref="BacklogVolumeTests"/> measures its reads, after one warm-up call.
/// The items are inserted with two statements: through the create endpoint the setup alone would take minutes.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class BoardChecklistsVolumeTests(MustaskApiFactory factory, ITestOutputHelper output)
{
    private const int Stories = 5_000;
    private const int ChildrenPerStory = 5;

    private static readonly TimeSpan ReadBudget = TimeSpan.FromSeconds(2);

    // spec § NFR · "The board of 5 000 cards with five children each, all lists collapsed, is read within 2 s": the 5 000
    // cards carry 25 000 children between them, every one in the answer.
    [Fact]
    public async Task Board_FiveThousandCardsWithFiveChildrenEach_ReadWithin2s()
    {
        var world = await BoardsTestData.CreateWorldAsync(factory);
        var us = world.Items.Types["US"];
        var task = world.Items.Types["TASK"];

        await WorkItemsTestData.ExecuteAsync(factory, $"""
            INSERT INTO work_items (id, project_id, number, type_id, state_id, area_id, title, description, creation_request_id, created_at, created_by, changed_at)
            SELECT gen_random_uuid(), {world.ProjectId}, n, {us.Id}, {us.StateId("Новая")}, {world.PlatformAreaId},
                   'История ' || n, '', gen_random_uuid(), now(), 'volume-test', now()
            FROM generate_series(1, {Stories}) AS n
            """);
        await WorkItemsTestData.ExecuteAsync(factory, $"""
            INSERT INTO work_items (id, project_id, number, type_id, state_id, area_id, title, description, creation_request_id, created_at, created_by, changed_at, parent_id)
            SELECT gen_random_uuid(), {world.ProjectId}, CAST({Stories} + (story.number - 1) * {ChildrenPerStory} + k AS integer), {task.Id}, {task.StateId("Новая")},
                   {world.PlatformAreaId}, 'Задача ' || story.number || '.' || k, '', gen_random_uuid(), now(), 'volume-test', now(), story.id
            FROM work_items story CROSS JOIN generate_series(1, {ChildrenPerStory}) AS k
            WHERE story.project_id = {world.ProjectId} AND story.type_id = {us.Id}
            """);
        await WorkItemsTestData.ExecuteAsync(factory, $"""
            INSERT INTO work_item_counters (project_id, last_number) VALUES ({world.ProjectId}, {Stories + (Stories * ChildrenPerStory)})
            ON CONFLICT (project_id) DO UPDATE SET last_number = excluded.last_number
            """);

        var warmUp = await BoardsTestData.GetBoardAsync(factory, world, world.PlatformId);
        Assert.Equal(HttpStatusCode.OK, warmUp.Status);

        var watch = Stopwatch.StartNew();
        var board = await BoardsTestData.GetBoardAsync(factory, world, world.PlatformId);
        watch.Stop();
        output.WriteLine($"board read with checklists: {watch.Elapsed.TotalMilliseconds:F0} ms");

        Assert.Equal(HttpStatusCode.OK, board.Status);
        Assert.True(watch.Elapsed < ReadBudget, $"The board read took {watch.Elapsed}.");
        var cards = board.Body.GetProperty("columns").EnumerateArray().SelectMany(column => column.GetProperty("items").EnumerateArray()).ToList();
        Assert.Equal(Stories, cards.Count);
        Assert.All(cards, card => Assert.Equal(ChildrenPerStory, card.GetProperty("checklist").GetArrayLength()));
    }
}
