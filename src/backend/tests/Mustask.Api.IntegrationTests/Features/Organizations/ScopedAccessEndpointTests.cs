using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Mustask.Api.IntegrationTests.Features.WorkItems;
using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Identity;
using Mustask.Domain.Organizations;

namespace Mustask.Api.IntegrationTests.Features.Organizations;

/// <summary>
/// The scoped reads and writes of mod-019 beyond the grant routes (api-contract §§ 3-6): the «Наследовать доступ» switch and its
/// impact read, the codes served per object, the link pickers' filter and the project's creation by place.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class ScopedAccessEndpointTests(MustaskApiFactory factory)
{
    private Task<Answer> SendAsync(HttpMethod method, string path, object? body, string? token) =>
        OrganizationsApi.SendAsync(factory, method, path, body, token);

    private async Task SeedAsync(ResourceScope scope, Guid organizationId, Guid objectId, string userId, GrantRole role) =>
        await OrganizationsTestData.InUnitOfWorkAsync(factory, async (services, ct) =>
        {
            await services.GetRequiredService<IGrantRepository>().AddAsync(scope, organizationId, objectId, userId, role, "seed", OrganizationsTestData.Now, ct);

            return true;
        });

    private sealed record World(
        OrganizationStructure Structure,
        MembershipTestData.TestMember Admin,
        Area Department,
        Project UnderDepartment,
        Area Node);

    // A department, with a project under it; the structure's own project stays at the top level.
    private async Task<World> WorldAsync()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var admin = await MembershipTestData.NewMemberAsync(factory, structure.OrganizationId, OrganizationRole.Admin);
        var department = await OrganizationsTestData.AddDepartmentAsync(factory, structure.OrganizationId, "Отдел");
        var project = await OrganizationsTestData.InUnitOfWorkAsync(factory, async (services, ct) =>
        {
            var projects = services.GetRequiredService<IProjectRepository>();
            var created = Project.Create(structure.OrganizationId, OrganizationsTestData.Name("Под отделом"), "DPT", OrganizationsTestData.Now, "seed", "blue", parentAreaId: department.Id);
            projects.Add(created);
            await projects.SaveAsync(ct);

            return created;
        });

        return new World(structure, admin, department, project.Project, project.Root);
    }

    private string Switch(World w) => $"{OrganizationsApi.Areas(w.Structure.OrganizationId)}/{w.Node.Id}/inherits-access";

    private string ProjectPath(World w) => OrganizationsApi.Project(w.Structure.OrganizationId, w.UnderDepartment.Id);

    private async Task<MembershipTestData.TestMember> DepartmentManagerAsync(World w)
    {
        var manager = await MembershipTestData.NewMemberAsync(factory, w.Structure.OrganizationId, OrganizationRole.Viewer);
        await SeedAsync(ResourceScope.Area, w.Structure.OrganizationId, w.Department.Id, manager.Id, GrantRole.Manager);

        return manager;
    }

    // US-2 · a department manager turns the switch off on a project under the department; the project is theirs no more, at once.
    [Fact]
    public async Task ADepartmentManager_TurnsTheSwitchOff_AndStopsReachingTheProject()
    {
        var w = await WorldAsync();
        var manager = await DepartmentManagerAsync(w);
        var vera = await WorkItemsTestData.AddUserAsync(factory, "Вера", "Доступ");
        await MembershipTestData.AddMemberAsync(factory, w.Structure.OrganizationId, vera, OrganizationRole.Viewer);
        var grants = $"{ProjectPath(w)}/grants";

        var before = await SendAsync(HttpMethod.Post, grants, new { userId = vera, role = "reader" }, manager.Token);
        var off = await SendAsync(HttpMethod.Put, Switch(w), new { value = false }, manager.Token);
        var after = await SendAsync(HttpMethod.Post, grants, new { userId = vera, role = "manager" }, manager.Token);

        Assert.Equal(HttpStatusCode.Created, before.Status);
        Assert.Equal((HttpStatusCode.OK, false), (off.Status, off.Body.GetProperty("inheritsAccess").GetBoolean()));
        Assert.Equal(HttpStatusCode.Forbidden, after.Status);
    }

    // US-2 · the switch of a department is stored and never shown: 409 for it, and a project manager without the parent's code gets 403.
    [Fact]
    public async Task TheSwitch_OfADepartmentIs409_AndAProjectManagerWithoutTheParentsCodeIs403()
    {
        var w = await WorldAsync();
        var projectManager = await MembershipTestData.NewMemberAsync(factory, w.Structure.OrganizationId, OrganizationRole.Viewer);
        await SeedAsync(ResourceScope.Project, w.Structure.OrganizationId, w.UnderDepartment.Id, projectManager.Id, GrantRole.Manager);
        var department = $"{OrganizationsApi.Areas(w.Structure.OrganizationId)}/{w.Department.Id}/inherits-access";

        var onDepartment = await SendAsync(HttpMethod.Put, department, new { value = false }, w.Admin.Token);
        var byProjectManager = await SendAsync(HttpMethod.Put, Switch(w), new { value = false }, projectManager.Token);

        Assert.Equal((HttpStatusCode.Conflict, "organizations.area.type_not_allowed"), (onDepartment.Status, onDepartment.ErrorCode));
        Assert.Equal(HttpStatusCode.Forbidden, byProjectManager.Status);
    }

    // US-2 · a missing value is 400; a switch to the value it has is a 200 that changes nothing.
    [Fact]
    public async Task TheSwitch_WithNoValueIs400_AndTheSameValueIsAnIdempotent200()
    {
        var w = await WorldAsync();

        var missing = await SendAsync(HttpMethod.Put, Switch(w), new { }, w.Admin.Token);
        var same = await SendAsync(HttpMethod.Put, Switch(w), new { value = true }, w.Admin.Token);

        Assert.Equal(HttpStatusCode.BadRequest, missing.Status);
        Assert.Equal((HttpStatusCode.OK, true), (same.Status, same.Body.GetProperty("inheritsAccess").GetBoolean()));
    }

    // US-2, OQ-6 · the impact counts the department's grant holders who would stop being reached, and lists the project grants they gave (R-27).
    [Fact]
    public async Task TheImpact_CountsTheHoldersWhoStopBeingReached_AndListsTheGrantsTheyGave()
    {
        var w = await WorldAsync();
        var manager = await DepartmentManagerAsync(w);
        var vera = await WorkItemsTestData.AddUserAsync(factory, "Вера", "Доступ");
        await MembershipTestData.AddMemberAsync(factory, w.Structure.OrganizationId, vera, OrganizationRole.Viewer);
        await SendAsync(HttpMethod.Post, $"{ProjectPath(w)}/grants", new { userId = vera, role = "reader" }, manager.Token);

        var forAdmin = await SendAsync(HttpMethod.Get, $"{Switch(w)}/impact", null, w.Admin.Token);
        var forManager = await SendAsync(HttpMethod.Get, $"{Switch(w)}/impact", null, manager.Token);

        Assert.Equal((HttpStatusCode.OK, 1, false, 1), (forAdmin.Status, forAdmin.Body.GetProperty("holderCount").GetInt32(), forAdmin.Body.GetProperty("callerIncluded").GetBoolean(), forAdmin.Body.GetProperty("givenGrantsTotal").GetInt32()));
        Assert.Equal(vera, Assert.Single(forAdmin.Body.GetProperty("givenGrants").EnumerateArray()).GetProperty("user").GetProperty("id").GetString());
        Assert.True(forManager.Body.GetProperty("callerIncluded").GetBoolean());
    }

    // US-2 · the project read serves the department above the node and the switch; a top-level project serves neither.
    [Fact]
    public async Task TheProjectRead_ServesTheDepartmentAndTheSwitch()
    {
        var w = await WorldAsync();

        var under = await SendAsync(HttpMethod.Get, ProjectPath(w), null, w.Admin.Token);
        var top = await SendAsync(HttpMethod.Get, OrganizationsApi.Project(w.Structure.OrganizationId, w.Structure.ProjectId), null, w.Admin.Token);

        Assert.Equal((w.Department.Id.ToString(), true), (under.Body.GetProperty("department").GetProperty("id").GetString(), under.Body.GetProperty("inheritsAccess").GetBoolean()));
        Assert.Equal(System.Text.Json.JsonValueKind.Null, top.Body.GetProperty("department").ValueKind);
    }

    // US-4 · the codes served on a project are the caller's own on it: a project reader holds the reads, a manager holds the grant code.
    [Fact]
    public async Task TheServedCodes_AreTheCallersOnThatObject()
    {
        var w = await WorldAsync();
        var reader = await MembershipTestData.NewMemberAsync(factory, w.Structure.OrganizationId, OrganizationRole.Viewer);
        var manager = await MembershipTestData.NewMemberAsync(factory, w.Structure.OrganizationId, OrganizationRole.Viewer);
        await SeedAsync(ResourceScope.Project, w.Structure.OrganizationId, w.UnderDepartment.Id, reader.Id, GrantRole.Reader);
        await SeedAsync(ResourceScope.Project, w.Structure.OrganizationId, w.UnderDepartment.Id, manager.Id, GrantRole.Manager);

        var asReader = await SendAsync(HttpMethod.Get, ProjectPath(w), null, reader.Token);
        var asManager = await SendAsync(HttpMethod.Get, ProjectPath(w), null, manager.Token);

        var readerCodes = asReader.Body.GetProperty("codes").EnumerateArray().Select(code => code.GetString()).ToList();
        var managerCodes = asManager.Body.GetProperty("codes").EnumerateArray().Select(code => code.GetString()).ToList();
        Assert.DoesNotContain(PermissionCatalog.OrganizationsProjectGrant, readerCodes);
        Assert.Contains(PermissionCatalog.OrganizationsProjectGrant, managerCodes);
    }

    // OQ-D2 · the filtered picker keeps the projects the caller may link; an unknown value is 400.
    [Fact]
    public async Task LinkableFor_KeepsTheProjectsTheCallerMayLink_AndAnUnknownValueIs400()
    {
        var w = await WorldAsync();
        var manager = await MembershipTestData.NewMemberAsync(factory, w.Structure.OrganizationId, OrganizationRole.Viewer);
        await SeedAsync(ResourceScope.Project, w.Structure.OrganizationId, w.UnderDepartment.Id, manager.Id, GrantRole.Manager);
        var projects = $"{OrganizationsApi.Organization(w.Structure.OrganizationId)}/projects";

        var filtered = await SendAsync(HttpMethod.Get, $"{projects}?linkableFor=team", null, manager.Token);
        var all = await SendAsync(HttpMethod.Get, projects, null, manager.Token);
        var unknown = await SendAsync(HttpMethod.Get, $"{projects}?linkableFor=nothing", null, manager.Token);

        Assert.Equal([w.UnderDepartment.Id.ToString()], filtered.Body.EnumerateArray().Select(row => row.GetProperty("id").GetString()));
        Assert.True(all.Body.GetArrayLength() > 1);
        Assert.Equal((HttpStatusCode.BadRequest, "organizations.validation_failed"), (unknown.Status, unknown.ErrorCode));
    }

    // BR-SA-17 · the place decides the creation: a department manager creates in their department and not at the top level.
    [Fact]
    public async Task ADepartmentManager_CreatesAProjectInTheirDepartment_NotAtTheTopLevel()
    {
        var w = await WorldAsync();
        var manager = await DepartmentManagerAsync(w);
        var projects = $"{OrganizationsApi.Organization(w.Structure.OrganizationId)}/projects";

        var inside = await SendAsync(HttpMethod.Post, projects, new { name = "Новый", key = "NEW", parentAreaId = w.Department.Id.ToString() }, manager.Token);
        var top = await SendAsync(HttpMethod.Post, projects, new { name = "Верхний", key = "TOP" }, manager.Token);
        var admin = await SendAsync(HttpMethod.Post, projects, new { name = "Верхний", key = "TOP" }, w.Admin.Token);

        Assert.Equal(HttpStatusCode.Created, inside.Status);
        Assert.Equal(HttpStatusCode.Forbidden, top.Status);
        Assert.Equal(HttpStatusCode.Created, admin.Status);
    }

    // BR-SA-17 · a plain member of the organization creates nowhere, and learns nothing about an unknown place.
    [Fact]
    public async Task APlainMember_CreatesNowhere_AndTheAdminGets404ForAnUnknownPlace()
    {
        var w = await WorldAsync();
        var member = await MembershipTestData.NewMemberAsync(factory, w.Structure.OrganizationId, OrganizationRole.Member);
        var projects = $"{OrganizationsApi.Organization(w.Structure.OrganizationId)}/projects";

        var known = await SendAsync(HttpMethod.Post, projects, new { name = "А", key = "AAA", parentAreaId = w.Department.Id.ToString() }, member.Token);
        var unknown = await SendAsync(HttpMethod.Post, projects, new { name = "Б", key = "BBB", parentAreaId = Guid.CreateVersion7().ToString() }, member.Token);
        var forAdmin = await SendAsync(HttpMethod.Post, projects, new { name = "Б", key = "BBB", parentAreaId = Guid.CreateVersion7().ToString() }, w.Admin.Token);

        Assert.Equal((HttpStatusCode.Forbidden, HttpStatusCode.Forbidden), (known.Status, unknown.Status));
        Assert.Equal((HttpStatusCode.NotFound, "organizations.area.not_found"), (forAdmin.Status, forAdmin.ErrorCode));
    }

    // US-4 · the tree's writable ids: the administrator holds every node, a department manager the department and what is under it.
    [Fact]
    public async Task TheWritableIds_FollowTheCallersReach()
    {
        var w = await WorldAsync();
        var manager = await DepartmentManagerAsync(w);
        var path = $"{OrganizationsApi.Areas(w.Structure.OrganizationId)}/writable-ids";

        var ofManager = await SendAsync(HttpMethod.Get, path, null, manager.Token);
        var ofAdmin = await SendAsync(HttpMethod.Get, path, null, w.Admin.Token);

        var ids = ofManager.Body.EnumerateArray().Select(id => id.GetString()).ToList();
        Assert.Contains(w.Department.Id.ToString(), ids);
        Assert.Contains(w.Node.Id.ToString(), ids);
        Assert.DoesNotContain(w.Structure.RootId.ToString(), ids);
        Assert.Contains(w.Structure.RootId.ToString(), ofAdmin.Body.EnumerateArray().Select(id => id.GetString()));
    }
}
