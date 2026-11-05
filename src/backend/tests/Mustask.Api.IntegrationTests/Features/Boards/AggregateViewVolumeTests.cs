using System.Diagnostics;
using System.Net;
using Mustask.Api.IntegrationTests.Features.Organizations;
using Mustask.Api.IntegrationTests.Features.WorkItems;
using Xunit.Abstractions;

namespace Mustask.Api.IntegrationTests.Features.Boards;

/// <summary>
/// feat-048 BR-AV-11 (spec, non-functional) and ADR-0036: an organization of 200 projects, ten departments, eight teams and
/// 1 000 000 work items (half of them stories) answers every grouping of the aggregate backlog, board and work-item list within
/// 2 s — the grouped counts, the first page of a group, a page three cursors deep, a board cell and the flat list. It is the proof of
/// the indexes of <c>AggregateViewIndexes</c> (plan-backend D-5): a budget that is missed is reported, never relaxed. The items are
/// inserted with SQL — through the endpoints the setup alone would take days — and the table is analysed, as production's
/// autovacuum would have done. A <c>*VolumeTests</c> class runs apart from the others (it holds a timing budget).
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class AggregateViewVolumeTests(MustaskApiFactory factory, ITestOutputHelper output)
{
    private const int Projects = 200;
    private const int Departments = 10;
    private const int Teams = 8;
    private const int Items = 1_000_000;
    private const int PerType = Items / 2;
    private const int Chunk = 50_000;

    private static readonly TimeSpan Budget = TimeSpan.FromSeconds(2);

    [Fact]
    public async Task EveryGrouping_OfAMillionItemsInTwoHundredProjects_IsReadWithin2s()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var types = await WorkItemsTestData.TypesAsync(factory, organization.Id);
        var story = types["US"];
        var task = types["TASK"];
        var projectIds = new List<Guid>();

        for (var index = 1; index <= Projects; index++)
        {
            var project = await OrganizationsTestData.CreateBareProjectAsync(factory, organization.Id, $"Проект {index:D3}", $"VP{index:D3}");
            projectIds.Add(project.Project.Id);
        }

        var teamIds = new List<Guid>();

        for (var index = 1; index <= Teams; index++)
        {
            teamIds.Add((await OrganizationsTestData.AddUnlinkedTeamAsync(factory, organization.Id, $"Команда {index}")).Id);
        }

        var departmentIds = new List<Guid>();

        for (var index = 1; index <= Departments; index++)
        {
            departmentIds.Add((await OrganizationsTestData.AddDepartmentAsync(factory, organization.Id, $"Департамент {index:D2}")).Id);
        }

        var projects = projectIds.ToArray();
        var teams = teamIds.ToArray();
        var departments = departmentIds.ToArray();

        // Every project is linked to every team (the owner key of an item needs the link); 
        await WorkItemsTestData.ExecuteAsync(factory, $"""
            INSERT INTO project_teams (project_id, team_id, organization_id, linked_by, linked_at)
            SELECT project.id, team.id, {organization.Id}, 'volume-test', now()
            FROM unnest({projects}) AS project(id) CROSS JOIN unnest({teams}) AS team(id)
            """);

        await InsertAsync(story, 0, projects, teams);
        await InsertAsync(task, PerType / Projects, projects, teams);
        await WorkItemsTestData.ExecuteAsync(factory, $"""
            INSERT INTO work_item_counters (project_id, last_number)
            SELECT project.id, {Items / Projects} FROM unnest({projects}) AS project(id)
            ON CONFLICT (project_id) DO UPDATE SET last_number = excluded.last_number
            """);

        // Nine in ten projects sit under a department (the project node's parent, ADR-0031), the rest at the top level.
        await WorkItemsTestData.ExecuteAsync(factory, $"""
            UPDATE areas SET parent_id = department.id
            FROM unnest({projects}) WITH ORDINALITY AS project(id, ordinal),
                 unnest({departments}) WITH ORDINALITY AS department(id, ordinal)
            WHERE areas.project_id = project.id AND areas.parent_id IS NULL
              AND project.ordinal % {Projects / 20} <> 0
              AND department.ordinal = (project.ordinal % {Departments}) + 1
            """);
        await WorkItemsTestData.ExecuteAsync(factory, $"VACUUM (ANALYZE) work_items");

        var org = $"/api/organizations/{organization.Id}";
        var someDepartment = (await Get($"{org}/backlogs/stories/groups?grouping=department")).Body.GetProperty("groups").EnumerateArray()
            .First(group => group.GetProperty("id").ValueKind == System.Text.Json.JsonValueKind.String && group.GetProperty("count").GetInt32() > 150)
            .GetProperty("id").GetGuid();
        var someTeam = teams[0];
        var someProject = projects[0];

        // Warm-up, not asserted: a process's first call of an operation also pays JIT and query compilation.
        var layout = await Get($"{org}/boards/stories/groups");
        var column = layout.Body.GetProperty("groups")[0].GetProperty("columns")[0].GetProperty("key").GetString()!;

        foreach (var path in Reads(org, someDepartment, someTeam, someProject, column))
        {
            Assert.Equal(HttpStatusCode.OK, (await Get(path.Path)).Status);
        }

        var missed = new List<string>();

        foreach (var (name, path) in Reads(org, someDepartment, someTeam, someProject, column))
        {
            var watch = Stopwatch.StartNew();
            var answer = await Get(path);
            watch.Stop();
            output.WriteLine($"{name}: {watch.Elapsed.TotalMilliseconds:F0} ms");
            Assert.True(answer.Status == HttpStatusCode.OK, $"{name} answered {answer.Status} {answer.ErrorCode}.");

            if (watch.Elapsed >= Budget)
            {
                missed.Add($"{name} took {watch.Elapsed}.");
            }
        }

        // The cursor three pages deep, for the change-time order and for the rank order.
        foreach (var (name, basePath) in new[]
        {
            ("stream none, 3 pages deep", $"{org}/backlogs/stories/rows?grouping=team&group=none&limit=50"),
            ("team group, 3 pages deep", $"{org}/backlogs/stories/rows?grouping=team&group={someTeam}&limit=50"),
            ("department group, 3 pages deep", $"{org}/backlogs/stories/rows?grouping=department&group={someDepartment}&limit=50"),
            ("project group by rank, 3 pages deep", $"{org}/backlogs/stories/rows?grouping=project&group={someProject}&limit=50"),
            ("work-item stream, 3 pages deep", $"{org}/workitems/rows?grouping=none&limit=50"),
        })
        {
            string? cursor = null;

            for (var page = 0; page < 3; page++)
            {
                var watch = Stopwatch.StartNew();
                var answer = await Get(basePath + (cursor is null ? string.Empty : "&cursor=" + Uri.EscapeDataString(cursor)));
                watch.Stop();
                output.WriteLine($"{name}, page {page + 1}: {watch.Elapsed.TotalMilliseconds:F0} ms");
                Assert.True(answer.Status == HttpStatusCode.OK, $"{name} answered {answer.Status} {answer.ErrorCode}.");
                cursor = answer.Body.TryGetProperty("nextCursor", out var next) && next.ValueKind == System.Text.Json.JsonValueKind.String ? next.GetString() : null;
                Assert.NotNull(cursor);

                if (watch.Elapsed >= Budget)
                {
                    missed.Add($"{name}, page {page + 1} took {watch.Elapsed}.");
                }
            }
        }

        Assert.True(missed.Count == 0, "Over the 2 s budget (BR-AV-11): " + string.Join(" ", missed));
    }

    private Task<Answer> Get(string path) =>
        OrganizationsApi.SendAsync(factory, HttpMethod.Get, path, token: OrganizationsTokens.SignedIn());

    private static IEnumerable<(string Name, string Path)> Reads(string org, Guid department, Guid team, Guid project, string column)
    {
        yield return ("backlog groups by project", $"{org}/backlogs/stories/groups?grouping=project");
        yield return ("backlog groups by team", $"{org}/backlogs/stories/groups?grouping=team");
        yield return ("backlog groups by department", $"{org}/backlogs/stories/groups?grouping=department");
        yield return ("backlog groups, none", $"{org}/backlogs/stories/groups");
        yield return ("board groups by team", $"{org}/boards/stories/groups?grouping=team");
        yield return ("board groups by project", $"{org}/boards/stories/groups?grouping=project");
        yield return ("work-item groups by department", $"{org}/workitems/groups?grouping=department");
        yield return ("backlog rows, stream", $"{org}/backlogs/stories/rows?grouping=none");
        yield return ("backlog rows, no team", $"{org}/backlogs/stories/rows?grouping=team&group=none");
        yield return ("backlog rows, a team", $"{org}/backlogs/stories/rows?grouping=team&group={team}");
        yield return ("backlog rows, a department", $"{org}/backlogs/stories/rows?grouping=department&group={department}");
        yield return ("backlog rows, no department", $"{org}/backlogs/stories/rows?grouping=department&group=none");
        yield return ("backlog rows, a project by rank", $"{org}/backlogs/stories/rows?grouping=project&group={project}");
        yield return ("backlog rows, the projects narrowed to a team", $"{org}/backlogs/stories/rows?grouping=project&group={project}&teamId={team}");
        yield return ("backlog rows, assignee filter", $"{org}/backlogs/stories/rows?grouping=none&assigneeId=user-4");
        yield return ("board cell, a team", $"{org}/boards/stories/cells?grouping=team&group={team}&column={column}");
        yield return ("board cell, a project", $"{org}/boards/stories/cells?grouping=project&group={project}&column={column}");
        yield return ("work-item rows, stream", $"{org}/workitems/rows?grouping=none");
        yield return ("work-item rows, a project", $"{org}/workitems/rows?grouping=project&group={project}");
        yield return ("flat list, page 1", $"{org}/workitems");
        yield return ("flat list, page 100", $"{org}/workitems?page=100");
        yield return ("flat list, keyword", $"{org}/workitems?search=%D0%AD%D0%BB%D0%B5%D0%BC%D0%B5%D0%BD%D1%82%2012345");
        yield return ("assignee options", $"{org}/workitems/assignee-options");
    }

    /// <summary>
    /// Inserts <see cref="PerType"/> items of one type: item n goes to project ((n - 1) mod 200) + 1 as its number
    /// ((n - 1) div 200) + 1 + <paramref name="numberOffset"/>, owned by one of eight teams or by none, assigned to one of fifty users
    /// in one item of four, changed within the last thirty days.
    /// </summary>
    private async Task InsertAsync(Mustask.Application.Abstractions.Process.WorkItemTypeRow type, int numberOffset, Guid[] projects, Guid[] teams)
    {
        for (var first = 1; first <= PerType; first += Chunk)
        {
            var last = Math.Min(first + Chunk - 1, PerType);
            await InsertChunkAsync(type, numberOffset, projects, teams, first, last);
        }
    }

    private Task InsertChunkAsync(Mustask.Application.Abstractions.Process.WorkItemTypeRow type, int numberOffset, Guid[] projects, Guid[] teams, int first, int last) =>
        WorkItemsTestData.ExecuteAsync(factory, $"""
            SET LOCAL session_replication_role = replica;
            INSERT INTO work_items (id, project_id, number, type_id, state_id, area_id, title, description, creation_request_id, created_at, created_by, changed_at, owning_team_id, assignee_id)
            SELECT gen_random_uuid(), project.id, ((n - 1) / {Projects}) + 1 + {numberOffset}, {type.Id}, {type.States.First(state => state.Position == 1).Id}, root.id,
                   'Элемент ' || n || ' ' || {type.Key}, '', gen_random_uuid(), now() - interval '40 days' + n * interval '1 second', 'volume-test',
                   now() - ((n::bigint * 7919) % 2592000) * interval '1 second', team.id,
                   CASE WHEN n % 4 = 0 THEN 'user-' || (n % 50) END
            FROM generate_series({first}, {last}) AS n
            JOIN unnest({projects}) WITH ORDINALITY AS project(id, ordinal) ON project.ordinal = ((n - 1) % {Projects}) + 1
            JOIN areas AS root ON root.project_id = project.id AND root.parent_id IS NULL
            LEFT JOIN unnest({teams}) WITH ORDINALITY AS team(id, ordinal) ON team.ordinal = n % {Teams + 1}
            """);
}
