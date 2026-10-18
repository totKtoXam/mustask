using System.Net;
using System.Text.Json;
using Mustask.Api.IntegrationTests.Features.Boards;
using Mustask.Api.IntegrationTests.Features.Organizations;
using Mustask.Domain.Identity;

namespace Mustask.Api.IntegrationTests.Features.WorkItems;

/// <summary>
/// mod-005 US-2 · "A deleted item leaves the views" (BR-DR-6, ADR-0030 rule 2): the project's list and its count, the team's and
/// the project's backlogs and boards, the parent picker, the card's children and the checklists do not show it; it is found by its
/// identifier and from a live child's «Родитель»; a team still owns it and an area still holds it. The cross-cutting proof of every
/// surface in one world is <c>DeletedItemSurfaceMatrixTests</c>; these are the stories' scenarios, one by one.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class DeletedItemViewsEndpointTests(MustaskApiFactory factory)
{
    private async Task DeleteAsync(BoardsWorld world, Item item) => await LinksTestData.DeleteAsync(factory, world.Items, item);

    private static List<string> Identifiers(JsonElement page) =>
        page.GetProperty("data").EnumerateArray().Select(row => row.GetProperty("identifier").GetString()!).ToList();

    // US-2 · "The list neither shows nor counts a deleted item, whatever the page": 8 items of which 1 is deleted are 7 rows and a
    // count of 7; 120 items of which 2 are deleted are 118 on three pages, the count 118 on each.
    [Fact]
    public async Task List_ADeletedItem_IsNeitherListedNorCounted_OnEveryPage()
    {
        var world = await BoardsTestData.CreateWorldAsync(factory);
        var items = new List<Item>();

        for (var number = 1; number <= 8; number++)
        {
            items.Add(await BoardsTestData.CreateAsync(factory, world, "TASK", $"Задача {number}"));
        }

        await DeleteAsync(world, items[3]);

        var small = await WorkItemsTestData.ListAsync(factory, world.Items);

        Assert.Equal(7, small.Body.GetProperty("count").GetInt32());
        Assert.Equal(7, Identifiers(small.Body).Count);
        Assert.DoesNotContain(items[3].Identifier, Identifiers(small.Body));

        var big = await BoardsTestData.CreateWorldAsync(factory);
        var task = big.Items.Types["TASK"];
        await WorkItemsTestData.ExecuteAsync(factory, $"""
            INSERT INTO work_items (id, project_id, number, type_id, state_id, area_id, title, description, creation_request_id, created_at, created_by, changed_at)
            SELECT gen_random_uuid(), {big.ProjectId}, n, {task.Id}, {task.StateId("Новая")}, {big.RootAreaId},
                   'Объёмная ' || n, '', gen_random_uuid(), now(), 'views-test', now() - n * interval '1 second'
            FROM generate_series(1, 120) AS n
            """);
        await WorkItemsTestData.ExecuteAsync(factory, $"""
            INSERT INTO work_item_counters (project_id, last_number) VALUES ({big.ProjectId}, 120)
            ON CONFLICT (project_id) DO UPDATE SET last_number = excluded.last_number
            """);
        foreach (var number in new[] { 3, 77 })
        {
            await DeleteAsync(big, new Item(Guid.Empty, $"MSK-TASK-{number}"));
        }

        var seen = new List<string>();
        var pageSizes = new List<int>();

        foreach (var page in new[] { 1, 2, 3 })
        {
            var answer = await WorkItemsTestData.ListAsync(factory, big.Items, $"?page={page}");

            Assert.Equal(118, answer.Body.GetProperty("count").GetInt32());
            pageSizes.Add(Identifiers(answer.Body).Count);
            seen.AddRange(Identifiers(answer.Body));
        }

        Assert.Equal([50, 50, 18], pageSizes);
        Assert.Equal(118, seen.Distinct().Count());
        Assert.DoesNotContain("MSK-TASK-3", seen);
        Assert.DoesNotContain("MSK-TASK-77", seen);
    }

    // US-2 · "Team backlogs and boards at every level lose the item; others keep their order".
    [Theory]
    [InlineData("epics", "EPIC")]
    [InlineData("features", "FEAT")]
    [InlineData("stories", "US")]
    public async Task TeamBacklogAndBoard_AtEveryLevel_LoseTheItem_OthersKeepTheirOrder(string level, string typeKey)
    {
        var world = await BoardsTestData.CreateWorldAsync(factory);
        var first = await BoardsTestData.CreateAsync(factory, world, typeKey, "Первая", world.PlatformAreaId);
        var middle = await BoardsTestData.CreateAsync(factory, world, typeKey, "Средняя", world.PlatformAreaId);
        var last = await BoardsTestData.CreateAsync(factory, world, typeKey, "Последняя", world.PlatformAreaId);
        foreach (var item in new[] { first, middle, last })
        {
            await BoardsTestData.MoveAsync(factory, world, world.PlatformId, BoardsTestData.Bottom(item), level);
        }

        Assert.Equal([first.Identifier, middle.Identifier, last.Identifier], await BoardsTestData.RowsAsync(factory, world, world.PlatformId, level));

        await DeleteAsync(world, middle);

        Assert.Equal([first.Identifier, last.Identifier], await BoardsTestData.RowsAsync(factory, world, world.PlatformId, level));
        var board = await BoardsTestData.GetBoardAsync(factory, world, world.PlatformId, level);
        Assert.Equal(HttpStatusCode.OK, board.Status);
        Assert.DoesNotContain(middle.Identifier, board.Body.GetProperty("columns").EnumerateArray().SelectMany(BoardsTestData.Cards));
        Assert.Contains(first.Identifier, board.Body.GetProperty("columns").EnumerateArray().SelectMany(BoardsTestData.Cards));
    }

    // The project's own backlog and board at the same levels (feat-039): the item is gone, the others keep their places.
    [Theory]
    [InlineData("epics", "EPIC")]
    [InlineData("features", "FEAT")]
    [InlineData("stories", "US")]
    public async Task ProjectBacklogAndBoard_AtEveryLevel_LoseTheItem_OthersKeepTheirOrder(string level, string typeKey)
    {
        var world = await BoardsTestData.CreateWorldAsync(factory);
        var first = await BoardsTestData.CreateAsync(factory, world, typeKey, "Первая", world.PlatformAreaId);
        var middle = await BoardsTestData.CreateAsync(factory, world, typeKey, "Средняя", world.PlatformAreaId);
        var last = await BoardsTestData.CreateAsync(factory, world, typeKey, "Последняя", world.PlatformAreaId);
        foreach (var item in new[] { first, middle, last })
        {
            await ProjectBoardsTestData.MoveAsync(factory, world, BoardsTestData.Bottom(item), level);
        }

        Assert.Equal([first.Identifier, middle.Identifier, last.Identifier], await ProjectBoardsTestData.RowsAsync(factory, world, level));

        await DeleteAsync(world, middle);

        Assert.Equal([first.Identifier, last.Identifier], await ProjectBoardsTestData.RowsAsync(factory, world, level));
        Assert.DoesNotContain(middle.Identifier, ProjectBoardsTestData.AllCards((await ProjectBoardsTestData.GetBoardAsync(factory, world, level)).Body));
    }

    // US-2 · "A deleted item keeps what it carried; its area is not deletable": team, area, assignee and parent stay; the area
    // still holds it (mod-003 BR-M3-24, until mod-018).
    [Fact]
    public async Task Delete_KeepsTeamAreaAssigneeParent_AndTheAreaIsStillNotDeletable_AreaNotEmpty()
    {
        var world = await BoardsTestData.CreateWorldAsync(factory);
        var anna = await WorkItemsTestData.AddMemberAsync(factory, world.Items, "Анна", "Смирнова");
        var feature = await BoardsTestData.CreateAsync(factory, world, "FEAT", "Фича", world.PlatformAreaId);
        var story = await BoardsTestData.CreateAsync(factory, world, "US", "История", world.ReportsAreaId);
        await LinksTestData.LinkAsync(factory, world.Items, story, feature);
        Assert.Equal(HttpStatusCode.OK, (await WorkItemsTestData.SaveAsync(factory, world.Items, story.Identifier, new { assignee = new { userId = anna } })).Status);
        var before = await WorkItemsTestData.ReadAsync(factory, world.Items, story.Identifier);

        await DeleteAsync(world, story);

        var deleted = await WorkItemsTestData.ReadAsync(factory, world.Items, story.Identifier);
        foreach (var property in new[] { "area", "assignee", "team", "parent" })
        {
            Assert.Equal(before.GetProperty(property).GetRawText(), deleted.GetProperty(property).GetRawText());
        }

        var removeArea = await OrganizationsApi.SendAsync(
            factory, HttpMethod.Delete, $"{OrganizationsApi.Areas(world.OrganizationId, world.ProjectId)}/{world.ReportsAreaId}", token: OrganizationsTokens.SignedIn());
        LinksTestData.AssertRefused(removeArea, HttpStatusCode.Conflict, "organizations.area.not_empty");
    }

    // US-2 · "A deleted item is still counted among a team's items; the team deletion releases it" (BR-DR-9, feat-028 SC-09, doc-012
    // BR-MX-12 "in any state"): the unlink's count in the project and the deletion's counts over the projects both include it.
    [Fact]
    public async Task OwnedItemCount_CountsADeletedItem()
    {
        var world = await BoardsTestData.CreateWorldAsync(factory);
        var live = await BoardsTestData.CreateAsync(factory, world, "US", "Живая", world.PlatformAreaId);
        var gone = await BoardsTestData.CreateAsync(factory, world, "US", "Удалённая", world.PlatformAreaId);
        await DeleteAsync(world, gone);

        var unlinkCount = await OrganizationsApi.SendAsync(
            factory,
            HttpMethod.Get,
            $"{TeamTestData.ProjectTeam(world.OrganizationId, world.ProjectId, world.PlatformId)}/unlink-count",
            token: OrganizationsTokens.SignedIn());
        var deletionCounts = await OrganizationsApi.SendAsync(
            factory,
            HttpMethod.Get,
            $"{TeamTestData.Team(world.OrganizationId, world.PlatformId)}/deletion-counts",
            token: OrganizationsTokens.SignedIn());

        Assert.Equal(2, unlinkCount.Body.GetProperty("count").GetInt32());
        Assert.Equal([2], deletionCounts.Body.GetProperty("projects").EnumerateArray().Select(project => project.GetProperty("count").GetInt32()));
        Assert.NotNull(live);
    }

    [Fact]
    public async Task DeleteTeam_ReleasesADeletedItemToo_StillDeleted_ChangedAtMoves()
    {
        var world = await BoardsTestData.CreateWorldAsync(factory);
        var gone = await BoardsTestData.CreateAsync(factory, world, "US", "Удалённая", world.PlatformAreaId);
        await DeleteAsync(world, gone);
        var before = await WorkItemsTestData.ReadAsync(factory, world.Items, gone.Identifier);

        var removal = await TeamTestData.DeleteTeamAsync(factory, world.OrganizationId, world.PlatformId);
        Assert.Equal(HttpStatusCode.NoContent, removal.Status);

        var after = await WorkItemsTestData.ReadAsync(factory, world.Items, gone.Identifier);
        Assert.Equal(JsonValueKind.Null, after.GetProperty("team").ValueKind);
        Assert.Equal("Removed", after.GetProperty("state").GetProperty("category").GetProperty("code").GetString());
        Assert.True(after.GetProperty("changedAt").GetDateTimeOffset() > before.GetProperty("changedAt").GetDateTimeOffset());
        Assert.Equal(before.GetProperty("deletion").GetRawText(), after.GetProperty("deletion").GetRawText());
    }

    // BR-DR-9 (owed to the second of mod-005 and feat-028, spec BR-DR-9 and plan-backend § 8.2): an unlink is a clean-up of the same
    // kind as a team's deletion — it reassigns the items the team owns in the project in any state, so an item in «Удалена» goes
    // to the destination too (a team, or «Без команды») and STAYS deleted: its state and its three marks are untouched, only the
    // owner and «Изменён» move, and its place on the unlinked team's backlog goes with the rest. BR-DR-10: a Restore afterwards brings
    // it back owned by the destination — the owner as stored — and on that team's backlog, unplaced below every placed item.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Unlink_ReassignsADeletedItemToo_StillDeleted(bool toATeam)
    {
        var world = await BoardsTestData.CreateWorldAsync(factory);
        var gone = await BoardsTestData.CreateAsync(factory, world, "US", "Удалённая", world.PlatformAreaId);
        var stays = await BoardsTestData.CreateAsync(factory, world, "US", "Живая", world.PlatformAreaId);
        await BoardsTestData.ReorderToAsync(factory, world, world.PlatformId, gone, stays);
        await DeleteAsync(world, gone);
        var before = await WorkItemsTestData.ReadAsync(factory, world.Items, gone.Identifier);
        Guid? destination = toATeam ? world.ProjectTeamId : null;

        Assert.Equal(1, await OwningTeamTestData.TeamPlacesOfAsync(factory, gone.Id));

        var unlink = await TeamTestData.UnlinkAsync(factory, world.OrganizationId, world.ProjectId, world.PlatformId, TeamTestData.To(destination));
        Assert.Equal(HttpStatusCode.NoContent, unlink.Status);

        // Still deleted: the state and the three marks are as they were; the owner and «Изменён» moved.
        var after = await WorkItemsTestData.ReadAsync(factory, world.Items, gone.Identifier);
        Assert.Equal(destination, OwningTeamTestData.TeamOf(after)?.Id);
        Assert.Equal("Removed", after.GetProperty("state").GetProperty("category").GetProperty("code").GetString());
        Assert.Equal(before.GetProperty("state").GetRawText(), after.GetProperty("state").GetRawText());
        Assert.Equal(before.GetProperty("deletion").GetRawText(), after.GetProperty("deletion").GetRawText());
        Assert.True(after.GetProperty("changedAt").GetDateTimeOffset() > before.GetProperty("changedAt").GetDateTimeOffset());
        Assert.Equal(0, await OwningTeamTestData.TeamPlacesOfAsync(factory, gone.Id));
        Assert.Equal(destination, (await OwningTeamTestData.StoredAsync(factory, world.ProjectId, gone.Identifier)).TeamId);

        // It is on no list, and the live item went to the same destination (the two are one operation).
        Assert.Equal(destination, (await OwningTeamTestData.StoredAsync(factory, world.ProjectId, stays.Identifier)).TeamId);
        Assert.DoesNotContain(gone.Identifier, Identifiers((await WorkItemsTestData.ListAsync(factory, world.Items)).Body));

        if (toATeam)
        {
            // The live item is placed on the destination's backlog now, so the restore has a placed item to land below.
            await BoardsTestData.MoveAsync(factory, world, world.ProjectTeamId, BoardsTestData.Bottom(stays));
        }

        // BR-DR-10: the restore keeps the owner as the unlink stored it, and places the item below the placed ones of its backlog.
        var restored = await OrganizationsApi.SendAsync(
            factory, HttpMethod.Post, $"{WorkItemsTestData.Item(world.Items, gone.Identifier)}/restore", token: OrganizationsTokens.SignedIn());

        Assert.Equal(HttpStatusCode.OK, restored.Status);
        Assert.Equal(destination, OwningTeamTestData.TeamOf(restored.Body)?.Id);
        Assert.Equal("Proposed", restored.Body.GetProperty("state").GetProperty("category").GetProperty("code").GetString());
        Assert.Equal(JsonValueKind.Null, restored.Body.GetProperty("deletion").ValueKind);

        if (toATeam)
        {
            Assert.Equal([stays.Identifier, gone.Identifier], await BoardsTestData.RowsAsync(factory, world, world.ProjectTeamId));
        }
        else
        {
            Assert.DoesNotContain(gone.Identifier, await BoardsTestData.RowsAsync(factory, world, world.ProjectTeamId));
            Assert.Contains(gone.Identifier, await ProjectBoardsTestData.RowsAsync(factory, world));
        }
    }

    // US-2 · "Found by its address (a reader too; a foreign identifier is an unknown one)".
    [Fact]
    public async Task Get_ADeletedItem_ReadsInFull_WithItsDeletion_ForAMemberAndAViewer()
    {
        var world = await BoardsTestData.CreateWorldAsync(factory);
        var story = await BoardsTestData.CreateAsync(factory, world, "US", "Читаемая", world.PlatformAreaId);
        await DeleteAsync(world, story);
        var member = await MembershipTestData.NewMemberAsync(factory, world.OrganizationId, OrganizationRole.Member);
        var viewer = await MembershipTestData.NewMemberAsync(factory, world.OrganizationId, OrganizationRole.Viewer);

        foreach (var token in new[] { member.Token, viewer.Token })
        {
            var answer = await OrganizationsApi.SendAsync(factory, HttpMethod.Get, WorkItemsTestData.Item(world.Items, story.Identifier), token: token);

            Assert.Equal(HttpStatusCode.OK, answer.Status);
            Assert.Equal("Читаемая", answer.Body.GetProperty("title").GetString());
            Assert.Equal("Removed", answer.Body.GetProperty("state").GetProperty("category").GetProperty("code").GetString());
            Assert.Equal("Новая", answer.Body.GetProperty("deletion").GetProperty("fromState").GetProperty("name").GetString());
            Assert.Equal(["at", "by", "fromState"], answer.Body.GetProperty("deletion").EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));
        }
    }

    [Fact]
    public async Task Get_AForeignIdentifier_IsNotFound_LikeAnUnknownOne()
    {
        var world = await BoardsTestData.CreateWorldAsync(factory);
        var other = await BoardsTestData.CreateWorldAsync(factory);
        var foreign = await BoardsTestData.CreateAsync(factory, other, "US", "Чужая", other.PlatformAreaId);
        await DeleteAsync(other, foreign);

        var crossed = await WorkItemsTestData.GetAsync(factory, world.Items with { ProjectId = other.ProjectId }, foreign.Identifier);
        var unknown = await WorkItemsTestData.GetAsync(factory, world.Items, "MSK-US-999");
        var sameKey = await WorkItemsTestData.GetAsync(factory, world.Items, foreign.Identifier);

        Assert.Equal(HttpStatusCode.NotFound, crossed.Status);
        Assert.Equal(HttpStatusCode.NotFound, unknown.Status);
        Assert.Equal(HttpStatusCode.NotFound, sameKey.Status);
        Assert.Equal("workitems.workitem.not_found", sameKey.ErrorCode);
    }

    [Fact]
    public async Task Get_ADeletedItemOfAnotherOrganization_IsOrganizationNotFound()
    {
        var world = await BoardsTestData.CreateWorldAsync(factory);
        var story = await BoardsTestData.CreateAsync(factory, world, "US", "Чужой организации", world.PlatformAreaId);
        await DeleteAsync(world, story);
        var stranger = await MembershipTestData.NewMemberAsync(factory, (await OrganizationsTestData.CreateOrganizationAsync(factory)).Id, OrganizationRole.Admin);

        var answer = await OrganizationsApi.SendAsync(factory, HttpMethod.Get, WorkItemsTestData.Item(world.Items, story.Identifier), token: stranger.Token);

        LinksTestData.AssertRefused(answer, HttpStatusCode.NotFound, "organizations.organization.not_found");
    }

    // US-2 · "A live child leads to its deleted parent": the stored parent reads in the state «Удалена».
    [Fact]
    public async Task Get_ALiveChild_ShowsItsDeletedParent_WithStateRemoved()
    {
        var world = await BoardsTestData.CreateWorldAsync(factory);
        var feature = await BoardsTestData.CreateAsync(factory, world, "FEAT", "Фича", world.PlatformAreaId);
        var story = await BoardsTestData.CreateAsync(factory, world, "US", "История", world.PlatformAreaId);
        await LinksTestData.LinkAsync(factory, world.Items, story, feature);
        await DeleteAsync(world, feature);

        var child = await WorkItemsTestData.ReadAsync(factory, world.Items, story.Identifier);

        Assert.Equal(feature.Identifier, LinksTestData.IdentifierOf(child.GetProperty("parent")));
        Assert.Equal(("Удалена", "Removed"), (
            child.GetProperty("parent").GetProperty("state").GetProperty("name").GetString(),
            child.GetProperty("parent").GetProperty("state").GetProperty("category").GetProperty("code").GetString()));
    }

    // US-2 · "Pickers and the form's lists skip a deleted item": the parent picker, whatever is typed, and «Дочерние элементы».
    [Fact]
    public async Task ParentCandidates_SkipADeletedItem_WhateverIsTyped()
    {
        var world = await BoardsTestData.CreateWorldAsync(factory);
        var live = await BoardsTestData.CreateAsync(factory, world, "FEAT", "Подходящая фича", world.PlatformAreaId);
        var gone = await BoardsTestData.CreateAsync(factory, world, "FEAT", "Подходящая удалённая", world.PlatformAreaId);
        var story = await BoardsTestData.CreateAsync(factory, world, "US", "История", world.PlatformAreaId);
        await DeleteAsync(world, gone);

        foreach (var text in new[] { null, "Подходящая", "удалённая", gone.Identifier })
        {
            var offered = await LinksTestData.OfferedAsync(factory, world.Items, story.Identifier, text);

            Assert.DoesNotContain(gone.Identifier, offered);
        }

        Assert.Contains(live.Identifier, await LinksTestData.OfferedAsync(factory, world.Items, story.Identifier, "Подходящая"));
    }

    [Fact]
    public async Task Get_Children_ListsLiveOnly_AndTheCountIgnoresDeletedOnes()
    {
        var world = await BoardsTestData.CreateWorldAsync(factory);
        var feature = await BoardsTestData.CreateAsync(factory, world, "FEAT", "Фича", world.PlatformAreaId);
        var kept = await BoardsTestData.CreateAsync(factory, world, "US", "Остаётся", world.PlatformAreaId);
        var gone = await BoardsTestData.CreateAsync(factory, world, "US", "Уходит", world.PlatformAreaId);
        await LinksTestData.LinkAsync(factory, world.Items, kept, feature);
        await LinksTestData.LinkAsync(factory, world.Items, gone, feature);
        await DeleteAsync(world, gone);

        var parent = await WorkItemsTestData.ReadAsync(factory, world.Items, feature.Identifier);

        Assert.Equal([kept.Identifier], LinksTestData.Identifiers(parent.GetProperty("children")));
    }

    // US-2 · "A deactivated project hides its deleted items; restored, they read as before".
    [Fact]
    public async Task Get_ADeletedItem_UnderADeactivatedProject_Is404_RestoredProjectReadsItAsBefore()
    {
        var world = await BoardsTestData.CreateWorldAsync(factory);
        var story = await BoardsTestData.CreateAsync(factory, world, "US", "Под проектом", world.PlatformAreaId);
        await DeleteAsync(world, story);
        var before = await WorkItemsTestData.ReadAsync(factory, world.Items, story.Identifier);
        var project = $"/api/organizations/{world.OrganizationId}/projects/{world.ProjectId}";

        Assert.Equal(HttpStatusCode.NoContent, (await OrganizationsApi.SendAsync(factory, HttpMethod.Post, project + "/deactivate", token: OrganizationsTokens.SignedIn())).Status);
        var hidden = await WorkItemsTestData.GetAsync(factory, world.Items, story.Identifier);
        Assert.Equal(HttpStatusCode.OK, (await OrganizationsApi.SendAsync(factory, HttpMethod.Post, project + "/restore", token: OrganizationsTokens.SignedIn())).Status);

        LinksTestData.AssertRefused(hidden, HttpStatusCode.NotFound, "organizations.project.not_found");
        Assert.Equal(before.GetRawText(), (await WorkItemsTestData.ReadAsync(factory, world.Items, story.Identifier)).GetRawText());
    }
}
