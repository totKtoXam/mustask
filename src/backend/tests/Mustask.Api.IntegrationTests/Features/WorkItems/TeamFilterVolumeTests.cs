using System.Diagnostics;
using System.Net;
using Mustask.Api.IntegrationTests.Features.Organizations;
using Xunit.Abstractions;
using static Mustask.Api.IntegrationTests.Features.WorkItems.OwningTeamTestData;

namespace Mustask.Api.IntegrationTests.Features.WorkItems;

/// <summary>
/// mod-017 US-4 and US-5, "large views" and "a filter keeps its time budget" (NFR Volumes), through the real host and a real
/// PostgreSQL: with 5 000 items on each of the four levels of a project, linked under the level above and owned by MARS, VENUS and
/// none in turn, the team's own backlog, the project's list, backlog and board — unfiltered, by a team, by none and by both — are
/// each served within 2 s, as the panel sends them, after one warm-up call of each. The items are inserted with one statement per
/// level: through the endpoints the setup alone would take minutes.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class TeamFilterVolumeTests(MustaskApiFactory factory, ITestOutputHelper output)
{
    private const int PerLevel = 5_000;
    private static readonly TimeSpan ReadBudget = TimeSpan.FromSeconds(2);

    [Fact]
    public async Task Views_FourLevelsOf5000_WithAndWithoutAFilter_ReadWithin2s()
    {
        var world = await CreateWorldAsync(factory);
        var boards = BoardsViewOf(world);
        var types = new[] { "EPIC", "FEAT", "US", "TASK" };

        for (var level = 0; level < types.Length; level++)
        {
            var type = world.Items.Types[types[level]];
            var from = (level * PerLevel) + 1;

            await WorkItemsTestData.ExecuteAsync(factory, $"""
                INSERT INTO work_items (id, project_id, number, type_id, state_id, title, description, creation_request_id, created_at, created_by, changed_at, parent_id, owning_team_id)
                SELECT gen_random_uuid(), {world.ProjectId}, n, {type.Id}, {type.StateId("Новая")},
                       {types[level]} || ' ' || n, '', gen_random_uuid(), now() + n * interval '1 millisecond', 'volume-test', now(), parent.id,
                       CASE n % 3 WHEN 0 THEN {world.Mars} WHEN 1 THEN {world.Venus} END
                FROM generate_series({from}, {from + PerLevel - 1}) AS n
                LEFT JOIN work_items AS parent ON parent.project_id = {world.ProjectId} AND parent.number = n - {PerLevel} AND {level > 0}
                """);
        }

        await WorkItemsTestData.ExecuteAsync(factory, $"""
            INSERT INTO work_item_counters (project_id, last_number) VALUES ({world.ProjectId}, {types.Length * PerLevel})
            ON CONFLICT (project_id) DO UPDATE SET last_number = excluded.last_number
            """);

        Task<Answer> Get(string path) => OrganizationsApi.SendAsync(factory, HttpMethod.Get, path, token: OrganizationsTokens.SignedIn());

        var requests = new (string What, Func<Task<Answer>> Send)[]
        {
            ("the list", () => WorkItemsTestData.ListAsync(factory, world.Items)),
            ("the list, page 3", () => WorkItemsTestData.ListAsync(factory, world.Items, "?page=3")),
            ("the list by MARS", () => WorkItemsTestData.ListAsync(factory, world.Items, $"?teamId={world.Mars}")),
            ("the list by none", () => WorkItemsTestData.ListAsync(factory, world.Items, "?teamId=none")),
            ("the list by MARS and none", () => WorkItemsTestData.ListAsync(factory, world.Items, $"?teamId={world.Mars}&teamId=none")),
            ("the project backlog", () => Get(Boards.ProjectBoardsTestData.Backlog(boards, "stories"))),
            ("the project backlog by VENUS", () => Get($"{Boards.ProjectBoardsTestData.Backlog(boards, "stories")}?teamId={world.Venus}")),
            ("the project backlog by none", () => Get($"{Boards.ProjectBoardsTestData.Backlog(boards, "stories")}?teamId=none")),
            ("the project board", () => Get(Boards.ProjectBoardsTestData.Board(boards, "stories"))),
            ("the project board by VENUS", () => Get($"{Boards.ProjectBoardsTestData.Board(boards, "stories")}?teamId={world.Venus}")),
            ("MARS's own backlog", () => Boards.BoardsTestData.GetBacklogAsync(factory, boards, world.Mars)),
        };

        // Warm-up, not asserted: a process's first call of an operation also pays JIT, query compilation and connection warm-up.
        foreach (var (_, send) in requests)
        {
            Assert.Equal(HttpStatusCode.OK, (await send()).Status);
        }

        foreach (var (what, send) in requests)
        {
            var watch = Stopwatch.StartNew();
            var answer = await send();
            watch.Stop();
            output.WriteLine($"{what}: {watch.Elapsed.TotalMilliseconds:F0} ms");

            Assert.Equal(HttpStatusCode.OK, answer.Status);
            Assert.True(watch.Elapsed < ReadBudget, $"{what} took {watch.Elapsed}.");
        }
    }
}
