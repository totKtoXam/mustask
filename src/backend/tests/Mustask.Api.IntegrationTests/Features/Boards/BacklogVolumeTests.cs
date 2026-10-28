using System.Diagnostics;
using System.Net;
using Mustask.Api.IntegrationTests.Features.Organizations;
using Mustask.Api.IntegrationTests.Features.WorkItems;
using Xunit.Abstractions;

namespace Mustask.Api.IntegrationTests.Features.Boards;

/// <summary>
/// spec § Non-functional requirements, "Volumes and speed": a backlog or a board
/// of one team and level with 5 000 items is served within 2 s, a reorder within
/// 1 s — measured on the request as the panel sends it, after one warm-up call of each operation,
/// against the real membership query and its index. The level's 5 000 items
/// share their area with 2 000 tasks with no parent: the board must not touch
/// them, and the backlog's tree set (feat-027) reads them but nests none, so no
/// row shows them. The items are inserted with one statement: through the create
/// endpoint the setup alone would take minutes.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class BacklogVolumeTests(MustaskApiFactory factory, ITestOutputHelper output)
{
    private const int Stories = 5_000;
    private const int Tasks = 2_000;

    private static readonly TimeSpan ReadBudget = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan WriteBudget = TimeSpan.FromSeconds(1);

    [Fact]
    public async Task Backlog_FiveThousandItems_ReadWithin2s_ReorderWithin1s()
    {
        var world = await BoardsTestData.CreateWorldAsync(factory);
        var us = world.Items.Types["US"];
        var task = world.Items.Types["TASK"];

        await WorkItemsTestData.ExecuteAsync(factory, $"""
            INSERT INTO work_items (id, project_id, number, type_id, state_id, area_id, title, description, creation_request_id, created_at, created_by, changed_at, owning_team_id)
            SELECT gen_random_uuid(), {world.ProjectId}, n,
                   CASE WHEN n <= {Stories} THEN {us.Id} ELSE {task.Id} END,
                   CASE WHEN n <= {Stories} THEN {us.StateId("Новая")} ELSE {task.StateId("Новая")} END,
                   {world.PlatformAreaId}, 'Элемент ' || n, '', gen_random_uuid(), now(), 'volume-test', now(), {world.PlatformId}
            FROM generate_series(1, {Stories + Tasks}) AS n
            """);
        await WorkItemsTestData.ExecuteAsync(factory, $"""
            INSERT INTO work_item_counters (project_id, last_number) VALUES ({world.ProjectId}, {Stories + Tasks})
            ON CONFLICT (project_id) DO UPDATE SET last_number = excluded.last_number
            """);

        var warmUp = await BoardsTestData.GetBacklogAsync(factory, world, world.PlatformId);
        Assert.Equal(HttpStatusCode.OK, warmUp.Status);
        var items = warmUp.Body.GetProperty("items");
        Assert.Equal(Stories, items.GetArrayLength());
        var last = new Item(items[Stories - 1].GetProperty("id").GetGuid(), items[Stories - 1].GetProperty("identifier").GetString()!);
        var middle = new Item(items[Stories / 2].GetProperty("id").GetGuid(), items[Stories / 2].GetProperty("identifier").GetString()!);

        // Warm-up, not asserted: a process's first call of an operation also pays JIT, query compilation and
        // connection warm-up, which on a loaded machine alone can eat the 1 s. The reads were warmed above
        // (the backlog) and here (the board); the warm-up reorder is the one that places every item, and its
        // time is only printed.
        var boardWarmUp = await BoardsTestData.GetBoardAsync(factory, world, world.PlatformId);
        Assert.Equal(HttpStatusCode.OK, boardWarmUp.Status);
        var reorderWarmUp = await TimedAsync("warm-up reorder (places every item, not asserted)", () =>
            BoardsTestData.ReorderAsync(factory, world, world.PlatformId, BoardsTestData.Bottom(middle)));
        Assert.Equal(HttpStatusCode.NoContent, reorderWarmUp.Answer.Status);

        var read = await TimedAsync("backlog read", () => BoardsTestData.GetBacklogAsync(factory, world, world.PlatformId));
        var firstReorder = await TimedAsync("first reorder", () =>
            BoardsTestData.ReorderAsync(factory, world, world.PlatformId, BoardsTestData.Top(last)));
        var secondReorder = await TimedAsync("second reorder", () =>
            BoardsTestData.ReorderAsync(factory, world, world.PlatformId, BoardsTestData.After(middle, last)));
        var board = await TimedAsync("board read", () => BoardsTestData.GetBoardAsync(factory, world, world.PlatformId));

        Assert.Equal(HttpStatusCode.OK, read.Answer.Status);
        Assert.Equal((HttpStatusCode.NoContent, HttpStatusCode.NoContent), (firstReorder.Answer.Status, secondReorder.Answer.Status));
        Assert.Equal(HttpStatusCode.OK, board.Answer.Status);
        Assert.True(read.Elapsed < ReadBudget, $"The backlog read took {read.Elapsed}.");
        Assert.True(board.Elapsed < ReadBudget, $"The board read took {board.Elapsed}.");
        Assert.True(firstReorder.Elapsed < WriteBudget, $"The first reorder took {firstReorder.Elapsed}.");
        Assert.True(secondReorder.Elapsed < WriteBudget, $"The second reorder took {secondReorder.Elapsed}.");

        var rows = await BoardsTestData.RowsAsync(factory, world, world.PlatformId);
        Assert.Equal(Stories, rows.Length);
        Assert.Equal([last.Identifier, middle.Identifier, "MSK-US-1"], rows.Take(3));
    }

    private async Task<(Answer Answer, TimeSpan Elapsed)> TimedAsync(string what, Func<Task<Answer>> request)
    {
        var watch = Stopwatch.StartNew();
        var answer = await request();
        watch.Stop();
        output.WriteLine($"{what}: {watch.Elapsed.TotalMilliseconds:F0} ms");

        return (answer, watch.Elapsed);
    }
}
