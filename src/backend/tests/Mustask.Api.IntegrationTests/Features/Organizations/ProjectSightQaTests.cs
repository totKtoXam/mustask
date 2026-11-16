using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Mustask.Api.IntegrationTests.Features.WorkItems;
using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Identity;
using Mustask.Domain.Organizations;

namespace Mustask.Api.IntegrationTests.Features.Organizations;

/// <summary>
/// feat-041 backend QA (qa.md): the paths the developer's scenarios leave open — the non-disclosure of a hidden id against one
/// naming nothing, sight that survives «Наследовать доступ» off through a linked team, a team grant, the viewer in a linked team
/// (sight adds no code), the department «Менеджер» whose reach is cut, and a newly set assignee who does not see the project.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class ProjectSightQaTests(MustaskApiFactory factory)
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

    private sealed record World(Guid OrganizationId, MembershipTestData.TestMember Ann, Area Logistics, Area Finance, Project A, Project C, Team Venus, Team Jupiter);

    private async Task<World> WorldAsync()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory, founded: false);
        var ann = await MembershipTestData.NewMemberAsync(factory, organization.Id, OrganizationRole.Admin);
        var logistics = await OrganizationsTestData.AddDepartmentAsync(factory, organization.Id, "Логистика");
        var finance = await OrganizationsTestData.AddDepartmentAsync(factory, organization.Id, "Финансы");
        var a = await ProjectInAsync(organization.Id, logistics, "Проект A", "PQA");
        var c = await ProjectInAsync(organization.Id, finance, "Проект C", "PQC");
        var venus = await OrganizationsTestData.AddTeamInAsync(factory, organization.Id, logistics.Id, "VENUS");
        var jupiter = await OrganizationsTestData.AddTeamInAsync(factory, organization.Id, finance.Id, "JUPITER");
        await OrganizationsTestData.LinkAsync(factory, a.Id, venus.Id);
        await OrganizationsTestData.LinkAsync(factory, c.Id, jupiter.Id);

        return new World(organization.Id, ann, logistics, finance, a, c, venus, jupiter);
    }

    private Task<Project> ProjectInAsync(Guid organizationId, Area department, string name, string key) =>
        OrganizationsTestData.InUnitOfWorkAsync(factory, async (services, ct) =>
        {
            var projects = services.GetRequiredService<IProjectRepository>();
            var created = Project.Create(organizationId, OrganizationsTestData.Name(name), key, OrganizationsTestData.Now, "seed", "blue", parentAreaId: department.Id);
            projects.Add(created);
            await projects.SaveAsync(ct);

            return created.Project;
        });

    private static string P(World w, Project project) => OrganizationsApi.Project(w.OrganizationId, project.Id);

    private static string T(World w, Team team) => $"{OrganizationsApi.OrgTeams(w.OrganizationId)}/{team.Id}";

    private static string Ar(World w, Area area) => $"{OrganizationsApi.Areas(w.OrganizationId)}/{area.Id}";

    private Task<MembershipTestData.TestMember> MemberAsync(World w, OrganizationRole role = OrganizationRole.Member) =>
        MembershipTestData.NewMemberAsync(factory, w.OrganizationId, role);

    // Non-disclosure (spec US-6 "A hidden object does not exist": «as for an address that names nothing»): through a query id, a hidden
    // existing object and an id that names nothing must be indistinguishable by status and error code.
    [Fact]
    public async Task AHiddenQueryId_IsIndistinguishableFromOneThatNamesNothing()
    {
        var w = await WorldAsync();
        var olga = await MemberAsync(w);
        await SendAsync(HttpMethod.Put, $"{P(w, w.A)}/visibility", new { visibleToOrganization = true }, w.Ann.Token);
        var path = $"{P(w, w.A)}/workitems";
        var nothing = Guid.NewGuid();
        var differences = new List<string>();

        foreach (var (name, hidden) in new[] { ("teamId", w.Jupiter.Id), ("departmentId", w.Finance.Id) })
        {
            var hiddenAnswer = await SendAsync(HttpMethod.Get, $"{path}?{name}={hidden}", null, olga.Token);
            var missingAnswer = await SendAsync(HttpMethod.Get, $"{path}?{name}={nothing}", null, olga.Token);

            if ((missingAnswer.Status, missingAnswer.ErrorCode) != (hiddenAnswer.Status, hiddenAnswer.ErrorCode))
            {
                differences.Add($"{name}: hidden {hiddenAnswer.Status}/{hiddenAnswer.ErrorCode} vs nothing {missingAnswer.Status}/{missingAnswer.ErrorCode}");
            }
        }

        var orgItems = $"/api/organizations/{w.OrganizationId}/workitems";
        var hiddenProject = await SendAsync(HttpMethod.Get, $"{orgItems}?projectId={w.C.Id}", null, olga.Token);
        var missingProject = await SendAsync(HttpMethod.Get, $"{orgItems}?projectId={nothing}", null, olga.Token);

        if ((missingProject.Status, missingProject.ErrorCode) != (hiddenProject.Status, hiddenProject.ErrorCode))
        {
            differences.Add($"projectId: hidden {hiddenProject.Status}/{hiddenProject.ErrorCode} vs nothing {missingProject.Status}/{missingProject.ErrorCode}");
        }

        Assert.True(differences.Count == 0, string.Join("; ", differences));
    }

    // Path ids: a hidden project, team and department answer byte for byte as an unknown one (status, code, detail).
    [Fact]
    public async Task APathIdThatIsHidden_AnswersTheSameBytesAsOneThatNamesNothing()
    {
        var w = await WorldAsync();
        var olga = await MemberAsync(w);
        var nothing = Guid.NewGuid();
        var cases = new[]
        {
            (P(w, w.C), OrganizationsApi.Project(w.OrganizationId, nothing)),
            (T(w, w.Jupiter), $"{OrganizationsApi.OrgTeams(w.OrganizationId)}/{nothing}"),
            (Ar(w, w.Finance), $"{OrganizationsApi.Areas(w.OrganizationId)}/{nothing}"),
            ($"{P(w, w.C)}/workitems/PQC-1", $"{OrganizationsApi.Project(w.OrganizationId, nothing)}/workitems/PQC-1"),
        };

        foreach (var (hidden, missing) in cases)
        {
            var h = await SendAsync(HttpMethod.Get, hidden, null, olga.Token);
            var m = await SendAsync(HttpMethod.Get, missing, null, olga.Token);

            Assert.Equal((m.Status, m.ErrorCode, m.Detail), (h.Status, h.ErrorCode, h.Detail));
        }
    }

    // ADR-0040 § 4: "No value hides a project from its linked teams' members": a member of the linked team keeps sight with the node's
    // «Наследовать доступ» off and the flag off; a department grantee loses it.
    [Fact]
    public async Task ALinkedTeamMember_KeepsSight_WhenTheNodesInheritanceIsOff_AndADepartmentGranteeLosesIt()
    {
        var w = await WorldAsync();
        var member = await MemberAsync(w);
        var grantee = await MemberAsync(w, OrganizationRole.Viewer);
        await MembershipTestData.AddTeamMemberAsync(factory, w.OrganizationId, w.Venus.Id, member.Id);
        await SeedAsync(ResourceScope.Area, w.OrganizationId, w.Logistics.Id, grantee.Id, GrantRole.Reader);
        var node = await OrganizationsTestData.NodeOfAsync(factory, w.A.Id);

        var before = await SendAsync(HttpMethod.Get, P(w, w.A), null, grantee.Token);
        await SendAsync(HttpMethod.Put, $"{OrganizationsApi.Areas(w.OrganizationId)}/{node.Id}/inherits-access", new { value = false }, w.Ann.Token);
        var granteeAfter = await SendAsync(HttpMethod.Get, P(w, w.A), null, grantee.Token);
        var memberAfter = await SendAsync(HttpMethod.Get, P(w, w.A), null, member.Token);

        Assert.Equal(HttpStatusCode.OK, before.Status);
        Assert.Equal((HttpStatusCode.NotFound, ProjectNotFound), (granteeAfter.Status, granteeAfter.ErrorCode));
        Assert.Equal(HttpStatusCode.OK, memberAfter.Status);
    }

    // BR-TD-16 / BR-MX-26: sight adds no code. An organization «Читатель» in a linked team sees the project and changes nothing: 403, not 404.
    // The department «Менеджер» whose node switch is off no longer reaches the project: 404 on the visibility write, not 403.
    [Fact]
    public async Task SightAddsNoCode_AViewerInALinkedTeamIsRefused403_AndACutDepartmentManagerAnswers404()
    {
        var w = await WorldAsync();
        var viewer = await MemberAsync(w, OrganizationRole.Viewer);
        var manager = await MemberAsync(w, OrganizationRole.Viewer);
        await MembershipTestData.AddTeamMemberAsync(factory, w.OrganizationId, w.Venus.Id, viewer.Id);
        await SeedAsync(ResourceScope.Area, w.OrganizationId, w.Logistics.Id, manager.Id, GrantRole.Manager);

        var read = await SendAsync(HttpMethod.Get, P(w, w.A), null, viewer.Token);
        var rename = await SendAsync(HttpMethod.Put, $"{P(w, w.A)}/name", new { name = "Другое" }, viewer.Token);
        var flag = await SendAsync(HttpMethod.Put, $"{P(w, w.A)}/visibility", new { visibleToOrganization = true }, viewer.Token);
        var node = await OrganizationsTestData.NodeOfAsync(factory, w.A.Id);
        await SendAsync(HttpMethod.Put, $"{OrganizationsApi.Areas(w.OrganizationId)}/{node.Id}/inherits-access", new { value = false }, w.Ann.Token);
        var cut = await SendAsync(HttpMethod.Put, $"{P(w, w.A)}/visibility", new { visibleToOrganization = true }, manager.Token);

        Assert.Equal(HttpStatusCode.OK, read.Status);
        Assert.Equal((HttpStatusCode.Forbidden, "organizations.access.forbidden"), (rename.Status, rename.ErrorCode));
        Assert.Equal((HttpStatusCode.Forbidden, "organizations.access.forbidden"), (flag.Status, flag.ErrorCode));
        Assert.Equal((HttpStatusCode.NotFound, ProjectNotFound), (cut.Status, cut.ErrorCode));
    }

    // BR-TD-13: a grant on a team gives sight of that team and of the department holding it, and nothing of the projects linked to it.
    [Fact]
    public async Task ATeamGrant_GivesSightOfTheTeamAndItsDepartment_NotOfItsProjects()
    {
        var w = await WorldAsync();
        var user = await MemberAsync(w, OrganizationRole.Viewer);
        await SeedAsync(ResourceScope.Team, w.OrganizationId, w.Venus.Id, user.Id, GrantRole.Reader);

        var team = await SendAsync(HttpMethod.Get, T(w, w.Venus), null, user.Token);
        var department = await SendAsync(HttpMethod.Get, Ar(w, w.Logistics), null, user.Token);
        var project = await SendAsync(HttpMethod.Get, P(w, w.A), null, user.Token);
        var other = await SendAsync(HttpMethod.Get, Ar(w, w.Finance), null, user.Token);

        Assert.Equal(HttpStatusCode.OK, team.Status);
        Assert.Equal(HttpStatusCode.OK, department.Status);
        Assert.Equal((HttpStatusCode.NotFound, ProjectNotFound), (project.Status, project.ErrorCode));
        Assert.Equal((HttpStatusCode.NotFound, AreaNotFound), (other.Status, other.ErrorCode));
    }

    // BR-TD-12 (c) + BR-TD-13: a team the member sees only through a visible linked project answers its routes while another team stays 404.
    [Fact]
    public async Task ATeamSeenThroughAVisibleProject_AnswersItsRoutes_WhileAnotherTeamStaysHidden()
    {
        var w = await WorldAsync();
        var olga = await MemberAsync(w);
        await SendAsync(HttpMethod.Put, $"{P(w, w.A)}/visibility", new { visibleToOrganization = true }, w.Ann.Token);

        var seen = await SendAsync(HttpMethod.Get, $"{T(w, w.Venus)}/sprints", null, olga.Token);
        var hidden = await SendAsync(HttpMethod.Get, $"{T(w, w.Jupiter)}/sprints", null, olga.Token);

        Assert.Equal(HttpStatusCode.OK, seen.Status);
        Assert.Equal((HttpStatusCode.NotFound, TeamNotFound), (hidden.Status, hidden.ErrorCode));
    }

    // api-contract § 3 (ADR-0029 § 11): a newly set assignee who does not see the project is refused 409; once they see it, accepted;
    // an assignee who then loses sight stays and never refuses a save of other fields.
    [Fact]
    public async Task Save_ANewAssigneeWhoDoesNotSeeTheProject_IsRefused_ThenAccepted_AndStaysAfterLosingSight()
    {
        var world = await WorkItemsTestData.CreateWorldAsync(factory);
        var bug = await WorkItemsTestData.CreateItemAsync(factory, world, "BUG", "Падение");
        var projectPath = $"/api/organizations/{world.OrganizationId}/projects/{world.ProjectId}";
        var off = await OrganizationsApi.SendAsync(factory, HttpMethod.Put, $"{projectPath}/visibility", new { visibleToOrganization = false }, OrganizationsTokens.SignedIn());
        Assert.Equal(HttpStatusCode.OK, off.Status);
        var blind = await WorkItemsTestData.AddMemberAsync(factory, world, "Слепой", "Иванов");

        var refused = await WorkItemsTestData.SaveAsync(factory, world, bug, new { assignee = new { userId = blind } });
        await OrganizationsTestData.InUnitOfWorkAsync(factory, async (services, ct) =>
        {
            await services.GetRequiredService<IGrantRepository>().AddAsync(ResourceScope.Project, world.OrganizationId, world.ProjectId, blind, GrantRole.Reader, "seed", OrganizationsTestData.Now, ct);

            return true;
        });
        var accepted = await WorkItemsTestData.SaveAsync(factory, world, bug, new { assignee = new { userId = blind } });
        await OrganizationsTestData.InUnitOfWorkAsync(factory, async (services, ct) =>
        {
            await services.GetRequiredService<IGrantRepository>().RemoveAsync(ResourceScope.Project, world.ProjectId, blind, ct);

            return true;
        });
        var stays = await WorkItemsTestData.SaveAsync(factory, world, bug, new { title = "Иное" });

        Assert.Equal((HttpStatusCode.Conflict, "workitems.workitem.assignee_unavailable"), (refused.Status, refused.ErrorCode));
        Assert.Equal(HttpStatusCode.OK, accepted.Status);
        Assert.Equal(HttpStatusCode.OK, stays.Status);
    }
}
