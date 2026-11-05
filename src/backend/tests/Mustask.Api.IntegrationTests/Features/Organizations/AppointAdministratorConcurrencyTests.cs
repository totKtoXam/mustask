using System.Net;
using Mustask.Domain.Identity;
using Mustask.Domain.Organizations;

namespace Mustask.Api.IntegrationTests.Features.Organizations;

/// <summary>
/// The appointment against concurrent writes (feat-033 BR-RS-11): it locks the organization row, then the user's membership row, so
/// a second request waits and then decides on what the first committed. A request is held at its commit with <c>CommitGates</c>.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class AppointAdministratorConcurrencyTests(MustaskApiFactory factory)
{
    private Task<Answer> AppointAsync(Guid organizationId, string userId, string token, CommitGate? gate = null) =>
        OrganizationsApi.SendAsync(
            factory, HttpMethod.Post, RescueTestData.AppointmentsPath(organizationId), new { userId, reason = "Гонка" }, token, gate);

    private static async Task HoldAsync(CommitGate gate, Task<Answer> held)
    {
        await Task.WhenAny(gate.Reached, held);
        Assert.False(held.IsCompleted, "The held request finished before reaching its commit gate.");
    }

    private async Task WaitsAsync(Task<Answer> waiting)
    {
        await PgLocks.WaitUntilBlockedAsync(factory.ConnectionString, waiting);
        Assert.False(waiting.IsCompleted, "The second request did not wait for the first one's lock.");
    }

    // The same user twice: exactly one appointment is saved, the other is told they are already an administrator; one record.
    [Fact]
    public async Task SameUserTwice_OneCreated_OneAlreadyAdmin_OneRecord()
    {
        var organization = await RescueTestData.OrphanedOrganizationAsync(factory);
        var user = await RescueTestData.SignedInUserAsync(factory, "Гонка", "Один");
        var (_, one) = RescueTestData.NewRescuer();
        var (_, two) = RescueTestData.NewRescuer();
        using var gate = factory.CommitGates.Open();

        var first = AppointAsync(organization.Id, user, one, gate);
        await HoldAsync(gate, first);
        var second = AppointAsync(organization.Id, user, two);
        await WaitsAsync(second);
        gate.Release();
        var (created, refused) = (await first, await second);

        Assert.Equal(HttpStatusCode.Created, created.Status);
        Assert.Equal((HttpStatusCode.Conflict, OrganizationErrorCodes.RescueAlreadyAdmin), (refused.Status, refused.ErrorCode));
        Assert.Single(await RescueTestData.RecordsAsync(factory, organization.Id));
    }

    [Fact]
    public async Task DifferentUsers_BothAreAppointed_TwoRecords()
    {
        var organization = await RescueTestData.OrphanedOrganizationAsync(factory);
        var a = await RescueTestData.SignedInUserAsync(factory, "Гонка", "Два");
        var b = await RescueTestData.SignedInUserAsync(factory, "Гонка", "Три");
        var (_, token) = RescueTestData.NewRescuer();
        using var gate = factory.CommitGates.Open();

        var first = AppointAsync(organization.Id, a, token, gate);
        await HoldAsync(gate, first);
        var second = AppointAsync(organization.Id, b, token);
        await WaitsAsync(second);
        gate.Release();

        Assert.Equal(HttpStatusCode.Created, (await first).Status);
        Assert.Equal(HttpStatusCode.Created, (await second).Status);
        Assert.Equal(2, (await RescueTestData.RecordsAsync(factory, organization.Id)).Count);
    }

    // Against a removal of the same person: appointment first, then the removal succeeds and the record stays.
    [Fact]
    public async Task AppointmentThenRemoval_TheMemberIsGone_TheRecordStays()
    {
        var organization = await RescueTestData.OrphanedOrganizationAsync(factory);
        var admin = await MembershipTestData.NewNamedMemberAsync(factory, organization.Id, OrganizationRole.Admin, "Анна", "Админова");
        var viewer = await MembershipTestData.NewNamedMemberAsync(factory, organization.Id, OrganizationRole.Viewer, "Читатель", "Известный");
        var (_, token) = RescueTestData.NewRescuer();
        using var gate = factory.CommitGates.Open();

        var appointment = AppointAsync(organization.Id, viewer.Id, token, gate);
        await HoldAsync(gate, appointment);
        var removal = OrganizationsApi.SendAsync(
            factory, HttpMethod.Delete, $"{OrganizationsApi.Organization(organization.Id)}/members/{viewer.Id}", token: admin.Token);
        await WaitsAsync(removal);
        gate.Release();

        Assert.Equal(HttpStatusCode.Created, (await appointment).Status);
        Assert.Equal(HttpStatusCode.NoContent, (await removal).Status);
        Assert.Null(await MembershipTestData.FindMembershipAsync(factory, organization.Id, viewer.Id));
        Assert.Single(await RescueTestData.RecordsAsync(factory, organization.Id));
    }

    // The removal first: the appointment then finds no membership and adds the person as a new administrator.
    [Fact]
    public async Task RemovalThenAppointment_TheMemberIsAddedAsAnAdministrator()
    {
        var organization = await RescueTestData.OrphanedOrganizationAsync(factory);
        var admin = await MembershipTestData.NewNamedMemberAsync(factory, organization.Id, OrganizationRole.Admin, "Анна", "Админова");
        var viewer = await RescueTestData.SignedInUserAsync(factory, "Уходит", "Читатель");
        await MembershipTestData.AddMemberAsync(factory, organization.Id, viewer, OrganizationRole.Viewer);
        var (rescuerId, token) = RescueTestData.NewRescuer();
        using var gate = factory.CommitGates.Open();

        var removal = OrganizationsApi.SendAsync(
            factory, HttpMethod.Delete, $"{OrganizationsApi.Organization(organization.Id)}/members/{viewer}", token: admin.Token, gate: gate);
        await HoldAsync(gate, removal);
        var appointment = AppointAsync(organization.Id, viewer, token);
        await WaitsAsync(appointment);
        gate.Release();

        Assert.Equal(HttpStatusCode.NoContent, (await removal).Status);
        Assert.Equal(HttpStatusCode.Created, (await appointment).Status);
        var stored = await MembershipTestData.FindMembershipAsync(factory, organization.Id, viewer);
        Assert.Equal((OrganizationRole.Admin, rescuerId, null), (stored!.Role, stored.AddedBy, stored.RoleChangedBy));
    }

    // Against a role change of the same person, in both orders: the later one decides on what the earlier committed.
    [Fact]
    public async Task RoleChangeThenAppointment_AndAppointmentThenRoleChange_EndInTheLaterOnesRole()
    {
        var organization = await RescueTestData.OrphanedOrganizationAsync(factory);
        var admin = await MembershipTestData.NewNamedMemberAsync(factory, organization.Id, OrganizationRole.Admin, "Анна", "Админова");
        var one = await MembershipTestData.NewNamedMemberAsync(factory, organization.Id, OrganizationRole.Viewer, "Один", "Известный");
        var two = await MembershipTestData.NewNamedMemberAsync(factory, organization.Id, OrganizationRole.Viewer, "Два", "Известный");
        var (_, token) = RescueTestData.NewRescuer();
        string RolePath(string id) => $"{OrganizationsApi.Organization(organization.Id)}/members/{id}/role";

        using (var gate = factory.CommitGates.Open())
        {
            var change = OrganizationsApi.SendAsync(factory, HttpMethod.Put, RolePath(one.Id), new { role = "member" }, admin.Token, gate);
            await HoldAsync(gate, change);
            var appointment = AppointAsync(organization.Id, one.Id, token);
            await WaitsAsync(appointment);
            gate.Release();

            Assert.Equal(HttpStatusCode.OK, (await change).Status);
            Assert.Equal(HttpStatusCode.Created, (await appointment).Status);
            Assert.Equal(OrganizationRole.Admin, (await MembershipTestData.FindMembershipAsync(factory, organization.Id, one.Id))!.Role);
        }

        using (var gate = factory.CommitGates.Open())
        {
            var appointment = AppointAsync(organization.Id, two.Id, token, gate);
            await HoldAsync(gate, appointment);
            var change = OrganizationsApi.SendAsync(factory, HttpMethod.Put, RolePath(two.Id), new { role = "member" }, admin.Token);
            await WaitsAsync(change);
            gate.Release();

            Assert.Equal(HttpStatusCode.Created, (await appointment).Status);
            Assert.Equal(HttpStatusCode.OK, (await change).Status);
            Assert.Equal(OrganizationRole.Member, (await MembershipTestData.FindMembershipAsync(factory, organization.Id, two.Id))!.Role);
        }
    }
}
