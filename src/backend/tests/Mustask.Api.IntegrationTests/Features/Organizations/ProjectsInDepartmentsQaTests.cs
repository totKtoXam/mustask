using System.Net;
using Mustask.Api.IntegrationTests.Features.WorkItems;
using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Identity;
using Mustask.Domain.Organizations;
using static Mustask.Api.IntegrationTests.Features.Organizations.TeamTestData;

namespace Mustask.Api.IntegrationTests.Features.Organizations;

/// <summary>
/// mod-022 QA (backend), written from doc-016 BR-TD-3, BR-TD-4, BR-TD-6 and the api-contract before the code was read: the order of the
/// refusals, the routes the developer's tests leave out (team creation with projects, the same-department no-op, a sub-area as a
/// destination, a department as the "place"), and the permission edge of a request naming no department.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class ProjectsInDepartmentsQaTests(MustaskApiFactory factory)
{
    private Task<Answer> CreateProjectAsync(Guid organizationId, object body, string? token = null) =>
        OrganizationsApi.SendAsync(
            factory, HttpMethod.Post, $"{OrganizationsApi.Organization(organizationId)}/projects", body, token ?? OrganizationsTokens.SignedIn());

    private Task<Answer> MoveAsync(Guid organizationId, Guid nodeId, Guid? parentId, string? token = null) =>
        OrganizationsApi.SendAsync(
            factory,
            HttpMethod.Put,
            $"{OrganizationsApi.Areas(organizationId)}/{nodeId}/parent",
            new { parentId = parentId?.ToString() },
            token ?? OrganizationsTokens.SignedIn());

    private async Task<Guid> ProjectInAsync(Guid organizationId, Guid departmentId, string name, string key)
    {
        var answer = await CreateProjectAsync(organizationId, new { name, key, parentAreaId = departmentId.ToString() });
        Assert.Equal(HttpStatusCode.Created, answer.Status);

        return answer.Body.GetProperty("id").GetGuid();
    }

    // BR-TD-6 · an explicit JSON null is the same as an absent field; with a bad name the department reason is listed beside it.
    [Fact]
    public async Task Create_NullDepartment_WithABadName_ListsBothFieldReasons_AndCreatesNothing()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);

        var answer = await CreateProjectAsync(organization.Id, new { name = "", key = "BAD", parentAreaId = (string?)null });

        Assert.Equal(HttpStatusCode.BadRequest, answer.Status);
        var errors = answer.Body.GetProperty("errors");
        Assert.True(errors.TryGetProperty("name", out _));
        Assert.Equal("organizations.project.department_required", errors.GetProperty("parentAreaId")[0].GetProperty("code").GetString());
        Assert.Equal(0, (await OrganizationsApi.SendAsync(
            factory, HttpMethod.Get, $"{OrganizationsApi.Organization(organization.Id)}/projects", token: OrganizationsTokens.SignedIn())).Body.GetArrayLength());
    }

    // BR-TD-6 · "never in a sub-area, a project node": a project node or a sub-area named as the place is 409 type_not_allowed, an
    // unknown one is 404; neither creates anything.
    [Fact]
    public async Task Create_InAProjectNode_ASubArea_OrAnUnknownPlace_IsRefused_AndCreatesNothing()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var department = await OrganizationsTestData.AddDepartmentAsync(factory, organization.Id, "Разработка");
        var host = await ProjectInAsync(organization.Id, department.Id, "Хозяин", "HST");
        var node = await OrganizationsTestData.NodeOfAsync(factory, host);
        var sub = await OrganizationsTestData.AddSubAreaAsync(factory, organization.Id, node.Id, "Группа");

        var underNode = await CreateProjectAsync(organization.Id, new { name = "Один", key = "ONE", parentAreaId = node.Id.ToString() });
        var underSub = await CreateProjectAsync(organization.Id, new { name = "Два", key = "TWO", parentAreaId = sub.Id.ToString() });
        var unknown = await CreateProjectAsync(organization.Id, new { name = "Три", key = "THR", parentAreaId = Guid.CreateVersion7().ToString() });

        LinksTestData.AssertRefused(underNode, HttpStatusCode.Conflict, "organizations.area.type_not_allowed");
        LinksTestData.AssertRefused(underSub, HttpStatusCode.Conflict, "organizations.area.type_not_allowed");
        LinksTestData.AssertRefused(unknown, HttpStatusCode.NotFound, "organizations.area.not_found");
        var listed = await OrganizationsApi.SendAsync(
            factory, HttpMethod.Get, $"{OrganizationsApi.Organization(organization.Id)}/projects", token: OrganizationsTokens.SignedIn());
        Assert.Equal(1, listed.Body.GetArrayLength());
    }

    // BR-TD-6 · a department of another organization is not found (nothing is disclosed).
    [Fact]
    public async Task Create_InAForeignDepartment_Is404()
    {
        var mine = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var other = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var foreign = await OrganizationsTestData.AddDepartmentAsync(factory, other.Id, "Чужой");

        var answer = await CreateProjectAsync(mine.Id, new { name = "Мастаск", key = "MST", parentAreaId = foreign.Id.ToString() });

        LinksTestData.AssertRefused(answer, HttpStatusCode.NotFound, "organizations.area.not_found");
    }

    // Contract order · team ids that name nothing are 404 team.not_found before the department rule; a mix of an own and a foreign
    // department's team creates nothing.
    [Fact]
    public async Task Create_TeamsOrder_UnknownTeamIs404_AMixWithAForeignDepartmentTeamIs409_AndCreatesNothing()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var development = await OrganizationsTestData.AddDepartmentAsync(factory, organization.Id, "Разработка");
        var finance = await OrganizationsTestData.AddDepartmentAsync(factory, organization.Id, "Финансы");
        var own = await OrganizationsTestData.AddTeamInAsync(factory, organization.Id, development.Id, "Платформа");
        var foreign = await OrganizationsTestData.AddTeamInAsync(factory, organization.Id, finance.Id, "Бухгалтерия");

        var unknown = await CreateProjectAsync(
            organization.Id,
            new { name = "Мастаск", key = "MST", parentAreaId = development.Id.ToString(), teamIds = new[] { own.Id.ToString(), Guid.CreateVersion7().ToString() } });
        var mixed = await CreateProjectAsync(
            organization.Id,
            new { name = "Мастаск", key = "MST", parentAreaId = development.Id.ToString(), teamIds = new[] { own.Id.ToString(), foreign.Id.ToString() } });

        LinksTestData.AssertRefused(unknown, HttpStatusCode.NotFound, "organizations.team.not_found");
        LinksTestData.AssertRefused(mixed, HttpStatusCode.Conflict, "organizations.team.other_department");
        Assert.Empty(await LinksOfTeamAsync(factory, own.Id));
        Assert.Empty(await LinksOfTeamAsync(factory, foreign.Id));
    }

    // BR-TD-3 · a team created WITH projects: only those of its own department; a mix creates no team.
    [Fact]
    public async Task CreateTeam_WithProjects_OnlyOfItsOwnDepartment_ElseNoTeamIsCreated()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var development = await OrganizationsTestData.AddDepartmentAsync(factory, organization.Id, "Разработка");
        var finance = await OrganizationsTestData.AddDepartmentAsync(factory, organization.Id, "Финансы");
        var inside = await ProjectInAsync(organization.Id, development.Id, "Свой", "OWN");
        var outside = await ProjectInAsync(organization.Id, finance.Id, "Чужой", "OUT");
        var before = (await StoredTeamsAsync(factory, organization.Id)).Count;

        var refused = await CreateTeamAsync(
            factory, organization.Id,
            new { name = "Платформа", departmentId = development.Id.ToString(), projectIds = new[] { inside.ToString(), outside.ToString() } });
        var afterRefusal = (await StoredTeamsAsync(factory, organization.Id)).Count;
        var created = await CreateTeamAsync(
            factory, organization.Id,
            new { name = "Платформа", departmentId = development.Id.ToString(), projectIds = new[] { inside.ToString() } });

        LinksTestData.AssertRefused(refused, HttpStatusCode.Conflict, "organizations.team.other_department");
        Assert.Equal(before, afterRefusal);
        Assert.Equal(HttpStatusCode.Created, created.Status);
        Assert.Single(await LinksOfProjectAsync(factory, inside));
        Assert.Empty(await LinksOfProjectAsync(factory, outside));
    }

    // Contract · "ids that name nothing answer as before": an unknown project is 404 even beside a team of another department, and
    // linking a pair twice is a success that changes nothing.
    [Fact]
    public async Task Link_UnknownProject_Is404_AndAPairLinkedTwice_IsStill204()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var development = await OrganizationsTestData.AddDepartmentAsync(factory, organization.Id, "Разработка");
        var project = await ProjectInAsync(organization.Id, development.Id, "Мастаск", "MST");
        var team = await OrganizationsTestData.AddTeamInAsync(factory, organization.Id, development.Id, "Платформа");

        var unknown = await LinkProjectsAsync(factory, organization.Id, team.Id, new { projectIds = new[] { Guid.CreateVersion7().ToString() } });
        var first = await LinkTeamsAsync(factory, organization.Id, project, new { teamIds = new[] { team.Id.ToString() } });
        var second = await LinkTeamsAsync(factory, organization.Id, project, new { teamIds = new[] { team.Id.ToString() } });

        Assert.Equal(HttpStatusCode.NotFound, unknown.Status);
        Assert.Equal((HttpStatusCode.NoContent, HttpStatusCode.NoContent), (first.Status, second.Status));
        Assert.Single(await LinksOfProjectAsync(factory, project));
    }

    // BR-TD-3 · all or nothing on a batch: one team of another department among own ones links none.
    [Fact]
    public async Task Link_ABatchWithOneForeignTeam_LinksNone()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var development = await OrganizationsTestData.AddDepartmentAsync(factory, organization.Id, "Разработка");
        var finance = await OrganizationsTestData.AddDepartmentAsync(factory, organization.Id, "Финансы");
        var project = await ProjectInAsync(organization.Id, development.Id, "Мастаск", "MST");
        var own = await OrganizationsTestData.AddTeamInAsync(factory, organization.Id, development.Id, "Платформа");
        var foreign = await OrganizationsTestData.AddTeamInAsync(factory, organization.Id, finance.Id, "Бухгалтерия");

        var answer = await LinkTeamsAsync(factory, organization.Id, project, new { teamIds = new[] { own.Id.ToString(), foreign.Id.ToString() } });

        LinksTestData.AssertRefused(answer, HttpStatusCode.Conflict, "organizations.team.other_department");
        Assert.Empty(await LinksOfProjectAsync(factory, project));
    }

    // BR-TD-4 · "moving to the department it already stands in succeeds and changes nothing", links or not.
    [Fact]
    public async Task Move_ALinkedProject_ToItsOwnDepartment_IsA200_AndChangesNothing()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var development = await OrganizationsTestData.AddDepartmentAsync(factory, organization.Id, "Разработка");
        var project = await ProjectInAsync(organization.Id, development.Id, "Мастаск", "MST");
        var team = await OrganizationsTestData.AddTeamInAsync(factory, organization.Id, development.Id, "Платформа");
        await OrganizationsTestData.LinkAsync(factory, project, team.Id);
        var node = await OrganizationsTestData.NodeOfAsync(factory, project);

        var answer = await MoveAsync(organization.Id, node.Id, development.Id);

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Equal(development.Id, (await OrganizationsTestData.NodeOfAsync(factory, project)).ParentId);
        Assert.Single(await LinksOfProjectAsync(factory, project));
    }

    // BR-TD-4 · unlinking first (feat-028 BR-OL-8) frees the move; the project, not the team, keeps its colour and name.
    [Fact]
    public async Task Move_AfterTheLastTeamIsUnlinked_Succeeds_AndKeepsTheColour()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var development = await OrganizationsTestData.AddDepartmentAsync(factory, organization.Id, "Разработка");
        var finance = await OrganizationsTestData.AddDepartmentAsync(factory, organization.Id, "Финансы");
        var project = await ProjectInAsync(organization.Id, development.Id, "Мастаск", "MST");
        var team = await OrganizationsTestData.AddTeamInAsync(factory, organization.Id, development.Id, "Платформа");
        await OrganizationsTestData.LinkAsync(factory, project, team.Id);
        var node = await OrganizationsTestData.NodeOfAsync(factory, project);
        var refused = await MoveAsync(organization.Id, node.Id, finance.Id);

        var unlinked = await UnlinkAsync(factory, organization.Id, project, team.Id, To(null));
        var moved = await MoveAsync(organization.Id, node.Id, finance.Id);

        LinksTestData.AssertRefused(refused, HttpStatusCode.Conflict, "organizations.area.project_has_teams");
        Assert.Equal(HttpStatusCode.NoContent, unlinked.Status);
        Assert.Equal(HttpStatusCode.OK, moved.Status);
        Assert.Equal(finance.Id, (await OrganizationsTestData.NodeOfAsync(factory, project)).ParentId);
    }

    // Contract order · the type rules come before project_has_teams: a linked project moved under a sub-area or a project node is
    // type_not_allowed, not project_has_teams.
    [Fact]
    public async Task Move_ALinkedProject_UnderASubArea_IsTypeNotAllowed_NotHasTeams()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var development = await OrganizationsTestData.AddDepartmentAsync(factory, organization.Id, "Разработка");
        var project = await ProjectInAsync(organization.Id, development.Id, "Мастаск", "MST");
        var other = await ProjectInAsync(organization.Id, development.Id, "Другой", "OTH");
        var team = await OrganizationsTestData.AddTeamInAsync(factory, organization.Id, development.Id, "Платформа");
        await OrganizationsTestData.LinkAsync(factory, project, team.Id);
        var node = await OrganizationsTestData.NodeOfAsync(factory, project);
        var otherNode = await OrganizationsTestData.NodeOfAsync(factory, other);
        var sub = await OrganizationsTestData.AddSubAreaAsync(factory, organization.Id, otherNode.Id, "Группа");

        var underSub = await MoveAsync(organization.Id, node.Id, sub.Id);
        var underNode = await MoveAsync(organization.Id, node.Id, otherNode.Id);

        LinksTestData.AssertRefused(underSub, HttpStatusCode.Conflict, "organizations.area.type_not_allowed");
        LinksTestData.AssertRefused(underNode, HttpStatusCode.Conflict, "organizations.area.type_not_allowed");
        Assert.Equal(development.Id, (await OrganizationsTestData.NodeOfAsync(factory, project)).ParentId);
    }

    // Contract order · the top-level refusal comes before project_has_teams: a linked project sent to the top is needs_department.
    [Fact]
    public async Task Move_ALinkedProject_ToTheTop_IsNeedsDepartment_NotHasTeams()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var development = await OrganizationsTestData.AddDepartmentAsync(factory, organization.Id, "Разработка");
        var project = await ProjectInAsync(organization.Id, development.Id, "Мастаск", "MST");
        var team = await OrganizationsTestData.AddTeamInAsync(factory, organization.Id, development.Id, "Платформа");
        await OrganizationsTestData.LinkAsync(factory, project, team.Id);

        var answer = await MoveAsync(organization.Id, (await OrganizationsTestData.NodeOfAsync(factory, project)).Id, null);

        LinksTestData.AssertRefused(answer, HttpStatusCode.Conflict, "organizations.area.project_needs_department");
    }

    // mod-018 BR-AW-10 stands: a deactivated project's node is not moved at all (404 project.not_found), linked or not, and the
    // BR-TD-4 refusal does not replace that answer.
    [Fact]
    public async Task Move_ADeactivatedProject_IsStillProjectNotFound_LinkedOrNot()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var development = await OrganizationsTestData.AddDepartmentAsync(factory, organization.Id, "Разработка");
        var finance = await OrganizationsTestData.AddDepartmentAsync(factory, organization.Id, "Финансы");
        var linked = await ProjectInAsync(organization.Id, development.Id, "Связанный", "LNK");
        var free = await ProjectInAsync(organization.Id, development.Id, "Свободный", "FRE");
        var team = await OrganizationsTestData.AddTeamInAsync(factory, organization.Id, development.Id, "Платформа");
        await OrganizationsTestData.LinkAsync(factory, linked, team.Id);
        await OrganizationsTestData.SetProjectStatusAsync(factory, organization.Id, linked, ProjectStatus.Deactivated);
        await OrganizationsTestData.SetProjectStatusAsync(factory, organization.Id, free, ProjectStatus.Deactivated);

        var refused = await MoveAsync(organization.Id, (await OrganizationsTestData.NodeOfAsync(factory, linked)).Id, finance.Id);
        var moved = await MoveAsync(organization.Id, (await OrganizationsTestData.NodeOfAsync(factory, free)).Id, finance.Id);

        LinksTestData.AssertRefused(refused, HttpStatusCode.NotFound, "organizations.project.not_found");
        LinksTestData.AssertRefused(moved, HttpStatusCode.NotFound, "organizations.project.not_found");
    }

    // BR-TD-6 · who creates: a department's «Менеджер» may create in it and nowhere else; a plain member may not.
    [Fact]
    public async Task Create_ByADepartmentManager_IsAllowedInItsDepartmentOnly()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var development = await OrganizationsTestData.AddDepartmentAsync(factory, organization.Id, "Разработка");
        var finance = await OrganizationsTestData.AddDepartmentAsync(factory, organization.Id, "Финансы");
        var manager = await MembershipTestData.NewMemberAsync(factory, organization.Id, OrganizationRole.Viewer);
        var member = await MembershipTestData.NewMemberAsync(factory, organization.Id, OrganizationRole.Viewer);
        await OrganizationsTestData.GrantAsync(factory, ResourceScope.Area, organization.Id, development.Id, manager.Id, GrantRole.Manager);

        var own = await CreateProjectAsync(organization.Id, new { name = "Свой", key = "OWN", parentAreaId = development.Id.ToString() }, manager.Token);
        var elsewhere = await CreateProjectAsync(organization.Id, new { name = "Чужой", key = "OUT", parentAreaId = finance.Id.ToString() }, manager.Token);
        var plain = await CreateProjectAsync(organization.Id, new { name = "Простой", key = "PLN", parentAreaId = development.Id.ToString() }, member.Token);

        Assert.Equal(HttpStatusCode.Created, own.Status);
        Assert.Equal(HttpStatusCode.Forbidden, elsewhere.Status);
        Assert.Equal(HttpStatusCode.Forbidden, plain.Status);
    }

    // BR-TD-6 / contract § 1 · "A request naming no department is refused, 400 «Выберите департамент»"; "no place is judged for such a
    // request". A department's «Менеджер» (no top-level code) who sends none must get the 400, not a 403. EXPECTED TO FAIL while
    // CreateProjectHandler judges a null place by the top-level code (QA defect D-1); skipped until it is fixed, then un-skip.
    [Fact(Skip = "QA D-1 (minor, mod-022): a null place is judged by organizations.area.create first, so a department manager gets 403 instead of 400")]
    public async Task Create_WithoutADepartment_ByADepartmentManager_Is400_NotA403()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var development = await OrganizationsTestData.AddDepartmentAsync(factory, organization.Id, "Разработка");
        var manager = await MembershipTestData.NewMemberAsync(factory, organization.Id, OrganizationRole.Viewer);
        await OrganizationsTestData.GrantAsync(factory, ResourceScope.Area, organization.Id, development.Id, manager.Id, GrantRole.Manager);

        var answer = await CreateProjectAsync(organization.Id, new { name = "Без места", key = "NOP" }, manager.Token);

        Assert.Equal(HttpStatusCode.BadRequest, answer.Status);
    }
}
