using System.Diagnostics;
using System.Net;
using Mustask.Api.IntegrationTests.Features.WorkItems;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;
using static Mustask.Api.IntegrationTests.Features.WorkItems.OwningTeamTestData;

namespace Mustask.Api.IntegrationTests.Features.Organizations;

/// <summary>
/// mod-017 US-7, "a big deletion" and the NFR Volumes through the real host and a real PostgreSQL: a team that owns 5 000 items of
/// all four levels (and holds a place for some of them) is deleted within 5 s, as the panel sends it after one warm-up call; every
/// item is released to «Без команды» with the time of the operation, none keeps the team, the places are gone, the count the
/// confirmation showed was the number released, and the address answers not found. The items are inserted with one statement per
/// level: through the endpoints the setup alone would take minutes.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class DeleteTeamVolumeTests(MustaskApiFactory factory, ITestOutputHelper output)
{
    private const int Owned = 5_000;
    private static readonly TimeSpan Budget = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task Delete_5000OwnedItems_Within5Seconds()
    {
        var world = await CreateWorldAsync(factory);
        var story = world.Items.Types["US"];
        await WorkItemsTestData.ExecuteAsync(factory, $"""
            INSERT INTO work_items (id, project_id, number, type_id, state_id, area_id, title, description, creation_request_id, created_at, created_by, changed_at, owning_team_id)
            SELECT gen_random_uuid(), {world.ProjectId}, n, {story.Id}, {story.StateId("Новая")}, {world.Items.RootAreaId},
                   'Объёмная ' || n, '', gen_random_uuid(), now() + n * interval '1 millisecond', 'volume-test', timestamptz '2026-10-01 09:00:00+00', {world.Mars}
            FROM generate_series(1, {Owned}) AS n
            """);
        await WorkItemsTestData.ExecuteAsync(factory, $"""
            INSERT INTO work_item_counters (project_id, last_number) VALUES ({world.ProjectId}, {Owned})
            ON CONFLICT (project_id) DO UPDATE SET last_number = excluded.last_number
            """);
        var level = story.BacklogLevelId;
        await WorkItemsTestData.ExecuteAsync(factory, $"""
            INSERT INTO backlog_orders (team_id, backlog_level_id) VALUES ({world.Mars}, {level}) ON CONFLICT DO NOTHING
            """);
        await WorkItemsTestData.ExecuteAsync(factory, $"""
            INSERT INTO backlog_places (work_item_id, team_id, backlog_level_id, position)
            SELECT item.id, {world.Mars}, {level}, item.number FROM work_items AS item WHERE item.project_id = {world.ProjectId} AND item.number <= 1000
            """);
        var teamRoute = $"{OrganizationsApi.Teams(world.OrganizationId, world.ProjectId)}/{world.Mars}";

        // A warm-up of the deletion's path on another team, not asserted: a process's first call of an operation also pays JIT,
        // query compilation and connection warm-up.
        Assert.Equal(
            HttpStatusCode.NoContent,
            (await OrganizationsApi.SendAsync(
                factory, HttpMethod.Delete, $"{OrganizationsApi.Teams(world.OrganizationId, world.ProjectId)}/{world.Venus}", token: OrganizationsTokens.SignedIn())).Status);

        var count = await OrganizationsApi.SendAsync(factory, HttpMethod.Get, $"{teamRoute}/owned-items/count", token: OrganizationsTokens.SignedIn());
        var watch = Stopwatch.StartNew();
        var answer = await OrganizationsApi.SendAsync(factory, HttpMethod.Delete, teamRoute, token: OrganizationsTokens.SignedIn());
        watch.Stop();
        output.WriteLine($"deleting a team that owns {Owned} items: {watch.Elapsed.TotalMilliseconds:F0} ms");

        Assert.Equal(HttpStatusCode.OK, count.Status);
        Assert.Equal(Owned, count.Body.GetProperty("count").GetInt32());
        Assert.Equal(HttpStatusCode.NoContent, answer.Status);
        Assert.True(watch.Elapsed < Budget, $"The deletion took {watch.Elapsed}.");

        // Every item is still there, none keeps a team, and every «Изменён» moved to the time of the operation.
        Assert.Equal(
            [$"{Owned}|0|0"],
            await QueryAsync($"""
                SELECT count(*)::text || '|' || (count(*) FILTER (WHERE owning_team_id IS NOT NULL))::text
                       || '|' || (count(*) FILTER (WHERE changed_at = timestamptz '2026-10-01 09:00:00+00'))::text AS "Value"
                FROM work_items WHERE project_id = {world.ProjectId}
                """));
        Assert.Equal(["0"], await QueryAsync($"SELECT count(*)::text AS \"Value\" FROM backlog_places WHERE team_id = {world.Mars}"));
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await OrganizationsApi.SendAsync(factory, HttpMethod.Get, teamRoute, token: OrganizationsTokens.SignedIn())).Status);
    }

    /// <summary>One text column named <c>Value</c> of every row of <paramref name="sql"/>.</summary>
    private async Task<List<string>> QueryAsync(FormattableString sql)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<Mustask.Infrastructure.MustaskDbContext>();

        return await context.Database.SqlQuery<string>(sql).ToListAsync();
    }
}
