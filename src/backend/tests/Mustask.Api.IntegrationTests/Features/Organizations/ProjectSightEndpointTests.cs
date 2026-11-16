using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Mustask.Api.IntegrationTests.Features.WorkItems;
using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Identity;
using Mustask.Domain.Organizations;

namespace Mustask.Api.IntegrationTests.Features.Organizations;

/// <summary>
/// feat-041 (doc-016 US-6 core, BR-TD-12..-16, OQ-5 A): a non-administrator sees a project through a reaching grant, a linked
/// team's membership or «Видим всей организации»; a hidden project, team or department answers its «не найден» before any 403;
/// the organization role applies on what one sees; sight changes on the next request; the flag is written under
/// <c>organizations.visibility.write</c>; the assignee candidates are those who see the project.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class ProjectSightEndpointTests(MustaskApiFactory factory)
{
    private const string ProjectNotFound = "organizations.project.not_found";
    private const string TeamNotFound = "organizations.team.not_found";
    private const string AreaNotFound = "organizations.area.not_found";

    private Task<Answer> SendAsync(HttpMethod method, string path, object? body, string? token) =>
        OrganizationsApi.SendAsync(factory, method, path, body, token);

    private async Task SeedAsync(ResourceScope scope, Guid organizationId, Guid objectId, string userId, GrantRole role) =>
        await OrganizationsTestData.InUnitOfWorkAsync(factory, async (services, ct) =>
        {
            await services.GetRequiredService<IGrantRepository>().AddAsync(scope, organizationId, objectId, userId, role, "seed", OrganizationsTestData.Now, ct);

            return true;
        });

    /// <summary>Ann (administrator); department «Логистика» with project A (flag off) and team VENUS linked to A; department «Финансы» with project C (flag off) and team JUPITER linked to C.</summary>
    private sealed record World(
        Guid OrganizationId,
        MembershipTestData.TestMember Ann,
        Area Logistics,
        Area Finance,
        Project A,
        Area NodeOfA,
        Project C,
        Team Venus,
        Team Jupiter);

    private async Task<World> WorldAsync(bool flagOnA = false)
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory, founded: false);
        var ann = await MembershipTestData.NewMemberAsync(factory, organization.Id, OrganizationRole.Admin);
        var logistics = await OrganizationsTestData.AddDepartmentAsync(factory, organization.Id, "Логистика");
        var finance = await OrganizationsTestData.AddDepartmentAsync(factory, organization.Id, "Финансы");
        var a = await ProjectInAsync(organization.Id, logistics, "Проект A", "PRA", flagOnA);
        var c = await ProjectInAsync(organization.Id, finance, "Проект C", "PRC", visible: false);
        var venus = await OrganizationsTestData.AddTeamInAsync(factory, organization.Id, logistics.Id, "VENUS");
        var jupiter = await OrganizationsTestData.AddTeamInAsync(factory, organization.Id, finance.Id, "JUPITER");
        await OrganizationsTestData.LinkAsync(factory, a.Project.Id, venus.Id);
        await OrganizationsTestData.LinkAsync(factory, c.Project.Id, jupiter.Id);

        return new World(organization.Id, ann, logistics, finance, a.Project, a.Root, c.Project, venus, jupiter);
    }

    private Task<NewProject> ProjectInAsync(Guid organizationId, Area department, string name, string key, bool visible) =>
        OrganizationsTestData.InUnitOfWorkAsync(factory, async (services, ct) =>
        {
            var projects = services.GetRequiredService<IProjectRepository>();
            var created = Project.Create(organizationId, OrganizationsTestData.Name(name), key, OrganizationsTestData.Now, "seed", "blue", parentAreaId: department.Id);
            created.Project.SetVisibleToOrganization(visible);
            projects.Add(created);
            await projects.SaveAsync(ct);

            return created;
        });

    private static string ProjectPath(World w, Project project) => OrganizationsApi.Project(w.OrganizationId, project.Id);

    private static string TeamPath(World w, Team team) => $"{OrganizationsApi.OrgTeams(w.OrganizationId)}/{team.Id}";

    private static string AreaPath(World w, Area area) => $"{OrganizationsApi.Areas(w.OrganizationId)}/{area.Id}";

    private async Task<MembershipTestData.TestMember> MemberAsync(World w, OrganizationRole role = OrganizationRole.Member) =>
        await MembershipTestData.NewMemberAsync(factory, w.OrganizationId, role);

    // US-6 · "A hidden object does not exist": a member with no grant and no team opens A's address, A's items and a rename: «Проект не найден.»
    // each, and a viewer's write answers the same, never «Недостаточно прав» (ADR-0029 § 8 item 3 before item 5).
    [Fact]
    public async Task AHiddenProject_AnswersNotFound_ForReadAndForWrite_BeforeAny403()
    {
        var w = await WorldAsync();
        var member = await MemberAsync(w);
        var viewer = await MemberAsync(w, OrganizationRole.Viewer);
        var path = ProjectPath(w, w.A);

        var read = await SendAsync(HttpMethod.Get, path, null, member.Token);
        var items = await SendAsync(HttpMethod.Get, $"{path}/workitems", null, member.Token);
        var viewerRename = await SendAsync(HttpMethod.Put, $"{path}/name", new { name = "Другое" }, viewer.Token);
        var viewerVisibility = await SendAsync(HttpMethod.Put, $"{path}/visibility", new { visibleToOrganization = true }, viewer.Token);

        Assert.All(new[] { read, items, viewerRename, viewerVisibility }, answer => Assert.Equal((HttpStatusCode.NotFound, ProjectNotFound), (answer.Status, answer.ErrorCode)));
    }

    // US-6 · "The administrator sees everything": whatever the grants, memberships and settings.
    [Fact]
    public async Task TheAdministrator_SeesEveryProjectTeamAndDepartment()
    {
        var w = await WorldAsync();

        Assert.Equal(HttpStatusCode.OK, (await SendAsync(HttpMethod.Get, ProjectPath(w, w.A), null, w.Ann.Token)).Status);
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(HttpMethod.Get, ProjectPath(w, w.C), null, w.Ann.Token)).Status);
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(HttpMethod.Get, TeamPath(w, w.Jupiter), null, w.Ann.Token)).Status);
        Assert.Equal(HttpStatusCode.OK, (await SendAsync(HttpMethod.Get, AreaPath(w, w.Finance), null, w.Ann.Token)).Status);
    }

    // US-6 · "Sight through a team membership": Olga, a member of VENUS with no grant, sees A (linked), VENUS and «Логистика» (holding both),
    // and not C, JUPITER or «Финансы» — the codes of her organization role apply on A (BR-TD-16).
    [Fact]
    public async Task SightThroughALinkedTeamsMembership_ShowsTheProjectTheTeamAndTheDepartment_AndTheRoleApplies()
    {
        var w = await WorldAsync();
        var olga = await MemberAsync(w);
        await MembershipTestData.AddTeamMemberAsync(factory, w.OrganizationId, w.Venus.Id, olga.Id);

        var a = await SendAsync(HttpMethod.Get, ProjectPath(w, w.A), null, olga.Token);
        var venus = await SendAsync(HttpMethod.Get, TeamPath(w, w.Venus), null, olga.Token);
        var logistics = await SendAsync(HttpMethod.Get, AreaPath(w, w.Logistics), null, olga.Token);
        var c = await SendAsync(HttpMethod.Get, ProjectPath(w, w.C), null, olga.Token);
        var jupiter = await SendAsync(HttpMethod.Get, TeamPath(w, w.Jupiter), null, olga.Token);
        var finance = await SendAsync(HttpMethod.Get, AreaPath(w, w.Finance), null, olga.Token);

        Assert.Equal(HttpStatusCode.OK, a.Status);
        Assert.Equal(HttpStatusCode.OK, venus.Status);
        Assert.Equal(HttpStatusCode.OK, logistics.Status);
        Assert.Contains(a.Body.GetProperty("codes").EnumerateArray().Select(code => code.GetString()), code => code == PermissionCatalog.WorkItemsWorkItemWrite);
        Assert.Equal((HttpStatusCode.NotFound, ProjectNotFound), (c.Status, c.ErrorCode));
        Assert.Equal((HttpStatusCode.NotFound, TeamNotFound), (jupiter.Status, jupiter.ErrorCode));
        Assert.Equal((HttpStatusCode.NotFound, AreaNotFound), (finance.Status, finance.ErrorCode));
    }

    // US-6 · "Sight through a project grant": Vera, «Читатель» on B, sees B and the team linked to it and the department holding it, and changes nothing.
    [Fact]
    public async Task SightThroughAProjectGrant_ShowsTheProject_TheLinkedTeamAndTheDepartment_AndTheViewerChangesNothing()
    {
        var w = await WorldAsync();
        var vera = await MemberAsync(w, OrganizationRole.Viewer);
        await SeedAsync(ResourceScope.Project, w.OrganizationId, w.A.Id, vera.Id, GrantRole.Reader);

        var a = await SendAsync(HttpMethod.Get, ProjectPath(w, w.A), null, vera.Token);
        var venus = await SendAsync(HttpMethod.Get, TeamPath(w, w.Venus), null, vera.Token);
        var logistics = await SendAsync(HttpMethod.Get, AreaPath(w, w.Logistics), null, vera.Token);
        var rename = await SendAsync(HttpMethod.Put, $"{ProjectPath(w, w.A)}/name", new { name = "Другое" }, vera.Token);
        var c = await SendAsync(HttpMethod.Get, ProjectPath(w, w.C), null, vera.Token);

        Assert.Equal(HttpStatusCode.OK, a.Status);
        Assert.Equal(HttpStatusCode.OK, venus.Status);
        Assert.Equal(HttpStatusCode.OK, logistics.Status);
        Assert.Equal((HttpStatusCode.Forbidden, "organizations.access.forbidden"), (rename.Status, rename.ErrorCode));
        Assert.Equal((HttpStatusCode.NotFound, ProjectNotFound), (c.Status, c.ErrorCode));
    }

    // US-6 · "Sight through a department grant" and "Seeing a team does not disclose its projects": Max, «Участник» on «Финансы», sees JUPITER and C;
    // with «Наследовать доступ» off on C's node he sees JUPITER still and no longer C. «Логистика», A, VENUS stay hidden.
    [Fact]
    public async Task SightThroughADepartmentGrant_ReachesItsProjects_UntilTheNodesSwitchIsOff_AndNeverDisclosesALinkedProject()
    {
        var w = await WorldAsync();
        var max = await MemberAsync(w, OrganizationRole.Viewer);
        await SeedAsync(ResourceScope.Area, w.OrganizationId, w.Finance.Id, max.Id, GrantRole.Member);
        var nodeOfC = await OrganizationsTestData.NodeOfAsync(factory, w.C.Id);

        var before = await SendAsync(HttpMethod.Get, ProjectPath(w, w.C), null, max.Token);
        var team = await SendAsync(HttpMethod.Get, TeamPath(w, w.Jupiter), null, max.Token);
        var logistics = await SendAsync(HttpMethod.Get, AreaPath(w, w.Logistics), null, max.Token);
        var a = await SendAsync(HttpMethod.Get, ProjectPath(w, w.A), null, max.Token);
        var off = await SendAsync(HttpMethod.Put, $"{OrganizationsApi.Areas(w.OrganizationId)}/{nodeOfC.Id}/inherits-access", new { value = false }, w.Ann.Token);
        var after = await SendAsync(HttpMethod.Get, ProjectPath(w, w.C), null, max.Token);
        var teamAfter = await SendAsync(HttpMethod.Get, TeamPath(w, w.Jupiter), null, max.Token);

        Assert.Equal(HttpStatusCode.OK, before.Status);
        Assert.Equal(HttpStatusCode.OK, team.Status);
        Assert.Equal((HttpStatusCode.NotFound, AreaNotFound), (logistics.Status, logistics.ErrorCode));
        Assert.Equal((HttpStatusCode.NotFound, ProjectNotFound), (a.Status, a.ErrorCode));
        Assert.Equal(HttpStatusCode.OK, off.Status);
        Assert.Equal((HttpStatusCode.NotFound, ProjectNotFound), (after.Status, after.ErrorCode));
        Assert.Equal(HttpStatusCode.OK, teamAfter.Status);
    }

    // US-6 · "«Видим всей организации»": on for A, everyone sees A, VENUS (linked) and «Логистика» (holding it); off again, they do not, on the next request.
    [Fact]
    public async Task TheFlag_MakesAProjectVisibleToEveryMember_AndSwitchingItOffHidesItOnTheNextRequest()
    {
        var w = await WorldAsync();
        var olga = await MemberAsync(w);
        var visibility = $"{ProjectPath(w, w.A)}/visibility";

        var hidden = await SendAsync(HttpMethod.Get, ProjectPath(w, w.A), null, olga.Token);
        var on = await SendAsync(HttpMethod.Put, visibility, new { visibleToOrganization = true }, w.Ann.Token);
        var shown = await SendAsync(HttpMethod.Get, ProjectPath(w, w.A), null, olga.Token);
        var team = await SendAsync(HttpMethod.Get, TeamPath(w, w.Venus), null, olga.Token);
        var department = await SendAsync(HttpMethod.Get, AreaPath(w, w.Logistics), null, olga.Token);
        var off = await SendAsync(HttpMethod.Put, visibility, new { visibleToOrganization = false }, w.Ann.Token);
        var hiddenAgain = await SendAsync(HttpMethod.Get, ProjectPath(w, w.A), null, olga.Token);

        Assert.Equal((HttpStatusCode.NotFound, ProjectNotFound), (hidden.Status, hidden.ErrorCode));
        Assert.Equal((HttpStatusCode.OK, true), (on.Status, on.Body.GetProperty("visibleToOrganization").GetBoolean()));
        Assert.Equal(HttpStatusCode.OK, shown.Status);
        Assert.True(shown.Body.GetProperty("visibleToOrganization").GetBoolean());
        Assert.Equal(HttpStatusCode.OK, team.Status);
        Assert.Equal(HttpStatusCode.OK, department.Status);
        Assert.Equal((HttpStatusCode.OK, false), (off.Status, off.Body.GetProperty("visibleToOrganization").GetBoolean()));
        Assert.Equal((HttpStatusCode.NotFound, ProjectNotFound), (hiddenAgain.Status, hiddenAgain.ErrorCode));
    }

    // US-6 · "Sight changes on the next request": adding Olga to JUPITER shows JUPITER, C and «Финансы»; removing her hides them again.
    [Fact]
    public async Task ATeamMembershipChange_IsSeenOnTheNextRequest()
    {
        var w = await WorldAsync();
        var olga = await MemberAsync(w);

        var before = await SendAsync(HttpMethod.Get, TeamPath(w, w.Jupiter), null, olga.Token);
        await MembershipTestData.AddTeamMemberAsync(factory, w.OrganizationId, w.Jupiter.Id, olga.Id);
        var team = await SendAsync(HttpMethod.Get, TeamPath(w, w.Jupiter), null, olga.Token);
        var project = await SendAsync(HttpMethod.Get, ProjectPath(w, w.C), null, olga.Token);
        var department = await SendAsync(HttpMethod.Get, AreaPath(w, w.Finance), null, olga.Token);

        Assert.Equal((HttpStatusCode.NotFound, TeamNotFound), (before.Status, before.ErrorCode));
        Assert.Equal(HttpStatusCode.OK, team.Status);
        Assert.Equal(HttpStatusCode.OK, project.Status);
        Assert.Equal(HttpStatusCode.OK, department.Status);
    }

    // US-6 · "A hidden object does not exist": a team's backlog reorder answers «Команда не найдена.» and not «Недостаточно прав»; its sprints and backlog too;
    // a project's team filter naming it answers the same; a department filter, «Область не найдена.»
    [Fact]
    public async Task AHiddenTeamAndDepartment_AnswerNotFound_OnTheirAddressesAndOnAnotherAddressesQuery()
    {
        var w = await WorldAsync(flagOnA: true);
        var olga = await MemberAsync(w);
        var team = TeamPath(w, w.Jupiter);

        var reorder = await SendAsync(HttpMethod.Post, $"{team}/backlogs/story/reorder", new { itemId = "x", position = 1 }, olga.Token);
        var backlog = await SendAsync(HttpMethod.Get, $"{team}/backlogs/story", null, olga.Token);
        var sprints = await SendAsync(HttpMethod.Get, $"{team}/sprints", null, olga.Token);
        var filteredByTeam = await SendAsync(HttpMethod.Get, $"{ProjectPath(w, w.A)}/workitems?teamId={w.Jupiter.Id}", null, olga.Token);
        var filteredByDepartment = await SendAsync(HttpMethod.Get, $"{ProjectPath(w, w.A)}/workitems?departmentId={w.Finance.Id}", null, olga.Token);
        var visibleFilter = await SendAsync(HttpMethod.Get, $"{ProjectPath(w, w.A)}/workitems?teamId={w.Venus.Id}", null, olga.Token);

        Assert.All(new[] { reorder, backlog, sprints, filteredByTeam }, answer => Assert.Equal((HttpStatusCode.NotFound, TeamNotFound), (answer.Status, answer.ErrorCode)));
        Assert.Equal((HttpStatusCode.NotFound, AreaNotFound), (filteredByDepartment.Status, filteredByDepartment.ErrorCode));
        Assert.Equal(HttpStatusCode.OK, visibleFilter.Status);
    }

    // US-6 · "Sight of a department" (BR-TD-14): a grant on it, or a team inside it that one sees; a project node and its sub-areas follow their project.
    [Fact]
    public async Task ADepartmentIsSeen_ByAGrantOnIt_OrAVisibleChild_AndANodeFollowsItsProject()
    {
        var w = await WorldAsync();
        var byGrant = await MemberAsync(w, OrganizationRole.Viewer);
        await SeedAsync(ResourceScope.Area, w.OrganizationId, w.Finance.Id, byGrant.Id, GrantRole.Reader);
        var other = await MemberAsync(w);

        var granted = await SendAsync(HttpMethod.Get, AreaPath(w, w.Finance), null, byGrant.Token);
        var nodeHidden = await SendAsync(HttpMethod.Get, AreaPath(w, w.NodeOfA), null, other.Token);
        await SendAsync(HttpMethod.Put, $"{ProjectPath(w, w.A)}/visibility", new { visibleToOrganization = true }, w.Ann.Token);
        var nodeShown = await SendAsync(HttpMethod.Get, AreaPath(w, w.NodeOfA), null, other.Token);

        Assert.Equal(HttpStatusCode.OK, granted.Status);
        Assert.Equal((HttpStatusCode.NotFound, AreaNotFound), (nodeHidden.Status, nodeHidden.ErrorCode));
        Assert.Equal(HttpStatusCode.OK, nodeShown.Status);
    }

    // US-6 · the flag's control: only a holder of `organizations.visibility.write` on the project (its «Менеджер», the department «Менеджер», the
    // administrator) writes it; a reader who sees the project is refused 403; a bad body is 400; the value it already holds is a 200.
    [Fact]
    public async Task TheFlagIsWrittenUnderTheVisibilityCode_ByTheProjectManagerAndTheDepartmentManager_NotByAReader()
    {
        var w = await WorldAsync();
        var projectManager = await MemberAsync(w, OrganizationRole.Viewer);
        var departmentManager = await MemberAsync(w, OrganizationRole.Viewer);
        var reader = await MemberAsync(w, OrganizationRole.Viewer);
        await SeedAsync(ResourceScope.Project, w.OrganizationId, w.A.Id, projectManager.Id, GrantRole.Manager);
        await SeedAsync(ResourceScope.Area, w.OrganizationId, w.Logistics.Id, departmentManager.Id, GrantRole.Manager);
        await SeedAsync(ResourceScope.Project, w.OrganizationId, w.A.Id, reader.Id, GrantRole.Reader);
        var visibility = $"{ProjectPath(w, w.A)}/visibility";

        var byProjectManager = await SendAsync(HttpMethod.Put, visibility, new { visibleToOrganization = true }, projectManager.Token);
        var same = await SendAsync(HttpMethod.Put, visibility, new { visibleToOrganization = true }, projectManager.Token);
        var byDepartmentManager = await SendAsync(HttpMethod.Put, visibility, new { visibleToOrganization = false }, departmentManager.Token);
        var byReader = await SendAsync(HttpMethod.Put, visibility, new { visibleToOrganization = true }, reader.Token);
        var bad = await SendAsync(HttpMethod.Put, visibility, new { }, w.Ann.Token);
        var managerCodes = await SendAsync(HttpMethod.Get, ProjectPath(w, w.A), null, projectManager.Token);

        Assert.Equal(HttpStatusCode.OK, byProjectManager.Status);
        Assert.Equal(HttpStatusCode.OK, same.Status);
        Assert.Equal((HttpStatusCode.OK, false), (byDepartmentManager.Status, byDepartmentManager.Body.GetProperty("visibleToOrganization").GetBoolean()));
        Assert.Equal((HttpStatusCode.Forbidden, "organizations.access.forbidden"), (byReader.Status, byReader.ErrorCode));
        Assert.Equal(HttpStatusCode.BadRequest, bad.Status);
        Assert.Contains(managerCodes.Body.GetProperty("codes").EnumerateArray().Select(code => code.GetString()), code => code == PermissionCatalog.OrganizationsVisibilityWrite);
    }

    // US-6 / BR-M1-26 reworded: the assignee candidates of a project are those who see it; the flag widens them to every member.
    [Fact]
    public async Task TheAssigneeCandidates_AreThoseWhoSeeTheProject()
    {
        var w = await WorldAsync();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var sees = await MemberWithNameAsync(w, "Видящий" + suffix);
        var blind = await MemberWithNameAsync(w, "Невидящий" + suffix);
        await SeedAsync(ResourceScope.Project, w.OrganizationId, w.A.Id, sees, GrantRole.Reader);
        var url = $"/api/organizations/{w.OrganizationId}/workitems/assignee-candidates?projectId={w.A.Id}&text=";

        var before = await SendAsync(HttpMethod.Get, url + Uri.EscapeDataString("идящий" + suffix), null, w.Ann.Token);
        await SendAsync(HttpMethod.Put, $"{ProjectPath(w, w.A)}/visibility", new { visibleToOrganization = true }, w.Ann.Token);
        var after = await SendAsync(HttpMethod.Get, url + Uri.EscapeDataString("идящий" + suffix), null, w.Ann.Token);

        Assert.Equal(new List<string> { sees }, Ids(before));
        Assert.Equal(new[] { sees, blind }.Order().ToList(), Ids(after).Order().ToList());
    }

    private async Task<string> MemberWithNameAsync(World w, string lastName)
    {
        var id = await WorkItemsTestData.AddUserAsync(factory, "Иван", lastName);
        await MembershipTestData.AddMemberAsync(factory, w.OrganizationId, id, OrganizationRole.Member);

        return id;
    }

    private static List<string> Ids(Answer answer) =>
        answer.Body.GetProperty("users").EnumerateArray().Select(user => user.GetProperty("id").GetString()!).ToList();
}
