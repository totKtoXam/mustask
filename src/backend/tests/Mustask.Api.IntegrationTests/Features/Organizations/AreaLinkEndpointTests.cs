using System.Net;
using System.Text.Json;
using Mustask.Api.IntegrationTests.Features.WorkItems;
using Mustask.Domain.Identity;

namespace Mustask.Api.IntegrationTests.Features.Organizations;

/// <summary>
/// mod-018 US-3 (the links between areas, projects and teams, from all three ends, and creation with links) and the link part of
/// US-6 (containment and roles). Each test builds its own organization.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class AreaLinkEndpointTests(MustaskApiFactory factory)
{
    private Task<Answer> SendAsync(HttpMethod method, string path, object? body = null, string? token = null) =>
        OrganizationsApi.SendAsync(factory, method, path, body, token ?? OrganizationsTokens.SignedIn());

    private static string Area(Guid organizationId, Guid areaId) => $"{OrganizationsApi.Areas(organizationId)}/{areaId}";

    private static IEnumerable<Guid> Ids(Answer answer) => answer.Body.EnumerateArray().Select(row => row.GetProperty("id").GetGuid());

    // US-3 · "A project is linked to an area from the area's end": the link shows from both ends, a repeat is a 204 no-op, and an
    // empty list is a 204 no-op too.
    [Fact]
    public async Task LinkProject_FromTheAreaEnd_ShowsFromBothEnds_IsIdempotent()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var organization = structure.OrganizationId;
        var area = await OrganizationsTestData.AddAreaAsync(factory, organization, null, "Отчёты");
        var body = new { projectIds = new[] { structure.ProjectId.ToString() } };

        var linked = await SendAsync(HttpMethod.Post, $"{Area(organization, area.Id)}/projects", body);
        var again = await SendAsync(HttpMethod.Post, $"{Area(organization, area.Id)}/projects", body);
        var empty = await SendAsync(HttpMethod.Post, $"{Area(organization, area.Id)}/projects", new { projectIds = Array.Empty<string>() });
        var fromArea = await SendAsync(HttpMethod.Get, $"{Area(organization, area.Id)}/projects");
        var fromProject = await SendAsync(HttpMethod.Get, OrganizationsApi.ProjectAreas(organization, structure.ProjectId));

        Assert.Equal(HttpStatusCode.NoContent, linked.Status);
        Assert.Equal(HttpStatusCode.NoContent, again.Status);
        Assert.Equal(HttpStatusCode.NoContent, empty.Status);
        var project = Assert.Single(fromArea.Body.EnumerateArray());
        Assert.Equal(["id", "key", "name"], project.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));
        Assert.Equal(structure.ProjectId, project.GetProperty("id").GetGuid());
        var reference = Assert.Single(fromProject.Body.EnumerateArray());
        Assert.Equal(["id", "name", "path"], reference.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal));
        Assert.Equal(area.Id, reference.GetProperty("id").GetGuid());
    }

    // US-3 · "The same link is made from the project's end and from the team's end": areas are linked from either side.
    [Fact]
    public async Task Link_FromTheProjectEndAndTheTeamEnd_ShowsFromTheAreaEnd()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var organization = structure.OrganizationId;
        var reports = await OrganizationsTestData.AddAreaAsync(factory, organization, null, "Отчёты");
        var store = await OrganizationsTestData.AddAreaAsync(factory, organization, reports.Id, "Склад");

        var projectLink = await SendAsync(HttpMethod.Post, OrganizationsApi.ProjectAreas(organization, structure.ProjectId), new { areaIds = new[] { reports.Id.ToString() } });
        var teamLink = await SendAsync(HttpMethod.Post, OrganizationsApi.TeamAreas(organization, structure.Platform.Id), new { areaIds = new[] { reports.Id.ToString(), store.Id.ToString() } });
        var projects = await SendAsync(HttpMethod.Get, $"{Area(organization, reports.Id)}/projects");
        var teams = await SendAsync(HttpMethod.Get, $"{Area(organization, reports.Id)}/teams");
        var teamAreas = await SendAsync(HttpMethod.Get, OrganizationsApi.TeamAreas(organization, structure.Platform.Id));

        Assert.Equal(HttpStatusCode.NoContent, projectLink.Status);
        Assert.Equal(HttpStatusCode.NoContent, teamLink.Status);
        Assert.Equal(structure.ProjectId, Assert.Single(Ids(projects)));
        Assert.Equal(structure.Platform.Id, Assert.Single(Ids(teams)));
        Assert.Equal(["Отчёты", "Отчёты / Склад"], teamAreas.Body.EnumerateArray().Select(row => row.GetProperty("path").GetString()));
    }

    // US-3 · "A link is removed from any end": the area, the project and the team stay; only the link goes.
    [Fact]
    public async Task Unlink_FromEveryEnd_RemovesOnlyTheLink()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var organization = structure.OrganizationId;
        var area = await OrganizationsTestData.AddAreaAsync(factory, organization, null, "Отчёты");
        await OrganizationsTestData.LinkAreaToProjectAsync(factory, organization, area.Id, structure.ProjectId);
        await OrganizationsTestData.LinkAreaToTeamAsync(factory, organization, area.Id, structure.Platform.Id);

        var fromArea = await SendAsync(HttpMethod.Delete, $"{Area(organization, area.Id)}/projects/{structure.ProjectId}");
        var fromTeam = await SendAsync(HttpMethod.Delete, $"{OrganizationsApi.TeamAreas(organization, structure.Platform.Id)}/{area.Id}");
        var repeat = await SendAsync(HttpMethod.Delete, $"{OrganizationsApi.TeamAreas(organization, structure.Platform.Id)}/{area.Id}");

        Assert.Equal(HttpStatusCode.NoContent, fromArea.Status);
        Assert.Equal(HttpStatusCode.NoContent, fromTeam.Status);
        Assert.Equal(HttpStatusCode.NoContent, repeat.Status);
        Assert.Empty((await SendAsync(HttpMethod.Get, $"{Area(organization, area.Id)}/projects")).Body.EnumerateArray());
        Assert.Empty((await SendAsync(HttpMethod.Get, $"{Area(organization, area.Id)}/teams")).Body.EnumerateArray());
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(HttpMethod.Get, Area(organization, area.Id))).Status);
    }

    // US-3 · "Only active projects are listed": a deactivated project stays linked, is left out of the list, and shows in the
    // deletion dialog with active = false.
    [Fact]
    public async Task DeactivatedProject_IsLeftOutOfTheList_ShownInTheDeletionDialog()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var organization = structure.OrganizationId;
        var area = await OrganizationsTestData.AddAreaAsync(factory, organization, null, "Отчёты");
        var dormant = await OrganizationsTestData.CreateProjectAsync(factory, organization, "Спящий", "SLP", Mustask.Domain.Organizations.ProjectStatus.Deactivated);
        await OrganizationsTestData.LinkAreaToProjectAsync(factory, organization, area.Id, dormant.Project.Id);

        var list = await SendAsync(HttpMethod.Get, $"{Area(organization, area.Id)}/projects");
        var deletion = await SendAsync(HttpMethod.Get, $"{Area(organization, area.Id)}/deletion");

        Assert.Empty(list.Body.EnumerateArray());
        var shown = Assert.Single(deletion.Body.GetProperty("projects").EnumerateArray());
        Assert.Equal(dormant.Project.Id, shown.GetProperty("id").GetGuid());
        Assert.False(shown.GetProperty("active").GetBoolean());
    }

    // US-3 · "A project of another organization is the same 404 as an unknown one", from every end; nothing is linked, not even
    // the ids that were fine (all or nothing).
    [Fact]
    public async Task Link_ProjectOrTeamOfAnotherOrganization_Returns404_LinksNothing()
    {
        var mine = await OrganizationsTestData.CreateStructureAsync(factory);
        var theirs = await OrganizationsTestData.CreateStructureAsync(factory);
        var area = await OrganizationsTestData.AddAreaAsync(factory, mine.OrganizationId, null, "Отчёты");
        var organization = mine.OrganizationId;

        var project = await SendAsync(HttpMethod.Post, $"{Area(organization, area.Id)}/projects", new { projectIds = new[] { mine.ProjectId.ToString(), theirs.ProjectId.ToString() } });
        var team = await SendAsync(HttpMethod.Post, $"{Area(organization, area.Id)}/teams", new { teamIds = new[] { mine.Platform.Id.ToString(), theirs.Platform.Id.ToString() } });
        var fromProject = await SendAsync(HttpMethod.Post, OrganizationsApi.ProjectAreas(organization, theirs.ProjectId), new { areaIds = new[] { area.Id.ToString() } });
        var malformed = await SendAsync(HttpMethod.Post, $"{Area(organization, area.Id)}/projects", new { projectIds = new[] { "x" } });

        LinksTestData.AssertRefused(project, HttpStatusCode.NotFound, "organizations.project.not_found");
        LinksTestData.AssertRefused(team, HttpStatusCode.NotFound, "organizations.team.not_found");
        LinksTestData.AssertRefused(fromProject, HttpStatusCode.NotFound, "organizations.project.not_found");
        LinksTestData.AssertRefused(malformed, HttpStatusCode.NotFound, "organizations.project.not_found");
        Assert.Empty((await SendAsync(HttpMethod.Get, $"{Area(organization, area.Id)}/projects")).Body.EnumerateArray());
        Assert.Empty((await SendAsync(HttpMethod.Get, $"{Area(organization, area.Id)}/teams")).Body.EnumerateArray());
    }

    // US-3 · "An area of another organization is refused when linking from a project's or a team's end".
    [Fact]
    public async Task Link_AnAreaOfAnotherOrganization_FromAProjectEnd_Returns404AreaNotFound()
    {
        var mine = await OrganizationsTestData.CreateStructureAsync(factory);
        var theirs = await OrganizationsTestData.CreateStructureAsync(factory);
        var foreign = await OrganizationsTestData.AddAreaAsync(factory, theirs.OrganizationId, null, "Чужая");
        var body = new { areaIds = new[] { foreign.Id.ToString() } };

        var project = await SendAsync(HttpMethod.Post, OrganizationsApi.ProjectAreas(mine.OrganizationId, mine.ProjectId), body);
        var team = await SendAsync(HttpMethod.Post, OrganizationsApi.TeamAreas(mine.OrganizationId, mine.Platform.Id), body);

        LinksTestData.AssertRefused(project, HttpStatusCode.NotFound, "organizations.area.not_found");
        LinksTestData.AssertRefused(team, HttpStatusCode.NotFound, "organizations.area.not_found");
    }

    // US-3 · "A project is created with its areas, a team too, an area with both": all or nothing.
    [Fact]
    public async Task Create_WithLinks_LinksInTheSameStep_AllOrNothing()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var organization = structure.OrganizationId;
        var area = await OrganizationsTestData.AddAreaAsync(factory, organization, null, "Отчёты");

        var created = await SendAsync(HttpMethod.Post, $"{OrganizationsApi.Areas(organization)}", new
        {
            name = "Склад",
            projectIds = new[] { structure.ProjectId.ToString() },
            teamIds = new[] { structure.Platform.Id.ToString() },
        });
        var refused = await SendAsync(HttpMethod.Post, $"{OrganizationsApi.Areas(organization)}", new
        {
            name = "Цех",
            projectIds = new[] { Guid.CreateVersion7().ToString() },
        });
        var project = await SendAsync(HttpMethod.Post, $"/api/organizations/{organization}/projects", new { name = "Новый", key = "NEW", areaIds = new[] { area.Id.ToString() } });
        var team = await SendAsync(HttpMethod.Post, OrganizationsApi.OrgTeams(organization), new { name = "Новая", areaIds = new[] { area.Id.ToString() } });
        var badTeam = await SendAsync(HttpMethod.Post, OrganizationsApi.OrgTeams(organization), new { name = "Лишняя", areaIds = new[] { Guid.CreateVersion7().ToString() } });

        Assert.Equal(HttpStatusCode.Created, created.Status);
        var createdId = created.Body.GetProperty("id").GetGuid();
        Assert.Equal(structure.ProjectId, Assert.Single(Ids(await SendAsync(HttpMethod.Get, $"{Area(organization, createdId)}/projects"))));
        Assert.Equal(structure.Platform.Id, Assert.Single(Ids(await SendAsync(HttpMethod.Get, $"{Area(organization, createdId)}/teams"))));
        LinksTestData.AssertRefused(refused, HttpStatusCode.NotFound, "organizations.project.not_found");
        Assert.DoesNotContain(
            (await OrganizationsTestData.ReadTreeAsync(factory, organization)).PreOrder(),
            node => node.Name == "Цех");
        Assert.Equal(HttpStatusCode.Created, project.Status);
        Assert.Equal(project.Body.GetProperty("id").GetGuid(), Assert.Single(Ids(await SendAsync(HttpMethod.Get, $"{Area(organization, area.Id)}/projects"))));
        Assert.Equal(HttpStatusCode.Created, team.Status);
        Assert.Equal(team.Body.GetProperty("id").GetGuid(), Assert.Single(Ids(await SendAsync(HttpMethod.Get, $"{Area(organization, area.Id)}/teams"))));
        LinksTestData.AssertRefused(badTeam, HttpStatusCode.NotFound, "organizations.area.not_found");
    }

    // US-3 / US-6 · "A deactivated project or a deleted team of the address is a 404": a link naming it changes nothing.
    [Fact]
    public async Task Link_ADeactivatedProjectOfTheAddress_Returns404ProjectNotFound()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var organization = structure.OrganizationId;
        var area = await OrganizationsTestData.AddAreaAsync(factory, organization, null, "Отчёты");
        var dormant = await OrganizationsTestData.CreateProjectAsync(factory, organization, "Спящий", "SLP", Mustask.Domain.Organizations.ProjectStatus.Deactivated);

        var answer = await SendAsync(HttpMethod.Post, OrganizationsApi.ProjectAreas(organization, dormant.Project.Id), new { areaIds = new[] { area.Id.ToString() } });

        LinksTestData.AssertRefused(answer, HttpStatusCode.NotFound, "organizations.project.not_found");
        Assert.Empty((await SendAsync(HttpMethod.Get, $"{Area(organization, area.Id)}/projects")).Body.EnumerateArray());
    }

    // US-3 / BR-AW-10 · unlinking from the area's end names a deactivated project: 404 `project.not_found`, and the stored link
    // survives, so a restore brings it back.
    [Fact]
    public async Task Unlink_ADeactivatedProjectFromTheAreaEnd_Returns404_LinkSurvivesRestore()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var organization = structure.OrganizationId;
        var area = await OrganizationsTestData.AddAreaAsync(factory, organization, null, "Отчёты");
        var project = await OrganizationsTestData.CreateProjectAsync(factory, organization, "Спящий", "SLP");
        await OrganizationsTestData.LinkAreaToProjectAsync(factory, organization, area.Id, project.Project.Id);
        await OrganizationsTestData.SetProjectStatusAsync(factory, organization, project.Project.Id, Mustask.Domain.Organizations.ProjectStatus.Deactivated);

        var answer = await SendAsync(HttpMethod.Delete, $"{Area(organization, area.Id)}/projects/{project.Project.Id}");
        await OrganizationsTestData.SetProjectStatusAsync(factory, organization, project.Project.Id, Mustask.Domain.Organizations.ProjectStatus.Active);

        LinksTestData.AssertRefused(answer, HttpStatusCode.NotFound, "organizations.project.not_found");
        Assert.Contains(project.Project.Id, Ids(await SendAsync(HttpMethod.Get, $"{Area(organization, area.Id)}/projects")));
    }

    // US-3 · unlinking from the area's end names a team that is gone: 404 `team.not_found`.
    [Fact]
    public async Task Unlink_AnUnreachableTeamFromTheAreaEnd_Returns404TeamNotFound()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var organization = structure.OrganizationId;
        var area = await OrganizationsTestData.AddAreaAsync(factory, organization, null, "Отчёты");
        var team = await OrganizationsTestData.AddTeamAsync(factory, structure.ProjectId, "Временная");
        await OrganizationsTestData.DeleteTeamAsync(factory, structure.ProjectId, team.Id);

        var answer = await SendAsync(HttpMethod.Delete, $"{Area(organization, area.Id)}/teams/{team.Id}");

        LinksTestData.AssertRefused(answer, HttpStatusCode.NotFound, "organizations.team.not_found");
    }

    // US-6 · "The link routes follow the roles": a viewer and a member read the links of every end and write none of them.
    [Theory]
    [InlineData(OrganizationRole.Member)]
    [InlineData(OrganizationRole.Viewer)]
    public async Task LinkRoutes_ForAMemberAndAViewer_ReadsOpen_WritesAre403(OrganizationRole role)
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var organization = structure.OrganizationId;
        var area = await OrganizationsTestData.AddAreaAsync(factory, organization, null, "Отчёты");
        var user = await MembershipTestData.NewMemberAsync(factory, organization, role);
        var projectAreas = OrganizationsApi.ProjectAreas(organization, structure.ProjectId);
        var teamAreas = OrganizationsApi.TeamAreas(organization, structure.Platform.Id);

        foreach (var read in new[] { projectAreas, teamAreas, $"{Area(organization, area.Id)}/projects", $"{Area(organization, area.Id)}/teams" })
        {
            Assert.Equal(HttpStatusCode.OK, (await SendAsync(HttpMethod.Get, read, token: user.Token)).Status);
        }

        var writes = new (HttpMethod, string, object?)[]
        {
            (HttpMethod.Post, projectAreas, new { areaIds = new[] { area.Id.ToString() } }),
            (HttpMethod.Post, teamAreas, new { areaIds = new[] { area.Id.ToString() } }),
            (HttpMethod.Delete, $"{projectAreas}/{area.Id}", null),
            (HttpMethod.Delete, $"{teamAreas}/{area.Id}", null),
        };

        foreach (var (method, path, body) in writes)
        {
            LinksTestData.AssertRefused(await SendAsync(method, path, body, user.Token), HttpStatusCode.Forbidden, "organizations.access.forbidden");
        }

        Assert.Empty((await SendAsync(HttpMethod.Get, $"{Area(organization, area.Id)}/projects")).Body.EnumerateArray());
    }

    // US-6 · "A non-member sees no link": the organization is not found, from every end.
    [Fact]
    public async Task LinkRoutes_ANonMember_Returns404OrganizationNotFound()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var organization = structure.OrganizationId;
        var stranger = MembershipTestData.Outsider();

        foreach (var path in new[]
        {
            OrganizationsApi.ProjectAreas(organization, structure.ProjectId),
            OrganizationsApi.TeamAreas(organization, structure.Platform.Id),
        })
        {
            LinksTestData.AssertRefused(await SendAsync(HttpMethod.Get, path, token: stranger), HttpStatusCode.NotFound, "organizations.organization.not_found");
        }
    }
}
