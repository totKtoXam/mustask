using System.Diagnostics;
using System.Net;
using Mustask.Api.IntegrationTests.Features.WorkItems;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;
using static Mustask.Api.IntegrationTests.Features.WorkItems.OwningTeamTestData;

namespace Mustask.Api.IntegrationTests.Features.Organizations;

/// <summary>
/// feat-028 US-5, "a big deletion is all or nothing" and the NFR Volumes (mod-017 US-7 before it) through the real host and a real
/// PostgreSQL: a team linked to <b>three projects</b> that owns 5 000 items of all four levels across them (and holds a place for
/// some of them) is deleted within 5 s, as the panel sends it after one warm-up call; every item is released to «Без команды» with the
/// time of the operation, none keeps the team, the places and the links are gone, the counts the confirmation showed were the numbers
/// released, and the address answers not found. The items are inserted with one statement per project: through the endpoints the setup
/// alone would take minutes.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class DeleteTeamVolumeTests(MustaskApiFactory factory, ITestOutputHelper output)
{
    private const int InAlpha = 3_000;
    private const int InBeta = 1_200;
    private const int InGamma = 800;
    private const int Owned = InAlpha + InBeta + InGamma;
    private static readonly TimeSpan Budget = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task Delete_5000OwnedItemsOverThreeProjects_Within5Seconds()
    {
        var world = await CreateWorldAsync(factory);
        var gamma = await OrganizationsTestData.CreateBareProjectAsync(factory, world.OrganizationId, "Гамма", "GAM");
        await OrganizationsTestData.LinkAsync(factory, world.OtherProjectId, world.Mars);
        await OrganizationsTestData.LinkAsync(factory, gamma.Id, world.Mars);
        var story = world.Items.Types["US"];
        var level = story.BacklogLevelId;
        await WorkItemsTestData.ExecuteAsync(factory, $"""
            INSERT INTO backlog_orders (team_id, backlog_level_id) VALUES ({world.Mars}, {level}) ON CONFLICT DO NOTHING
            """);

        await SeedAsync(world, world.ProjectId, InAlpha, placed: 1_000);
        await SeedAsync(world, world.OtherProjectId, InBeta, placed: 300);
        await SeedAsync(world, gamma.Id, InGamma, placed: 0);
        var teamRoute = TeamTestData.Team(world.OrganizationId, world.Mars);

        // A warm-up of the deletion's path on another team, not asserted: a process's first call of an operation also pays JIT,
        // query compilation and connection warm-up.
        Assert.Equal(
            HttpStatusCode.NoContent,
            (await OrganizationsApi.SendAsync(factory, HttpMethod.Delete, TeamTestData.Team(world.OrganizationId, world.Venus), token: OrganizationsTokens.SignedIn())).Status);

        var counts = await OrganizationsApi.SendAsync(factory, HttpMethod.Get, $"{teamRoute}/deletion-counts", token: OrganizationsTokens.SignedIn());
        var watch = Stopwatch.StartNew();
        var answer = await OrganizationsApi.SendAsync(factory, HttpMethod.Delete, teamRoute, token: OrganizationsTokens.SignedIn());
        watch.Stop();
        output.WriteLine($"deleting a team that owns {Owned} items in three projects: {watch.Elapsed.TotalMilliseconds:F0} ms");

        Assert.Equal(HttpStatusCode.OK, counts.Status);
        Assert.Equal(
            [("Альфа", InAlpha), ("Бета", InBeta), ("Гамма", InGamma)],
            counts.Body.GetProperty("projects").EnumerateArray()
                .Select(project => (project.GetProperty("name").GetString()!, project.GetProperty("count").GetInt32())));
        Assert.Equal(HttpStatusCode.NoContent, answer.Status);
        Assert.True(watch.Elapsed < Budget, $"The deletion took {watch.Elapsed}.");

        // Every item is still there, none keeps a team, and every «Изменён» moved to the time of the operation.
        Assert.Equal(
            [$"{Owned}|0|0"],
            await QueryAsync($"""
                SELECT count(*)::text || '|' || (count(*) FILTER (WHERE owning_team_id IS NOT NULL))::text
                       || '|' || (count(*) FILTER (WHERE changed_at = timestamptz '2026-10-01 09:00:00+00'))::text AS "Value"
                FROM work_items WHERE project_id IN ({world.ProjectId}, {world.OtherProjectId}, {gamma.Id})
                """));
        Assert.Equal(["0"], await QueryAsync($"SELECT count(*)::text AS \"Value\" FROM backlog_places WHERE team_id = {world.Mars}"));
        Assert.Equal(["0"], await QueryAsync($"SELECT count(*)::text AS \"Value\" FROM project_teams WHERE team_id = {world.Mars}"));
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await OrganizationsApi.SendAsync(factory, HttpMethod.Get, teamRoute, token: OrganizationsTokens.SignedIn())).Status);
    }

    /// <summary><paramref name="count"/> stories of one project owned by the world's MARS, the first <paramref name="placed"/> of them on its backlog.</summary>
    private async Task SeedAsync(OwningTeamWorld world, Guid projectId, int count, int placed)
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

        if (placed > 0)
        {
            await WorkItemsTestData.ExecuteAsync(factory, $"""
                INSERT INTO backlog_places (work_item_id, team_id, backlog_level_id, position)
                SELECT item.id, {world.Mars}, {story.BacklogLevelId}, item.number + {(projectId == world.ProjectId ? 0 : 10_000)}
                FROM work_items AS item WHERE item.project_id = {projectId} AND item.number <= {placed}
                """);
        }
    }

    /// <summary>One text column named <c>Value</c> of every row of <paramref name="sql"/>.</summary>
    private async Task<List<string>> QueryAsync(FormattableString sql)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<Mustask.Infrastructure.MustaskDbContext>();

        return await context.Database.SqlQuery<string>(sql).ToListAsync();
    }
}
