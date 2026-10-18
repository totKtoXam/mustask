using System.Net;
using System.Text.Json;
using Mustask.Api.IntegrationTests.Features.WorkItems;

namespace Mustask.Api.IntegrationTests.Features.Boards;

/// <summary>
/// feat-027 US-1 (api-contract § 2.1, BR-T-2, BR-T-3): what every backlog row
/// carries for the reference layout — «Порядок» on top-level rows only, and
/// beside feat-022's «Приоритет» the three planning values «Трудоёмкость»,
/// «Бизнес-ценность», «Вид ценности», as the form stores them, <c>null</c> where
/// the type has no such field. US-1's data, the values saved on the forms.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class BacklogLayoutEndpointTests(MustaskApiFactory factory)
{
    private async Task SaveAsync(BoardsWorld world, string identifier, object body)
    {
        var answer = await WorkItemsTestData.SaveAsync(factory, world.Items, identifier, body);

        Assert.True(answer.Status == HttpStatusCode.OK, $"Saving {identifier} answered {answer.Status} {answer.ErrorCode}.");
    }

    /// <summary>US-1's values: MSK-EPIC-1 20 / 300 / «Бизнес» / 1, MSK-EPIC-20 «Архитектура» / 2, MSK-FEAT-8 2,5, MSK-TASK-3 3.</summary>
    private async Task<BoardsWorld> WorldWithValuesAsync()
    {
        var (world, _) = await BacklogTreeTestData.CreateWorldAsync(factory);
        await SaveAsync(world, "MSK-EPIC-1", new { effort = new { value = 20 }, businessValue = new { value = 300 }, priority = new { value = 1 } });
        await SaveAsync(world, "MSK-EPIC-20", new { valueArea = new { value = "Architectural" } });
        await SaveAsync(world, "MSK-FEAT-8", new { effort = new { value = 2.5m } });
        await SaveAsync(world, "MSK-TASK-3", new { priority = new { value = 3 } });

        return world;
    }

    /// <summary>A row's four values as raw JSON: «effort|businessValue|valueArea|priority».</summary>
    private static string Values(JsonElement row) =>
        string.Join('|', new[] { "effort", "businessValue", "valueArea", "priority" }.Select(name => row.GetProperty(name).GetRawText()));

    // US-1 · "The planning values, and blanks where a type has no field": every row of «Эпики» — top-level and nested,
    // a bug and a task included — reads the values the form stored; a field the type does not have, or an empty one, is
    // null. Effort in its shortest form (2.5, 20).
    [Fact]
    public async Task Get_EveryRow_CarriesThePlanningValues_BlankWhereTheTypeHasNone()
    {
        var world = await WorldWithValuesAsync();

        var rows = await BacklogTreeTestData.TopRowsAsync(factory, world, world.PlatformId, "epics");

        Assert.Equal(
            [
                "MSK-EPIC-1 20|300|\"Business\"|1",
                "MSK-FEAT-8 2.5|null|\"Business\"|2",
                "MSK-FEAT-23 null|null|\"Business\"|2",
                "MSK-FEAT-6 null|null|\"Business\"|2",
                "MSK-US-2 null|null|\"Business\"|2",
                "MSK-BUG-5 null|null|null|2",
                "MSK-TASK-3 null|null|null|3",
                "MSK-BUG-4 null|null|null|2",
                "MSK-US-22 null|null|\"Business\"|2",
                "MSK-EPIC-20 null|null|\"Architectural\"|2",
            ],
            BacklogTreeTestData.AllRows(rows).Select(row => $"{row.GetProperty("identifier").GetString()} {Values(row)}"));
    }

    // US-1 · the same values on every level (OQ-7): MSK-FEAT-8 as a top-level row of «Фичи», MSK-US-2 of «Истории».
    [Fact]
    public async Task Get_TheSameValuesOnEveryLevel()
    {
        var world = await WorldWithValuesAsync();

        var features = await BacklogTreeTestData.TopRowsAsync(factory, world, world.PlatformId, "features");
        var stories = await BacklogTreeTestData.TopRowsAsync(factory, world, world.PlatformId, "stories");

        Assert.Equal("2.5|null|\"Business\"|2", Values(features[0]));
        Assert.Equal("null|null|\"Business\"|2", Values(stories[0]));
        Assert.Equal("null|null|null|3", Values(BacklogTreeTestData.Row(stories, "MSK-TASK-3")));
    }

    // US-1 · "A value changed on the form shows at the next read": MSK-EPIC-1's «Трудоёмкость» saved as 13; emptied, null.
    [Fact]
    public async Task Get_AValueChangedOnTheForm_ShowsAtTheNextRead()
    {
        var world = await WorldWithValuesAsync();

        await SaveAsync(world, "MSK-EPIC-1", new { effort = new { value = 13 } });
        var changed = BacklogTreeTestData.Row(await BacklogTreeTestData.TopRowsAsync(factory, world, world.PlatformId, "epics"), "MSK-EPIC-1");
        await SaveAsync(world, "MSK-EPIC-1", new { effort = new { value = (decimal?)null } });
        var emptied = BacklogTreeTestData.Row(await BacklogTreeTestData.TopRowsAsync(factory, world, world.PlatformId, "epics"), "MSK-EPIC-1");

        Assert.Equal(13m, changed.GetProperty("effort").GetDecimal());
        Assert.Equal(JsonValueKind.Null, emptied.GetProperty("effort").ValueKind);
    }

    // US-1 · "Only top-level rows are numbered" (BR-T-2): «Порядок» is dense over the top-level rows of each level, and
    // a nested row carries none; the members of a row are the contract's, in its order.
    [Fact]
    public async Task Get_OrderOnTopLevelRowsOnly_TheRowsMembersAreTheContracts()
    {
        var world = await WorldWithValuesAsync();

        var epics = await BacklogTreeTestData.TopRowsAsync(factory, world, world.PlatformId, "epics");

        Assert.Equal([1, 2], epics.Select(row => row.GetProperty("order").GetInt32()));
        Assert.Equal(
            ["order", "id", "identifier", "number", "type", "title", "assignee", "state", "priority", "team", "project", "effort", "businessValue", "valueArea", "parent", "children"],
            epics[0].EnumerateObject().Select(member => member.Name));
        Assert.Equal(
            ["id", "identifier", "number", "type", "title", "assignee", "state", "priority", "team", "project", "effort", "businessValue", "valueArea", "parent", "children"],
            epics[0].GetProperty("children")[0].EnumerateObject().Select(member => member.Name));
    }
}
