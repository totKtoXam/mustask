using System.Diagnostics;
using System.Net;
using Mustask.Api.IntegrationTests.Features.Organizations;
using Mustask.Api.IntegrationTests.Features.WorkItems;
using Xunit.Abstractions;

namespace Mustask.Api.IntegrationTests.Features.Boards;

/// <summary>
/// feat-040 spec § Non-functional requirements and US-7 "A large team's views stay fast": MARS linked to 50 projects and owning
/// 5 000 stories across them, its order holding places for 4 000 of them — its backlog and its board, unfiltered and filtered to
/// ten projects, are each served within 2 s, and a move to the top of the backlog within 1 s — measured as the panel sends them,
/// after one warm-up call of each. The items and places are inserted with SQL: through the endpoints the setup alone would take
/// minutes. (The one-project forms are <c>BacklogVolumeTests</c>', kept, and <c>ProjectBacklogVolumeTests</c>'.)
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class TeamBacklogVolumeTests(MustaskApiFactory factory, ITestOutputHelper output)
{
    private const int Projects = 50;
    private const int Stories = 5_000;
    private const int Placed = 4_000;
    private const int FilterProjects = 10;

    private static readonly TimeSpan ReadBudget = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan WriteBudget = TimeSpan.FromSeconds(1);

    [Fact]
    public async Task TeamViews_FiftyProjectsAndFiveThousandStories_ReadWithin2s_FilteredToo_MoveToTheTopWithin1s()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var mars = await OrganizationsTestData.AddUnlinkedTeamAsync(factory, organization.Id, "MARS");
        var types = await WorkItemsTestData.TypesAsync(factory, organization.Id);
        var story = types["US"];
        var projects = new List<Guid>();

        for (var index = 1; index <= Projects; index++)
        {
            var project = await OrganizationsTestData.CreateBareProjectAsync(factory, organization.Id, $"Проект {index:D2}", $"VP{index:D2}");
            await OrganizationsTestData.LinkAsync(factory, project.Id, mars.Id);
            projects.Add(project.Id);
        }

        var projectIds = projects.ToArray();

        // Story n goes to project ((n - 1) mod 50) + 1 as that project's number ((n - 1) div 50) + 1 — a hundred each; every one is
        // owned by MARS, created a millisecond after the one before it, in the project's root area.
        await WorkItemsTestData.ExecuteAsync(factory, $"""
            INSERT INTO work_items (id, project_id, number, type_id, state_id, title, description, creation_request_id, created_at, created_by, changed_at, owning_team_id)
            SELECT gen_random_uuid(), project.id, ((n - 1) / {Projects}) + 1, {story.Id}, {story.StateId("Новая")},
                   'Элемент ' || n, '', gen_random_uuid(), now() + n * interval '1 millisecond', 'volume-test', now(), {mars.Id}
            FROM generate_series(1, {Stories}) AS n
            JOIN unnest({projectIds}) WITH ORDINALITY AS project(id, ordinal) ON project.ordinal = ((n - 1) % {Projects}) + 1
            """);
        await WorkItemsTestData.ExecuteAsync(factory, $"""
            INSERT INTO work_item_counters (project_id, last_number)
            SELECT project.id, {Stories / Projects} FROM unnest({projectIds}) AS project(id)
            ON CONFLICT (project_id) DO UPDATE SET last_number = excluded.last_number
            """);
        await WorkItemsTestData.ExecuteAsync(factory, $"""
            INSERT INTO backlog_orders (team_id, backlog_level_id) VALUES ({mars.Id}, {story.BacklogLevelId})
            ON CONFLICT (team_id, backlog_level_id) DO NOTHING
            """);
        await WorkItemsTestData.ExecuteAsync(factory, $"""
            INSERT INTO backlog_places (work_item_id, team_id, backlog_level_id, position)
            SELECT item.id, {mars.Id}, {story.BacklogLevelId}, (row_number() OVER (ORDER BY item.created_at, item.id))::integer
            FROM (SELECT id, created_at FROM work_items WHERE owning_team_id = {mars.Id} ORDER BY created_at, id LIMIT {Placed}) AS item
            """);

        var team = TeamTestData.Team(organization.Id, mars.Id);
        var filter = string.Join("&", projects.Take(FilterProjects).Select(id => $"projectId={id}"));

        Task<Answer> Backlog(string? query = null) =>
            OrganizationsApi.SendAsync(factory, HttpMethod.Get, $"{team}/backlogs/stories{(query is null ? string.Empty : "?" + query)}", token: OrganizationsTokens.SignedIn());

        Task<Answer> Board(string? query = null) =>
            OrganizationsApi.SendAsync(factory, HttpMethod.Get, $"{team}/boards/stories{(query is null ? string.Empty : "?" + query)}", token: OrganizationsTokens.SignedIn());

        Task<Answer> Move(Guid id, string position, Guid? after = null) =>
            OrganizationsApi.SendAsync(
                factory,
                HttpMethod.Post,
                $"{team}/backlogs/stories/reorder",
                new { itemId = id.ToString(), position, afterItemId = after?.ToString() },
                OrganizationsTokens.SignedIn());

        // Warm-up, not asserted: a process's first call of an operation also pays JIT, query compilation and connection warm-up.
        var warm = await Backlog();
        Assert.Equal(HttpStatusCode.OK, warm.Status);
        var rows = warm.Body.GetProperty("items");
        Assert.Equal(Stories, rows.GetArrayLength());
        Assert.Equal(Projects, warm.Body.GetProperty("projects").GetArrayLength());
        var last = rows[Stories - 1].GetProperty("id").GetGuid();
        var middle = rows[Stories / 2].GetProperty("id").GetGuid();
        Assert.Equal(HttpStatusCode.OK, (await Board()).Status);
        Assert.Equal(HttpStatusCode.OK, (await Backlog(filter)).Status);
        Assert.Equal(HttpStatusCode.OK, (await Board(filter)).Status);
        var warmReorder = await TimedAsync("warm-up reorder (places every item, not asserted)", () => Move(middle, "bottom"));
        Assert.Equal(HttpStatusCode.NoContent, warmReorder.Answer.Status);

        var backlog = await TimedAsync("backlog, unfiltered", () => Backlog());
        var board = await TimedAsync("board, unfiltered", () => Board());
        var filteredBacklog = await TimedAsync("backlog, filtered to 10 projects", () => Backlog(filter));
        var filteredBoard = await TimedAsync("board, filtered to 10 projects", () => Board(filter));
        var toTheTop = await TimedAsync("move to the top", () => Move(last, "top"));
        var after = await TimedAsync("move after", () => Move(middle, "after", last));

        Assert.Equal(HttpStatusCode.OK, backlog.Answer.Status);
        Assert.Equal(Stories, backlog.Answer.Body.GetProperty("items").GetArrayLength());
        Assert.Equal(Stories, board.Answer.Body.GetProperty("columns").EnumerateArray().Sum(column => column.GetProperty("items").GetArrayLength()));
        var expectedFiltered = Stories / Projects * FilterProjects;
        Assert.Equal(expectedFiltered, filteredBacklog.Answer.Body.GetProperty("items").GetArrayLength());
        Assert.Equal(expectedFiltered, filteredBoard.Answer.Body.GetProperty("columns").EnumerateArray().Sum(column => column.GetProperty("items").GetArrayLength()));
        Assert.Equal(Projects, filteredBacklog.Answer.Body.GetProperty("projects").GetArrayLength());
        Assert.Equal((HttpStatusCode.NoContent, HttpStatusCode.NoContent), (toTheTop.Answer.Status, after.Answer.Status));

        Assert.True(backlog.Elapsed < ReadBudget, $"The backlog read took {backlog.Elapsed}.");
        Assert.True(board.Elapsed < ReadBudget, $"The board read took {board.Elapsed}.");
        Assert.True(filteredBacklog.Elapsed < ReadBudget, $"The filtered backlog read took {filteredBacklog.Elapsed}.");
        Assert.True(filteredBoard.Elapsed < ReadBudget, $"The filtered board read took {filteredBoard.Elapsed}.");
        Assert.True(toTheTop.Elapsed < WriteBudget, $"The move to the top took {toTheTop.Elapsed}.");
        Assert.True(after.Elapsed < WriteBudget, $"The move after took {after.Elapsed}.");

        var order = (await Backlog()).Body.GetProperty("items").EnumerateArray().Take(3).Select(row => row.GetProperty("id").GetGuid()).ToArray();
        Assert.Equal([last, middle], order.Take(2));
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
