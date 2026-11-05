using System.Net;
using System.Text.Json;
using Mustask.Api.IntegrationTests.Features.Boards;
using Mustask.Api.IntegrationTests.Features.Organizations;

namespace Mustask.Api.IntegrationTests.Features.WorkItems;

/// <summary>
/// mod-005 US-5 · "No cascade", the restore half (BR-DR-4, BR-DR-10): a parent restored brings back the links its children kept;
/// a child restored restores no parent; a chain of deleted items restores one by one in any order; and a link released by a
/// purge-like clean-up stays released. Feat-001's US-7 scenarios, set up by the real Delete.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class RestoreDoesNotCascadeEndpointTests(MustaskApiFactory factory)
{
    private Task<Answer> RestoreAsync(WorkItemsWorld world, Item item, object? body = null) =>
        OrganizationsApi.SendAsync(
            factory, HttpMethod.Post, $"{WorkItemsTestData.Item(world, item.Identifier)}/restore", body, OrganizationsTokens.SignedIn());

    private async Task RestoredAsync(WorkItemsWorld world, Item item)
    {
        var answer = await RestoreAsync(world, item);
        Assert.True(answer.Status == HttpStatusCode.OK, $"Restoring {item.Identifier} answered {answer.Status} {answer.ErrorCode}.");
    }

    // US-5 · "Restoring the parent brings its kept links back; a released link stays released": the children that kept their
    // link read the parent live again and nest beneath it in the backlog; a child that let go of it before stays without.
    [Fact]
    public async Task Restore_TheParent_BringsItsKeptChildrenBack_NestsThemInTheBacklog_AReleasedLinkStaysReleased()
    {
        var world = await BoardsTestData.CreateWorldAsync(factory);
        var feature = await BoardsTestData.CreateAsync(factory, world, "FEAT", "Фича", world.PlatformAreaId);
        var kept = await BoardsTestData.CreateAsync(factory, world, "US", "Сохранила связь", world.PlatformAreaId);
        var released = await BoardsTestData.CreateAsync(factory, world, "US", "Отпустила связь", world.PlatformAreaId);
        await LinksTestData.LinkAsync(factory, world.Items, kept, feature);
        await LinksTestData.LinkAsync(factory, world.Items, released, feature);
        await LinksTestData.DeleteAsync(factory, world.Items, feature);

        // The released child lets go of the deleted parent (a form save of «Нет родителя» that saw it).
        var release = await WorkItemsTestData.SaveAsync(
            factory, world.Items, released.Identifier, new { parent = new { parentId = (string?)null, seenParentId = feature.Id.ToString() } });
        Assert.Equal(HttpStatusCode.OK, release.Status);
        Assert.Null(await LinksTestData.StoredParentAsync(factory, released));

        await RestoredAsync(world.Items, feature);

        Assert.Equal(feature.Id, await LinksTestData.StoredParentAsync(factory, kept));
        Assert.Null(await LinksTestData.StoredParentAsync(factory, released));
        var parent = await WorkItemsTestData.ReadAsync(factory, world.Items, feature.Identifier);
        Assert.Equal([kept.Identifier], LinksTestData.Identifiers(parent.GetProperty("children")));
        var keptForm = await WorkItemsTestData.ReadAsync(factory, world.Items, kept.Identifier);
        Assert.NotEqual("Removed", keptForm.GetProperty("parent").GetProperty("state").GetProperty("category").GetProperty("code").GetString());
    }

    // US-5 · "Restoring a child does not restore its parent": the parent stays deleted, and the child reads it as «Удалена».
    [Fact]
    public async Task Restore_AChildOfADeletedParent_StaysUnparentedInViews_ParentStaysDeleted()
    {
        var world = await BoardsTestData.CreateWorldAsync(factory);
        var feature = await BoardsTestData.CreateAsync(factory, world, "FEAT", "Фича", world.PlatformAreaId);
        var story = await BoardsTestData.CreateAsync(factory, world, "US", "История", world.PlatformAreaId);
        await LinksTestData.LinkAsync(factory, world.Items, story, feature);
        await LinksTestData.DeleteAsync(factory, world.Items, story);
        await LinksTestData.DeleteAsync(factory, world.Items, feature);

        await RestoredAsync(world.Items, story);

        var child = await WorkItemsTestData.ReadAsync(factory, world.Items, story.Identifier);
        Assert.Equal("Removed", child.GetProperty("parent").GetProperty("state").GetProperty("category").GetProperty("code").GetString());
        var parent = await WorkItemsTestData.ReadAsync(factory, world.Items, feature.Identifier);
        Assert.Equal("Removed", parent.GetProperty("state").GetProperty("category").GetProperty("code").GetString());
        // The kept link is live again from the parent's side: the restored child is among the deleted parent's children.
        Assert.Equal([story.Identifier], LinksTestData.Identifiers(parent.GetProperty("children")));
        var rows = (await ProjectBoardsTestData.GetBacklogAsync(factory, world)).Body.GetProperty("items").EnumerateArray().ToArray();
        Assert.Null(BacklogTreeTestData.ParentOf(BacklogTreeTestData.Row(rows, story.Identifier)));
    }

    // US-5 · "Deleting a chain deletes nothing below it; each restores alone in any order": an epic, a feature and a story, each
    // beneath the one above; deleted top-down, restored bottom-up and in other orders, each links as it did.
    [Theory]
    [InlineData("EPIC FEAT US")]
    [InlineData("US FEAT EPIC")]
    [InlineData("FEAT EPIC US")]
    public async Task Delete_AChain_DeletesNothingBelow_EachRestoresAlone_InAnyOrder(string restoreOrder)
    {
        var world = await BoardsTestData.CreateWorldAsync(factory);
        var epic = await BoardsTestData.CreateAsync(factory, world, "EPIC", "Эпик", world.PlatformAreaId);
        var feature = await BoardsTestData.CreateAsync(factory, world, "FEAT", "Фича", world.PlatformAreaId);
        var story = await BoardsTestData.CreateAsync(factory, world, "US", "История", world.PlatformAreaId);
        await LinksTestData.LinkAsync(factory, world.Items, feature, epic);
        await LinksTestData.LinkAsync(factory, world.Items, story, feature);
        var byKey = new Dictionary<string, Item> { ["EPIC"] = epic, ["FEAT"] = feature, ["US"] = story };

        // Top-down: deleting the epic leaves the feature and the story live; deleting the feature leaves the story.
        await LinksTestData.DeleteAsync(factory, world.Items, epic);
        Assert.Equal("Removed", await CategoryAsync(world, epic));
        Assert.NotEqual("Removed", await CategoryAsync(world, feature));
        await LinksTestData.DeleteAsync(factory, world.Items, feature);
        Assert.NotEqual("Removed", await CategoryAsync(world, story));
        await LinksTestData.DeleteAsync(factory, world.Items, story);

        foreach (var key in restoreOrder.Split(' '))
        {
            await RestoredAsync(world.Items, byKey[key]);
            Assert.NotEqual("Removed", await CategoryAsync(world, byKey[key]));
        }

        Assert.Equal(epic.Id, await LinksTestData.StoredParentAsync(factory, feature));
        Assert.Equal(feature.Id, await LinksTestData.StoredParentAsync(factory, story));

        foreach (var item in new[] { epic, feature, story })
        {
            Assert.Equal(JsonValueKind.Null, (await WorkItemsTestData.ReadAsync(factory, world.Items, item.Identifier)).GetProperty("deletion").ValueKind);
        }
    }

    private async Task<string> CategoryAsync(BoardsWorld world, Item item) =>
        (await WorkItemsTestData.ReadAsync(factory, world.Items, item.Identifier)).GetProperty("state").GetProperty("category").GetProperty("code").GetString()!;

    // US-2 · "A board card's checklist skips a deleted child; restored, it returns in creation order" (feat-032 BR-C-5).
    [Fact]
    public async Task Checklist_SkipsADeletedChild_RestoredItReturnsInCreationOrder()
    {
        var world = await BoardsTestData.CreateWorldAsync(factory);
        var story = await BoardsTestData.CreateAsync(factory, world, "US", "История", world.PlatformAreaId);
        var first = await BoardsTestData.CreateAsync(factory, world, "TASK", "Первая");
        var second = await BoardsTestData.CreateAsync(factory, world, "TASK", "Вторая");
        var third = await BoardsTestData.CreateAsync(factory, world, "TASK", "Третья");
        foreach (var child in new[] { first, second, third })
        {
            await LinksTestData.LinkAsync(factory, world.Items, child, story);
        }

        await LinksTestData.DeleteAsync(factory, world.Items, second);

        Assert.Equal([first.Identifier, third.Identifier], await ChecklistAsync(world, story));

        await RestoredAsync(world.Items, second);

        Assert.Equal([first.Identifier, second.Identifier, third.Identifier], await ChecklistAsync(world, story));
    }

    private async Task<string[]> ChecklistAsync(BoardsWorld world, Item card)
    {
        var board = await BoardsTestData.GetBoardAsync(factory, world, world.PlatformId);
        Assert.Equal(HttpStatusCode.OK, board.Status);

        return board.Body.GetProperty("columns").EnumerateArray()
            .SelectMany(column => column.GetProperty("items").EnumerateArray())
            .Single(candidate => candidate.GetProperty("identifier").GetString() == card.Identifier)
            .GetProperty("checklist").EnumerateArray().Select(child => child.GetProperty("identifier").GetString()!).ToArray();
    }

    // BR-DR-10 · "No hierarchy check": the child and its parent are both deleted and restored child first, parent second; the
    // stored link was kept the whole time and neither restore is a link write, so neither is refused for a cycle or a depth.
    [Fact]
    public async Task Restore_NeverReadsTheHierarchy_ItIsAStateWriteOnly()
    {
        var world = await BoardsTestData.CreateWorldAsync(factory);
        var feature = await BoardsTestData.CreateAsync(factory, world, "FEAT", "Фича", world.PlatformAreaId);
        var story = await BoardsTestData.CreateAsync(factory, world, "US", "История", world.PlatformAreaId);
        await LinksTestData.LinkAsync(factory, world.Items, story, feature);
        var link = await LinksTestData.StoredAsync(factory, story);
        await LinksTestData.DeleteAsync(factory, world.Items, story);
        await LinksTestData.DeleteAsync(factory, world.Items, feature);

        await RestoredAsync(world.Items, story);
        await RestoredAsync(world.Items, feature);

        Assert.Equal(feature.Id, await LinksTestData.StoredParentAsync(factory, story));
        Assert.Equal(link.ParentId, (await LinksTestData.StoredAsync(factory, story)).ParentId);
    }
}
