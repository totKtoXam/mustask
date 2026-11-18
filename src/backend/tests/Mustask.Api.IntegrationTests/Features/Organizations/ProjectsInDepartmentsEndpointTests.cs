using System.Net;
using Mustask.Api.IntegrationTests.Features.WorkItems;
using static Mustask.Api.IntegrationTests.Features.Organizations.TeamTestData;

namespace Mustask.Api.IntegrationTests.Features.Organizations;

/// <summary>
/// doc-016 / mod-022 through the real host and a real PostgreSQL: a project stands in a department (BR-TD-6), a link joins a team and
/// a project of one department (BR-TD-3), and a linked project is not moved (BR-TD-4); the link against the move ends in one order
/// whichever is first (the project node is locked <c>FOR SHARE</c> by the link and <c>FOR NO KEY UPDATE</c> by the move).
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class ProjectsInDepartmentsEndpointTests(MustaskApiFactory factory)
{
    private Task<Answer> CreateAsync(Guid organizationId, object body, CommitGate? gate = null) =>
        OrganizationsApi.SendAsync(
            factory, HttpMethod.Post, $"{OrganizationsApi.Organization(organizationId)}/projects", body, OrganizationsTokens.SignedIn(), gate);

    private Task<Answer> MoveAsync(Guid organizationId, Guid nodeId, Guid? parentId, CommitGate? gate = null) =>
        OrganizationsApi.SendAsync(
            factory,
            HttpMethod.Put,
            $"{OrganizationsApi.Areas(organizationId)}/{nodeId}/parent",
            new { parentId = parentId?.ToString() },
            OrganizationsTokens.SignedIn(),
            gate);

    // US-2 · BR-TD-6: a request naming no department creates nothing, and the reason sits on the field.
    [Fact]
    public async Task Create_WithoutADepartment_Is400OnTheField_AndCreatesNothing()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);

        var answer = await CreateAsync(organization.Id, new { name = "Без департамента", key = "NOD" });

        Assert.Equal(HttpStatusCode.BadRequest, answer.Status);
        var errors = answer.Body.GetProperty("errors");
        Assert.Equal("parentAreaId", Assert.Single(errors.EnumerateObject()).Name);
        Assert.Equal("organizations.project.department_required", errors.GetProperty("parentAreaId")[0].GetProperty("code").GetString());
        var listed = await OrganizationsApi.SendAsync(
            factory, HttpMethod.Get, $"{OrganizationsApi.Organization(organization.Id)}/projects", token: OrganizationsTokens.SignedIn());
        Assert.Equal(0, listed.Body.GetArrayLength());
    }

    // US-2 · the same request with a department creates the project, its node standing in it.
    [Fact]
    public async Task Create_InADepartment_PutsTheNodeThere()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var department = await OrganizationsTestData.AddDepartmentAsync(factory, organization.Id, "Разработка");

        var answer = await CreateAsync(organization.Id, new { name = "Мастаск", key = "MST", parentAreaId = department.Id.ToString() });

        Assert.Equal(HttpStatusCode.Created, answer.Status);
        var node = await OrganizationsTestData.NodeOfAsync(factory, answer.Body.GetProperty("id").GetGuid());
        Assert.Equal(department.Id, node.ParentId);
    }

    // US-5 · BR-TD-3: a project created with a team of another department is refused, and nothing is created.
    [Fact]
    public async Task Create_WithATeamOfAnotherDepartment_Is409OtherDepartment_AndCreatesNothing()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var development = await OrganizationsTestData.AddDepartmentAsync(factory, organization.Id, "Разработка");
        var finance = await OrganizationsTestData.AddDepartmentAsync(factory, organization.Id, "Финансы");
        var team = await OrganizationsTestData.AddTeamInAsync(factory, organization.Id, finance.Id, "Бухгалтерия");

        var refused = await CreateAsync(
            organization.Id, new { name = "Мастаск", key = "MST", parentAreaId = development.Id.ToString(), teamIds = new[] { team.Id.ToString() } });
        var own = await CreateAsync(
            organization.Id, new { name = "Мастаск", key = "MST", parentAreaId = finance.Id.ToString(), teamIds = new[] { team.Id.ToString() } });

        LinksTestData.AssertRefused(refused, HttpStatusCode.Conflict, "organizations.team.other_department");
        Assert.Equal(HttpStatusCode.Created, own.Status);
        Assert.Single(await LinksOfTeamAsync(factory, team.Id));
    }

    // US-5 · BR-TD-3 from both ends: a link across departments is 409 and writes nothing; one inside the department is 204.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Link_AcrossDepartments_Is409OtherDepartment_InsideOneIs204(bool fromTheProject)
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var development = await OrganizationsTestData.AddDepartmentAsync(factory, organization.Id, "Разработка");
        var finance = await OrganizationsTestData.AddDepartmentAsync(factory, organization.Id, "Финансы");
        var project = await ProjectInAsync(organization.Id, development.Id, "Мастаск", "MST");
        var inside = await OrganizationsTestData.AddTeamInAsync(factory, organization.Id, development.Id, "Платформа");
        var outside = await OrganizationsTestData.AddTeamInAsync(factory, organization.Id, finance.Id, "Бухгалтерия");

        Task<Answer> Link(Guid team, CommitGate? at = null) => fromTheProject
            ? LinkTeamsAsync(factory, organization.Id, project, new { teamIds = new[] { team.ToString() } }, gate: at)
            : LinkProjectsAsync(factory, organization.Id, team, new { projectIds = new[] { project.ToString() } }, gate: at);

        var across = await Link(outside.Id);
        var within = await Link(inside.Id);

        LinksTestData.AssertRefused(across, HttpStatusCode.Conflict, "organizations.team.other_department");
        Assert.Equal(HttpStatusCode.NoContent, within.Status);
        Assert.Equal([inside.Id], (await LinksOfProjectAsync(factory, project)).Select(link => link.TeamId));
    }

    // US-2 · BR-TD-4: a project that has a team is not moved to another department; one without a team is.
    [Fact]
    public async Task Move_ALinkedProject_Is409HasTeams_AnUnlinkedOneMoves()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var development = await OrganizationsTestData.AddDepartmentAsync(factory, organization.Id, "Разработка");
        var finance = await OrganizationsTestData.AddDepartmentAsync(factory, organization.Id, "Финансы");
        var linked = await ProjectInAsync(organization.Id, development.Id, "Связанный", "LNK");
        var free = await ProjectInAsync(organization.Id, development.Id, "Свободный", "FRE");
        var team = await OrganizationsTestData.AddTeamInAsync(factory, organization.Id, development.Id, "Платформа");
        await OrganizationsTestData.LinkAsync(factory, linked, team.Id);

        var refused = await MoveAsync(organization.Id, (await OrganizationsTestData.NodeOfAsync(factory, linked)).Id, finance.Id);
        var moved = await MoveAsync(organization.Id, (await OrganizationsTestData.NodeOfAsync(factory, free)).Id, finance.Id);

        LinksTestData.AssertRefused(refused, HttpStatusCode.Conflict, "organizations.area.project_has_teams");
        Assert.Equal(HttpStatusCode.OK, moved.Status);
        Assert.Equal(development.Id, (await OrganizationsTestData.NodeOfAsync(factory, linked)).ParentId);
        Assert.Equal(finance.Id, (await OrganizationsTestData.NodeOfAsync(factory, free)).ParentId);
    }

    // US-2 · BR-TD-6: a project node is never moved to the top level, whatever its links.
    [Fact]
    public async Task Move_AProjectToTheTopLevel_Is409NeedsDepartment()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var development = await OrganizationsTestData.AddDepartmentAsync(factory, organization.Id, "Разработка");
        var project = await ProjectInAsync(organization.Id, development.Id, "Мастаск", "MST");
        var node = await OrganizationsTestData.NodeOfAsync(factory, project);

        var answer = await MoveAsync(organization.Id, node.Id, null);

        LinksTestData.AssertRefused(answer, HttpStatusCode.Conflict, "organizations.area.project_needs_department");
        Assert.Equal(development.Id, (await OrganizationsTestData.NodeOfAsync(factory, project)).ParentId);
    }

    // BR-TD-3 against BR-TD-4 · the link first: the move waits on the node, then sees the link and is refused project_has_teams.
    [Fact]
    public async Task Link_VersusMove_LinkFirst_TheMoveIsRefused_NoLinkCrossesADepartment()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var development = await OrganizationsTestData.AddDepartmentAsync(factory, organization.Id, "Разработка");
        var finance = await OrganizationsTestData.AddDepartmentAsync(factory, organization.Id, "Финансы");
        var project = await ProjectInAsync(organization.Id, development.Id, "Мастаск", "MST");
        var node = await OrganizationsTestData.NodeOfAsync(factory, project);
        var team = await OrganizationsTestData.AddTeamInAsync(factory, organization.Id, development.Id, "Платформа");

        var (link, move) = await RaceAsync(
            factory,
            at => LinkTeamsAsync(factory, organization.Id, project, new { teamIds = new[] { team.Id.ToString() } }, gate: at),
            at => MoveAsync(organization.Id, node.Id, finance.Id, at));

        Assert.Equal(HttpStatusCode.NoContent, link.Status);
        LinksTestData.AssertRefused(move, HttpStatusCode.Conflict, "organizations.area.project_has_teams");
        Assert.Equal(development.Id, (await OrganizationsTestData.NodeOfAsync(factory, project)).ParentId);
        Assert.Single(await LinksOfProjectAsync(factory, project));
    }

    // BR-TD-3 against BR-TD-4 · the move first: the link waits on the node, reads the new department and is refused other_department.
    [Fact]
    public async Task Link_VersusMove_MoveFirst_TheLinkIsRefused_NoLinkCrossesADepartment()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var development = await OrganizationsTestData.AddDepartmentAsync(factory, organization.Id, "Разработка");
        var finance = await OrganizationsTestData.AddDepartmentAsync(factory, organization.Id, "Финансы");
        var project = await ProjectInAsync(organization.Id, development.Id, "Мастаск", "MST");
        var node = await OrganizationsTestData.NodeOfAsync(factory, project);
        var team = await OrganizationsTestData.AddTeamInAsync(factory, organization.Id, development.Id, "Платформа");

        var (move, link) = await RaceAsync(
            factory,
            at => MoveAsync(organization.Id, node.Id, finance.Id, at),
            at => LinkProjectsAsync(factory, organization.Id, team.Id, new { projectIds = new[] { project.ToString() } }, gate: at));

        Assert.Equal(HttpStatusCode.OK, move.Status);
        LinksTestData.AssertRefused(link, HttpStatusCode.Conflict, "organizations.team.other_department");
        Assert.Equal(finance.Id, (await OrganizationsTestData.NodeOfAsync(factory, project)).ParentId);
        Assert.Empty(await LinksOfProjectAsync(factory, project));
    }

    private async Task<Guid> ProjectInAsync(Guid organizationId, Guid departmentId, string name, string key)
    {
        var answer = await CreateAsync(organizationId, new { name, key, parentAreaId = departmentId.ToString() });
        Assert.Equal(HttpStatusCode.Created, answer.Status);

        return answer.Body.GetProperty("id").GetGuid();
    }
}
