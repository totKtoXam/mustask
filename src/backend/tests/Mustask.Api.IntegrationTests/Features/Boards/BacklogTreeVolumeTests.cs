using System.Diagnostics;
using System.Net;
using System.Text.Json;
using Mustask.Api.IntegrationTests.Features.Organizations;
using Mustask.Api.IntegrationTests.Features.WorkItems;
using Xunit.Abstractions;

namespace Mustask.Api.IntegrationTests.Features.Boards;

/// <summary>
/// feat-027 spec § Non-functional requirements, "Volumes and speed", and US-2 ·
/// "A large backlog stays fast" (after feat-001's of three levels): with 5 000
/// items on each of «Платформа»'s four levels, every feature under an epic, every
/// story under a feature and every task under a story, the «Эпики» backlog with
/// its whole tree (20 000 rows) and the «Истории» backlog with its tasks are each
/// served within 2 s — so no expand needs a further request — and a create with
/// a parent, a picker search over the project's items, a parent write and a form
/// save with a parent within 1 s each. Measured as the panel sends them, after
/// one warm-up call of each. The items are inserted with one statement per level:
/// through the endpoints the setup alone would take minutes.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class BacklogTreeVolumeTests(MustaskApiFactory factory, ITestOutputHelper output)
{
    private const int PerLevel = 5_000;

    private static readonly TimeSpan ReadBudget = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan WriteBudget = TimeSpan.FromSeconds(1);

    [Fact]
    public async Task FourLevelsOf5000AllLinked_EpicsAndStoriesTreesWithin2s_CreateSearchWriteAndSaveWithin1s()
    {
        var world = await BoardsTestData.CreateWorldAsync(factory);
        var (epic, feature, story, task) = (world.Items.Types["EPIC"], world.Items.Types["FEAT"], world.Items.Types["US"], world.Items.Types["TASK"]);

        // Epics 1..5 000; feature n + 5 000 under epic n; story n + 10 000 under feature n + 5 000; task n + 15 000 under
        // story n + 10 000.
        await WorkItemsTestData.ExecuteAsync(factory, $"""
            INSERT INTO work_items (id, project_id, number, type_id, state_id, area_id, title, description, creation_request_id, created_at, created_by, changed_at, owning_team_id)
            SELECT gen_random_uuid(), {world.ProjectId}, n, {epic.Id}, {epic.StateId("Новая")}, {world.PlatformAreaId},
                   'Эпик ' || n, '', gen_random_uuid(), now(), 'volume-test', now(), {world.PlatformId}
            FROM generate_series(1, {PerLevel}) AS n
            """);
        await InsertLevelAsync(world, feature.Id, feature.StateId("Новая"), "Фича", from: PerLevel + 1);
        await InsertLevelAsync(world, story.Id, story.StateId("Новая"), "История", from: (2 * PerLevel) + 1);
        await InsertLevelAsync(world, task.Id, task.StateId("Новая"), "Задача", from: (3 * PerLevel) + 1);
        await WorkItemsTestData.ExecuteAsync(factory, $"""
            INSERT INTO work_item_counters (project_id, last_number) VALUES ({world.ProjectId}, {4 * PerLevel})
            ON CONFLICT (project_id) DO UPDATE SET last_number = excluded.last_number
            """);

        // Warm-up, not asserted: a process's first call of an operation also pays JIT, query compilation and connection
        // warm-up, which on a loaded machine alone can eat the budget.
        var warmUp = await BoardsTestData.GetBacklogAsync(factory, world, world.PlatformId, "epics");
        Assert.Equal(HttpStatusCode.OK, warmUp.Status);
        Assert.Equal(HttpStatusCode.OK, (await BoardsTestData.GetBacklogAsync(factory, world, world.PlatformId, "stories")).Status);
        Assert.Equal(HttpStatusCode.Created, (await CreateChildAsync(world, await IdOfAsync(world, "MSK-US-10001"))).Status);
        await LinksTestData.CandidatesAsync(factory, world.Items, $"MSK-FEAT-{PerLevel + 1}", "эпик 1");
        await WorkItemsTestData.SaveAsync(factory, world.Items, $"MSK-FEAT-{PerLevel + 2}", new { description = "Прогрев" });
        var story12345 = await IdOfAsync(world, "MSK-US-12345");

        var epics = await TimedAsync("«Эпики» with its tree", () => BoardsTestData.GetBacklogAsync(factory, world, world.PlatformId, "epics"));
        var stories = await TimedAsync("«Истории» with its tree", () => BoardsTestData.GetBacklogAsync(factory, world, world.PlatformId, "stories"));
        var create = await TimedAsync("create with a parent", () => CreateChildAsync(world, story12345));
        var search = await TimedAsync("picker search", () => LinksTestData.CandidatesAsync(factory, world.Items, $"MSK-FEAT-{PerLevel + 1}", "эпик 4999"));
        var epic2 = await IdOfAsync(world, "MSK-EPIC-2");
        var epic1 = await IdOfAsync(world, "MSK-EPIC-1");
        var write = await TimedAsync("parent write", () =>
            LinksTestData.SetParentAsync(factory, world.Items, $"MSK-FEAT-{PerLevel + 1}", epic2, epic1));
        var save = await TimedAsync("form save with a parent", () => WorkItemsTestData.SaveAsync(
            factory, world.Items, $"MSK-FEAT-{PerLevel + 2}", new { title = "Фича с родителем", parent = new { parentId = epic1.ToString(), seenParentId = epic2.ToString() } }));

        Assert.Equal(HttpStatusCode.OK, epics.Answer.Status);
        var epicRows = epics.Answer.Body.GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(PerLevel, epicRows.Count);
        Assert.All(epicRows, row => Assert.Equal(1, row.GetProperty("children").GetArrayLength()));
        Assert.Equal((4 * PerLevel) + 1, BacklogTreeTestData.AllRows(epicRows).Count());
        Assert.Equal(["MSK-EPIC-1", "MSK-FEAT-5001", "MSK-US-10001", "MSK-TASK-15001"], Chain(epicRows[0]));
        Assert.Equal(HttpStatusCode.OK, stories.Answer.Status);
        var storyRows = stories.Answer.Body.GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(PerLevel, storyRows.Count);
        Assert.Equal(PerLevel + 1, storyRows.Sum(row => row.GetProperty("children").GetArrayLength()));
        Assert.Equal(HttpStatusCode.Created, create.Answer.Status);
        Assert.Equal(HttpStatusCode.OK, search.Answer.Status);
        Assert.Contains("MSK-EPIC-4999", LinksTestData.Identifiers(search.Answer.Body.GetProperty("items")));
        Assert.Equal((HttpStatusCode.OK, HttpStatusCode.OK), (write.Answer.Status, save.Answer.Status));

        Assert.True(epics.Elapsed < ReadBudget, $"The «Эпики» read with its tree took {epics.Elapsed}.");
        Assert.True(stories.Elapsed < ReadBudget, $"The «Истории» read with its tree took {stories.Elapsed}.");
        Assert.True(create.Elapsed < WriteBudget, $"The create with a parent took {create.Elapsed}.");
        Assert.True(search.Elapsed < WriteBudget, $"The picker search took {search.Elapsed}.");
        Assert.True(write.Elapsed < WriteBudget, $"The parent write took {write.Elapsed}.");
        Assert.True(save.Elapsed < WriteBudget, $"The form save with a parent took {save.Elapsed}.");
    }

    /// <summary>The <see cref="PerLevel"/> items numbered from <paramref name="from"/>, item n under item n − <see cref="PerLevel"/>.</summary>
    private Task InsertLevelAsync(BoardsWorld world, Guid typeId, Guid stateId, string title, int from) =>
        WorkItemsTestData.ExecuteAsync(factory, $"""
            INSERT INTO work_items (id, project_id, number, type_id, state_id, area_id, title, description, creation_request_id, created_at, created_by, changed_at, parent_id, owning_team_id)
            SELECT gen_random_uuid(), {world.ProjectId}, n, {typeId}, {stateId}, {world.PlatformAreaId},
                   {title} || ' ' || n, '', gen_random_uuid(), now(), 'volume-test', now(), parent.id, {world.PlatformId}
            FROM generate_series({from}, {from + PerLevel - 1}) AS n
            JOIN work_items AS parent ON parent.project_id = {world.ProjectId} AND parent.number = n - {PerLevel}
            """);

    /// <summary>A task created beneath the story <paramref name="parentId"/>, as a row's «Добавить: Задача» sends it.</summary>
    private Task<Answer> CreateChildAsync(BoardsWorld world, Guid parentId) =>
        BacklogTreeTestData.CreateChildAsync(factory, world, "TASK", "Новая задача", parentId.ToString());

    /// <summary>A row and its first descendants, down to the bottom, by identifier.</summary>
    private static string[] Chain(JsonElement row)
    {
        var chain = new List<string> { row.GetProperty("identifier").GetString()! };

        for (; row.GetProperty("children").GetArrayLength() > 0; row = row.GetProperty("children")[0])
        {
            chain.Add(row.GetProperty("children")[0].GetProperty("identifier").GetString()!);
        }

        return [.. chain];
    }

    private async Task<Guid> IdOfAsync(BoardsWorld world, string identifier) =>
        (await WorkItemsTestData.ReadAsync(factory, world.Items, identifier)).GetProperty("id").GetGuid();

    private async Task<(Answer Answer, TimeSpan Elapsed)> TimedAsync(string what, Func<Task<Answer>> request)
    {
        var watch = Stopwatch.StartNew();
        var answer = await request();
        watch.Stop();
        output.WriteLine($"{what}: {watch.Elapsed.TotalMilliseconds:F0} ms");

        return (answer, watch.Elapsed);
    }
}
