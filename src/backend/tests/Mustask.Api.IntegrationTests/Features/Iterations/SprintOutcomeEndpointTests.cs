using System.Net;
using System.Text.Json;
using Mustask.Api.IntegrationTests.Features.Boards;
using Mustask.Api.IntegrationTests.Features.WorkItems;
using Mustask.Api.IntegrationTests.Kit;
using static Mustask.Api.IntegrationTests.Features.Iterations.IterationsTestData;

namespace Mustask.Api.IntegrationTests.Features.Iterations;

/// <summary>
/// US-9 and US-10 (mod-007 api-contract § 2.9, BR-14 to BR-16): the dynamics of a Closed sprint against the team's earlier ones,
/// the results by participant with «k из N», and the list of the done items. One world: sprints A (Aug), B (Aug) and C (Sep) of the
/// team «Платформа», each closed with no destination, so the figures are the snapshots taken at their closes.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class SprintOutcomeEndpointTests(MustaskApiFactory factory)
{
    private sealed record Outcome(BoardsWorld World, Guid A, Guid B, Guid C, string Anna, string Boris, Item[] CItems);

    /// <summary>
    /// A holds a done story of 3 points (Anna's) and an open one of 5 (nobody's): 1 of 2 items, 3 of 8 points. B holds one done
    /// story of 2 points. C (the page) holds Anna's done 5 and open 3, Boris's done 2 and nobody's open 1.
    /// </summary>
    private async Task<Outcome> BuildAsync()
    {
        var world = await CreateWorldAsync(factory);
        var anna = await WorkItemsTestData.AddMemberAsync(factory, world.Items, "Анна", "Антонова");
        var boris = await WorkItemsTestData.AddMemberAsync(factory, world.Items, "Борис", "Борисов");

        var a = await ActiveAsync(factory, world, world.PlatformId, "A", "2026-08-03", "2026-08-14");
        await ItemAsync(factory, world, "a1", 3, "Закрыта", anna, a);
        await ItemAsync(factory, world, "a2", 5, sprintId: a);
        Assert.Equal(HttpStatusCode.OK, (await CloseAsync(factory, world, world.PlatformId, a)).Status);

        var b = await ActiveAsync(factory, world, world.PlatformId, "B", "2026-08-17", "2026-08-28");
        await ItemAsync(factory, world, "b1", 2, "Закрыта", sprintId: b);
        Assert.Equal(HttpStatusCode.OK, (await CloseAsync(factory, world, world.PlatformId, b)).Status);

        var c = await ActiveAsync(factory, world, world.PlatformId, "C", "2026-09-07", "2026-09-18");
        var items = new[]
        {
            await ItemAsync(factory, world, "c1", 5, "Закрыта", anna, c),
            await ItemAsync(factory, world, "c2", 3, assignee: anna, sprintId: c),
            await ItemAsync(factory, world, "c3", 2, "Закрыта", boris, c),
            await ItemAsync(factory, world, "c4", 1, sprintId: c),
        };
        Assert.Equal(HttpStatusCode.OK, (await CloseAsync(factory, world, world.PlatformId, c)).Status);

        return new Outcome(world, a, b, c, anna, boris, items);
    }

    private static decimal? Num(JsonElement element) => element.ValueKind == JsonValueKind.Null ? null : element.GetDecimal();

    private static (decimal?, decimal?, decimal?) Fig(JsonElement figure) =>
        (Num(figure.GetProperty("min")), Num(figure.GetProperty("average")), Num(figure.GetProperty("max")));

    // US-9 · the default range is the 90 days before the sprint's start; the current sprint's row and the earlier ones' rows.
    [Fact]
    public async Task Dynamics_DefaultRange_RowsAndSummary_RoundedOnceHalfUp()
    {
        var o = await BuildAsync();

        var page = await ReadAsync(factory, o.World, o.World.PlatformId, o.C, "/dynamics");

        var body = page.Body;
        Assert.Equal(("stories", "2026-06-09", "2026-09-06"), (body.GetProperty("level").GetString(), body.GetProperty("from").GetString(), body.GetProperty("to").GetString()));
        Assert.Equal((2, 2, false), (body.GetProperty("rangeSprints").GetInt32(), body.GetProperty("shownSprints").GetInt32(), body.GetProperty("truncated").GetBoolean()));

        var current = body.GetProperty("current");
        Assert.Equal((4, 11m, 2, 50m, 7m, 64m), (
            current.GetProperty("items").GetInt32(),
            current.GetProperty("storyPoints").GetDecimal(),
            current.GetProperty("closedItems").GetInt32(),
            current.GetProperty("closedItemsPercent").GetDecimal(),
            current.GetProperty("closedStoryPoints").GetDecimal(),
            current.GetProperty("closedStoryPointsPercent").GetDecimal()));

        // Latest end first: B, then A. A's 3 of 8 points is 37.5 %, shown 38.
        var rows = body.GetProperty("sprints").EnumerateArray().ToList();
        Assert.Equal(["B", "A"], rows.Select(row => row.GetProperty("name").GetString()));
        Assert.Equal((1, 100m), (rows[0].GetProperty("items").GetInt32(), rows[0].GetProperty("closedStoryPointsPercent").GetDecimal()));
        Assert.Equal((2, 38m, 50m), (rows[1].GetProperty("items").GetInt32(), rows[1].GetProperty("closedStoryPointsPercent").GetDecimal(), rows[1].GetProperty("closedItemsPercent").GetDecimal()));

        // The summary is over the earlier sprints, averages exact and rounded once: SP percents 37.5 and 100 average 68.75 -> 69.
        var summary = body.GetProperty("summary");
        Assert.Equal((1m, 1.0m, 1m), Fig(summary.GetProperty("closedItems")));
        Assert.Equal((50m, 75m, 100m), Fig(summary.GetProperty("closedItemsPercent")));
        Assert.Equal((2m, 2.5m, 3m), Fig(summary.GetProperty("closedStoryPoints")));
        Assert.Equal((38m, 69m, 100m), Fig(summary.GetProperty("closedStoryPointsPercent")));
    }

    // US-9 · an explicit range takes the sprints whose end date lies in it; half of a pair is the default.
    [Fact]
    public async Task Dynamics_ExplicitRange_TakesTheSprintsEndingInIt()
    {
        var o = await BuildAsync();

        var only = await ReadAsync(factory, o.World, o.World.PlatformId, o.C, "/dynamics?from=2026-08-01&to=2026-08-20");
        Assert.Equal(["A"], only.Body.GetProperty("sprints").EnumerateArray().Select(row => row.GetProperty("name").GetString()));
        Assert.Equal(1, only.Body.GetProperty("rangeSprints").GetInt32());

        var none = await ReadAsync(factory, o.World, o.World.PlatformId, o.C, "/dynamics?from=2026-01-01&to=2026-01-31");
        Assert.Empty(none.Body.GetProperty("sprints").EnumerateArray());
        Assert.Equal((null, null, null), Fig(none.Body.GetProperty("summary").GetProperty("closedItems")));
        Assert.Equal((null, null, null), Fig(none.Body.GetProperty("summary").GetProperty("closedStoryPointsPercent")));

        var half = await ReadAsync(factory, o.World, o.World.PlatformId, o.C, "/dynamics?from=2026-08-01");
        Assert.Equal("2026-06-09", half.Body.GetProperty("from").GetString());
    }

    // US-9 · the level: another level shows its own (here empty) figures; an unknown one is 404.
    [Fact]
    public async Task Dynamics_AtALevelWithNoItems_IsEmptyNotAnError_AndAnUnknownLevelIs404()
    {
        var o = await BuildAsync();

        var epics = await ReadAsync(factory, o.World, o.World.PlatformId, o.C, "/dynamics?level=epics");
        Assert.Equal(0, epics.Body.GetProperty("current").GetProperty("items").GetInt32());
        Assert.Equal(JsonValueKind.Null, epics.Body.GetProperty("current").GetProperty("closedItemsPercent").ValueKind);
        // A sprint holding nothing counts as 0 in the count figures and in no percent.
        Assert.Equal((0m, 0m, 0m), Fig(epics.Body.GetProperty("summary").GetProperty("closedItems")));
        Assert.Equal((null, null, null), Fig(epics.Body.GetProperty("summary").GetProperty("closedItemsPercent")));

        var unknown = await SendAsync(factory, HttpMethod.Get, $"{Sprint(o.World, o.World.PlatformId, o.C)}/dynamics?level=bogus");
        ProblemAssert.Problem(unknown, HttpStatusCode.NotFound, "boards.level.not_found");
    }

    // BR-15 · at most 20 sprints are read, the latest by end date; the answer says it was cut.
    [Fact]
    public async Task Dynamics_MoreThanTwentySprintsInTheRange_ReadsTheLatestTwenty_AndSaysSo()
    {
        var o = await BuildAsync();
        await WorkItemsTestData.ExecuteAsync(factory, $"""
            INSERT INTO sprints (id, organization_id, team_id, number, name, normalized_name, state, start_date, end_date, created_at, created_by, closed_at, closed_by)
            SELECT gen_random_uuid(), {o.World.OrganizationId}, {o.World.PlatformId}, 100 + n, 'S' || n, 'S' || n, 'Closed',
                   DATE '2026-07-01' + n, DATE '2026-07-01' + n, now(), 'seed', now(), 'seed'
            FROM generate_series(1, 20) AS n
            """);

        var page = await ReadAsync(factory, o.World, o.World.PlatformId, o.C, "/dynamics");

        Assert.Equal((22, 20, true), (page.Body.GetProperty("rangeSprints").GetInt32(), page.Body.GetProperty("shownSprints").GetInt32(), page.Body.GetProperty("truncated").GetBoolean()));
        var names = page.Body.GetProperty("sprints").EnumerateArray().Select(row => row.GetProperty("name").GetString()).ToList();
        Assert.Equal("B", names[0]);
        Assert.Contains("A", names);
        Assert.DoesNotContain("S1", names);
        Assert.DoesNotContain("S2", names);
    }

    // US-10 · the participants: by full name, «Без исполнителя» last, the total equal to the sum, «k из N».
    [Fact]
    public async Task Participants_RowsTotalAndKOfN()
    {
        var o = await BuildAsync();

        var page = await ReadAsync(factory, o.World, o.World.PlatformId, o.C, "/participants");

        var body = page.Body;
        Assert.Equal((2, 3), (body.GetProperty("rangeSprints").GetInt32(), body.GetProperty("totalRows").GetInt32()));
        var rows = body.GetProperty("rows").EnumerateArray().ToList();
        Assert.Equal(3, rows.Count);
        Assert.Equal(["Анна Антонова", "Борис Борисов"], rows.Take(2).Select(row => row.GetProperty("displayName").GetString()));
        Assert.Equal(JsonValueKind.Null, rows[2].GetProperty("userId").ValueKind);

        var anna = rows[0];
        Assert.Equal(o.Anna, anna.GetProperty("userId").GetString());
        Assert.Equal((2, 8m, 5m, 63m, 1, 50m), (
            anna.GetProperty("items").GetInt32(),
            anna.GetProperty("storyPoints").GetDecimal(),
            anna.GetProperty("closedStoryPoints").GetDecimal(),
            anna.GetProperty("closedStoryPointsPercent").GetDecimal(),
            anna.GetProperty("closedItems").GetInt32(),
            anna.GetProperty("closedItemsPercent").GetDecimal()));
        // Anna held items in A only: 1 of the 2 sprints, and her figures run over A.
        Assert.Equal(1, anna.GetProperty("comparedSprints").GetInt32());
        Assert.Equal((1m, 1.0m, 1m), Fig(anna.GetProperty("dynamics").GetProperty("closedItems")));
        Assert.Equal((100m, 100m, 100m), Fig(anna.GetProperty("dynamics").GetProperty("closedItemsPercent")));

        var boris = rows[1];
        Assert.Equal(0, boris.GetProperty("comparedSprints").GetInt32());
        Assert.Equal((null, null, null), Fig(boris.GetProperty("dynamics").GetProperty("closedItems")));

        var total = body.GetProperty("total");
        Assert.Equal((4, 11m, 7m, 64m, 2, 50m), (
            total.GetProperty("items").GetInt32(),
            total.GetProperty("storyPoints").GetDecimal(),
            total.GetProperty("closedStoryPoints").GetDecimal(),
            total.GetProperty("closedStoryPointsPercent").GetDecimal(),
            total.GetProperty("closedItems").GetInt32(),
            total.GetProperty("closedItemsPercent").GetDecimal()));
        Assert.Equal(JsonValueKind.Null, total.GetProperty("userId").ValueKind);
        Assert.Equal(total.GetProperty("items").GetInt32(), rows.Sum(row => row.GetProperty("items").GetInt32()));
    }

    // BR-16 · rows follow the assignee at the close: a person who left the organization keeps the last known name, marked.
    [Fact]
    public async Task Participants_APersonWhoLeft_KeepsTheirRow_MarkedNotAMember()
    {
        var o = await BuildAsync();
        await WorkItemsTestData.ExecuteAsync(factory, $"DELETE FROM organization_memberships WHERE organization_id = {o.World.OrganizationId} AND user_id = {o.Boris}");

        var page = await ReadAsync(factory, o.World, o.World.PlatformId, o.C, "/participants");

        var rows = page.Body.GetProperty("rows").EnumerateArray().ToList();
        Assert.Equal(["Анна Антонова", "Борис Борисов"], rows.Take(2).Select(row => row.GetProperty("displayName").GetString()));
        Assert.True(rows[0].GetProperty("isMember").GetBoolean());
        Assert.False(rows[1].GetProperty("isMember").GetBoolean());
    }

    // US-10 · paging: 200 rows a page, the total the same on every page.
    [Fact]
    public async Task Participants_APageBeyondTheRows_IsEmpty_WithTheSameTotal()
    {
        var o = await BuildAsync();

        var page = await ReadAsync(factory, o.World, o.World.PlatformId, o.C, "/participants?offset=3");

        Assert.Empty(page.Body.GetProperty("rows").EnumerateArray());
        Assert.Equal(3, page.Body.GetProperty("totalRows").GetInt32());
        Assert.Equal(4, page.Body.GetProperty("total").GetProperty("items").GetInt32());
    }

    // US-10 · the done items, whole, by project key then number; a purged item keeps its line without a title.
    [Fact]
    public async Task Items_AreTheDoneOnes_AndAPurgedOneKeepsItsLine()
    {
        var o = await BuildAsync();

        var page = await ReadAsync(factory, o.World, o.World.PlatformId, o.C, "/items");

        Assert.Equal((2, 7m), (page.Body.GetProperty("totalItems").GetInt32(), page.Body.GetProperty("totalStoryPoints").GetDecimal()));
        var rows = page.Body.GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(["c1", "c3"], rows.Select(row => row.GetProperty("title").GetString()));
        Assert.Equal([o.CItems[0].Identifier, o.CItems[2].Identifier], rows.Select(row => row.GetProperty("identifier").GetString()));
        Assert.All(rows, row => Assert.False(row.GetProperty("purged").GetBoolean()));
        Assert.Equal(o.World.ProjectId, rows[0].GetProperty("project").GetProperty("id").GetGuid());

        Assert.Equal(HttpStatusCode.OK, (await DeleteItemAsync(factory, o.World, o.CItems[0])).Status);
        Assert.Equal(HttpStatusCode.NoContent, (await PurgeItemAsync(factory, o.World, o.CItems[0])).Status);

        var after = await ReadAsync(factory, o.World, o.World.PlatformId, o.C, "/items");
        Assert.Equal((2, 7m), (after.Body.GetProperty("totalItems").GetInt32(), after.Body.GetProperty("totalStoryPoints").GetDecimal()));
        var purged = after.Body.GetProperty("items").EnumerateArray().Single(row => row.GetProperty("purged").GetBoolean());
        Assert.Equal(JsonValueKind.Null, purged.GetProperty("title").ValueKind);
        Assert.Equal(5m, purged.GetProperty("storyPoints").GetDecimal());
    }

    // BR-9 · an item of a deactivated project counts on no figure until the project is restored.
    [Fact]
    public async Task ADeactivatedProjectsItems_CountNowhere()
    {
        var o = await BuildAsync();
        await WorkItemsTestData.ExecuteAsync(factory, $"UPDATE projects SET status = 'Deactivated' WHERE id = {o.World.ProjectId}");

        var items = await ReadAsync(factory, o.World, o.World.PlatformId, o.C, "/items");
        var dynamics = await ReadAsync(factory, o.World, o.World.PlatformId, o.C, "/dynamics");

        Assert.Equal(0, items.Body.GetProperty("totalItems").GetInt32());
        Assert.Equal(0, dynamics.Body.GetProperty("current").GetProperty("items").GetInt32());
    }

    // api-contract § 2.9 · only a Closed sprint has these pages; an unknown team or sprint is 404.
    [Fact]
    public async Task Pages_OfASprintThatIsNotClosed_AreNotFound()
    {
        var world = await CreateWorldAsync(factory);
        var active = await ActiveAsync(factory, world, world.PlatformId);

        foreach (var tail in new[] { "/dynamics", "/participants", "/items" })
        {
            ProblemAssert.Problem(
                await SendAsync(factory, HttpMethod.Get, Sprint(world, world.PlatformId, active) + tail),
                HttpStatusCode.NotFound,
                "iterations.sprint.not_found");
        }

        var otherTeam = await SendAsync(factory, HttpMethod.Get, Sprint(world, world.ProjectTeamId, active) + "/dynamics");
        Assert.Equal(HttpStatusCode.NotFound, otherTeam.Status);
    }
}
