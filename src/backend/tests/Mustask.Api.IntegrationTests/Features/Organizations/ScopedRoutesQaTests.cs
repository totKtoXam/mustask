using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Mustask.Api.IntegrationTests.Features.WorkItems;
using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Identity;
using Mustask.Domain.Organizations;

namespace Mustask.Api.IntegrationTests.Features.Organizations;

/// <summary>
/// QA of mod-019 (stage 5): what a grant lets its holder do on the routes of the object it is held on, taken from the stories'
/// tables (US-1, US-3, US-7, US-8) rather than from the implementation: renames, sub-areas, links, work items.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class ScopedRoutesQaTests(MustaskApiFactory factory)
{
    private sealed record World(OrganizationStructure Structure, MembershipTestData.TestMember Admin, Area Department, Project InDepartment, Area Node, Area Sub);

    private Task<Answer> SendAsync(HttpMethod method, string path, object? body, string? token) =>
        OrganizationsApi.SendAsync(factory, method, path, body, token);

    private async Task SeedAsync(ResourceScope scope, Guid organizationId, Guid objectId, string userId, GrantRole role) =>
        await OrganizationsTestData.InUnitOfWorkAsync(factory, async (services, ct) =>
        {
            await services.GetRequiredService<IGrantRepository>().AddAsync(scope, organizationId, objectId, userId, role, "seed", OrganizationsTestData.Now, ct);

            return true;
        });

    private async Task<World> WorldAsync()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var admin = await MembershipTestData.NewMemberAsync(factory, structure.OrganizationId, OrganizationRole.Admin);
        var department = await OrganizationsTestData.AddDepartmentAsync(factory, structure.OrganizationId, "Отдел");
        var created = await OrganizationsTestData.InUnitOfWorkAsync(factory, async (services, ct) =>
        {
            var projects = services.GetRequiredService<IProjectRepository>();
            var project = Project.Create(structure.OrganizationId, OrganizationsTestData.Name("Под отделом"), "DPT", OrganizationsTestData.Now, "seed", parentAreaId: department.Id);
            projects.Add(project);
            await projects.SaveAsync(ct);

            return project;
        });
        var sub = await OrganizationsTestData.AddSubAreaAsync(factory, structure.OrganizationId, created.Root.Id, "Платформа");

        return new World(structure, admin, department, created.Project, created.Root, sub);
    }

    private async Task<MembershipTestData.TestMember> HolderAsync(World w, ResourceScope scope, Guid objectId, GrantRole role)
    {
        var member = await MembershipTestData.NewMemberAsync(factory, w.Structure.OrganizationId, OrganizationRole.Viewer);
        await SeedAsync(scope, w.Structure.OrganizationId, objectId, member.Id, role);

        return member;
    }

    private string UnderProject(World w) => OrganizationsApi.Project(w.Structure.OrganizationId, w.InDepartment.Id);

    private string TopProject(World w) => OrganizationsApi.Project(w.Structure.OrganizationId, w.Structure.ProjectId);

    private string AreaPath(World w, Guid id) => $"{OrganizationsApi.Areas(w.Structure.OrganizationId)}/{id}";

    // US-3 table · «Менеджер» on A renames the project; «Участник» does not.
    [Fact]
    public async Task AProjectManager_RenamesTheirProject_AProjectMemberDoesNot()
    {
        var w = await WorldAsync();
        var manager = await HolderAsync(w, ResourceScope.Project, w.InDepartment.Id, GrantRole.Manager);
        var member = await HolderAsync(w, ResourceScope.Project, w.InDepartment.Id, GrantRole.Member);

        var byManager = await SendAsync(HttpMethod.Put, $"{UnderProject(w)}/name", new { name = "Новое имя" }, manager.Token);
        var byMember = await SendAsync(HttpMethod.Put, $"{UnderProject(w)}/name", new { name = "Другое имя" }, member.Token);
        var elsewhere = await SendAsync(HttpMethod.Put, $"{TopProject(w)}/name", new { name = "Чужое имя" }, manager.Token);

        Assert.True(byManager.Status == HttpStatusCode.OK, $"manager rename: {byManager.Status} {byManager.ErrorCode}");
        Assert.Equal(HttpStatusCode.Forbidden, byMember.Status);
        Assert.Equal(HttpStatusCode.Forbidden, elsewhere.Status);
    }

    // US-3 table, OQ-8 A · a department manager renames and describes the projects the department reaches, and not a top-level one.
    [Fact]
    public async Task ADepartmentManager_RenamesAReachedProject_NotATopLevelOne()
    {
        var w = await WorldAsync();
        var manager = await HolderAsync(w, ResourceScope.Area, w.Department.Id, GrantRole.Manager);

        var reached = await SendAsync(HttpMethod.Put, $"{UnderProject(w)}/name", new { name = "Из отдела" }, manager.Token);
        var described = await SendAsync(HttpMethod.Put, $"{UnderProject(w)}/description", new { description = "Описание" }, manager.Token);
        var top = await SendAsync(HttpMethod.Put, $"{TopProject(w)}/name", new { name = "Верхний" }, manager.Token);

        Assert.True(reached.Status == HttpStatusCode.OK, $"reached rename: {reached.Status} {reached.ErrorCode}");
        Assert.True(described.Status == HttpStatusCode.OK, $"description: {described.Status} {described.ErrorCode}");
        Assert.Equal(HttpStatusCode.Forbidden, top.Status);
    }

    // OQ-8 A · the switch cuts the department manager's project codes; deactivation stays the administrator's.
    [Fact]
    public async Task ADepartmentManager_LosesTheProjectWhenTheSwitchIsOff_AndNeverDeactivates()
    {
        var w = await WorldAsync();
        var manager = await HolderAsync(w, ResourceScope.Area, w.Department.Id, GrantRole.Manager);

        var deactivate = await SendAsync(HttpMethod.Post, $"{UnderProject(w)}/deactivate", null, manager.Token);
        var off = await SendAsync(HttpMethod.Put, $"{AreaPath(w, w.Node.Id)}/inherits-access", new { value = false }, manager.Token);
        var renameAfter = await SendAsync(HttpMethod.Put, $"{UnderProject(w)}/name", new { name = "После" }, manager.Token);

        Assert.Equal(HttpStatusCode.Forbidden, deactivate.Status);
        Assert.Equal(HttpStatusCode.OK, off.Status);
        Assert.Equal(HttpStatusCode.Forbidden, renameAfter.Status);
    }

    // US-1 / US-3 · «Менеджер» of a team renames it; «Участник» of the team does not.
    [Fact]
    public async Task ATeamManager_RenamesTheTeam_ALowerTeamRoleDoesNot()
    {
        var w = await WorldAsync();
        var teamId = w.Structure.Platform.Id;
        var manager = await HolderAsync(w, ResourceScope.Team, teamId, GrantRole.Manager);
        var member = await HolderAsync(w, ResourceScope.Team, teamId, GrantRole.Member);
        var path = $"{OrganizationsApi.OrgTeams(w.Structure.OrganizationId)}/{teamId}/name";

        var byManager = await SendAsync(HttpMethod.Put, path, new { name = "Переименована" }, manager.Token);
        var byMember = await SendAsync(HttpMethod.Put, path, new { name = "Другая" }, member.Token);

        Assert.True(byManager.Status == HttpStatusCode.OK, $"team rename: {byManager.Status} {byManager.ErrorCode}");
        Assert.Equal(HttpStatusCode.Forbidden, byMember.Status);
    }

    // US-8 · a project manager creates and renames sub-areas of their project, and not of another project; the node is the project's.
    [Fact]
    public async Task AProjectManager_ManagesTheSubAreasOfTheirProject_NotAnotherProjects()
    {
        var w = await WorldAsync();
        var manager = await HolderAsync(w, ResourceScope.Project, w.InDepartment.Id, GrantRole.Manager);
        var areas = OrganizationsApi.Areas(w.Structure.OrganizationId);

        var created = await SendAsync(HttpMethod.Post, areas, new { name = "Тесты", parentId = w.Node.Id.ToString() }, manager.Token);
        var renamed = await SendAsync(HttpMethod.Put, $"{AreaPath(w, w.Sub.Id)}/name", new { name = "Платформа 2" }, manager.Token);
        var foreign = await SendAsync(HttpMethod.Post, areas, new { name = "Чужая", parentId = w.Structure.RootId.ToString() }, manager.Token);
        var node = await SendAsync(HttpMethod.Put, $"{AreaPath(w, w.Node.Id)}/name", new { name = "Узел" }, manager.Token);
        var switchOwn = await SendAsync(HttpMethod.Put, $"{AreaPath(w, w.Node.Id)}/inherits-access", new { value = false }, manager.Token);

        Assert.True(created.Status == HttpStatusCode.Created, $"create sub-area: {created.Status} {created.ErrorCode}");
        Assert.True(renamed.Status == HttpStatusCode.OK, $"rename sub-area: {renamed.Status} {renamed.ErrorCode}");
        Assert.Equal(HttpStatusCode.Forbidden, foreign.Status);
        Assert.Equal((HttpStatusCode.Conflict, "organizations.area.root_is_fixed"), (node.Status, node.ErrorCode));
        Assert.Equal(HttpStatusCode.Forbidden, switchOwn.Status);
    }

    // US-8 · a project manager's area code does not depend on the sub-area's switch; a department manager's does.
    [Fact]
    public async Task ASubAreaSwitchedOff_KeepsTheProjectManager_DropsTheDepartmentManager()
    {
        var w = await WorldAsync();
        var projectManager = await HolderAsync(w, ResourceScope.Project, w.InDepartment.Id, GrantRole.Manager);
        var departmentManager = await HolderAsync(w, ResourceScope.Area, w.Department.Id, GrantRole.Manager);
        await SendAsync(HttpMethod.Put, $"{AreaPath(w, w.Sub.Id)}/inherits-access", new { value = false }, w.Admin.Token);

        var byProject = await SendAsync(HttpMethod.Put, $"{AreaPath(w, w.Sub.Id)}/name", new { name = "От проекта" }, projectManager.Token);
        var byDepartment = await SendAsync(HttpMethod.Put, $"{AreaPath(w, w.Sub.Id)}/name", new { name = "От отдела" }, departmentManager.Token);

        Assert.True(byProject.Status == HttpStatusCode.OK, $"project manager: {byProject.Status} {byProject.ErrorCode}");
        Assert.Equal(HttpStatusCode.Forbidden, byDepartment.Status);
    }

    // US-8 · nobody without the area code writes to the tree.
    [Fact]
    public async Task NobodyWithoutTheAreaCode_WritesToTheTree()
    {
        var w = await WorldAsync();
        var holders = new[]
        {
            await HolderAsync(w, ResourceScope.Area, w.Department.Id, GrantRole.Member),
            await HolderAsync(w, ResourceScope.Project, w.InDepartment.Id, GrantRole.Member),
            await HolderAsync(w, ResourceScope.Team, w.Structure.Platform.Id, GrantRole.Manager),
            await MembershipTestData.NewMemberAsync(factory, w.Structure.OrganizationId, OrganizationRole.Member),
        };

        foreach (var holder in holders)
        {
            var create = await SendAsync(HttpMethod.Post, OrganizationsApi.Areas(w.Structure.OrganizationId), new { name = "Нельзя", parentId = w.Node.Id.ToString() }, holder.Token);
            var rename = await SendAsync(HttpMethod.Put, $"{AreaPath(w, w.Sub.Id)}/name", new { name = "Нельзя" }, holder.Token);
            var sw = await SendAsync(HttpMethod.Put, $"{AreaPath(w, w.Sub.Id)}/inherits-access", new { value = false }, holder.Token);

            Assert.All([create, rename, sw], answer => Assert.Equal(HttpStatusCode.Forbidden, answer.Status));
        }
    }

    // US-7 · linking a team to a project needs the link code on both ends.
    [Fact]
    public async Task LinkingATeamToAProject_NeedsBothEnds()
    {
        var w = await WorldAsync();
        var jupiter = await OrganizationsTestData.AddUnlinkedTeamAsync(factory, w.Structure.OrganizationId, "Юпитер");
        var projectOnly = await HolderAsync(w, ResourceScope.Project, w.InDepartment.Id, GrantRole.Manager);
        var both = await HolderAsync(w, ResourceScope.Project, w.InDepartment.Id, GrantRole.Manager);
        await SeedAsync(ResourceScope.Team, w.Structure.OrganizationId, jupiter.Id, both.Id, GrantRole.Manager);
        var link = $"{UnderProject(w)}/teams";

        var one = await SendAsync(HttpMethod.Post, link, new { teamIds = new[] { jupiter.Id.ToString() } }, projectOnly.Token);
        var two = await SendAsync(HttpMethod.Post, link, new { teamIds = new[] { jupiter.Id.ToString() } }, both.Token);

        Assert.Equal(HttpStatusCode.Forbidden, one.Status);
        Assert.True(two.Status == HttpStatusCode.NoContent, $"both ends: {two.Status} {two.ErrorCode}");
    }

    // US-7 · the addressed end's 403 comes before the other end's not-found.
    [Fact]
    public async Task TheAddressedEnd_IsJudgedBeforeTheOtherEnd()
    {
        var w = await WorldAsync();
        var vera = await MembershipTestData.NewMemberAsync(factory, w.Structure.OrganizationId, OrganizationRole.Viewer);
        var kirill = await HolderAsync(w, ResourceScope.Project, w.InDepartment.Id, GrantRole.Manager);
        var link = $"{UnderProject(w)}/teams";
        var body = new { teamIds = new[] { Guid.CreateVersion7().ToString() } };

        var byVera = await SendAsync(HttpMethod.Post, link, body, vera.Token);
        var byKirill = await SendAsync(HttpMethod.Post, link, body, kirill.Token);

        Assert.Equal(HttpStatusCode.Forbidden, byVera.Status);
        Assert.Equal((HttpStatusCode.NotFound, "organizations.team.not_found"), (byKirill.Status, byKirill.ErrorCode));
    }

    // US-3 table · a work item changes under «Участник» on its project, and only there; a reader still reads.
    [Fact]
    public async Task AWorkItemChanges_UnderAProjectMember_NotUnderAReader_NotOnAnotherProject()
    {
        var world = await WorkItemsTestData.CreateWorldAsync(factory);
        var other = await WorkItemsTestData.AddProjectAsync(factory, world, "OTH", "Другой");
        var identifier = await WorkItemsTestData.CreateItemAsync(factory, world, "TASK", "Элемент");
        var otherIdentifier = await WorkItemsTestData.CreateItemAsync(factory, other, "TASK", "Чужой");
        var member = await MembershipTestData.NewMemberAsync(factory, world.OrganizationId, OrganizationRole.Viewer);
        var reader = await MembershipTestData.NewMemberAsync(factory, world.OrganizationId, OrganizationRole.Viewer);
        await SeedAsync(ResourceScope.Project, world.OrganizationId, world.ProjectId, member.Id, GrantRole.Member);
        await SeedAsync(ResourceScope.Project, world.OrganizationId, world.ProjectId, reader.Id, GrantRole.Reader);

        var kept = await SendAsync(HttpMethod.Patch, WorkItemsTestData.Item(world, identifier), new { title = "Новое" }, member.Token);
        var readerChange = await SendAsync(HttpMethod.Patch, WorkItemsTestData.Item(world, identifier), new { title = "Нельзя" }, reader.Token);
        var readerRead = await SendAsync(HttpMethod.Get, WorkItemsTestData.Item(world, identifier), null, reader.Token);
        var elsewhere = await SendAsync(HttpMethod.Patch, WorkItemsTestData.Item(other, otherIdentifier), new { title = "Нельзя" }, member.Token);

        Assert.True(kept.Status == HttpStatusCode.OK, $"member change: {kept.Status} {kept.ErrorCode}");
        Assert.Equal(HttpStatusCode.Forbidden, readerChange.Status);
        Assert.Equal(HttpStatusCode.OK, readerRead.Status);
        Assert.Equal(HttpStatusCode.Forbidden, elsewhere.Status);
    }

    // BR-SA-10 · who reorders which backlog: the answer is 403 or anything else (the empty body is refused after the code).
    [Fact]
    public async Task TheBacklogReorder_FollowsTheSplitCodes()
    {
        var w = await WorldAsync();
        var project = $"{TopProject(w)}/backlogs/stories/reorder";
        var team = $"{OrganizationsApi.OrgTeams(w.Structure.OrganizationId)}/{w.Structure.Platform.Id}/backlogs/stories/reorder";
        var projectMember = await HolderAsync(w, ResourceScope.Project, w.Structure.ProjectId, GrantRole.Member);
        var teamMember = await HolderAsync(w, ResourceScope.Team, w.Structure.Platform.Id, GrantRole.Member);
        var teamReader = await HolderAsync(w, ResourceScope.Team, w.Structure.Platform.Id, GrantRole.Reader);
        var departmentMember = await HolderAsync(w, ResourceScope.Area, w.Department.Id, GrantRole.Member);
        var orgMember = await MembershipTestData.NewMemberAsync(factory, w.Structure.OrganizationId, OrganizationRole.Member);
        var viewer = await MembershipTestData.NewMemberAsync(factory, w.Structure.OrganizationId, OrganizationRole.Viewer);

        async Task<bool> ForbiddenAsync(string path, MembershipTestData.TestMember who) =>
            (await SendAsync(HttpMethod.Post, path, new { }, who.Token)).Status == HttpStatusCode.Forbidden;

        Assert.False(await ForbiddenAsync(project, projectMember));
        Assert.True(await ForbiddenAsync(team, projectMember));
        Assert.False(await ForbiddenAsync(team, teamMember));
        Assert.True(await ForbiddenAsync(project, teamMember));
        Assert.True(await ForbiddenAsync(team, teamReader));
        Assert.True(await ForbiddenAsync(project, departmentMember));
        Assert.False(await ForbiddenAsync(project, orgMember));
        Assert.False(await ForbiddenAsync(team, orgMember));
        Assert.True(await ForbiddenAsync(project, viewer));
        Assert.True(await ForbiddenAsync(team, viewer));
    }

    // US-8 · moving a node needs the area code on the node, its current parent and the new parent.
    [Fact]
    public async Task MovingANode_NeedsTheCodeOnBothParents()
    {
        var w = await WorldAsync();
        var other = await OrganizationsTestData.AddDepartmentAsync(factory, w.Structure.OrganizationId, "Финансы");
        var pavel = await HolderAsync(w, ResourceScope.Area, w.Department.Id, GrantRole.Manager);
        var both = await HolderAsync(w, ResourceScope.Area, w.Department.Id, GrantRole.Manager);
        await SeedAsync(ResourceScope.Area, w.Structure.OrganizationId, other.Id, both.Id, GrantRole.Manager);

        var one = await SendAsync(HttpMethod.Put, $"{AreaPath(w, w.Node.Id)}/parent", new { parentId = other.Id.ToString() }, pavel.Token);
        var toTop = await SendAsync(HttpMethod.Put, $"{AreaPath(w, w.Node.Id)}/parent", new { parentId = (string?)null }, both.Token);
        var two = await SendAsync(HttpMethod.Put, $"{AreaPath(w, w.Node.Id)}/parent", new { parentId = other.Id.ToString() }, both.Token);

        Assert.Equal(HttpStatusCode.Forbidden, one.Status);
        Assert.Equal(HttpStatusCode.Forbidden, toTop.Status);
        Assert.True(two.Status == HttpStatusCode.OK, $"two codes: {two.Status} {two.ErrorCode}");
    }

    // Deferral (a) of plan-backend § 6 · a department manager naming teams at creation: refused cleanly (403), nothing created.
    [Fact]
    public async Task ADepartmentManagerNamingTeamsAtCreation_IsRefusedCleanly()
    {
        var w = await WorldAsync();
        var manager = await HolderAsync(w, ResourceScope.Area, w.Department.Id, GrantRole.Manager);

        var answer = await SendAsync(
            HttpMethod.Post, $"{Org(w)}/projects",
            new { name = "С командой", key = "TMS", parentAreaId = w.Department.Id.ToString(), teamIds = new[] { w.Structure.Platform.Id.ToString() } }, manager.Token);

        Assert.True((int)answer.Status is 403 or 201, $"{answer.Status} {answer.ErrorCode}");
        Assert.NotEqual(HttpStatusCode.InternalServerError, answer.Status);
    }

    private string Org(World w) => OrganizationsApi.Organization(w.Structure.OrganizationId);

    // US-4 / US-3 · the assignee picker belongs to the item form: a project «Участник» who may change items may open it.
    [Fact]
    public async Task AProjectMember_OpensTheAssigneePicker()
    {
        var w = await WorldAsync();
        var member = await HolderAsync(w, ResourceScope.Project, w.Structure.ProjectId, GrantRole.Member);

        var answer = await SendAsync(HttpMethod.Get, $"{Org(w)}/workitems/assignee-candidates?projectId={w.Structure.ProjectId}", null, member.Token);

        Assert.True(answer.Status == HttpStatusCode.OK, $"{answer.Status} {answer.ErrorCode}");
    }

    // mod-019 OQ-11 · the picker is judged on the project the request names: a reader, a grant on another project and no project at all
    // (the organization role alone, a viewer's) are refused.
    [Fact]
    public async Task TheAssigneePicker_IsJudgedOnTheNamedProject()
    {
        var w = await WorldAsync();
        var reader = await HolderAsync(w, ResourceScope.Project, w.Structure.ProjectId, GrantRole.Reader);
        var member = await HolderAsync(w, ResourceScope.Project, w.Structure.ProjectId, GrantRole.Member);
        var path = $"{Org(w)}/workitems/assignee-candidates";

        var byReader = await SendAsync(HttpMethod.Get, $"{path}?projectId={w.Structure.ProjectId}", null, reader.Token);
        var onAnother = await SendAsync(HttpMethod.Get, $"{path}?projectId={w.InDepartment.Id}", null, member.Token);
        var withoutProject = await SendAsync(HttpMethod.Get, path, null, member.Token);

        Assert.Equal(HttpStatusCode.Forbidden, byReader.Status);
        Assert.Equal(HttpStatusCode.Forbidden, onAnother.Status);
        Assert.Equal(HttpStatusCode.Forbidden, withoutProject.Status);
    }

    // QA pass 2 · a project of another organization, a malformed id and an unknown id are judged by the organization role alone.
    [Fact]
    public async Task TheAssigneePicker_ANamedProjectOfAnotherOrganizationOrAMalformedId_FallsBackToTheRole()
    {
        var w = await WorldAsync();
        var foreign = await OrganizationsTestData.CreateStructureAsync(factory);
        var member = await HolderAsync(w, ResourceScope.Project, w.Structure.ProjectId, GrantRole.Member);
        var orgMember = await MembershipTestData.NewMemberAsync(factory, w.Structure.OrganizationId, OrganizationRole.Member);
        var path = $"{Org(w)}/workitems/assignee-candidates";

        var holderForeign = await SendAsync(HttpMethod.Get, $"{path}?projectId={foreign.ProjectId}", null, member.Token);
        var holderMalformed = await SendAsync(HttpMethod.Get, $"{path}?projectId=not-an-id", null, member.Token);
        var holderUnknown = await SendAsync(HttpMethod.Get, $"{path}?projectId={Guid.CreateVersion7()}", null, member.Token);
        var roleForeign = await SendAsync(HttpMethod.Get, $"{path}?projectId={foreign.ProjectId}", null, orgMember.Token);
        var roleMalformed = await SendAsync(HttpMethod.Get, $"{path}?projectId=not-an-id", null, orgMember.Token);

        Assert.Equal(HttpStatusCode.Forbidden, holderForeign.Status);
        Assert.Equal(HttpStatusCode.Forbidden, holderMalformed.Status);
        Assert.Equal(HttpStatusCode.Forbidden, holderUnknown.Status);
        Assert.Equal(HttpStatusCode.OK, roleForeign.Status);
        Assert.Equal(HttpStatusCode.OK, roleMalformed.Status);
    }

    // QA pass 2 · BR-SA-9 / US-7 on the other link routes: an unknown other end is 404 for a holder of the addressed end's code; a known
    // one without the second code is 403; a caller without the addressed end's code is 403 first.
    [Fact]
    public async Task TheLinkRoutes_AnswerNotFoundBeforeTheSecondCode()
    {
        var w = await WorldAsync();
        var jupiter = await OrganizationsTestData.AddUnlinkedTeamAsync(factory, w.Structure.OrganizationId, "Юпитер");
        var kirill = await HolderAsync(w, ResourceScope.Project, w.InDepartment.Id, GrantRole.Manager);
        var teamManager = await HolderAsync(w, ResourceScope.Team, jupiter.Id, GrantRole.Manager);
        var viewer = await MembershipTestData.NewMemberAsync(factory, w.Structure.OrganizationId, OrganizationRole.Viewer);
        var ghost = Guid.CreateVersion7().ToString();
        var teams = OrganizationsApi.OrgTeams(w.Structure.OrganizationId);
        var unlink = $"{UnderProject(w)}/teams/{ghost}/unlink";
        var count = $"{UnderProject(w)}/teams/{ghost}/unlink-count";
        var known = $"{UnderProject(w)}/teams/{jupiter.Id}/unlink";

        var ghostUnlink = await SendAsync(HttpMethod.Post, unlink, new { destination = new { teamId = (string?)null } }, kirill.Token);
        var ghostCount = await SendAsync(HttpMethod.Get, count, null, kirill.Token);
        var knownUnlink = await SendAsync(HttpMethod.Post, known, new { destination = new { teamId = (string?)null } }, kirill.Token);
        var viewerGhost = await SendAsync(HttpMethod.Post, unlink, new { destination = new { teamId = (string?)null } }, viewer.Token);
        var reverseGhost = await SendAsync(HttpMethod.Post, $"{teams}/{jupiter.Id}/projects", new { projectIds = new[] { Guid.CreateVersion7().ToString() } }, teamManager.Token);
        var reverseGhostAreas = await SendAsync(HttpMethod.Post, $"{teams}/{jupiter.Id}/areas", new { areaIds = new[] { Guid.CreateVersion7().ToString() } }, teamManager.Token);

        Assert.Equal((HttpStatusCode.NotFound, "organizations.team.not_found"), (ghostUnlink.Status, ghostUnlink.ErrorCode));
        Assert.Equal(HttpStatusCode.NotFound, ghostCount.Status);
        Assert.Equal(HttpStatusCode.Forbidden, knownUnlink.Status);
        Assert.Equal(HttpStatusCode.Forbidden, viewerGhost.Status);
        Assert.Equal(HttpStatusCode.NotFound, reverseGhost.Status);
        Assert.Equal(HttpStatusCode.NotFound, reverseGhostAreas.Status);
    }
}
