using System.Net;
using Mustask.Api.IntegrationTests.Features.Organizations;
using Mustask.Api.IntegrationTests.Features.WorkItems;
using Mustask.Domain.Organizations;
using Mustask.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Mustask.Api.IntegrationTests.Features.Boards;

/// <summary>
/// <c>POST /api/organizations/{organizationId}/projects/{projectId}/teams/{teamId}/backlogs/{level}/reorder</c>
/// (mod-006 api-contract § 3.2): US-2 — one item to the top, to the bottom or
/// directly after another; reorders of one backlog one at a time, last write
/// wins with no token (BR-M4-25); a move next to an item that has left refused
/// (BR-M4-26).
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class ReorderBacklogEndpointTests(MustaskApiFactory factory)
{
    /// <summary>US-2's data: the «Истории» backlog of «Платформа» reads US-3, BUG-5, US-9, US-11 (by number, never reordered).</summary>
    private async Task<(BoardsWorld World, Item Us3, Item Bug5, Item Us9, Item Us11)> BacklogAsync()
    {
        var world = await BoardsTestData.CreateWorldAsync(factory);
        var us3 = await BoardsTestData.CreateAsync(factory, world, "US", "Третья", world.PlatformAreaId);
        var bug5 = await BoardsTestData.CreateAsync(factory, world, "BUG", "Пятый", world.PlatformAreaId);
        var us9 = await BoardsTestData.CreateAsync(factory, world, "US", "Девятая", world.PlatformAreaId);
        var us11 = await BoardsTestData.CreateAsync(factory, world, "US", "Одиннадцатая", world.ApiAreaId);

        return (world, us3, bug5, us9, us11);
    }

    private static string[] Ids(params Item[] items) => items.Select(item => item.Identifier).ToArray();

    // US-2 · "A drag between two rows persists": dropped between US-3 and BUG-5 is "directly after US-3"; 204, and the
    // backlog reads the new order, «Порядок» 1 to 4, at every later read.
    [Fact]
    public async Task Reorder_AfterAnItem_Returns204_OrderPersists()
    {
        var (world, us3, bug5, us9, us11) = await BacklogAsync();

        var answer = await BoardsTestData.ReorderAsync(factory, world, world.PlatformId, BoardsTestData.After(us11, us3));

        Assert.Equal(HttpStatusCode.NoContent, answer.Status);
        Assert.Equal(Ids(us3, us11, bug5, us9), await BoardsTestData.RowsAsync(factory, world, world.PlatformId));
        Assert.Equal(Ids(us3, us11, bug5, us9), await BoardsTestData.RowsAsync(factory, world, world.PlatformId));
    }

    // US-2 · "A drop at either end".
    [Fact]
    public async Task Reorder_TopThenBottom_FirstThenLast()
    {
        var (world, us3, bug5, us9, us11) = await BacklogAsync();

        await BoardsTestData.MoveAsync(factory, world, world.PlatformId, BoardsTestData.Top(us9));
        Assert.Equal(Ids(us9, us3, bug5, us11), await BoardsTestData.RowsAsync(factory, world, world.PlatformId));

        await BoardsTestData.MoveAsync(factory, world, world.PlatformId, BoardsTestData.Bottom(us9));
        Assert.Equal(Ids(us3, bug5, us11, us9), await BoardsTestData.RowsAsync(factory, world, world.PlatformId));
    }

    // US-2 · "The handle's menu moves a row by keyboard": BUG-5, the second row — «В начало» and «Выше» are the top, «Ниже»
    // is directly after the row below, «В конец» the bottom (api-contract § 3.2's mapping).
    [Theory]
    [InlineData("В начало", "BUG5,US3,US9,US11")]
    [InlineData("Выше", "BUG5,US3,US9,US11")]
    [InlineData("Ниже", "US3,US9,BUG5,US11")]
    [InlineData("В конец", "US3,US9,US11,BUG5")]
    public async Task Reorder_KeyboardEntries_MoveTheSecondRow(string entry, string expected)
    {
        var (world, us3, bug5, us9, us11) = await BacklogAsync();
        var byName = new Dictionary<string, Item> { ["US3"] = us3, ["BUG5"] = bug5, ["US9"] = us9, ["US11"] = us11 };

        var body = entry switch
        {
            "В начало" or "Выше" => BoardsTestData.Top(bug5),
            "Ниже" => BoardsTestData.After(bug5, us9),
            _ => BoardsTestData.Bottom(bug5),
        };

        await BoardsTestData.MoveAsync(factory, world, world.PlatformId, body);

        Assert.Equal(expected.Split(',').Select(name => byName[name].Identifier), await BoardsTestData.RowsAsync(factory, world, world.PlatformId));
    }

    // US-2 · "Reordering changes nothing on the work item": its «Изменён» time and its place in «Рабочие элементы» stay.
    [Fact]
    public async Task Reorder_ChangesNoWorkItem()
    {
        var (world, _, _, _, us11) = await BacklogAsync();
        var storedBefore = await WorkItemsTestData.StoredAsync(factory, world.ProjectId);
        var listBefore = (await WorkItemsTestData.ListAsync(factory, world.Items)).Body.GetRawText();

        await BoardsTestData.MoveAsync(factory, world, world.PlatformId, BoardsTestData.Top(us11));

        Assert.Equal(storedBefore, await WorkItemsTestData.StoredAsync(factory, world.ProjectId));
        Assert.Equal(listBefore, (await WorkItemsTestData.ListAsync(factory, world.Items)).Body.GetRawText());
    }

    // US-2 · "Two users reorder different items, both moves count": Борис's page still shows the old order; his move applies
    // to the order Анна left, no notice either way. US-7 · "Nothing is pushed to an open page": Анна sees Борис's order at
    // her next read.
    [Fact]
    public async Task Reorder_DifferentItemsOneAfterTheOther_BothCount()
    {
        var (world, us3, bug5, us9, us11) = await BacklogAsync();

        await BoardsTestData.MoveAsync(factory, world, world.PlatformId, BoardsTestData.Top(us11));
        await BoardsTestData.MoveAsync(factory, world, world.PlatformId, BoardsTestData.After(us3, us9));

        Assert.Equal(Ids(us11, bug5, us9, us3), await BoardsTestData.RowsAsync(factory, world, world.PlatformId));
    }

    // BR-M4-25 under a real overlap: Анна's reorder holds the backlog's lock at its commit; Борис's reorder of another item
    // waits on it, then reads the order Анна committed and moves only its own item. Both answer 204 and both moves count.
    [Fact]
    public async Task Reorder_DifferentItemsAtTheSameMoment_SecondWaits_BothCount()
    {
        var (world, us3, bug5, us9, us11) = await BacklogAsync();

        using var gate = factory.CommitGates.Open();
        var anna = BoardsTestData.ReorderAsync(factory, world, world.PlatformId, BoardsTestData.Top(us11), gate: gate);
        await Task.WhenAny(gate.Reached, anna);
        Assert.False(anna.IsCompleted, "Анна's reorder finished before reaching its commit gate.");

        var boris = BoardsTestData.ReorderAsync(factory, world, world.PlatformId, BoardsTestData.After(us3, us9));
        await PgLocks.WaitUntilBlockedAsync(factory.ConnectionString, boris);
        Assert.False(boris.IsCompleted, "Борис's reorder did not wait on the backlog's lock.");

        gate.Release();
        var (a, b) = (await anna, await boris);

        Assert.Equal((HttpStatusCode.NoContent, HttpStatusCode.NoContent), (a.Status, b.Status));
        Assert.Equal(Ids(us11, bug5, us9, us3), await BoardsTestData.RowsAsync(factory, world, world.PlatformId));
    }

    // US-2 · "Two users move the same item, the later decides", under a real overlap: Анна moves US-9 to the top and holds
    // her commit; Борис's move of US-9 to the bottom waits, then wins; the other rows keep their order, no notice.
    [Fact]
    public async Task Reorder_SameItemAtTheSameMoment_TheLaterDecides()
    {
        var (world, us3, bug5, us9, us11) = await BacklogAsync();

        using var gate = factory.CommitGates.Open();
        var anna = BoardsTestData.ReorderAsync(factory, world, world.PlatformId, BoardsTestData.Top(us9), gate: gate);
        await Task.WhenAny(gate.Reached, anna);
        Assert.False(anna.IsCompleted, "Анна's reorder finished before reaching its commit gate.");

        var boris = BoardsTestData.ReorderAsync(factory, world, world.PlatformId, BoardsTestData.Bottom(us9));
        await PgLocks.WaitUntilBlockedAsync(factory.ConnectionString, boris);
        Assert.False(boris.IsCompleted, "Борис's reorder did not wait on the backlog's lock.");

        gate.Release();
        var (a, b) = (await anna, await boris);

        Assert.Equal((HttpStatusCode.NoContent, HttpStatusCode.NoContent), (a.Status, b.Status));
        Assert.Equal(Ids(us3, bug5, us11, us9), await BoardsTestData.RowsAsync(factory, world, world.PlatformId));
    }

    // US-2 · "A drop next to an item that has left is refused" (BR-M4-26): BUG-5's Область became «Мастаск»; the drop after
    // it answers the code's text and no place changes — and the same when the moved item itself has left.
    [Fact]
    public async Task Reorder_NextToOrOfAnItemThatLeft_Returns409Stale_NothingChanges()
    {
        var (world, us3, bug5, us9, us11) = await BacklogAsync();
        await BoardsTestData.MoveAsync(factory, world, world.PlatformId, BoardsTestData.Top(us9));
        await BoardsTestData.MoveToAreaAsync(factory, world, bug5, world.RootAreaId);
        var placesBefore = await PlacesAsync(world);

        var anchorLeft = await BoardsTestData.ReorderAsync(factory, world, world.PlatformId, BoardsTestData.After(us11, bug5));
        var itemLeft = await BoardsTestData.ReorderAsync(factory, world, world.PlatformId, BoardsTestData.Top(bug5));

        BoardsTestData.AssertRefused(anchorLeft, HttpStatusCode.Conflict, "boards.backlog.reorder_stale");
        Assert.Equal("Не удалось переместить: бэклог изменился. Список обновлён — повторите перемещение.", anchorLeft.Detail);
        BoardsTestData.AssertRefused(itemLeft, HttpStatusCode.Conflict, "boards.backlog.reorder_stale");
        Assert.Equal(placesBefore, await PlacesAsync(world));
        Assert.Equal(Ids(us9, us3, us11), await BoardsTestData.RowsAsync(factory, world, world.PlatformId));
    }

    // BR-M4-24: a reorder places every item where it stands and drops the places of items that left; the place of an item
    // that left is kept until then (US-7 "comes back before anything was ordered").
    [Fact]
    public async Task Reorder_PlacesEveryItem_DropsThePlacesOfItemsThatLeft()
    {
        var (world, us3, bug5, us9, us11) = await BacklogAsync();
        await BoardsTestData.MoveAsync(factory, world, world.PlatformId, BoardsTestData.Top(us9));
        Assert.Equal(
            [(us9.Id, 1), (us3.Id, 2), (bug5.Id, 3), (us11.Id, 4)],
            (await PlacesAsync(world)).OrderBy(place => place.Position).Select(place => (place.ItemId, place.Position)));

        await BoardsTestData.MoveToAreaAsync(factory, world, bug5, world.RootAreaId);
        Assert.Contains(await PlacesAsync(world), place => place.ItemId == bug5.Id);

        await BoardsTestData.MoveAsync(factory, world, world.PlatformId, BoardsTestData.Bottom(us3));

        Assert.Equal(
            [(us9.Id, 1), (us11.Id, 2), (us3.Id, 3)],
            (await PlacesAsync(world)).OrderBy(place => place.Position).Select(place => (place.ItemId, place.Position)));
    }

    // api-contract § 3.2: a position that is none of the three words is a panel defect, 400 with the field's reason.
    [Theory]
    [InlineData(null)]
    [InlineData("first")]
    [InlineData("TOP")]
    public async Task Reorder_InvalidPosition_Returns400(string? position)
    {
        var (world, _, _, _, us11) = await BacklogAsync();

        var answer = await BoardsTestData.ReorderAsync(factory, world, world.PlatformId, new { itemId = us11.Id.ToString(), position });

        BoardsTestData.AssertRefused(answer, HttpStatusCode.BadRequest, "boards.validation_failed");
        var reason = Assert.Single(answer.Body.GetProperty("errors").GetProperty("position").EnumerateArray());
        Assert.Equal(("boards.backlog.position_invalid", "Некорректная позиция перемещения."), (
            reason.GetProperty("code").GetString(), reason.GetProperty("detail").GetString()));
    }

    // api-contract § 3.2 step 3: a missing, malformed or unknown id names no item of this backlog — stale, nothing changes.
    [Theory]
    [InlineData(null, "top", null)]
    [InlineData("not-an-id", "top", null)]
    [InlineData("unknown", "top", null)]
    [InlineData("us11", "after", null)]
    [InlineData("us11", "after", "not-an-id")]
    public async Task Reorder_ItemOrAnchorNamingNothing_Returns409Stale(string? item, string position, string? after)
    {
        var (world, _, _, _, us11) = await BacklogAsync();

        string? Id(string? value) => value switch
        {
            "us11" => us11.Id.ToString(),
            "unknown" => Guid.CreateVersion7().ToString(),
            _ => value,
        };

        var answer = await BoardsTestData.ReorderAsync(
            factory, world, world.PlatformId, new { itemId = Id(item), position, afterItemId = Id(after) });

        BoardsTestData.AssertRefused(answer, HttpStatusCode.Conflict, "boards.backlog.reorder_stale");
        Assert.Empty(await PlacesAsync(world));
    }

    // An item of another level, or of another team's backlog, is not on this backlog either.
    [Fact]
    public async Task Reorder_ItemOfAnotherBacklog_Returns409Stale()
    {
        var (world, _, _, _, us11) = await BacklogAsync();
        var epic = await BoardsTestData.CreateAsync(factory, world, "EPIC", "Эпик", world.PlatformAreaId);
        var rootStory = await BoardsTestData.CreateAsync(factory, world, "US", "В корне");

        BoardsTestData.AssertRefused(
            await BoardsTestData.ReorderAsync(factory, world, world.PlatformId, BoardsTestData.Top(epic)), HttpStatusCode.Conflict, "boards.backlog.reorder_stale");
        BoardsTestData.AssertRefused(
            await BoardsTestData.ReorderAsync(factory, world, world.PlatformId, BoardsTestData.After(us11, rootStory)), HttpStatusCode.Conflict, "boards.backlog.reorder_stale");
    }

    // api-contract § 3.2: after itself, the item stays where it is; the reorder still places the backlog.
    [Fact]
    public async Task Reorder_AfterItself_LeavesTheOrder()
    {
        var (world, us3, bug5, us9, us11) = await BacklogAsync();

        await BoardsTestData.MoveAsync(factory, world, world.PlatformId, BoardsTestData.After(bug5, bug5));

        Assert.Equal(Ids(us3, bug5, us9, us11), await BoardsTestData.RowsAsync(factory, world, world.PlatformId));
        Assert.Equal(4, (await PlacesAsync(world)).Count);
    }

    // The epics backlog orders its own items, apart from the stories' (BR-M4-1: one backlog per team and level).
    [Fact]
    public async Task Reorder_EpicsBacklog_IsItsOwnOrder()
    {
        var (world, us3, bug5, us9, us11) = await BacklogAsync();
        var first = await BoardsTestData.CreateAsync(factory, world, "EPIC", "Первый эпик", world.PlatformAreaId);
        var second = await BoardsTestData.CreateAsync(factory, world, "EPIC", "Второй эпик", world.PlatformAreaId);

        await BoardsTestData.MoveAsync(factory, world, world.PlatformId, BoardsTestData.Top(second), "epics");

        Assert.Equal(Ids(second, first), await BoardsTestData.RowsAsync(factory, world, world.PlatformId, "epics"));
        Assert.Equal(Ids(us3, bug5, us9, us11), await BoardsTestData.RowsAsync(factory, world, world.PlatformId));
    }

    // US-1's not-found outline, for the reorder: the address answers before the body.
    [Theory]
    [InlineData("team-deleted", "organizations.team.not_found")]
    [InlineData("team-of-another-project", "organizations.team.not_found")]
    [InlineData("project-deactivated", "organizations.project.not_found")]
    [InlineData("level-tasks", "boards.level.not_found")]
    public async Task Reorder_ContextNotFound_AnswersItsLevelsCode(string part, string code)
    {
        var world = await BoardsTestData.CreateWorldAsync(factory);
        var address = await GetBacklogEndpointTests.NotFoundAddressAsync(factory, world, part, "backlogs");

        var answer = await OrganizationsApi.SendAsync(
            factory, HttpMethod.Post, $"{address}/reorder", new { itemId = Guid.CreateVersion7().ToString(), position = "sideways" }, OrganizationsTokens.SignedIn());

        BoardsTestData.AssertRefused(answer, HttpStatusCode.NotFound, code);
    }

    // The team deleted while the reorder waited for its lock (api-contract § 0.4 step 3): the deletion holds the team at its
    // commit; the reorder waits on the team, then answers not found and places nothing.
    [Fact]
    public async Task Reorder_TeamDeletedWhileWaiting_Returns404TeamNotFound()
    {
        var world = await BoardsTestData.CreateWorldAsync(factory);
        var (analytics, area) = await BoardsTestData.AddTeamAsync(factory, world, "Аналитика");
        var story = await BoardsTestData.CreateAsync(factory, world, "US", "Аналитика", area);

        using var gate = factory.CommitGates.Open();
        var deletion = OrganizationsApi.SendAsync(
            factory, HttpMethod.Delete, BoardsTestData.Team(world, analytics), token: OrganizationsTokens.SignedIn(), gate: gate);
        await Task.WhenAny(gate.Reached, deletion);
        Assert.False(deletion.IsCompleted, "The deletion finished before reaching its commit gate.");

        // The address check reads the committed team, active; the lock then waits for the deletion.
        var reorder = BoardsTestData.ReorderAsync(factory, world, analytics, BoardsTestData.Top(story));
        await PgLocks.WaitUntilBlockedAsync(factory.ConnectionString, reorder);
        Assert.False(reorder.IsCompleted, "The reorder did not wait on the team's lock.");

        gate.Release();
        Assert.Equal(HttpStatusCode.NoContent, (await deletion).Status);
        BoardsTestData.AssertRefused(await reorder, HttpStatusCode.NotFound, "organizations.team.not_found");
        Assert.DoesNotContain(await PlacesAsync(world, analytics), place => place.ItemId == story.Id);
    }

    private async Task<List<StoredPlace>> PlacesAsync(BoardsWorld world, Guid? teamId = null)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var team = teamId ?? world.PlatformId;

        return await scope.ServiceProvider.GetRequiredService<MustaskDbContext>().Database
            .SqlQuery<StoredPlace>($"SELECT work_item_id AS \"ItemId\", position AS \"Position\" FROM backlog_places WHERE team_id = {team} ORDER BY work_item_id")
            .ToListAsync();
    }

    private sealed record StoredPlace(Guid ItemId, int Position);
}
