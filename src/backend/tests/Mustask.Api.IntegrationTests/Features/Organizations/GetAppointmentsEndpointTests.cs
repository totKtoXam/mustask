using System.Net;
using Mustask.Domain.Identity;
using Mustask.Domain.Organizations;

namespace Mustask.Api.IntegrationTests.Features.Organizations;

/// <summary>
/// The appointment record's two reads (feat-033 api-contract § 1.4 and § 2; US-3 "An appointment leaves a record"): the rescue
/// route for a system administrator (any organization, deactivated included) and the organization route for its administrators.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class GetAppointmentsEndpointTests(MustaskApiFactory factory)
{
    private Task<Answer> RescueReadAsync(Guid id, string? token) =>
        OrganizationsApi.SendAsync(factory, HttpMethod.Get, RescueTestData.AppointmentsPath(id), token: token);

    private Task<Answer> OrganizationReadAsync(Guid id, string? token) =>
        OrganizationsApi.SendAsync(factory, HttpMethod.Get, RescueTestData.OrganizationAppointmentsPath(id), token: token);

    // US-3: newest first; both routes return the same rows; the record outlives the appointee's later role change and removal.
    [Fact]
    public async Task Read_NewestFirst_TheSameFromBothRoutes_AndTheRecordSurvivesTheAppointeesChanges()
    {
        var organization = await RescueTestData.OrphanedOrganizationAsync(factory);
        var admin = await MembershipTestData.NewNamedMemberAsync(factory, organization.Id, OrganizationRole.Admin, "Анна", "Админова");
        var (_, rescuer) = RescueTestData.NewRescuer();
        var first = await RescueTestData.SignedInUserAsync(factory, "Первая", "Назначенная");
        var second = await RescueTestData.SignedInUserAsync(factory, "Вторая", "Назначенная");
        await OrganizationsApi.SendAsync(factory, HttpMethod.Post, RescueTestData.AppointmentsPath(organization.Id), new { userId = first, reason = "Один" }, rescuer);
        await OrganizationsApi.SendAsync(factory, HttpMethod.Post, RescueTestData.AppointmentsPath(organization.Id), new { userId = second, reason = "Два" }, rescuer);
        await OrganizationsApi.SendAsync(
            factory, HttpMethod.Put, $"{OrganizationsApi.Organization(organization.Id)}/members/{first}/role", new { role = "viewer" }, admin.Token);
        await MembershipTestData.RemoveMemberAsync(factory, organization.Id, second);

        var fromRescue = await RescueReadAsync(organization.Id, rescuer);
        var fromOrganization = await OrganizationReadAsync(organization.Id, admin.Token);

        Assert.Equal(HttpStatusCode.OK, fromRescue.Status);
        Assert.Equal(["Два", "Один"], fromRescue.Body.EnumerateArray().Select(r => r.GetProperty("reason").GetString()));
        Assert.Equal(fromRescue.Body.GetRawText(), fromOrganization.Body.GetRawText());
        Assert.Equal(
            new[] { "id", "appointedAt", "appointedBy", "appointee", "reason" }.Order(StringComparer.Ordinal),
            fromRescue.Body[0].EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task Read_NoAppointments_IsAnEmptyList_AndARefusedAppointmentLeavesNone()
    {
        var organization = await RescueTestData.OrphanedOrganizationAsync(factory, status: OrganizationStatus.Deactivated);
        var (_, rescuer) = RescueTestData.NewRescuer();

        await OrganizationsApi.SendAsync(
            factory, HttpMethod.Post, RescueTestData.AppointmentsPath(organization.Id), new { userId = MembershipTestData.NewUserId(), reason = "Х" }, rescuer);
        var answer = await RescueReadAsync(organization.Id, rescuer);

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Empty(answer.Body.EnumerateArray());
    }

    // US-3 / mod-016: an organization's own administrator reads; a member or viewer is refused 403; a stranger gets the 404.
    [Fact]
    public async Task OrganizationRoute_AdministratorReads_OthersAreRefused()
    {
        var organization = await RescueTestData.OrphanedOrganizationAsync(factory);
        var admin = await MembershipTestData.NewMemberAsync(factory, organization.Id, OrganizationRole.Admin);
        var member = await MembershipTestData.NewMemberAsync(factory, organization.Id, OrganizationRole.Member);
        var viewer = await MembershipTestData.NewMemberAsync(factory, organization.Id, OrganizationRole.Viewer);

        Assert.Equal(HttpStatusCode.OK, (await OrganizationReadAsync(organization.Id, admin.Token)).Status);
        Assert.Equal(HttpStatusCode.Forbidden, (await OrganizationReadAsync(organization.Id, member.Token)).Status);
        Assert.Equal(HttpStatusCode.Forbidden, (await OrganizationReadAsync(organization.Id, viewer.Token)).Status);
        var stranger = await OrganizationReadAsync(organization.Id, MembershipTestData.Outsider());
        Assert.Equal((HttpStatusCode.NotFound, OrganizationErrorCodes.OrganizationNotFound), (stranger.Status, stranger.ErrorCode));
        Assert.Equal(HttpStatusCode.Unauthorized, (await OrganizationReadAsync(organization.Id, null)).Status);
    }

    [Fact]
    public async Task RescueRoute_UnknownOrMalformedOrganization_Returns404_AndWithoutTheRight403()
    {
        var (_, rescuer) = RescueTestData.NewRescuer();

        var unknown = await RescueReadAsync(Guid.CreateVersion7(), rescuer);
        var malformed = await OrganizationsApi.SendAsync(factory, HttpMethod.Get, $"{RescueTestData.Rescue}/nope/appointments", token: rescuer);
        var forbidden = await RescueReadAsync(Guid.CreateVersion7(), OrganizationsTokens.AllInstanceRolesButSystemAdmin());

        Assert.Equal((HttpStatusCode.NotFound, OrganizationErrorCodes.OrganizationNotFound), (unknown.Status, unknown.ErrorCode));
        Assert.Equal((HttpStatusCode.NotFound, OrganizationErrorCodes.OrganizationNotFound), (malformed.Status, malformed.ErrorCode));
        Assert.Equal((HttpStatusCode.Forbidden, OrganizationErrorCodes.RescueForbidden), (forbidden.Status, forbidden.ErrorCode));
    }
}
