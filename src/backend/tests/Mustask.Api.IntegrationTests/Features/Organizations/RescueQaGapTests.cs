using System.Net;
using System.Text;
using Mustask.Domain.Identity;
using Mustask.Domain.Organizations;

namespace Mustask.Api.IntegrationTests.Features.Organizations;

/// <summary>
/// feat-033 backend QA (qa.md, Backend): the cases the stage-4 tests left open. The 403 of every rescue route for every token
/// that does not reach <c>organizations.organization.rescue</c> (each other client role alone, <c>system-admin</c> outside the
/// <c>mustask-api</c> client, <c>system-admin</c> without <c>user</c>), before the address and before the body; the remaining
/// rows of BR-RS-6a; the user id taken as given; Unicode edges of «Причина»; the raise-first race of BR-RS-11; the organization
/// route's refusals; the system administrator's 404 on the organization's content; and the name filter's edges.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class RescueQaGapTests(MustaskApiFactory factory)
{
    private Task<Answer> AppointAsync(Guid organizationId, object body, string? token, CommitGate? gate = null) =>
        OrganizationsApi.SendAsync(factory, HttpMethod.Post, RescueTestData.AppointmentsPath(organizationId), body, token, gate);

    private static string Roles(params string[] roles) =>
        KeycloakTestRealm.CreateToken([], options =>
        {
            options.Subject = OrganizationsTokens.NewSubject();
            options.Roles = roles;
        });

    /// <summary>Every token that must not reach the rescue code, with a name for the failure message.</summary>
    private static IEnumerable<(string Name, string Token)> TokensWithoutTheRight()
    {
        yield return ("no client role", Roles());
        yield return ("user", Roles(InstanceRoles.User));
        yield return ("user + organization-creator", Roles(InstanceRoles.User, InstanceRoles.OrganizationCreator));
        yield return ("user + dictionaries-admin", Roles(InstanceRoles.User, InstanceRoles.DictionariesAdmin));
        yield return ("user + organization-creator + dictionaries-admin", OrganizationsTokens.AllInstanceRolesButSystemAdmin());
        yield return ("system-admin without user", Roles(InstanceRoles.SystemAdmin));
        yield return ("system-admin of a foreign client", KeycloakTestRealm.CreateToken([], options =>
        {
            options.Roles = [InstanceRoles.User];
            options.ForeignClientPermissions = [InstanceRoles.User, InstanceRoles.SystemAdmin];
        }));
        yield return ("system-admin as a realm role", KeycloakTestRealm.CreateToken([], options =>
        {
            options.Roles = [InstanceRoles.User];
            options.RealmAccessRoles = [InstanceRoles.SystemAdmin];
        }));
    }

    // BR-RS-1 · every rescue route, every token without the code: 403 organizations.rescue.forbidden «Недостаточно прав» (or 401
    // for the principal without `user`, which has no session); for the addressed routes, an organization that does not exist is
    // still a 403, not a 404 (BR-RS-6a row 1 before row 2); nothing is written.
    [Fact]
    public async Task EveryRescueRoute_EveryTokenWithoutTheRight_Is403_BeforeTheAddress_AndWritesNothing()
    {
        var organization = await RescueTestData.OrphanedOrganizationAsync(factory);
        var admin = await MembershipTestData.NewMemberAsync(factory, organization.Id, OrganizationRole.Admin);
        var user = await RescueTestData.SignedInUserAsync(factory, "Без", "Права");
        var missing = Guid.CreateVersion7();
        var tokens = TokensWithoutTheRight().Append(("an administrator of the organization", admin.Token)).ToList();
        var problems = new List<string>();

        foreach (var (name, token) in tokens)
        {
            var requests = new (string Route, HttpMethod Method, string Path, object? Body)[]
            {
                ("R1", HttpMethod.Get, RescueTestData.Rescue, null),
                ("R1 page", HttpMethod.Get, $"{RescueTestData.Rescue}?page=0", null),
                ("R2", HttpMethod.Get, $"{RescueTestData.Rescue}/appointee-candidates?text=%D0%B1%D0%B5%D0%B7", null),
                ("R3", HttpMethod.Post, RescueTestData.AppointmentsPath(organization.Id), new { userId = user, reason = "Причина" }),
                ("R3 missing", HttpMethod.Post, RescueTestData.AppointmentsPath(missing), new { userId = user, reason = "Причина" }),
                ("R4", HttpMethod.Get, RescueTestData.AppointmentsPath(organization.Id), null),
                ("R4 missing", HttpMethod.Get, RescueTestData.AppointmentsPath(missing), null),
            };

            foreach (var (route, method, path, body) in requests)
            {
                var answer = await OrganizationsApi.SendAsync(factory, method, path, body, token);
                var ok = name == "system-admin without user"
                    ? answer.Status is HttpStatusCode.Unauthorized
                        || (answer.Status == HttpStatusCode.Forbidden && answer.ErrorCode == OrganizationErrorCodes.RescueForbidden)
                    : answer.Status == HttpStatusCode.Forbidden
                        && answer.ErrorCode == OrganizationErrorCodes.RescueForbidden
                        && answer.Detail == "Недостаточно прав";

                if (!ok)
                {
                    problems.Add($"{name} / {route}: {(int)answer.Status} {answer.ErrorCode} {answer.Detail}");
                }
            }
        }

        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
        Assert.Null(await MembershipTestData.FindMembershipAsync(factory, organization.Id, user));
        Assert.Empty(await RescueTestData.RecordsAsync(factory, organization.Id));
    }

    // api-contract § 0.2 row 2: the 403 comes before the body is read — a malformed body from a caller without the right is a 403.
    [Fact]
    public async Task Appoint_WithoutTheRight_AndAMalformedBody_Is403_Not400()
    {
        var organization = await RescueTestData.OrphanedOrganizationAsync(factory);
        using var request = new HttpRequestMessage(HttpMethod.Post, RescueTestData.AppointmentsPath(organization.Id))
        {
            Content = new StringContent("{not json", Encoding.UTF8, "application/json"),
        };
        request.WithBearer(OrganizationsTokens.AllInstanceRolesButSystemAdmin());

        using var response = await factory.CreateClient().SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains(OrganizationErrorCodes.RescueForbidden, await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    // BR-RS-6a · the rows the stage-4 test does not cover: a pre-tech-025 inactive row that already administers the organization is
    // "not appointable" (row 3 before row 5); a rescuer who is not a member and names themselves with no reason gets "self" (row 4
    // before row 6); the already-administrator refusal leaves every membership exactly as it was.
    [Fact]
    public async Task Appoint_RefusalOrder_InactiveAdmin_SelfWithoutReason_AndAlreadyAdminChangesNothing()
    {
        var organization = await RescueTestData.OrphanedOrganizationAsync(factory);
        var (rescuerId, token) = RescueTestData.NewRescuer();
        await MembershipTestData.ProjectUserAsync(factory, rescuerId, "Сам", "Спасатель");
        var inactiveAdmin = MembershipTestData.NewUserId();
        await MembershipTestData.ProjectUserAsync(factory, inactiveAdmin, "Старый", "Админ", active: false);
        await MembershipTestData.AddMemberAsync(factory, organization.Id, inactiveAdmin, OrganizationRole.Admin);
        var admin = await RescueTestData.SignedInUserAsync(factory, "Действующий", "Админ");
        await MembershipTestData.AddMemberAsync(factory, organization.Id, admin, OrganizationRole.Admin);
        await MembershipTestData.AddMemberAsync(factory, organization.Id, MembershipTestData.NewUserId(), OrganizationRole.Viewer);
        var before = await RescueTestData.MembershipsAsync(factory, organization.Id);

        var inactive = await AppointAsync(organization.Id, new { userId = inactiveAdmin, reason = "" }, token);
        var self = await AppointAsync(organization.Id, new { userId = rescuerId, reason = "   " }, token);
        var already = await AppointAsync(organization.Id, new { userId = admin, reason = "Причина" }, token);

        Assert.Equal((HttpStatusCode.Conflict, OrganizationErrorCodes.RescueUserUnavailable), (inactive.Status, inactive.ErrorCode));
        Assert.Equal((HttpStatusCode.Conflict, OrganizationErrorCodes.RescueSelfAppointment), (self.Status, self.ErrorCode));
        Assert.Equal((HttpStatusCode.Conflict, OrganizationErrorCodes.RescueAlreadyAdmin), (already.Status, already.ErrorCode));
        Assert.Equal(before, await RescueTestData.MembershipsAsync(factory, organization.Id));
        Assert.Empty(await RescueTestData.RecordsAsync(factory, organization.Id));
    }

    // api-contract § 0.3 / § 1.3 row 4a: the user id is taken as given — 256 characters is a field error, an id with spaces around a
    // known one is not trimmed into it (an unknown user).
    [Fact]
    public async Task Appoint_TheUserIdIsTakenAsGiven()
    {
        var organization = await RescueTestData.OrphanedOrganizationAsync(factory);
        var user = await RescueTestData.SignedInUserAsync(factory, "Точный", "Идентификатор");
        var (_, token) = RescueTestData.NewRescuer();

        var tooLong = await AppointAsync(organization.Id, new { userId = new string('u', 256), reason = "Причина" }, token);
        var padded = await AppointAsync(organization.Id, new { userId = $" {user} ", reason = "Причина" }, token);

        Assert.Equal((HttpStatusCode.BadRequest, OrganizationErrorCodes.ValidationFailed), (tooLong.Status, tooLong.ErrorCode));
        Assert.Equal(
            OrganizationErrorCodes.MembershipUserInvalid,
            tooLong.Body.GetProperty("errors").GetProperty("userId")[0].GetProperty("code").GetString());
        Assert.Equal((HttpStatusCode.Conflict, OrganizationErrorCodes.RescueUserUnavailable), (padded.Status, padded.ErrorCode));
        Assert.Null(await MembershipTestData.FindMembershipAsync(factory, organization.Id, user));
    }

    // BR-RS-5 "the dialog and the server count the same way": the panel trims with String.prototype.trim, which removes Unicode
    // spaces and U+FEFF; a reason made only of them is «Укажите причину» on the server too, never an invisible stored reason.
    [Theory]
    [InlineData(0x00A0, 0x3000, 0x2003)]
    [InlineData(0xFEFF)]
    [InlineData(0x20, 0xFEFF, 0x20)]
    public async Task Appoint_AReasonOfUnicodeSpacesOnly_IsRequired(params int[] codePoints)
    {
        // Code points, not literals: the source holds no invisible text.
        var reason = string.Concat(codePoints.Select(code => (char)code));
        var organization = await RescueTestData.OrphanedOrganizationAsync(factory);
        var user = await RescueTestData.SignedInUserAsync(factory, "Пробел", "Юникод");
        var (_, token) = RescueTestData.NewRescuer();

        var answer = await AppointAsync(organization.Id, new { userId = user, reason }, token);

        Assert.Equal((HttpStatusCode.BadRequest, OrganizationErrorCodes.ValidationFailed), (answer.Status, answer.ErrorCode));
        Assert.Equal(
            OrganizationErrorCodes.RescueReasonRequired,
            answer.Body.GetProperty("errors").GetProperty("reason")[0].GetProperty("code").GetString());
        Assert.Empty(await RescueTestData.RecordsAsync(factory, organization.Id));
    }

    // A reason of 502 code points that trims to 500 passes: the limit counts after trimming.
    [Fact]
    public async Task Appoint_500CodePointsAfterTrimming_502Before_Succeeds()
    {
        var organization = await RescueTestData.OrphanedOrganizationAsync(factory);
        var user = await RescueTestData.SignedInUserAsync(factory, "Граница", "Обрезки");
        var (_, token) = RescueTestData.NewRescuer();
        var core = new string('я', 500);

        var answer = await AppointAsync(organization.Id, new { userId = user, reason = $" {core} " }, token);

        Assert.Equal(HttpStatusCode.Created, answer.Status);
        Assert.Equal(core, answer.Body.GetProperty("reason").GetString());
    }

    // An API caller sends a lone surrogate (JSON "\ud800"): whatever the answer, it is not a server error and nothing is written.
    [Fact]
    public async Task Appoint_ALoneSurrogateInTheReason_IsNotAServerError()
    {
        var organization = await RescueTestData.OrphanedOrganizationAsync(factory);
        var user = await RescueTestData.SignedInUserAsync(factory, "Одинокий", "Суррогат");
        var (_, token) = RescueTestData.NewRescuer();
        using var request = new HttpRequestMessage(HttpMethod.Post, RescueTestData.AppointmentsPath(organization.Id))
        {
            Content = new StringContent($"{{\"userId\":\"{user}\",\"reason\":\"a\\ud800b\"}}", Encoding.UTF8, "application/json"),
        };
        request.WithBearer(token);

        using var response = await factory.CreateClient().SendAsync(request);

        Assert.True((int)response.StatusCode < 500, $"{(int)response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty(await RescueTestData.RecordsAsync(factory, organization.Id));
    }

    // BR-RS-11 · an organization administrator raises the member to admin first: the appointment, which waited on the lock, is
    // refused with «Пользователь уже администратор этой организации» and writes no record.
    [Fact]
    public async Task RaiseToAdminThenAppointment_TheAppointmentIsRefusedAlreadyAdmin()
    {
        var organization = await RescueTestData.OrphanedOrganizationAsync(factory);
        var admin = await MembershipTestData.NewNamedMemberAsync(factory, organization.Id, OrganizationRole.Admin, "Анна", "Админова");
        var viewer = await MembershipTestData.NewNamedMemberAsync(factory, organization.Id, OrganizationRole.Viewer, "Пётр", "Читатель");
        var (_, token) = RescueTestData.NewRescuer();
        using var gate = factory.CommitGates.Open();

        var raise = OrganizationsApi.SendAsync(
            factory, HttpMethod.Put, $"{OrganizationsApi.Organization(organization.Id)}/members/{viewer.Id}/role", new { role = "admin" }, admin.Token, gate);
        await Task.WhenAny(gate.Reached, raise);
        Assert.False(raise.IsCompleted, "The raise finished before reaching its commit gate.");
        var appointment = AppointAsync(organization.Id, new { userId = viewer.Id, reason = "Гонка" }, token);
        await PgLocks.WaitUntilBlockedAsync(factory.ConnectionString, appointment);
        Assert.False(appointment.IsCompleted, "The appointment did not wait for the raise's lock.");
        gate.Release();

        Assert.Equal(HttpStatusCode.OK, (await raise).Status);
        var refused = await appointment;
        Assert.Equal((HttpStatusCode.Conflict, OrganizationErrorCodes.RescueAlreadyAdmin), (refused.Status, refused.ErrorCode));
        Assert.Empty(await RescueTestData.RecordsAsync(factory, organization.Id));
        Assert.Equal(admin.Id, (await MembershipTestData.FindMembershipAsync(factory, organization.Id, viewer.Id))!.RoleChangedBy);
    }

    // BR-RS-9 "under the administrator's codes": right after the appointment the appointee, with the same token, reads the
    // organization's appointment record (organizations.membership.write) and sees their own appointment.
    [Fact]
    public async Task Appoint_TheAppointeeHoldsTheAdministratorsCodesOnTheNextRequest()
    {
        var organization = await RescueTestData.OrphanedOrganizationAsync(factory);
        var user = await RescueTestData.SignedInUserAsync(factory, "Новая", "Администратор");
        var appointee = OrganizationsTokens.SignedIn(user);
        var (_, rescuer) = RescueTestData.NewRescuer();

        await AppointAsync(organization.Id, new { userId = user, reason = "Назначена" }, rescuer);
        var record = await OrganizationsApi.SendAsync(factory, HttpMethod.Get, RescueTestData.OrganizationAppointmentsPath(organization.Id), token: appointee);

        Assert.Equal(HttpStatusCode.OK, record.Status);
        Assert.Equal("Назначена", Assert.Single(record.Body.EnumerateArray()).GetProperty("reason").GetString());
    }

    // US-1 "The organization's content stays hidden", US-2 "I still see no content": the rescuer, before and after appointing someone,
    // gets the organization's 404 on its content routes; the organization route of the record is a 404 for them too.
    [Fact]
    public async Task TheRescuer_StillGets404_OnTheOrganizationsContent_AfterAnAppointment()
    {
        var organization = await RescueTestData.OrphanedOrganizationAsync(factory);
        var user = await RescueTestData.SignedInUserAsync(factory, "Назначенный", "Кто-то");
        var (_, rescuer) = RescueTestData.NewRescuer();
        await AppointAsync(organization.Id, new { userId = user, reason = "Причина" }, rescuer);
        var root = OrganizationsApi.Organization(organization.Id);
        var problems = new List<string>();

        foreach (var path in new[] { root, $"{root}/access", $"{root}/members", $"{root}/projects", $"{root}/teams", $"{root}/overview", $"{root}/appointments", $"{root}/member-candidates?text=%D0%BD%D0%B0" })
        {
            var answer = await OrganizationsApi.SendAsync(factory, HttpMethod.Get, path, token: rescuer);

            if (answer.Status != HttpStatusCode.NotFound || answer.ErrorCode != OrganizationErrorCodes.OrganizationNotFound)
            {
                problems.Add($"{path}: {(int)answer.Status} {answer.ErrorCode}");
            }
        }

        Assert.True(problems.Count == 0, string.Join(Environment.NewLine, problems));
    }

    // api-contract § 2.1: a member's and a reader's 403 carry organizations.access.forbidden «Недостаточно прав»; an administrator of
    // a deactivated organization and an administrator of another organization get the 404.
    [Fact]
    public async Task OrganizationRoute_RefusalCodes_DeactivatedAndAnotherOrganization()
    {
        var organization = await RescueTestData.OrphanedOrganizationAsync(factory);
        var member = await MembershipTestData.NewMemberAsync(factory, organization.Id, OrganizationRole.Member);
        var other = await RescueTestData.OrphanedOrganizationAsync(factory);
        var otherAdmin = await MembershipTestData.NewMemberAsync(factory, other.Id, OrganizationRole.Admin);
        var deactivated = await RescueTestData.OrphanedOrganizationAsync(factory, status: OrganizationStatus.Deactivated);
        var deactivatedAdmin = await MembershipTestData.NewMemberAsync(factory, deactivated.Id, OrganizationRole.Admin);

        var forbidden = await OrganizationsApi.SendAsync(factory, HttpMethod.Get, RescueTestData.OrganizationAppointmentsPath(organization.Id), token: member.Token);
        var foreign = await OrganizationsApi.SendAsync(factory, HttpMethod.Get, RescueTestData.OrganizationAppointmentsPath(organization.Id), token: otherAdmin.Token);
        var closed = await OrganizationsApi.SendAsync(factory, HttpMethod.Get, RescueTestData.OrganizationAppointmentsPath(deactivated.Id), token: deactivatedAdmin.Token);

        Assert.Equal((HttpStatusCode.Forbidden, OrganizationErrorCodes.AccessForbidden, "Недостаточно прав"), (forbidden.Status, forbidden.ErrorCode, forbidden.Detail));
        Assert.Equal((HttpStatusCode.NotFound, OrganizationErrorCodes.OrganizationNotFound), (foreign.Status, foreign.ErrorCode));
        Assert.Equal((HttpStatusCode.NotFound, OrganizationErrorCodes.OrganizationNotFound), (closed.Status, closed.ErrorCode));
    }

    // US-3 / api-contract § 1.4: the rescue read of a deactivated organization returns its records.
    [Fact]
    public async Task RescueRead_ADeactivatedOrganizationsRecord_IsReturned()
    {
        var organization = await RescueTestData.OrphanedOrganizationAsync(factory, status: OrganizationStatus.Deactivated);
        var user = await RescueTestData.SignedInUserAsync(factory, "Деактив", "Назначен");
        var (_, rescuer) = RescueTestData.NewRescuer();
        await AppointAsync(organization.Id, new { userId = user, reason = "Закрыта" }, rescuer);

        var answer = await OrganizationsApi.SendAsync(factory, HttpMethod.Get, RescueTestData.AppointmentsPath(organization.Id), token: rescuer);

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Equal("Закрыта", Assert.Single(answer.Body.EnumerateArray()).GetProperty("reason").GetString());
    }

    // api-contract § 1.1: a filter of spaces is no filter; a text over 100 characters is never an error (count 0); a backslash is
    // literal; an empty page parameter is the contract's 400.
    [Fact]
    public async Task List_BlankFilter_LongFilter_Backslash_AndAnEmptyPage()
    {
        var tag = Guid.CreateVersion7().ToString("N");
        var (_, token) = RescueTestData.NewRescuer();
        await RescueTestData.OrphanedOrganizationAsync(factory, $"{tag} a\\b");

        var unfiltered = await OrganizationsApi.SendAsync(factory, HttpMethod.Get, RescueTestData.Rescue, token: token);
        var blank = await OrganizationsApi.SendAsync(factory, HttpMethod.Get, $"{RescueTestData.Rescue}?name=%20%20%20", token: token);
        var longText = await OrganizationsApi.SendAsync(factory, HttpMethod.Get, $"{RescueTestData.Rescue}?name={new string('я', 150)}", token: token);
        var backslash = await OrganizationsApi.SendAsync(factory, HttpMethod.Get, $"{RescueTestData.Rescue}?name={Uri.EscapeDataString($"{tag} a\\b")}", token: token);
        var notBackslash = await OrganizationsApi.SendAsync(factory, HttpMethod.Get, $"{RescueTestData.Rescue}?name={Uri.EscapeDataString($"{tag} a\\\\b")}", token: token);
        var emptyPage = await OrganizationsApi.SendAsync(factory, HttpMethod.Get, $"{RescueTestData.Rescue}?page=", token: token);

        Assert.Equal(HttpStatusCode.OK, blank.Status);
        Assert.True(blank.Body.GetProperty("count").GetInt32() >= unfiltered.Body.GetProperty("count").GetInt32());
        Assert.Equal(
            Math.Min(25, blank.Body.GetProperty("count").GetInt32()),
            blank.Body.GetProperty("data").GetArrayLength());
        Assert.Equal((HttpStatusCode.OK, 0), (longText.Status, longText.Body.GetProperty("count").GetInt32()));
        Assert.Equal(1, backslash.Body.GetProperty("count").GetInt32());
        Assert.Equal(0, notBackslash.Body.GetProperty("count").GetInt32());
        Assert.Equal((HttpStatusCode.BadRequest, OrganizationErrorCodes.ValidationFailed), (emptyPage.Status, emptyPage.ErrorCode));
    }
}
