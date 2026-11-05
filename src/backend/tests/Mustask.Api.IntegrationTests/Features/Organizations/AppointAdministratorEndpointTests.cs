using System.Net;
using System.Text.Json;
using Mustask.Domain.Identity;
using Mustask.Domain.Organizations;

namespace Mustask.Api.IntegrationTests.Features.Organizations;

/// <summary>
/// <c>POST /api/organizations/rescue/{organizationId}/appointments</c> (feat-033 api-contract § 1.3; US-2 "A system administrator
/// appoints an administrator", "…raises a member", "The reason is required", "Several refusals at once", "Without the right"; US-3
/// "An appointment leaves a record"): the rescuer is a <c>system-admin</c> who belongs to nothing; the appointment adds or raises to
/// <c>admin</c> and leaves one record, in one transaction, and every refusal writes nothing.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class AppointAdministratorEndpointTests(MustaskApiFactory factory)
{
    private Task<Answer> AppointAsync(Guid organizationId, object? body, string? token) =>
        OrganizationsApi.SendAsync(factory, HttpMethod.Post, RescueTestData.AppointmentsPath(organizationId), body, token);

    private static object Body(string userId, string reason = "Борис уволился") => new { userId, reason };

    // US-2 "A system administrator appoints an administrator" / US-3 "An appointment leaves a record": a non-member becomes admin,
    // added by the rescuer, in no team; the existing administrator and everyone else are untouched; one record; the 201 body is the record.
    [Fact]
    public async Task Appoint_ANonMember_AddsAnAdmin_RecordsIt_AndChangesNothingElse()
    {
        var organization = await RescueTestData.OrphanedOrganizationAsync(factory);
        var existing = await MembershipTestData.NewMemberAsync(factory, organization.Id, OrganizationRole.Admin);
        var viewer = await MembershipTestData.NewMemberAsync(factory, organization.Id, OrganizationRole.Viewer);
        var (rescuerId, token) = RescueTestData.NewRescuer();
        await MembershipTestData.ProjectUserAsync(factory, rescuerId, "Системный", "Администратор", "sa@example.org");
        var vera = await RescueTestData.SignedInUserAsync(factory, "Вера", "Иванова", "vera@example.org");
        var before = await RescueTestData.MembershipsAsync(factory, organization.Id);

        var answer = await AppointAsync(organization.Id, Body(vera, "  Борис уволился  "), token);

        Assert.Equal(HttpStatusCode.Created, answer.Status);
        Assert.Null(answer.Location);
        Assert.Equal(["appointedAt", "appointedBy", "appointee", "id", "reason"], answer.Body.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal));
        Assert.Equal("Борис уволился", answer.Body.GetProperty("reason").GetString());
        Assert.Equal((rescuerId, "Системный Администратор", "active"), Ref(answer.Body.GetProperty("appointedBy")));
        Assert.Equal((vera, "Вера Иванова", "active"), Ref(answer.Body.GetProperty("appointee")));

        var after = await RescueTestData.MembershipsAsync(factory, organization.Id);
        var added = Assert.Single(after, row => row.UserId == vera);
        Assert.Equal((OrganizationRole.Admin, rescuerId, null, null), (added.Role, added.AddedBy, added.RoleChangedBy, added.RoleChangedAt));
        Assert.Equal(before, after.Where(row => row.UserId != vera));
        Assert.Contains(after, row => row.UserId == existing.Id && row.Role == OrganizationRole.Admin);
        Assert.Contains(after, row => row.UserId == viewer.Id && row.Role == OrganizationRole.Viewer);
        Assert.Empty(await MembershipTestData.TeamMembershipsOfAsync(factory, vera));

        var record = Assert.Single(await RescueTestData.RecordsAsync(factory, organization.Id));
        Assert.Equal((vera, rescuerId, "Борис уволился"), (record.AppointeeId, record.AppointedBy, record.Reason));
        Assert.Equal(record.Id, answer.Body.GetProperty("id").GetGuid());
        Assert.Equal(record.AppointedAt, answer.Body.GetProperty("appointedAt").GetDateTimeOffset());
        Assert.Equal(added.AddedAt, record.AppointedAt);
    }

    // US-2 "…raises a member": a viewer in a team becomes admin; "added by" and the team are kept, "role changed" names the rescuer.
    [Fact]
    public async Task Appoint_AViewerInATeam_RaisesTheRole_KeepsAddedByAndTheTeam()
    {
        var organization = await RescueTestData.OrphanedOrganizationAsync(factory);
        var team = await OrganizationsTestData.AddUnlinkedTeamAsync(factory, organization.Id, OrganizationsTestData.UniqueName("Команда"));
        var viewer = await RescueTestData.SignedInUserAsync(factory, "Нина", "Читателева");
        await MembershipTestData.AddMemberAsync(factory, organization.Id, viewer, OrganizationRole.Viewer, addedBy: "adder-1");
        await MembershipTestData.AddTeamMemberAsync(factory, organization.Id, team.Id, viewer);
        var (rescuerId, token) = RescueTestData.NewRescuer();
        var stored = await MembershipTestData.FindMembershipAsync(factory, organization.Id, viewer);

        var answer = await AppointAsync(organization.Id, Body(viewer), token);

        Assert.Equal(HttpStatusCode.Created, answer.Status);
        var raised = await MembershipTestData.FindMembershipAsync(factory, organization.Id, viewer);
        Assert.Equal(OrganizationRole.Admin, raised!.Role);
        Assert.Equal(("adder-1", stored!.AddedAt), (raised.AddedBy, raised.AddedAt));
        Assert.Equal((rescuerId, true), (raised.RoleChangedBy, raised.RoleChangedAt is not null));
        Assert.Equal([team.Id], (await MembershipTestData.TeamMembershipsOfAsync(factory, viewer)).Select(membership => membership.TeamId));
        Assert.Single(await RescueTestData.RecordsAsync(factory, organization.Id));
    }

    // US-2: a member raised too (the other role below admin).
    [Fact]
    public async Task Appoint_AMember_RaisesTheRole()
    {
        var organization = await RescueTestData.OrphanedOrganizationAsync(factory);
        var member = await RescueTestData.SignedInUserAsync(factory, "Мария", "Участникова");
        await MembershipTestData.AddMemberAsync(factory, organization.Id, member, OrganizationRole.Member);
        var (_, token) = RescueTestData.NewRescuer();

        var answer = await AppointAsync(organization.Id, Body(member), token);

        Assert.Equal(HttpStatusCode.Created, answer.Status);
        Assert.Equal(OrganizationRole.Admin, (await MembershipTestData.FindMembershipAsync(factory, organization.Id, member))!.Role);
    }

    // BR-RS-4, BR-RS-9 · a deactivated organization is accepted and stays deactivated; the appointee finds it in the list of
    // deactivated organizations on their next request, with no new token, and may restore it (mod-002 BR-M1-35).
    [Fact]
    public async Task Appoint_InADeactivatedOrganization_StaysDeactivated_AndTheAppointeeSeesItAndRestoresIt()
    {
        var organization = await RescueTestData.OrphanedOrganizationAsync(factory, status: OrganizationStatus.Deactivated);
        var user = await RescueTestData.SignedInUserAsync(factory, "Олег", "Восстановов");
        var (_, rescuer) = RescueTestData.NewRescuer();
        var appointee = OrganizationsTokens.SignedIn(user);

        var appointed = await AppointAsync(organization.Id, Body(user), rescuer);
        var listed = await OrganizationsApi.SendAsync(factory, HttpMethod.Get, "/api/organizations/deactivated", token: appointee);
        var restored = await OrganizationsApi.SendAsync(
            factory, HttpMethod.Post, $"{OrganizationsApi.Organization(organization.Id)}/restore", token: appointee);

        Assert.Equal(HttpStatusCode.Created, appointed.Status);
        Assert.Contains(listed.Body.EnumerateArray(), item => item.GetProperty("id").GetGuid() == organization.Id);
        Assert.Equal(HttpStatusCode.OK, restored.Status);
    }

    // BR-RS-9, US-2 "…takes effect on the next request": the appointee's next request passes the scoped check with the same token;
    // the rescuer, who is no member, still gets the organization's 404 on the organization's own page.
    [Fact]
    public async Task Appoint_TakesEffectOnTheNextRequest_AndTheRescuerStillHasNoAccess()
    {
        var organization = await RescueTestData.OrphanedOrganizationAsync(factory);
        var user = await RescueTestData.SignedInUserAsync(factory, "Лев", "Следующий");
        var appointee = OrganizationsTokens.SignedIn(user);
        var (_, rescuer) = RescueTestData.NewRescuer();
        var path = OrganizationsApi.Organization(organization.Id);

        var before = await OrganizationsApi.SendAsync(factory, HttpMethod.Get, path, token: appointee);
        await AppointAsync(organization.Id, Body(user), rescuer);
        var after = await OrganizationsApi.SendAsync(factory, HttpMethod.Get, path, token: appointee);
        var rescuerRead = await OrganizationsApi.SendAsync(factory, HttpMethod.Get, path, token: rescuer);

        Assert.Equal(HttpStatusCode.NotFound, before.Status);
        Assert.Equal(HttpStatusCode.OK, after.Status);
        Assert.Equal((HttpStatusCode.NotFound, OrganizationErrorCodes.OrganizationNotFound), (rescuerRead.Status, rescuerRead.ErrorCode));
    }

    // US-2 "Repeatable": a second appointee leaves the first untouched; two records.
    [Fact]
    public async Task Appoint_Twice_TwoAdministrators_TwoRecords()
    {
        var organization = await RescueTestData.OrphanedOrganizationAsync(factory);
        var first = await RescueTestData.SignedInUserAsync(factory, "Первый", "Назначенный");
        var second = await RescueTestData.SignedInUserAsync(factory, "Второй", "Назначенный");
        var (_, token) = RescueTestData.NewRescuer();

        await AppointAsync(organization.Id, Body(first, "Причина 1"), token);
        var firstRow = (await RescueTestData.MembershipsAsync(factory, organization.Id)).Single(row => row.UserId == first);
        await AppointAsync(organization.Id, Body(second, "Причина 2"), token);

        Assert.Equal(firstRow, (await RescueTestData.MembershipsAsync(factory, organization.Id)).Single(row => row.UserId == first));
        Assert.Equal(OrganizationRole.Admin, (await MembershipTestData.FindMembershipAsync(factory, organization.Id, second))!.Role);
        Assert.Equal(["Причина 1", "Причина 2"], (await RescueTestData.RecordsAsync(factory, organization.Id)).Select(record => record.Reason));
    }

    // US-2 "Already an administrator" / "Never signed in" / "marked inactive before tech-025" / "Nobody appoints themselves" /
    // "unknown organization" / "malformed id": each refusal answers its code, changes no membership and writes no record.
    [Fact]
    public async Task Appoint_AnAlreadyAdministrator_Returns409_NothingChanges()
    {
        var organization = await RescueTestData.OrphanedOrganizationAsync(factory);
        var admin = await RescueTestData.SignedInUserAsync(factory, "Анна", "Админова");
        await MembershipTestData.AddMemberAsync(factory, organization.Id, admin, OrganizationRole.Admin);
        var (_, token) = RescueTestData.NewRescuer();

        var answer = await AppointAsync(organization.Id, Body(admin), token);

        await AssertRefusedAsync(answer, organization.Id, "organizations.rescue.already_admin", "Пользователь уже администратор этой организации");
    }

    [Fact]
    public async Task Appoint_AUserWhoNeverSignedIn_Returns409Unavailable_NothingChanges()
    {
        var organization = await RescueTestData.OrphanedOrganizationAsync(factory);
        var (_, token) = RescueTestData.NewRescuer();

        var answer = await AppointAsync(organization.Id, Body(MembershipTestData.NewUserId()), token);

        await AssertRefusedAsync(answer, organization.Id, "organizations.rescue.user_unavailable", "Этого пользователя нельзя назначить администратором");
    }

    [Fact]
    public async Task Appoint_ARowMarkedInactiveBeforeTech025_Returns409Unavailable_NothingChanges()
    {
        var organization = await RescueTestData.OrphanedOrganizationAsync(factory);
        var inactive = MembershipTestData.NewUserId();
        await MembershipTestData.ProjectUserAsync(factory, inactive, "Старая", "Строка", active: false);
        var (_, token) = RescueTestData.NewRescuer();

        var answer = await AppointAsync(organization.Id, Body(inactive), token);

        await AssertRefusedAsync(answer, organization.Id, "organizations.rescue.user_unavailable", "Этого пользователя нельзя назначить администратором");
    }

    [Fact]
    public async Task Appoint_Oneself_Returns409Self_AndAnotherRescuerMayAppointThem()
    {
        var organization = await RescueTestData.OrphanedOrganizationAsync(factory);
        var (selfId, selfToken) = RescueTestData.NewRescuer();
        await MembershipTestData.ProjectUserAsync(factory, selfId, "Сама", "Себя");
        var (_, otherToken) = RescueTestData.NewRescuer();

        var refused = await AppointAsync(organization.Id, Body(selfId), selfToken);

        await AssertRefusedAsync(refused, organization.Id, "organizations.rescue.self_appointment", "Нельзя назначить администратором себя");

        var allowed = await AppointAsync(organization.Id, Body(selfId), otherToken);

        Assert.Equal(HttpStatusCode.Created, allowed.Status);
    }

    [Theory]
    [InlineData("unknown")]
    [InlineData("malformed")]
    [InlineData("zeros")]
    public async Task Appoint_AnOrganizationThatIsNotThere_Returns404_WritesNothing(string kind)
    {
        var user = await RescueTestData.SignedInUserAsync(factory, "Тихий", "Никуда");
        var (_, token) = RescueTestData.NewRescuer();
        var id = kind switch
        {
            "unknown" => Guid.CreateVersion7().ToString(),
            "malformed" => "not-an-id",
            _ => Guid.Empty.ToString(),
        };

        var answer = await OrganizationsApi.SendAsync(factory, HttpMethod.Post, $"{RescueTestData.Rescue}/{id}/appointments", Body(user), token);

        Assert.Equal((HttpStatusCode.NotFound, OrganizationErrorCodes.OrganizationNotFound), (answer.Status, answer.ErrorCode));
        Assert.Equal("Организация не найдена.", answer.Detail);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Appoint_AMissingOrBlankUser_Returns400OnTheUserField(string? userId)
    {
        var organization = await RescueTestData.OrphanedOrganizationAsync(factory);
        var (_, token) = RescueTestData.NewRescuer();

        var answer = await AppointAsync(organization.Id, new { userId, reason = "Причина" }, token);

        Assert.Equal((HttpStatusCode.BadRequest, OrganizationErrorCodes.ValidationFailed), (answer.Status, answer.ErrorCode));
        Assert.Equal(OrganizationErrorCodes.MembershipUserInvalid, FieldCode(answer, "userId"));
        Assert.Empty(await RescueTestData.RecordsAsync(factory, organization.Id));
    }

    // US-2 "The reason is required": missing, empty, spaces only, over 500 code points, NUL / noncharacter — 400 on the field, nothing
    // written; 500 code points (an emoji is one) pass and the trimmed text is stored.
    [Theory]
    [InlineData(null, "organizations.rescue.reason_required")]
    [InlineData("", "organizations.rescue.reason_required")]
    [InlineData("  \t ", "organizations.rescue.reason_required")]
    [InlineData("TOO_LONG", "organizations.rescue.reason_too_long")]
    [InlineData("TOO_LONG_EMOJI", "organizations.rescue.reason_too_long")]
    [InlineData("a\0b", "organizations.rescue.reason_invalid")]
    [InlineData("a￾b", "organizations.rescue.reason_invalid")]
    public async Task Appoint_ABadReason_Returns400OnTheReasonField_NothingWritten(string? reason, string expected)
    {
        var organization = await RescueTestData.OrphanedOrganizationAsync(factory);
        var user = await RescueTestData.SignedInUserAsync(factory, "Причин", "Нет");
        var (_, token) = RescueTestData.NewRescuer();
        reason = reason switch
        {
            "TOO_LONG" => new string('я', 501),
            "TOO_LONG_EMOJI" => string.Concat(Enumerable.Repeat("😀", 501)),
            _ => reason,
        };

        var answer = await AppointAsync(organization.Id, new { userId = user, reason }, token);

        Assert.Equal((HttpStatusCode.BadRequest, OrganizationErrorCodes.ValidationFailed), (answer.Status, answer.ErrorCode));
        Assert.Equal(expected, FieldCode(answer, "reason"));
        Assert.Null(await MembershipTestData.FindMembershipAsync(factory, organization.Id, user));
        Assert.Empty(await RescueTestData.RecordsAsync(factory, organization.Id));
    }

    [Fact]
    public async Task Appoint_AReasonOfExactly500CodePoints_IsStoredTrimmed()
    {
        var organization = await RescueTestData.OrphanedOrganizationAsync(factory);
        var user = await RescueTestData.SignedInUserAsync(factory, "Длинный", "Повод");
        var (_, token) = RescueTestData.NewRescuer();
        var reason = string.Concat(Enumerable.Repeat("😀", 500));

        var answer = await AppointAsync(organization.Id, new { userId = user, reason = $"  {reason}  " }, token);

        Assert.Equal(HttpStatusCode.Created, answer.Status);
        Assert.Equal(reason, Assert.Single(await RescueTestData.RecordsAsync(factory, organization.Id)).Reason);
    }

    // US-2 "Several refusals at once": the first applicable one answers (BR-RS-6a).
    [Fact]
    public async Task Appoint_SeveralRefusalsAtOnce_AnswerInTheOrderOfTheContract()
    {
        var (rescuerId, token) = RescueTestData.NewRescuer();

        // an unknown user and an organization that does not exist: the organization.
        var unknownEverything = await OrganizationsApi.SendAsync(
            factory, HttpMethod.Post, $"{RescueTestData.Rescue}/{Guid.CreateVersion7()}/appointments", Body(MembershipTestData.NewUserId()), token);
        Assert.Equal(OrganizationErrorCodes.OrganizationNotFound, unknownEverything.ErrorCode);

        // the user never signed in and the reason is empty: the user.
        var organization = await RescueTestData.OrphanedOrganizationAsync(factory);
        var neverSignedIn = await AppointAsync(organization.Id, Body(MembershipTestData.NewUserId(), ""), token);
        Assert.Equal(OrganizationErrorCodes.RescueUserUnavailable, neverSignedIn.ErrorCode);

        // I administer the organization, I name myself, no reason: self.
        await MembershipTestData.ProjectUserAsync(factory, rescuerId, "Сама", "Администратор");
        await MembershipTestData.AddMemberAsync(factory, organization.Id, rescuerId, OrganizationRole.Admin);
        var self = await AppointAsync(organization.Id, Body(rescuerId, ""), token);
        Assert.Equal(OrganizationErrorCodes.RescueSelfAppointment, self.ErrorCode);

        // the user is already an administrator and the reason is empty: already admin.
        var admin = await RescueTestData.SignedInUserAsync(factory, "Уже", "Админ");
        await MembershipTestData.AddMemberAsync(factory, organization.Id, admin, OrganizationRole.Admin);
        var alreadyAdmin = await AppointAsync(organization.Id, Body(admin, ""), token);
        Assert.Equal(OrganizationErrorCodes.RescueAlreadyAdmin, alreadyAdmin.ErrorCode);

        // no refusal of the user, an empty reason: the reason.
        var fresh = await RescueTestData.SignedInUserAsync(factory, "Свежий", "Пользователь");
        var emptyReason = await AppointAsync(organization.Id, Body(fresh, ""), token);
        Assert.Equal(OrganizationErrorCodes.ValidationFailed, emptyReason.ErrorCode);
    }

    // US-2 "Without the right": no token 401; a user without system-admin — an organization administrator included, and a user
    // holding every other instance role — gets 403 «Недостаточно прав» with the rescue code, before the body is read.
    [Fact]
    public async Task Appoint_WithoutTheRight_Returns403_WithTheRescueCode_NothingChanges()
    {
        var organization = await RescueTestData.OrphanedOrganizationAsync(factory);
        var admin = await MembershipTestData.NewMemberAsync(factory, organization.Id, OrganizationRole.Admin);
        var user = await RescueTestData.SignedInUserAsync(factory, "Кто", "Нибудь");
        var tokens = new[] { admin.Token, OrganizationsTokens.AllInstanceRolesButSystemAdmin(), OrganizationsTokens.SignedIn() };

        foreach (var token in tokens)
        {
            var answer = await AppointAsync(organization.Id, Body(user), token);

            Assert.Equal((HttpStatusCode.Forbidden, OrganizationErrorCodes.RescueForbidden), (answer.Status, answer.ErrorCode));
            Assert.Equal("Недостаточно прав", answer.Detail);
        }

        var anonymous = await AppointAsync(organization.Id, Body(user), token: null);

        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.Status);
        Assert.Null(await MembershipTestData.FindMembershipAsync(factory, organization.Id, user));
        Assert.Empty(await RescueTestData.RecordsAsync(factory, organization.Id));
    }

    private async Task AssertRefusedAsync(Answer answer, Guid organizationId, string code, string text)
    {
        Assert.Equal((HttpStatusCode.Conflict, code), (answer.Status, answer.ErrorCode));
        Assert.Equal(text, answer.Detail);
        Assert.Empty(await RescueTestData.RecordsAsync(factory, organizationId));
    }

    private static (string? Id, string? Name, string? State) Ref(JsonElement reference) =>
        (reference.GetProperty("id").GetString(), reference.GetProperty("displayName").GetString(), reference.GetProperty("state").GetString());

    private static string? FieldCode(Answer answer, string field) =>
        answer.Body.GetProperty("errors").GetProperty(field)[0].GetProperty("code").GetString();
}
