using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Mustask.Api.IntegrationTests.Features.WorkItems;
using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Identity;
using Mustask.Domain.Organizations;

namespace Mustask.Api.IntegrationTests.Features.Organizations;

/// <summary>QA of mod-019 (stage 5): the edges of the grant routes and the switch impact that the developer's tests leave out (US-1, US-2, BR-SA-3, BR-SA-19).</summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class GrantsQaTests(MustaskApiFactory factory)
{
    private Task<Answer> SendAsync(HttpMethod method, string path, object? body, string? token) =>
        OrganizationsApi.SendAsync(factory, method, path, body, token);

    private async Task SeedAsync(ResourceScope scope, Guid organizationId, Guid objectId, string userId, GrantRole role) =>
        await OrganizationsTestData.InUnitOfWorkAsync(factory, async (services, ct) =>
        {
            await services.GetRequiredService<IGrantRepository>().AddAsync(scope, organizationId, objectId, userId, role, "seed", OrganizationsTestData.Now, ct);

            return true;
        });

    private async Task<(OrganizationStructure S, MembershipTestData.TestMember Admin)> WorldAsync()
    {
        var s = await OrganizationsTestData.CreateStructureAsync(factory);

        return (s, await MembershipTestData.NewMemberAsync(factory, s.OrganizationId, OrganizationRole.Admin));
    }

    private async Task<string> UserAsync(OrganizationStructure s, string name, OrganizationRole role = OrganizationRole.Viewer)
    {
        var id = await WorkItemsTestData.AddUserAsync(factory, name, "Грант");
        await MembershipTestData.AddMemberAsync(factory, s.OrganizationId, id, role);

        return id;
    }

    private static string Grants(OrganizationStructure s) => $"{OrganizationsApi.Project(s.OrganizationId, s.ProjectId)}/grants";

    // BR-SA-19 · the actor may remove their own grant; a manager removes their own and then loses the grant code.
    [Fact]
    public async Task AManager_MayRemoveTheirOwnGrant_ButNotChangeIt()
    {
        var (s, _) = await WorldAsync();
        var manager = await MembershipTestData.NewMemberAsync(factory, s.OrganizationId, OrganizationRole.Viewer);
        await SeedAsync(ResourceScope.Project, s.OrganizationId, s.ProjectId, manager.Id, GrantRole.Manager);

        var change = await SendAsync(HttpMethod.Put, $"{Grants(s)}/{manager.Id}", new { role = "reader" }, manager.Token);
        var remove = await SendAsync(HttpMethod.Delete, $"{Grants(s)}/{manager.Id}", null, manager.Token);
        var again = await SendAsync(HttpMethod.Post, Grants(s), new { userId = manager.Id, role = "manager" }, manager.Token);

        Assert.Equal((HttpStatusCode.BadRequest, HttpStatusCode.NoContent, HttpStatusCode.Forbidden), (change.Status, remove.Status, again.Status));
    }

    // Contract § 2 · a PUT to the same role is a 200 that keeps the giver and the time.
    [Fact]
    public async Task APutToTheSameRole_KeepsTheGiverAndTheTime()
    {
        var (s, admin) = await WorldAsync();
        var other = await MembershipTestData.NewMemberAsync(factory, s.OrganizationId, OrganizationRole.Admin);
        var vera = await UserAsync(s, "Вера");
        var given = await SendAsync(HttpMethod.Post, Grants(s), new { userId = vera, role = "member" }, admin.Token);

        var same = await SendAsync(HttpMethod.Put, $"{Grants(s)}/{vera}", new { role = "member" }, other.Token);

        Assert.Equal(HttpStatusCode.OK, same.Status);
        Assert.Equal(given.Body.GetProperty("givenBy").GetProperty("id").GetString(), same.Body.GetProperty("givenBy").GetProperty("id").GetString());
        Assert.True((given.Body.GetProperty("givenAt").GetDateTimeOffset() - same.Body.GetProperty("givenAt").GetDateTimeOffset()).Duration() < TimeSpan.FromMilliseconds(1));
    }

    // BR-SA-19 · Pavel and Irina grant each other: both are kept.
    [Fact]
    public async Task TwoManagers_MayGrantEachOther()
    {
        var (s, _) = await WorldAsync();
        var a = await MembershipTestData.NewNamedMemberAsync(factory, s.OrganizationId, OrganizationRole.Viewer, "Павел", "Грант");
        var b = await MembershipTestData.NewNamedMemberAsync(factory, s.OrganizationId, OrganizationRole.Viewer, "Ирина", "Грант");
        await SeedAsync(ResourceScope.Project, s.OrganizationId, s.ProjectId, a.Id, GrantRole.Manager);

        var first = await SendAsync(HttpMethod.Post, Grants(s), new { userId = b.Id, role = "manager" }, a.Token);
        var second = await SendAsync(HttpMethod.Put, $"{Grants(s)}/{a.Id}", new { role = "manager" }, b.Token);

        Assert.Equal((HttpStatusCode.Created, HttpStatusCode.OK), (first.Status, second.Status));
    }

    // BR-SA-2 · a grant to an organization admin is stored and kept after the demotion.
    [Fact]
    public async Task AGrantToAnAdmin_IsKept_WhenTheAdminIsDemoted()
    {
        var (s, admin) = await WorldAsync();
        var other = await UserAsync(s, "Админ", OrganizationRole.Admin);

        var given = await SendAsync(HttpMethod.Post, Grants(s), new { userId = other, role = "reader" }, admin.Token);
        await OrganizationsTestData.InUnitOfWorkAsync(factory, async (services, ct) =>
        {
            var memberships = services.GetRequiredService<IMembershipRepository>();
            var membership = await memberships.LockMemberAsync(s.OrganizationId, other, ct);
            membership!.ChangeRole(OrganizationRole.Member, "seed", OrganizationsTestData.Now);
            await memberships.SaveAsync(ct);

            return true;
        });
        var listed = await SendAsync(HttpMethod.Get, Grants(s), null, admin.Token);

        Assert.Equal(HttpStatusCode.Created, given.Status);
        Assert.Equal([other], listed.Body.GetProperty("items").EnumerateArray().Select(row => row.GetProperty("user").GetProperty("id").GetString()));
    }

    // BR-SA-4 · every grant route on a project node answers 409 scope_not_allowed, the reads included.
    [Fact]
    public async Task EveryGrantRouteOnAProjectNode_Is409()
    {
        var (s, admin) = await WorldAsync();
        var vera = await UserAsync(s, "Вера");
        var node = $"{OrganizationsApi.Areas(s.OrganizationId)}/{s.RootId}";

        var answers = new[]
        {
            await SendAsync(HttpMethod.Get, $"{node}/grants", null, admin.Token),
            await SendAsync(HttpMethod.Get, $"{node}/grant-candidates?text=Грант", null, admin.Token),
            await SendAsync(HttpMethod.Put, $"{node}/grants/{vera}", new { role = "reader" }, admin.Token),
            await SendAsync(HttpMethod.Delete, $"{node}/grants/{vera}", null, admin.Token),
        };

        Assert.All(answers, a => Assert.Equal((HttpStatusCode.Conflict, "organizations.grant.scope_not_allowed"), (a.Status, a.ErrorCode)));
    }

    // BR-SA-3 · deleting a team ends its grants; a deactivated project keeps them and restoring brings them back.
    [Fact]
    public async Task DeletingATeamEndsItsGrants_DeactivatingAProjectKeepsThem()
    {
        var (s, admin) = await WorldAsync();
        var vera = await UserAsync(s, "Вера");
        var team = await OrganizationsTestData.AddUnlinkedTeamAsync(factory, s.OrganizationId, "Удаляемая");
        await SeedAsync(ResourceScope.Team, s.OrganizationId, team.Id, vera, GrantRole.Manager);
        await SeedAsync(ResourceScope.Project, s.OrganizationId, s.ProjectId, vera, GrantRole.Member);

        var deleted = await SendAsync(HttpMethod.Delete, $"{OrganizationsApi.OrgTeams(s.OrganizationId)}/{team.Id}", null, admin.Token);
        var teamGrants = await OrganizationsTestData.InUnitOfWorkAsync(factory, async (services, ct) =>
            await services.GetRequiredService<IGrantRepository>().FindAsync(ResourceScope.Team, team.Id, vera, ct));
        await OrganizationsTestData.SetProjectStatusAsync(factory, s.OrganizationId, s.ProjectId, ProjectStatus.Deactivated);
        await OrganizationsTestData.SetProjectStatusAsync(factory, s.OrganizationId, s.ProjectId, ProjectStatus.Active);
        var listed = await SendAsync(HttpMethod.Get, Grants(s), null, admin.Token);

        Assert.True(deleted.Status is HttpStatusCode.NoContent or HttpStatusCode.OK, $"team delete {deleted.Status} {deleted.ErrorCode}");
        Assert.Null(teamGrants);
        Assert.Equal([vera], listed.Body.GetProperty("items").EnumerateArray().Select(row => row.GetProperty("user").GetProperty("id").GetString()));
    }

    // Bad input · a numeric or null role, a blank user and a malformed id are 400/404, never 500.
    [Fact]
    public async Task BadInput_NeverReaches500()
    {
        var (s, admin) = await WorldAsync();

        var numeric = await SendAsync(HttpMethod.Post, Grants(s), new { userId = "x", role = 1 }, admin.Token);
        var nullRole = await SendAsync(HttpMethod.Post, Grants(s), new { userId = "x", role = (string?)null }, admin.Token);
        var blank = await SendAsync(HttpMethod.Post, Grants(s), new { userId = " ", role = "reader" }, admin.Token);
        var malformed = await SendAsync(HttpMethod.Get, $"{OrganizationsApi.Organization(s.OrganizationId)}/projects/not-a-guid/grants", null, admin.Token);
        var longText = await SendAsync(HttpMethod.Get, $"{OrganizationsApi.Project(s.OrganizationId, s.ProjectId)}/grant-candidates?text={new string('я', 300)}", null, admin.Token);
        var shortText = await SendAsync(HttpMethod.Get, $"{OrganizationsApi.Project(s.OrganizationId, s.ProjectId)}/grant-candidates?text=%27%3B--", null, admin.Token);

        Assert.All([numeric, nullRole, blank], a => Assert.Equal(HttpStatusCode.BadRequest, a.Status));
        Assert.Equal(HttpStatusCode.NotFound, malformed.Status);
        Assert.True((int)longText.Status < 500);
        Assert.Equal(HttpStatusCode.OK, shortText.Status);
    }

    // A grant in a foreign organization's object through this organization's address names nothing.
    [Fact]
    public async Task AnObjectOfAnotherOrganization_NamesNothing()
    {
        var (s, admin) = await WorldAsync();
        var vera = await UserAsync(s, "Вера");
        var foreign = await OrganizationsTestData.CreateStructureAsync(factory);

        var project = await SendAsync(HttpMethod.Post, $"{OrganizationsApi.Project(s.OrganizationId, foreign.ProjectId)}/grants", new { userId = vera, role = "reader" }, admin.Token);
        var team = await SendAsync(HttpMethod.Post, $"{OrganizationsApi.OrgTeams(s.OrganizationId)}/{foreign.Platform.Id}/grants", new { userId = vera, role = "reader" }, admin.Token);

        Assert.Equal((HttpStatusCode.NotFound, "organizations.project.not_found"), (project.Status, project.ErrorCode));
        Assert.Equal((HttpStatusCode.NotFound, "organizations.team.not_found"), (team.Status, team.ErrorCode));
    }

    // US-2 / OQ-6 / OQ-12 · the impact of a department, of an already-off node, and with admins and covering project-grant holders left out.
    [Fact]
    public async Task TheImpact_LeavesOutAdminsAndProjectHolders_AndIsEmptyWhenAlreadyOff()
    {
        var (s, admin) = await WorldAsync();
        var department = await OrganizationsTestData.AddDepartmentAsync(factory, s.OrganizationId, "Отдел");
        var created = await OrganizationsTestData.InUnitOfWorkAsync(factory, async (services, ct) =>
        {
            var projects = services.GetRequiredService<IProjectRepository>();
            var project = Project.Create(s.OrganizationId, OrganizationsTestData.Name("Под отделом"), "DPT", OrganizationsTestData.Now, "seed", parentAreaId: department.Id);
            projects.Add(project);
            await projects.SaveAsync(ct);

            return project;
        });
        var plain = await UserAsync(s, "Обычный");
        var holder = await UserAsync(s, "Держатель");
        var reader = await UserAsync(s, "Читатель проекта");
        var adminUser = await UserAsync(s, "Админ", OrganizationRole.Admin);
        foreach (var user in new[] { plain, holder, reader, adminUser })
        {
            await SeedAsync(ResourceScope.Area, s.OrganizationId, department.Id, user, GrantRole.Member);
        }

        await SeedAsync(ResourceScope.Project, s.OrganizationId, created.Project.Id, holder, GrantRole.Member);
        await SeedAsync(ResourceScope.Project, s.OrganizationId, created.Project.Id, reader, GrantRole.Reader);
        var impact = $"{OrganizationsApi.Areas(s.OrganizationId)}/{created.Root.Id}/inherits-access/impact";

        var before = await SendAsync(HttpMethod.Get, impact, null, admin.Token);
        await SendAsync(HttpMethod.Put, $"{OrganizationsApi.Areas(s.OrganizationId)}/{created.Root.Id}/inherits-access", new { value = false }, admin.Token);
        var after = await SendAsync(HttpMethod.Get, impact, null, admin.Token);
        var onDepartment = await SendAsync(HttpMethod.Get, $"{OrganizationsApi.Areas(s.OrganizationId)}/{department.Id}/inherits-access/impact", null, admin.Token);

        // «Участник» on the department who holds project «Участник» loses nothing; one who holds only project «Читатель» loses writes.
        Assert.Equal(2, before.Body.GetProperty("holderCount").GetInt32());
        Assert.Equal(0, after.Body.GetProperty("holderCount").GetInt32());
        Assert.Equal((HttpStatusCode.Conflict, "organizations.area.type_not_allowed"), (onDepartment.Status, onDepartment.ErrorCode));
    }

    // US-2 / OQ-12 · Pavel (department «Менеджер», holding project «Читатель» too) is counted and is warned; Irina («Участник» with project «Участник») is not.
    [Fact]
    public async Task TheImpact_CountsAManagerWhoseProjectGrantDoesNotCoverWhatTheSwitchTakes()
    {
        var (s, _) = await WorldAsync();
        var department = await OrganizationsTestData.AddDepartmentAsync(factory, s.OrganizationId, "Логистика");
        var created = await OrganizationsTestData.InUnitOfWorkAsync(factory, async (services, ct) =>
        {
            var projects = services.GetRequiredService<IProjectRepository>();
            var project = Project.Create(s.OrganizationId, OrganizationsTestData.Name("Под отделом"), "DPT", OrganizationsTestData.Now, "seed", parentAreaId: department.Id);
            projects.Add(project);
            await projects.SaveAsync(ct);

            return project;
        });
        var pavel = await MembershipTestData.NewMemberAsync(factory, s.OrganizationId, OrganizationRole.Viewer);
        var irina = await UserAsync(s, "Ирина");
        await SeedAsync(ResourceScope.Area, s.OrganizationId, department.Id, pavel.Id, GrantRole.Manager);
        await SeedAsync(ResourceScope.Area, s.OrganizationId, department.Id, irina, GrantRole.Member);
        await SeedAsync(ResourceScope.Project, s.OrganizationId, created.Project.Id, pavel.Id, GrantRole.Reader);
        await SeedAsync(ResourceScope.Project, s.OrganizationId, created.Project.Id, irina, GrantRole.Member);
        var impact = $"{OrganizationsApi.Areas(s.OrganizationId)}/{created.Root.Id}/inherits-access/impact";

        var answer = await SendAsync(HttpMethod.Get, impact, null, pavel.Token);

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Equal(1, answer.Body.GetProperty("holderCount").GetInt32());
        Assert.True(answer.Body.GetProperty("callerIncluded").GetBoolean());
    }

    // Reach · a department Reader does not stop at the sub-area switch; a project grant is held whatever the node's switch says.
    [Fact]
    public async Task AProjectGrant_StaysAfterTheNodeSwitchIsOff()
    {
        var (s, admin) = await WorldAsync();
        var department = await OrganizationsTestData.AddDepartmentAsync(factory, s.OrganizationId, "Отдел");
        var created = await OrganizationsTestData.InUnitOfWorkAsync(factory, async (services, ct) =>
        {
            var projects = services.GetRequiredService<IProjectRepository>();
            var project = Project.Create(s.OrganizationId, OrganizationsTestData.Name("Под отделом"), "DPT", OrganizationsTestData.Now, "seed", parentAreaId: department.Id);
            projects.Add(project);
            await projects.SaveAsync(ct);

            return project;
        });
        var vera = await MembershipTestData.NewMemberAsync(factory, s.OrganizationId, OrganizationRole.Viewer);
        await SeedAsync(ResourceScope.Project, s.OrganizationId, created.Project.Id, vera.Id, GrantRole.Manager);
        await SendAsync(HttpMethod.Put, $"{OrganizationsApi.Areas(s.OrganizationId)}/{created.Root.Id}/inherits-access", new { value = false }, admin.Token);

        var projectRead = await SendAsync(HttpMethod.Get, OrganizationsApi.Project(s.OrganizationId, created.Project.Id), null, vera.Token);
        var codes = projectRead.Body.GetProperty("codes").EnumerateArray().Select(c => c.GetString()).ToList();

        Assert.Contains(PermissionCatalog.OrganizationsProjectGrant, codes);
    }
}
