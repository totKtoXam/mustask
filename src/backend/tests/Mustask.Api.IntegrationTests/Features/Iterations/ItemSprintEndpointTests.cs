using System.Net;
using Mustask.Api.IntegrationTests.Features.Boards;
using Mustask.Api.IntegrationTests.Features.WorkItems;
using Mustask.Api.IntegrationTests.Kit;
using Mustask.Domain.Iterations;
using static Mustask.Api.IntegrationTests.Features.Iterations.IterationsTestData;

namespace Mustask.Api.IntegrationTests.Features.Iterations;

/// <summary>
/// US-4 (mod-007 api-contract § 3, BR-2, BR-5): the item's sprint on mod-003's save and on the card. The team rule, the closed
/// target, the deleted item, the owner change and the same-sprint save.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class ItemSprintEndpointTests(MustaskApiFactory factory)
{
    // US-4 · "Set a sprint": the card shows it, and clearing it shows none.
    [Fact]
    public async Task SetSprint_OfTheItemsTeam_ShowsOnTheCard_AndClears()
    {
        var world = await CreateWorldAsync(factory);
        var sprint = await NewAsync(factory, world, world.PlatformId, "Релиз");
        var item = await ItemAsync(factory, world, "Задача");
        Assert.Null(await SprintOfAsync(factory, world, item));

        var set = await SetSprintAsync(factory, world, item, sprint);

        Assert.Equal(HttpStatusCode.OK, set.Status);
        var shown = set.Body.GetProperty("sprint");
        Assert.Equal((sprint, "Релиз", "Planned"), (shown.GetProperty("id").GetGuid(), shown.GetProperty("name").GetString(), shown.GetProperty("state").GetString()));
        Assert.Equal(sprint, await SprintOfAsync(factory, world, item));

        var cleared = await SetSprintAsync(factory, world, item, null);
        Assert.Equal(HttpStatusCode.OK, cleared.Status);
        Assert.Null(await SprintOfAsync(factory, world, item));
    }

    [Fact]
    public async Task SetSprint_AnActiveSprint_IsAccepted()
    {
        var world = await CreateWorldAsync(factory);
        var active = await ActiveAsync(factory, world, world.PlatformId);
        var item = await ItemAsync(factory, world, "Задача");

        Assert.Equal(HttpStatusCode.OK, (await SetSprintAsync(factory, world, item, active)).Status);
        Assert.Equal(active, await StoredSprintAsync(factory, item));
    }

    // BR-2 · an item with no team has no sprint.
    [Fact]
    public async Task SetSprint_OnAnItemWithNoTeam_Is409TeamRequired()
    {
        var world = await CreateWorldAsync(factory);
        var sprint = await NewAsync(factory, world, world.PlatformId);
        var item = await ItemAsync(factory, world, "Без команды");
        await BoardsTestData.OwnedByAsync(factory, item, null);

        var answer = await SetSprintAsync(factory, world, item, sprint);

        ProblemAssert.Problem(answer, HttpStatusCode.Conflict, SprintErrorCodes.TeamRequired);
        Assert.Null(await StoredSprintAsync(factory, item));
    }

    // BR-2 · the sprint is one of the item's own team.
    [Fact]
    public async Task SetSprint_OfAnotherTeam_Is409OtherTeam()
    {
        var world = await CreateWorldAsync(factory);
        var foreign = await NewAsync(factory, world, world.ProjectTeamId);
        var item = await ItemAsync(factory, world, "Задача");

        ProblemAssert.Problem(await SetSprintAsync(factory, world, item, foreign), HttpStatusCode.Conflict, SprintErrorCodes.OtherTeam);
        Assert.Null(await StoredSprintAsync(factory, item));
    }

    // BR-5 · items join only Planned and Active sprints.
    [Fact]
    public async Task SetSprint_ToAClosedSprint_Is409ClosedTarget()
    {
        var world = await CreateWorldAsync(factory);
        var closed = await ActiveAsync(factory, world, world.PlatformId);
        Assert.Equal(HttpStatusCode.OK, (await CloseAsync(factory, world, world.PlatformId, closed)).Status);
        var item = await ItemAsync(factory, world, "Задача");

        ProblemAssert.Problem(await SetSprintAsync(factory, world, item, closed), HttpStatusCode.Conflict, SprintErrorCodes.ClosedTarget);
    }

    [Fact]
    public async Task SetSprint_ToANonexistentSprint_Is404NotFound()
    {
        var world = await CreateWorldAsync(factory);
        var item = await ItemAsync(factory, world, "Задача");

        ProblemAssert.Problem(await SetSprintAsync(factory, world, item, Guid.NewGuid()), HttpStatusCode.NotFound, SprintErrorCodes.NotFound);
    }

    // BR-5 · the check applies when the pointer changes, not on a later save that leaves it: an item stays in a sprint that closed
    // meanwhile (it was moved or it kept the pointer as done), and a save of another field works.
    [Fact]
    public async Task Save_LeavingThePointer_IsNotCheckedAgain()
    {
        var world = await CreateWorldAsync(factory);
        var sprint = await ActiveAsync(factory, world, world.PlatformId);
        var item = await ItemAsync(factory, world, "Готовая", 3, "Закрыта", sprintId: sprint);
        Assert.Equal(HttpStatusCode.OK, (await CloseAsync(factory, world, world.PlatformId, sprint)).Status);

        var renamed = await WorkItemsTestData.SaveAsync(factory, world.Items, item.Identifier, new { title = "Готовая, правка" });
        Assert.Equal(HttpStatusCode.OK, renamed.Status);

        // Sending the same Closed sprint again changes nothing, so it is not refused either.
        var same = await SetSprintAsync(factory, world, item, sprint);
        Assert.Equal(HttpStatusCode.OK, same.Status);
        Assert.Equal(sprint, await StoredSprintAsync(factory, item));
    }

    // BR-5 · an item in «Удалена» takes no write: the answer is mod-005's, before any sprint check.
    [Fact]
    public async Task SetSprint_OnADeletedItem_Is409ItemDeleted_First()
    {
        var world = await CreateWorldAsync(factory);
        var sprint = await NewAsync(factory, world, world.PlatformId);
        var item = await ItemAsync(factory, world, "Задача");
        Assert.Equal(HttpStatusCode.OK, (await DeleteItemAsync(factory, world, item)).Status);

        var answer = await SetSprintAsync(factory, world, item, sprint);
        Assert.Equal(HttpStatusCode.Conflict, answer.Status);
        Assert.Equal("workitems.workitem.deleted", answer.ErrorCode);

        // Even a sprint that does not exist: the deleted item answers first.
        Assert.Equal("workitems.workitem.deleted", (await SetSprintAsync(factory, world, item, Guid.NewGuid())).ErrorCode);
    }

    // BR-2 · a save that changes the owning team and sends no sprint clears the pointer.
    [Fact]
    public async Task ChangeOfTeam_WithoutASprint_ClearsThePointer()
    {
        var world = await CreateWorldAsync(factory);
        var sprint = await NewAsync(factory, world, world.PlatformId);
        var item = await ItemAsync(factory, world, "Задача", sprintId: sprint);

        var moved = await WorkItemsTestData.SaveAsync(factory, world.Items, item.Identifier, new { team = new { teamId = world.ProjectTeamId.ToString() } });

        Assert.Equal(HttpStatusCode.OK, moved.Status);
        Assert.Null(await StoredSprintAsync(factory, item));
        Assert.Equal(System.Text.Json.JsonValueKind.Null, moved.Body.GetProperty("sprint").ValueKind);
    }

    // BR-2 · … unless it sets a Planned or Active sprint of the new team.
    [Fact]
    public async Task ChangeOfTeam_WithASprintOfTheNewTeam_KeepsThatSprint()
    {
        var world = await CreateWorldAsync(factory);
        var old = await NewAsync(factory, world, world.PlatformId);
        var fresh = await NewAsync(factory, world, world.ProjectTeamId);
        var item = await ItemAsync(factory, world, "Задача", sprintId: old);

        var moved = await WorkItemsTestData.SaveAsync(
            factory,
            world.Items,
            item.Identifier,
            new { team = new { teamId = world.ProjectTeamId.ToString() }, sprint = new { sprintId = fresh.ToString() } });

        Assert.Equal(HttpStatusCode.OK, moved.Status);
        Assert.Equal(fresh, await StoredSprintAsync(factory, item));
    }

    // BR-2 · the sprint sent with a team change must still be one of the new team.
    [Fact]
    public async Task ChangeOfTeam_WithASprintOfTheOldTeam_Is409OtherTeam()
    {
        var world = await CreateWorldAsync(factory);
        var old = await NewAsync(factory, world, world.PlatformId);
        var item = await ItemAsync(factory, world, "Задача", sprintId: old);

        var moved = await WorkItemsTestData.SaveAsync(
            factory,
            world.Items,
            item.Identifier,
            new { team = new { teamId = world.ProjectTeamId.ToString() }, sprint = new { sprintId = old.ToString() } });

        ProblemAssert.Problem(moved, HttpStatusCode.Conflict, SprintErrorCodes.OtherTeam);
        Assert.Equal(old, await StoredSprintAsync(factory, item));
    }
}
