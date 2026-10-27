using System.Diagnostics;
using System.Net;
using System.Text.Json;
using Mustask.Api.IntegrationTests.Features.Organizations;
using Mustask.Domain.Organizations;

namespace Mustask.Api.IntegrationTests.Features.WorkItems;

/// <summary>
/// <c>GET /api/organizations/{organizationId}/projects/{projectId}/workitems?page=</c>
/// (mod-003 api-contract § 2.1, US-2): every item of the project, newest change
/// first, then number descending, 50 a page, in ADR-0015's <c>{ count, data }</c>.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class GetWorkItemsEndpointTests(MustaskApiFactory factory)
{
    private static List<string> Identifiers(JsonElement page) =>
        page.GetProperty("data").EnumerateArray().Select(row => row.GetProperty("identifier").GetString()!).ToList();

    // US-2 · "A row shows the item's columns": MSK-BUG-4 «Падение при входе», assigned to «Анна Смирнова», in «Активна»,
    // with its time of change; the row carries no description.
    [Fact]
    public async Task List_Row_CarriesEveryColumn()
    {
        var world = await WorkItemsTestData.CreateWorldAsync(factory);
        var anna = await WorkItemsTestData.AddMemberAsync(factory, world, "Анна", "Смирнова", "anna.smirnova@example.com");
        foreach (var type in new[] { "EPIC", "FEAT", "US" })
        {
            await WorkItemsTestData.CreateItemAsync(factory, world, type, $"Элемент {type}");
        }

        var bug = await WorkItemsTestData.CreateItemAsync(factory, world, "BUG", "Падение при входе");
        var saved = await WorkItemsTestData.SaveAsync(factory, world, bug, new
        {
            stateId = world.Types["BUG"].StateId("Активна").ToString(),
            assignee = new { userId = anna },
        });

        var answer = await WorkItemsTestData.ListAsync(factory, world);

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Equal(4, answer.Body.GetProperty("count").GetInt32());
        var row = answer.Body.GetProperty("data")[0];
        Assert.Equal("MSK-BUG-4", row.GetProperty("identifier").GetString());
        Assert.Equal(4, row.GetProperty("number").GetInt32());
        Assert.Equal(("BUG", "Баг", "bug"), (
            row.GetProperty("type").GetProperty("key").GetString(),
            row.GetProperty("type").GetProperty("name").GetString(),
            row.GetProperty("type").GetProperty("appearance").GetString()));
        Assert.Equal("Падение при входе", row.GetProperty("title").GetString());
        Assert.Equal((anna, "Анна Смирнова", "anna.smirnova@example.com"), (
            row.GetProperty("assignee").GetProperty("id").GetString(),
            row.GetProperty("assignee").GetProperty("displayName").GetString(),
            row.GetProperty("assignee").GetProperty("email").GetString()));

        // mod-016 BR-M1-26 (api-contract § 3.3): the assignee is a member of the item's organization.
        Assert.True(row.GetProperty("assignee").GetProperty("isMember").GetBoolean());

        // feat-003 OQ-2 A (api-contract § 1.1): the row's assignee carries the directory's active flag beside the membership's.
        Assert.True(row.GetProperty("assignee").GetProperty("isActive").GetBoolean());
        Assert.Equal(
            ["displayName", "email", "id", "isActive", "isMember"],
            row.GetProperty("assignee").EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));
        Assert.Equal(("Активна", "InProgress"), (
            row.GetProperty("state").GetProperty("name").GetString(),
            row.GetProperty("state").GetProperty("category").GetProperty("code").GetString()));
        Assert.False(row.TryGetProperty("area", out _));
        Assert.Equal(saved.Body.GetProperty("changedAt").GetDateTimeOffset(), row.GetProperty("changedAt").GetDateTimeOffset());
        Assert.False(row.TryGetProperty("description", out _));
    }

    // US-2 · "Only this project's items are listed".
    [Fact]
    public async Task List_OnlyTheProjectsItems()
    {
        var world = await WorkItemsTestData.CreateWorldAsync(factory);
        var operations = await WorkItemsTestData.AddProjectAsync(factory, world, "OPS", "Операции");
        await WorkItemsTestData.CreateItemAsync(factory, world, "BUG", "Мастаск 1");
        await WorkItemsTestData.CreateItemAsync(factory, operations, "BUG", "Операции 1");
        await WorkItemsTestData.CreateItemAsync(factory, world, "TASK", "Мастаск 2");

        var answer = await WorkItemsTestData.ListAsync(factory, world);

        Assert.Equal(2, answer.Body.GetProperty("count").GetInt32());
        Assert.All(Identifiers(answer.Body), identifier => Assert.StartsWith("MSK-", identifier, StringComparison.Ordinal));
    }

    // US-2 · "The latest change comes first": a save moves an item to the top; items changed at the same moment are
    // ordered by number, higher first (BR-M3-29).
    [Fact]
    public async Task List_LatestChangeFirst_ThenNumberDescending()
    {
        var world = await WorkItemsTestData.CreateWorldAsync(factory);
        var first = await WorkItemsTestData.CreateItemAsync(factory, world, "TASK", "Первая");
        await WorkItemsTestData.CreateItemAsync(factory, world, "TASK", "Вторая");

        Assert.Equal(["MSK-TASK-2", "MSK-TASK-1"], Identifiers((await WorkItemsTestData.ListAsync(factory, world)).Body));

        await WorkItemsTestData.SaveAsync(factory, world, first, new { title = "Первая, изменена" });

        Assert.Equal(["MSK-TASK-1", "MSK-TASK-2"], Identifiers((await WorkItemsTestData.ListAsync(factory, world)).Body));

        await WorkItemsTestData.CreateItemAsync(factory, world, "BUG", "Третий");
        var moment = new DateTimeOffset(2026, 10, 5, 14, 30, 0, TimeSpan.Zero);
        await WorkItemsTestData.ExecuteAsync(factory, $"UPDATE work_items SET changed_at = {moment} WHERE project_id = {world.ProjectId}");

        Assert.Equal(["MSK-BUG-3", "MSK-TASK-2", "MSK-TASK-1"], Identifiers((await WorkItemsTestData.ListAsync(factory, world)).Body));
    }

    // US-2 · "The list is paged by 50": 120 items, 50 on the first page, 20 on the third, «1–50 из 120» from count;
    // a page past the last is empty with the same count.
    [Fact]
    public async Task List_120Items_PagedBy50()
    {
        var world = await WorkItemsTestData.CreateWorldAsync(factory);
        await SeedAsync(world, 120);

        var first = await WorkItemsTestData.ListAsync(factory, world);
        var third = await WorkItemsTestData.ListAsync(factory, world, "?page=3");
        var past = await WorkItemsTestData.ListAsync(factory, world, "?page=4");

        Assert.Equal((120, 50), (first.Body.GetProperty("count").GetInt32(), first.Body.GetProperty("data").GetArrayLength()));
        Assert.Equal("MSK-TASK-120", Identifiers(first.Body)[0]);
        Assert.Equal((120, 20), (third.Body.GetProperty("count").GetInt32(), third.Body.GetProperty("data").GetArrayLength()));
        Assert.Equal("MSK-TASK-1", Identifiers(third.Body)[^1]);
        Assert.Equal((120, 0), (past.Body.GetProperty("count").GetInt32(), past.Body.GetProperty("data").GetArrayLength()));
    }

    // api-contract § 2.1: a page below 1 is a field reason; a page that is no integer fails binding.
    [Fact]
    public async Task List_PageBelowOne_Returns400InvalidPage_NonIntegerReturns400BadRequest()
    {
        var world = await WorkItemsTestData.CreateWorldAsync(factory);

        var zero = await WorkItemsTestData.ListAsync(factory, world, "?page=0");
        var text = await WorkItemsTestData.ListAsync(factory, world, "?page=abc");

        Assert.Equal(HttpStatusCode.BadRequest, zero.Status);
        Assert.Equal("workitems.validation_failed", zero.ErrorCode);
        Assert.Equal("workitems.invalid_page", zero.Body.GetProperty("errors").GetProperty("page")[0].GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.BadRequest, text.Status);
    }

    // US-2 · "An empty project invites the first item": the answer the empty frame reads.
    [Fact]
    public async Task List_EmptyProject_CountZero_NoRows()
    {
        var world = await WorkItemsTestData.CreateWorldAsync(factory);

        var answer = await WorkItemsTestData.ListAsync(factory, world);

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Equal((0, 0), (answer.Body.GetProperty("count").GetInt32(), answer.Body.GetProperty("data").GetArrayLength()));
    }

    // US-2 · "A list whose context is not found": a deactivated project, another organization's project, a malformed or
    // unknown project id answer «Проект не найден» (the organization's own answers are ScopedAccessTests').
    [Fact]
    public async Task List_ProjectDeactivatedForeignMalformedOrUnknown_Returns404ProjectNotFound()
    {
        var world = await WorkItemsTestData.CreateWorldAsync(factory);
        var other = await WorkItemsTestData.CreateWorldAsync(factory);
        var deactivated = await WorkItemsTestData.AddProjectAsync(factory, world, "OLD", "Архив");
        await OrganizationsTestData.SetProjectStatusAsync(factory, world.OrganizationId, deactivated.ProjectId, ProjectStatus.Deactivated);

        foreach (var path in new[]
                 {
                     WorkItemsTestData.List(deactivated),
                     WorkItemsTestData.List(world with { ProjectId = other.ProjectId }),
                     $"/api/organizations/{world.OrganizationId}/projects/abc/workitems",
                     WorkItemsTestData.List(world with { ProjectId = Guid.CreateVersion7() }),
                 })
        {
            var answer = await OrganizationsApi.SendAsync(factory, HttpMethod.Get, path, token: OrganizationsTokens.SignedIn());

            Assert.True(answer.Status == HttpStatusCode.NotFound, $"{path} answered {answer.Status}.");
            Assert.Equal("organizations.project.not_found", answer.ErrorCode);
        }
    }

    // US-2 · "An assignee the directory does not know": the row keeps the id and has no name («Неизвестный пользователь»).
    [Fact]
    public async Task List_AssigneeUnknownToTheDirectory_HasIdAndNoName()
    {
        var world = await WorkItemsTestData.CreateWorldAsync(factory);
        var user = await WorkItemsTestData.AddMemberAsync(factory, world, "Временный", "Пользователь");
        var bug = await WorkItemsTestData.CreateItemAsync(factory, world, "BUG", "Баг");
        await WorkItemsTestData.SaveAsync(factory, world, bug, new { assignee = new { userId = user } });
        await WorkItemsTestData.ExecuteAsync(factory, $"DELETE FROM directory_users WHERE id = {user}");

        var row = (await WorkItemsTestData.ListAsync(factory, world)).Body.GetProperty("data")[0];

        Assert.Equal(user, row.GetProperty("assignee").GetProperty("id").GetString());
        Assert.Equal(JsonValueKind.Null, row.GetProperty("assignee").GetProperty("displayName").ValueKind);
        Assert.Equal(JsonValueKind.Null, row.GetProperty("assignee").GetProperty("email").ValueKind);

        // mod-016: the flag reads the membership alone — a member the projection does not know is still a member.
        Assert.True(row.GetProperty("assignee").GetProperty("isMember").GetBoolean());

        // feat-003 OQ-2 A: an id the directory does not know is not active (the panel adds «(неактивен)» to a named user only).
        Assert.False(row.GetProperty("assignee").GetProperty("isActive").GetBoolean());
    }

    // US-2 · "A large project stays usable" (NFR Volumes): with 5 000 items the first page and a later one each answer
    // within 2 s.
    [Fact]
    public async Task List_5000Items_EachPageAnswersWithinTwoSeconds()
    {
        var world = await WorkItemsTestData.CreateWorldAsync(factory);
        await SeedAsync(world, 5000);
        await WorkItemsTestData.ListAsync(factory, world); // warm-up: the first request of a test pays for JIT, not for the query

        foreach (var query in new[] { "?page=1", "?page=57", "?page=100" })
        {
            var watch = Stopwatch.StartNew();
            var answer = await WorkItemsTestData.ListAsync(factory, world, query);
            watch.Stop();

            Assert.Equal(HttpStatusCode.OK, answer.Status);
            Assert.Equal(5000, answer.Body.GetProperty("count").GetInt32());
            Assert.True(watch.Elapsed < TimeSpan.FromSeconds(2), $"{query} took {watch.Elapsed}.");
        }
    }

    /// <summary>
    /// <paramref name="count"/> «Задача» items numbered 1..count straight into the table, each changed one second after
    /// the previous, and the counter set past them: the list's reads are under test here, not the create endpoint.
    /// </summary>
    private async Task SeedAsync(WorkItemsWorld world, int count)
    {
        var type = world.Types["TASK"];
        var state = type.StateId("Новая");
        var start = new DateTimeOffset(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);

        await WorkItemsTestData.ExecuteAsync(factory, $"""
            INSERT INTO work_items (id, project_id, number, type_id, state_id, assignee_id, title, description,
                                    creation_request_id, created_at, created_by, changed_at)
            SELECT gen_random_uuid(), {world.ProjectId}, n, {type.Id}, {state}, NULL, 'Задача ' || n, '',
                   gen_random_uuid(), {start}, 'seed', {start} + n * interval '1 second'
            FROM generate_series(1, {count}) AS n
            """);
        await WorkItemsTestData.ExecuteAsync(factory, $"""
            INSERT INTO work_item_counters (project_id, last_number) VALUES ({world.ProjectId}, {count})
            """);
    }
}
