using System.Net;
using Mustask.Api.IntegrationTests.Features.Boards;
using Mustask.Api.IntegrationTests.Features.Organizations;
using Mustask.Domain.Identity;
using Mustask.Domain.Process;

namespace Mustask.Api.IntegrationTests.Features.WorkItems;

/// <summary>
/// mod-005 US-3 · "A deleted item takes no write" (BR-DR-8, ADR-0030 rule 3): <c>PATCH …/workitems/{identifier}</c> on an item
/// in «Удалена» answers 409 <c>workitems.workitem.deleted</c> whatever the body carries, decided on the stored state under the
/// item's lock before any value is looked at, and changes nothing. The item is put in «Удалена» straight in the table (the
/// helper writes no marks): the refusal is the category's, not the marks'. Closes tech-012 m1.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class DeletedItemIsReadOnlyEndpointTests(MustaskApiFactory factory)
{
    private const string DeletedCode = "workitems.workitem.deleted";

    /// <summary>A world with a live story «История» and a deleted task «Задача» beneath no one.</summary>
    private async Task<(BoardsWorld World, Item Story, Item Task)> WorldAsync()
    {
        var world = await BoardsTestData.CreateWorldAsync(factory);
        var story = await BoardsTestData.CreateAsync(factory, world, "US", "История", world.PlatformAreaId);
        var task = await BoardsTestData.CreateAsync(factory, world, "TASK", "Задача", world.PlatformAreaId);
        await LinksTestData.DeleteAsync(factory, world.Items, task);

        return (world, story, task);
    }

    /// <summary>Everything the table holds of the project's items and of this item's link and team, as one comparable text.</summary>
    private async Task<string> SnapshotAsync(BoardsWorld world, Item item)
    {
        var items = await WorkItemsTestData.StoredAsync(factory, world.ProjectId);
        var link = await LinksTestData.StoredAsync(factory, item);
        var team = await OwningTeamTestData.StoredAsync(factory, world.ProjectId, item.Identifier);

        return string.Join(
            "|",
            items.Select(stored => $"{stored.Number}:{stored.Title}:{stored.Description}:{stored.StateId}:{stored.AssigneeId}:{stored.AreaId}:{stored.ChangedAt:O}")
                .Append($"{link.ParentId}:{link.StateId}:{link.ChangedAt:O}:{team.TeamId}"));
    }

    // US-3 · "A save of any field is refused": a title, an assignee, a team and a field of the type; the item and its
    // «Изменён» are as they were.
    [Theory]
    [InlineData("title")]
    [InlineData("assignee")]
    [InlineData("team")]
    [InlineData("field")]
    public async Task Patch_ADeletedItem_AnyField_Is409Deleted_ItemUnchanged(string field)
    {
        var (world, _, task) = await WorldAsync();
        var member = await WorkItemsTestData.AddMemberAsync(factory, world.Items, "Анна", "Смирнова");
        var before = await SnapshotAsync(world, task);

        object body = field switch
        {
            "title" => new { title = "Другая задача" },
            "assignee" => new { assignee = new { userId = member } },
            "team" => new { team = new { teamId = world.ProjectTeamId.ToString() } },
            _ => new { priority = new { value = 1 } },
        };

        var answer = await WorkItemsTestData.SaveAsync(factory, world.Items, task.Identifier, body);

        LinksTestData.AssertRefused(answer, HttpStatusCode.Conflict, DeletedCode);
        Assert.Equal(before, await SnapshotAsync(world, task));
    }

    // US-3 · "The refusal comes before the value checks": a 300-character title on a deleted item is 409, not 400.
    [Fact]
    public async Task Patch_ADeletedItem_With300CharTitle_Is409NotAnd400()
    {
        var (world, _, task) = await WorldAsync();

        var answer = await WorkItemsTestData.SaveAsync(factory, world.Items, task.Identifier, new { title = new string('я', 300) });

        LinksTestData.AssertRefused(answer, HttpStatusCode.Conflict, DeletedCode);
    }

    // US-3 · "an unknown identifier is 404 before the 409": the path's not-founds come first.
    [Fact]
    public async Task Patch_UnknownIdentifier_Is404BeforeTheRefusal()
    {
        var (world, _, _) = await WorldAsync();

        var answer = await WorkItemsTestData.SaveAsync(factory, world.Items, "MSK-TASK-999", new { title = "Другая задача" });

        LinksTestData.AssertRefused(answer, HttpStatusCode.NotFound, "workitems.workitem.not_found");
    }

    // US-3 · the refusal comes before the body's references are looked up: an unknown area, team, parent or state is 409, not 404.
    [Theory]
    [InlineData("area")]
    [InlineData("team")]
    [InlineData("parent")]
    [InlineData("state")]
    public async Task Patch_ADeletedItem_NamingAnUnknownAreaTeamParentOrState_Is409Deleted(string reference)
    {
        var (world, _, task) = await WorldAsync();
        var unknown = Guid.NewGuid().ToString();

        object body = reference switch
        {
            "area" => new { areaId = unknown },
            "team" => new { team = new { teamId = unknown } },
            "parent" => new { parent = new { parentId = unknown, seenParentId = (string?)null } },
            _ => new { stateId = unknown },
        };

        var answer = await WorkItemsTestData.SaveAsync(factory, world.Items, task.Identifier, body);

        LinksTestData.AssertRefused(answer, HttpStatusCode.Conflict, DeletedCode);
    }

    // US-3 · "A live state sent with a parent does not slip past" (tech-012 m1): the stored state is judged before anything
    // is applied, so the item is not linked and «Изменён» does not move.
    [Fact]
    public async Task Patch_ADeletedItem_LiveStateAndAParent_Is409Deleted_NoLinkNoChangedAtMoves()
    {
        var (world, story, task) = await WorldAsync();
        var before = await SnapshotAsync(world, task);
        var storyBefore = await LinksTestData.StoredAsync(factory, story);

        var answer = await WorkItemsTestData.SaveAsync(
            factory,
            world.Items,
            task.Identifier,
            new
            {
                stateId = world.Items.Types["TASK"].StateId("Активна").ToString(),
                parent = new { parentId = story.Id.ToString(), seenParentId = (string?)null },
            });

        LinksTestData.AssertRefused(answer, HttpStatusCode.Conflict, DeletedCode);
        Assert.Equal(before, await SnapshotAsync(world, task));
        Assert.Equal(storyBefore, await LinksTestData.StoredAsync(factory, story));
        Assert.Null((await LinksTestData.StoredAsync(factory, task)).ParentId);
    }

    // US-3 · "No state write brings the item back without Restore": a state alone, and the empty body.
    [Fact]
    public async Task Patch_ADeletedItem_AStateAlone_Is409Deleted_StaysDeleted()
    {
        var (world, _, task) = await WorldAsync();
        var removed = world.Items.Types["TASK"].StateId(StateCategory.Removed);

        var answer = await BoardsTestData.SetStateAsync(factory, world, task, "TASK", "Активна");

        LinksTestData.AssertRefused(answer, HttpStatusCode.Conflict, DeletedCode);
        Assert.Equal(removed, (await LinksTestData.StoredAsync(factory, task)).StateId);
    }

    [Fact]
    public async Task Patch_ADeletedItem_EmptyBody_Is409Deleted()
    {
        var (world, _, task) = await WorldAsync();
        var before = await SnapshotAsync(world, task);

        var answer = await WorkItemsTestData.SaveAsync(factory, world.Items, task.Identifier, new { });

        LinksTestData.AssertRefused(answer, HttpStatusCode.Conflict, DeletedCode);
        Assert.Equal(before, await SnapshotAsync(world, task));
    }

    // US-3 · "A stale board card cannot be moved": a card move is this save with a stateId.
    [Fact]
    public async Task Patch_State_OnADeletedBoardCard_Is409Deleted()
    {
        var world = await BoardsTestData.CreateWorldAsync(factory);
        var story = await BoardsTestData.CreateAsync(factory, world, "US", "Карточка", world.PlatformAreaId);
        await LinksTestData.DeleteAsync(factory, world.Items, story);

        var answer = await BoardsTestData.SetStateAsync(factory, world, story, "US", "Активна");

        LinksTestData.AssertRefused(answer, HttpStatusCode.Conflict, DeletedCode);
    }

    // US-3 · "a stale checklist row cannot be ticked": a tick is the same save of the child.
    [Fact]
    public async Task Patch_State_OnADeletedChecklistChild_Is409Deleted()
    {
        var world = await BoardsTestData.CreateWorldAsync(factory);
        var story = await BoardsTestData.CreateAsync(factory, world, "US", "Карточка", world.PlatformAreaId);
        var child = await BoardsTestData.CreateAsync(factory, world, "TASK", "Пункт");
        await LinksTestData.LinkAsync(factory, world.Items, child, story);
        await LinksTestData.DeleteAsync(factory, world.Items, child);

        var answer = await BoardsTestData.SetStateAsync(factory, world, child, "TASK", "Закрыта");

        LinksTestData.AssertRefused(answer, HttpStatusCode.Conflict, DeletedCode);
    }

    // US-3 · "a form save of the deleted item answers …deleted" even when it names a parent: not …item_deleted.
    [Fact]
    public async Task Patch_ADeletedItem_NamingAParent_Is409Deleted_NotItemDeleted()
    {
        var (world, story, task) = await WorldAsync();

        var answer = await WorkItemsTestData.SaveAsync(
            factory, world.Items, task.Identifier, new { parent = new { parentId = story.Id.ToString(), seenParentId = (string?)null } });

        LinksTestData.AssertRefused(answer, HttpStatusCode.Conflict, DeletedCode);
    }

    // US-3 · "Link writes keep their own refusal": a live item that names a deleted parent answers …item_deleted.
    [Fact]
    public async Task Patch_LiveItem_NamingADeletedParent_Is409ItemDeleted()
    {
        var world = await BoardsTestData.CreateWorldAsync(factory);
        var story = await BoardsTestData.CreateAsync(factory, world, "US", "Родитель", world.PlatformAreaId);
        var task = await BoardsTestData.CreateAsync(factory, world, "TASK", "Дочерняя");
        await LinksTestData.DeleteAsync(factory, world.Items, story);

        var answer = await WorkItemsTestData.SaveAsync(
            factory, world.Items, task.Identifier, new { parent = new { parentId = story.Id.ToString(), seenParentId = (string?)null } });

        LinksTestData.AssertRefused(answer, HttpStatusCode.Conflict, "workitems.link.item_deleted");
    }

    // US-3 · "A reader's write answers as on a live item": the route's code first, so the answer tells nothing of the deletion.
    [Fact]
    public async Task Patch_ADeletedItem_AsAViewer_Is403_TellsNothingOfTheDeletion()
    {
        var (world, _, task) = await WorldAsync();
        var viewer = await MembershipTestData.NewMemberAsync(factory, world.OrganizationId, OrganizationRole.Viewer);

        var answer = await OrganizationsApi.SendAsync(
            factory, HttpMethod.Patch, WorkItemsTestData.Item(world.Items, task.Identifier), new { title = "Другая" }, viewer.Token);

        LinksTestData.AssertRefused(answer, HttpStatusCode.Forbidden, "organizations.access.forbidden");
    }

    // US-3 · "A stale reorder naming a deleted item is refused": the moved item or the anchor, on a team's backlog and on the
    // project's; the order does not change (a deleted item is a member of no backlog).
    [Fact]
    public async Task Reorder_TeamAndProject_NamingADeletedItemAsMovedOrAnchor_Is409ReorderStale_OrderUnchanged()
    {
        var world = await BoardsTestData.CreateWorldAsync(factory);
        var first = await BoardsTestData.CreateAsync(factory, world, "US", "Первая", world.PlatformAreaId);
        var second = await BoardsTestData.CreateAsync(factory, world, "US", "Вторая", world.PlatformAreaId);
        var gone = await BoardsTestData.CreateAsync(factory, world, "US", "Удаляемая", world.PlatformAreaId);
        await BoardsTestData.ReorderToAsync(factory, world, world.PlatformId, first, second, gone);
        await LinksTestData.DeleteAsync(factory, world.Items, gone);

        var teamRows = await BoardsTestData.RowsAsync(factory, world, world.PlatformId);
        var projectBacklog = $"/api/organizations/{world.OrganizationId}/projects/{world.ProjectId}/backlogs/stories/reorder";

        foreach (var body in new object[] { BoardsTestData.Top(gone), BoardsTestData.After(first, gone) })
        {
            var team = await BoardsTestData.ReorderAsync(factory, world, world.PlatformId, body);
            BoardsTestData.AssertRefused(team, HttpStatusCode.Conflict, "boards.backlog.reorder_stale");

            var project = await OrganizationsApi.SendAsync(factory, HttpMethod.Post, projectBacklog, body, OrganizationsTokens.SignedIn());
            BoardsTestData.AssertRefused(project, HttpStatusCode.Conflict, "boards.backlog.reorder_stale");
        }

        Assert.Equal(teamRows, await BoardsTestData.RowsAsync(factory, world, world.PlatformId));
        Assert.Equal([first.Identifier, second.Identifier], teamRows);
    }
}
