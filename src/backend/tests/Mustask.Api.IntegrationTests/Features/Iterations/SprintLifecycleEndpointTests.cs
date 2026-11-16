using System.Net;
using Mustask.Api.IntegrationTests.Features.Boards;
using Mustask.Api.IntegrationTests.Features.Organizations;
using Mustask.Api.IntegrationTests.Features.WorkItems;
using Mustask.Api.IntegrationTests.Kit;
using Mustask.Domain.Iterations;
using static Mustask.Api.IntegrationTests.Features.Iterations.IterationsTestData;

namespace Mustask.Api.IntegrationTests.Features.Iterations;

/// <summary>
/// The sprint routes of US-1, US-2 and US-3 (mod-007 api-contract § 2.1 to § 2.5): create with the team's own number, name and
/// date rules, start, remove and the team's list.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class SprintLifecycleEndpointTests(MustaskApiFactory factory)
{
    // US-1 · "A sprint takes the next number of its team's own sequence": 1, 2, 3 in creation order, each team its own.
    [Fact]
    public async Task Create_TakesTheNextNumberOfTheTeamsOwnSequence()
    {
        var world = await CreateWorldAsync(factory);

        var first = await CreateAsync(factory, world, world.PlatformId);
        var second = await CreateAsync(factory, world, world.PlatformId);
        var other = await CreateAsync(factory, world, world.ProjectTeamId);

        Assert.Equal(HttpStatusCode.Created, first.Status);
        Assert.Equal((1, "Спринт 1", "Planned"), (first.Body.GetProperty("number").GetInt32(), first.Body.GetProperty("name").GetString(), first.Body.GetProperty("state").GetString()));
        Assert.Equal(2, second.Body.GetProperty("number").GetInt32());
        Assert.Equal(1, other.Body.GetProperty("number").GetInt32());
        Assert.Equal(world.PlatformId, first.Body.GetProperty("teamId").GetGuid());
        Assert.NotNull(first.Location);
        Assert.Equal(System.Text.Json.JsonValueKind.Null, first.Body.GetProperty("closedAt").ValueKind);
    }

    // US-1 · "Concurrent creations never share a number": eight at once, eight numbers 1..8.
    [Fact]
    public async Task Create_Concurrently_NeverSharesANumber()
    {
        var world = await CreateWorldAsync(factory);

        var answers = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => CreateAsync(factory, world, world.PlatformId)));

        Assert.All(answers, answer => Assert.Equal(HttpStatusCode.Created, answer.Status));
        Assert.Equal(Enumerable.Range(1, 8), answers.Select(answer => answer.Body.GetProperty("number").GetInt32()).Order());
    }

    // US-1 · "A creation whose default name is taken is refused and issues no number".
    [Fact]
    public async Task Create_WhoseDefaultNameIsTaken_IsRefused_AndIssuesNoNumber()
    {
        var world = await CreateWorldAsync(factory);
        var first = await NewAsync(factory, world, world.PlatformId, "Спринт 2");

        // Number 2 would be named «Спринт 2», which the first sprint holds.
        var refused = await CreateAsync(factory, world, world.PlatformId);
        ProblemAssert.Problem(refused, HttpStatusCode.Conflict, SprintErrorCodes.NameTaken);

        // The refusal issued nothing: once the name is free, the next sprint is still number 2.
        var renamed = await SendAsync(factory, HttpMethod.Put, Sprint(world, world.PlatformId, first), new { name = "Другое" });
        Assert.Equal(HttpStatusCode.OK, renamed.Status);

        var next = await CreateAsync(factory, world, world.PlatformId);
        Assert.Equal(HttpStatusCode.Created, next.Status);
        Assert.Equal((2, "Спринт 2"), (next.Body.GetProperty("number").GetInt32(), next.Body.GetProperty("name").GetString()));
    }

    // US-1 · "Names are 1-100 characters, unique among the team's own sprints": ignoring case and surrounding spaces, never
    // against itself, another team may repeat it, and a removed sprint frees it.
    [Fact]
    public async Task Names_AreUniqueInTheTeam_IgnoringCaseAndSpaces()
    {
        var world = await CreateWorldAsync(factory);
        var release = await NewAsync(factory, world, world.PlatformId, "Релиз");

        var duplicate = await CreateAsync(factory, world, world.PlatformId, "  рЕЛИЗ ");
        ProblemAssert.Problem(duplicate, HttpStatusCode.Conflict, SprintErrorCodes.NameTaken);

        var otherTeam = await CreateAsync(factory, world, world.ProjectTeamId, "Релиз");
        Assert.Equal(HttpStatusCode.Created, otherTeam.Status);

        var itself = await SendAsync(factory, HttpMethod.Put, Sprint(world, world.PlatformId, release), new { name = "РЕЛИЗ" });
        Assert.Equal(HttpStatusCode.OK, itself.Status);
        Assert.Equal("РЕЛИЗ", itself.Body.GetProperty("name").GetString());
        Assert.Equal(1, itself.Body.GetProperty("number").GetInt32());

        Assert.Equal(HttpStatusCode.NoContent, (await SendAsync(factory, HttpMethod.Delete, Sprint(world, world.PlatformId, release))).Status);
        Assert.Equal(HttpStatusCode.Created, (await CreateAsync(factory, world, world.PlatformId, "Релиз")).Status);
    }

    [Theory]
    [InlineData("   ")]
    [InlineData("")]
    public async Task Create_WithNoUsableName_TakesTheDefault(string name)
    {
        var world = await CreateWorldAsync(factory);

        var answer = await CreateAsync(factory, world, world.PlatformId, name);

        Assert.Equal("Спринт 1", answer.Body.GetProperty("name").GetString());
    }

    [Fact]
    public async Task Update_WithAnEmptyOrTooLongName_IsAValidationError()
    {
        var world = await CreateWorldAsync(factory);
        var id = await NewAsync(factory, world, world.PlatformId);

        foreach (var name in new[] { "", "   ", new string('ы', SprintLimits.NameMaxLength + 1) })
        {
            var answer = await SendAsync(factory, HttpMethod.Put, Sprint(world, world.PlatformId, id), new { name });
            ProblemAssert.Validation(answer, SprintErrorCodes.ValidationFailed, "name", SprintErrorCodes.NameInvalid);
        }

        var atTheLimit = await SendAsync(
            factory, HttpMethod.Put, Sprint(world, world.PlatformId, id), new { name = new string('ы', SprintLimits.NameMaxLength) });
        Assert.Equal(HttpStatusCode.OK, atTheLimit.Status);
    }

    // US-1 · "Dates are optional but paired, finish not before start", every reason at once and nothing written.
    [Fact]
    public async Task Dates_ArePairedAndNotReversed()
    {
        var world = await CreateWorldAsync(factory);

        ProblemAssert.Validation(
            await CreateAsync(factory, world, world.PlatformId, start: "2026-11-02"), SprintErrorCodes.ValidationFailed, "endDate", SprintErrorCodes.DatesUnpaired);
        ProblemAssert.Validation(
            await CreateAsync(factory, world, world.PlatformId, end: "2026-11-02"), SprintErrorCodes.ValidationFailed, "endDate", SprintErrorCodes.DatesUnpaired);
        ProblemAssert.Validation(
            await CreateAsync(factory, world, world.PlatformId, start: "2026-11-13", end: "2026-11-02"),
            SprintErrorCodes.ValidationFailed,
            "endDate",
            SprintErrorCodes.DatesReversed);

        var oneDay = await CreateAsync(factory, world, world.PlatformId, start: "2026-11-02", end: "2026-11-02");
        Assert.Equal(HttpStatusCode.Created, oneDay.Status);

        // Nothing was written by the refusals, and no number was issued: the first sprint is 1, the one-day sprint.
        Assert.Equal(1, oneDay.Body.GetProperty("number").GetInt32());

        var both = await CreateAsync(factory, world, world.PlatformId, name: "", start: "x", end: "y");
        Assert.Equal(HttpStatusCode.BadRequest, both.Status);
    }

    [Fact]
    public async Task Create_WithAnInvalidNameAndReversedDates_ReportsBothFields()
    {
        var world = await CreateWorldAsync(factory);

        var answer = await SendAsync(
            factory,
            HttpMethod.Post,
            Sprints(world, world.PlatformId),
            new { name = new string('я', 101), startDate = "2026-11-13", endDate = "2026-11-02" });

        Assert.Equal(HttpStatusCode.BadRequest, answer.Status);
        var errors = answer.Body.GetProperty("errors");
        Assert.Equal(SprintErrorCodes.NameInvalid, errors.GetProperty("name")[0].GetProperty("code").GetString());
        Assert.Equal(SprintErrorCodes.DatesReversed, errors.GetProperty("endDate")[0].GetProperty("code").GetString());
    }

    // US-1 · "A started sprint keeps both dates, and a Closed sprint's dates never change".
    [Fact]
    public async Task Update_ByState_FollowsTheDateRules()
    {
        var world = await CreateWorldAsync(factory);
        var planned = await NewAsync(factory, world, world.PlatformId, "Плановый");
        var path = Sprint(world, world.PlatformId, planned);

        var dated = await SendAsync(factory, HttpMethod.Put, path, new { name = "Плановый", startDate = "2026-11-02", endDate = "2026-11-13" });
        Assert.Equal(("2026-11-02", "2026-11-13"), (dated.Body.GetProperty("startDate").GetString(), dated.Body.GetProperty("endDate").GetString()));

        Assert.Equal(HttpStatusCode.OK, (await StartAsync(factory, world, world.PlatformId, planned)).Status);

        ProblemAssert.Problem(
            await SendAsync(factory, HttpMethod.Put, path, new { name = "Плановый" }), HttpStatusCode.Conflict, SprintErrorCodes.ActiveDatesRequired);
        var moved = await SendAsync(factory, HttpMethod.Put, path, new { name = "Плановый", startDate = "2026-11-03", endDate = "2026-11-14" });
        Assert.Equal(HttpStatusCode.OK, moved.Status);

        Assert.Equal(HttpStatusCode.OK, (await CloseAsync(factory, world, world.PlatformId, planned)).Status);

        ProblemAssert.Problem(
            await SendAsync(factory, HttpMethod.Put, path, new { name = "Итог", startDate = "2026-11-03", endDate = "2026-11-15" }),
            HttpStatusCode.Conflict,
            SprintErrorCodes.ClosedDatesLocked);
        var renamed = await SendAsync(factory, HttpMethod.Put, path, new { name = "Итог", startDate = "2026-11-03", endDate = "2026-11-14" });
        Assert.Equal("Итог", renamed.Body.GetProperty("name").GetString());
    }

    // US-2 · "Start": only a Planned sprint with both dates; the team has one Active sprint.
    [Fact]
    public async Task Start_NeedsPlannedBothDatesAndNoOtherActive()
    {
        var world = await CreateWorldAsync(factory);
        var undated = await NewAsync(factory, world, world.PlatformId);
        var first = await NewAsync(factory, world, world.PlatformId, start: "2026-11-02", end: "2026-11-13");
        var second = await NewAsync(factory, world, world.PlatformId, start: "2026-11-16", end: "2026-11-27");

        ProblemAssert.Problem(
            await StartAsync(factory, world, world.PlatformId, undated), HttpStatusCode.Conflict, SprintErrorCodes.StartDatesRequired);

        var started = await StartAsync(factory, world, world.PlatformId, first);
        Assert.Equal("Active", started.Body.GetProperty("state").GetString());

        ProblemAssert.Problem(await StartAsync(factory, world, world.PlatformId, first), HttpStatusCode.Conflict, SprintErrorCodes.NotPlanned);
        ProblemAssert.Problem(await StartAsync(factory, world, world.PlatformId, second), HttpStatusCode.Conflict, SprintErrorCodes.ActiveExists);

        // Another team has its own Active sprint.
        var other = await NewAsync(factory, world, world.ProjectTeamId, start: "2026-11-02", end: "2026-11-13");
        Assert.Equal(HttpStatusCode.OK, (await StartAsync(factory, world, world.ProjectTeamId, other)).Status);

        // Once the first is closed, the second can start.
        Assert.Equal(HttpStatusCode.OK, (await CloseAsync(factory, world, world.PlatformId, first)).Status);
        Assert.Equal(HttpStatusCode.OK, (await StartAsync(factory, world, world.PlatformId, second)).Status);
    }

    // US-2 · two starts at once leave one Active sprint: the unique index decides.
    [Fact]
    public async Task Start_TwoAtOnce_LeaveOneActive()
    {
        var world = await CreateWorldAsync(factory);
        var first = await NewAsync(factory, world, world.PlatformId, start: "2026-11-02", end: "2026-11-13");
        var second = await NewAsync(factory, world, world.PlatformId, start: "2026-11-16", end: "2026-11-27");

        var answers = await Task.WhenAll(
            StartAsync(factory, world, world.PlatformId, first), StartAsync(factory, world, world.PlatformId, second));

        Assert.Equal(1, answers.Count(answer => answer.Status == HttpStatusCode.OK));
        var refused = Assert.Single(answers, answer => answer.Status != HttpStatusCode.OK);
        ProblemAssert.Problem(refused, HttpStatusCode.Conflict, SprintErrorCodes.ActiveExists);
    }

    // US-2 · "Remove": only a Planned sprint with no item outside «Удалена»; its «Удалена» items lose the pointer; the number is
    // never reused.
    [Fact]
    public async Task Remove_OnlyAnEmptyPlannedSprint_AndTheNumberIsNeverReused()
    {
        var world = await CreateWorldAsync(factory);
        var empty = await NewAsync(factory, world, world.PlatformId);
        var held = await NewAsync(factory, world, world.PlatformId);
        var active = await ActiveAsync(factory, world, world.PlatformId, "Активный");
        var item = await ItemAsync(factory, world, "Занят", sprintId: held);

        ProblemAssert.Problem(
            await SendAsync(factory, HttpMethod.Delete, Sprint(world, world.PlatformId, held)), HttpStatusCode.Conflict, SprintErrorCodes.NotRemovable);
        ProblemAssert.Problem(
            await SendAsync(factory, HttpMethod.Delete, Sprint(world, world.PlatformId, active)), HttpStatusCode.Conflict, SprintErrorCodes.NotRemovable);

        Assert.Equal(HttpStatusCode.NoContent, (await SendAsync(factory, HttpMethod.Delete, Sprint(world, world.PlatformId, empty))).Status);
        ProblemAssert.Problem(
            await SendAsync(factory, HttpMethod.Delete, Sprint(world, world.PlatformId, empty)), HttpStatusCode.NotFound, SprintErrorCodes.NotFound);

        // Number 1 was removed; the next sprint is number 4, never 1 again.
        Assert.Equal(4, (await CreateAsync(factory, world, world.PlatformId)).Body.GetProperty("number").GetInt32());

        // The item leaves, deleted: «Удалена» does not hold the sprint, and the removal clears its pointer.
        Assert.Equal(HttpStatusCode.OK, (await DeleteItemAsync(factory, world, item)).Status);
        Assert.Equal(held, await StoredSprintAsync(factory, item));
        Assert.Equal(HttpStatusCode.NoContent, (await SendAsync(factory, HttpMethod.Delete, Sprint(world, world.PlatformId, held))).Status);
        Assert.Null(await StoredSprintAsync(factory, item));
    }

    // US-3 · the list: highest number first, the counts after the rules, 200 a page, `before` the cursor, `open` the open ones.
    [Fact]
    public async Task List_HighestNumberFirst_WithTheirCounts_AndTheOpenFilter()
    {
        var world = await CreateWorldAsync(factory);
        var closed = await ActiveAsync(factory, world, world.PlatformId, "Закрытый", "2026-10-05", "2026-10-16");
        Assert.Equal(HttpStatusCode.OK, (await CloseAsync(factory, world, world.PlatformId, closed)).Status);
        var active = await ActiveAsync(factory, world, world.PlatformId, "Активный", "2026-10-19", "2026-10-30");
        var planned = await NewAsync(factory, world, world.PlatformId, "Плановый");
        await ItemAsync(factory, world, "Первая", 5, sprintId: active);
        await ItemAsync(factory, world, "Вторая", 3, "Закрыта", sprintId: active);
        await ItemAsync(factory, world, "Третья", sprintId: planned);

        var all = (await ReadListAsync(world, "")).Items;
        Assert.Equal(new[] { "Плановый", "Активный", "Закрытый" }, all.Select(row => row.GetProperty("name").GetString()));
        Assert.Equal(new[] { 3, 2, 1 }, all.Select(row => row.GetProperty("number").GetInt32()));

        var activeRow = all[1];
        Assert.Equal((2, 8m), (activeRow.GetProperty("itemCount").GetInt32(), activeRow.GetProperty("storyPoints").GetDecimal()));
        Assert.Equal(System.Text.Json.JsonValueKind.Null, activeRow.GetProperty("closedItems").ValueKind);
        var plannedRow = all[0];
        Assert.Equal((1, 0m), (plannedRow.GetProperty("itemCount").GetInt32(), plannedRow.GetProperty("storyPoints").GetDecimal()));
        Assert.Equal((0, 0m), (all[2].GetProperty("itemCount").GetInt32(), all[2].GetProperty("storyPoints").GetDecimal()));
        Assert.Equal(0, all[2].GetProperty("closedItems").GetInt32());

        Assert.Equal(new[] { 3, 2 }, (await ReadListAsync(world, "?open=true")).Items.Select(row => row.GetProperty("number").GetInt32()));
        Assert.Equal(new[] { 2, 1 }, (await ReadListAsync(world, "?before=3")).Items.Select(row => row.GetProperty("number").GetInt32()));
        Assert.Empty((await ReadListAsync(world, "?before=1")).Items);
        Assert.Equal(3, (await ReadListAsync(world, "?before=zz")).Items.Count);
    }

    // US-3 · "A page is 200 rows", then the cursor: 205 sprints are two pages and nothing is lost between them.
    [Fact]
    public async Task List_PagesOfTwoHundred_WithTheCursor()
    {
        var world = await CreateWorldAsync(factory);
        var teamId = world.PlatformId;
        var organizationId = world.OrganizationId;

        await WorkItemsTestData.ExecuteAsync(factory, $"""
            INSERT INTO sprint_counters (team_id, last_number) VALUES ({teamId}, 205) ON CONFLICT (team_id) DO UPDATE SET last_number = 205
            """);
        await WorkItemsTestData.ExecuteAsync(factory, $"""
            INSERT INTO sprints (id, organization_id, team_id, number, name, normalized_name, state, created_at, created_by)
            SELECT gen_random_uuid(), {organizationId}, {teamId}, n, 'Спринт ' || n, upper('Спринт ' || n), 'Planned', now(), 'seed'
            FROM generate_series(1, 205) AS n
            """);

        var page = await ReadListAsync(world, "");
        Assert.Equal(200, page.Items.Count);
        Assert.Equal(205, page.Items[0].GetProperty("number").GetInt32());
        Assert.Equal(6, page.NextBefore);

        var rest = await ReadListAsync(world, $"?before={page.NextBefore}");
        Assert.Equal(new[] { 5, 4, 3, 2, 1 }, rest.Items.Select(row => row.GetProperty("number").GetInt32()));
        Assert.Null(rest.NextBefore);
    }

    private async Task<(IReadOnlyList<System.Text.Json.JsonElement> Items, int? NextBefore)> ReadListAsync(BoardsWorld world, string query)
    {
        var answer = await SendAsync(factory, HttpMethod.Get, Sprints(world, world.PlatformId) + query);
        Assert.Equal(HttpStatusCode.OK, answer.Status);

        var next = answer.Body.GetProperty("nextBefore");

        return (answer.Body.GetProperty("items").EnumerateArray().ToList(), next.ValueKind == System.Text.Json.JsonValueKind.Null ? null : next.GetInt32());
    }
}
