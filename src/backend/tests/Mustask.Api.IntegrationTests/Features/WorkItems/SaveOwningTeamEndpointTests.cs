using System.Net;
using Mustask.Api.IntegrationTests.Features.Organizations;
using Mustask.Domain.Identity;
using Mustask.Domain.Process;
using static Mustask.Api.IntegrationTests.Features.WorkItems.OwningTeamTestData;

namespace Mustask.Api.IntegrationTests.Features.WorkItems;

/// <summary>
/// <c>PATCH …/workitems/{identifier}</c> with the owning team (mod-017 api-contract §§ 0.2, 2.4; US-1) through the real host and a
/// real PostgreSQL. The team travels in a wrapper, <c>{ "team": { "teamId": … } }</c>; absent keeps the stored value, <c>null</c>
/// inside the wrapper is «Без команды». Every test builds its own world (<see cref="OwningTeamTestData"/>): the database is
/// shared and never emptied.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class SaveOwningTeamEndpointTests(MustaskApiFactory factory)
{
    private const string TeamNotFound = "organizations.team.not_found";
    private const string TeamNotInProject = "organizations.team.not_in_project";

    private static object TeamBody(Guid? teamId) => new { team = new { teamId = teamId?.ToString() } };

    private Task<Answer> SaveAsync(OwningTeamWorld world, string identifier, object body, string? token = null) =>
        OrganizationsApi.SendAsync(
            factory, HttpMethod.Patch, WorkItemsTestData.Item(world.Items, identifier), body, token ?? OrganizationsTokens.SignedIn());

    private async Task<(Guid Id, string Identifier)> StoryAsync(OwningTeamWorld world, string title = "Вход по ссылке") =>
        await CreateAsync(factory, world.Items, "US", title);

    // US-1 · "shown wherever the item is": the item answers its team as an id and a name, or null for «Без команды»; the
    // create, the read and the save all answer it, and a rename shows at once.
    [Fact]
    public async Task Get_Save_AnswerTheTeamIdAndName_OrNone()
    {
        var world = await CreateWorldAsync(factory);
        var story = await StoryAsync(world);

        Assert.Null(TeamOf(await WorkItemsTestData.ReadAsync(factory, world.Items, story.Identifier)));

        var saved = await SaveAsync(world, story.Identifier, TeamBody(world.Mars));
        Assert.Equal(HttpStatusCode.OK, saved.Status);
        Assert.Equal((world.Mars, "MARS"), TeamOf(saved.Body));
        Assert.Equal((world.Mars, "MARS"), TeamOf(await WorkItemsTestData.ReadAsync(factory, world.Items, story.Identifier)));

        await WorkItemsTestData.ExecuteAsync(factory, $"UPDATE teams SET name = 'ARES', normalized_name = 'ARES' WHERE id = {world.Mars}");
        Assert.Equal((world.Mars, "ARES"), TeamOf(await WorkItemsTestData.ReadAsync(factory, world.Items, story.Identifier)));

        var cleared = await SaveAsync(world, story.Identifier, TeamBody(null));
        Assert.Null(TeamOf(cleared.Body));
    }

    // US-1 · "a reader cannot change it": «Читатель» holds no workitems.workitem.write.
    [Fact]
    public async Task Save_ReaderSetsTeam_Returns403_ChangesNothing()
    {
        var world = await CreateWorldAsync(factory);
        var story = await StoryAsync(world);
        var reader = await MembershipTestData.NewMemberAsync(factory, world.OrganizationId, OrganizationRole.Viewer);
        var before = await StoredAsync(factory, world.ProjectId, story.Identifier);

        var answer = await SaveAsync(world, story.Identifier, TeamBody(world.Mars), reader.Token);

        Assert.Equal((HttpStatusCode.Forbidden, "organizations.access.forbidden"), (answer.Status, answer.ErrorCode));
        Assert.Equal(before, await StoredAsync(factory, world.ProjectId, story.Identifier));
    }

    // US-1 · "changing the team changes nothing else": the team and «Изменён» move; the project, the area, the assignee, the
    // state, the project's place are what they were; the item's old team place is dropped in the same write (BR-OT-6).
    [Fact]
    public async Task Save_SetTeam_ChangesTeamAndChangedAtOnly_KeepsProjectOrderAndPlaces_DropsTheTeamPlace()
    {
        var world = await CreateWorldAsync(factory);
        var story = await StoryAsync(world);
        var assignee = await WorkItemsTestData.AddMemberAsync(factory, world.Items, "Анна", "Иванова");
        await WorkItemsTestData.SaveAsync(factory, world.Items, story.Identifier, new { assignee = new { userId = assignee } });

        // The project's place (a reorder of the project's stories backlog) and a team place on VENUS.
        var reorder = await OrganizationsApi.SendAsync(
            factory,
            HttpMethod.Post,
            $"{OrganizationsApi.Project(world.OrganizationId, world.ProjectId)}/backlogs/stories/reorder",
            new { itemId = story.Id.ToString(), position = "top" },
            OrganizationsTokens.SignedIn());
        Assert.Equal(HttpStatusCode.NoContent, reorder.Status);
        await SetOwningTeamAsync(factory, story.Id, world.Venus);
        await PlaceAsync(factory, story.Id, world.Venus, world.Items.Types["US"].BacklogLevelId, 1);
        var before = await StoredAsync(factory, world.ProjectId, story.Identifier);
        Assert.Equal(1, await TeamPlacesOfAsync(factory, story.Id));
        Assert.Equal(1, await ProjectPlacesOfAsync(factory, story.Id));

        var answer = await SaveAsync(world, story.Identifier, TeamBody(world.Mars));

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        var after = await StoredAsync(factory, world.ProjectId, story.Identifier);
        Assert.Equal(world.Mars, after.TeamId);
        Assert.True(after.ChangedAt > before.ChangedAt);
        Assert.Equal((before.AssigneeId, before.StateId), (after.AssigneeId, after.StateId));
        Assert.Equal(0, await TeamPlacesOfAsync(factory, story.Id));
        Assert.Equal(1, await ProjectPlacesOfAsync(factory, story.Id));
    }

    // US-1 · "not sent keeps": a save of the title alone, after another save changed the team, keeps the new team (BR-M3-25).
    [Fact]
    public async Task Save_TitleOnlyWhileTeamChangedMeanwhile_KeepsTheNewTeam()
    {
        var world = await CreateWorldAsync(factory);
        var story = await StoryAsync(world);

        await SaveAsync(world, story.Identifier, TeamBody(world.Mars));
        var titleOnly = await SaveAsync(world, story.Identifier, new { title = "Вход по ссылке из письма" });

        Assert.Equal(HttpStatusCode.OK, titleOnly.Status);
        Assert.Equal((world.Mars, "MARS"), TeamOf(titleOnly.Body));
        Assert.Equal(world.Mars, (await StoredAsync(factory, world.ProjectId, story.Identifier)).TeamId);
    }

    // US-1 · "none is a choice; none again is a no-op": «Без команды» is stored as a value; sending it again changes nothing.
    [Fact]
    public async Task Save_NoTeam_ThenNoTeamAgain_ChangedAtDoesNotMove()
    {
        var world = await CreateWorldAsync(factory);
        var story = await StoryAsync(world);
        await SetOwningTeamAsync(factory, story.Id, world.Mars);

        var first = await SaveAsync(world, story.Identifier, TeamBody(null));
        Assert.Equal(HttpStatusCode.OK, first.Status);
        Assert.Null(TeamOf(first.Body));
        var afterFirst = await StoredAsync(factory, world.ProjectId, story.Identifier);
        Assert.Null(afterFirst.TeamId);

        var second = await SaveAsync(world, story.Identifier, TeamBody(null));
        Assert.Equal(HttpStatusCode.OK, second.Status);
        Assert.Equal(afterFirst, await StoredAsync(factory, world.ProjectId, story.Identifier));
    }

    // US-1 · "a team already set": naming the stored team again is a no-op too.
    [Fact]
    public async Task Save_TheSameTeamAgain_ChangedAtDoesNotMove()
    {
        var world = await CreateWorldAsync(factory);
        var story = await StoryAsync(world);
        await SaveAsync(world, story.Identifier, TeamBody(world.Mars));
        var before = await StoredAsync(factory, world.ProjectId, story.Identifier);

        var again = await SaveAsync(world, story.Identifier, TeamBody(world.Mars));

        Assert.Equal(HttpStatusCode.OK, again.Status);
        Assert.Equal(before, await StoredAsync(factory, world.ProjectId, story.Identifier));
    }

    // api-contract § 0.2 · `"team": {}` is `{ "teamId": null }`: the key left out inside the wrapper means none.
    [Fact]
    public async Task Save_EmptyTeamWrapper_IsNone()
    {
        var world = await CreateWorldAsync(factory);
        var story = await StoryAsync(world);
        await SetOwningTeamAsync(factory, story.Id, world.Mars);

        var answer = await SaveAsync(world, story.Identifier, new { team = new { } });

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Null(TeamOf(answer.Body));
        Assert.Null((await StoredAsync(factory, world.ProjectId, story.Identifier)).TeamId);
    }

    // api-contract § 0.2 · `"team": null` in place of the wrapper is «not sent».
    [Fact]
    public async Task Save_NullInPlaceOfTheWrapper_IsNotSent_KeepsTheTeam()
    {
        var world = await CreateWorldAsync(factory);
        var story = await StoryAsync(world);
        await SetOwningTeamAsync(factory, story.Id, world.Mars);

        var answer = await SaveAsync(world, story.Identifier, new { title = "Новый заголовок", team = (object?)null });

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Equal((world.Mars, "MARS"), TeamOf(answer.Body));
    }

    // US-1 · "a team the project does not have" (feat-028 api-contract § 0.3): an active team of the organization that is not linked to the
    // project — another project's, a deactivated project's (it is nobody's project's team now: mod-017's row "a team of a deactivated project is
    // 404" is gone) — is 409 «Команда не участвует в проекте.»; a deleted team, an unknown and a malformed id are 404 «Команда не найдена.».
    // The refusal stores nothing of the request, the title sent with it included.
    [Theory]
    [InlineData("otherProject", HttpStatusCode.Conflict, TeamNotInProject)]
    [InlineData("deleted", HttpStatusCode.NotFound, TeamNotFound)]
    [InlineData("deactivatedProject", HttpStatusCode.Conflict, TeamNotInProject)]
    [InlineData("unknown", HttpStatusCode.NotFound, TeamNotFound)]
    [InlineData("malformed", HttpStatusCode.NotFound, TeamNotFound)]
    public async Task Save_TeamRefusals_StoreNothing(string which, HttpStatusCode status, string code)
    {
        var world = await CreateWorldAsync(factory);
        var story = await StoryAsync(world);
        await SetOwningTeamAsync(factory, story.Id, world.Venus);
        var before = await StoredAsync(factory, world.ProjectId, story.Identifier);
        var numbersBefore = (await WorkItemsTestData.StoredAsync(factory, world.ProjectId)).Count;

        var named = which switch
        {
            "otherProject" => world.OtherProjectTeam.ToString(),
            "deleted" => world.DeletedTeam.ToString(),
            "deactivatedProject" => world.DeactivatedProjectTeam.ToString(),
            "unknown" => Guid.CreateVersion7().ToString(),
            _ => "not-an-id",
        };

        var answer = await SaveAsync(world, story.Identifier, new { title = "Не должен сохраниться", team = new { teamId = named } });

        Assert.Equal((status, code), (answer.Status, answer.ErrorCode));
        Assert.Equal(before, await StoredAsync(factory, world.ProjectId, story.Identifier));
        Assert.Equal("Вход по ссылке", Assert.Single(await WorkItemsTestData.StoredAsync(factory, world.ProjectId), item => item.Number == int.Parse(story.Identifier[(story.Identifier.LastIndexOf('-') + 1)..])).Title);
        Assert.Equal(numbersBefore, (await WorkItemsTestData.StoredAsync(factory, world.ProjectId)).Count);
    }

    // US-1 · "another organization's team answers as unknown" (BR-OT-16): the status and the body are those of an id that
    // names nothing.
    [Fact]
    public async Task Save_ForeignTeam_AnswersExactlyAsUnknown()
    {
        var world = await CreateWorldAsync(factory);
        var story = await StoryAsync(world);

        var foreign = await SaveAsync(world, story.Identifier, TeamBody(world.OtherOrganizationTeam));
        var unknown = await SaveAsync(world, story.Identifier, TeamBody(Guid.CreateVersion7()));

        Assert.Equal(HttpStatusCode.NotFound, foreign.Status);
        Assert.Equal((unknown.Status, unknown.ErrorCode, unknown.Detail, unknown.MediaType), (foreign.Status, foreign.ErrorCode, foreign.Detail, foreign.MediaType));
        Assert.Equal(TeamNotFound, foreign.ErrorCode);
        Assert.Null((await StoredAsync(factory, world.ProjectId, story.Identifier)).TeamId);
    }

    // US-1 · "the address is answered before the team", api-contract § 0.4: the item's 404 and the role's 403 come before the
    // team's, and the values' 400 before the team's 409.
    [Fact]
    public async Task Save_AnswerOrder_ItemNotFound_Then403_Then400_Then409()
    {
        var world = await CreateWorldAsync(factory);
        var story = await StoryAsync(world);
        var reader = await MembershipTestData.NewMemberAsync(factory, world.OrganizationId, OrganizationRole.Viewer);
        var otherProjectTeam = TeamBody(world.OtherProjectTeam);

        var noItem = await SaveAsync(world, "ALP-US-9999", otherProjectTeam);
        Assert.Equal((HttpStatusCode.NotFound, "workitems.workitem.not_found"), (noItem.Status, noItem.ErrorCode));

        var readerNoItem = await SaveAsync(world, "ALP-US-9999", otherProjectTeam, reader.Token);
        Assert.Equal((HttpStatusCode.Forbidden, "organizations.access.forbidden"), (readerNoItem.Status, readerNoItem.ErrorCode));

        var badTitle = await SaveAsync(world, story.Identifier, new { title = string.Empty, team = new { teamId = world.OtherProjectTeam.ToString() } });
        Assert.Equal((HttpStatusCode.BadRequest, "workitems.validation_failed"), (badTitle.Status, badTitle.ErrorCode));


        var badParent = await SaveAsync(
            world,
            story.Identifier,
            new { parent = new { parentId = Guid.CreateVersion7().ToString() }, team = new { teamId = world.OtherProjectTeam.ToString() } });
        Assert.Equal((HttpStatusCode.Conflict, TeamNotInProject), (badParent.Status, badParent.ErrorCode));
    }

    // US-1 · "a child may have another team" (BR-OT-1): a parent's team and its child's team are independent; neither
    // follows the other.
    [Fact]
    public async Task Save_ChildAndParentTeams_AreIndependent()
    {
        var world = await CreateWorldAsync(factory);
        var feature = await CreateAsync(factory, world.Items, "FEAT", "Вход");
        var childAnswer = await OrganizationsApi.SendAsync(
            factory,
            HttpMethod.Post,
            WorkItemsTestData.List(world.Items),
            new { requestId = Guid.NewGuid().ToString(), typeId = world.Items.Types["US"].Id.ToString(), title = "Ссылка", parentId = feature.Id.ToString() },
            OrganizationsTokens.SignedIn());
        Assert.Equal(HttpStatusCode.Created, childAnswer.Status);
        var child = childAnswer.Body.GetProperty("identifier").GetString()!;

        await SaveAsync(world, feature.Identifier, TeamBody(world.Mars));
        await SaveAsync(world, child, TeamBody(world.Venus));

        Assert.Equal(world.Mars, (await StoredAsync(factory, world.ProjectId, feature.Identifier)).TeamId);
        Assert.Equal(world.Venus, (await StoredAsync(factory, world.ProjectId, child)).TeamId);

        await SaveAsync(world, feature.Identifier, TeamBody(null));

        Assert.Equal(world.Venus, (await StoredAsync(factory, world.ProjectId, child)).TeamId);
    }

    // US-1 · "the assignee need not belong to the team" (BR-OT-1): an assignee outside the team is accepted, and a team change
    // keeps the assignee.
    [Fact]
    public async Task Save_AssigneeOutsideTheTeam_IsAccepted_AndTeamChangeKeepsTheAssignee()
    {
        var world = await CreateWorldAsync(factory);
        var story = await StoryAsync(world);
        var person = await WorkItemsTestData.AddMemberAsync(factory, world.Items, "Борис", "Петров");
        await SaveAsync(world, story.Identifier, TeamBody(world.Mars));

        var assigned = await SaveAsync(world, story.Identifier, new { assignee = new { userId = person } });
        Assert.Equal(HttpStatusCode.OK, assigned.Status);

        var moved = await SaveAsync(world, story.Identifier, TeamBody(world.Venus));
        Assert.Equal(HttpStatusCode.OK, moved.Status);
        Assert.Equal(person, moved.Body.GetProperty("assignee").GetProperty("id").GetString());
        Assert.Equal(person, (await StoredAsync(factory, world.ProjectId, story.Identifier)).AssigneeId);
    }


    // US-1 · "the team survives every other write": a state set by the form and by a card move (both are a save of the state)
    // leave the team, and so does a tick of the card's checklist (feat-039: the tick is the same save of the child's state), which
    // also leaves the child's team its own.
    [Fact]
    public async Task Save_StateByFormAndCardMoveAndChecklistTick_KeepTheTeams()
    {
        var world = await CreateWorldAsync(factory);
        var story = await StoryAsync(world);
        await SetOwningTeamAsync(factory, story.Id, world.Mars);
        var type = world.Items.Types["US"];

        var childAnswer = await OrganizationsApi.SendAsync(
            factory,
            HttpMethod.Post,
            WorkItemsTestData.List(world.Items),
            new { requestId = Guid.NewGuid().ToString(), typeId = world.Items.Types["TASK"].Id.ToString(), title = "Письмо", parentId = story.Id.ToString() },
            OrganizationsTokens.SignedIn());
        Assert.Equal(HttpStatusCode.Created, childAnswer.Status);
        var child = (Id: childAnswer.Body.GetProperty("id").GetGuid(), Identifier: childAnswer.Body.GetProperty("identifier").GetString()!);
        await SetOwningTeamAsync(factory, child.Id, world.Venus);

        var active = await SaveAsync(world, story.Identifier, new { stateId = type.StateId(StateCategory.InProgress).ToString() });
        Assert.Equal(HttpStatusCode.OK, active.Status);
        Assert.Equal((world.Mars, "MARS"), TeamOf(active.Body));

        var done = await SaveAsync(world, story.Identifier, new { stateId = type.StateId(StateCategory.Completed).ToString() });
        Assert.Equal(HttpStatusCode.OK, done.Status);
        Assert.Equal(world.Mars, (await StoredAsync(factory, world.ProjectId, story.Identifier)).TeamId);

        var tick = await SaveAsync(world, child.Identifier, new { stateId = world.Items.Types["TASK"].StateId(StateCategory.Completed).ToString() });
        Assert.Equal(HttpStatusCode.OK, tick.Status);
        Assert.Equal((world.Venus, "VENUS"), TeamOf(tick.Body));
        Assert.Equal(world.Venus, (await StoredAsync(factory, world.ProjectId, child.Identifier)).TeamId);
        Assert.Equal(world.Mars, (await StoredAsync(factory, world.ProjectId, story.Identifier)).TeamId);
    }

    // BR-OT-2 · the team is judged with the other values in one save: a refused state leaves the team as it was, and a refused
    // team leaves the state — all or nothing (BR-M3-25).
    [Fact]
    public async Task Save_TeamAndRefusedState_StoreNothing()
    {
        var world = await CreateWorldAsync(factory);
        var story = await StoryAsync(world);
        var before = await StoredAsync(factory, world.ProjectId, story.Identifier);

        var answer = await SaveAsync(
            world, story.Identifier, new { stateId = Guid.CreateVersion7().ToString(), team = new { teamId = world.Mars.ToString() } });

        Assert.Equal((HttpStatusCode.Conflict, "workitems.workitem.state_not_allowed"), (answer.Status, answer.ErrorCode));
        Assert.Equal(before, await StoredAsync(factory, world.ProjectId, story.Identifier));
    }
}
