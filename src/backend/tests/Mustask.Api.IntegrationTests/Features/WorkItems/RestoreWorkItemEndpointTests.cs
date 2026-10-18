using System.Net;
using System.Text.Json;
using Mustask.Api.IntegrationTests.Features.Boards;
using Mustask.Api.IntegrationTests.Features.Organizations;
using Mustask.Domain.Identity;
using Mustask.Domain.Process;

namespace Mustask.Api.IntegrationTests.Features.WorkItems;

/// <summary>
/// <c>POST /api/organizations/{organizationId}/projects/{projectId}/workitems/{identifier}/restore</c> (mod-005 api-contract
/// § 2.2, US-4; BR-DR-10, -11, -17): a deleted item returns to the state it held, or to one the user chose among the states of
/// its own type, with the marks cleared; it is back on every surface it qualifies for, as stored, and nothing else changes.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class RestoreWorkItemEndpointTests(MustaskApiFactory factory)
{
    private const string NotDeletedCode = "workitems.workitem.not_deleted";
    private const string StateNotAllowedCode = "workitems.workitem.state_not_allowed";

    private static string RestorePath(WorkItemsWorld world, string identifier) => $"{WorkItemsTestData.Item(world, identifier)}/restore";

    private Task<Answer> RestoreAsync(WorkItemsWorld world, string identifier, object? body = null, string? token = null) =>
        OrganizationsApi.SendAsync(factory, HttpMethod.Post, RestorePath(world, identifier), body, token ?? OrganizationsTokens.SignedIn());

    private async Task<(BoardsWorld World, Item Story)> DeletedStoryAsync(string held = "Активна")
    {
        var world = await BoardsTestData.CreateWorldAsync(factory);
        var story = await BoardsTestData.CreateAsync(factory, world, "US", "История", world.PlatformAreaId);
        Assert.Equal(HttpStatusCode.OK, (await BoardsTestData.SetStateAsync(factory, world, story, "US", held)).Status);
        await LinksTestData.DeleteAsync(factory, world.Items, story);

        return (world, story);
    }

    // US-4 · "The proposal is the held state" and "Accepting the proposal returns the item as it was; back in the list, on the
    // backlog and board, in its column": no body, or {}, restores the held state, clears the marks and moves «Изменён».
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Restore_NoBody_ReturnsTheItemToTheHeldState_BackOnEverySurface_MarksCleared_ChangedAtIsTheRestore(bool emptyObject)
    {
        var (world, story) = await DeletedStoryAsync();
        var deleted = await WorkItemsTestData.ReadAsync(factory, world.Items, story.Identifier);
        Assert.DoesNotContain(story.Identifier, (await WorkItemsTestData.ListAsync(factory, world.Items)).Body.GetProperty("data").EnumerateArray().Select(row => row.GetProperty("identifier").GetString()));

        var answer = await RestoreAsync(world.Items, story.Identifier, emptyObject ? (object?)new { } : null);

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Equal(("Активна", "InProgress"), (answer.Body.GetProperty("state").GetProperty("name").GetString(), answer.Body.GetProperty("state").GetProperty("category").GetProperty("code").GetString()));
        Assert.Equal(JsonValueKind.Null, answer.Body.GetProperty("deletion").ValueKind);
        Assert.True(answer.Body.GetProperty("changedAt").GetDateTimeOffset() > deleted.GetProperty("changedAt").GetDateTimeOffset());

        var list = await WorkItemsTestData.ListAsync(factory, world.Items);
        Assert.Contains(story.Identifier, list.Body.GetProperty("data").EnumerateArray().Select(row => row.GetProperty("identifier").GetString()));
        Assert.Contains(story.Identifier, await BoardsTestData.RowsAsync(factory, world, world.PlatformId));
        Assert.Contains(story.Identifier, await ProjectBoardsTestData.RowsAsync(factory, world));
        var board = await BoardsTestData.GetBoardAsync(factory, world, world.PlatformId);
        Assert.Contains(story.Identifier, BoardsTestData.Cards(BoardsTestData.Column(board.Body, "Активна")));
        Assert.Equal(JsonValueKind.Null, (await WorkItemsTestData.ReadAsync(factory, world.Items, story.Identifier)).GetProperty("deletion").ValueKind);
    }

    // US-4 · "Any other state of the type may be chosen; the held state is forgotten": each state but «Удалена» is accepted.
    [Theory]
    [InlineData("Новая")]
    [InlineData("Активна")]
    [InlineData("Разрешена")]
    [InlineData("Закрыта")]
    public async Task Restore_ToAChosenState_AnyOfTheTypes_ForgetsTheHeldOne(string stateName)
    {
        var (world, story) = await DeletedStoryAsync("Разрешена");
        var chosen = world.Items.Types["US"].StateId(stateName);

        var answer = await RestoreAsync(world.Items, story.Identifier, new { stateId = chosen.ToString() });

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Equal(chosen, answer.Body.GetProperty("state").GetProperty("id").GetGuid());
        Assert.Equal(JsonValueKind.Null, answer.Body.GetProperty("deletion").ValueKind);

        // A later deletion remembers the chosen state, not the first deletion's.
        await LinksTestData.DeleteAsync(factory, world.Items, story);
        Assert.Equal(chosen, (await WorkItemsTestData.ReadAsync(factory, world.Items, story.Identifier)).GetProperty("deletion").GetProperty("fromState").GetProperty("id").GetGuid());
    }

    // US-4 · "The choice is limited to the item's type": «Удалена», a state of another type or organization, an id that names
    // nothing and a value that is no id are refused, and the item stays deleted with its record.
    [Fact]
    public async Task Restore_StateOfAnotherTypeRemovedOrNotAnId_Is409StateNotAllowed_StaysDeleted()
    {
        var (world, story) = await DeletedStoryAsync();
        var foreign = await BoardsTestData.CreateWorldAsync(factory);
        var before = await WorkItemsTestData.ReadAsync(factory, world.Items, story.Identifier);
        var candidates = new object?[]
        {
            world.Items.Types["US"].StateId(StateCategory.Removed).ToString(),
            world.Items.Types["TASK"].StateId("Новая").ToString(),
            foreign.Items.Types["US"].StateId("Новая").ToString(),
            Guid.NewGuid().ToString(),
            Guid.Empty.ToString(),
            "not-a-guid",
            string.Empty,
        };

        foreach (var stateId in candidates)
        {
            var answer = await RestoreAsync(world.Items, story.Identifier, new { stateId });

            LinksTestData.AssertRefused(answer, HttpStatusCode.Conflict, StateNotAllowedCode);
        }

        Assert.Equal(before.GetRawText(), (await WorkItemsTestData.ReadAsync(factory, world.Items, story.Identifier)).GetRawText());
    }

    // US-4 · "Restoring something not deleted is refused, even naming «Удалена» or a foreign state": not_deleted comes first and
    // no state is applied.
    [Fact]
    public async Task Restore_ALiveItem_Is409NotDeleted_EvenWithAForeignOrRemovedState_StateNotApplied()
    {
        var world = await BoardsTestData.CreateWorldAsync(factory);
        var story = await BoardsTestData.CreateAsync(factory, world, "US", "Живая", world.PlatformAreaId);
        var before = await LinksTestData.StoredAsync(factory, story);

        foreach (var body in new object?[]
        {
            null,
            new { },
            new { stateId = world.Items.Types["US"].StateId(StateCategory.Removed).ToString() },
            new { stateId = world.Items.Types["TASK"].StateId("Новая").ToString() },
            new { stateId = "not-a-guid" },
            new { stateId = world.Items.Types["US"].StateId("Закрыта").ToString() },
        })
        {
            LinksTestData.AssertRefused(await RestoreAsync(world.Items, story.Identifier, body), HttpStatusCode.Conflict, NotDeletedCode);
        }

        Assert.Equal(before, await LinksTestData.StoredAsync(factory, story));
    }

    // US-4 · "An item with no remembered state has a way back" (BR-DR-10): one put in «Удалена» outside Delete goes to the first
    // state of its type.
    [Fact]
    public async Task Restore_AnItemPutInRemovedWithNoMarks_GoesToTheFirstStateOfItsType()
    {
        var world = await BoardsTestData.CreateWorldAsync(factory);
        var story = await BoardsTestData.CreateAsync(factory, world, "US", "Без отметок", world.PlatformAreaId);
        Assert.Equal(HttpStatusCode.OK, (await BoardsTestData.SetStateAsync(factory, world, story, "US", "Активна")).Status);
        await LinksTestData.PutInRemovedWithNoMarksAsync(factory, world.Items, story);
        var deleted = await WorkItemsTestData.ReadAsync(factory, world.Items, story.Identifier);
        Assert.Equal(JsonValueKind.Null, deleted.GetProperty("deletion").ValueKind);

        var answer = await RestoreAsync(world.Items, story.Identifier);

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Equal(world.Items.Types["US"].StateId("Новая"), answer.Body.GetProperty("state").GetProperty("id").GetGuid());
    }

    // US-4 · "A restore changes nothing but the state": the parent's «Изменён», the children, the fields and the team are as
    // they were; only the state, «Изменён» and the marks differ.
    [Fact]
    public async Task Restore_ChangesNothingButTheState_AndTheMarks()
    {
        var world = await BoardsTestData.CreateWorldAsync(factory);
        var feature = await BoardsTestData.CreateAsync(factory, world, "FEAT", "Фича", world.PlatformAreaId);
        var story = await BoardsTestData.CreateAsync(factory, world, "US", "История", world.PlatformAreaId);
        var task = await BoardsTestData.CreateAsync(factory, world, "TASK", "Задача");
        await LinksTestData.LinkAsync(factory, world.Items, story, feature);
        await LinksTestData.LinkAsync(factory, world.Items, task, story);
        Assert.Equal(HttpStatusCode.OK, (await WorkItemsTestData.SaveAsync(factory, world.Items, story.Identifier, new { description = "Описание", priority = new { value = 1 } })).Status);
        await LinksTestData.DeleteAsync(factory, world.Items, story);
        var deleted = await WorkItemsTestData.ReadAsync(factory, world.Items, story.Identifier);
        var parentBefore = await LinksTestData.StoredAsync(factory, feature);
        var childBefore = await LinksTestData.StoredAsync(factory, task);

        var answer = await RestoreAsync(world.Items, story.Identifier);

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        foreach (var property in deleted.EnumerateObject().Where(property => property.Name is not ("state" or "changedAt" or "deletion")))
        {
            Assert.True(
                property.Value.GetRawText() == answer.Body.GetProperty(property.Name).GetRawText(),
                $"{property.Name} changed by a restore: {property.Value.GetRawText()} -> {answer.Body.GetProperty(property.Name).GetRawText()}.");
        }

        Assert.Equal(parentBefore, await LinksTestData.StoredAsync(factory, feature));
        Assert.Equal(childBefore, await LinksTestData.StoredAsync(factory, task));
    }

    // US-4 · "The item returns to its owner as stored": its team's backlog has it again.
    [Fact]
    public async Task Restore_ReturnsToItsOwnersViews()
    {
        var (world, story) = await DeletedStoryAsync();
        Assert.DoesNotContain(story.Identifier, await BoardsTestData.RowsAsync(factory, world, world.PlatformId));

        Assert.Equal(HttpStatusCode.OK, (await RestoreAsync(world.Items, story.Identifier)).Status);

        Assert.Contains(story.Identifier, await BoardsTestData.RowsAsync(factory, world, world.PlatformId));
        Assert.DoesNotContain(story.Identifier, await BoardsTestData.RowsAsync(factory, world, world.ProjectTeamId));
    }

    // US-4 · "an owner deleted meanwhile leaves it «Без команды»": it is on no team's views, and on the project's.
    [Fact]
    public async Task Restore_AfterItsTeamWasDeleted_IsOnNoTeamsViews_OnTheProjects()
    {
        var (world, story) = await DeletedStoryAsync();
        var removal = await OrganizationsApi.SendAsync(
            factory, HttpMethod.Delete, $"{OrganizationsApi.Teams(world.OrganizationId, world.ProjectId)}/{world.PlatformId}", token: OrganizationsTokens.SignedIn());
        Assert.Equal(HttpStatusCode.NoContent, removal.Status);

        var answer = await RestoreAsync(world.Items, story.Identifier);

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Equal(JsonValueKind.Null, answer.Body.GetProperty("team").ValueKind);
        Assert.Contains(story.Identifier, await ProjectBoardsTestData.RowsAsync(factory, world));
        Assert.DoesNotContain(story.Identifier, await BoardsTestData.RowsAsync(factory, world, world.ProjectTeamId));
    }

    // US-4 · "The backlog place follows mod-006": a backlog nobody reordered since returns the item where it stood.
    [Fact]
    public async Task Restore_BacklogNotReordered_ReturnsWhereItStood()
    {
        var world = await BoardsTestData.CreateWorldAsync(factory);
        var first = await BoardsTestData.CreateAsync(factory, world, "US", "Первая", world.PlatformAreaId);
        var middle = await BoardsTestData.CreateAsync(factory, world, "US", "Средняя", world.PlatformAreaId);
        var last = await BoardsTestData.CreateAsync(factory, world, "US", "Последняя", world.PlatformAreaId);
        await BoardsTestData.ReorderToAsync(factory, world, world.PlatformId, first, middle, last);
        await ProjectBoardsTestData.ReorderToAsync(factory, world, first, middle, last);
        await LinksTestData.DeleteAsync(factory, world.Items, middle);

        Assert.Equal(HttpStatusCode.OK, (await RestoreAsync(world.Items, middle.Identifier)).Status);

        Assert.Equal([first.Identifier, middle.Identifier, last.Identifier], await BoardsTestData.RowsAsync(factory, world, world.PlatformId));
        Assert.Equal([first.Identifier, middle.Identifier, last.Identifier], await ProjectBoardsTestData.RowsAsync(factory, world));
    }

    // US-4 · "A backlog reordered meanwhile": the reorder drops the stale place of the deleted item, so it returns unplaced,
    // below every placed item.
    [Fact]
    public async Task Restore_BacklogReorderedMeanwhile_ReturnsBelowEveryPlacedItem()
    {
        var world = await BoardsTestData.CreateWorldAsync(factory);
        var first = await BoardsTestData.CreateAsync(factory, world, "US", "Первая", world.PlatformAreaId);
        var middle = await BoardsTestData.CreateAsync(factory, world, "US", "Средняя", world.PlatformAreaId);
        var last = await BoardsTestData.CreateAsync(factory, world, "US", "Последняя", world.PlatformAreaId);
        await BoardsTestData.ReorderToAsync(factory, world, world.PlatformId, first, middle, last);
        await ProjectBoardsTestData.ReorderToAsync(factory, world, first, middle, last);
        await LinksTestData.DeleteAsync(factory, world.Items, middle);

        // Somebody reorders both backlogs while the item is deleted: the last item goes to the top.
        await BoardsTestData.MoveAsync(factory, world, world.PlatformId, BoardsTestData.Top(last));
        await ProjectBoardsTestData.MoveAsync(factory, world, BoardsTestData.Top(last));
        Assert.Equal(HttpStatusCode.OK, (await RestoreAsync(world.Items, middle.Identifier)).Status);

        Assert.Equal([last.Identifier, first.Identifier, middle.Identifier], await BoardsTestData.RowsAsync(factory, world, world.PlatformId));
        Assert.Equal([last.Identifier, first.Identifier, middle.Identifier], await ProjectBoardsTestData.RowsAsync(factory, world));
    }

    // US-4 · "A second deletion remembers the latest state" (BR-DR-2).
    [Fact]
    public async Task Delete_Restore_Move_Delete_RemembersTheLatestState()
    {
        var (world, story) = await DeletedStoryAsync();
        Assert.Equal(HttpStatusCode.OK, (await RestoreAsync(world.Items, story.Identifier)).Status);
        Assert.Equal(HttpStatusCode.OK, (await BoardsTestData.SetStateAsync(factory, world, story, "US", "Закрыта")).Status);

        await LinksTestData.DeleteAsync(factory, world.Items, story);

        var deletion = (await WorkItemsTestData.ReadAsync(factory, world.Items, story.Identifier)).GetProperty("deletion");
        Assert.Equal("Закрыта", deletion.GetProperty("fromState").GetProperty("name").GetString());
        Assert.Equal(HttpStatusCode.OK, (await RestoreAsync(world.Items, story.Identifier)).Status);
        Assert.Equal("Закрыта", (await WorkItemsTestData.ReadAsync(factory, world.Items, story.Identifier)).GetProperty("state").GetProperty("name").GetString());
    }

    // US-4 · "Who may restore" (BR-DR-17): a member and the administrator; a reader is refused; a non-member gets the
    // organization's not-found.
    [Fact]
    public async Task Restore_AViewer_Is403()
    {
        var (world, story) = await DeletedStoryAsync();
        var viewer = await MembershipTestData.NewMemberAsync(factory, world.OrganizationId, OrganizationRole.Viewer);
        var before = await WorkItemsTestData.ReadAsync(factory, world.Items, story.Identifier);

        var answer = await RestoreAsync(world.Items, story.Identifier, token: viewer.Token);

        LinksTestData.AssertRefused(answer, HttpStatusCode.Forbidden, "organizations.access.forbidden");
        Assert.Equal(before.GetRawText(), (await WorkItemsTestData.ReadAsync(factory, world.Items, story.Identifier)).GetRawText());
    }

    [Fact]
    public async Task Restore_ANonMember_Is404OrganizationNotFound()
    {
        var (world, story) = await DeletedStoryAsync();

        var answer = await RestoreAsync(world.Items, story.Identifier, token: MembershipTestData.Outsider());

        LinksTestData.AssertRefused(answer, HttpStatusCode.NotFound, "organizations.organization.not_found");
    }

    [Theory]
    [InlineData(OrganizationRole.Member)]
    [InlineData(OrganizationRole.Admin)]
    public async Task Restore_AMember_AndTheAdministrator_Can(OrganizationRole role)
    {
        var (world, story) = await DeletedStoryAsync();
        var caller = await MembershipTestData.NewMemberAsync(factory, world.OrganizationId, role);

        var answer = await RestoreAsync(world.Items, story.Identifier, token: caller.Token);

        Assert.Equal(HttpStatusCode.OK, answer.Status);
    }

    // US-4 · "A deactivated project is not reached": restorable once the project is.
    [Fact]
    public async Task Restore_UnderADeactivatedProject_Is404ProjectNotFound_RestorableOnceTheProjectIs()
    {
        var (world, story) = await DeletedStoryAsync();
        var project = $"/api/organizations/{world.OrganizationId}/projects/{world.ProjectId}";

        Assert.Equal(HttpStatusCode.NoContent, (await OrganizationsApi.SendAsync(factory, HttpMethod.Post, project + "/deactivate", token: OrganizationsTokens.SignedIn())).Status);
        var hidden = await RestoreAsync(world.Items, story.Identifier);
        Assert.Equal(HttpStatusCode.OK, (await OrganizationsApi.SendAsync(factory, HttpMethod.Post, project + "/restore", token: OrganizationsTokens.SignedIn())).Status);

        LinksTestData.AssertRefused(hidden, HttpStatusCode.NotFound, "organizations.project.not_found");
        Assert.Equal(HttpStatusCode.OK, (await RestoreAsync(world.Items, story.Identifier)).Status);
    }

    // Unknown, foreign or malformed addresses answer the level's not-found, as Delete's do.
    [Fact]
    public async Task Restore_UnknownForeignOrMalformedAddress_Is404TheLevelsCode()
    {
        var (world, story) = await DeletedStoryAsync();

        foreach (var identifier in new[] { "MSK-US-99", "MSK-BUG-" + story.Identifier[(story.Identifier.LastIndexOf('-') + 1)..], "XYZ-US-1", "garbage" })
        {
            LinksTestData.AssertRefused(await RestoreAsync(world.Items, identifier), HttpStatusCode.NotFound, "workitems.workitem.not_found");
        }

        LinksTestData.AssertRefused(
            await RestoreAsync(world.Items with { ProjectId = Guid.CreateVersion7() }, story.Identifier), HttpStatusCode.NotFound, "organizations.project.not_found");
    }
}
