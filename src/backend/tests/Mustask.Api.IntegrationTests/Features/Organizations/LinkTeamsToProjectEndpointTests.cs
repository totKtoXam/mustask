using System.Net;
using Mustask.Domain.Identity;
using Mustask.Domain.Organizations;
using static Mustask.Api.IntegrationTests.Features.Organizations.TeamTestData;

namespace Mustask.Api.IntegrationTests.Features.Organizations;

/// <summary>
/// <c>POST …/projects/{projectId}/teams</c> — link teams to the project (feat-028 api-contract § 2.4; US-3 "a link from either page",
/// "only active ends are linked"; BR-OL-4, BR-OL-7) through the real host and a real PostgreSQL. The path is the old "add a team" route
/// with a new meaning: an old body <c>{ "name" }</c> reads as an empty list (D-14). Every test builds its own organization.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class LinkTeamsToProjectEndpointTests(MustaskApiFactory factory)
{
    private Task<Answer> LinkAsync(Guid organizationId, string projectId, object? body, string? token = null) =>
        OrganizationsApi.SendAsync(
            factory,
            HttpMethod.Post,
            $"/api/organizations/{organizationId}/projects/{Uri.EscapeDataString(projectId)}/teams",
            body,
            token ?? OrganizationsTokens.SignedIn());

    private Task<Answer> LinkAsync(Guid organizationId, Guid projectId, params Guid[] teamIds) =>
        LinkAsync(organizationId, projectId.ToString(), new { teamIds = teamIds.Select(id => id.ToString()) });

    private async Task<IReadOnlyList<(Guid TeamId, string LinkedBy, DateTimeOffset LinkedAt)>> LinksAsync(Guid projectId) =>
        (await LinksOfProjectAsync(factory, projectId)).Select(link => (link.TeamId, link.LinkedBy, link.LinkedAt)).ToList();

    // feat-028 US-3 · "Link_FromTheProjectsPage_RecordsTheCallerAndTheTime_BothListsShowIt": two teams are linked in one request, each link
    // records the caller and the time of the request, and both ends list it — the project's teams, the organization's list, the team's page.
    [Fact]
    public async Task Link_FromTheProjectsPage_RecordsTheCallerAndTheTime_BothListsShowIt()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var project = await OrganizationsTestData.CreateBareProjectAsync(factory, organization.Id, "Альфа", "ALP");
        var first = await OrganizationsTestData.AddUnlinkedTeamAsync(factory, organization.Id, "Первая");
        var second = await OrganizationsTestData.AddUnlinkedTeamAsync(factory, organization.Id, "Вторая");

        var started = DateTimeOffset.UtcNow;
        var answer = await LinkAsync(organization.Id, project.Id, first.Id, second.Id);
        var answered = DateTimeOffset.UtcNow;

        Assert.Equal(HttpStatusCode.NoContent, answer.Status);
        Assert.Null(answer.MediaType);
        var links = await LinksAsync(project.Id);
        Assert.Equal(new[] { first.Id, second.Id }.Order(), links.Select(link => link.TeamId));
        Assert.All(links, link =>
        {
            Assert.Equal(OrganizationsTestData.Creator, link.LinkedBy);
            Assert.InRange(link.LinkedAt, started, answered);
        });
        var projectList = await GetAsync(factory, OrganizationsApi.Teams(organization.Id, project.Id));
        Assert.Equal(["Вторая", "Первая"], projectList.Body.EnumerateArray().Select(team => team.GetProperty("name").GetString()));
        var organizationList = await GetAsync(factory, OrganizationsApi.OrgTeams(organization.Id));
        Assert.All(organizationList.Body.EnumerateArray(), team => Assert.Equal(["Альфа"], team.GetProperty("projects").EnumerateArray().Select(p => p.GetProperty("name").GetString())));
        var teamPage = await GetAsync(factory, $"{Team(organization.Id, first.Id)}/projects");
        Assert.Equal([project.Id], Ids(teamPage.Body));
    }

    // feat-028 BR-OL-7 · "a project that gets its first team has that team as its default by inference; nothing is named": the setting
    // stays empty, and the project's list marks the only linked team «onlyTeam».
    [Fact]
    public async Task Link_FirstTeam_IsTheDefaultByInference_NothingIsNamed()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var project = await OrganizationsTestData.CreateBareProjectAsync(factory, organization.Id);
        var team = await OrganizationsTestData.AddUnlinkedTeamAsync(factory, organization.Id, "Единственная");

        var answer = await LinkAsync(organization.Id, project.Id, team.Id);
        var list = await GetAsync(factory, OrganizationsApi.Teams(organization.Id, project.Id));

        Assert.Equal(HttpStatusCode.NoContent, answer.Status);
        Assert.Null(await SettingAsync(factory, project.Id));
        Assert.Equal("onlyTeam", Assert.Single(list.Body.EnumerateArray()).GetProperty("defaultMark").GetString());
    }

    // feat-028 US-3 · "one object, no copies": linking moves no item, sets no default, creates no team and no project.
    [Fact]
    public async Task Link_MovesNoItem_SetsNoDefault_CreatesNothing()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var second = await OrganizationsTestData.CreateBareProjectAsync(factory, structure.OrganizationId, "Второй проект", "SEC");
        var teamsBefore = (await StoredTeamsAsync(factory, structure.OrganizationId)).Select(team => team.Id).Order().ToList();

        var answer = await LinkAsync(structure.OrganizationId, second.Id, structure.Platform.Id);

        Assert.Equal(HttpStatusCode.NoContent, answer.Status);
        Assert.Equal(teamsBefore, (await StoredTeamsAsync(factory, structure.OrganizationId)).Select(team => team.Id).Order());
        Assert.Equal(2, (await LinksOfTeamAsync(factory, structure.Platform.Id)).Count);
        Assert.Null(await SettingAsync(factory, second.Id));
        Assert.Equal(0, await OwnedItemCountAsync(factory, structure.Platform.Id));
    }

    // feat-028 BR-OL-4 · "Link_AlreadyLinked_ChangesNothing_KeepsWhoAndWhen": linking a linked pair again — by another administrator, later —
    // answers 204 and the link keeps who made it and when.
    [Fact]
    public async Task Link_AlreadyLinked_ChangesNothing_KeepsWhoAndWhen()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var other = await MembershipTestData.NewMemberAsync(factory, structure.OrganizationId, OrganizationRole.Admin);
        var before = await LinksAsync(structure.ProjectId);

        var again = await LinkAsync(structure.OrganizationId, structure.ProjectId.ToString(), new { teamIds = new[] { structure.Platform.Id.ToString() } }, other.Token);

        Assert.Equal(HttpStatusCode.NoContent, again.Status);
        Assert.Equal(before, await LinksAsync(structure.ProjectId));
        Assert.All(await LinksAsync(structure.ProjectId), link => Assert.Equal(OrganizationsTestData.Creator, link.LinkedBy));
    }

    // feat-028 D-14 · an absent, null or empty list is a no-op 204; duplicates collapse; the old body `{ "name" }` reads as an empty
    // list, answers 204 and creates no team.
    [Theory]
    [InlineData("absent")]
    [InlineData("null")]
    [InlineData("empty")]
    [InlineData("old name body")]
    [InlineData("no body")]
    public async Task Link_EmptyAbsentOrTheOldBody_Returns204_CreatesNothing(string how)
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var second = await OrganizationsTestData.CreateBareProjectAsync(factory, structure.OrganizationId, "Второй проект", "SEC");
        object? body = how switch
        {
            "absent" => new { },
            "null" => new { teamIds = (string[]?)null },
            "empty" => new { teamIds = Array.Empty<string>() },
            "old name body" => new { name = "Новая команда" },
            _ => null,
        };
        var teamsBefore = (await StoredTeamsAsync(factory, structure.OrganizationId)).Count;

        var answer = await LinkAsync(structure.OrganizationId, second.Id.ToString(), body);

        Assert.Equal(HttpStatusCode.NoContent, answer.Status);
        Assert.Empty(await LinksOfProjectAsync(factory, second.Id));
        Assert.Equal(teamsBefore, (await StoredTeamsAsync(factory, structure.OrganizationId)).Count);
    }

    [Fact]
    public async Task Link_DuplicateEntries_Collapse_OneLink()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var project = await OrganizationsTestData.CreateBareProjectAsync(factory, organization.Id);
        var team = await OrganizationsTestData.AddUnlinkedTeamAsync(factory, organization.Id, "Дубль");

        var answer = await LinkAsync(organization.Id, project.Id, team.Id, team.Id, team.Id);

        Assert.Equal(HttpStatusCode.NoContent, answer.Status);
        Assert.Equal([team.Id], (await LinksOfProjectAsync(factory, project.Id)).Select(link => link.TeamId));
    }

    // feat-028 US-3 · "only active ends are linked" and "all or nothing": one entry that is not an active team of the organization —
    // malformed, unknown, another organization's or deleted — and the answer is 404 team.not_found with no link written, the good
    // entries included.
    [Theory]
    [InlineData("malformed")]
    [InlineData("unknown")]
    [InlineData("of another organization")]
    [InlineData("deleted")]
    public async Task Link_OneBadTeam_Returns404TeamNotFound_AllOrNothing(string how)
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var other = await OrganizationsTestData.CreateStructureAsync(factory);
        var target = await OrganizationsTestData.CreateBareProjectAsync(factory, structure.OrganizationId, "Цель", "TRG");
        var good = await OrganizationsTestData.AddUnlinkedTeamAsync(factory, structure.OrganizationId, "Хорошая");
        var gone = await OrganizationsTestData.AddUnlinkedTeamAsync(factory, structure.OrganizationId, "Ушла");
        await DeleteTeamAsync(factory, structure.OrganizationId, gone.Id);
        var bad = how switch
        {
            "malformed" => "not-a-team",
            "unknown" => Guid.CreateVersion7().ToString(),
            "of another organization" => other.Platform.Id.ToString(),
            _ => gone.Id.ToString(),
        };

        var answer = await LinkAsync(structure.OrganizationId, target.Id.ToString(), new { teamIds = new[] { good.Id.ToString(), bad } });

        Assert.Equal((HttpStatusCode.NotFound, "organizations.team.not_found"), (answer.Status, answer.ErrorCode));
        Assert.Empty(await LinksOfProjectAsync(factory, target.Id));
    }

    // feat-028 § 0.5 · "the address from the outside in": a project that is not an active project of the organization answers
    // project.not_found before the teams of the body are looked at (they are bad too).
    [Theory]
    [InlineData("malformed")]
    [InlineData("unknown")]
    [InlineData("of another organization")]
    [InlineData("deactivated")]
    public async Task Link_ProjectNotReachable_Returns404ProjectNotFound_BeforeTheTeams(string how)
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var other = await OrganizationsTestData.CreateStructureAsync(factory);
        var sleeping = await OrganizationsTestData.CreateBareProjectAsync(factory, structure.OrganizationId, "Спящий", "SLP", ProjectStatus.Deactivated);
        var projectId = how switch
        {
            "malformed" => "ALP",
            "unknown" => Guid.CreateVersion7().ToString(),
            "of another organization" => other.ProjectId.ToString(),
            _ => sleeping.Id.ToString(),
        };

        var answer = await LinkAsync(structure.OrganizationId, projectId, new { teamIds = new[] { "not-a-team" } });

        Assert.Equal((HttpStatusCode.NotFound, "organizations.project.not_found"), (answer.Status, answer.ErrorCode));
        Assert.Empty(await LinksOfProjectAsync(factory, sleeping.Id));
    }

    // feat-028 US-3 · "only a manager of both ends links": organizations.project.link is the administrator's alone; the 403 comes before
    // any id is looked at; a non-member gets the organization's 404; no token gets 401.
    [Theory]
    [InlineData(OrganizationRole.Member)]
    [InlineData(OrganizationRole.Viewer)]
    public async Task Link_MemberAndViewer_Return403_LinkNothing(OrganizationRole role)
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var target = await OrganizationsTestData.CreateBareProjectAsync(factory, structure.OrganizationId, "Цель", "TRG");
        var caller = await MembershipTestData.NewMemberAsync(factory, structure.OrganizationId, role);

        var real = await LinkAsync(structure.OrganizationId, target.Id.ToString(), new { teamIds = new[] { structure.Platform.Id.ToString() } }, caller.Token);
        var missing = await LinkAsync(structure.OrganizationId, Guid.CreateVersion7().ToString(), new { teamIds = new[] { "not-a-team" } }, caller.Token);

        Assert.Equal((HttpStatusCode.Forbidden, "organizations.access.forbidden"), (real.Status, real.ErrorCode));
        Assert.Equal((HttpStatusCode.Forbidden, "organizations.access.forbidden"), (missing.Status, missing.ErrorCode));
        Assert.Empty(await LinksOfProjectAsync(factory, target.Id));
    }

    [Fact]
    public async Task Link_NonMember_Returns404Organization_NoToken_Returns401()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var body = new { teamIds = new[] { structure.Platform.Id.ToString() } };

        var stranger = await LinkAsync(structure.OrganizationId, structure.ProjectId.ToString(), body, OrganizationsTokens.Stranger());
        var anonymous = await OrganizationsApi.SendAsync(factory, HttpMethod.Post, OrganizationsApi.Teams(structure.OrganizationId, structure.ProjectId), body);

        Assert.Equal((HttpStatusCode.NotFound, "organizations.organization.not_found"), (stranger.Status, stranger.ErrorCode));
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.Status);
    }
}
