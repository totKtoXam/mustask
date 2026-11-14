using System.Net;
using System.Text.Json;
using Mustask.Domain.Identity;

namespace Mustask.Api.IntegrationTests.Features.Organizations;

/// <summary>
/// <c>GET /api/organizations/{organizationId}/overview</c> and <c>…/selector-offers</c> (feat-043 api-contract § 1-§ 2; US-2, US-8;
/// BR-NV-9, BR-NV-15), through the real host and a real PostgreSQL. Every test builds its own organization: the database is shared
/// and never emptied.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class NavigationReadsEndpointTests(MustaskApiFactory factory)
{
    private Task<Answer> GetAsync(Guid organizationId, string path, string? token = null) =>
        OrganizationsApi.SendAsync(factory, HttpMethod.Get, $"{OrganizationsApi.Organization(organizationId)}/{path}", token: token ?? OrganizationsTokens.SignedIn());

    private static string[] PropertyNames(JsonElement element) => element.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal).ToArray();

    // US-2 · «Обзор»: exactly the contract's shape, the counts and the first rows by name.
    [Fact]
    public async Task Overview_Shape_IsCountsListsAndMyTeams_DepartmentsOnlyInTheirCount()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        await OrganizationsTestData.AddDepartmentAsync(factory, structure.OrganizationId, "Логистика");
        var node = await OrganizationsTestData.NodeOfAsync(factory, structure.ProjectId);
        await OrganizationsTestData.AddSubAreaAsync(factory, structure.OrganizationId, node.Id, "Склад");

        var answer = await GetAsync(structure.OrganizationId, "overview");

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Equal(
            ["departmentCount", "departments", "myTeams", "projectCount", "projects", "teamCount", "teams"],
            PropertyNames(answer.Body));
        Assert.Equal(2, answer.Body.GetProperty("departmentCount").GetInt32());
        Assert.Equal(1, answer.Body.GetProperty("projectCount").GetInt32());
        Assert.Equal(2, answer.Body.GetProperty("teamCount").GetInt32());
        Assert.Equal(["id", "name"], PropertyNames(answer.Body.GetProperty("departments")[0]));
        Assert.Equal(["id", "key", "name"], PropertyNames(answer.Body.GetProperty("projects")[0]));
        Assert.Equal(["id", "isMember", "name"], PropertyNames(answer.Body.GetProperty("teams")[0]));
    }

    // US-2 · "the team block follows her teams": isMember is the caller's.
    [Fact]
    public async Task Overview_MyTeams_AreTheCallers()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var venus = await OrganizationsTestData.AddUnlinkedTeamAsync(factory, structure.OrganizationId, "Венера");
        var max = await MembershipTestData.NewMemberAsync(factory, structure.OrganizationId, OrganizationRole.Member);
        await MembershipTestData.AddTeamMemberAsync(factory, structure.OrganizationId, venus.Id, max.Id);

        var answer = await GetAsync(structure.OrganizationId, "overview", max.Token);

        var mine = Assert.Single(answer.Body.GetProperty("myTeams").EnumerateArray());
        Assert.Equal(venus.Id, mine.GetProperty("id").GetGuid());
        Assert.True(mine.GetProperty("isMember").GetBoolean());
    }

    // US-8 · the offers: a project's place and its team links, a team's department links.
    [Fact]
    public async Task SelectorOffers_Shape_IsDepartmentsProjectsWithPlaceAndTeams()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var department = await OrganizationsTestData.AddDepartmentAsync(factory, structure.OrganizationId, "Логистика");
        var venus = await OrganizationsTestData.AddUnlinkedTeamAsync(factory, structure.OrganizationId, "Венера");
        await OrganizationsTestData.PlaceTeamAsync(factory, structure.OrganizationId, department.Id, venus.Id);

        var answer = await GetAsync(structure.OrganizationId, "selector-offers");

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Equal(["departments", "projects", "teams"], PropertyNames(answer.Body));
        Assert.Equal(["color", "id", "name"], PropertyNames(answer.Body.GetProperty("departments")[0]));
        var project = answer.Body.GetProperty("projects")[0];
        Assert.Equal(["color", "departmentId", "departmentName", "id", "key", "name", "teamIds"], PropertyNames(project));
        Assert.Equal(JsonValueKind.Null, project.GetProperty("departmentId").ValueKind);
        Assert.Equal(2, project.GetProperty("teamIds").GetArrayLength());
        var team = answer.Body.GetProperty("teams").EnumerateArray().Single(candidate => candidate.GetProperty("id").GetGuid() == venus.Id);
        Assert.Equal(["color", "departmentId", "id", "isMember", "name"], PropertyNames(team));
        Assert.Equal(department.Id, team.GetProperty("departmentId").GetGuid());
    }

    // BR-NV-9 · every member reads (the viewer included); a non-member answers «Организация не найдена», never a 403.
    [Theory]
    [InlineData("overview")]
    [InlineData("selector-offers")]
    public async Task Reads_AreForEveryMember_AndANonMemberGetsOrganizationNotFound(string path)
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var viewer = await MembershipTestData.NewMemberAsync(factory, structure.OrganizationId, OrganizationRole.Viewer);
        var stranger = await MembershipTestData.NewMemberAsync(factory, (await OrganizationsTestData.CreateOrganizationAsync(factory)).Id, OrganizationRole.Admin);

        Assert.Equal(HttpStatusCode.OK, (await GetAsync(structure.OrganizationId, path, viewer.Token)).Status);
        Assert.Equal(HttpStatusCode.NotFound, (await GetAsync(structure.OrganizationId, path, stranger.Token)).Status);
        Assert.Equal(HttpStatusCode.Unauthorized, (await OrganizationsApi.SendAsync(
            factory, HttpMethod.Get, $"{OrganizationsApi.Organization(structure.OrganizationId)}/{path}", token: null)).Status);
    }

    // BR-NV-13 · a deactivated project is in neither read.
    [Fact]
    public async Task Reads_LeaveOutADeactivatedProject()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        await OrganizationsTestData.CreateBareProjectAsync(factory, structure.OrganizationId, "Старый", "OLD", Mustask.Domain.Organizations.ProjectStatus.Deactivated);

        var overview = await GetAsync(structure.OrganizationId, "overview");
        var offers = await GetAsync(structure.OrganizationId, "selector-offers");

        Assert.Equal(1, overview.Body.GetProperty("projectCount").GetInt32());
        Assert.Single(offers.Body.GetProperty("projects").EnumerateArray());
    }
}
