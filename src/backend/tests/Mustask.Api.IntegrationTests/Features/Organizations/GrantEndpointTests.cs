using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Mustask.Api.IntegrationTests.Features.WorkItems;
using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Identity;
using Mustask.Domain.Organizations;

namespace Mustask.Api.IntegrationTests.Features.Organizations;

/// <summary>
/// The grants of mod-019 (api-contract § 2; US-1 "A manager gives, changes and takes back access on their own object"): the five
/// routes under a department, a project and a team, checked on the addressed object, with the order of answers of § 2.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class GrantEndpointTests(MustaskApiFactory factory)
{
    private static string ProjectGrants(OrganizationStructure s) => $"{OrganizationsApi.Project(s.OrganizationId, s.ProjectId)}/grants";

    private static string TeamGrants(OrganizationStructure s, Guid teamId) => $"{OrganizationsApi.OrgTeams(s.OrganizationId)}/{teamId}/grants";

    private static string AreaGrants(OrganizationStructure s, Guid areaId) => $"{OrganizationsApi.Areas(s.OrganizationId)}/{areaId}/grants";

    private static string FieldCode(Answer answer, string field) =>
        answer.Body.GetProperty("errors").GetProperty(field)[0].GetProperty("code").GetString()!;

    private Task<Answer> SendAsync(HttpMethod method, string path, object? body, string? token) =>
        OrganizationsApi.SendAsync(factory, method, path, body, token);

    private async Task<(OrganizationStructure Structure, MembershipTestData.TestMember Admin)> WorldAsync()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var admin = await MembershipTestData.NewMemberAsync(factory, structure.OrganizationId, OrganizationRole.Admin);

        return (structure, admin);
    }

    private async Task<string> MemberAsync(OrganizationStructure structure, string firstName, OrganizationRole role = OrganizationRole.Viewer)
    {
        var id = await WorkItemsTestData.AddUserAsync(factory, firstName, "Грант");
        await MembershipTestData.AddMemberAsync(factory, structure.OrganizationId, id, role);

        return id;
    }

    private async Task SeedAsync(ResourceScope scope, Guid organizationId, Guid objectId, string userId, GrantRole role) =>
        await OrganizationsTestData.InUnitOfWorkAsync(factory, async (services, ct) =>
        {
            await services.GetRequiredService<IGrantRepository>().AddAsync(scope, organizationId, objectId, userId, role, "seed", OrganizationsTestData.Now, ct);

            return true;
        });

    // US-1 · the administrator gives, lists, changes and takes back a grant: 201 with the row, 200 with the changed role, 204.
    [Fact]
    public async Task Admin_GivesChangesAndRevokesAProjectGrant()
    {
        var (structure, admin) = await WorldAsync();
        var vera = await MemberAsync(structure, "Вера");

        var given = await SendAsync(HttpMethod.Post, ProjectGrants(structure), new { userId = vera, role = "member" }, admin.Token);
        Assert.Equal(HttpStatusCode.Created, given.Status);
        Assert.Equal(("member", "held"), (given.Body.GetProperty("role").GetString(), given.Body.GetProperty("origin").GetString()));

        var changed = await SendAsync(HttpMethod.Put, $"{ProjectGrants(structure)}/{vera}", new { role = "manager" }, admin.Token);
        Assert.Equal((HttpStatusCode.OK, "manager"), (changed.Status, changed.Body.GetProperty("role").GetString()));

        var listed = await SendAsync(HttpMethod.Get, ProjectGrants(structure), null, admin.Token);
        Assert.Equal([vera], listed.Body.GetProperty("items").EnumerateArray().Select(row => row.GetProperty("user").GetProperty("id").GetString()));

        Assert.Equal(HttpStatusCode.NoContent, (await SendAsync(HttpMethod.Delete, $"{ProjectGrants(structure)}/{vera}", null, admin.Token)).Status);
        Assert.Empty((await SendAsync(HttpMethod.Get, ProjectGrants(structure), null, admin.Token)).Body.GetProperty("items").EnumerateArray());
    }

    // US-1 · a project manager (a grant, an organization viewer) gives access on their own project, and only there.
    [Fact]
    public async Task AProjectManager_GivesAccessOnTheirProject_AndGetsForbiddenOnAnother()
    {
        var (structure, _) = await WorldAsync();
        var other = await OrganizationsTestData.CreateProjectAsync(factory, structure.OrganizationId, "Другой", "OTH");
        var manager = await MembershipTestData.NewMemberAsync(factory, structure.OrganizationId, OrganizationRole.Viewer);
        var vera = await MemberAsync(structure, "Вера");
        await SeedAsync(ResourceScope.Project, structure.OrganizationId, structure.ProjectId, manager.Id, GrantRole.Manager);

        var own = await SendAsync(HttpMethod.Post, ProjectGrants(structure), new { userId = vera, role = "reader" }, manager.Token);
        var foreign = await SendAsync(
            HttpMethod.Post, $"{OrganizationsApi.Project(structure.OrganizationId, other.Project.Id)}/grants", new { userId = vera, role = "reader" }, manager.Token);

        Assert.True(own.Status == HttpStatusCode.Created, own.Body.ToString());
        Assert.Equal((HttpStatusCode.Forbidden, "organizations.access.forbidden"), (foreign.Status, foreign.ErrorCode));
    }

    // US-1 · a project member or reader gives nothing; a plain viewer neither.
    [Theory]
    [InlineData(GrantRole.Reader)]
    [InlineData(GrantRole.Member)]
    public async Task AProjectReaderOrMember_CannotGive(GrantRole role)
    {
        var (structure, _) = await WorldAsync();
        var holder = await MembershipTestData.NewMemberAsync(factory, structure.OrganizationId, OrganizationRole.Viewer);
        var vera = await MemberAsync(structure, "Вера");
        await SeedAsync(ResourceScope.Project, structure.OrganizationId, structure.ProjectId, holder.Id, role);

        var answer = await SendAsync(HttpMethod.Post, ProjectGrants(structure), new { userId = vera, role = "reader" }, holder.Token);

        Assert.Equal(HttpStatusCode.Forbidden, answer.Status);
    }

    // US-1 · a team manager manages the team's grants; a team member does not.
    [Fact]
    public async Task ATeamManager_GivesAccessOnTheTeam_ATeamMemberCannot()
    {
        var (structure, _) = await WorldAsync();
        var teamId = structure.Platform.Id;
        var manager = await MembershipTestData.NewMemberAsync(factory, structure.OrganizationId, OrganizationRole.Viewer);
        var member = await MembershipTestData.NewMemberAsync(factory, structure.OrganizationId, OrganizationRole.Viewer);
        var vera = await MemberAsync(structure, "Вера");
        await SeedAsync(ResourceScope.Team, structure.OrganizationId, teamId, manager.Id, GrantRole.Manager);
        await SeedAsync(ResourceScope.Team, structure.OrganizationId, teamId, member.Id, GrantRole.Member);

        var byManager = await SendAsync(HttpMethod.Post, TeamGrants(structure, teamId), new { userId = vera, role = "member" }, manager.Token);
        var byMember = await SendAsync(HttpMethod.Post, TeamGrants(structure, teamId), new { userId = vera, role = "reader" }, member.Token);

        Assert.Equal((HttpStatusCode.Created, HttpStatusCode.Forbidden), (byManager.Status, byMember.Status));
    }

    // US-1 · a department manager reaches a project under the department while the node's switch is on; the project's grants list
    // carries the department's grant as an inherited row.
    [Fact]
    public async Task ADepartmentManager_ReachesAProjectUnderTheDepartment_AndItsGrantListsAsInherited()
    {
        var (structure, admin) = await WorldAsync();
        var department = await OrganizationsTestData.AddDepartmentAsync(factory, structure.OrganizationId, "Отдел");
        var project = await OrganizationsTestData.InUnitOfWorkAsync(factory, async (services, ct) =>
        {
            var projects = services.GetRequiredService<IProjectRepository>();
            var created = Project.Create(structure.OrganizationId, OrganizationsTestData.Name("Под отделом"), "DPT", OrganizationsTestData.Now, "seed", "blue", parentAreaId: department.Id);
            projects.Add(created);
            await projects.SaveAsync(ct);

            return created.Project;
        });
        var manager = await MembershipTestData.NewMemberAsync(factory, structure.OrganizationId, OrganizationRole.Viewer);
        var vera = await MemberAsync(structure, "Вера");
        await SeedAsync(ResourceScope.Area, structure.OrganizationId, department.Id, manager.Id, GrantRole.Manager);
        var path = $"{OrganizationsApi.Project(structure.OrganizationId, project.Id)}/grants";

        var given = await SendAsync(HttpMethod.Post, path, new { userId = vera, role = "reader" }, manager.Token);
        var listed = await SendAsync(HttpMethod.Get, path, null, admin.Token);

        Assert.Equal(HttpStatusCode.Created, given.Status);
        var rows = listed.Body.GetProperty("items").EnumerateArray().ToList();
        Assert.Equal(
            [("department", manager.Id), ("held", vera)],
            rows.Select(row => (row.GetProperty("origin").GetString()!, row.GetProperty("user").GetProperty("id").GetString()!)).Order());
    }

    // BR-SA-19 · nobody gives access to themselves, even the administrator; the answer is 400 before anything is written.
    [Fact]
    public async Task GivingAccessToOneself_Returns400SelfGrant_AndWritesNothing()
    {
        var (structure, admin) = await WorldAsync();

        var give = await SendAsync(HttpMethod.Post, ProjectGrants(structure), new { userId = admin.Id, role = "manager" }, admin.Token);
        var change = await SendAsync(HttpMethod.Put, $"{ProjectGrants(structure)}/{admin.Id}", new { role = "manager" }, admin.Token);

        Assert.All([give, change], answer => Assert.Equal((HttpStatusCode.BadRequest, "organizations.validation_failed", "organizations.grant.self_grant"), (answer.Status, answer.ErrorCode, FieldCode(answer, "userId"))));
        Assert.Empty((await SendAsync(HttpMethod.Get, ProjectGrants(structure), null, admin.Token)).Body.GetProperty("items").EnumerateArray());
    }

    // US-1 · an unknown role is 400 with the role's code; the role is spelled in lower case, as the wire says.
    [Theory]
    [InlineData("viewer")]
    [InlineData("Manager")]
    [InlineData("")]
    public async Task AnUnknownRole_Returns400RoleInvalid(string role)
    {
        var (structure, admin) = await WorldAsync();
        var vera = await MemberAsync(structure, "Вера");

        var answer = await SendAsync(HttpMethod.Post, ProjectGrants(structure), new { userId = vera, role }, admin.Token);

        Assert.Equal((HttpStatusCode.BadRequest, "organizations.grant.role_invalid"), (answer.Status, FieldCode(answer, "role")));
    }

    // BR-SA-4 · a project node and a sub-area hold no grant; a department does.
    [Fact]
    public async Task AGrantOnAProjectNode_Returns409ScopeNotAllowed_OnADepartmentItIsGiven()
    {
        var (structure, admin) = await WorldAsync();
        var vera = await MemberAsync(structure, "Вера");
        var department = await OrganizationsTestData.AddDepartmentAsync(factory, structure.OrganizationId, "Отдел");

        var onNode = await SendAsync(HttpMethod.Post, AreaGrants(structure, structure.RootId), new { userId = vera, role = "reader" }, admin.Token);
        var onDepartment = await SendAsync(HttpMethod.Post, AreaGrants(structure, department.Id), new { userId = vera, role = "reader" }, admin.Token);

        Assert.Equal((HttpStatusCode.Conflict, "organizations.grant.scope_not_allowed"), (onNode.Status, onNode.ErrorCode));
        Assert.Equal(HttpStatusCode.Created, onDepartment.Status);
    }

    // §2 · the grantee must be an active member of THIS organization: a stranger, a member of another organization and an unknown id alike.
    [Fact]
    public async Task AGranteeWhoIsNotAMember_Returns404MembershipNotFound()
    {
        var (structure, admin) = await WorldAsync();
        var other = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var elsewhere = MembershipTestData.NewUserId();
        await MembershipTestData.AddMemberAsync(factory, other.Id, elsewhere);

        foreach (var userId in new[] { elsewhere, MembershipTestData.NewUserId() })
        {
            var answer = await SendAsync(HttpMethod.Post, ProjectGrants(structure), new { userId, role = "reader" }, admin.Token);

            Assert.Equal((HttpStatusCode.NotFound, "organizations.membership.not_found"), (answer.Status, answer.ErrorCode));
        }
    }

    // §2 · a second POST for the same member, and a PUT or DELETE of a grant that is not there, are 409 «grant already changed».
    [Fact]
    public async Task APostOnAHeldGrant_AndAPutOrDeleteOfAMissingOne_Return409Changed()
    {
        var (structure, admin) = await WorldAsync();
        var vera = await MemberAsync(structure, "Вера");
        var oleg = await MemberAsync(structure, "Олег");
        await SendAsync(HttpMethod.Post, ProjectGrants(structure), new { userId = vera, role = "reader" }, admin.Token);

        var again = await SendAsync(HttpMethod.Post, ProjectGrants(structure), new { userId = vera, role = "manager" }, admin.Token);
        var put = await SendAsync(HttpMethod.Put, $"{ProjectGrants(structure)}/{oleg}", new { role = "reader" }, admin.Token);
        var delete = await SendAsync(HttpMethod.Delete, $"{ProjectGrants(structure)}/{oleg}", null, admin.Token);

        Assert.All([again, put, delete], answer => Assert.Equal((HttpStatusCode.Conflict, "organizations.grant.changed"), (answer.Status, answer.ErrorCode)));
    }

    // BR-SA-3 · removing a membership ends the member's grants in the same statement.
    [Fact]
    public async Task RemovingTheMembership_EndsTheGrants()
    {
        var (structure, admin) = await WorldAsync();
        var vera = await MemberAsync(structure, "Вера");
        await SendAsync(HttpMethod.Post, ProjectGrants(structure), new { userId = vera, role = "reader" }, admin.Token);

        await MembershipTestData.RemoveMemberAsync(factory, structure.OrganizationId, vera);

        Assert.Empty((await SendAsync(HttpMethod.Get, ProjectGrants(structure), null, admin.Token)).Body.GetProperty("items").EnumerateArray());
    }

    // OQ-D2 · the picker leaves out the caller and everyone who already holds a grant on this object.
    [Fact]
    public async Task TheCandidates_LeaveOutTheCallerAndTheHolders()
    {
        var (structure, admin) = await WorldAsync();
        var held = await MemberAsync(structure, "Держатель");
        var free = await MemberAsync(structure, "Свободный");
        await SendAsync(HttpMethod.Post, ProjectGrants(structure), new { userId = held, role = "reader" }, admin.Token);

        var answer = await SendAsync(HttpMethod.Get, $"{OrganizationsApi.Project(structure.OrganizationId, structure.ProjectId)}/grant-candidates?text=Грант", null, admin.Token);

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Equal([free], answer.Body.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetString()));
    }

    // The order of answers · 401, then 404 of the organization for a stranger, then 404 of the object for the administrator.
    [Fact]
    public async Task TheOrderOfAnswers_Is401_Then404Organization_Then404Object()
    {
        var (structure, admin) = await WorldAsync();
        var missing = $"{OrganizationsApi.Project(structure.OrganizationId, Guid.CreateVersion7())}/grants";

        Assert.Equal(HttpStatusCode.Unauthorized, (await SendAsync(HttpMethod.Get, ProjectGrants(structure), null, null)).Status);
        Assert.Equal(
            "organizations.organization.not_found",
            (await SendAsync(HttpMethod.Get, ProjectGrants(structure), null, MembershipTestData.Outsider())).ErrorCode);
        Assert.Equal("organizations.project.not_found", (await SendAsync(HttpMethod.Get, missing, null, admin.Token)).ErrorCode);
    }
}
