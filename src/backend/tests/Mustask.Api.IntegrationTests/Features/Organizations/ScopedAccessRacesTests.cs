using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Mustask.Api.IntegrationTests.Features.WorkItems;
using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Identity;
using Mustask.Domain.Organizations;
using static Mustask.Api.IntegrationTests.Features.Organizations.TeamTestData;

namespace Mustask.Api.IntegrationTests.Features.Organizations;

/// <summary>
/// The concurrency scenarios of mod-019 US-1..US-3 (tech-065, QA debt D-5): a membership removal racing a grant (BR-SA-3), a node move
/// racing the inherit switch on the locked row (BR-SA-13), and a grant removal racing a role change of that grant. Each pair is
/// ordered by a commit gate, not by time: the first request is held at its commit with its locks, the second is sent and must block on
/// the database, then both are released and the end state is read back.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class ScopedAccessRacesTests(MustaskApiFactory factory)
{
    private async Task SeedAsync(ResourceScope scope, Guid organizationId, Guid objectId, string userId, GrantRole role) =>
        await OrganizationsTestData.InUnitOfWorkAsync(factory, async (services, ct) =>
        {
            await services.GetRequiredService<IGrantRepository>().AddAsync(scope, organizationId, objectId, userId, role, "seed", OrganizationsTestData.Now, ct);

            return true;
        });

    private static string Grants(OrganizationStructure s) => $"{OrganizationsApi.Project(s.OrganizationId, s.ProjectId)}/grants";

    private async Task<(OrganizationStructure S, MembershipTestData.TestMember Admin, string Grantee)> GrantWorldAsync()
    {
        var s = await OrganizationsTestData.CreateStructureAsync(factory);
        var admin = await MembershipTestData.NewMemberAsync(factory, s.OrganizationId, OrganizationRole.Admin);
        var grantee = await WorkItemsTestData.AddUserAsync(factory, "Гонка", "Грант");
        await MembershipTestData.AddMemberAsync(factory, s.OrganizationId, grantee, OrganizationRole.Viewer);

        return (s, admin, grantee);
    }

    private async Task<IReadOnlyList<string?>> HoldersAsync(OrganizationStructure s, string adminToken)
    {
        var listed = await OrganizationsApi.SendAsync(factory, HttpMethod.Get, Grants(s), null, adminToken);

        return listed.Body.GetProperty("items").EnumerateArray().Select(row => row.GetProperty("user").GetProperty("id").GetString()).ToList();
    }

    private Task<Answer> GiveAsync(OrganizationStructure s, string grantee, string token, CommitGate? gate) =>
        OrganizationsApi.SendAsync(factory, HttpMethod.Post, Grants(s), new { userId = grantee, role = "reader" }, token, gate);

    private Task<Answer> RemoveMemberAsync(OrganizationStructure s, string grantee, string token, CommitGate? gate) =>
        OrganizationsApi.SendAsync(factory, HttpMethod.Delete, $"{OrganizationsApi.Organization(s.OrganizationId)}/members/{grantee}", null, token, gate);

    private Task<Answer> ChangeRoleAsync(OrganizationStructure s, string grantee, string token, CommitGate? gate) =>
        OrganizationsApi.SendAsync(factory, HttpMethod.Put, $"{Grants(s)}/{grantee}", new { role = "member" }, token, gate);

    private Task<Answer> RevokeAsync(OrganizationStructure s, string grantee, string token, CommitGate? gate) =>
        OrganizationsApi.SendAsync(factory, HttpMethod.Delete, $"{Grants(s)}/{grantee}", null, token, gate);

    // BR-SA-3 · a grant written while the member is being removed loses: the give holds the membership key, the removal waits, and
    // its cascade takes the new row away. Both are answered as asked and no grant survives for a removed member.
    [Fact]
    public async Task GiveThenRemoval_TheRemovalWaitsAndCascades_NoGrantSurvives()
    {
        var (s, admin, grantee) = await GrantWorldAsync();

        var (give, removal) = await RaceAsync(
            factory, at => GiveAsync(s, grantee, admin.Token, at), at => RemoveMemberAsync(s, grantee, admin.Token, at));

        Assert.Equal((HttpStatusCode.Created, HttpStatusCode.NoContent), (give.Status, removal.Status));
        Assert.Null(await MembershipTestData.FindMembershipAsync(factory, s.OrganizationId, grantee));
        Assert.Empty(await HoldersAsync(s, admin.Token));
    }

    // BR-SA-3 · the removal first: a give that read the member before the removal committed is refused cleanly, never a 500.
    [Fact]
    public async Task RemovalThenGive_TheGiveIsRefusedWithoutA500_NoGrantIsStored()
    {
        var (s, admin, grantee) = await GrantWorldAsync();

        var (removal, give) = await RaceAsync(
            factory, at => RemoveMemberAsync(s, grantee, admin.Token, at), at => GiveAsync(s, grantee, admin.Token, at));

        Assert.Equal(HttpStatusCode.NoContent, removal.Status);
        Assert.True((int)give.Status is >= 400 and < 500, $"The give answered {(int)give.Status}.");
        Assert.Null(await MembershipTestData.FindMembershipAsync(factory, s.OrganizationId, grantee));
        Assert.Empty(await HoldersAsync(s, admin.Token));
    }

    // US-1 · a grant removal against a role change of the same grant, the change first: the change is kept in order, then the grant is gone.
    [Fact]
    public async Task RoleChangeThenRemoval_BothSucceed_TheGrantIsGone()
    {
        var (s, admin, grantee) = await GrantWorldAsync();
        await SeedAsync(ResourceScope.Project, s.OrganizationId, s.ProjectId, grantee, GrantRole.Reader);

        var (change, removal) = await RaceAsync(
            factory, at => ChangeRoleAsync(s, grantee, admin.Token, at), at => RevokeAsync(s, grantee, admin.Token, at));

        Assert.Equal((HttpStatusCode.OK, HttpStatusCode.NoContent), (change.Status, removal.Status));
        Assert.Empty(await HoldersAsync(s, admin.Token));
    }

    // US-1 · the removal first: the change finds the grant gone and answers the documented 409 grant.changed (no 500, nothing re-created).
    [Fact]
    public async Task RemovalThenRoleChange_TheChangeAnswersGrantChanged_TheGrantStaysGone()
    {
        var (s, admin, grantee) = await GrantWorldAsync();
        await SeedAsync(ResourceScope.Project, s.OrganizationId, s.ProjectId, grantee, GrantRole.Reader);

        var (removal, change) = await RaceAsync(
            factory, at => RevokeAsync(s, grantee, admin.Token, at), at => ChangeRoleAsync(s, grantee, admin.Token, at));

        Assert.Equal(HttpStatusCode.NoContent, removal.Status);
        Assert.Equal((HttpStatusCode.Conflict, OrganizationErrorCodes.GrantChanged), (change.Status, change.ErrorCode));
        Assert.Empty(await HoldersAsync(s, admin.Token));
    }

    private sealed record Tree(OrganizationStructure Structure, MembershipTestData.TestMember Admin, Area Source, Area Destination, Area Node);

    // Two departments and a project under the first one; the node inherits.
    private async Task<Tree> TreeAsync()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var admin = await MembershipTestData.NewMemberAsync(factory, structure.OrganizationId, OrganizationRole.Admin);
        var source = await OrganizationsTestData.AddDepartmentAsync(factory, structure.OrganizationId, "Исходный");
        var destination = await OrganizationsTestData.AddDepartmentAsync(factory, structure.OrganizationId, "Целевой");
        var created = await OrganizationsTestData.InUnitOfWorkAsync(factory, async (services, ct) =>
        {
            var projects = services.GetRequiredService<IProjectRepository>();
            var project = Project.Create(structure.OrganizationId, OrganizationsTestData.Name("Под отделом"), "RCE", OrganizationsTestData.Now, "seed", parentAreaId: source.Id);
            projects.Add(project);
            await projects.SaveAsync(ct);

            return project;
        });

        return new Tree(structure, admin, source, destination, created.Root);
    }

    private Task<Answer> MoveAsync(Tree t, string token, CommitGate? gate) =>
        OrganizationsApi.SendAsync(
            factory, HttpMethod.Put, $"{OrganizationsApi.Areas(t.Structure.OrganizationId)}/{t.Node.Id}/parent", new { parentId = t.Destination.Id.ToString() }, token, gate);

    private Task<Answer> SwitchOffAsync(Tree t, string token, CommitGate? gate) =>
        OrganizationsApi.SendAsync(
            factory, HttpMethod.Put, $"{OrganizationsApi.Areas(t.Structure.OrganizationId)}/{t.Node.Id}/inherits-access", new { value = false }, token, gate);

    private async Task<Area> StoredNodeAsync(Tree t)
    {
        using var scope = factory.Services.CreateScope();

        return (await scope.ServiceProvider.GetRequiredService<IAreaRepository>().FindAsync(t.Structure.OrganizationId, t.Node.Id, default))!;
    }

    // BR-SA-13 · a move against a switch by administrators, both orders: both finish, and the end state is the same in either serial order.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task MoveAndSwitch_ByAnAdministrator_BothFinish_TheEndStateIsTheSameInEitherOrder(bool moveFirst)
    {
        var t = await TreeAsync();

        var (first, second) = moveFirst
            ? await RaceAsync(factory, at => MoveAsync(t, t.Admin.Token, at), at => SwitchOffAsync(t, t.Admin.Token, at))
            : await RaceAsync(factory, at => SwitchOffAsync(t, t.Admin.Token, at), at => MoveAsync(t, t.Admin.Token, at));

        Assert.Equal((HttpStatusCode.OK, HttpStatusCode.OK), (first.Status, second.Status));
        var stored = await StoredNodeAsync(t);
        Assert.Equal((t.Destination.Id, false), (stored.ParentId, stored.InheritsAccess));
    }

    // BR-SA-13 · the switch is allowed by the node old parent only. The move commits first; the switch, waiting on the tree lock, then
    // judges the code on the new parent, finds none and is refused: no stale pass, the node keeps inheriting.
    [Fact]
    public async Task AMoveOutOfTheManagersDepartment_RefusesTheSwitchThatWaitedOnIt()
    {
        var t = await TreeAsync();
        var manager = await MembershipTestData.NewMemberAsync(factory, t.Structure.OrganizationId, OrganizationRole.Viewer);
        await SeedAsync(ResourceScope.Area, t.Structure.OrganizationId, t.Source.Id, manager.Id, GrantRole.Manager);

        var (move, switchOff) = await RaceAsync(factory, at => MoveAsync(t, t.Admin.Token, at), at => SwitchOffAsync(t, manager.Token, at));

        Assert.Equal(HttpStatusCode.OK, move.Status);
        Assert.Equal((HttpStatusCode.Forbidden, OrganizationErrorCodes.AccessForbidden), (switchOff.Status, switchOff.ErrorCode));
        var stored = await StoredNodeAsync(t);
        Assert.Equal((t.Destination.Id, true), (stored.ParentId, stored.InheritsAccess));
    }

    // BR-SA-13 · the other order: the switch commits while the node is still in the manager department, then the move follows.
    [Fact]
    public async Task TheManagersSwitchThenTheMove_BothApply()
    {
        var t = await TreeAsync();
        var manager = await MembershipTestData.NewMemberAsync(factory, t.Structure.OrganizationId, OrganizationRole.Viewer);
        await SeedAsync(ResourceScope.Area, t.Structure.OrganizationId, t.Source.Id, manager.Id, GrantRole.Manager);

        var (switchOff, move) = await RaceAsync(factory, at => SwitchOffAsync(t, manager.Token, at), at => MoveAsync(t, t.Admin.Token, at));

        Assert.Equal((HttpStatusCode.OK, HttpStatusCode.OK), (switchOff.Status, move.Status));
        var stored = await StoredNodeAsync(t);
        Assert.Equal((t.Destination.Id, false), (stored.ParentId, stored.InheritsAccess));
    }
}
