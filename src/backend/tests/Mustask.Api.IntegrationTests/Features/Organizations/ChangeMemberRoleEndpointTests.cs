using System.Net;
using System.Text.Json;
using Mustask.Domain.Identity;

namespace Mustask.Api.IntegrationTests.Features.Organizations;

/// <summary>
/// <c>PUT /api/organizations/{organizationId}/members/{userId}/role</c> (mod-016 api-contract § 1.5; US-1 "An administrator
/// changes a member's role from the row menu", "A role outside the three is refused", "An unknown administrator does not count as
/// the one who remains", doc-006 US-3 scenarios 9–11): the organization row is locked first, the caller is read again under the
/// lock, and BR-M1-4 counts the other administrators who are known and active — so of two administrators who downgrade each other
/// exactly one change is saved.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class ChangeMemberRoleEndpointTests(MustaskApiFactory factory)
{
    private const string LastAdmin = "organizations.membership.last_admin";

    private static string Tag() => Guid.NewGuid().ToString("N")[..8];

    private Task<Answer> ChangeAsync(Guid organizationId, string userId, object? body, string? token, CommitGate? gate = null) =>
        OrganizationsApi.SendAsync(
            factory,
            HttpMethod.Put,
            $"{OrganizationsApi.Organization(organizationId)}/members/{Uri.EscapeDataString(userId)}/role",
            body,
            token,
            gate);

    private Task<MembershipTestData.TestMember> KnownAdminAsync(Guid organizationId, string name) =>
        MembershipTestData.NewNamedMemberAsync(factory, organizationId, OrganizationRole.Admin, name, "Администратор");

    private async Task<OrganizationRole?> RoleOfAsync(Guid organizationId, string userId) =>
        (await MembershipTestData.FindMembershipAsync(factory, organizationId, userId))?.Role;

    private static string? Name(JsonElement reference) =>
        reference.GetProperty("displayName").ValueKind == JsonValueKind.Null ? null : reference.GetProperty("displayName").GetString();

    // US-1 · "An administrator changes a member's role from the row menu": 200 with the Member, «Роль изменена» naming the caller
    // and the time; «Добавлен» is untouched; the stored row says the same.
    [Fact]
    public async Task Change_ToAnotherRole_Returns200_NamingWhoChangedItAndWhen()
    {
        var tag = Tag();
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var admin = await KnownAdminAsync(organization.Id, $"Анна{tag}");
        var member = await MembershipTestData.NewMemberAsync(factory, organization.Id, OrganizationRole.Member);
        var before = DateTimeOffset.UtcNow;

        var answer = await ChangeAsync(organization.Id, member.Id, new { role = "viewer" }, admin.Token);

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Equal("viewer", answer.Body.GetProperty("role").GetString());
        Assert.Equal(member.Id, answer.Body.GetProperty("user").GetProperty("id").GetString());
        Assert.Equal((admin.Id, $"Анна{tag} Администратор"), (
            answer.Body.GetProperty("roleChangedBy").GetProperty("id").GetString(), Name(answer.Body.GetProperty("roleChangedBy"))));
        Assert.InRange(answer.Body.GetProperty("roleChangedAt").GetDateTimeOffset(), before.AddMinutes(-1), DateTimeOffset.UtcNow.AddMinutes(1));
        Assert.Equal(OrganizationsTestData.Creator, answer.Body.GetProperty("addedBy").GetProperty("id").GetString());
        Assert.Equal(OrganizationsTestData.Now, answer.Body.GetProperty("addedAt").GetDateTimeOffset());

        var stored = await MembershipTestData.FindMembershipAsync(factory, organization.Id, member.Id);
        Assert.Equal((OrganizationRole.Viewer, admin.Id), (stored!.Role, stored.RoleChangedBy));
    }

    // api-contract § 1.5 · "Setting the role the member already has answers 200 and changes nothing — not even the two «changed»
    // fields."
    [Fact]
    public async Task Change_ToTheSameRole_Returns200_AndChangesNothing_NotEvenTheChangedFields()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var admin = await KnownAdminAsync(organization.Id, "Анна");
        var member = await MembershipTestData.NewMemberAsync(factory, organization.Id, OrganizationRole.Member);

        var answer = await ChangeAsync(organization.Id, member.Id, new { role = "member" }, admin.Token);

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Equal("member", answer.Body.GetProperty("role").GetString());
        Assert.Equal(JsonValueKind.Null, answer.Body.GetProperty("roleChangedBy").ValueKind);
        Assert.Equal(JsonValueKind.Null, answer.Body.GetProperty("roleChangedAt").ValueKind);
        var stored = await MembershipTestData.FindMembershipAsync(factory, organization.Id, member.Id);
        Assert.Null(stored!.RoleChangedBy);
        Assert.Null(stored.RoleChangedAt);
    }

    // US-1 · "A role outside the three is refused": nothing changes; the reason is on the field under the module's validation code.
    [Theory]
    [InlineData("owner")]
    [InlineData("Viewer")]
    [InlineData("")]
    [InlineData(null)]
    public async Task Change_ARoleOutsideTheThree_Returns400RoleInvalid_RoleUnchanged(string? role)
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var admin = await KnownAdminAsync(organization.Id, "Анна");
        var member = await MembershipTestData.NewMemberAsync(factory, organization.Id, OrganizationRole.Member);

        var answer = await ChangeAsync(organization.Id, member.Id, new { role }, admin.Token);

        Assert.Equal(HttpStatusCode.BadRequest, answer.Status);
        Assert.Equal("organizations.validation_failed", answer.ErrorCode);
        Assert.Equal("organizations.membership.role_invalid", answer.Body.GetProperty("errors").GetProperty("role")[0].GetProperty("code").GetString());
        Assert.Equal(OrganizationRole.Member, await RoleOfAsync(organization.Id, member.Id));
    }

    // doc-006 US-3 scenario 10 · "membership already gone": 404 «Участник не найден.» for a user with no membership here — one of
    // another organization, an unknown id and an over-long one alike — and it is answered before the role is judged.
    [Fact]
    public async Task Change_AUserWithNoMembershipHere_Returns404MembershipNotFound_BeforeTheRoleIsJudged()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var other = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var admin = await KnownAdminAsync(organization.Id, "Анна");
        var elsewhere = await MembershipTestData.NewMemberAsync(factory, other.Id, OrganizationRole.Member);

        foreach (var userId in new[] { elsewhere.Id, MembershipTestData.NewUserId(), new string('u', 256) })
        {
            var valid = await ChangeAsync(organization.Id, userId, new { role = "viewer" }, admin.Token);
            var invalid = await ChangeAsync(organization.Id, userId, new { role = "boss" }, admin.Token);

            foreach (var answer in new[] { valid, invalid })
            {
                Assert.Equal(HttpStatusCode.NotFound, answer.Status);
                Assert.Equal("organizations.membership.not_found", answer.ErrorCode);
                Assert.Equal("Участник не найден.", answer.Detail);
            }
        }

        Assert.Equal(OrganizationRole.Member, await RoleOfAsync(other.Id, elsewhere.Id));
    }

    // doc-006 US-3 scenario 9 / BR-M1-4 · "the last administrator cannot step down": the sole administrator, changing themselves,
    // gets 409 «В организации должен остаться хотя бы один администратор» and stays an administrator.
    [Fact]
    public async Task Change_TheOnlyAdministratorStepsDown_Returns409LastAdmin_RoleUnchanged()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var admin = await KnownAdminAsync(organization.Id, "Анна");

        var answer = await ChangeAsync(organization.Id, admin.Id, new { role = "viewer" }, admin.Token);

        Assert.Equal(HttpStatusCode.Conflict, answer.Status);
        Assert.Equal(LastAdmin, answer.ErrorCode);
        Assert.Equal("В организации должен остаться хотя бы один администратор", answer.Detail);
        Assert.Equal(OrganizationRole.Admin, await RoleOfAsync(organization.Id, admin.Id));
    }

    // US-1 · "The caller may change their own role": with another known active administrator, an administrator steps down — and the
    // next write of theirs is refused (403), the role being read on every request.
    [Fact]
    public async Task Change_AnAdministratorStepsDownWhileAnotherRemains_Returns200_AndTheNextWriteIsForbidden()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var admin = await KnownAdminAsync(organization.Id, "Анна");
        var other = await KnownAdminAsync(organization.Id, "Борис");

        var answer = await ChangeAsync(organization.Id, admin.Id, new { role = "member" }, admin.Token);
        var next = await ChangeAsync(organization.Id, other.Id, new { role = "viewer" }, admin.Token);

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Equal(admin.Id, answer.Body.GetProperty("roleChangedBy").GetProperty("id").GetString());
        Assert.Equal((HttpStatusCode.Forbidden, "organizations.access.forbidden"), (next.Status, next.ErrorCode));
        Assert.Equal(OrganizationRole.Admin, await RoleOfAsync(organization.Id, other.Id));
    }

    // US-1 · "An unknown administrator does not count as the one who remains" (BR-M1-32, mod-014 BR-UP-17): the known administrator A
    // and B, whom the projection has never received; A stepping down is refused, and so is B demoting A — only known active
    // administrators count.
    [Fact]
    public async Task Change_AnUnknownAdministratorDoesNotCount_AsTheOneWhoRemains()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var known = await KnownAdminAsync(organization.Id, "Анна");
        var unknown = await MembershipTestData.NewMemberAsync(factory, organization.Id, OrganizationRole.Admin);

        var stepDown = await ChangeAsync(organization.Id, known.Id, new { role = "member" }, known.Token);
        var demotedByUnknown = await ChangeAsync(organization.Id, known.Id, new { role = "viewer" }, unknown.Token);

        Assert.Equal((HttpStatusCode.Conflict, LastAdmin), (stepDown.Status, stepDown.ErrorCode));
        Assert.Equal((HttpStatusCode.Conflict, LastAdmin), (demotedByUnknown.Status, demotedByUnknown.ErrorCode));
        Assert.Equal(OrganizationRole.Admin, await RoleOfAsync(organization.Id, known.Id));
    }

    // US-1 · the same for an administrator whose account is disabled (inactive): they are not the one who remains. And a
    // creator the projection does not know administers as usual — and may demote a plain member, which threatens nothing.
    [Fact]
    public async Task Change_AnInactiveAdministratorDoesNotCount_ButDemotingAMemberNeedsNoOne()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var known = await KnownAdminAsync(organization.Id, "Анна");
        var inactive = await MembershipTestData.NewMemberAsync(factory, organization.Id, OrganizationRole.Admin);
        await MembershipTestData.ProjectUserAsync(factory, inactive.Id, "Олег", "Отключённый", active: false);
        var member = await MembershipTestData.NewMemberAsync(factory, organization.Id, OrganizationRole.Member);

        var stepDown = await ChangeAsync(organization.Id, known.Id, new { role = "member" }, known.Token);
        var promote = await ChangeAsync(organization.Id, member.Id, new { role = "viewer" }, known.Token);

        Assert.Equal((HttpStatusCode.Conflict, LastAdmin), (stepDown.Status, stepDown.ErrorCode));
        Assert.Equal(HttpStatusCode.OK, promote.Status);
        Assert.Equal(OrganizationRole.Viewer, await RoleOfAsync(organization.Id, member.Id));
    }

    // US-6 · "A creator the projection does not know yet administers as usual": the sole, unknown administrator changes a member's
    // role, is refused when stepping down, and is permitted once the projection receives them and a second known one exists.
    [Fact]
    public async Task Change_ACreatorTheProjectionDoesNotKnow_AdministersAsUsual_UntilTheyStepDown()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var creator = await MembershipTestData.NewMemberAsync(factory, organization.Id, OrganizationRole.Admin);
        var member = await MembershipTestData.NewMemberAsync(factory, organization.Id, OrganizationRole.Member);

        var changeOther = await ChangeAsync(organization.Id, member.Id, new { role = "viewer" }, creator.Token);
        var stepDown = await ChangeAsync(organization.Id, creator.Id, new { role = "member" }, creator.Token);

        Assert.Equal(HttpStatusCode.OK, changeOther.Status);
        Assert.Equal((HttpStatusCode.Conflict, LastAdmin), (stepDown.Status, stepDown.ErrorCode));

        await MembershipTestData.ProjectUserAsync(factory, creator.Id, "Создатель", "Организации");
        var stillRefused = await ChangeAsync(organization.Id, creator.Id, new { role = "member" }, creator.Token);

        // Now known and active — but the only administrator: still the last one.
        Assert.Equal((HttpStatusCode.Conflict, LastAdmin), (stillRefused.Status, stillRefused.ErrorCode));

        var second = await KnownAdminAsync(organization.Id, "Вторая");
        var permitted = await ChangeAsync(organization.Id, creator.Id, new { role = "member" }, creator.Token);

        Assert.Equal(HttpStatusCode.OK, permitted.Status);
        Assert.Equal(OrganizationRole.Admin, await RoleOfAsync(organization.Id, second.Id));
    }

    // doc-006 US-4 scenario 7 / BR-M1-6 · a promotion applies on the next request, with the same token: a viewer, promoted, writes.
    [Fact]
    public async Task Change_APromotion_AppliesOnTheNextRequest_WithTheSameToken()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var admin = await KnownAdminAsync(organization.Id, "Анна");
        var viewer = await MembershipTestData.NewMemberAsync(factory, organization.Id, OrganizationRole.Viewer);
        var target = await MembershipTestData.NewMemberAsync(factory, organization.Id, OrganizationRole.Member);

        var refused = await ChangeAsync(organization.Id, target.Id, new { role = "viewer" }, viewer.Token);
        await ChangeAsync(organization.Id, viewer.Id, new { role = "admin" }, admin.Token);
        var allowed = await ChangeAsync(organization.Id, target.Id, new { role = "viewer" }, viewer.Token);

        Assert.Equal(HttpStatusCode.Forbidden, refused.Status);
        Assert.Equal(HttpStatusCode.OK, allowed.Status);
    }

    // NFR Concurrency / doc-006 US-3 scenario 11 (BR-M1-4) · "two administrators downgrade each other at the same moment": A's
    // request is held at its commit with the organization locked; B's waits on the lock; then exactly one change is saved — A's —
    // and B, now a member, is answered by the invariant: 409 last_admin. One administrator is left.
    [Fact]
    public async Task Change_TwoAdministratorsDowngradeEachOther_ExactlyOneIsSaved_TheOtherLastAdmin()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var a = await KnownAdminAsync(organization.Id, "Анна");
        var b = await KnownAdminAsync(organization.Id, "Борис");
        using var gate = factory.CommitGates.Open();

        var first = ChangeAsync(organization.Id, b.Id, new { role = "member" }, a.Token, gate);
        await Task.WhenAny(gate.Reached, first);
        Assert.False(first.IsCompleted, "The first change finished before reaching its commit gate.");

        var second = ChangeAsync(organization.Id, a.Id, new { role = "member" }, b.Token);
        await PgLocks.WaitUntilBlockedAsync(factory.ConnectionString, second);
        Assert.False(second.IsCompleted, "The second change did not wait for the first one's lock on the organization.");

        gate.Release();
        var (saved, refused) = (await first, await second);

        Assert.Equal(HttpStatusCode.OK, saved.Status);
        Assert.Equal((HttpStatusCode.Conflict, LastAdmin), (refused.Status, refused.ErrorCode));
        Assert.Equal(OrganizationRole.Admin, await RoleOfAsync(organization.Id, a.Id));
        Assert.Equal(OrganizationRole.Member, await RoleOfAsync(organization.Id, b.Id));
    }

    // BE-SEC-1 / plan-backend § 3.6 row 5 · a caller demoted at the same moment cannot undo it nor act as an administrator: A's
    // demotion of B is held at its commit with the organization locked; B's request — admitted by the scoped check while B was still
    // an administrator — waits for the lock, and once A commits it reads its own role again and is refused: 403. B stays a member
    // (they cannot promote themselves back), C stays a member (no promotion), D stays an administrator (A remains, so BR-M1-4 would
    // have allowed the demotion; only the caller's role refuses it).
    [Theory]
    [InlineData("self")]
    [InlineData("promote")]
    [InlineData("demote")]
    public async Task Change_ACallerDemotedAtTheSameMoment_IsForbidden_NothingTheyAskedForHappens(string kind)
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var a = await KnownAdminAsync(organization.Id, "Анна");
        var b = await KnownAdminAsync(organization.Id, "Борис");
        var d = await KnownAdminAsync(organization.Id, "Дина");
        var c = await MembershipTestData.NewMemberAsync(factory, organization.Id, OrganizationRole.Member);
        using var gate = factory.CommitGates.Open();

        var demotion = ChangeAsync(organization.Id, b.Id, new { role = "member" }, a.Token, gate);
        await Task.WhenAny(gate.Reached, demotion);
        Assert.False(demotion.IsCompleted, "The demotion finished before reaching its commit gate.");

        var (targetId, role) = kind switch
        {
            "self" => (b.Id, "admin"),
            "promote" => (c.Id, "admin"),
            _ => (d.Id, "member"),
        };
        var attempt = ChangeAsync(organization.Id, targetId, new { role }, b.Token);
        await PgLocks.WaitUntilBlockedAsync(factory.ConnectionString, attempt);
        Assert.False(attempt.IsCompleted, "The caller's request did not wait for the demotion's lock on the organization.");

        gate.Release();
        var (demoted, refused) = (await demotion, await attempt);

        Assert.Equal(HttpStatusCode.OK, demoted.Status);
        Assert.Equal((HttpStatusCode.Forbidden, "organizations.access.forbidden"), (refused.Status, refused.ErrorCode));
        Assert.Equal(OrganizationRole.Member, await RoleOfAsync(organization.Id, b.Id));
        Assert.Equal(OrganizationRole.Member, await RoleOfAsync(organization.Id, c.Id));
        Assert.Equal(OrganizationRole.Admin, await RoleOfAsync(organization.Id, d.Id));
    }

    // US-1 · "membership already gone" at the same moment: a removal held at its commit, a role change of the same member waits for
    // the organization lock and then finds the member gone — 404 «Участник не найден.»
    [Fact]
    public async Task Change_AMemberRemovedAtTheSameMoment_Returns404MembershipNotFound()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var a = await KnownAdminAsync(organization.Id, "Анна");
        var b = await KnownAdminAsync(organization.Id, "Борис");
        var member = await MembershipTestData.NewMemberAsync(factory, organization.Id, OrganizationRole.Member);
        using var gate = factory.CommitGates.Open();

        var removal = OrganizationsApi.SendAsync(
            factory, HttpMethod.Delete, $"{OrganizationsApi.Organization(organization.Id)}/members/{member.Id}", token: a.Token, gate: gate);
        await Task.WhenAny(gate.Reached, removal);
        Assert.False(removal.IsCompleted, "The removal finished before reaching its commit gate.");

        var change = ChangeAsync(organization.Id, member.Id, new { role = "viewer" }, b.Token);
        await PgLocks.WaitUntilBlockedAsync(factory.ConnectionString, change);
        Assert.False(change.IsCompleted, "The change did not wait for the removal's lock on the organization.");

        gate.Release();
        var (removed, changed) = (await removal, await change);

        Assert.Equal(HttpStatusCode.NoContent, removed.Status);
        Assert.Equal((HttpStatusCode.NotFound, "organizations.membership.not_found"), (changed.Status, changed.ErrorCode));
        Assert.Null(await MembershipTestData.FindMembershipAsync(factory, organization.Id, member.Id));
    }

    // US-2 · "A member whose role lacks the code is refused": a member and a viewer get 403; nothing changes.
    [Theory]
    [InlineData(OrganizationRole.Member)]
    [InlineData(OrganizationRole.Viewer)]
    public async Task Change_MemberAndViewer_Get403_RoleUnchanged(OrganizationRole role)
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var caller = await MembershipTestData.NewMemberAsync(factory, organization.Id, role);
        var target = await MembershipTestData.NewMemberAsync(factory, organization.Id, OrganizationRole.Viewer);

        var answer = await ChangeAsync(organization.Id, target.Id, new { role = "admin" }, caller.Token);
        var self = await ChangeAsync(organization.Id, caller.Id, new { role = "admin" }, caller.Token);

        foreach (var refused in new[] { answer, self })
        {
            Assert.Equal((HttpStatusCode.Forbidden, "organizations.access.forbidden"), (refused.Status, refused.ErrorCode));
        }

        Assert.Equal(OrganizationRole.Viewer, await RoleOfAsync(organization.Id, target.Id));
        Assert.Equal(role, await RoleOfAsync(organization.Id, caller.Id));
    }

    // US-2 · "A non-member gets «Организация не найдена»", and 401 without a token.
    [Fact]
    public async Task Change_NonMember_Is404_NoToken_Is401()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var target = await MembershipTestData.NewMemberAsync(factory, organization.Id, OrganizationRole.Member);

        var nonMember = await ChangeAsync(organization.Id, target.Id, new { role = "admin" }, MembershipTestData.Outsider());
        var anonymous = await ChangeAsync(organization.Id, target.Id, new { role = "admin" }, token: null);

        Assert.Equal((HttpStatusCode.NotFound, "organizations.organization.not_found"), (nonMember.Status, nonMember.ErrorCode));
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.Status);
        Assert.Equal(OrganizationRole.Member, await RoleOfAsync(organization.Id, target.Id));
    }
}
