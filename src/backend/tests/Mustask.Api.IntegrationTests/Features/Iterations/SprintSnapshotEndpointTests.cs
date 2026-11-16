using System.Net;
using System.Text.Json;
using Mustask.Api.IntegrationTests.Features.Boards;
using Mustask.Api.IntegrationTests.Features.Organizations;
using Mustask.Api.IntegrationTests.Features.WorkItems;
using Mustask.Api.IntegrationTests.Kit;
using Mustask.Domain.Iterations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using static Mustask.Api.IntegrationTests.Features.Iterations.IterationsTestData;

namespace Mustask.Api.IntegrationTests.Features.Iterations;

/// <summary>
/// QA of mod-007 (BR-12, BR-14, BR-15, BR-16): what the close snapshot holds and keeps, the date range of the dynamics, the figures
/// of a person over several sprints, the level filter, the pointers a team's unlink or deletion clears, and the races of the close.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class SprintSnapshotEndpointTests(MustaskApiFactory factory)
{
    private async Task<Guid> ClosedAsync(BoardsWorld world, Guid teamId, string name, string start, string end, Func<Guid, Task>? fill = null)
    {
        var id = await ActiveAsync(factory, world, teamId, name, start, end);

        if (fill is not null)
        {
            await fill(id);
        }

        Assert.Equal(HttpStatusCode.OK, (await CloseAsync(factory, world, teamId, id)).Status);

        return id;
    }

    /// <summary>The snapshot's rows as the table holds them: item, project, level, assignee, points, done.</summary>
    private async Task<List<string>> SnapshotAsync(Guid sprintId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<Mustask.Infrastructure.MustaskDbContext>();

        return await context.Database
            .SqlQuery<string>(
                $"""
                 SELECT concat_ws('|', work_item_id::text, project_id::text, level_id::text, coalesce(assignee_id, '-'), story_points::text, is_done::text) AS "Value"
                 FROM sprint_snapshot_items WHERE sprint_id = {sprintId} ORDER BY work_item_id
                 """)
            .ToListAsync();
    }

    private static decimal? Num(JsonElement element) => element.ValueKind == JsonValueKind.Null ? null : element.GetDecimal();

    private static (decimal?, decimal?, decimal?) Fig(JsonElement figure) =>
        (Num(figure.GetProperty("min")), Num(figure.GetProperty("average")), Num(figure.GetProperty("max")));

    // BR-14 · every item outside «Удалена», done and moved alike, with project, level, points as then (none = 0), assignee (none = none)
    // and the done flag; nothing of a deleted item; and nothing that happens to the items afterwards changes a line.
    [Fact]
    public async Task Snapshot_HoldsEveryItemOutsideDeleted_WithItsFacts_AndKeepsThemWhateverHappensAfter()
    {
        var world = await CreateWorldAsync(factory);
        var anna = await WorkItemsTestData.AddMemberAsync(factory, world.Items, "Анна", "Антонова");
        var active = await ActiveAsync(factory, world, world.PlatformId);
        var next = await NewAsync(factory, world, world.PlatformId, "Следующий");
        var story = await ItemAsync(factory, world, "История", 3, "Закрыта", anna, active);
        var bare = await ItemAsync(factory, world, "Без очков", sprintId: active);
        var feature = await ItemAsync(factory, world, "Фича", sprintId: active, typeKey: "FEAT");
        var deleted = await ItemAsync(factory, world, "Удалённая", 8, "Закрыта", sprintId: active);
        Assert.Equal(HttpStatusCode.OK, (await DeleteItemAsync(factory, world, deleted)).Status);

        Assert.Equal(HttpStatusCode.OK, (await CloseAsync(factory, world, world.PlatformId, active, next)).Status);

        var rows = await SnapshotAsync(active);
        Assert.Equal(3, rows.Count);
        string[] Of(Item item) => rows.Single(row => row.StartsWith(item.Id.ToString(), StringComparison.Ordinal)).Split('|');

        Assert.Equal((world.ProjectId.ToString(), anna, 3m, "true"), (Of(story)[1], Of(story)[3], decimal.Parse(Of(story)[4], System.Globalization.CultureInfo.InvariantCulture), Of(story)[5]));
        Assert.Equal(("-", 0m, "false"), (Of(bare)[3], decimal.Parse(Of(bare)[4], System.Globalization.CultureInfo.InvariantCulture), Of(bare)[5]));
        Assert.Equal((0m, "false"), (decimal.Parse(Of(feature)[4], System.Globalization.CultureInfo.InvariantCulture), Of(feature)[5]));
        Assert.NotEqual(Of(story)[2], Of(feature)[2]);
        Assert.Equal(Of(story)[2], Of(bare)[2]);

        var before = await ReadAsync(factory, world, world.PlatformId, active, "/participants");
        Assert.Equal(HttpStatusCode.OK, (await BoardsTestData.SetStateAsync(factory, world, story, "US", "Новая")).Status);
        Assert.Equal(HttpStatusCode.OK, (await BoardsTestData.SetStateAsync(factory, world, bare, "US", "Закрыта")).Status);
        Assert.Equal(HttpStatusCode.OK, (await WorkItemsTestData.SaveAsync(factory, world.Items, story.Identifier, new { storyPoints = new { value = 13m } })).Status);
        Assert.Equal(HttpStatusCode.OK, (await WorkItemsTestData.SaveAsync(factory, world.Items, story.Identifier, new { assignee = new { userId = (string?)null } })).Status);
        Assert.Equal(HttpStatusCode.OK, (await WorkItemsTestData.SaveAsync(factory, world.Items, bare.Identifier, new { team = new { teamId = world.ProjectTeamId.ToString() } })).Status);

        Assert.Equal(rows, await SnapshotAsync(active));
        var after = await ReadAsync(factory, world, world.PlatformId, active, "/participants");
        Assert.Equal(before.Body.GetProperty("total").ToString(), after.Body.GetProperty("total").ToString());
        Assert.Equal(before.Body.GetProperty("rows").ToString(), after.Body.GetProperty("rows").ToString());
    }

    // BR-14 · a purge keeps the line in every figure; a deactivated project's items count again once it is restored.
    [Fact]
    public async Task Snapshot_SurvivesAPurge_AndARestoredProjectCountsAgain()
    {
        var world = await CreateWorldAsync(factory);
        Item? item = null;
        var sprint = await ClosedAsync(world, world.PlatformId, "A", "2026-08-03", "2026-08-14", async id =>
            item = await ItemAsync(factory, world, "Готова", 4, "Закрыта", sprintId: id));

        Assert.Equal(HttpStatusCode.OK, (await DeleteItemAsync(factory, world, item!)).Status);
        Assert.Equal(HttpStatusCode.NoContent, (await PurgeItemAsync(factory, world, item!)).Status);

        Assert.Single(await SnapshotAsync(sprint));
        var participants = await ReadAsync(factory, world, world.PlatformId, sprint, "/participants");
        Assert.Equal((1, 4m, 1), (
            participants.Body.GetProperty("total").GetProperty("items").GetInt32(),
            participants.Body.GetProperty("total").GetProperty("storyPoints").GetDecimal(),
            participants.Body.GetProperty("total").GetProperty("closedItems").GetInt32()));

        await WorkItemsTestData.ExecuteAsync(factory, $"UPDATE projects SET status = 'Deactivated' WHERE id = {world.ProjectId}");
        Assert.Equal(0, (await ReadAsync(factory, world, world.PlatformId, sprint, "/participants")).Body.GetProperty("total").GetProperty("items").GetInt32());
        await WorkItemsTestData.ExecuteAsync(factory, $"UPDATE projects SET status = 'Active' WHERE id = {world.ProjectId}");
        Assert.Equal(1, (await ReadAsync(factory, world, world.PlatformId, sprint, "/participants")).Body.GetProperty("total").GetProperty("items").GetInt32());
    }

    // BR-15 · both bounds inclusive, this sprint never in its own baseline, other teams' and unfinished sprints never in it;
    // a pair that is reversed, half, or not a date is the default.
    [Fact]
    public async Task Range_BoundsAreInclusive_TheCurrentAndForeignSprintsAreNeverIn_AndAnUnusablePairIsTheDefault()
    {
        var world = await CreateWorldAsync(factory);
        await ClosedAsync(world, world.PlatformId, "A", "2026-08-03", "2026-08-14");
        await ClosedAsync(world, world.PlatformId, "B", "2026-08-17", "2026-08-28");
        var current = await ClosedAsync(world, world.PlatformId, "C", "2026-09-07", "2026-09-18");
        await ClosedAsync(world, world.ProjectTeamId, "O", "2026-08-10", "2026-08-20");
        await NewAsync(factory, world, world.PlatformId, "P", "2026-08-18", "2026-08-22");
        await ActiveAsync(factory, world, world.PlatformId, "D", "2026-08-18", "2026-08-21");

        async Task<string[]> NamesAsync(string query)
        {
            var answer = await ReadAsync(factory, world, world.PlatformId, current, "/dynamics" + query);

            return answer.Body.GetProperty("sprints").EnumerateArray().Select(row => row.GetProperty("name").GetString()!).ToArray();
        }

        Assert.Equal(["B", "A"], await NamesAsync("?from=2026-08-14&to=2026-08-28"));
        Assert.Empty(await NamesAsync("?from=2026-08-15&to=2026-08-27"));
        Assert.Equal(["A"], await NamesAsync("?from=2026-08-14&to=2026-08-14"));
        Assert.Equal(["B"], await NamesAsync("?from=2026-08-28&to=2026-09-18"));
        Assert.Equal(["B", "A"], await NamesAsync("?from=2026-01-01&to=2026-12-31"));
        var participants = await ReadAsync(factory, world, world.PlatformId, current, "/participants?from=2026-01-01&to=2026-12-31");
        Assert.Equal(2, participants.Body.GetProperty("rangeSprints").GetInt32());

        foreach (var unusable in new[] { "?from=2026-09-01&to=2026-08-01", "?from=abc&to=2026-08-01", "?from=2026-02-30&to=2026-08-01", "?to=2026-08-01", "?from=&to=" })
        {
            foreach (var page in new[] { "/dynamics", "/participants" })
            {
                var answer = await ReadAsync(factory, world, world.PlatformId, current, page + unusable);
                Assert.Equal(("2026-06-09", "2026-09-06"), (answer.Body.GetProperty("from").GetString(), answer.Body.GetProperty("to").GetString()));
            }
        }
    }

    // BR-15, BR-16 · a person's min, average and maximum run over the range sprints where they held items: the mean of the sprints'
    // percents, exact, shown half up (0 % and 25 % average 12.5 -> 13); a sprint where the person's story points are 0 is out of the
    // points percent; «k из N» counts the sprints where they held items.
    [Fact]
    public async Task Participants_FiguresOverSeveralSprints_AreTheMeanOfPercents_HalfUp_WithKOfN()
    {
        var world = await CreateWorldAsync(factory);
        var anna = await WorkItemsTestData.AddMemberAsync(factory, world.Items, "Анна", "Антонова");
        var carla = await WorkItemsTestData.AddMemberAsync(factory, world.Items, "Карла", "Карлова");
        var boris = await WorkItemsTestData.AddMemberAsync(factory, world.Items, "Борис", "Борисов");

        await ClosedAsync(world, world.PlatformId, "S1", "2026-08-03", "2026-08-14", async id =>
        {
            await ItemAsync(factory, world, "a1", 1, assignee: anna, sprintId: id);
            await ItemAsync(factory, world, "a2", 1, assignee: anna, sprintId: id);
            await ItemAsync(factory, world, "c1", stateName: "Закрыта", assignee: carla, sprintId: id);
        });
        await ClosedAsync(world, world.PlatformId, "S2", "2026-08-17", "2026-08-28", async id =>
        {
            await ItemAsync(factory, world, "a3", 1, "Закрыта", anna, id);
            await ItemAsync(factory, world, "a4", 1, assignee: anna, sprintId: id);
            await ItemAsync(factory, world, "a5", 1, assignee: anna, sprintId: id);
            await ItemAsync(factory, world, "a6", 1, assignee: anna, sprintId: id);
        });
        var current = await ClosedAsync(world, world.PlatformId, "S3", "2026-09-07", "2026-09-18", async id =>
        {
            await ItemAsync(factory, world, "a7", 1, assignee: anna, sprintId: id);
            await ItemAsync(factory, world, "c2", 2, "Закрыта", carla, id);
            await ItemAsync(factory, world, "b1", 1, "Закрыта", boris, id);
        });

        var page = await ReadAsync(factory, world, world.PlatformId, current, "/participants");

        Assert.Equal(2, page.Body.GetProperty("rangeSprints").GetInt32());
        var rows = page.Body.GetProperty("rows").EnumerateArray().ToDictionary(row => row.GetProperty("userId").GetString()!);

        var a = rows[anna];
        Assert.Equal(2, a.GetProperty("comparedSprints").GetInt32());
        Assert.Equal((0m, 0.5m, 1m), Fig(a.GetProperty("dynamics").GetProperty("closedItems")));
        Assert.Equal((0m, 13m, 25m), Fig(a.GetProperty("dynamics").GetProperty("closedItemsPercent")));
        Assert.Equal((0m, 0.5m, 1m), Fig(a.GetProperty("dynamics").GetProperty("closedStoryPoints")));
        Assert.Equal((0m, 13m, 25m), Fig(a.GetProperty("dynamics").GetProperty("closedStoryPointsPercent")));

        var c = rows[carla];
        Assert.Equal(1, c.GetProperty("comparedSprints").GetInt32());
        Assert.Equal((1m, 1.0m, 1m), Fig(c.GetProperty("dynamics").GetProperty("closedItems")));
        Assert.Equal((100m, 100m, 100m), Fig(c.GetProperty("dynamics").GetProperty("closedItemsPercent")));
        Assert.Equal((0m, 0.0m, 0m), Fig(c.GetProperty("dynamics").GetProperty("closedStoryPoints")));
        Assert.Equal((null, null, null), Fig(c.GetProperty("dynamics").GetProperty("closedStoryPointsPercent")));

        var b = rows[boris];
        Assert.Equal(0, b.GetProperty("comparedSprints").GetInt32());
        Assert.Equal((null, null, null), Fig(b.GetProperty("dynamics").GetProperty("closedItemsPercent")));
    }

    // BR-14, OQ-14 · every figure is of the chosen level; the done list stays whole.
    [Fact]
    public async Task Level_FiltersEveryFigure_ButNotTheOutcomeList()
    {
        var world = await CreateWorldAsync(factory);
        await ClosedAsync(world, world.PlatformId, "A", "2026-08-03", "2026-08-14", async id =>
        {
            await ItemAsync(factory, world, "f0", stateName: "Закрыта", sprintId: id, typeKey: "FEAT");
            await ItemAsync(factory, world, "f1", sprintId: id, typeKey: "FEAT");
        });
        var current = await ClosedAsync(world, world.PlatformId, "C", "2026-09-07", "2026-09-18", async id =>
        {
            await ItemAsync(factory, world, "u1", 3, "Закрыта", sprintId: id);
            await ItemAsync(factory, world, "f2", stateName: "Закрыта", sprintId: id, typeKey: "FEAT");
            await ItemAsync(factory, world, "e1", sprintId: id, typeKey: "EPIC");
        });

        async Task<JsonElement> DynamicsAsync(string level) => (await ReadAsync(factory, world, world.PlatformId, current, "/dynamics?level=" + level)).Body;

        var stories = await DynamicsAsync("stories");
        var features = await DynamicsAsync("features");
        var epics = await DynamicsAsync("epics");

        (int, decimal, int) Current(JsonElement body) =>
            (body.GetProperty("current").GetProperty("items").GetInt32(), body.GetProperty("current").GetProperty("storyPoints").GetDecimal(), body.GetProperty("current").GetProperty("closedItems").GetInt32());

        Assert.Equal((1, 3m, 1), Current(stories));
        Assert.Equal((1, 0m, 1), Current(features));
        Assert.Equal((1, 0m, 0), Current(epics));
        Assert.Equal(("features", "epics"), (features.GetProperty("level").GetString(), epics.GetProperty("level").GetString()));

        // The earlier sprint held two features and no story: stories see it as held-0, features as 50 %.
        Assert.Equal(0, stories.GetProperty("sprints")[0].GetProperty("items").GetInt32());
        Assert.Equal(JsonValueKind.Null, stories.GetProperty("sprints")[0].GetProperty("closedItemsPercent").ValueKind);
        Assert.Equal(50m, features.GetProperty("sprints")[0].GetProperty("closedItemsPercent").GetDecimal());

        var items = await ReadAsync(factory, world, world.PlatformId, current, "/items?level=epics");
        Assert.Equal(2, items.Body.GetProperty("totalItems").GetInt32());
    }

    // BR-2, BR-8 · the removal of a Planned sprint, the unlink and the deletion of the team clear the pointers they reassign, a Closed
    // sprint's included; the snapshot keeps its rows; a deleted team has no page.
    [Fact]
    public async Task UnlinkAndDeletionOfTheTeam_ClearThePointers_ClosedIncluded_AndTheRemovalClearsDeletedItems()
    {
        var world = await CreateWorldAsync(factory);
        var done = await ItemAsync(factory, world, "Готова", 2, "Закрыта");
        var held = await ActiveAsync(factory, world, world.PlatformId, "B", "2026-08-17", "2026-08-28");
        Assert.Equal(HttpStatusCode.OK, (await SetSprintAsync(factory, world, done, held)).Status);
        Assert.Equal(HttpStatusCode.OK, (await CloseAsync(factory, world, world.PlatformId, held)).Status);
        var planned = await NewAsync(factory, world, world.PlatformId, "Планируемый");
        var open = await ItemAsync(factory, world, "Открыта", sprintId: planned);
        Assert.Equal(held, await StoredSprintAsync(factory, done));

        Assert.Equal(HttpStatusCode.NoContent, (await TeamTestData.UnlinkAsync(factory, world.OrganizationId, world.ProjectId, world.PlatformId, TeamTestData.To(world.ProjectTeamId))).Status);

        Assert.Null(await StoredSprintAsync(factory, done));
        Assert.Null(await StoredSprintAsync(factory, open));
        Assert.Single(await SnapshotAsync(held));

        var other = await CreateWorldAsync(factory);
        var sprint = await ActiveAsync(factory, other, other.PlatformId, "A", "2026-08-03", "2026-08-14");
        var finished = await ItemAsync(factory, other, "Готова", 2, "Закрыта", sprintId: sprint);
        Assert.Equal(HttpStatusCode.OK, (await CloseAsync(factory, other, other.PlatformId, sprint)).Status);
        Assert.Equal(sprint, await StoredSprintAsync(factory, finished));

        await OrganizationsTestData.DeleteTeamAsync(factory, other.ProjectId, other.PlatformId);

        Assert.Null(await StoredSprintAsync(factory, finished));
        Assert.Single(await SnapshotAsync(sprint));
        var page = await SendAsync(factory, HttpMethod.Get, $"{Sprint(other, other.PlatformId, sprint)}/dynamics");
        ProblemAssert.Problem(page, HttpStatusCode.NotFound, "organizations.team.not_found");

        // A Planned sprint holding only an item in «Удалена» is removable and the item loses its pointer.
        var third = await CreateWorldAsync(factory);
        var removable = await NewAsync(factory, third, third.PlatformId);
        var gone = await ItemAsync(factory, third, "Удаляемая", sprintId: removable);
        Assert.Equal(HttpStatusCode.OK, (await DeleteItemAsync(factory, third, gone)).Status);
        Assert.Equal(HttpStatusCode.NoContent, (await SendAsync(factory, HttpMethod.Delete, Sprint(third, third.PlatformId, removable))).Status);
        Assert.Null(await StoredSprintAsync(factory, gone));
    }

    // BR-12 · a state save that comes while the close holds its locks is not in the snapshot; one that was there first is.
    [Fact]
    public async Task Close_RacingAStateSave_TheSnapshotHoldsWhatWasCommittedBeforeTheCloseLock()
    {
        var world = await CreateWorldAsync(factory);
        var first = await ActiveAsync(factory, world, world.PlatformId, "A", "2026-08-03", "2026-08-14");
        var next = await NewAsync(factory, world, world.PlatformId, "Следующий");
        var item = await ItemAsync(factory, world, "Новая", 2, sprintId: first);

        var (close, save) = await Race.TwoAsync(
            factory,
            gate => CloseAsync(factory, world, world.PlatformId, first, next, gate: gate),
            () => BoardsTestData.SetStateAsync(factory, world, item, "US", "Закрыта"));

        Assert.Equal((HttpStatusCode.OK, HttpStatusCode.OK), (close.Status, save.Status));
        Assert.EndsWith("|false", Assert.Single(await SnapshotAsync(first)));
        Assert.Equal(next, await StoredSprintAsync(factory, item));

        var second = await ActiveAsync(factory, world, world.PlatformId, "B", "2026-08-17", "2026-08-28");
        var other = await ItemAsync(factory, world, "Другая", 2, sprintId: second);
        var done = world.Items.Types["US"].StateId("Закрыта");

        var (saved, closed) = await Race.TwoAsync(
            factory,
            gate => WorkItemsTestData.SaveAsync(factory, world.Items, other.Identifier, new { stateId = done.ToString() }, gate),
            () => CloseAsync(factory, world, world.PlatformId, second, null));

        Assert.Equal((HttpStatusCode.OK, HttpStatusCode.OK), (saved.Status, closed.Status));
        Assert.EndsWith("|true", Assert.Single(await SnapshotAsync(second)));
        Assert.Equal(second, await StoredSprintAsync(factory, other));
    }

    // BR-12 · the destination's removal and the close meet in one order, whichever comes first, and nothing ends in a 500.
    [Fact]
    public async Task Close_RacingTheRemovalOfItsDestination_EndsInOneOrder()
    {
        var world = await CreateWorldAsync(factory);
        var active = await ActiveAsync(factory, world, world.PlatformId);
        var next = await NewAsync(factory, world, world.PlatformId, "Следующий");
        var item = await ItemAsync(factory, world, "Новая", sprintId: active);

        var (close, removal) = await Race.TwoAsync(
            factory,
            gate => CloseAsync(factory, world, world.PlatformId, active, next, gate: gate),
            () => SendAsync(factory, HttpMethod.Delete, Sprint(world, world.PlatformId, next)));

        Assert.Equal(HttpStatusCode.OK, close.Status);
        ProblemAssert.Problem(removal, HttpStatusCode.Conflict, SprintErrorCodes.NotRemovable);
        Assert.Equal(next, await StoredSprintAsync(factory, item));

        var worldTwo = await CreateWorldAsync(factory);
        var activeTwo = await ActiveAsync(factory, worldTwo, worldTwo.PlatformId);
        var nextTwo = await NewAsync(factory, worldTwo, worldTwo.PlatformId, "Следующий");
        var itemTwo = await ItemAsync(factory, worldTwo, "Новая", sprintId: activeTwo);

        var (removed, closeTwo) = await Race.TwoAsync(
            factory,
            gate => SendAsync(factory, HttpMethod.Delete, Sprint(worldTwo, worldTwo.PlatformId, nextTwo), gate: gate),
            () => CloseAsync(factory, worldTwo, worldTwo.PlatformId, activeTwo, nextTwo));

        Assert.Equal(HttpStatusCode.NoContent, removed.Status);
        ProblemAssert.Problem(closeTwo, HttpStatusCode.NotFound, SprintErrorCodes.NotFound);
        Assert.Equal(activeTwo, await StoredSprintAsync(factory, itemTwo));
        Assert.Empty(await SnapshotAsync(activeTwo));
    }

    // BR-12 · the team's unlink and the close meet in one order: the unlink first leaves the close an empty sprint; the close first
    // leaves the unlink its Closed sprint's pointers to clear, and the snapshot is kept.
    [Fact]
    public async Task Close_RacingTheUnlinkOfTheTeam_EndsInOneOrder()
    {
        var world = await CreateWorldAsync(factory);
        var active = await ActiveAsync(factory, world, world.PlatformId);
        var item = await ItemAsync(factory, world, "Готова", 2, "Закрыта", sprintId: active);

        var (unlink, close) = await Race.TwoAsync(
            factory,
            gate => TeamTestData.UnlinkAsync(factory, world.OrganizationId, world.ProjectId, world.PlatformId, TeamTestData.To(world.ProjectTeamId), gate: gate),
            () => CloseAsync(factory, world, world.PlatformId, active));

        Assert.Equal((HttpStatusCode.NoContent, HttpStatusCode.OK), (unlink.Status, close.Status));
        Assert.Null(await StoredSprintAsync(factory, item));
        Assert.Empty(await SnapshotAsync(active));

        var worldTwo = await CreateWorldAsync(factory);
        var activeTwo = await ActiveAsync(factory, worldTwo, worldTwo.PlatformId);
        var itemTwo = await ItemAsync(factory, worldTwo, "Готова", 2, "Закрыта", sprintId: activeTwo);

        var (closed, unlinked) = await Race.TwoAsync(
            factory,
            gate => CloseAsync(factory, worldTwo, worldTwo.PlatformId, activeTwo, null, null, gate),
            () => TeamTestData.UnlinkAsync(factory, worldTwo.OrganizationId, worldTwo.ProjectId, worldTwo.PlatformId, TeamTestData.To(worldTwo.ProjectTeamId)));

        Assert.Equal((HttpStatusCode.OK, HttpStatusCode.NoContent), (closed.Status, unlinked.Status));
        Assert.Null(await StoredSprintAsync(factory, itemTwo));
        Assert.Single(await SnapshotAsync(activeTwo));
    }

    // BR-12 · an item's team change racing the close: the snapshot keeps the line, the pointer ends cleared.
    [Fact]
    public async Task Close_RacingATeamChangeOfAnItem_KeepsTheSnapshotLine()
    {
        var world = await CreateWorldAsync(factory);
        var active = await ActiveAsync(factory, world, world.PlatformId);
        var item = await ItemAsync(factory, world, "Готова", 2, "Закрыта", sprintId: active);

        var (close, change) = await Race.TwoAsync(
            factory,
            gate => CloseAsync(factory, world, world.PlatformId, active, null, null, gate),
            () => WorkItemsTestData.SaveAsync(factory, world.Items, item.Identifier, new { team = new { teamId = world.ProjectTeamId.ToString() } }));

        Assert.Equal((HttpStatusCode.OK, HttpStatusCode.OK), (close.Status, change.Status));
        Assert.Null(await StoredSprintAsync(factory, item));
        Assert.EndsWith("|true", Assert.Single(await SnapshotAsync(active)));
    }
}
