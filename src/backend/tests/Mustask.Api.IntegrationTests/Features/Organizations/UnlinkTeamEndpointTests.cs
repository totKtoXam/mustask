using System.Net;
using Mustask.Api.IntegrationTests.Features.Boards;
using Mustask.Api.IntegrationTests.Features.WorkItems;
using Mustask.Domain.Identity;
using Mustask.Domain.Organizations;
using static Mustask.Api.IntegrationTests.Features.Organizations.TeamTestData;
using static Mustask.Api.IntegrationTests.Features.WorkItems.OwningTeamTestData;

namespace Mustask.Api.IntegrationTests.Features.Organizations;

/// <summary>
/// <c>POST …/projects/{projectId}/teams/{teamId}/unlink</c> — take a team off a project (feat-028 api-contract § 2.4, SC-09; US-4 "the
/// dialog states the items and one destination", "confirming with another team", "confirming with «Без команды»", "a bad destination",
/// "linking again moves nothing back") through the real host and a real PostgreSQL. The items of the project that the team owns go to the
/// destination or to nobody, their places on the team's backlog are dropped, the setting that named the team is cleared, the link is
/// removed — in one operation. Every test builds its own world (<see cref="OwningTeamTestData"/>).
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class UnlinkTeamEndpointTests(MustaskApiFactory factory)
{
    private const string TeamNotFound = "organizations.team.not_found";
    private const string TeamNotInProject = "organizations.team.not_in_project";

    private Task<Answer> UnlinkAsync(OwningTeamWorld world, Guid teamId, object? body, string? token = null) =>
        TeamTestData.UnlinkAsync(factory, world.OrganizationId, world.ProjectId, teamId, body, token);

    private Task<Answer> GetAsync(string path) =>
        OrganizationsApi.SendAsync(factory, HttpMethod.Get, path, token: OrganizationsTokens.SignedIn());

    private string TeamBacklog(OwningTeamWorld world, Guid teamId) =>
        $"{TeamTestData.Team(world.OrganizationId, teamId)}/backlogs/stories";

    private async Task<Item> StoryAsync(OwningTeamWorld world, string title, Guid? team, Guid? projectId = null, string? key = null)
    {
        var items = projectId is null ? world.Items : world.Items with { ProjectId = projectId.Value, Key = key! };
        var answer = await OrganizationsApi.SendAsync(
            factory,
            HttpMethod.Post,
            WorkItemsTestData.List(items),
            new
            {
                requestId = Guid.NewGuid().ToString(),
                typeId = world.Items.Types["US"].Id.ToString(),
                title,
                team = new { teamId = team?.ToString() },
            },
            OrganizationsTokens.SignedIn());
        Assert.Equal(HttpStatusCode.Created, answer.Status);

        return new Item(answer.Body.GetProperty("id").GetGuid(), answer.Body.GetProperty("identifier").GetString()!);
    }

    private static string[] Rows(Answer answer) =>
        answer.Body.GetProperty("items").EnumerateArray().Select(row => row.GetProperty("identifier").GetString()!).ToArray();

    private Guid Level(OwningTeamWorld world) => world.Items.Types["US"].BacklogLevelId;

    /// <summary>Everything an unlink that changed nothing must leave: the items' owners, the setting, the links of the project.</summary>
    private async Task<string> StateAsync(OwningTeamWorld world) =>
        $"{await TeamStateAsync(factory, world.ProjectId)} | {string.Join(",", (await LinksOfProjectAsync(factory, world.ProjectId)).Select(link => link.TeamId))}";

    // feat-028 US-4 · "Unlink_ToATeam_ReassignsOnce_UnplacesBelowTheDestinationsPlaced_KeepsEverythingElse": MARS is taken off ALP naming
    // VENUS. Its two placed stories become VENUS's, with no place on any team backlog, so on VENUS's backlog they stand below the story
    // VENUS already had placed; the area, the assignee, the state and the project backlog place are untouched; «Изменён» shows the
    // operation's time on the two, and on nothing else.
    [Fact]
    public async Task Unlink_ToATeam_ReassignsOnce_UnplacesBelowTheDestinationsPlaced_KeepsEverythingElse()
    {
        var world = await CreateWorldAsync(factory);
        var first = await StoryAsync(world, "Первая", world.Mars);
        var second = await StoryAsync(world, "Вторая", world.Mars);
        var venus = await StoryAsync(world, "Венерина", world.Venus);
        await PlaceAsync(factory, first.Id, world.Mars, Level(world), 1);
        await PlaceAsync(factory, second.Id, world.Mars, Level(world), 2);
        await PlaceAsync(factory, venus.Id, world.Venus, Level(world), 1);
        var firstBefore = await StoredAsync(factory, world.ProjectId, first.Identifier);
        var venusBefore = await StoredAsync(factory, world.ProjectId, venus.Identifier);
        var projectPlaces = (await ProjectPlacesOfAsync(factory, first.Id), await ProjectPlacesOfAsync(factory, second.Id));

        var started = DateTimeOffset.UtcNow;
        var answer = await UnlinkAsync(world, world.Mars, To(world.Venus));
        var answered = DateTimeOffset.UtcNow;

        Assert.Equal(HttpStatusCode.NoContent, answer.Status);
        Assert.Null(answer.MediaType);
        foreach (var item in new[] { first, second })
        {
            var stored = await StoredAsync(factory, world.ProjectId, item.Identifier);
            Assert.Equal(world.Venus, stored.TeamId);
            Assert.InRange(stored.ChangedAt, started, answered);
            Assert.Equal(0, await TeamPlacesOfAsync(factory, item.Id));
        }

        var firstAfter = await StoredAsync(factory, world.ProjectId, first.Identifier);
        Assert.Equal((firstBefore.AreaId, firstBefore.AssigneeId, firstBefore.StateId), (firstAfter.AreaId, firstAfter.AssigneeId, firstAfter.StateId));
        Assert.Equal((projectPlaces.Item1, projectPlaces.Item2), (await ProjectPlacesOfAsync(factory, first.Id), await ProjectPlacesOfAsync(factory, second.Id)));
        Assert.Equal(venusBefore, await StoredAsync(factory, world.ProjectId, venus.Identifier));
        Assert.Equal(1, await TeamPlacesOfAsync(factory, venus.Id));

        var backlog = Rows(await GetAsync(TeamBacklog(world, world.Venus)));
        Assert.Equal(venus.Identifier, backlog[0]);
        Assert.Equal(new[] { first.Identifier, second.Identifier }.Order(), backlog[1..].Order());
        Assert.Equal(0, await PlacesNamingAnotherTeamAsync(factory, world.ProjectId));
    }

    // feat-028 US-4 · "…_TouchesNoOtherProject": MARS is linked to BET as well and owns a placed story there, with BET naming MARS as its
    // default; unlinking it from ALP moves nothing there — the story, its place, the setting and the link stay.
    [Fact]
    public async Task Unlink_TouchesNoOtherProject()
    {
        var world = await CreateWorldAsync(factory);
        await OrganizationsTestData.LinkAsync(factory, world.OtherProjectId, world.Mars);
        var alpha = await StoryAsync(world, "Альфа", world.Mars);
        var beta = await StoryAsync(world, "Бета", world.Mars, world.OtherProjectId, "BET");
        await PlaceAsync(factory, beta.Id, world.Mars, Level(world), 1);
        await WorkItemsTestData.ExecuteAsync(factory, $"UPDATE projects SET default_team_id = {world.Mars} WHERE id = {world.OtherProjectId}");
        var betaBefore = await StoredAsync(factory, world.OtherProjectId, beta.Identifier);

        var answer = await UnlinkAsync(world, world.Mars, To(null));

        Assert.Equal(HttpStatusCode.NoContent, answer.Status);
        Assert.Null((await StoredAsync(factory, world.ProjectId, alpha.Identifier)).TeamId);
        Assert.Equal(betaBefore, await StoredAsync(factory, world.OtherProjectId, beta.Identifier));
        Assert.Equal(1, await TeamPlacesOfAsync(factory, beta.Id));
        Assert.Equal(world.Mars, await SettingAsync(factory, world.OtherProjectId));
        Assert.Equal([world.OtherProjectId], (await LinksOfTeamAsync(factory, world.Mars)).Select(link => link.ProjectId));
    }

    // feat-028 US-4 · "…_ClearsTheNamedDefault" and "the default is each project's own": the setting that named the unlinked team is
    // cleared; a setting that names another team stays.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Unlink_ClearsTheNamedDefault_AnotherTeamKeepsIt(bool namesTheUnlinkedTeam)
    {
        var world = await CreateWorldAsync(factory);
        var named = namesTheUnlinkedTeam ? world.Mars : world.Venus;
        await WorkItemsTestData.ExecuteAsync(factory, $"UPDATE projects SET default_team_id = {named} WHERE id = {world.ProjectId}");

        var answer = await UnlinkAsync(world, world.Mars, To(world.OwnTeam));

        Assert.Equal(HttpStatusCode.NoContent, answer.Status);
        Assert.Equal(namesTheUnlinkedTeam ? null : world.Venus, await SettingAsync(factory, world.ProjectId));
    }

    // feat-028 US-4 · "…_ChangedAtIsTheOperationsTime" and "nothing but the team's items moves": the stories of VENUS and of nobody keep
    // their owner and their «Изменён».
    [Fact]
    public async Task Unlink_TouchesNoOtherTeamsItems_NorTheItemsOfNobody()
    {
        var world = await CreateWorldAsync(factory);
        var venus = await StoryAsync(world, "Венерина", world.Venus);
        var nobody = await StoryAsync(world, "Ничья", null);
        await StoryAsync(world, "Марсова", world.Mars);
        var venusBefore = await StoredAsync(factory, world.ProjectId, venus.Identifier);
        var nobodyBefore = await StoredAsync(factory, world.ProjectId, nobody.Identifier);

        var answer = await UnlinkAsync(world, world.Mars, To(world.Venus));

        Assert.Equal(HttpStatusCode.NoContent, answer.Status);
        Assert.Equal(venusBefore, await StoredAsync(factory, world.ProjectId, venus.Identifier));
        Assert.Equal(nobodyBefore, await StoredAsync(factory, world.ProjectId, nobody.Identifier));
    }

    // feat-028 US-4 · "…_RemovesTheLink_ViewsAnswer404": after the unlink the team is not in the project's list, the count answers
    // team.not_found, and it can be neither named the default nor given to an item. feat-040 (BR-TS-1, BR-TS-2) rewrites the views' half:
    // the team's views are at the team's own address and the team is still a team — it answers 200 and, linked to no project now, holds
    // nothing (its one item went to «Без команды»); the address of the project's old team views is no route at all.
    [Fact]
    public async Task Unlink_RemovesTheLink_ViewsCountAndNamingAnswerAsNotLinked()
    {
        var world = await CreateWorldAsync(factory);
        var story = await StoryAsync(world, "Марсова", world.Mars);
        var before = await GetAsync(TeamBacklog(world, world.Mars));
        Assert.Equal(HttpStatusCode.OK, before.Status);
        Assert.Equal(
            [story.Identifier],
            before.Body.GetProperty("items").EnumerateArray().Select(row => row.GetProperty("identifier").GetString()));

        var answer = await UnlinkAsync(world, world.Mars, To(null));
        var backlog = await GetAsync(TeamBacklog(world, world.Mars));
        var count = await GetAsync($"{ProjectTeam(world.OrganizationId, world.ProjectId, world.Mars)}/unlink-count");
        var list = await GetAsync(OrganizationsApi.Teams(world.OrganizationId, world.ProjectId));
        var naming = await OrganizationsApi.SendAsync(
            factory,
            HttpMethod.Put,
            $"{OrganizationsApi.Project(world.OrganizationId, world.ProjectId)}/default-team",
            new { teamId = world.Mars.ToString() },
            OrganizationsTokens.SignedIn());
        var save = await WorkItemsTestData.SaveAsync(factory, world.Items, story.Identifier, new { team = new { teamId = world.Mars.ToString() } });

        Assert.Equal(HttpStatusCode.NoContent, answer.Status);
        Assert.Equal(HttpStatusCode.OK, backlog.Status);
        Assert.Empty(backlog.Body.GetProperty("items").EnumerateArray());
        Assert.Empty(backlog.Body.GetProperty("projects").EnumerateArray());
        Assert.Equal((HttpStatusCode.NotFound, TeamNotFound), (count.Status, count.ErrorCode));
        Assert.DoesNotContain(world.Mars, Ids(list.Body));
        Assert.Equal((HttpStatusCode.Conflict, TeamNotInProject), (naming.Status, naming.ErrorCode));
        Assert.Equal((HttpStatusCode.Conflict, TeamNotInProject), (save.Status, save.ErrorCode));
        Assert.DoesNotContain(world.Mars, (await LinksOfProjectAsync(factory, world.ProjectId)).Select(link => link.TeamId));
        Assert.NotEmpty(await LinksOfTeamAsync(factory, world.Venus));
    }

    // feat-028 US-4 · "…_LinkAgain_MovesNothingBack": after the unlink, linking MARS to the project again hands it nothing: the stories stay
    // where they went and it owns nothing.
    [Fact]
    public async Task LinkAgain_MovesNothingBack()
    {
        var world = await CreateWorldAsync(factory);
        var story = await StoryAsync(world, "Марсова", world.Mars);
        await UnlinkAsync(world, world.Mars, To(world.Venus));

        var relink = await LinkTeamsAsync(factory, world.OrganizationId, world.ProjectId, new { teamIds = new[] { world.Mars.ToString() } });

        Assert.Equal(HttpStatusCode.NoContent, relink.Status);
        Assert.Equal(world.Venus, (await StoredAsync(factory, world.ProjectId, story.Identifier)).TeamId);
        Assert.Equal(0, await OwnedItemCountAsync(factory, world.Mars));
        Assert.Empty(Rows(await GetAsync(TeamBacklog(world, world.Mars))));
        Assert.Null(await SettingAsync(factory, world.ProjectId));
    }

    // feat-028 US-4 · "Unlink_ToNone_OnNoTeamBacklog_StaysOnTheProjectBacklog": with «Без команды» the stories are on no team backlog and
    // stay on the project's backlog.
    [Fact]
    public async Task Unlink_ToNone_OnNoTeamBacklog_StaysOnTheProjectBacklog()
    {
        var world = await CreateWorldAsync(factory);
        var story = await StoryAsync(world, "Марсова", world.Mars);
        await PlaceAsync(factory, story.Id, world.Mars, Level(world), 1);
        var projectPlace = await ProjectPlacesOfAsync(factory, story.Id);

        var answer = await UnlinkAsync(world, world.Mars, To(null));

        Assert.Equal(HttpStatusCode.NoContent, answer.Status);
        Assert.Null((await StoredAsync(factory, world.ProjectId, story.Identifier)).TeamId);
        Assert.Equal(0, await TeamPlacesOfAsync(factory, story.Id));
        Assert.Equal(projectPlace, await ProjectPlacesOfAsync(factory, story.Id));
        foreach (var team in new[] { world.Venus, world.OwnTeam })
        {
            Assert.DoesNotContain(story.Identifier, Rows(await GetAsync(TeamBacklog(world, team))));
        }

        var projectBacklog = await GetAsync($"{OrganizationsApi.Project(world.OrganizationId, world.ProjectId)}/backlogs/stories");
        Assert.Contains(story.Identifier, projectBacklog.Body.GetProperty("items").EnumerateArray().Select(row => row.GetProperty("identifier").GetString()));
    }

    // feat-028 US-4 · "Unlink_TheOnlyTeam_ToNone_LeavesTheProjectTeamless": the project's only team goes with «Без команды»; the project's
    // list is empty, it has no default, and a new story is created with no team.
    [Fact]
    public async Task Unlink_TheOnlyTeam_ToNone_LeavesTheProjectTeamless()
    {
        var world = await CreateWorldAsync(factory);
        var single = await OrganizationsTestData.CreateBareProjectAsync(factory, world.OrganizationId, "Одна", "ONE");
        await OrganizationsTestData.LinkAsync(factory, single.Project.Id, world.Mars);
        var story = await StoryAsync(world, "Марсова", world.Mars, single.Project.Id, "ONE");
        Assert.Equal(world.Mars, await DefaultTeamAsync(factory, single.Project.Id));

        var answer = await TeamTestData.UnlinkAsync(factory, world.OrganizationId, single.Project.Id, world.Mars, To(null));
        var list = await GetAsync(OrganizationsApi.Teams(world.OrganizationId, single.Project.Id));
        var created = await StoryAsync(world, "Новая", null, single.Project.Id, "ONE");

        Assert.Equal(HttpStatusCode.NoContent, answer.Status);
        Assert.Empty(list.Body.EnumerateArray());
        Assert.Null(await DefaultTeamAsync(factory, single.Project.Id));
        Assert.Null((await StoredAsync(factory, single.Project.Id, story.Identifier)).TeamId);
        Assert.Null((await StoredAsync(factory, single.Project.Id, created.Identifier)).TeamId);
    }

    // feat-028 US-4 · "Unlink_OwningNothing_RemovesLink_ClearsDefault": a team that owns nothing here is unlinked all the same: the link
    // goes, the setting that named it goes, no item changes.
    [Fact]
    public async Task Unlink_OwningNothing_RemovesLink_ClearsDefault()
    {
        var world = await CreateWorldAsync(factory);
        var other = await StoryAsync(world, "Венерина", world.Venus);
        await WorkItemsTestData.ExecuteAsync(factory, $"UPDATE projects SET default_team_id = {world.Mars} WHERE id = {world.ProjectId}");
        var otherBefore = await StoredAsync(factory, world.ProjectId, other.Identifier);

        var answer = await UnlinkAsync(world, world.Mars, To(world.Venus));

        Assert.Equal(HttpStatusCode.NoContent, answer.Status);
        Assert.Null(await SettingAsync(factory, world.ProjectId));
        Assert.DoesNotContain(world.Mars, (await LinksOfProjectAsync(factory, world.ProjectId)).Select(link => link.TeamId));
        Assert.Equal(otherBefore, await StoredAsync(factory, world.ProjectId, other.Identifier));
    }

    // feat-028 US-4 · "…_ItemGivenAfterTheCount_GoesToTheNamedDestination": the dialog's count was read, then a story was given to the
    // team; the unlink reassigns both — the count is of the moment it was read, the operation is of the moment it runs.
    [Fact]
    public async Task Unlink_ItemGivenAfterTheCount_GoesToTheNamedDestination()
    {
        var world = await CreateWorldAsync(factory);
        await StoryAsync(world, "Раньше", world.Mars);
        var count = await GetAsync($"{ProjectTeam(world.OrganizationId, world.ProjectId, world.Mars)}/unlink-count");
        var later = await StoryAsync(world, "Позже", world.Mars);

        var answer = await UnlinkAsync(world, world.Mars, To(world.Venus));

        Assert.Equal(1, count.Body.GetProperty("count").GetInt32());
        Assert.Equal(HttpStatusCode.NoContent, answer.Status);
        Assert.Equal(world.Venus, (await StoredAsync(factory, world.ProjectId, later.Identifier)).TeamId);
        Assert.Equal(0, await OwnedItemCountAsync(factory, world.Mars));
    }

    // feat-028 US-4 · "Unlink_DestinationRefusals_ChangeNothing" (6 rows): the unlinked team itself and an active team not linked to the
    // project are 409 not_in_project; a deleted, an unknown, another organization's and a malformed destination are 404 team.not_found;
    // nothing changes.
    [Theory]
    [InlineData("itself", HttpStatusCode.Conflict, TeamNotInProject)]
    [InlineData("linked to another project", HttpStatusCode.Conflict, TeamNotInProject)]
    [InlineData("linked to none", HttpStatusCode.Conflict, TeamNotInProject)]
    [InlineData("of a deactivated project", HttpStatusCode.Conflict, TeamNotInProject)]
    [InlineData("deleted", HttpStatusCode.NotFound, TeamNotFound)]
    [InlineData("unknown", HttpStatusCode.NotFound, TeamNotFound)]
    [InlineData("foreign", HttpStatusCode.NotFound, TeamNotFound)]
    [InlineData("malformed", HttpStatusCode.NotFound, TeamNotFound)]
    public async Task Unlink_DestinationRefusals_ChangeNothing(string which, HttpStatusCode status, string code)
    {
        var world = await CreateWorldAsync(factory);
        await StoryAsync(world, "Марсова", world.Mars);
        await WorkItemsTestData.ExecuteAsync(factory, $"UPDATE projects SET default_team_id = {world.Mars} WHERE id = {world.ProjectId}");
        var unlinked = await OrganizationsTestData.AddUnlinkedTeamAsync(factory, world.OrganizationId, "Ничья");
        var destination = which switch
        {
            "itself" => world.Mars.ToString(),
            "linked to another project" => world.OtherProjectTeam.ToString(),
            "linked to none" => unlinked.Id.ToString(),
            "of a deactivated project" => world.DeactivatedProjectTeam.ToString(),
            "deleted" => world.DeletedTeam.ToString(),
            "foreign" => world.OtherOrganizationTeam.ToString(),
            "unknown" => Guid.CreateVersion7().ToString(),
            _ => "not-an-id",
        };
        var before = await StateAsync(world);

        var answer = await UnlinkAsync(world, world.Mars, new { destination = new { teamId = destination } });

        Assert.Equal((status, code), (answer.Status, answer.ErrorCode));
        Assert.Equal(before, await StateAsync(world));
        Assert.Equal(1, await OwnedItemCountAsync(factory, world.Mars));
    }

    // feat-028 D-8 · "the destination object and its teamId key must be present": an absent body, `{}`, `{ "destination": null }` and
    // `{ "destination": {} }` are 400 with the reason under `destination`; nothing changes. «Без команды» is `{ "teamId": null }`.
    [Theory]
    [InlineData("no body")]
    [InlineData("empty object")]
    [InlineData("null destination")]
    [InlineData("empty destination")]
    public async Task Unlink_DestinationAbsent_Returns400DestinationRequired_ChangesNothing(string how)
    {
        var world = await CreateWorldAsync(factory);
        await StoryAsync(world, "Марсова", world.Mars);
        object? body = how switch
        {
            "empty object" => new { },
            "null destination" => new { destination = (object?)null },
            "empty destination" => new { destination = new { } },
            _ => null,
        };
        var before = await StateAsync(world);

        var answer = await UnlinkAsync(world, world.Mars, body);

        Assert.Equal(HttpStatusCode.BadRequest, answer.Status);
        Assert.Equal("organizations.validation_failed", answer.ErrorCode);
        Assert.Equal("organizations.team.destination_required", ReasonOf(answer, "destination"));
        Assert.Equal("Выберите команду или «Без команды»", answer.Body.GetProperty("errors").GetProperty("destination")[0].GetProperty("detail").GetString());
        Assert.Equal(before, await StateAsync(world));
    }

    // feat-028 BR-OL-4 · "Unlink_NotLinked_Returns204_NamingAnyDestination": a pair that is not linked is a success that changes nothing,
    // whatever active team the destination names — the team itself included.
    [Theory]
    [InlineData("a team of the project")]
    [InlineData("the team itself")]
    [InlineData("none")]
    public async Task Unlink_NotLinked_Returns204_NamingAnyDestination_ChangesNothing(string destination)
    {
        var world = await CreateWorldAsync(factory);
        await StoryAsync(world, "Венерина", world.Venus);
        var before = await StateAsync(world);
        Guid? named = destination switch
        {
            "a team of the project" => world.Venus,
            "the team itself" => world.OtherProjectTeam,
            _ => null,
        };

        var answer = await UnlinkAsync(world, world.OtherProjectTeam, To(named));

        Assert.Equal(HttpStatusCode.NoContent, answer.Status);
        Assert.Equal(before, await StateAsync(world));
        Assert.Equal([world.OtherProjectId], (await LinksOfTeamAsync(factory, world.OtherProjectTeam)).Select(link => link.ProjectId));
    }

    // feat-028 BR-OL-4 · unlinking the same pair twice: the second is the success of an unlinked pair.
    [Fact]
    public async Task Unlink_Twice_TheSecondIsANoOp()
    {
        var world = await CreateWorldAsync(factory);
        await StoryAsync(world, "Марсова", world.Mars);
        var first = await UnlinkAsync(world, world.Mars, To(world.Venus));
        var stateAfterFirst = await StateAsync(world);

        var second = await UnlinkAsync(world, world.Mars, To(world.OwnTeam));

        Assert.Equal((HttpStatusCode.NoContent, HttpStatusCode.NoContent), (first.Status, second.Status));
        Assert.Equal(stateAfterFirst, await StateAsync(world));
    }

    // feat-028 US-4 · "Unlink_FromADeactivatedProject_Returns404Project_ChangesNothing": a deactivated project answers project.not_found.
    [Fact]
    public async Task Unlink_FromADeactivatedProject_Returns404Project_ChangesNothing()
    {
        var world = await CreateWorldAsync(factory);
        await StoryAsync(world, "Марсова", world.Mars);
        await OrganizationsTestData.SetProjectStatusAsync(factory, world.OrganizationId, world.ProjectId, ProjectStatus.Deactivated);
        var before = await StateAsync(world);

        var answer = await UnlinkAsync(world, world.Mars, To(world.Venus));

        Assert.Equal((HttpStatusCode.NotFound, "organizations.project.not_found"), (answer.Status, answer.ErrorCode));
        Assert.Equal(before, await StateAsync(world));
    }

    // feat-028 § 0.5 · the address from the outside in: a project that is malformed, unknown or of another organization answers
    // project.not_found before the team, and a team that is not an active team of the organization answers team.not_found before the
    // destination is looked at (the destination is wrong too).
    [Theory]
    [InlineData("project malformed")]
    [InlineData("project unknown")]
    [InlineData("project of another organization")]
    [InlineData("team malformed")]
    [InlineData("team unknown")]
    [InlineData("team deleted")]
    [InlineData("team foreign")]
    public async Task Unlink_AddressFromTheOutsideIn_ProjectThenTeam_BeforeTheDestination(string how)
    {
        var world = await CreateWorldAsync(factory);
        var path = how.StartsWith("project", StringComparison.Ordinal)
            ? how switch
            {
                "project malformed" => $"/api/organizations/{world.OrganizationId}/projects/ALP/teams/{world.Mars}/unlink",
                "project unknown" => $"/api/organizations/{world.OrganizationId}/projects/{Guid.CreateVersion7()}/teams/{world.Mars}/unlink",
                _ => $"/api/organizations/{world.OrganizationId}/projects/{world.DeactivatedProjectTeam}/teams/{world.Mars}/unlink",
            }
            : $"{OrganizationsApi.Teams(world.OrganizationId, world.ProjectId)}/{how switch
            {
                "team malformed" => "not-a-team",
                "team unknown" => Guid.CreateVersion7().ToString(),
                "team deleted" => world.DeletedTeam.ToString(),
                _ => world.OtherOrganizationTeam.ToString(),
            }}/unlink";
        var before = await StateAsync(world);

        var answer = await OrganizationsApi.SendAsync(
            factory, HttpMethod.Post, path, new { destination = new { teamId = "not-a-team" } }, OrganizationsTokens.SignedIn());

        var expected = how.StartsWith("project", StringComparison.Ordinal) ? "organizations.project.not_found" : TeamNotFound;
        Assert.Equal((HttpStatusCode.NotFound, expected), (answer.Status, answer.ErrorCode));
        Assert.Equal(before, await StateAsync(world));
    }

    // feat-028 US-4 · "only a manager of both ends": organizations.project.link is the administrator's alone; the 403 comes before the
    // address and the body; a non-member gets the organization's 404; no token gets 401.
    [Theory]
    [InlineData(OrganizationRole.Member)]
    [InlineData(OrganizationRole.Viewer)]
    public async Task Unlink_MemberAndViewer_Return403_ChangeNothing(OrganizationRole role)
    {
        var world = await CreateWorldAsync(factory);
        await StoryAsync(world, "Марсова", world.Mars);
        var caller = await MembershipTestData.NewMemberAsync(factory, world.OrganizationId, role);
        var before = await StateAsync(world);

        var real = await UnlinkAsync(world, world.Mars, To(world.Venus), caller.Token);
        var noBody = await UnlinkAsync(world, Guid.CreateVersion7(), null, caller.Token);

        Assert.Equal((HttpStatusCode.Forbidden, "organizations.access.forbidden"), (real.Status, real.ErrorCode));
        Assert.Equal((HttpStatusCode.Forbidden, "organizations.access.forbidden"), (noBody.Status, noBody.ErrorCode));
        Assert.Equal(before, await StateAsync(world));
    }

    [Fact]
    public async Task Unlink_NonMember_Returns404Organization_NoToken_Returns401()
    {
        var world = await CreateWorldAsync(factory);
        var before = await StateAsync(world);

        var stranger = await UnlinkAsync(world, world.Mars, To(world.Venus), OrganizationsTokens.Stranger());
        var anonymous = await OrganizationsApi.SendAsync(
            factory, HttpMethod.Post, $"{ProjectTeam(world.OrganizationId, world.ProjectId, world.Mars)}/unlink", To(world.Venus));

        Assert.Equal((HttpStatusCode.NotFound, "organizations.organization.not_found"), (stranger.Status, stranger.ErrorCode));
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.Status);
        Assert.Equal(before, await StateAsync(world));
    }

    // feat-028 D-8 · a destination wrapper whose teamId is a JSON number or object is not an id: 400 http.bad_request (api-contract § 0.2).
    [Fact]
    public async Task Unlink_DestinationTeamIdOfTheWrongJsonShape_Returns400BadRequest_ChangesNothing()
    {
        var world = await CreateWorldAsync(factory);
        await StoryAsync(world, "Марсова", world.Mars);
        var before = await StateAsync(world);

        var answer = await UnlinkAsync(world, world.Mars, new { destination = new { teamId = 42 } });

        Assert.Equal((HttpStatusCode.BadRequest, "http.bad_request"), (answer.Status, answer.ErrorCode));
        Assert.Equal(before, await StateAsync(world));
    }
}
