using System.Diagnostics;
using System.Net;
using Mustask.Api.IntegrationTests.Features.WorkItems;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;
using static Mustask.Api.IntegrationTests.Features.WorkItems.OwningTeamTestData;

namespace Mustask.Api.IntegrationTests.Features.Organizations;

/// <summary>
/// feat-028 US-4, "5 000 items" and the NFR Volumes through the real host and a real PostgreSQL: a team that owns 5 000 stories of the
/// project (and holds a place for 1 000 of them) is taken off the project within 5 s, as the panel sends it after one warm-up call; every
/// story goes to the destination with the time of the operation and no place on a team backlog, the count the dialog showed was the number
/// moved, the team's 200 stories in <b>another</b> project are not touched, and the link is gone. The stories are inserted with one
/// statement: through the endpoints the setup alone would take minutes.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class UnlinkTeamVolumeTests(MustaskApiFactory factory, ITestOutputHelper output)
{
    private const int Owned = 5_000;
    private const int Elsewhere = 200;
    private static readonly TimeSpan Budget = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task Unlink_5000Items_Within5Seconds_AllMoved_OtherProjectUntouched()
    {
        var world = await CreateWorldAsync(factory);
        await OrganizationsTestData.LinkAsync(factory, world.OtherProjectId, world.Mars);
        var story = world.Items.Types["US"];
        var level = story.BacklogLevelId;
        await SeedAsync(world, world.ProjectId, Owned);
        await SeedAsync(world, world.OtherProjectId, Elsewhere);
        await WorkItemsTestData.ExecuteAsync(factory, $"""
            INSERT INTO backlog_orders (team_id, backlog_level_id) VALUES ({world.Mars}, {level}) ON CONFLICT DO NOTHING
            """);
        await WorkItemsTestData.ExecuteAsync(factory, $"""
            INSERT INTO backlog_places (work_item_id, team_id, backlog_level_id, position)
            SELECT item.id, {world.Mars}, {level}, item.number + CASE WHEN item.project_id = {world.OtherProjectId} THEN 10000 ELSE 0 END FROM work_items AS item
            WHERE item.project_id IN ({world.ProjectId}, {world.OtherProjectId}) AND item.number <= 1000
            """);
        await WorkItemsTestData.ExecuteAsync(factory, $"UPDATE projects SET default_team_id = {world.Mars} WHERE id = {world.ProjectId}");
        var route = ProjectTeamRoute(world, world.Mars);

        // A warm-up of the unlink's path on another team, not asserted: a process's first call of an operation also pays JIT, query
        // compilation and connection warm-up.
        Assert.Equal(
            HttpStatusCode.NoContent,
            (await TeamTestData.UnlinkAsync(factory, world.OrganizationId, world.ProjectId, world.Venus, TeamTestData.To(null))).Status);

        var count = await OrganizationsApi.SendAsync(factory, HttpMethod.Get, $"{route}/unlink-count", token: OrganizationsTokens.SignedIn());
        var watch = Stopwatch.StartNew();
        var answer = await TeamTestData.UnlinkAsync(factory, world.OrganizationId, world.ProjectId, world.Mars, TeamTestData.To(world.OwnTeam));
        watch.Stop();
        output.WriteLine($"taking off a team that owns {Owned} items: {watch.Elapsed.TotalMilliseconds:F0} ms");

        Assert.Equal(HttpStatusCode.OK, count.Status);
        Assert.Equal(Owned, count.Body.GetProperty("count").GetInt32());
        Assert.Equal(HttpStatusCode.NoContent, answer.Status);
        Assert.True(watch.Elapsed < Budget, $"The unlink took {watch.Elapsed}.");

        // Every story is still there, all are the destination's, none keeps MARS, and none keeps its «Изменён».
        Assert.Equal(
            [$"{Owned}|{Owned}|0|0"],
            await QueryAsync($"""
                SELECT count(*)::text || '|' || (count(*) FILTER (WHERE owning_team_id = {world.OwnTeam}))::text
                       || '|' || (count(*) FILTER (WHERE owning_team_id = {world.Mars}))::text
                       || '|' || (count(*) FILTER (WHERE changed_at = timestamptz '2026-10-01 09:00:00+00'))::text AS "Value"
                FROM work_items WHERE project_id = {world.ProjectId}
                """));
        Assert.Equal(
            ["0"],
            await QueryAsync($"""
                SELECT count(*)::text AS "Value" FROM backlog_places AS place JOIN work_items AS item ON item.id = place.work_item_id
                WHERE place.team_id = {world.Mars} AND item.project_id = {world.ProjectId}
                """));

        // The other project: its stories are MARS's still, with the same changed_at and the places, and MARS is linked to it.
        Assert.Equal(
            [$"{Elsewhere}|{Elsewhere}|{Elsewhere}"],
            await QueryAsync($"""
                SELECT count(*)::text || '|' || (count(*) FILTER (WHERE owning_team_id = {world.Mars}))::text
                       || '|' || (count(*) FILTER (WHERE changed_at = timestamptz '2026-10-01 09:00:00+00'))::text AS "Value"
                FROM work_items WHERE project_id = {world.OtherProjectId}
                """));
        Assert.Equal(
            ["200"],
            await QueryAsync($"""
                SELECT count(*)::text AS "Value" FROM backlog_places AS place JOIN work_items AS item ON item.id = place.work_item_id
                WHERE place.team_id = {world.Mars} AND item.project_id = {world.OtherProjectId}
                """));
        Assert.Equal([world.OtherProjectId], (await TeamTestData.LinksOfTeamAsync(factory, world.Mars)).Select(link => link.ProjectId));
        Assert.Null(await TeamTestData.SettingAsync(factory, world.ProjectId));
    }

    private static string ProjectTeamRoute(OwningTeamWorld world, Guid teamId) => TeamTestData.ProjectTeam(world.OrganizationId, world.ProjectId, teamId);

    /// <summary><paramref name="count"/> stories of one project owned by the world's MARS, all last changed on 2026-10-01 at 09:00.</summary>
    private async Task SeedAsync(OwningTeamWorld world, Guid projectId, int count)
    {
        var story = world.Items.Types["US"];
        await WorkItemsTestData.ExecuteAsync(factory, $"""
            INSERT INTO work_items (id, project_id, number, type_id, state_id, title, description, creation_request_id, created_at, created_by, changed_at, owning_team_id)
            SELECT gen_random_uuid(), {projectId}, n, {story.Id}, {story.StateId("Новая")},
                   'Объёмная ' || n, '', gen_random_uuid(), now() + n * interval '1 millisecond', 'volume-test', timestamptz '2026-10-01 09:00:00+00', {world.Mars}
            FROM generate_series(1, {count}) AS n
            """);
        await WorkItemsTestData.ExecuteAsync(factory, $"""
            INSERT INTO work_item_counters (project_id, last_number) VALUES ({projectId}, {count})
            ON CONFLICT (project_id) DO UPDATE SET last_number = excluded.last_number
            """);
    }

    /// <summary>One text column named <c>Value</c> of every row of <paramref name="sql"/>.</summary>
    private async Task<List<string>> QueryAsync(FormattableString sql)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<Mustask.Infrastructure.MustaskDbContext>();

        return await context.Database.SqlQuery<string>(sql).ToListAsync();
    }
}
