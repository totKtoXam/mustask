using System.Net;
using System.Text.Json;
using Mustask.Api.IntegrationTests.Features.WorkItems;

namespace Mustask.Api.IntegrationTests.Features.Boards;

/// <summary>
/// feat-022 US-6 (api-contract § 5, BR-M3-39, OQ-1 (a)): every backlog row —
/// nested rows included — and every board card carries the item's «Приоритет»,
/// as the form last saved it; a reorder or a card move changes no priority.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class BacklogPriorityEndpointTests(MustaskApiFactory factory)
{
    private async Task SetPriorityAsync(BoardsWorld world, Item item, int priority)
    {
        var answer = await WorkItemsTestData.SaveAsync(factory, world.Items, item.Identifier, new { priority = new { value = priority } });

        Assert.Equal(HttpStatusCode.OK, answer.Status);
    }

    /// <summary>Every row of the backlog, nested ones included, as «identifier:priority», depth first.</summary>
    private async Task<string[]> BacklogPrioritiesAsync(BoardsWorld world, string level)
    {
        var answer = await BoardsTestData.GetBacklogAsync(factory, world, world.PlatformId, level);
        Assert.Equal(HttpStatusCode.OK, answer.Status);

        return Flatten(answer.Body.GetProperty("items").EnumerateArray()).ToArray();

        static IEnumerable<string> Flatten(IEnumerable<JsonElement> rows) =>
            rows.SelectMany(row => new[] { $"{row.GetProperty("identifier").GetString()}:{row.GetProperty("priority").GetInt32()}" }
                .Concat(Flatten(row.GetProperty("children").EnumerateArray())));
    }

    /// <summary>Every card of the board as «identifier:priority», column by column.</summary>
    private async Task<string[]> CardPrioritiesAsync(BoardsWorld world)
    {
        var answer = await BoardsTestData.GetBoardAsync(factory, world, world.PlatformId);
        Assert.Equal(HttpStatusCode.OK, answer.Status);

        return answer.Body.GetProperty("columns").EnumerateArray()
            .SelectMany(column => column.GetProperty("items").EnumerateArray())
            .Select(card => $"{card.GetProperty("identifier").GetString()}:{card.GetProperty("priority").GetInt32()}")
            .ToArray();
    }

    // US-6 · "The backlog shows a «Приоритет» column": MSK-US-1 of 1 and MSK-BUG-2 of 3 on «Истории»; the «Эпики» and «Фичи»
    // backlogs carry it too, a nested row included.
    [Fact]
    public async Task Backlog_EveryRowAndNestedRow_CarriesItsPriority()
    {
        var world = await BoardsTestData.CreateWorldAsync(factory);
        var story = await BoardsTestData.CreateAsync(factory, world, "US", "Вход по ссылке из письма", world.PlatformAreaId);
        var bug = await BoardsTestData.CreateAsync(factory, world, "BUG", "Падение при входе", world.PlatformAreaId);
        var epic = await BoardsTestData.CreateAsync(factory, world, "EPIC", "Вход и регистрация", world.PlatformAreaId);
        var feature = await BoardsTestData.CreateAsync(factory, world, "FEAT", "Вход через SSO", world.PlatformAreaId);
        await SetPriorityAsync(world, story, 1);
        await SetPriorityAsync(world, bug, 3);
        await SetPriorityAsync(world, epic, 4);
        var linked = await WorkItemsTestData.SaveAsync(
            factory, world.Items, feature.Identifier, new { parent = new { parentId = epic.Id.ToString(), seenParentId = (string?)null } });
        Assert.Equal(HttpStatusCode.OK, linked.Status);

        Assert.Equal(["MSK-US-1:1", "MSK-BUG-2:3"], await BacklogPrioritiesAsync(world, "stories"));
        Assert.Equal(["MSK-EPIC-3:4", "MSK-FEAT-4:2"], await BacklogPrioritiesAsync(world, "epics"));
        Assert.Equal(["MSK-FEAT-4:2"], await BacklogPrioritiesAsync(world, "features"));
    }

    // US-6 · "A card shows the priority", "A change on the form shows on the backlog and the board".
    [Fact]
    public async Task Board_CardsCarryThePriority_AFormChangeShowsOnBoth()
    {
        var world = await BoardsTestData.CreateWorldAsync(factory);
        var story = await BoardsTestData.CreateAsync(factory, world, "US", "Вход по ссылке из письма", world.PlatformAreaId);
        await SetPriorityAsync(world, story, 1);

        Assert.Equal(["MSK-US-1:1"], await CardPrioritiesAsync(world));

        await SetPriorityAsync(world, story, 4);

        Assert.Equal(["MSK-US-1:4"], await BacklogPrioritiesAsync(world, "stories"));
        Assert.Equal(["MSK-US-1:4"], await CardPrioritiesAsync(world));
    }

    // US-6 · "Nothing else on the backlog or the board changes": a reorder and a card move change no item's priority.
    [Fact]
    public async Task ReorderAndCardMove_ChangeNoPriority()
    {
        var world = await BoardsTestData.CreateWorldAsync(factory);
        var first = await BoardsTestData.CreateAsync(factory, world, "US", "Первая", world.PlatformAreaId);
        var second = await BoardsTestData.CreateAsync(factory, world, "BUG", "Вторая", world.PlatformAreaId);
        await SetPriorityAsync(world, first, 1);
        await SetPriorityAsync(world, second, 4);

        await BoardsTestData.MoveAsync(factory, world, world.PlatformId, BoardsTestData.Top(second));
        Assert.Equal(HttpStatusCode.OK, (await BoardsTestData.SetStateAsync(factory, world, first, "US", "Активна")).Status);

        Assert.Equal(["MSK-BUG-2:4", "MSK-US-1:1"], await BacklogPrioritiesAsync(world, "stories"));
        Assert.Equal(
            ("priority=1", "priority=4"),
            (
                $"priority={(await WorkItemsTestData.ReadAsync(factory, world.Items, first.Identifier)).GetProperty("priority").GetInt32()}",
                $"priority={(await WorkItemsTestData.ReadAsync(factory, world.Items, second.Identifier)).GetProperty("priority").GetInt32()}"));
    }
}
