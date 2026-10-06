using System.Diagnostics;
using System.Net;
using Mustask.Api.IntegrationTests.Features.Organizations;
using Mustask.Api.IntegrationTests.Features.WorkItems;
using Xunit.Abstractions;

namespace Mustask.Api.IntegrationTests.Features.Boards;

/// <summary>
/// feat-001 spec § Non-functional requirements, "Volumes and speed", and US-4 ·
/// "A large backlog stays fast": with 5 000 items on each of «Платформа»'s three
/// levels, every feature under an epic and every story under a feature, the
/// «Эпики» backlog with its whole tree is served within 2 s — so expanding any
/// epic needs no further request — and a picker search over the project's items,
/// a parent write and a form save with a parent within 1 s each. Measured as the
/// panel sends them, after one warm-up call of each. The items are inserted with
/// one statement per level: through the endpoints the setup alone would take
/// minutes.
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
    public async Task Epics_FiveThousandPerLevelAllLinked_TreeWithin2s_SearchWriteAndSaveWithin1s()
    {
        var world = await BoardsTestData.CreateWorldAsync(factory);
        var (epic, feature, story) = (world.Items.Types["EPIC"], world.Items.Types["FEAT"], world.Items.Types["US"]);

        // Epics 1..5 000; feature n + 5 000 under epic n; story n + 10 000 under feature n + 5 000.
        await WorkItemsTestData.ExecuteAsync(factory, $"""
            INSERT INTO work_items (id, project_id, number, type_id, state_id, area_id, title, description, creation_request_id, created_at, created_by, changed_at)
            SELECT gen_random_uuid(), {world.ProjectId}, n, {epic.Id}, {epic.StateId("Новая")}, {world.PlatformAreaId},
                   'Эпик ' || n, '', gen_random_uuid(), now(), 'volume-test', now()
            FROM generate_series(1, {PerLevel}) AS n
            """);
        await WorkItemsTestData.ExecuteAsync(factory, $"""
            INSERT INTO work_items (id, project_id, number, type_id, state_id, area_id, title, description, creation_request_id, created_at, created_by, changed_at, parent_id)
            SELECT gen_random_uuid(), {world.ProjectId}, n, {feature.Id}, {feature.StateId("Новая")}, {world.PlatformAreaId},
                   'Фича ' || n, '', gen_random_uuid(), now(), 'volume-test', now(), parent.id
            FROM generate_series({PerLevel + 1}, {2 * PerLevel}) AS n
            JOIN work_items AS parent ON parent.project_id = {world.ProjectId} AND parent.number = n - {PerLevel}
            """);
        await WorkItemsTestData.ExecuteAsync(factory, $"""
            INSERT INTO work_items (id, project_id, number, type_id, state_id, area_id, title, description, creation_request_id, created_at, created_by, changed_at, parent_id)
            SELECT gen_random_uuid(), {world.ProjectId}, n, {story.Id}, {story.StateId("Новая")}, {world.PlatformAreaId},
                   'История ' || n, '', gen_random_uuid(), now(), 'volume-test', now(), parent.id
            FROM generate_series({2 * PerLevel + 1}, {3 * PerLevel}) AS n
            JOIN work_items AS parent ON parent.project_id = {world.ProjectId} AND parent.number = n - {PerLevel}
            """);
        await WorkItemsTestData.ExecuteAsync(factory, $"""
            INSERT INTO work_item_counters (project_id, last_number) VALUES ({world.ProjectId}, {3 * PerLevel})
            ON CONFLICT (project_id) DO UPDATE SET last_number = excluded.last_number
            """);

        // Warm-up, not asserted: a process's first call of an operation also pays JIT, query compilation and connection
        // warm-up, which on a loaded machine alone can eat the budget.
        var warmUp = await BoardsTestData.GetBacklogAsync(factory, world, world.PlatformId, "epics");
        Assert.Equal(HttpStatusCode.OK, warmUp.Status);
        await LinksTestData.CandidatesAsync(factory, world.Items, $"MSK-FEAT-{PerLevel + 1}", "эпик 1");
        await WorkItemsTestData.SaveAsync(factory, world.Items, $"MSK-FEAT-{PerLevel + 2}", new { description = "Прогрев" });

        var read = await TimedAsync("«Эпики» with its tree", () => BoardsTestData.GetBacklogAsync(factory, world, world.PlatformId, "epics"));
        var search = await TimedAsync("picker search", () => LinksTestData.CandidatesAsync(factory, world.Items, $"MSK-FEAT-{PerLevel + 1}", "эпик 4999"));
        var epic2 = await IdOfAsync(world, "MSK-EPIC-2");
        var epic1 = await IdOfAsync(world, "MSK-EPIC-1");
        var write = await TimedAsync("parent write", () =>
            LinksTestData.SetParentAsync(factory, world.Items, $"MSK-FEAT-{PerLevel + 1}", epic2, epic1));
        var save = await TimedAsync("form save with a parent", () => WorkItemsTestData.SaveAsync(
            factory, world.Items, $"MSK-FEAT-{PerLevel + 2}", new { title = "Фича с родителем", parent = new { parentId = epic1.ToString(), seenParentId = epic2.ToString() } }));

        Assert.Equal(HttpStatusCode.OK, read.Answer.Status);
        var rows = read.Answer.Body.GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(PerLevel, rows.Count);
        Assert.All(rows, row => Assert.Equal(1, row.GetProperty("children").GetArrayLength()));
        Assert.Equal(PerLevel, rows.Sum(row => row.GetProperty("children")[0].GetProperty("children").GetArrayLength()));
        Assert.Equal(HttpStatusCode.OK, search.Answer.Status);
        Assert.Contains("MSK-EPIC-4999", LinksTestData.Identifiers(search.Answer.Body.GetProperty("items")));
        Assert.Equal((HttpStatusCode.OK, HttpStatusCode.OK), (write.Answer.Status, save.Answer.Status));

        Assert.True(read.Elapsed < ReadBudget, $"The «Эпики» read with its tree took {read.Elapsed}.");
        Assert.True(search.Elapsed < WriteBudget, $"The picker search took {search.Elapsed}.");
        Assert.True(write.Elapsed < WriteBudget, $"The parent write took {write.Elapsed}.");
        Assert.True(save.Elapsed < WriteBudget, $"The form save with a parent took {save.Elapsed}.");
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
