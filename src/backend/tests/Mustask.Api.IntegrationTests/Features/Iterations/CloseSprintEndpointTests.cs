using System.Net;
using Mustask.Api.IntegrationTests.Features.WorkItems;
using Mustask.Api.IntegrationTests.Kit;
using Mustask.Domain.Iterations;
using static Mustask.Api.IntegrationTests.Features.Iterations.IterationsTestData;

namespace Mustask.Api.IntegrationTests.Features.Iterations;

/// <summary>
/// US-5 (mod-007 api-contract § 2.6 to § 2.8, BR-6, BR-14): the close dialog's figures, the close with and without a destination,
/// what moves and what stays, the destination's errors, the page of a Closed sprint and the race with an item save.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class CloseSprintEndpointTests(MustaskApiFactory factory)
{
    // US-5 · the dialog: what moves (Proposed, In Progress, Resolved), what stays (Completed), «Удалена» in neither, the team's
    // Planned sprints as destinations in number order.
    [Fact]
    public async Task Preview_CountsWhatMovesAndWhatStays_AndOffersThePlannedSprints()
    {
        var world = await CreateWorldAsync(factory);
        var active = await ActiveAsync(factory, world, world.PlatformId);
        var next = await NewAsync(factory, world, world.PlatformId, "Следующий", "2026-11-16", "2026-11-27");
        var later = await NewAsync(factory, world, world.PlatformId, "Позже");
        await NewAsync(factory, world, world.ProjectTeamId, "Чужой");
        await ItemAsync(factory, world, "Новая", 2, sprintId: active);
        await ItemAsync(factory, world, "В работе", 3, "Активна", sprintId: active);
        await ItemAsync(factory, world, "Готова", 5, "Закрыта", sprintId: active);
        var deleted = await ItemAsync(factory, world, "Удалённая", 8, sprintId: active);
        Assert.Equal(HttpStatusCode.OK, (await DeleteItemAsync(factory, world, deleted)).Status);

        var preview = await ReadAsync(factory, world, world.PlatformId, active, "/close-preview");

        Assert.Equal((2, 5m), (preview.Body.GetProperty("moving").GetProperty("items").GetInt32(), preview.Body.GetProperty("moving").GetProperty("storyPoints").GetDecimal()));
        Assert.Equal((1, 5m), (preview.Body.GetProperty("staying").GetProperty("items").GetInt32(), preview.Body.GetProperty("staying").GetProperty("storyPoints").GetDecimal()));
        var destinations = preview.Body.GetProperty("destinations").EnumerateArray().ToList();
        Assert.Equal([next, later], destinations.Select(row => row.GetProperty("id").GetGuid()));
        Assert.Equal("2026-11-16", destinations[0].GetProperty("startDate").GetString());
    }

    [Fact]
    public async Task Preview_OfAPlannedOrClosedSprint_Is409NotActive()
    {
        var world = await CreateWorldAsync(factory);
        var planned = await NewAsync(factory, world, world.PlatformId);

        var answer = await SendAsync(factory, HttpMethod.Get, $"{Sprint(world, world.PlatformId, planned)}/close-preview");

        ProblemAssert.Problem(answer, HttpStatusCode.Conflict, SprintErrorCodes.NotActive);
    }

    // US-5 · the close moves the unfinished items to the destination and keeps the finished ones; «Изменён» of the moved ones is the
    // close's time.
    [Fact]
    public async Task Close_MovesTheUnfinishedToTheDestination_AndKeepsTheFinishedOnes()
    {
        var world = await CreateWorldAsync(factory);
        var active = await ActiveAsync(factory, world, world.PlatformId);
        var next = await NewAsync(factory, world, world.PlatformId, "Следующий");
        var open = await ItemAsync(factory, world, "Новая", 2, sprintId: active);
        var working = await ItemAsync(factory, world, "В работе", 3, "Активна", sprintId: active);
        var done = await ItemAsync(factory, world, "Готова", 5, "Закрыта", sprintId: active);
        var before = DateTimeOffset.UtcNow;

        var closed = await CloseAsync(factory, world, world.PlatformId, active, next);

        Assert.Equal(HttpStatusCode.OK, closed.Status);
        Assert.Equal("Closed", closed.Body.GetProperty("state").GetString());
        Assert.NotEqual(System.Text.Json.JsonValueKind.Null, closed.Body.GetProperty("closedAt").ValueKind);
        Assert.Equal((next, next, active), (await StoredSprintAsync(factory, open), await StoredSprintAsync(factory, working), await StoredSprintAsync(factory, done)));

        var stored = (await WorkItemsTestData.StoredAsync(factory, world.ProjectId)).ToDictionary(item => item.Title);
        Assert.True(stored["Новая"].ChangedAt >= before.AddSeconds(-5), "«Изменён» of a moved item is the close's time.");
        Assert.Equal(stored["Новая"].ChangedAt, stored["В работе"].ChangedAt);
        Assert.True(stored["Готова"].ChangedAt < stored["Новая"].ChangedAt, "A finished item is not touched by the close.");

        // The sprint's own page reads the done part of the snapshot: one item, five points.
        var page = await ReadAsync(factory, world, world.PlatformId, active);
        Assert.Equal((1, 5m), (page.Body.GetProperty("itemCount").GetInt32(), page.Body.GetProperty("storyPoints").GetDecimal()));
        Assert.Equal(active, page.Body.GetProperty("sprint").GetProperty("id").GetGuid());
    }

    // US-5 · no destination: the unfinished items are left with no sprint.
    [Fact]
    public async Task Close_WithNoDestination_LeavesTheUnfinishedWithoutASprint()
    {
        var world = await CreateWorldAsync(factory);
        var active = await ActiveAsync(factory, world, world.PlatformId);
        var open = await ItemAsync(factory, world, "Новая", sprintId: active);

        Assert.Equal(HttpStatusCode.OK, (await CloseAsync(factory, world, world.PlatformId, active)).Status);

        Assert.Null(await StoredSprintAsync(factory, open));
        Assert.Null(await SprintOfAsync(factory, world, open));
    }

    // BR-6 · a deleted item is neither moved nor counted: it stays in the sprint, in «Удалена».
    [Fact]
    public async Task Close_LeavesADeletedItemWhereItWas_AndCountsItNowhere()
    {
        var world = await CreateWorldAsync(factory);
        var active = await ActiveAsync(factory, world, world.PlatformId);
        var next = await NewAsync(factory, world, world.PlatformId, "Следующий");
        var deleted = await ItemAsync(factory, world, "Удалённая", 8, "Закрыта", sprintId: active);
        Assert.Equal(HttpStatusCode.OK, (await DeleteItemAsync(factory, world, deleted)).Status);

        Assert.Equal(HttpStatusCode.OK, (await CloseAsync(factory, world, world.PlatformId, active, next)).Status);

        Assert.Equal(active, await StoredSprintAsync(factory, deleted));
        var page = await ReadAsync(factory, world, world.PlatformId, active);
        Assert.Equal((0, 0m), (page.Body.GetProperty("itemCount").GetInt32(), page.Body.GetProperty("storyPoints").GetDecimal()));
    }

    // US-5 · "Active only".
    [Fact]
    public async Task Close_APlannedOrAClosedSprint_Is409NotActive()
    {
        var world = await CreateWorldAsync(factory);
        var planned = await NewAsync(factory, world, world.PlatformId);
        var active = await ActiveAsync(factory, world, world.PlatformId, "Активный", "2026-10-05", "2026-10-16");

        ProblemAssert.Problem(await CloseAsync(factory, world, world.PlatformId, planned), HttpStatusCode.Conflict, SprintErrorCodes.NotActive);
        Assert.Equal(HttpStatusCode.OK, (await CloseAsync(factory, world, world.PlatformId, active)).Status);
        ProblemAssert.Problem(await CloseAsync(factory, world, world.PlatformId, active), HttpStatusCode.Conflict, SprintErrorCodes.NotActive);
    }

    // api-contract § 2.7 · an existing destination that is not a Planned sprint of the team is 409; one that names nothing is 404;
    // either way nothing was closed and nothing moved (the close is one operation).
    [Fact]
    public async Task Close_WithAnInvalidDestination_ChangesNothing()
    {
        var world = await CreateWorldAsync(factory);
        var active = await ActiveAsync(factory, world, world.PlatformId);
        var foreign = await NewAsync(factory, world, world.ProjectTeamId);
        var open = await ItemAsync(factory, world, "Новая", sprintId: active);

        ProblemAssert.Problem(
            await CloseAsync(factory, world, world.PlatformId, active, foreign), HttpStatusCode.Conflict, SprintErrorCodes.CloseDestinationInvalid);
        ProblemAssert.Problem(
            await CloseAsync(factory, world, world.PlatformId, active, active), HttpStatusCode.Conflict, SprintErrorCodes.CloseDestinationInvalid);
        ProblemAssert.Problem(
            await CloseAsync(factory, world, world.PlatformId, active, Guid.NewGuid()), HttpStatusCode.NotFound, SprintErrorCodes.NotFound);
        var malformed = await SendAsync(factory, HttpMethod.Post, $"{Sprint(world, world.PlatformId, active)}/close", new { destinationSprintId = "not-a-guid" });
        ProblemAssert.Problem(malformed, HttpStatusCode.NotFound, SprintErrorCodes.NotFound);

        Assert.Equal(active, await StoredSprintAsync(factory, open));
        Assert.Equal("Active", (await SendAsync(factory, HttpMethod.Get, Sprints(world, world.PlatformId))).Body.GetProperty("items").EnumerateArray()
            .Single(row => row.GetProperty("id").GetGuid() == active).GetProperty("state").GetString());
    }

    // api-contract § 2.8 · only a Closed sprint has a page.
    [Fact]
    public async Task Page_OfAPlannedOrActiveSprint_Is404()
    {
        var world = await CreateWorldAsync(factory);
        var active = await ActiveAsync(factory, world, world.PlatformId);

        var answer = await SendAsync(factory, HttpMethod.Get, Sprint(world, world.PlatformId, active));

        ProblemAssert.Problem(answer, HttpStatusCode.NotFound, SprintErrorCodes.NotFound);
    }

    // BR-12 · a save into the sprint that comes while the close holds its locks waits and then meets a Closed sprint.
    [Fact]
    public async Task Close_RacingAnItemSaveIntoTheSprint_EndsInOneOrder()
    {
        var world = await CreateWorldAsync(factory);
        var active = await ActiveAsync(factory, world, world.PlatformId);
        var item = await ItemAsync(factory, world, "Новая");

        var (close, save) = await Race.TwoAsync(
            factory,
            gate => CloseAsync(factory, world, world.PlatformId, active, null, null, gate),
            () => SetSprintAsync(factory, world, item, active));

        Assert.Equal(HttpStatusCode.OK, close.Status);
        ProblemAssert.Problem(save, HttpStatusCode.Conflict, SprintErrorCodes.ClosedTarget);
        Assert.Null(await StoredSprintAsync(factory, item));
    }
}
