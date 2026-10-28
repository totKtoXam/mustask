using System.Net;
using System.Text.Json;
using Mustask.Api.IntegrationTests.Features.Boards;
using Mustask.Api.IntegrationTests.Features.Organizations;
using Mustask.Domain.Identity;
using Mustask.Domain.Process;

namespace Mustask.Api.IntegrationTests.Features.WorkItems;

/// <summary>
/// <c>POST /api/organizations/{organizationId}/projects/{projectId}/workitems/{identifier}/delete</c> (mod-005 api-contract
/// § 2.1, US-1; BR-DR-1..-5, -8, -11, -17): the item moves into its type's «Удалена», and the state it held, who deleted it and
/// when are recorded on its row; nothing else changes.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class DeleteWorkItemEndpointTests(MustaskApiFactory factory)
{
    private const string DeletedCode = "workitems.workitem.deleted";

    private static string DeletePath(WorkItemsWorld world, string identifier) => $"{WorkItemsTestData.Item(world, identifier)}/delete";

    private Task<Answer> DeleteAsync(WorkItemsWorld world, string identifier, string? token = null) =>
        OrganizationsApi.SendAsync(factory, HttpMethod.Post, DeletePath(world, identifier), token: token ?? OrganizationsTokens.SignedIn());

    // US-1 · "Confirming moves the item into «Удалена»": the answer is the item, read-only, with who deleted it and when, the
    // state it held, and «Изменён» at the deletion's time (BR-DR-1, BR-DR-2). A reload reads the same.
    [Fact]
    public async Task Delete_LiveItem_Answers200WithTheDeletedItem_StateRemoved_DeletionMarksSet_ChangedAtIsTheDeletion()
    {
        var world = await BoardsTestData.CreateWorldAsync(factory);
        var anna = await MembershipTestData.NewNamedMemberAsync(factory, world.OrganizationId, OrganizationRole.Member, "Анна", "Смирнова");
        var story = await BoardsTestData.CreateAsync(factory, world, "US", "Письмо со ссылкой", world.PlatformAreaId);
        Assert.Equal(HttpStatusCode.OK, (await BoardsTestData.SetStateAsync(factory, world, story, "US", "Активна")).Status);
        var before = (await LinksTestData.StoredAsync(factory, story)).ChangedAt;

        var answer = await DeleteAsync(world.Items, story.Identifier, anna.Token);

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        var state = answer.Body.GetProperty("state");
        Assert.Equal(("Удалена", "Removed"), (state.GetProperty("name").GetString(), state.GetProperty("category").GetProperty("code").GetString()));
        Assert.Equal(world.Items.Types["US"].StateId(StateCategory.Removed), state.GetProperty("id").GetGuid());

        var deletion = answer.Body.GetProperty("deletion");
        Assert.Equal(("Активна", "InProgress"), (
            deletion.GetProperty("fromState").GetProperty("name").GetString(),
            deletion.GetProperty("fromState").GetProperty("category").GetProperty("code").GetString()));
        Assert.Equal(world.Items.Types["US"].StateId("Активна"), deletion.GetProperty("fromState").GetProperty("id").GetGuid());
        var by = deletion.GetProperty("by");
        Assert.Equal((anna.Id, "Анна Смирнова", true, true), (
            by.GetProperty("id").GetString(),
            by.GetProperty("displayName").GetString(),
            by.GetProperty("isActive").GetBoolean(),
            by.GetProperty("isMember").GetBoolean()));
        Assert.Equal(["displayName", "id", "isActive", "isMember"], by.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));

        var changedAt = answer.Body.GetProperty("changedAt").GetDateTimeOffset();
        Assert.True(changedAt > before);
        Assert.Equal(changedAt, deletion.GetProperty("at").GetDateTimeOffset());

        var reloaded = await WorkItemsTestData.ReadAsync(factory, world.Items, story.Identifier);
        Assert.Equal(answer.Body.GetProperty("deletion").GetRawText(), reloaded.GetProperty("deletion").GetRawText());
        Assert.Equal(("Removed", changedAt), (
            reloaded.GetProperty("state").GetProperty("category").GetProperty("code").GetString(), reloaded.GetProperty("changedAt").GetDateTimeOffset()));
    }

    // A live item carries no deletion: the field is present and null.
    [Fact]
    public async Task Get_ALiveItem_HasDeletionNull()
    {
        var world = await BoardsTestData.CreateWorldAsync(factory);
        var story = await BoardsTestData.CreateAsync(factory, world, "US", "Живая", world.PlatformAreaId);

        var item = await WorkItemsTestData.ReadAsync(factory, world.Items, story.Identifier);

        Assert.Equal(JsonValueKind.Null, item.GetProperty("deletion").ValueKind);
    }

    // US-1 · "A deletion changes nothing else" (BR-DR-3): fields, assignee, area, team, priority and parent are as they were,
    // and the parent's «Изменён» does not move (BR-DR-4).
    [Fact]
    public async Task Delete_ChangesNothingElse_AndTheParentsChangedAtIsUnchanged()
    {
        var world = await BoardsTestData.CreateWorldAsync(factory);
        var anna = await WorkItemsTestData.AddMemberAsync(factory, world.Items, "Анна", "Смирнова");
        var feature = await BoardsTestData.CreateAsync(factory, world, "FEAT", "Фича", world.PlatformAreaId);
        var story = await BoardsTestData.CreateAsync(factory, world, "US", "История", world.ApiAreaId);
        await LinksTestData.LinkAsync(factory, world.Items, story, feature);
        Assert.Equal(
            HttpStatusCode.OK,
            (await WorkItemsTestData.SaveAsync(
                factory,
                world.Items,
                story.Identifier,
                new { description = "Описание", assignee = new { userId = anna }, priority = new { value = 1 }, valueArea = new { value = "Architectural" } })).Status);
        var parentBefore = await LinksTestData.StoredAsync(factory, feature);
        var before = await WorkItemsTestData.ReadAsync(factory, world.Items, story.Identifier);

        var answer = await DeleteAsync(world.Items, story.Identifier);

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        var changed = new[] { "state", "changedAt", "deletion" };

        foreach (var property in before.EnumerateObject().Where(property => !changed.Contains(property.Name)))
        {
            Assert.True(
                property.Value.GetRawText() == answer.Body.GetProperty(property.Name).GetRawText(),
                $"{property.Name} changed by a deletion: {property.Value.GetRawText()} -> {answer.Body.GetProperty(property.Name).GetRawText()}.");
        }

        Assert.Equal(parentBefore, await LinksTestData.StoredAsync(factory, feature));
        Assert.Equal(feature.Id, (await LinksTestData.StoredParentAsync(factory, story)));
    }

    // US-1 · "An item is deleted from any non-Removed state, each into its own type's «Удалена»".
    [Theory]
    [InlineData("EPIC")]
    [InlineData("FEAT")]
    [InlineData("US")]
    [InlineData("BUG")]
    [InlineData("TASK")]
    public async Task Delete_FromEveryNonRemovedState_OfEveryType_RemembersTheStateHeld(string typeKey)
    {
        var world = await WorkItemsTestData.CreateWorldAsync(factory);
        var type = world.Types[typeKey];

        foreach (var state in type.States.Where(state => state.Category != StateCategory.Removed))
        {
            var identifier = await WorkItemsTestData.CreateItemAsync(factory, world, typeKey, $"Из состояния {state.Name}");
            await WorkItemsTestData.ExecuteAsync(factory, $"UPDATE work_items SET state_id = {state.Id} WHERE project_id = {world.ProjectId} AND number = {NumberOf(identifier)}");

            var answer = await DeleteAsync(world, identifier);

            Assert.Equal(HttpStatusCode.OK, answer.Status);
            Assert.Equal(type.StateId(StateCategory.Removed), answer.Body.GetProperty("state").GetProperty("id").GetGuid());
            Assert.Equal(state.Id, answer.Body.GetProperty("deletion").GetProperty("fromState").GetProperty("id").GetGuid());
        }
    }

    private static int NumberOf(string identifier) => int.Parse(identifier[(identifier.LastIndexOf('-') + 1)..]);

    // US-1 · "Nobody else's work is touched": the other items, the project, its teams and areas, the members, and another
    // organization's items are as they were.
    [Fact]
    public async Task Delete_TouchesNoOtherItemProjectTeamAreaOrMember()
    {
        var world = await BoardsTestData.CreateWorldAsync(factory);
        var other = await BoardsTestData.CreateWorldAsync(factory);
        var target = await BoardsTestData.CreateAsync(factory, world, "US", "Удаляемая", world.PlatformAreaId);
        var sibling = await BoardsTestData.CreateAsync(factory, world, "US", "Соседняя", world.PlatformAreaId);
        var foreign = await BoardsTestData.CreateAsync(factory, other, "US", "Чужая", other.PlatformAreaId);
        var targetNumber = NumberOf(target.Identifier);
        var before = await SnapshotAsync(world, other, targetNumber);

        Assert.Equal(HttpStatusCode.OK, (await DeleteAsync(world.Items, target.Identifier)).Status);

        Assert.Equal(before, await SnapshotAsync(world, other, targetNumber));
        Assert.Equal(HttpStatusCode.OK, (await WorkItemsTestData.GetAsync(factory, world.Items, sibling.Identifier)).Status);
        Assert.Equal(HttpStatusCode.OK, (await WorkItemsTestData.GetAsync(factory, other.Items, foreign.Identifier)).Status);
        Assert.Equal("Removed", (await WorkItemsTestData.ReadAsync(factory, world.Items, target.Identifier)).GetProperty("state").GetProperty("category").GetProperty("code").GetString());
    }

    /// <summary>
    /// Every item of both projects but <paramref name="exceptNumber"/> of the first, and both projects' teams and areas and both
    /// organizations' members as the API reads them, as rows of text.
    /// </summary>
    private async Task<List<string>> SnapshotAsync(BoardsWorld world, BoardsWorld other, int exceptNumber)
    {
        var rows = new List<string>();

        foreach (var (organization, project, except) in new[] { (world.OrganizationId, world.ProjectId, exceptNumber), (other.OrganizationId, other.ProjectId, 0) })
        {
            rows.AddRange((await WorkItemsTestData.StoredAsync(factory, project))
                .Where(item => item.Number != except)
                .Select(item => $"{item.Number}:{item.Title}:{item.StateId}:{item.AssigneeId}:{item.AreaId}:{item.ChangedAt:O}"));

            foreach (var path in new[] { OrganizationsApi.Teams(organization, project), OrganizationsApi.Areas(organization, project), OrganizationsApi.Organization(organization) + "/members" })
            {
                var answer = await OrganizationsApi.SendAsync(factory, HttpMethod.Get, path, token: OrganizationsTokens.SignedIn());
                Assert.Equal(HttpStatusCode.OK, answer.Status);
                rows.Add($"{path}:{answer.Body.GetRawText()}");
            }
        }

        return rows;
    }

    // US-1 · "Deleting something already deleted" (BR-DR-11): the second is refused; the record of the first stays.
    [Fact]
    public async Task Delete_AlreadyDeleted_Is409Deleted_TheHeldStateIsTheFirstDeletions()
    {
        var world = await BoardsTestData.CreateWorldAsync(factory);
        var first = await MembershipTestData.NewNamedMemberAsync(factory, world.OrganizationId, OrganizationRole.Member, "Анна", "Смирнова");
        var second = await MembershipTestData.NewMemberAsync(factory, world.OrganizationId, OrganizationRole.Member);
        var story = await BoardsTestData.CreateAsync(factory, world, "US", "Дважды", world.PlatformAreaId);
        Assert.Equal(HttpStatusCode.OK, (await BoardsTestData.SetStateAsync(factory, world, story, "US", "Активна")).Status);

        var done = await DeleteAsync(world.Items, story.Identifier, first.Token);
        var again = await DeleteAsync(world.Items, story.Identifier, second.Token);

        Assert.Equal(HttpStatusCode.OK, done.Status);
        LinksTestData.AssertRefused(again, HttpStatusCode.Conflict, DeletedCode);
        var deletion = (await WorkItemsTestData.ReadAsync(factory, world.Items, story.Identifier)).GetProperty("deletion");
        Assert.Equal(first.Id, deletion.GetProperty("by").GetProperty("id").GetString());
        Assert.Equal("Активна", deletion.GetProperty("fromState").GetProperty("name").GetString());
        Assert.Equal(done.Body.GetProperty("deletion").GetRawText(), deletion.GetRawText());
    }

    // US-1 · "A reader and a non-member cannot delete" (BR-DR-17): the route's code first; nothing changes.
    [Fact]
    public async Task Delete_AViewer_Is403_AndChangesNothing()
    {
        var world = await BoardsTestData.CreateWorldAsync(factory);
        var viewer = await MembershipTestData.NewMemberAsync(factory, world.OrganizationId, OrganizationRole.Viewer);
        var story = await BoardsTestData.CreateAsync(factory, world, "US", "Читателю нельзя", world.PlatformAreaId);
        var before = await LinksTestData.StoredAsync(factory, story);

        var answer = await DeleteAsync(world.Items, story.Identifier, viewer.Token);

        LinksTestData.AssertRefused(answer, HttpStatusCode.Forbidden, "organizations.access.forbidden");
        Assert.Equal(before, await LinksTestData.StoredAsync(factory, story));
    }

    // BR-DR-17 · "a member and the administrator can": the role bundle holds the code.
    [Theory]
    [InlineData(OrganizationRole.Member)]
    [InlineData(OrganizationRole.Admin)]
    public async Task Delete_AMember_AndTheAdministrator_Can(OrganizationRole role)
    {
        var world = await BoardsTestData.CreateWorldAsync(factory);
        var caller = await MembershipTestData.NewMemberAsync(factory, world.OrganizationId, role);
        var story = await BoardsTestData.CreateAsync(factory, world, "US", "Можно", world.PlatformAreaId);

        var answer = await DeleteAsync(world.Items, story.Identifier, caller.Token);

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Equal(caller.Id, answer.Body.GetProperty("deletion").GetProperty("by").GetProperty("id").GetString());
    }

    // BR-DR-17 · a non-member — another organization's administrator, and the instance administrator — gets the organization's
    // not-found, never a 403, and the item is as it was.
    [Fact]
    public async Task Delete_ANonMember_AnotherOrganizationsAdministrator_AndTheInstanceAdministrator_Is404OrganizationNotFound()
    {
        var world = await BoardsTestData.CreateWorldAsync(factory);
        var other = await BoardsTestData.CreateWorldAsync(factory);
        var story = await BoardsTestData.CreateAsync(factory, world, "US", "Чужим нельзя", world.PlatformAreaId);
        var foreignAdmin = await MembershipTestData.NewMemberAsync(factory, other.OrganizationId, OrganizationRole.Admin);
        var instanceAdmin = KeycloakTestRealm.CreateToken([.. PermissionCatalog.InstanceCodes], options => options.Subject = OrganizationsTokens.NewSubject());
        var before = await LinksTestData.StoredAsync(factory, story);

        foreach (var token in new[] { foreignAdmin.Token, instanceAdmin, MembershipTestData.Outsider() })
        {
            var answer = await DeleteAsync(world.Items, story.Identifier, token);

            LinksTestData.AssertRefused(answer, HttpStatusCode.NotFound, "organizations.organization.not_found");
        }

        Assert.Equal(before, await LinksTestData.StoredAsync(factory, story));
    }

    // US-1 · "Unknown, foreign or deactivated addresses": each answers the level's own not-found, nothing changes, and a
    // project restored after its deactivation still has the item live.
    [Fact]
    public async Task Delete_UnknownForeignMalformedOrDeactivated_Is404TheLevelsCode_NothingChanges_AndTheRestoredProjectKeepsItLive()
    {
        var world = await BoardsTestData.CreateWorldAsync(factory);
        var story = await BoardsTestData.CreateAsync(factory, world, "US", "Адрес", world.PlatformAreaId);
        var other = await WorkItemsTestData.AddProjectAsync(factory, world.Items, "OTH", "Другой");
        var otherStory = await WorkItemsTestData.CreateItemAsync(factory, other, "US", "В другом проекте");
        var before = await LinksTestData.StoredAsync(factory, story);

        foreach (var identifier in new[] { "MSK-US-99", "MSK-BUG-" + NumberOf(story.Identifier), "XYZ-US-1", "garbage", "MSK-US-0", otherStory })
        {
            LinksTestData.AssertRefused(await DeleteAsync(world.Items, identifier), HttpStatusCode.NotFound, "workitems.workitem.not_found");
        }

        var unknownProject = world.Items with { ProjectId = Guid.CreateVersion7() };
        LinksTestData.AssertRefused(await DeleteAsync(unknownProject, story.Identifier), HttpStatusCode.NotFound, "organizations.project.not_found");

        // A deactivated project is not reached; once it is restored the item is live as it was.
        var project = $"/api/organizations/{world.OrganizationId}/projects/{world.ProjectId}";
        Assert.Equal(HttpStatusCode.NoContent, (await OrganizationsApi.SendAsync(factory, HttpMethod.Post, project + "/deactivate", token: OrganizationsTokens.SignedIn())).Status);
        LinksTestData.AssertRefused(await DeleteAsync(world.Items, story.Identifier), HttpStatusCode.NotFound, "organizations.project.not_found");
        Assert.Equal(HttpStatusCode.OK, (await OrganizationsApi.SendAsync(factory, HttpMethod.Post, project + "/restore", token: OrganizationsTokens.SignedIn())).Status);

        Assert.Equal(before, await LinksTestData.StoredAsync(factory, story));
        Assert.Equal(JsonValueKind.Null, (await WorkItemsTestData.ReadAsync(factory, world.Items, story.Identifier)).GetProperty("deletion").ValueKind);
        Assert.Equal(HttpStatusCode.OK, (await DeleteAsync(world.Items, story.Identifier)).Status);
    }
}
