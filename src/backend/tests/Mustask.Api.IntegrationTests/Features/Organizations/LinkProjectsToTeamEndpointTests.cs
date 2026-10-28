using System.Net;
using Mustask.Domain.Identity;
using Mustask.Domain.Organizations;
using static Mustask.Api.IntegrationTests.Features.Organizations.TeamTestData;

namespace Mustask.Api.IntegrationTests.Features.Organizations;

/// <summary>
/// <c>POST …/teams/{teamId}/projects</c> — link projects to the team (feat-028 api-contract § 2.2; US-3 "a link from either page",
/// "several ends in one confirmation", "one object, no copies"; BR-OL-4, BR-OL-5) through the real host and a real PostgreSQL. Every test
/// builds its own organization.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class LinkProjectsToTeamEndpointTests(MustaskApiFactory factory)
{
    private Task<Answer> LinkAsync(Guid organizationId, string teamId, object? body, string? token = null) =>
        OrganizationsApi.SendAsync(
            factory,
            HttpMethod.Post,
            $"{OrganizationsApi.OrgTeams(organizationId)}/{Uri.EscapeDataString(teamId)}/projects",
            body,
            token ?? OrganizationsTokens.SignedIn());

    private Task<Answer> LinkAsync(Guid organizationId, Guid teamId, params Guid[] projectIds) =>
        LinkAsync(organizationId, teamId.ToString(), new { projectIds = projectIds.Select(id => id.ToString()) });

    // feat-028 US-3 · "Link_FromTheTeamsPage_RecordsTheCallerAndTheTime_BothListsShowIt": the team's page links two projects in one request;
    // each link records the caller and the time, and the team's projects, the organization's list and each project's teams show it.
    [Fact]
    public async Task Link_FromTheTeamsPage_RecordsTheCallerAndTheTime_BothListsShowIt()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var alpha = await OrganizationsTestData.CreateBareProjectAsync(factory, organization.Id, "Альфа", "ALP");
        var beta = await OrganizationsTestData.CreateBareProjectAsync(factory, organization.Id, "Бета", "BET");
        var team = await OrganizationsTestData.AddUnlinkedTeamAsync(factory, organization.Id, "Общая");

        var started = DateTimeOffset.UtcNow;
        var answer = await LinkAsync(organization.Id, team.Id, alpha.Project.Id, beta.Project.Id);
        var answered = DateTimeOffset.UtcNow;

        Assert.Equal(HttpStatusCode.NoContent, answer.Status);
        Assert.Null(answer.MediaType);
        var links = await LinksOfTeamAsync(factory, team.Id);
        Assert.Equal(new[] { alpha.Project.Id, beta.Project.Id }.Order(), links.Select(link => link.ProjectId).Order());
        Assert.All(links, link =>
        {
            Assert.Equal(OrganizationsTestData.Creator, link.LinkedBy);
            Assert.InRange(link.LinkedAt, started, answered);
        });
        var teamPage = await GetAsync(factory, $"{Team(organization.Id, team.Id)}/projects");
        Assert.Equal(["ALP", "BET"], teamPage.Body.EnumerateArray().Select(project => project.GetProperty("key").GetString()));
        var organizationList = await GetAsync(factory, OrganizationsApi.OrgTeams(organization.Id));
        Assert.Equal(
            ["Альфа", "Бета"],
            Assert.Single(organizationList.Body.EnumerateArray()).GetProperty("projects").EnumerateArray().Select(project => project.GetProperty("name").GetString()));
        var projectList = await GetAsync(factory, OrganizationsApi.Teams(organization.Id, alpha.Project.Id));
        Assert.Equal([team.Id], Ids(projectList.Body));
    }

    // feat-028 US-3 · "Link_ThreeProjects_AllOrNothing_DeactivatedOneMeansNone404": one project of three is deactivated and the answer
    // is 404 project.not_found with nothing linked — the active ones included.
    [Fact]
    public async Task Link_ThreeProjects_AllOrNothing_DeactivatedOneMeansNone404()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var alpha = await OrganizationsTestData.CreateBareProjectAsync(factory, organization.Id, "Альфа", "ALP");
        var beta = await OrganizationsTestData.CreateBareProjectAsync(factory, organization.Id, "Бета", "BET");
        var sleeping = await OrganizationsTestData.CreateBareProjectAsync(factory, organization.Id, "Спящий", "SLP", ProjectStatus.Deactivated);
        var team = await OrganizationsTestData.AddUnlinkedTeamAsync(factory, organization.Id, "Общая");

        var answer = await LinkAsync(organization.Id, team.Id, alpha.Project.Id, beta.Project.Id, sleeping.Project.Id);

        Assert.Equal((HttpStatusCode.NotFound, "organizations.project.not_found"), (answer.Status, answer.ErrorCode));
        Assert.Empty(await LinksOfTeamAsync(factory, team.Id));
    }

    // feat-028 US-3 · "one object, no copies": linking one team to three projects creates no team, no project and no item; the same
    // team (the same id, the same name, the same people) is in all three.
    [Fact]
    public async Task Link_ToSeveralProjects_CreatesNoTeamProjectOrItem_SameTeam()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var second = await OrganizationsTestData.CreateBareProjectAsync(factory, structure.OrganizationId, "Второй", "SEC");
        var third = await OrganizationsTestData.CreateBareProjectAsync(factory, structure.OrganizationId, "Третий", "THR");
        var teamsBefore = (await StoredTeamsAsync(factory, structure.OrganizationId)).Select(team => team.Id).Order().ToList();
        var membersBefore = await TeamMemberIdsAsync(factory, structure.Platform.Id);

        var answer = await LinkAsync(structure.OrganizationId, structure.Platform.Id, second.Project.Id, third.Project.Id);

        Assert.Equal(HttpStatusCode.NoContent, answer.Status);
        Assert.Equal(teamsBefore, (await StoredTeamsAsync(factory, structure.OrganizationId)).Select(team => team.Id).Order());
        Assert.Equal(membersBefore, await TeamMemberIdsAsync(factory, structure.Platform.Id));
        Assert.Equal(3, (await LinksOfTeamAsync(factory, structure.Platform.Id)).Count);
        foreach (var project in new[] { second.Project.Id, third.Project.Id })
        {
            var teams = await GetAsync(factory, OrganizationsApi.Teams(structure.OrganizationId, project));
            Assert.Equal([structure.Platform.Id], Ids(teams.Body));
            Assert.Null(await SettingAsync(factory, project));
        }

        Assert.Equal(0, await OwnedItemCountAsync(factory, structure.Platform.Id));
    }

    // feat-028 BR-OL-4 · a pair that is already linked changes nothing and keeps who and when, even when the request carries new pairs.
    [Fact]
    public async Task Link_AlreadyLinkedPair_KeepsWhoAndWhen_NewPairIsAdded()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var second = await OrganizationsTestData.CreateBareProjectAsync(factory, structure.OrganizationId, "Второй", "SEC");
        var other = await MembershipTestData.NewMemberAsync(factory, structure.OrganizationId, OrganizationRole.Admin);
        var before = (await LinksOfTeamAsync(factory, structure.Platform.Id)).Single();

        var answer = await LinkAsync(
            structure.OrganizationId,
            structure.Platform.Id.ToString(),
            new { projectIds = new[] { structure.ProjectId.ToString(), second.Project.Id.ToString() } },
            other.Token);

        Assert.Equal(HttpStatusCode.NoContent, answer.Status);
        var links = await LinksOfTeamAsync(factory, structure.Platform.Id);
        Assert.Equal(2, links.Count);
        var kept = links.Single(link => link.ProjectId == structure.ProjectId);
        Assert.Equal((before.LinkedBy, before.LinkedAt), (kept.LinkedBy, kept.LinkedAt));
        Assert.Equal(other.Id, links.Single(link => link.ProjectId == second.Project.Id).LinkedBy);
    }

    // feat-028 D-14 · an absent, null or empty list is a no-op 204; the duplicates collapse.
    [Theory]
    [InlineData("absent")]
    [InlineData("null")]
    [InlineData("empty")]
    [InlineData("no body")]
    public async Task Link_EmptyOrAbsent_Returns204_LinksNothing(string how)
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var alone = await OrganizationsTestData.AddUnlinkedTeamAsync(factory, structure.OrganizationId, "Ничья");
        object? body = how switch
        {
            "absent" => new { },
            "null" => new { projectIds = (string[]?)null },
            "empty" => new { projectIds = Array.Empty<string>() },
            _ => null,
        };

        var answer = await LinkAsync(structure.OrganizationId, alone.Id.ToString(), body);

        Assert.Equal(HttpStatusCode.NoContent, answer.Status);
        Assert.Empty(await LinksOfTeamAsync(factory, alone.Id));
    }

    [Fact]
    public async Task Link_DuplicateEntries_Collapse_OneLink()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var project = await OrganizationsTestData.CreateBareProjectAsync(factory, organization.Id);
        var team = await OrganizationsTestData.AddUnlinkedTeamAsync(factory, organization.Id, "Дубль");

        var answer = await LinkAsync(organization.Id, team.Id, project.Project.Id, project.Project.Id);

        Assert.Equal(HttpStatusCode.NoContent, answer.Status);
        Assert.Single(await LinksOfTeamAsync(factory, team.Id));
    }

    // feat-028 US-3 · "only active ends are linked": an entry that is not an active project of the organization — malformed, unknown,
    // another organization's or deactivated — answers 404 project.not_found, nothing linked.
    [Theory]
    [InlineData("malformed")]
    [InlineData("unknown")]
    [InlineData("of another organization")]
    [InlineData("deactivated")]
    public async Task Link_OneBadProject_Returns404ProjectNotFound_AllOrNothing(string how)
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var other = await OrganizationsTestData.CreateStructureAsync(factory);
        var good = await OrganizationsTestData.CreateBareProjectAsync(factory, structure.OrganizationId, "Хороший", "GOD");
        var sleeping = await OrganizationsTestData.CreateBareProjectAsync(factory, structure.OrganizationId, "Спящий", "SLP", ProjectStatus.Deactivated);
        var bad = how switch
        {
            "malformed" => "ALP",
            "unknown" => Guid.CreateVersion7().ToString(),
            "of another organization" => other.ProjectId.ToString(),
            _ => sleeping.Project.Id.ToString(),
        };

        var answer = await LinkAsync(structure.OrganizationId, structure.Platform.Id.ToString(), new { projectIds = new[] { good.Project.Id.ToString(), bad } });

        Assert.Equal((HttpStatusCode.NotFound, "organizations.project.not_found"), (answer.Status, answer.ErrorCode));
        Assert.DoesNotContain(good.Project.Id, (await LinksOfTeamAsync(factory, structure.Platform.Id)).Select(link => link.ProjectId));
    }

    // feat-028 § 0.5 · the address from the outside in: the team (the address) answers team.not_found before the projects of the body
    // are looked at — a deleted team, an unknown one, another organization's and a malformed id.
    [Theory]
    [InlineData("malformed")]
    [InlineData("unknown")]
    [InlineData("of another organization")]
    [InlineData("deleted")]
    public async Task Link_TeamNotAnActiveTeamOfTheOrganization_Returns404TeamNotFound_BeforeTheProjects(string how)
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var other = await OrganizationsTestData.CreateStructureAsync(factory);
        var gone = await OrganizationsTestData.AddTeamAsync(factory, structure.ProjectId, "Ушла");
        await DeleteTeamAsync(factory, structure.OrganizationId, gone.Id);
        var teamId = how switch
        {
            "malformed" => "not-a-team",
            "unknown" => Guid.CreateVersion7().ToString(),
            "of another organization" => other.Platform.Id.ToString(),
            _ => gone.Id.ToString(),
        };

        var answer = await LinkAsync(structure.OrganizationId, teamId, new { projectIds = new[] { "not-a-project" } });

        Assert.Equal((HttpStatusCode.NotFound, "organizations.team.not_found"), (answer.Status, answer.ErrorCode));
        Assert.Empty(await LinksOfTeamAsync(factory, gone.Id));
    }

    // feat-028 US-3 · "only a manager of both ends links": organizations.team.link is the administrator's alone; the 403 comes first.
    [Theory]
    [InlineData(OrganizationRole.Member)]
    [InlineData(OrganizationRole.Viewer)]
    public async Task Link_MemberAndViewer_Return403_LinkNothing(OrganizationRole role)
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var target = await OrganizationsTestData.CreateBareProjectAsync(factory, structure.OrganizationId, "Цель", "TRG");
        var caller = await MembershipTestData.NewMemberAsync(factory, structure.OrganizationId, role);

        var real = await LinkAsync(structure.OrganizationId, structure.Platform.Id.ToString(), new { projectIds = new[] { target.Project.Id.ToString() } }, caller.Token);
        var missing = await LinkAsync(structure.OrganizationId, Guid.CreateVersion7().ToString(), new { projectIds = new[] { "not-a-project" } }, caller.Token);

        Assert.Equal((HttpStatusCode.Forbidden, "organizations.access.forbidden"), (real.Status, real.ErrorCode));
        Assert.Equal((HttpStatusCode.Forbidden, "organizations.access.forbidden"), (missing.Status, missing.ErrorCode));
        Assert.Empty(await LinksOfProjectAsync(factory, target.Project.Id));
    }

    [Fact]
    public async Task Link_NonMember_Returns404Organization_NoToken_Returns401()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var body = new { projectIds = new[] { structure.ProjectId.ToString() } };

        var stranger = await LinkAsync(structure.OrganizationId, structure.Platform.Id.ToString(), body, OrganizationsTokens.Stranger());
        var anonymous = await OrganizationsApi.SendAsync(factory, HttpMethod.Post, $"{Team(structure.OrganizationId, structure.Platform.Id)}/projects", body);

        Assert.Equal((HttpStatusCode.NotFound, "organizations.organization.not_found"), (stranger.Status, stranger.ErrorCode));
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.Status);
    }

    // feat-028 US-3 · "a deactivated project keeps its links": the team's projects list leaves the deactivated project, the link
    // survives, and a restore brings it back to the list (G Projects_OfATeam_LeavesTheDeactivatedProject_LinkSurvives).
    [Fact]
    public async Task Projects_OfATeam_LeavesTheDeactivatedProject_LinkSurvives()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var teamProjects = $"{Team(structure.OrganizationId, structure.Platform.Id)}/projects";

        var before = await GetAsync(factory, teamProjects);
        await OrganizationsTestData.SetProjectStatusAsync(factory, structure.OrganizationId, structure.ProjectId, ProjectStatus.Deactivated);
        var whileDeactivated = await GetAsync(factory, teamProjects);
        var linkKept = await LinksOfTeamAsync(factory, structure.Platform.Id);
        await OrganizationsTestData.SetProjectStatusAsync(factory, structure.OrganizationId, structure.ProjectId, ProjectStatus.Active);
        var restored = await GetAsync(factory, teamProjects);

        Assert.Equal([structure.ProjectId], Ids(before.Body));
        Assert.Empty(whileDeactivated.Body.EnumerateArray());
        Assert.Single(linkKept);
        Assert.Equal([structure.ProjectId], Ids(restored.Body));
        Assert.Equal(["id", "key", "name"], PropertyNames(before.Body[0]));
    }
}
