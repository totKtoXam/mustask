using System.Diagnostics;
using System.Net;
using Mustask.Api.IntegrationTests.Features.Organizations;
using Mustask.Api.IntegrationTests.Features.WorkItems;
using Xunit.Abstractions;

namespace Mustask.Api.IntegrationTests.Features.Boards;

/// <summary>
/// feat-039 spec § Non-functional requirements, "Volumes and speed", and the "large" scenarios of US-1..US-3: with 5 000 items
/// on each of the project's four levels, every item linked under an item of the level above and spread over two teams' areas,
/// the project backlog of «Эпики» (20 000 rows with its tree) and of «Истории», and the project board of «Истории», are each
/// served within 2 s, and a reorder of 5 000 stories and a card move within 1 s — measured as the panel sends them, after one
/// warm-up call of each. The items are inserted with one statement per level: through the endpoints the setup alone would take
/// minutes.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class ProjectBacklogVolumeTests(MustaskApiFactory factory, ITestOutputHelper output)
{
    private const int PerLevel = 5_000;

    private static readonly TimeSpan ReadBudget = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan WriteBudget = TimeSpan.FromSeconds(1);

    [Fact]
    public async Task ProjectViews_FourLevelsOf5000_ReadsWithin2s_ReorderAndCardMoveWithin1s()
    {
        var world = await BoardsTestData.CreateWorldAsync(factory);
        var (epic, feature, story, task) = (world.Items.Types["EPIC"], world.Items.Types["FEAT"], world.Items.Types["US"], world.Items.Types["TASK"]);

        // Epics 1..5 000; feature n + 5 000 under epic n; story n + 10 000 under feature n + 5 000; task n + 15 000 under story
        // n + 10 000. Every second item stands in the root area (team «Мастаск»), the others in «Платформа»'s.
        await InsertLevelAsync(world, epic.Id, epic.StateId("Новая"), "Эпик", from: 1, linked: false);
        await InsertLevelAsync(world, feature.Id, feature.StateId("Новая"), "Фича", from: PerLevel + 1, linked: true);
        await InsertLevelAsync(world, story.Id, story.StateId("Новая"), "История", from: (2 * PerLevel) + 1, linked: true);
        await InsertLevelAsync(world, task.Id, task.StateId("Новая"), "Задача", from: (3 * PerLevel) + 1, linked: true);
        await WorkItemsTestData.ExecuteAsync(factory, $"""
            INSERT INTO work_item_counters (project_id, last_number) VALUES ({world.ProjectId}, {4 * PerLevel})
            ON CONFLICT (project_id) DO UPDATE SET last_number = excluded.last_number
            """);

        // Warm-up, not asserted: a process's first call of an operation also pays JIT, query compilation and connection warm-up.
        Assert.Equal(HttpStatusCode.OK, (await ProjectBoardsTestData.GetBacklogAsync(factory, world, "epics")).Status);
        var warmStories = await ProjectBoardsTestData.GetBacklogAsync(factory, world);
        Assert.Equal(HttpStatusCode.OK, warmStories.Status);
        Assert.Equal(HttpStatusCode.OK, (await ProjectBoardsTestData.GetBoardAsync(factory, world)).Status);
        var rows = warmStories.Body.GetProperty("items");
        var last = new Item(rows[PerLevel - 1].GetProperty("id").GetGuid(), rows[PerLevel - 1].GetProperty("identifier").GetString()!);
        var middle = new Item(rows[PerLevel / 2].GetProperty("id").GetGuid(), rows[PerLevel / 2].GetProperty("identifier").GetString()!);
        Assert.Equal(HttpStatusCode.NoContent, (await ProjectBoardsTestData.ReorderAsync(factory, world, BoardsTestData.Bottom(middle))).Status);
        Assert.Equal(HttpStatusCode.OK, (await BoardsTestData.SetStateAsync(factory, world, last, "US", "Активна")).Status);

        var epics = await TimedAsync("«Эпики» with its tree", () => ProjectBoardsTestData.GetBacklogAsync(factory, world, "epics"));
        var stories = await TimedAsync("«Истории» with its tree", () => ProjectBoardsTestData.GetBacklogAsync(factory, world));
        var board = await TimedAsync("«Истории» board", () => ProjectBoardsTestData.GetBoardAsync(factory, world));
        var first = await TimedAsync("first reorder", () => ProjectBoardsTestData.ReorderAsync(factory, world, BoardsTestData.Top(last)));
        var second = await TimedAsync("second reorder", () => ProjectBoardsTestData.ReorderAsync(factory, world, BoardsTestData.After(middle, last)));
        var move = await TimedAsync("card move", () => BoardsTestData.SetStateAsync(factory, world, middle, "US", "Активна"));

        Assert.Equal(HttpStatusCode.OK, epics.Answer.Status);
        var epicRows = epics.Answer.Body.GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(PerLevel, epicRows.Count);
        Assert.All(epicRows, row => Assert.Equal(1, row.GetProperty("children").GetArrayLength()));
        Assert.Equal(4 * PerLevel, BacklogTreeTestData.AllRows(epicRows).Count());
        Assert.Equal(HttpStatusCode.OK, stories.Answer.Status);
        var storyRows = stories.Answer.Body.GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(PerLevel, storyRows.Count);
        Assert.Equal(PerLevel, storyRows.Sum(row => row.GetProperty("children").GetArrayLength()));
        Assert.Equal(HttpStatusCode.OK, board.Answer.Status);
        Assert.Equal(PerLevel, ProjectBoardsTestData.AllCards(board.Answer.Body).Length);
        Assert.Equal((HttpStatusCode.NoContent, HttpStatusCode.NoContent), (first.Answer.Status, second.Answer.Status));
        Assert.Equal(HttpStatusCode.OK, move.Answer.Status);

        Assert.True(epics.Elapsed < ReadBudget, $"The «Эпики» read with its tree took {epics.Elapsed}.");
        Assert.True(stories.Elapsed < ReadBudget, $"The «Истории» read with its tree took {stories.Elapsed}.");
        Assert.True(board.Elapsed < ReadBudget, $"The board read took {board.Elapsed}.");
        Assert.True(first.Elapsed < WriteBudget, $"The first reorder took {first.Elapsed}.");
        Assert.True(second.Elapsed < WriteBudget, $"The second reorder took {second.Elapsed}.");
        Assert.True(move.Elapsed < WriteBudget, $"The card move took {move.Elapsed}.");

        var order = await ProjectBoardsTestData.RowsAsync(factory, world);
        Assert.Equal(PerLevel, order.Length);
        Assert.Equal([last.Identifier, middle.Identifier], order.Take(2));
    }

    /// <summary>The <see cref="PerLevel"/> items numbered from <paramref name="from"/>, item n under item n − <see cref="PerLevel"/> when linked, in alternate areas.</summary>
    private Task InsertLevelAsync(BoardsWorld world, Guid typeId, Guid stateId, string title, int from, bool linked) =>
        WorkItemsTestData.ExecuteAsync(factory, $"""
            INSERT INTO work_items (id, project_id, number, type_id, state_id, area_id, title, description, creation_request_id, created_at, created_by, changed_at, parent_id)
            SELECT gen_random_uuid(), {world.ProjectId}, n, {typeId}, {stateId},
                   CASE WHEN n % 2 = 0 THEN {world.RootAreaId} ELSE {world.PlatformAreaId} END,
                   {title} || ' ' || n, '', gen_random_uuid(), now() + n * interval '1 millisecond', 'volume-test', now(), parent.id
            FROM generate_series({from}, {from + PerLevel - 1}) AS n
            LEFT JOIN work_items AS parent ON parent.project_id = {world.ProjectId} AND parent.number = n - {PerLevel} AND {linked}
            """);

    private async Task<(Answer Answer, TimeSpan Elapsed)> TimedAsync(string what, Func<Task<Answer>> request)
    {
        var watch = Stopwatch.StartNew();
        var answer = await request();
        watch.Stop();
        output.WriteLine($"{what}: {watch.Elapsed.TotalMilliseconds:F0} ms");

        return (answer, watch.Elapsed);
    }
}
