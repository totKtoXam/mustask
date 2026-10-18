using System.Net;
using System.Text.Json;
using Mustask.Api.IntegrationTests.Features.Boards;
using Mustask.Api.IntegrationTests.Features.Organizations;

namespace Mustask.Api.IntegrationTests.Features.WorkItems;

/// <summary>
/// mod-005 US-5 · "A deletion does not cascade" (BR-DR-3, BR-DR-4, BR-DR-18; doc-002 BR-M3-9): deleting a parent keeps every child
/// as it was — state, link and «Изменён» — and a child of a deleted parent reads it as its «Родитель» and is a top-level row in the
/// backlogs. Feat-001's US-7 scenarios, which used to write the state straight in the table, start from the real Delete here.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class DeleteDoesNotCascadeEndpointTests(MustaskApiFactory factory)
{
    /// <summary>A feature with two stories beneath it, and a task beneath the first story.</summary>
    private async Task<(BoardsWorld World, Item Feature, Item First, Item Second, Item Task)> WorldAsync()
    {
        var world = await BoardsTestData.CreateWorldAsync(factory);
        var feature = await BoardsTestData.CreateAsync(factory, world, "FEAT", "Фича", world.PlatformAreaId);
        var first = await BoardsTestData.CreateAsync(factory, world, "US", "Первая история", world.PlatformAreaId);
        var second = await BoardsTestData.CreateAsync(factory, world, "US", "Вторая история", world.PlatformAreaId);
        var task = await BoardsTestData.CreateAsync(factory, world, "TASK", "Задача");
        await LinksTestData.LinkAsync(factory, world.Items, first, feature);
        await LinksTestData.LinkAsync(factory, world.Items, second, feature);
        await LinksTestData.LinkAsync(factory, world.Items, task, first);

        return (world, feature, first, second, task);
    }

    // US-5 · "Deleting a parent keeps every child, its state and its link; no child's «Изменён» moves".
    [Fact]
    public async Task Delete_AParent_KeepsEveryChild_StateLinkAndChangedAt()
    {
        var (world, feature, first, second, task) = await WorldAsync();
        var before = new Dictionary<string, (Guid? ParentId, Guid StateId, DateTimeOffset ChangedAt)>();

        foreach (var item in new[] { first, second, task })
        {
            before[item.Identifier] = await LinksTestData.StoredAsync(factory, item);
        }

        await LinksTestData.DeleteAsync(factory, world.Items, feature);

        foreach (var item in new[] { first, second, task })
        {
            Assert.Equal(before[item.Identifier], await LinksTestData.StoredAsync(factory, item));
        }

        Assert.Equal(feature.Id, await LinksTestData.StoredParentAsync(factory, first));
        Assert.Equal(feature.Id, await LinksTestData.StoredParentAsync(factory, second));
        var form = await WorkItemsTestData.ReadAsync(factory, world.Items, first.Identifier);
        Assert.Equal((feature.Identifier, "Удалена"), (LinksTestData.IdentifierOf(form.GetProperty("parent")), form.GetProperty("parent").GetProperty("state").GetProperty("name").GetString()));
    }

    // US-5 · "A child of a deleted parent is unparented in the backlogs": a top-level row whose «Родитель» cell is «Нет»; a
    // live parent's child keeps the cell (BR-H-7, feat-027 BR-T-4).
    [Fact]
    public async Task ProjectBacklog_ChildrenOfADeletedParent_AreTopLevelRows_ParentCellNone()
    {
        var (world, feature, first, second, _) = await WorldAsync();
        var before = await ProjectBoardsTestData.GetBacklogAsync(factory, world);
        var beforeRows = BacklogTreeTestData.AllRows(before.Body.GetProperty("items").EnumerateArray()).ToArray();
        Assert.Equal(feature.Identifier, BacklogTreeTestData.ParentOf(BacklogTreeTestData.Row(beforeRows, first.Identifier)));

        await LinksTestData.DeleteAsync(factory, world.Items, feature);

        var after = await ProjectBoardsTestData.GetBacklogAsync(factory, world);
        var rows = after.Body.GetProperty("items").EnumerateArray().ToArray();
        Assert.Equal([first.Identifier, second.Identifier], rows.Select(row => row.GetProperty("identifier").GetString()!).Order(StringComparer.Ordinal));
        Assert.All(rows, row => Assert.Null(BacklogTreeTestData.ParentOf(row)));
        Assert.DoesNotContain(feature.Identifier, BacklogTreeTestData.AllRows(rows).Select(row => row.GetProperty("identifier").GetString()));
    }

    // US-5 · "A deleted child leaves its parent's form, checklist and counter".
    [Fact]
    public async Task Delete_ALeaf_LeavesItsParentsChangedAtStateAndOtherChildren()
    {
        var world = await BoardsTestData.CreateWorldAsync(factory);
        var story = await BoardsTestData.CreateAsync(factory, world, "US", "История", world.PlatformAreaId);
        var kept = await BoardsTestData.CreateAsync(factory, world, "TASK", "Остаётся");
        var gone = await BoardsTestData.CreateAsync(factory, world, "TASK", "Уходит");
        await LinksTestData.LinkAsync(factory, world.Items, kept, story);
        await LinksTestData.LinkAsync(factory, world.Items, gone, story);
        var parentBefore = await LinksTestData.StoredAsync(factory, story);
        var keptBefore = await LinksTestData.StoredAsync(factory, kept);
        Assert.Equal(2, ChecklistOf(await BoardsTestData.GetBoardAsync(factory, world, world.PlatformId), story).Length);

        await LinksTestData.DeleteAsync(factory, world.Items, gone);

        Assert.Equal(parentBefore, await LinksTestData.StoredAsync(factory, story));
        Assert.Equal(keptBefore, await LinksTestData.StoredAsync(factory, kept));
        var form = await WorkItemsTestData.ReadAsync(factory, world.Items, story.Identifier);
        Assert.Equal([kept.Identifier], LinksTestData.Identifiers(form.GetProperty("children")));
        Assert.Equal(
            [kept.Identifier],
            ChecklistOf(await BoardsTestData.GetBoardAsync(factory, world, world.PlatformId), story).Select(child => child.GetProperty("identifier").GetString()!));
    }

    private static JsonElement[] ChecklistOf(Answer board, Item card)
    {
        Assert.Equal(HttpStatusCode.OK, board.Status);

        return board.Body.GetProperty("columns").EnumerateArray()
            .SelectMany(column => column.GetProperty("items").EnumerateArray())
            .Single(candidate => candidate.GetProperty("identifier").GetString() == card.Identifier)
            .GetProperty("checklist").EnumerateArray().ToArray();
    }

    // US-5 · "A deleted item is not a parent anyone can name": no write adds a link to or from it.
    [Fact]
    public async Task SetParent_ToADeletedParent_AndFromADeletedChild_Is409ItemDeleted_NothingLinked()
    {
        var world = await BoardsTestData.CreateWorldAsync(factory);
        var feature = await BoardsTestData.CreateAsync(factory, world, "FEAT", "Фича", world.PlatformAreaId);
        var story = await BoardsTestData.CreateAsync(factory, world, "US", "История", world.PlatformAreaId);
        var other = await BoardsTestData.CreateAsync(factory, world, "US", "Другая", world.PlatformAreaId);
        await LinksTestData.DeleteAsync(factory, world.Items, feature);
        await LinksTestData.DeleteAsync(factory, world.Items, other);

        var toDeletedParent = await LinksTestData.SetParentAsync(factory, world.Items, story.Identifier, feature.Id, null);
        var fromDeletedChild = await LinksTestData.SetParentAsync(factory, world.Items, other.Identifier, null, null);

        LinksTestData.AssertRefused(toDeletedParent, HttpStatusCode.Conflict, "workitems.link.item_deleted");
        LinksTestData.AssertRefused(fromDeletedChild, HttpStatusCode.Conflict, "workitems.link.item_deleted");
        Assert.Null(await LinksTestData.StoredParentAsync(factory, story));
    }
}
