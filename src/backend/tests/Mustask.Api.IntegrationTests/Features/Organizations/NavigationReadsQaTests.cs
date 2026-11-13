using System.Net;
using System.Text.Json;
using Mustask.Domain.Identity;
using Mustask.Domain.Organizations;

namespace Mustask.Api.IntegrationTests.Features.Organizations;

/// <summary>
/// QA pass over feat-043's two reads (US-2, US-8, BR-NV-9, BR-NV-13, BR-NV-15, BR-NV-17): the story's cast built through the real
/// writes (placement by the area move, team delete, link), the contract's null and empty cases, address handling and ordering.
/// Every test builds its own organization.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class NavigationReadsQaTests(MustaskApiFactory factory)
{
    private Task<Answer> GetAsync(Guid organizationId, string path, string? token = null) =>
        OrganizationsApi.SendAsync(factory, HttpMethod.Get, $"{OrganizationsApi.Organization(organizationId)}/{path}", token: token ?? OrganizationsTokens.SignedIn());

    private async Task PlaceAsync(Guid organizationId, Guid projectId, Guid? departmentId)
    {
        var node = await OrganizationsTestData.NodeOfAsync(factory, projectId);
        var moved = await OrganizationsApi.SendAsync(
            factory, HttpMethod.Put, $"{OrganizationsApi.Areas(organizationId)}/{node.Id}/parent",
            new { parentId = departmentId?.ToString() }, OrganizationsTokens.SignedIn());
        Assert.Equal(HttpStatusCode.OK, moved.Status);
    }

    private static string[] Names(JsonElement array) => array.EnumerateArray().Select(row => row.GetProperty("name").GetString()!).ToArray();

    private static JsonElement Row(JsonElement array, string name) => array.EnumerateArray().Single(row => row.GetProperty("name").GetString() == name);

    private static Guid[] Ids(JsonElement array) => array.EnumerateArray().Select(id => id.GetGuid()).Order().ToArray();

    // US-2 "The counts count each object once, active only" + US-8 fit data, with the story's own cast through real writes.
    [Fact]
    public async Task Cast_OfUs2_CountsAndOffersMatchTheStory()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var o = organization.Id;
        var logistics = await OrganizationsTestData.AddDepartmentAsync(factory, o, "Логистика");
        var finance = await OrganizationsTestData.AddDepartmentAsync(factory, o, "Финансы");
        var a = await OrganizationsTestData.CreateBareProjectAsync(factory, o, "A", "AAA");
        var b = await OrganizationsTestData.CreateBareProjectAsync(factory, o, "B", "BBB");
        var c = await OrganizationsTestData.CreateBareProjectAsync(factory, o, "C", "CCC");
        await OrganizationsTestData.CreateBareProjectAsync(factory, o, "D", "DDD");
        var e = await OrganizationsTestData.CreateBareProjectAsync(factory, o, "E", "EEE");
        await PlaceAsync(o, a.Project.Id, logistics.Id);
        await PlaceAsync(o, b.Project.Id, logistics.Id);
        await PlaceAsync(o, c.Project.Id, finance.Id);
        await PlaceAsync(o, e.Project.Id, finance.Id);
        await OrganizationsTestData.AddSubAreaAsync(factory, o, a.Root.Id, "Склад");
        await OrganizationsTestData.AddSubAreaAsync(factory, o, a.Root.Id, "Транспорт");

        var mars = await OrganizationsTestData.AddUnlinkedTeamAsync(factory, o, "MARS");
        var venus = await OrganizationsTestData.AddUnlinkedTeamAsync(factory, o, "VENUS");
        await OrganizationsTestData.AddUnlinkedTeamAsync(factory, o, "JUPITER");

        foreach (var project in new[] { a, b, c, e })
        {
            await OrganizationsTestData.LinkAsync(factory, project.Project.Id, mars.Id);
        }

        await OrganizationsTestData.LinkAsync(factory, a.Project.Id, venus.Id);
        await OrganizationsTestData.PlaceTeamAsync(factory, o, logistics.Id, mars.Id);
        await OrganizationsTestData.PlaceTeamAsync(factory, o, logistics.Id, venus.Id);
        await OrganizationsTestData.SetProjectStatusAsync(factory, o, e.Project.Id, ProjectStatus.Deactivated);

        var olga = await MembershipTestData.NewMemberAsync(factory, o, OrganizationRole.Member);
        await MembershipTestData.AddTeamMemberAsync(factory, o, venus.Id, olga.Id);

        var overview = (await GetAsync(o, "overview", olga.Token)).Body;
        Assert.Equal((2, 4, 3), (overview.GetProperty("departmentCount").GetInt32(), overview.GetProperty("projectCount").GetInt32(), overview.GetProperty("teamCount").GetInt32()));
        Assert.Equal(["Логистика", "Финансы"], Names(overview.GetProperty("departments")));
        Assert.Equal(["A", "B", "C", "D"], Names(overview.GetProperty("projects")));
        Assert.Equal(["JUPITER", "MARS", "VENUS"], Names(overview.GetProperty("teams")));
        Assert.Equal(["VENUS"], Names(overview.GetProperty("myTeams")));

        var offers = (await GetAsync(o, "selector-offers", olga.Token)).Body;
        Assert.Equal(["Логистика", "Финансы"], Names(offers.GetProperty("departments")));
        var projects = offers.GetProperty("projects");
        Assert.Equal(["A", "B", "C", "D"], Names(projects));
        Assert.Equal(logistics.Id, Row(projects, "A").GetProperty("departmentId").GetGuid());
        Assert.Equal("Логистика", Row(projects, "B").GetProperty("departmentName").GetString());
        Assert.Equal(finance.Id, Row(projects, "C").GetProperty("departmentId").GetGuid());
        Assert.Equal(JsonValueKind.Null, Row(projects, "D").GetProperty("departmentName").ValueKind);
        Assert.Equal(new[] { mars.Id, venus.Id }.Order(), Ids(Row(projects, "A").GetProperty("teamIds")));
        Assert.Equal([mars.Id], Ids(Row(projects, "C").GetProperty("teamIds")));
        Assert.Empty(Row(projects, "D").GetProperty("teamIds").EnumerateArray());
        var teams = offers.GetProperty("teams");
        Assert.True(Row(teams, "VENUS").GetProperty("isMember").GetBoolean());
        Assert.False(Row(teams, "MARS").GetProperty("isMember").GetBoolean());
        Assert.Equal(logistics.Id, Row(teams, "MARS").GetProperty("departmentId").GetGuid());
        Assert.Equal(logistics.Id, Row(teams, "JUPITER").GetProperty("departmentId").GetGuid());
    }

    // BR-NV-17 / US-7 · the offers follow the writes: a project moved to the top level loses its place, a deleted team vanishes everywhere.
    [Fact]
    public async Task Offers_FollowAMoveAndATeamDelete_NothingStale()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var o = structure.OrganizationId;
        var department = await OrganizationsTestData.AddDepartmentAsync(factory, o, "Логистика");
        await PlaceAsync(o, structure.ProjectId, department.Id);
        await OrganizationsTestData.PlaceTeamAsync(factory, o, department.Id, structure.Platform.Id);

        var before = (await GetAsync(o, "selector-offers")).Body;
        Assert.Equal(department.Id, before.GetProperty("projects")[0].GetProperty("departmentId").GetGuid());

        await PlaceAsync(o, structure.ProjectId, null);
        await OrganizationsTestData.DeleteTeamAsync(factory, structure.ProjectId, structure.Platform.Id);

        var offers = (await GetAsync(o, "selector-offers")).Body;
        var overview = (await GetAsync(o, "overview")).Body;

        Assert.Equal(JsonValueKind.Null, offers.GetProperty("projects")[0].GetProperty("departmentId").ValueKind);
        Assert.DoesNotContain(structure.Platform.Id, offers.GetProperty("teams").EnumerateArray().Select(t => t.GetProperty("id").GetGuid()));
        Assert.DoesNotContain(structure.Platform.Id, Ids(offers.GetProperty("projects")[0].GetProperty("teamIds")));
        Assert.Equal(1, overview.GetProperty("teamCount").GetInt32());
        Assert.Equal(2, overview.GetProperty("departmentCount").GetInt32());
    }

    // BR-NV-13 · a restored project returns to both reads with its place and links as they were.
    [Fact]
    public async Task RestoredProject_ReturnsWithItsPlaceAndLinks()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var o = structure.OrganizationId;
        var department = await OrganizationsTestData.AddDepartmentAsync(factory, o, "Логистика");
        await PlaceAsync(o, structure.ProjectId, department.Id);
        await OrganizationsTestData.SetProjectStatusAsync(factory, o, structure.ProjectId, ProjectStatus.Deactivated);

        var gone = (await GetAsync(o, "selector-offers")).Body;
        Assert.Empty(gone.GetProperty("projects").EnumerateArray());
        Assert.Equal(0, (await GetAsync(o, "overview")).Body.GetProperty("projectCount").GetInt32());

        await OrganizationsTestData.SetProjectStatusAsync(factory, o, structure.ProjectId, ProjectStatus.Active);

        var back = (await GetAsync(o, "selector-offers")).Body.GetProperty("projects")[0];
        Assert.Equal(department.Id, back.GetProperty("departmentId").GetGuid());
        Assert.Equal(2, back.GetProperty("teamIds").GetArrayLength());
    }

    // US-2 "Empty states": an organization with nothing answers zero counts and empty arrays, never null.
    [Fact]
    public async Task EmptyOrganization_AnswersZeroCountsAndEmptyArrays()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);

        var overview = (await GetAsync(organization.Id, "overview")).Body;
        var offers = (await GetAsync(organization.Id, "selector-offers")).Body;

        Assert.Equal((0, 0, 0), (overview.GetProperty("departmentCount").GetInt32(), overview.GetProperty("projectCount").GetInt32(), overview.GetProperty("teamCount").GetInt32()));

        foreach (var name in new[] { "departments", "projects", "teams", "myTeams" })
        {
            Assert.Equal(JsonValueKind.Array, overview.GetProperty(name).ValueKind);
            Assert.Equal(0, overview.GetProperty(name).GetArrayLength());
        }

        foreach (var name in new[] { "departments", "projects", "teams" })
        {
            Assert.Equal(0, offers.GetProperty(name).GetArrayLength());
        }
    }

    // mod-021 BR-TD-1: a team stands in a department, so an organization with teams has at least the department the helpers made; its projects still have no place.
    [Fact]
    public async Task OrganizationWithoutDepartments_CountsZero_ProjectsHaveNoPlace()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);

        var overview = (await GetAsync(structure.OrganizationId, "overview")).Body;
        var offers = (await GetAsync(structure.OrganizationId, "selector-offers")).Body;

        Assert.Equal(1, overview.GetProperty("departmentCount").GetInt32());
        Assert.Equal(1, overview.GetProperty("departments").GetArrayLength());
        Assert.Equal(JsonValueKind.Null, offers.GetProperty("projects")[0].GetProperty("departmentId").ValueKind);
    }

    // Isolation · another organization's rows never leak into the counts, rows or links.
    [Fact]
    public async Task OtherOrganizationsRows_NeverAppear()
    {
        var mine = await OrganizationsTestData.CreateStructureAsync(factory);
        var theirs = await OrganizationsTestData.CreateStructureAsync(factory);
        await OrganizationsTestData.AddDepartmentAsync(factory, theirs.OrganizationId, "Чужой департамент");

        var overview = (await GetAsync(mine.OrganizationId, "overview")).Body;
        var offers = (await GetAsync(mine.OrganizationId, "selector-offers")).Body;

        Assert.Equal((1, 1, 2), (overview.GetProperty("departmentCount").GetInt32(), overview.GetProperty("projectCount").GetInt32(), overview.GetProperty("teamCount").GetInt32()));
        Assert.Equal(1, offers.GetProperty("projects").GetArrayLength());
        Assert.Equal(2, offers.GetProperty("teams").GetArrayLength());
        Assert.Equal(mine.ProjectId, offers.GetProperty("projects")[0].GetProperty("id").GetGuid());
        Assert.Equal(1, offers.GetProperty("departments").GetArrayLength());
    }

    // Order · Russian collation, letters before letter case, then id; counts above five are cut to five rows.
    [Fact]
    public async Task Overview_OrdersByRussianCollation_CaseNotDeciding_AndCutsToFive()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var names = new[] { "яблоко", "Арбуз", "бета", "Ёж", "еж2", "Альфа", "арбуз2" };

        for (var i = 0; i < names.Length; i++)
        {
            await OrganizationsTestData.CreateBareProjectAsync(factory, organization.Id, names[i], $"K{i}");
            await OrganizationsTestData.AddUnlinkedTeamAsync(factory, organization.Id, names[i]);
        }

        var overview = (await GetAsync(organization.Id, "overview")).Body;
        var offers = (await GetAsync(organization.Id, "selector-offers")).Body;

        Assert.Equal(7, overview.GetProperty("projectCount").GetInt32());
        Assert.Equal(["Альфа", "Арбуз", "арбуз2", "бета", "Ёж"], Names(overview.GetProperty("projects")));
        Assert.Equal(["Альфа", "Арбуз", "арбуз2", "бета", "Ёж"], Names(overview.GetProperty("teams")));
        Assert.Equal(["Альфа", "Арбуз", "арбуз2", "бета", "Ёж", "еж2", "яблоко"], Names(offers.GetProperty("projects")));
    }

    // BR-M1-20 · a malformed organization id answers the level's not-found; an upper-case id is the same organization.
    [Theory]
    [InlineData("overview")]
    [InlineData("selector-offers")]
    public async Task MalformedOrganizationId_IsNotFound_UpperCaseIdIsTheSame(string path)
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var token = OrganizationsTokens.SignedIn();

        var malformed = await OrganizationsApi.SendAsync(factory, HttpMethod.Get, $"/api/organizations/not-a-guid/{path}", token: token);
        var upper = await OrganizationsApi.SendAsync(
            factory, HttpMethod.Get, $"/api/organizations/{structure.OrganizationId.ToString().ToUpperInvariant()}/{path}", token: token);

        Assert.Equal(HttpStatusCode.NotFound, malformed.Status);
        Assert.Equal("organizations.organization.not_found", malformed.ErrorCode);
        Assert.Equal(HttpStatusCode.OK, upper.Status);
    }

    // api-contract § 0 · a deactivated organization answers not-found to its own administrator.
    [Theory]
    [InlineData("overview")]
    [InlineData("selector-offers")]
    public async Task DeactivatedOrganization_IsNotFound(string path)
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        await OrganizationsTestData.SetOrganizationStatusAsync(factory, structure.OrganizationId, OrganizationStatus.Deactivated);

        var answer = await GetAsync(structure.OrganizationId, path);

        Assert.Equal(HttpStatusCode.NotFound, answer.Status);
        Assert.Equal("organizations.organization.not_found", answer.ErrorCode);
    }

    // Permissions · each role reads both, and the caller's flag is the caller's own, not another member's.
    [Fact]
    public async Task EveryRoleReads_AndIsMemberIsPerCaller()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var o = structure.OrganizationId;
        var venus = await OrganizationsTestData.AddUnlinkedTeamAsync(factory, o, "Венера");
        var olga = await MembershipTestData.NewMemberAsync(factory, o, OrganizationRole.Member);
        var vera = await MembershipTestData.NewMemberAsync(factory, o, OrganizationRole.Viewer);
        await MembershipTestData.AddTeamMemberAsync(factory, o, venus.Id, olga.Id);

        foreach (var path in new[] { "overview", "selector-offers" })
        {
            Assert.Equal(HttpStatusCode.OK, (await GetAsync(o, path, olga.Token)).Status);
            Assert.Equal(HttpStatusCode.OK, (await GetAsync(o, path, vera.Token)).Status);
        }

        var veraOverview = (await GetAsync(o, "overview", vera.Token)).Body;
        Assert.Equal(0, veraOverview.GetProperty("myTeams").GetArrayLength());
        Assert.All(veraOverview.GetProperty("teams").EnumerateArray(), team => Assert.False(team.GetProperty("isMember").GetBoolean()));
        var veraOffers = (await GetAsync(o, "selector-offers", vera.Token)).Body;
        Assert.All(veraOffers.GetProperty("teams").EnumerateArray(), team => Assert.False(team.GetProperty("isMember").GetBoolean()));
        Assert.True(Row((await GetAsync(o, "selector-offers", olga.Token)).Body.GetProperty("teams"), "Венера").GetProperty("isMember").GetBoolean());
    }
}
