using System.Diagnostics;
using System.Net;
using Mustask.Api.IntegrationTests.Features.WorkItems;
using static Mustask.Api.IntegrationTests.Features.Iterations.IterationsTestData;

namespace Mustask.Api.IntegrationTests.Features.Iterations;

/// <summary>
/// spec § Non-functional requirements (mod-007): a sprint of 5 000 items closes in one operation, and the outcome page reads a
/// range of 20 such sprints within 2 s, measured on the request as the panel sends it after one warm-up call. The items are
/// inserted with one statement: through the endpoints the setup alone would take minutes.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class SprintVolumeTests(MustaskApiFactory factory)
{
    private const int Items = 5_000;
    private const int Range = 20;

    private static readonly TimeSpan ReadBudget = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan CloseBudget = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task Close_FiveThousandItems_AndARangeOfTwentySprintsReadsWithin2s()
    {
        var world = await CreateWorldAsync(factory);
        var us = world.Items.Types["US"];
        var active = await ActiveAsync(factory, world, world.PlatformId, "Крупный", "2026-09-07", "2026-09-18");
        var target = await NewAsync(factory, world, world.PlatformId, "Следующий");

        await WorkItemsTestData.ExecuteAsync(factory, $"""
            INSERT INTO work_items (id, project_id, number, type_id, state_id, area_id, title, description, creation_request_id, created_at, created_by, changed_at, owning_team_id, sprint_id, story_points)
            SELECT gen_random_uuid(), {world.ProjectId}, n, {us.Id},
                   CASE WHEN n % 2 = 0 THEN {us.StateId("Закрыта")} ELSE {us.StateId("Активна")} END,
                   {world.PlatformAreaId}, 'Элемент ' || n, '', gen_random_uuid(), now(), 'volume-test', now(), {world.PlatformId}, {active}, 3
            FROM generate_series(1, {Items}) AS n
            """);

        var watch = Stopwatch.StartNew();
        var closed = await CloseAsync(factory, world, world.PlatformId, active, target);
        watch.Stop();

        Assert.Equal(HttpStatusCode.OK, closed.Status);
        Assert.True(watch.Elapsed < CloseBudget, $"The close of {Items} items took {watch.Elapsed}.");
        var page = await ReadAsync(factory, world, world.PlatformId, active);
        Assert.Equal((Items / 2, 3m * (Items / 2)), (page.Body.GetProperty("itemCount").GetInt32(), page.Body.GetProperty("storyPoints").GetDecimal()));

        // Nineteen more Closed sprints ending before it, each with a copy of the snapshot.
        await WorkItemsTestData.ExecuteAsync(factory, $"""
            INSERT INTO sprints (id, organization_id, team_id, number, name, normalized_name, state, start_date, end_date, created_at, created_by, closed_at, closed_by)
            SELECT gen_random_uuid(), {world.OrganizationId}, {world.PlatformId}, 100 + n, 'S' || n, 'S' || n, 'Closed',
                   DATE '2026-07-01' + n, DATE '2026-07-01' + n, now(), 'seed', now(), 'seed'
            FROM generate_series(1, {Range - 1}) AS n
            """);
        await WorkItemsTestData.ExecuteAsync(factory, $"""
            INSERT INTO sprint_snapshot_items (sprint_id, work_item_id, project_id, type_id, number, level_id, assignee_id, story_points, is_done)
            SELECT sprint.id, gen_random_uuid(), snapshot.project_id, snapshot.type_id, snapshot.number, snapshot.level_id,
                   snapshot.assignee_id, snapshot.story_points, snapshot.is_done
            FROM sprints AS sprint
            CROSS JOIN sprint_snapshot_items AS snapshot
            WHERE sprint.team_id = {world.PlatformId} AND sprint.created_by = 'seed' AND snapshot.sprint_id = {active}
            """);

        // One warm-up of each read, not asserted: the first call also pays JIT and query compilation.
        foreach (var tail in new[] { "/dynamics", "/participants", "/items" })
        {
            Assert.Equal(HttpStatusCode.OK, (await SendAsync(factory, HttpMethod.Get, Sprint(world, world.PlatformId, active) + tail)).Status);
        }

        foreach (var tail in new[] { "/dynamics", "/participants", "/items" })
        {
            var read = Stopwatch.StartNew();
            var answer = await SendAsync(factory, HttpMethod.Get, Sprint(world, world.PlatformId, active) + tail);
            read.Stop();

            Assert.Equal(HttpStatusCode.OK, answer.Status);
            Assert.True(read.Elapsed < ReadBudget, $"{tail} over {Range} sprints of {Items} items took {read.Elapsed}.");
        }

        var dynamics = await ReadAsync(factory, world, world.PlatformId, active, "/dynamics");
        Assert.Equal((Range - 1, false), (dynamics.Body.GetProperty("rangeSprints").GetInt32(), dynamics.Body.GetProperty("truncated").GetBoolean()));
    }
}
