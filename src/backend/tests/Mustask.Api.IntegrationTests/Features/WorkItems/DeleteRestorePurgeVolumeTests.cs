using System.Diagnostics;
using System.Net;
using Mustask.Api.IntegrationTests.Features.Organizations;
using Mustask.Domain.Process;
using Xunit.Abstractions;

namespace Mustask.Api.IntegrationTests.Features.WorkItems;

/// <summary>
/// mod-005 spec § Non-functional requirements, "Speed": Delete and Restore each within 1 s on a project of 5 000 items (one lock, one
/// write); a purge of an item with 5 000 children within 2 s (one hierarchy lock, one ordered locking statement, one set-based
/// <c>UPDATE</c>, one <c>DELETE</c>); the list within 2 s for 5 000 items with 1 000 of them deleted, its count 4 000. Each request is
/// measured after a warm-up of the same path on another item: a process's first call of an operation pays for JIT and compilation.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class DeleteRestorePurgeVolumeTests(MustaskApiFactory factory, ITestOutputHelper output)
{
    private const int Items = 5_000;
    private static readonly TimeSpan DeleteBudget = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan PurgeBudget = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan ListBudget = TimeSpan.FromSeconds(2);

    /// <summary><paramref name="count"/> tasks numbered 1..count straight into the table, the counter set past them.</summary>
    private async Task SeedTasksAsync(WorkItemsWorld world, int count)
    {
        var type = world.Types["TASK"];
        var start = new DateTimeOffset(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);

        await WorkItemsTestData.ExecuteAsync(factory, $"""
            INSERT INTO work_items (id, project_id, number, type_id, state_id, title, description,
                                    creation_request_id, created_at, created_by, changed_at)
            SELECT gen_random_uuid(), {world.ProjectId}, n, {type.Id}, {type.StateId("Новая")}, 'Задача ' || n, '',
                   gen_random_uuid(), {start}, 'seed', {start} + n * interval '1 second'
            FROM generate_series(1, {count}) AS n
            """);
        await WorkItemsTestData.ExecuteAsync(factory, $"""
            INSERT INTO work_item_counters (project_id, last_number) VALUES ({world.ProjectId}, {count})
            ON CONFLICT (project_id) DO UPDATE SET last_number = GREATEST(work_item_counters.last_number, excluded.last_number)
            """);
    }

    private async Task<TimeSpan> TimedAsync(string label, Func<Task<Answer>> request, HttpStatusCode expected)
    {
        var watch = Stopwatch.StartNew();
        var answer = await request();
        watch.Stop();
        output.WriteLine($"{label}: {watch.Elapsed.TotalMilliseconds:F0} ms");

        Assert.True(answer.Status == expected, $"{label} answered {answer.Status} {answer.ErrorCode}.");

        return watch.Elapsed;
    }

    private Task<Answer> Post(WorkItemsWorld world, string identifier, string route) =>
        OrganizationsApi.SendAsync(factory, HttpMethod.Post, $"{WorkItemsTestData.Item(world, identifier)}/{route}", token: OrganizationsTokens.SignedIn());

    // NFR · Delete and Restore of one item of a project of 5 000: each within a second.
    [Fact]
    public async Task Delete_AndRestore_OneItemOf5000_EachWithinOneSecond()
    {
        var world = await WorkItemsTestData.CreateWorldAsync(factory);
        await SeedTasksAsync(world, Items);

        await TimedAsync("warm-up delete", () => Post(world, "MSK-TASK-1", "delete"), HttpStatusCode.OK);
        await TimedAsync("warm-up restore", () => Post(world, "MSK-TASK-1", "restore"), HttpStatusCode.OK);

        var delete = await TimedAsync("delete in 5000", () => Post(world, "MSK-TASK-2500", "delete"), HttpStatusCode.OK);
        var restore = await TimedAsync("restore in 5000", () => Post(world, "MSK-TASK-2500", "restore"), HttpStatusCode.OK);

        Assert.True(delete < DeleteBudget, $"The delete took {delete}.");
        Assert.True(restore < DeleteBudget, $"The restore took {restore}.");
    }

    // NFR · a purge of an epic with 5 000 children: within two seconds; every child is live and unparented afterwards, its
    // «Изменён» moved, and nothing else of it.
    [Fact]
    public async Task Purge_AnEpicWith5000Children_WithinTwoSeconds_EveryChildLiveAndUnparented()
    {
        var world = await WorkItemsTestData.CreateWorldAsync(factory);
        var epic = await WorkItemsTestData.CreateItemAsync(factory, world, "EPIC", "Большой эпик");
        var warm = await WorkItemsTestData.CreateItemAsync(factory, world, "EPIC", "Прогрев");
        var epicId = await IdOfAsync(world, epic);
        var type = world.Types["TASK"];
        var start = new DateTimeOffset(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);

        await WorkItemsTestData.ExecuteAsync(factory, $"""
            INSERT INTO work_items (id, project_id, number, type_id, state_id, title, description,
                                    creation_request_id, created_at, created_by, changed_at, parent_id)
            SELECT gen_random_uuid(), {world.ProjectId}, 100 + n, {type.Id}, {type.StateId("Новая")}, 'Дочерняя ' || n, '',
                   gen_random_uuid(), {start}, 'seed', {start}, {epicId}
            FROM generate_series(1, {Items}) AS n
            """);
        await WorkItemsTestData.ExecuteAsync(factory, $"""
            UPDATE work_item_counters SET last_number = {100 + Items} WHERE project_id = {world.ProjectId}
            """);

        await TimedAsync("warm-up delete", () => Post(world, warm, "delete"), HttpStatusCode.OK);
        await TimedAsync("warm-up purge", () => Post(world, warm, "purge"), HttpStatusCode.NoContent);
        await TimedAsync("delete the epic", () => Post(world, epic, "delete"), HttpStatusCode.OK);

        var purge = await TimedAsync($"purge of an epic with {Items} children", () => Post(world, epic, "purge"), HttpStatusCode.NoContent);

        Assert.True(purge < PurgeBudget, $"The purge took {purge}.");
        var stored = await WorkItemsTestData.StoredAsync(factory, world.ProjectId);
        var children = stored.Where(item => item.Number > 100).ToList();
        Assert.Equal(Items, children.Count);
        Assert.All(children, child => Assert.NotEqual(start, child.ChangedAt));
        Assert.Single(children.Select(child => child.ChangedAt).Distinct());
        Assert.DoesNotContain(stored, item => item.Title == "Большой эпик");
        Assert.All(children, child => Assert.Equal(type.StateId("Новая"), child.StateId));
    }

    private async Task<Guid> IdOfAsync(WorkItemsWorld world, string identifier) =>
        (await WorkItemsTestData.ReadAsync(factory, world, identifier)).GetProperty("id").GetGuid();

    // NFR · "The list stays within two seconds for 5 000 items with deleted ones among them": the first page and later ones, and
    // the count of the live ones.
    [Fact]
    public async Task List_With1000DeletedAmong5000_FirstAndLaterPagesWithinTwoSeconds_CountIs4000()
    {
        var world = await WorkItemsTestData.CreateWorldAsync(factory);
        await SeedTasksAsync(world, Items);
        var removed = world.Types["TASK"].StateId(StateCategory.Removed);
        await WorkItemsTestData.ExecuteAsync(factory, $"UPDATE work_items SET state_id = {removed} WHERE project_id = {world.ProjectId} AND number % 5 = 0");
        await WorkItemsTestData.ListAsync(factory, world); // warm-up

        foreach (var query in new[] { "?page=1", "?page=40", "?page=80" })
        {
            var watch = Stopwatch.StartNew();
            var answer = await WorkItemsTestData.ListAsync(factory, world, query);
            watch.Stop();
            output.WriteLine($"list {query}: {watch.Elapsed.TotalMilliseconds:F0} ms");

            Assert.Equal(HttpStatusCode.OK, answer.Status);
            Assert.Equal(4_000, answer.Body.GetProperty("count").GetInt32());
            Assert.True(watch.Elapsed < ListBudget, $"{query} took {watch.Elapsed}.");
        }
    }
}
