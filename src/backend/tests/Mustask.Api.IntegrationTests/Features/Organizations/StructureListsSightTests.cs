using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Identity;
using Mustask.Domain.Organizations;

namespace Mustask.Api.IntegrationTests.Features.Organizations;

/// <summary>
/// feat-061 (doc-016 US-6 surfaces, part 1; BR-TD-17): every list, selector offer, count and picker that names departments, teams or
/// projects shows exactly the objects of BR-TD-12..-15 for the caller, narrowed before paging and counting, so a total, a limit or a
/// nested list never reveals a hidden one; a visible department shows only its visible children (BR-TD-14); the administrator sees all.
/// <para>The cast is US-6's: department «Логистика» holds A and B, with VENUS (linked to A) and MARS (linked to A and B); department
/// «Финансы» holds C and D, with JUPITER (linked to C and D). Olga is a member of VENUS, Vera holds a project grant on B, Max a
/// department grant on «Финансы»; Ann is the administrator. Every project's «Видим всей организации» is off unless a test turns it on.</para>
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class StructureListsSightTests(MustaskApiFactory factory)
{
    private const int HiddenFiller = 6; // more than «Обзор»'s five rows, all sorted before the visible ones

    private sealed record World(
        Guid OrganizationId,
        MembershipTestData.TestMember Ann,
        Area Logistics,
        Area Finance,
        Project A,
        Project B,
        Project C,
        Project D,
        Area NodeOfD,
        Team Venus,
        Team Mars,
        Team Jupiter);

    private Task<Answer> GetAsync(World w, string path, string token) =>
        OrganizationsApi.SendAsync(factory, HttpMethod.Get, $"{OrganizationsApi.Organization(w.OrganizationId)}/{path}", null, token);

    private async Task<World> WorldAsync()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory, founded: false);
        var ann = await MembershipTestData.NewMemberAsync(factory, organization.Id, OrganizationRole.Admin);
        var logistics = await OrganizationsTestData.AddDepartmentAsync(factory, organization.Id, "Логистика");
        var finance = await OrganizationsTestData.AddDepartmentAsync(factory, organization.Id, "Финансы");
        var a = await ProjectInAsync(organization.Id, logistics, "Проект A", "PRA");
        var b = await ProjectInAsync(organization.Id, logistics, "Проект B", "PRB");
        var c = await ProjectInAsync(organization.Id, finance, "Проект C", "PRC");
        var d = await ProjectInAsync(organization.Id, finance, "Проект D", "PRD");
        var venus = await OrganizationsTestData.AddTeamInAsync(factory, organization.Id, logistics.Id, "VENUS");
        var mars = await OrganizationsTestData.AddTeamInAsync(factory, organization.Id, logistics.Id, "MARS");
        var jupiter = await OrganizationsTestData.AddTeamInAsync(factory, organization.Id, finance.Id, "JUPITER");
        await OrganizationsTestData.LinkAsync(factory, a.Project.Id, venus.Id);
        await OrganizationsTestData.LinkAsync(factory, a.Project.Id, mars.Id);
        await OrganizationsTestData.LinkAsync(factory, b.Project.Id, mars.Id);
        await OrganizationsTestData.LinkAsync(factory, c.Project.Id, jupiter.Id);
        await OrganizationsTestData.LinkAsync(factory, d.Project.Id, jupiter.Id);

        // Hidden filler in a department of its own, named to sort before every visible object: a cut applied before the narrowing would show nothing.
        var closed = await OrganizationsTestData.AddDepartmentAsync(factory, organization.Id, "0 закрытый");
        for (var index = 0; index < HiddenFiller; index++)
        {
            await ProjectInAsync(organization.Id, closed, $"0 скрытый {index}", $"H{index}A");
            await OrganizationsTestData.AddTeamInAsync(factory, organization.Id, closed.Id, $"0 скрытая {index}");
            await OrganizationsTestData.AddDepartmentAsync(factory, organization.Id, $"0 закрытый {index + 1}");
        }

        return new World(organization.Id, ann, logistics, finance, a.Project, b.Project, c.Project, d.Project, d.Root, venus, mars, jupiter);
    }

    private Task<NewProject> ProjectInAsync(Guid organizationId, Area department, string name, string key) =>
        OrganizationsTestData.InUnitOfWorkAsync(factory, async (services, ct) =>
        {
            var projects = services.GetRequiredService<IProjectRepository>();
            var created = Project.Create(organizationId, OrganizationsTestData.Name(name), key, OrganizationsTestData.Now, "seed", "blue", parentAreaId: department.Id);
            projects.Add(created);
            await projects.SaveAsync(ct);

            return created;
        });

    private async Task GrantAsync(ResourceScope scope, World w, Guid objectId, string userId, GrantRole role = GrantRole.Reader) =>
        await OrganizationsTestData.InUnitOfWorkAsync(factory, async (services, ct) =>
        {
            await services.GetRequiredService<IGrantRepository>().AddAsync(scope, w.OrganizationId, objectId, userId, role, "seed", OrganizationsTestData.Now, ct);

            return true;
        });

    private async Task<MembershipTestData.TestMember> OlgaAsync(World w)
    {
        var olga = await MembershipTestData.NewMemberAsync(factory, w.OrganizationId, OrganizationRole.Member);
        await MembershipTestData.AddTeamMemberAsync(factory, w.OrganizationId, w.Venus.Id, olga.Id);

        return olga;
    }

    private async Task<MembershipTestData.TestMember> VeraAsync(World w)
    {
        var vera = await MembershipTestData.NewMemberAsync(factory, w.OrganizationId, OrganizationRole.Viewer);
        await GrantAsync(ResourceScope.Project, w, w.B.Id, vera.Id);

        return vera;
    }

    private async Task<MembershipTestData.TestMember> MaxAsync(World w)
    {
        var max = await MembershipTestData.NewMemberAsync(factory, w.OrganizationId, OrganizationRole.Member);
        await GrantAsync(ResourceScope.Area, w, w.Finance.Id, max.Id);

        return max;
    }

    private static HashSet<Guid> Ids(JsonElement array) => array.EnumerateArray().Select(item => item.GetProperty("id").GetGuid()).ToHashSet();

    private static HashSet<Guid> Of(params Guid[] ids) => ids.ToHashSet();

    private static HashSet<Guid> NamedLike(JsonElement array, string prefix) =>
        array.EnumerateArray().Where(item => item.GetProperty("name").GetString()!.StartsWith(prefix, StringComparison.Ordinal)).Select(item => item.GetProperty("id").GetGuid()).ToHashSet();

    // The set a persona sees through every read, as the cast gives it (US-6 scenarios).
    private sealed record Sight(HashSet<Guid> Projects, HashSet<Guid> Teams, HashSet<Guid> Departments);

    private static Sight OlgaSees(World w) => new(Of(w.A.Id), Of(w.Venus.Id, w.Mars.Id), Of(w.Logistics.Id));

    private static Sight VeraSees(World w) => new(Of(w.B.Id), Of(w.Mars.Id), Of(w.Logistics.Id));

    private static Sight MaxSees(World w) => new(Of(w.C.Id, w.D.Id), Of(w.Jupiter.Id), Of(w.Finance.Id));

    /// <summary>Asserts, for one caller, what every list surface returns: items, counts and the nested lists (BR-TD-17).</summary>
    private async Task AssertSurfacesAsync(World w, string token, Sight expected)
    {
        var offers = await GetAsync(w, "selector-offers", token);
        Assert.Equal(HttpStatusCode.OK, offers.Status);
        Assert.Equal(expected.Departments, Ids(offers.Body.GetProperty("departments")));
        Assert.Equal(expected.Projects, Ids(offers.Body.GetProperty("projects")));
        Assert.Equal(expected.Teams, Ids(offers.Body.GetProperty("teams")));
        foreach (var project in offers.Body.GetProperty("projects").EnumerateArray())
        {
            // a visible project's offered teams are visible teams (a team grant or a membership does not disclose a project, and the reverse holds here)
            Assert.All(project.GetProperty("teamIds").EnumerateArray().Select(id => id.GetGuid()), id => Assert.Contains(id, expected.Teams));
        }

        var overview = await GetAsync(w, "overview", token);
        Assert.Equal(HttpStatusCode.OK, overview.Status);
        Assert.Equal(expected.Departments.Count, overview.Body.GetProperty("departmentCount").GetInt32());
        Assert.Equal(expected.Projects.Count, overview.Body.GetProperty("projectCount").GetInt32());
        Assert.Equal(expected.Teams.Count, overview.Body.GetProperty("teamCount").GetInt32());
        Assert.Equal(expected.Departments, Ids(overview.Body.GetProperty("departments")));
        Assert.Equal(expected.Projects, Ids(overview.Body.GetProperty("projects")));
        Assert.Equal(expected.Teams, Ids(overview.Body.GetProperty("teams")));
        Assert.All(Ids(overview.Body.GetProperty("myTeams")), id => Assert.Contains(id, expected.Teams));

        var projects = await GetAsync(w, "projects", token);
        Assert.Equal(HttpStatusCode.OK, projects.Status);
        Assert.Equal(expected.Projects, Ids(projects.Body));

        var teams = await GetAsync(w, "teams", token);
        Assert.Equal(HttpStatusCode.OK, teams.Status);
        Assert.Equal(expected.Teams, Ids(teams.Body));
        foreach (var team in teams.Body.EnumerateArray())
        {
            // a visible team lists only the projects the caller sees (BR-TD-13: seeing a team does not disclose its projects)
            Assert.All(Ids(team.GetProperty("projects")), id => Assert.Contains(id, expected.Projects));
        }

        var tree = await GetAsync(w, "areas", token);
        Assert.Equal(HttpStatusCode.OK, tree.Status);
        var treeIds = Ids(tree.Body);
        Assert.Equal(expected.Departments, tree.Body.EnumerateArray().Where(node => node.GetProperty("type").GetString() == "department").Select(node => node.GetProperty("id").GetGuid()).ToHashSet());
        Assert.Equal(expected.Projects, tree.Body.EnumerateArray().Where(node => node.GetProperty("type").GetString() == "project").Select(node => node.GetProperty("projectId").GetGuid()).ToHashSet());
        Assert.DoesNotContain(tree.Body.EnumerateArray(), node => node.GetProperty("name").GetString()!.StartsWith("0 ", StringComparison.Ordinal));
        Assert.True(treeIds.Count >= expected.Departments.Count + expected.Projects.Count);

        var writable = await GetAsync(w, "areas/writable-ids", token);
        Assert.Equal(HttpStatusCode.OK, writable.Status);
        Assert.All(writable.Body.EnumerateArray().Select(id => id.GetGuid()), id => Assert.Contains(id, treeIds));
    }

    // US-6 "Sight through a team membership" · BR-TD-17: Olga's every surface shows VENUS, MARS, A and «Логистика» and nothing else,
    // and the hidden filler (six of each kind, sorted first) neither fills «Обзор»'s five rows nor adds to a count.
    [Fact]
    public async Task Olga_AMemberOfVenus_EverySurfaceShowsExactlyHerObjects_AndNoCountRevealsAHiddenOne()
    {
        var w = await WorldAsync();
        var olga = await OlgaAsync(w);

        await AssertSurfacesAsync(w, olga.Token, OlgaSees(w));
    }

    // US-6 "Sight through a project grant".
    [Fact]
    public async Task Vera_AProjectGrantOnB_EverySurfaceShowsB_Mars_AndLogistics_Only()
    {
        var w = await WorldAsync();
        var vera = await VeraAsync(w);

        await AssertSurfacesAsync(w, vera.Token, VeraSees(w));
    }

    // US-6 "Sight through a department grant".
    [Fact]
    public async Task Max_ADepartmentGrantOnFinance_EverySurfaceShowsFinance_Jupiter_CAndD_Only()
    {
        var w = await WorldAsync();
        var max = await MaxAsync(w);

        await AssertSurfacesAsync(w, max.Token, MaxSees(w));
    }

    // US-6 "The administrator sees everything": nothing narrowed, the filler included.
    [Fact]
    public async Task Ann_TheAdministrator_SeesEveryObjectOnEverySurface()
    {
        var w = await WorldAsync();
        var projects = Of(w.A.Id, w.B.Id, w.C.Id, w.D.Id).Count + HiddenFiller;
        var teams = 3 + HiddenFiller;
        var departments = 3 + HiddenFiller;

        var offers = await GetAsync(w, "selector-offers", w.Ann.Token);
        var overview = await GetAsync(w, "overview", w.Ann.Token);
        var projectList = await GetAsync(w, "projects", w.Ann.Token);
        var teamList = await GetAsync(w, "teams", w.Ann.Token);
        var tree = await GetAsync(w, "areas", w.Ann.Token);

        Assert.Equal((departments, projects, teams), (offers.Body.GetProperty("departments").GetArrayLength(), offers.Body.GetProperty("projects").GetArrayLength(), offers.Body.GetProperty("teams").GetArrayLength()));
        Assert.Equal((departments, projects, teams), (overview.Body.GetProperty("departmentCount").GetInt32(), overview.Body.GetProperty("projectCount").GetInt32(), overview.Body.GetProperty("teamCount").GetInt32()));
        Assert.Equal(projects, projectList.Body.GetArrayLength());
        Assert.Equal(teams, teamList.Body.GetArrayLength());
        Assert.Equal(departments + projects, tree.Body.GetArrayLength());
        Assert.Equal(Of(w.A.Id, w.B.Id), Ids(teamList.Body.EnumerateArray().Single(team => team.GetProperty("id").GetGuid() == w.Mars.Id).GetProperty("projects")));
    }

    // BR-TD-17 · «Обзор»: the limit of five rows is taken among the visible, so the hidden filler sorted first does not push Olga's A out.
    [Fact]
    public async Task Overview_TheCutIsAppliedAfterTheNarrowing_NotBefore()
    {
        var w = await WorldAsync();
        var olga = await OlgaAsync(w);

        var overview = await GetAsync(w, "overview", olga.Token);

        Assert.Equal(Of(w.A.Id), Ids(overview.Body.GetProperty("projects")));
        Assert.Equal(Of(w.Logistics.Id), Ids(overview.Body.GetProperty("departments")));
        Assert.Empty(NamedLike(overview.Body.GetProperty("teams"), "0 "));
    }

    // US-6 "Seeing a team does not disclose its projects" · BR-TD-13: with the switch off on D's node Max sees JUPITER and C, no longer D,
    // on the team list's nested projects, the team's own project list, «Проекты» and the offers.
    [Fact]
    public async Task SeeingATeamDoesNotDiscloseItsProjects_OnTheTeamListsNestedProjects_AndTheTeamsOwnList()
    {
        var w = await WorldAsync();
        var max = await MaxAsync(w);
        var off = await OrganizationsApi.SendAsync(factory, HttpMethod.Put, $"{OrganizationsApi.Areas(w.OrganizationId)}/{w.NodeOfD.Id}/inherits-access", new { value = false }, w.Ann.Token);
        Assert.Equal(HttpStatusCode.OK, off.Status);

        var teams = await GetAsync(w, "teams", max.Token);
        var own = await GetAsync(w, $"teams/{w.Jupiter.Id}/projects", max.Token);
        var projects = await GetAsync(w, "projects", max.Token);
        var offers = await GetAsync(w, "selector-offers", max.Token);
        var overview = await GetAsync(w, "overview", max.Token);

        Assert.Equal(Of(w.C.Id), Ids(teams.Body.EnumerateArray().Single().GetProperty("projects")));
        Assert.Equal(Of(w.C.Id), Ids(own.Body));
        Assert.Equal(Of(w.C.Id), Ids(projects.Body));
        Assert.Equal(Of(w.C.Id), Ids(offers.Body.GetProperty("projects")));
        Assert.Equal(1, overview.Body.GetProperty("projectCount").GetInt32());
    }

    // US-6 "«Видим всей организации»" · BR-TD-14: with D's flag on, Olga sees D, JUPITER (linked to D) and «Финансы», which shows D alone, not C;
    // the department's page lists only its visible children, its team list only the visible teams.
    [Fact]
    public async Task TheFlagOnD_ShowsDJupiterAndFinanceToOlga_AndFinanceShowsItsVisibleChildrenOnly()
    {
        var w = await WorldAsync();
        var olga = await OlgaAsync(w);
        var on = await OrganizationsApi.SendAsync(factory, HttpMethod.Put, $"{OrganizationsApi.Project(w.OrganizationId, w.D.Id)}/visibility", new { visibleToOrganization = true }, w.Ann.Token);
        Assert.Equal(HttpStatusCode.OK, on.Status);

        var projects = await GetAsync(w, "projects", olga.Token);
        var offers = await GetAsync(w, "selector-offers", olga.Token);
        var finance = await GetAsync(w, $"areas/{w.Finance.Id}", olga.Token);
        var financeTeams = await GetAsync(w, $"areas/{w.Finance.Id}/teams", olga.Token);
        var tree = await GetAsync(w, "areas", olga.Token);

        Assert.Equal(Of(w.A.Id, w.D.Id), Ids(projects.Body));
        Assert.Equal(Of(w.Logistics.Id, w.Finance.Id), Ids(offers.Body.GetProperty("departments")));
        Assert.Equal(Of(w.Venus.Id, w.Mars.Id, w.Jupiter.Id), Ids(offers.Body.GetProperty("teams")));
        Assert.Equal(HttpStatusCode.OK, finance.Status);
        Assert.Equal([w.NodeOfD.Id], finance.Body.GetProperty("children").EnumerateArray().Select(child => child.GetProperty("id").GetGuid()).ToList());
        Assert.Equal(Of(w.Jupiter.Id), Ids(Items(financeTeams)));
        Assert.DoesNotContain(tree.Body.EnumerateArray(), node => node.GetProperty("projectId").ValueKind != JsonValueKind.Null && node.GetProperty("projectId").GetGuid() == w.C.Id);
    }

    // US-6 · BR-TD-14 on a department's page and its team list: Olga's «Логистика» shows A's node, not B's, and VENUS and MARS;
    // Vera's shows B's node and MARS. The nested lists of a project and of a team follow.
    [Fact]
    public async Task ADepartmentsPageAndTeamList_ShowOnlyTheVisibleChildren_AndTheNestedProjectAndTeamListsFollow()
    {
        var w = await WorldAsync();
        var olga = await OlgaAsync(w);
        var vera = await VeraAsync(w);
        var nodeOfA = await OrganizationsTestData.NodeOfAsync(factory, w.A.Id);
        var nodeOfB = await OrganizationsTestData.NodeOfAsync(factory, w.B.Id);

        var olgaPage = await GetAsync(w, $"areas/{w.Logistics.Id}", olga.Token);
        var veraPage = await GetAsync(w, $"areas/{w.Logistics.Id}", vera.Token);
        var olgaTeams = await GetAsync(w, $"areas/{w.Logistics.Id}/teams", olga.Token);
        var veraTeams = await GetAsync(w, $"areas/{w.Logistics.Id}/teams", vera.Token);
        var olgaMars = await GetAsync(w, $"teams/{w.Mars.Id}/projects", olga.Token);
        var veraMars = await GetAsync(w, $"teams/{w.Mars.Id}/projects", vera.Token);
        var olgaLinks = await GetAsync(w, $"projects/{w.A.Id}/team-links", olga.Token);
        var olgaProjectTeams = await GetAsync(w, $"projects/{w.A.Id}/teams", olga.Token);

        Assert.Equal([nodeOfA.Id], olgaPage.Body.GetProperty("children").EnumerateArray().Select(child => child.GetProperty("id").GetGuid()).ToList());
        Assert.Equal([nodeOfB.Id], veraPage.Body.GetProperty("children").EnumerateArray().Select(child => child.GetProperty("id").GetGuid()).ToList());
        Assert.Equal(Of(w.Venus.Id, w.Mars.Id), Ids(Items(olgaTeams)));
        Assert.Equal(Of(w.Mars.Id), Ids(Items(veraTeams)));
        Assert.Equal(Of(w.A.Id), Ids(olgaMars.Body));
        Assert.Equal(Of(w.B.Id), Ids(veraMars.Body));
        Assert.Equal(Of(w.Venus.Id, w.Mars.Id), Ids(olgaLinks.Body));
        Assert.Equal(Of(w.Venus.Id, w.Mars.Id), Ids(olgaProjectTeams.Body));
    }

    private static JsonElement Items(Answer answer) =>
        answer.Body.ValueKind == JsonValueKind.Object ? answer.Body.GetProperty("items") : answer.Body;

    // US-6 "Sight changes on the next request": Olga added to JUPITER sees it, C, D and «Финансы» in the lists; removed from VENUS, A leaves them.
    [Fact]
    public async Task SightChanges_ApplyToTheListsOnTheNextRequest()
    {
        var w = await WorldAsync();
        var olga = await OlgaAsync(w);

        await MembershipTestData.AddTeamMemberAsync(factory, w.OrganizationId, w.Jupiter.Id, olga.Id);
        var after = await GetAsync(w, "selector-offers", olga.Token);

        Assert.Equal(Of(w.A.Id, w.C.Id, w.D.Id), Ids(after.Body.GetProperty("projects")));
        Assert.Equal(Of(w.Logistics.Id, w.Finance.Id), Ids(after.Body.GetProperty("departments")));

        var removal = await OrganizationsApi.SendAsync(factory, HttpMethod.Delete, $"{OrganizationsApi.OrgTeams(w.OrganizationId)}/{w.Venus.Id}/members/{olga.Id}", null, w.Ann.Token);
        Assert.Equal(HttpStatusCode.NoContent, removal.Status);
        var removed = await GetAsync(w, "selector-offers", olga.Token);

        Assert.Equal(Of(w.C.Id, w.D.Id), Ids(removed.Body.GetProperty("projects")));
        Assert.Equal(Of(w.Finance.Id), Ids(removed.Body.GetProperty("departments")));
    }

    // QA (backend): a hidden object's own addresses answer as one that names nothing, on every list route that names it (BR-TD-14, BR-TD-17).
    [Fact]
    public async Task HiddenObjectsListRoutes_AnswerNotFound_AndAnAreaDetailOfAHiddenAreaDisclosesNothing()
    {
        var w = await WorldAsync();
        var olga = await OlgaAsync(w);

        foreach (var path in new[] { $"areas/{w.Finance.Id}", $"areas/{w.Finance.Id}/teams", $"areas/{w.Finance.Id}/deletion", $"areas/{w.NodeOfD.Id}" })
        {
            var answer = await GetAsync(w, path, olga.Token);
            Assert.Equal((HttpStatusCode.NotFound, "organizations.area.not_found"), (answer.Status, answer.ErrorCode));
        }

        foreach (var path in new[] { $"projects/{w.C.Id}/teams", $"projects/{w.C.Id}/team-links", $"projects/{w.C.Id}/areas" })
        {
            var answer = await GetAsync(w, path, olga.Token);
            Assert.Equal((HttpStatusCode.NotFound, "organizations.project.not_found"), (answer.Status, answer.ErrorCode));
        }

        var team = await GetAsync(w, $"teams/{w.Jupiter.Id}/projects", olga.Token);
        Assert.Equal((HttpStatusCode.NotFound, "organizations.team.not_found"), (team.Status, team.ErrorCode));
    }

    // QA (backend): a deactivated project leaves the offers and counts of the caller who saw it; its teams stay visible: VENUS by membership, MARS through the deactivated A (m-1).
    [Fact]
    public async Task ADeactivatedVisibleProject_LeavesOffersAndCounts_WithoutHidingOrRevealingAnythingElse()
    {
        var w = await WorldAsync();
        var olga = await OlgaAsync(w);
        await OrganizationsTestData.SetProjectStatusAsync(factory, w.OrganizationId, w.A.Id, ProjectStatus.Deactivated);

        var offers = await GetAsync(w, "selector-offers", olga.Token);
        var overview = await GetAsync(w, "overview", olga.Token);
        var projects = await GetAsync(w, "projects", olga.Token);

        Assert.Empty(Ids(offers.Body.GetProperty("projects")));
        // feat-041's rule keeps a team seen through a deactivated project (it still stands in the sight data): recorded as qa.md m-1.
        Assert.Equal(Of(w.Venus.Id, w.Mars.Id), Ids(offers.Body.GetProperty("teams")));
        Assert.Equal(0, overview.Body.GetProperty("projectCount").GetInt32());
        Assert.Equal(2, overview.Body.GetProperty("teamCount").GetInt32());
        Assert.Empty(Ids(projects.Body));
    }

    // BR-TD-17 · a picker: the «linkable» filter of the pickers runs over the sight-narrowed set, never offering a hidden object.
    // The caller holds the link codes by grants (Manager on «Логистика», VENUS and MARS), so the lists are not empty; the filler,
    // «Финансы», C, D and JUPITER are hidden from her and absent from every picker.
    [Fact]
    public async Task TheLinkPickers_OfferExactlyTheVisibleLinkableObjects_ToAHolderOfTheLinkCodes()
    {
        var w = await WorldAsync();
        var manager = await MembershipTestData.NewMemberAsync(factory, w.OrganizationId, OrganizationRole.Member);
        await GrantAsync(ResourceScope.Area, w, w.Logistics.Id, manager.Id, GrantRole.Manager);
        await GrantAsync(ResourceScope.Team, w, w.Venus.Id, manager.Id, GrantRole.Manager);
        await GrantAsync(ResourceScope.Team, w, w.Mars.Id, manager.Id, GrantRole.Manager);

        var projects = await GetAsync(w, "projects?linkableFor=team", manager.Token);
        var teams = await GetAsync(w, "teams?linkableFor=project", manager.Token);
        var tree = await GetAsync(w, "areas?linkableFor=team", manager.Token);

        Assert.Equal(HttpStatusCode.OK, projects.Status);
        Assert.Equal(HttpStatusCode.OK, teams.Status);
        Assert.Equal(HttpStatusCode.OK, tree.Status);
        Assert.Equal(Of(w.A.Id, w.B.Id), Ids(projects.Body));
        Assert.Equal(Of(w.Venus.Id, w.Mars.Id), Ids(teams.Body));
        Assert.Equal(Of(w.Logistics.Id), Ids(tree.Body));
    }
}
