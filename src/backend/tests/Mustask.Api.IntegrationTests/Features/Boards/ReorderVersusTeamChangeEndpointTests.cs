using System.Net;
using Mustask.Api.IntegrationTests.Features.Organizations;
using Mustask.Api.IntegrationTests.Features.WorkItems;
using static Mustask.Api.IntegrationTests.Features.WorkItems.OwningTeamTestData;

namespace Mustask.Api.IntegrationTests.Features.Boards;

/// <summary>
/// A reorder of a team's backlog against a change of the owning team of one of its members (mod-017 US-4, BR-OT-6, BR-OT-13 (3);
/// plan-backend D-10, § 7.3). The reorder writes a place for every member, not only for the one it moves, and a place names the
/// team that owns its item (ADR-0028 rule 5); the two requests end in one order, in both orders, with no server error and no
/// place that names a team that does not own its item.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class ReorderVersusTeamChangeEndpointTests(MustaskApiFactory factory)
{
    private const string Stale = "boards.backlog.reorder_stale";

    private async Task<Item> StoryAsync(OwningTeamWorld world, string title, Guid? team)
    {
        var answer = await OrganizationsApi.SendAsync(
            factory,
            HttpMethod.Post,
            WorkItemsTestData.List(world.Items),
            new
            {
                requestId = Guid.NewGuid().ToString(),
                typeId = world.Items.Types["US"].Id.ToString(),
                title,
                team = new { teamId = team?.ToString() },
            },
            OrganizationsTokens.SignedIn());
        Assert.Equal(HttpStatusCode.Created, answer.Status);

        return new Item(answer.Body.GetProperty("id").GetGuid(), answer.Body.GetProperty("identifier").GetString()!);
    }

    private Task<Answer> ChangeTeamAsync(OwningTeamWorld world, Item item, Guid? team, CommitGate? gate = null) =>
        WorkItemsTestData.SaveAsync(factory, world.Items, item.Identifier, new { team = new { teamId = team?.ToString() } }, gate);

    /// <summary>No place of the project's items names a team that does not own the item (ADR-0028 rule 5).</summary>
    private async Task AssertNoPlaceNamesAnotherTeamAsync(OwningTeamWorld world) =>
        Assert.Equal(0, await PlacesNamingAnotherTeamAsync(factory, world.ProjectId));

    // US-4 · "a reorder naming an item that left the backlog is stale": the item given to another team before the reorder, as
    // the moved item or as the anchor of "after", answers 409 and changes nothing.
    [Fact]
    public async Task Reorder_AfterTheItemLeft_IsStale()
    {
        var world = await CreateWorldAsync(factory);
        var boards = BoardsViewOf(world);
        var stays = await StoryAsync(world, "Остаётся", world.Mars);
        var leaves = await StoryAsync(world, "Уходит", world.Mars);
        await BoardsTestData.ReorderToAsync(factory, boards, world.Mars, stays, leaves);
        await ChangeTeamAsync(world, leaves, world.Venus);

        var moved = await BoardsTestData.ReorderAsync(factory, boards, world.Mars, BoardsTestData.Top(leaves));
        var anchored = await BoardsTestData.ReorderAsync(factory, boards, world.Mars, BoardsTestData.After(stays, leaves));

        Assert.Equal((HttpStatusCode.Conflict, Stale), (moved.Status, moved.ErrorCode));
        Assert.Equal((HttpStatusCode.Conflict, Stale), (anchored.Status, anchored.ErrorCode));
        Assert.Equal([stays.Identifier], await BoardsTestData.RowsAsync(factory, boards, world.Mars));
        Assert.Equal(0, await TeamPlacesOfAsync(factory, leaves.Id));
    }

    // US-4 · outline "a reorder against a team change", the reorder first, the reorder moves the item that leaves: the reorder
    // writes the place of every member, the item's included; the save, which waits for the item, then drops the place the
    // reorder wrote.
    [Fact]
    public async Task Reorder_VersusTeamChange_ReorderFirst_MovesTheItemThatLeaves()
    {
        var (world, boards, other, leaving) = await ArrangeAsync();
        using var gate = factory.CommitGates.Open();

        var reorder = BoardsTestData.ReorderAsync(factory, boards, world.Mars, BoardsTestData.Top(leaving), gate: gate);
        await Task.WhenAny(gate.Reached, reorder);
        Assert.False(reorder.IsCompleted, "The reorder finished before reaching its commit gate.");

        var save = ChangeTeamAsync(world, leaving, world.Venus);
        await PgLocks.WaitUntilBlockedAsync(factory.ConnectionString, save);
        Assert.False(save.IsCompleted, "The save did not wait for the reorder's lock of the member.");

        gate.Release();
        var (reordered, saved) = (await reorder, await save);

        Assert.Equal((HttpStatusCode.NoContent, HttpStatusCode.OK), (reordered.Status, saved.Status));
        Assert.Equal(0, await TeamPlacesOfAsync(factory, leaving.Id));
        Assert.Equal(1, await TeamPlacesOfAsync(factory, other.Id));
        Assert.Equal([other.Identifier], await BoardsTestData.RowsAsync(factory, boards, world.Mars));
        Assert.Equal([leaving.Identifier], await BoardsTestData.RowsAsync(factory, boards, world.Venus));
        await AssertNoPlaceNamesAnotherTeamAsync(world);
    }

    // The reorder first, the reorder moves another member: the item that leaves is still written a place by the reorder (it is a
    // member when the reorder runs) and the save then drops it.
    [Fact]
    public async Task Reorder_VersusTeamChange_ReorderFirst_MovesAnotherMember()
    {
        var (world, boards, other, leaving) = await ArrangeAsync();
        using var gate = factory.CommitGates.Open();

        var reorder = BoardsTestData.ReorderAsync(factory, boards, world.Mars, BoardsTestData.Top(other), gate: gate);
        await Task.WhenAny(gate.Reached, reorder);
        Assert.False(reorder.IsCompleted, "The reorder finished before reaching its commit gate.");

        var save = ChangeTeamAsync(world, leaving, world.Venus);
        await PgLocks.WaitUntilBlockedAsync(factory.ConnectionString, save);
        Assert.False(save.IsCompleted, "The save did not wait for the reorder's lock of the member.");

        gate.Release();
        var (reordered, saved) = (await reorder, await save);

        Assert.Equal((HttpStatusCode.NoContent, HttpStatusCode.OK), (reordered.Status, saved.Status));
        Assert.Equal(0, await TeamPlacesOfAsync(factory, leaving.Id));
        Assert.Equal([other.Identifier], await BoardsTestData.RowsAsync(factory, boards, world.Mars));
        Assert.Equal([leaving.Identifier], await BoardsTestData.RowsAsync(factory, boards, world.Venus));
        await AssertNoPlaceNamesAnotherTeamAsync(world);
    }

    // The save first, the reorder moves the item that left: the reorder waits for the item, then reads the members again and
    // does not find it — stale, nothing changes.
    [Fact]
    public async Task Reorder_VersusTeamChange_SaveFirst_MovesTheItemThatLeft_IsStale()
    {
        var (world, boards, other, leaving) = await ArrangeAsync();
        using var gate = factory.CommitGates.Open();

        var save = ChangeTeamAsync(world, leaving, world.Venus, gate);
        await Task.WhenAny(gate.Reached, save);
        Assert.False(save.IsCompleted, "The save finished before reaching its commit gate.");

        var reorder = BoardsTestData.ReorderAsync(factory, boards, world.Mars, BoardsTestData.Top(leaving));
        await PgLocks.WaitUntilBlockedAsync(factory.ConnectionString, reorder);
        Assert.False(reorder.IsCompleted, "The reorder did not wait for the save's lock of the item.");

        gate.Release();
        var (saved, reordered) = (await save, await reorder);

        Assert.Equal(HttpStatusCode.OK, saved.Status);
        Assert.Equal((HttpStatusCode.Conflict, Stale), (reordered.Status, reordered.ErrorCode));
        Assert.Equal(0, await TeamPlacesOfAsync(factory, leaving.Id));
        Assert.Equal([other.Identifier], await BoardsTestData.RowsAsync(factory, boards, world.Mars));
        await AssertNoPlaceNamesAnotherTeamAsync(world);
    }

    // The save first, the reorder moves another member: it completes on the members that remain and writes no place for the one
    // that left.
    [Fact]
    public async Task Reorder_VersusTeamChange_SaveFirst_MovesAnotherMember_CompletesOnTheRemainingMembers()
    {
        var (world, boards, other, leaving) = await ArrangeAsync();
        using var gate = factory.CommitGates.Open();

        var save = ChangeTeamAsync(world, leaving, world.Venus, gate);
        await Task.WhenAny(gate.Reached, save);
        Assert.False(save.IsCompleted, "The save finished before reaching its commit gate.");

        var reorder = BoardsTestData.ReorderAsync(factory, boards, world.Mars, BoardsTestData.Top(other));
        await PgLocks.WaitUntilBlockedAsync(factory.ConnectionString, reorder);
        Assert.False(reorder.IsCompleted, "The reorder did not wait for the save's lock of the item.");

        gate.Release();
        var (saved, reordered) = (await save, await reorder);

        Assert.Equal((HttpStatusCode.OK, HttpStatusCode.NoContent), (saved.Status, reordered.Status));
        Assert.Equal(0, await TeamPlacesOfAsync(factory, leaving.Id));
        Assert.Equal(1, await TeamPlacesOfAsync(factory, other.Id));
        Assert.Equal([other.Identifier], await BoardsTestData.RowsAsync(factory, boards, world.Mars));
        await AssertNoPlaceNamesAnotherTeamAsync(world);
    }

    /// <summary>MARS owns two stories, ordered so that both hold a place; the second is the one a save will give to VENUS.</summary>
    private async Task<(OwningTeamWorld World, BoardsWorld Boards, Item Other, Item Leaving)> ArrangeAsync()
    {
        var world = await CreateWorldAsync(factory);
        var boards = BoardsViewOf(world);
        var other = await StoryAsync(world, "Остаётся", world.Mars);
        var leaving = await StoryAsync(world, "Уходит", world.Mars);
        await BoardsTestData.ReorderToAsync(factory, boards, world.Mars, other, leaving);
        Assert.Equal((1, 1), (await TeamPlacesOfAsync(factory, other.Id), await TeamPlacesOfAsync(factory, leaving.Id)));

        return (world, boards, other, leaving);
    }

    // US-4 · "the two orders are independent": a team's reorder changes no project place, and the project's changes no team place.
    [Fact]
    public async Task TeamAndProjectOrders_AreIndependent()
    {
        var world = await CreateWorldAsync(factory);
        var boards = BoardsViewOf(world);
        var first = await StoryAsync(world, "Первая", world.Mars);
        var second = await StoryAsync(world, "Вторая", world.Mars);
        var third = await StoryAsync(world, "Третья", world.Mars);

        await BoardsTestData.ReorderToAsync(factory, boards, world.Mars, third, first, second);
        Assert.Equal([third.Identifier, first.Identifier, second.Identifier], await BoardsTestData.RowsAsync(factory, boards, world.Mars));
        Assert.Equal([first.Identifier, second.Identifier, third.Identifier], await ProjectBoardsTestData.RowsAsync(factory, boards));

        await ProjectBoardsTestData.ReorderToAsync(factory, boards, second, third);
        Assert.Equal([first.Identifier, second.Identifier, third.Identifier], await ProjectBoardsTestData.RowsAsync(factory, boards));
        Assert.Equal([third.Identifier, first.Identifier, second.Identifier], await BoardsTestData.RowsAsync(factory, boards, world.Mars));
    }
}
