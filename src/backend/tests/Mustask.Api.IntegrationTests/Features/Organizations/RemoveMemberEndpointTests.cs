using System.Net;
using System.Text.Json;
using Mustask.Api.IntegrationTests.Features.WorkItems;
using Mustask.Domain.Identity;

namespace Mustask.Api.IntegrationTests.Features.Organizations;

/// <summary>
/// <c>DELETE /api/organizations/{organizationId}/members/{userId}</c> (mod-016 api-contract § 1.6; US-1 "An administrator removes
/// a member", "An administrator removes themselves while another remains", "Removing a membership that is already gone", doc-006
/// US-3 scenarios 8–13): the membership and — in the same statement — the user's team memberships end; work items keep their
/// assignee; the caller may remove themselves, and their next request answers 404; BR-M1-4 counts the other administrators who are
/// known and active.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class RemoveMemberEndpointTests(MustaskApiFactory factory)
{
    private const string LastAdmin = "organizations.membership.last_admin";

    private Task<Answer> RemoveAsync(Guid organizationId, string userId, string? token, CommitGate? gate = null) =>
        OrganizationsApi.SendAsync(
            factory,
            HttpMethod.Delete,
            $"{OrganizationsApi.Organization(organizationId)}/members/{Uri.EscapeDataString(userId)}",
            token: token,
            gate: gate);

    private Task<MembershipTestData.TestMember> KnownAdminAsync(Guid organizationId, string name) =>
        MembershipTestData.NewNamedMemberAsync(factory, organizationId, OrganizationRole.Admin, name, "Администратор");

    private async Task<bool> IsMemberAsync(Guid organizationId, string userId) =>
        await MembershipTestData.FindMembershipAsync(factory, organizationId, userId) is not null;

    private async Task<List<string>> ListedAsync(Guid organizationId, string token)
    {
        var answer = await OrganizationsApi.SendAsync(factory, HttpMethod.Get, $"{OrganizationsApi.Organization(organizationId)}/members", token: token);

        return answer.Body.EnumerateArray().Select(item => item.GetProperty("user").GetProperty("id").GetString()!).ToList();
    }

    // US-1 · "An administrator removes a member": 204; the member is gone from the list; the other members are untouched.
    [Fact]
    public async Task Remove_AMember_Returns204_TheyLeaveTheList_OthersStay()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var admin = await KnownAdminAsync(organization.Id, "Анна");
        var leaving = await MembershipTestData.NewMemberAsync(factory, organization.Id, OrganizationRole.Member);
        var staying = await MembershipTestData.NewMemberAsync(factory, organization.Id, OrganizationRole.Viewer);

        var answer = await RemoveAsync(organization.Id, leaving.Id, admin.Token);

        Assert.Equal(HttpStatusCode.NoContent, answer.Status);
        var listed = await ListedAsync(organization.Id, admin.Token);
        Assert.DoesNotContain(leaving.Id, listed);
        Assert.Contains(staying.Id, listed);
        Assert.Contains(admin.Id, listed);
    }

    // doc-006 US-3 scenario 8 / BR-M1-36 · the removed member's next request: «Организация не найдена».
    [Fact]
    public async Task Remove_TheRemovedMembersNextRequest_Answers404OrganizationNotFound()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var admin = await KnownAdminAsync(organization.Id, "Анна");
        var leaving = await MembershipTestData.NewMemberAsync(factory, organization.Id, OrganizationRole.Member);
        var accessPath = $"{OrganizationsApi.Organization(organization.Id)}/access";

        var before = await OrganizationsApi.SendAsync(factory, HttpMethod.Get, accessPath, token: leaving.Token);
        await RemoveAsync(organization.Id, leaving.Id, admin.Token);
        var after = await OrganizationsApi.SendAsync(factory, HttpMethod.Get, accessPath, token: leaving.Token);

        Assert.Equal(HttpStatusCode.OK, before.Status);
        Assert.Equal((HttpStatusCode.NotFound, "organizations.organization.not_found"), (after.Status, after.ErrorCode));
    }

    // BR-M1-36 · "removing an organization membership ends the user's team memberships" — in every team of the organization, in the
    // same transaction — and no team membership of another user, or of the same user elsewhere, is touched.
    [Fact]
    public async Task Remove_EndsTheUsersTeamMemberships_InEveryTeam_AndOnlyTheirs()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory, founded: false);
        var other = await OrganizationsTestData.CreateStructureAsync(factory);
        var admin = await KnownAdminAsync(structure.OrganizationId, "Анна");
        var leaving = await MembershipTestData.NewMemberAsync(factory, structure.OrganizationId, OrganizationRole.Member);
        var staying = await MembershipTestData.NewMemberAsync(factory, structure.OrganizationId, OrganizationRole.Member);
        await MembershipTestData.AddMemberAsync(factory, other.OrganizationId, leaving.Id);
        foreach (var teamId in new[] { structure.FirstTeamId, structure.Platform.Team.Id })
        {
            await MembershipTestData.AddTeamMemberAsync(factory, structure.OrganizationId, teamId, leaving.Id);
            await MembershipTestData.AddTeamMemberAsync(factory, structure.OrganizationId, teamId, staying.Id);
        }

        await MembershipTestData.AddTeamMemberAsync(factory, other.OrganizationId, other.FirstTeamId, leaving.Id);

        var answer = await RemoveAsync(structure.OrganizationId, leaving.Id, admin.Token);

        Assert.Equal(HttpStatusCode.NoContent, answer.Status);
        Assert.Equal([(other.FirstTeamId, other.OrganizationId)], await MembershipTestData.TeamMembershipsOfAsync(factory, leaving.Id));
        Assert.Equal([staying.Id], await MembershipTestData.TeamMemberIdsAsync(factory, structure.FirstTeamId));
        Assert.Equal([staying.Id], await MembershipTestData.TeamMemberIdsAsync(factory, structure.Platform.Team.Id));
        Assert.True(await IsMemberAsync(other.OrganizationId, leaving.Id));
    }

    // doc-006 US-3 scenario 8 / BR-M1-26 · "work items assigned to the removed member keep their assignee": the item still names
    // them, and the read says they are no longer a member.
    [Fact]
    public async Task Remove_AnAssignee_TheirWorkItemsKeepThem_FlaggedNoLongerAMember()
    {
        var world = await WorkItemsTestData.CreateWorldAsync(factory);
        var admin = await KnownAdminAsync(world.OrganizationId, "Анна");
        var bug = await WorkItemsTestData.CreateItemAsync(factory, world, "BUG", "Падение");
        var anna = await WorkItemsTestData.AddMemberAsync(factory, world, "Вера", "Исполнитель");
        await WorkItemsTestData.SaveAsync(factory, world, bug, new { assignee = new { userId = anna } });

        var answer = await RemoveAsync(world.OrganizationId, anna, admin.Token);

        Assert.Equal(HttpStatusCode.NoContent, answer.Status);
        var item = await WorkItemsTestData.ReadAsync(factory, world, bug);
        Assert.Equal(anna, item.GetProperty("assignee").GetProperty("id").GetString());
        Assert.False(item.GetProperty("assignee").GetProperty("isMember").GetBoolean());
        Assert.Equal(anna, (await WorkItemsTestData.StoredAsync(factory, world.ProjectId))[0].AssigneeId);
    }

    // doc-006 US-3 scenario 12 · "An administrator removes themselves while another remains": 204, and the next request of the
    // caller — the same token — answers 404 «Организация не найдена».
    [Fact]
    public async Task Remove_TheCallerThemself_WhileAnotherAdministratorRemains_Returns204_AndTheNextRequestIs404()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var admin = await KnownAdminAsync(organization.Id, "Анна");
        var other = await KnownAdminAsync(organization.Id, "Борис");

        var answer = await RemoveAsync(organization.Id, admin.Id, admin.Token);
        var next = await OrganizationsApi.SendAsync(factory, HttpMethod.Get, $"{OrganizationsApi.Organization(organization.Id)}/members", token: admin.Token);

        Assert.Equal(HttpStatusCode.NoContent, answer.Status);
        Assert.Equal((HttpStatusCode.NotFound, "organizations.organization.not_found"), (next.Status, next.ErrorCode));
        Assert.True(await IsMemberAsync(organization.Id, other.Id));
    }

    // BR-M1-4 · "the last administrator cannot be removed": the sole administrator, removing themselves, gets 409 and stays.
    [Fact]
    public async Task Remove_TheOnlyAdministratorThemself_Returns409LastAdmin_TheyStay()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var admin = await KnownAdminAsync(organization.Id, "Анна");
        await MembershipTestData.NewMemberAsync(factory, organization.Id, OrganizationRole.Member);

        var answer = await RemoveAsync(organization.Id, admin.Id, admin.Token);

        Assert.Equal((HttpStatusCode.Conflict, LastAdmin), (answer.Status, answer.ErrorCode));
        Assert.True(await IsMemberAsync(organization.Id, admin.Id));
    }

    // US-1 · "An unknown administrator does not count as the one who remains" at the removal too: the known administrator A and
    // B, whom the projection has never received. A removing themselves is refused, and so is B removing A; A removing B is fine.
    [Fact]
    public async Task Remove_AnUnknownAdministratorDoesNotCount_AsTheOneWhoRemains()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var known = await KnownAdminAsync(organization.Id, "Анна");
        var unknown = await MembershipTestData.NewMemberAsync(factory, organization.Id, OrganizationRole.Admin);

        var selfRemoval = await RemoveAsync(organization.Id, known.Id, known.Token);
        var refused = await RemoveAsync(organization.Id, known.Id, unknown.Token);
        var removed = await RemoveAsync(organization.Id, unknown.Id, known.Token);

        Assert.Equal((HttpStatusCode.Conflict, LastAdmin), (selfRemoval.Status, selfRemoval.ErrorCode));
        Assert.Equal((HttpStatusCode.Conflict, LastAdmin), (refused.Status, refused.ErrorCode));
        Assert.Equal(HttpStatusCode.NoContent, removed.Status);
        Assert.False(await IsMemberAsync(organization.Id, unknown.Id));
    }

    // api-contract § 1.6 · "An inactive administrator is removed only while a known active one remains": with a known active
    // administrator the removal passes; where the caller is the only other administrator and is unknown, it is refused.
    [Fact]
    public async Task Remove_AnInactiveAdministrator_OnlyWhileAKnownActiveOneRemains()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var known = await KnownAdminAsync(organization.Id, "Анна");
        var inactive = await MembershipTestData.NewMemberAsync(factory, organization.Id, OrganizationRole.Admin);
        await MembershipTestData.ProjectUserAsync(factory, inactive.Id, "Олег", "Отключённый", active: false);
        var unknownOnly = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var unknownCaller = await MembershipTestData.NewMemberAsync(factory, unknownOnly.Id, OrganizationRole.Admin);
        var inactiveThere = await MembershipTestData.NewMemberAsync(factory, unknownOnly.Id, OrganizationRole.Admin);
        await MembershipTestData.ProjectUserAsync(factory, inactiveThere.Id, "Пётр", "Отключённый", active: false);

        var passes = await RemoveAsync(organization.Id, inactive.Id, known.Token);
        var refused = await RemoveAsync(unknownOnly.Id, inactiveThere.Id, unknownCaller.Token);

        Assert.Equal(HttpStatusCode.NoContent, passes.Status);
        Assert.Equal((HttpStatusCode.Conflict, LastAdmin), (refused.Status, refused.ErrorCode));
        Assert.True(await IsMemberAsync(unknownOnly.Id, inactiveThere.Id));
    }

    // doc-006 US-3 scenario 10 · "membership already gone": 404 «Участник не найден.» — removed twice, a user of no membership
    // here, an over-long id.
    [Fact]
    public async Task Remove_AMembershipAlreadyGone_Returns404MembershipNotFound()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var admin = await KnownAdminAsync(organization.Id, "Анна");
        var member = await MembershipTestData.NewMemberAsync(factory, organization.Id, OrganizationRole.Member);

        var first = await RemoveAsync(organization.Id, member.Id, admin.Token);
        var second = await RemoveAsync(organization.Id, member.Id, admin.Token);
        var stranger = await RemoveAsync(organization.Id, MembershipTestData.NewUserId(), admin.Token);
        var tooLong = await RemoveAsync(organization.Id, new string('u', 256), admin.Token);

        Assert.Equal(HttpStatusCode.NoContent, first.Status);
        foreach (var answer in new[] { second, stranger, tooLong })
        {
            Assert.Equal((HttpStatusCode.NotFound, "organizations.membership.not_found"), (answer.Status, answer.ErrorCode));
            Assert.Equal("Участник не найден.", answer.Detail);
        }
    }

    // NFR Concurrency / doc-006 US-3 scenario 11 (BR-M1-4) · "two administrators remove each other at the same moment": A's
    // removal of B is held at its commit with the organization locked; B's removal of A waits on the lock; A commits and B is
    // gone — B's request, read again under the lock, is answered as a non-member of the organization (404). Exactly one
    // administrator is left.
    [Fact]
    public async Task Remove_TwoAdministratorsRemoveEachOther_ExactlyOneIsSaved_OneAdministratorLeft()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var a = await KnownAdminAsync(organization.Id, "Анна");
        var b = await KnownAdminAsync(organization.Id, "Борис");
        using var gate = factory.CommitGates.Open();

        var first = RemoveAsync(organization.Id, b.Id, a.Token, gate);
        await Task.WhenAny(gate.Reached, first);
        Assert.False(first.IsCompleted, "The first removal finished before reaching its commit gate.");

        var second = RemoveAsync(organization.Id, a.Id, b.Token);
        await PgLocks.WaitUntilBlockedAsync(factory.ConnectionString, second);
        Assert.False(second.IsCompleted, "The second removal did not wait for the first one's lock on the organization.");

        gate.Release();
        var (saved, refused) = (await first, await second);

        Assert.Equal(HttpStatusCode.NoContent, saved.Status);
        Assert.Equal((HttpStatusCode.NotFound, "organizations.organization.not_found"), (refused.Status, refused.ErrorCode));
        Assert.True(await IsMemberAsync(organization.Id, a.Id));
        Assert.False(await IsMemberAsync(organization.Id, b.Id));
    }

    // BE-SEC-1 / plan-backend § 3.6 row 6 · a caller demoted at the same moment removes no one: A's demotion of B is held at its
    // commit with the organization locked; B's removal — admitted by the scoped check while B was still an administrator — waits
    // for the lock, and once A commits it reads its own role again and is refused: 403. The member C is still a member, and the
    // administrator D too (A remains, so BR-M1-4 would have allowed that removal; only the caller's role refuses it).
    [Theory]
    [InlineData("member")]
    [InlineData("admin")]
    public async Task Remove_ACallerDemotedAtTheSameMoment_IsForbidden_NobodyIsRemoved(string kind)
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var a = await KnownAdminAsync(organization.Id, "Анна");
        var b = await KnownAdminAsync(organization.Id, "Борис");
        var d = await KnownAdminAsync(organization.Id, "Дина");
        var c = await MembershipTestData.NewMemberAsync(factory, organization.Id, OrganizationRole.Member);
        using var gate = factory.CommitGates.Open();

        var demotion = OrganizationsApi.SendAsync(
            factory,
            HttpMethod.Put,
            $"{OrganizationsApi.Organization(organization.Id)}/members/{b.Id}/role",
            new { role = "member" },
            a.Token,
            gate);
        await Task.WhenAny(gate.Reached, demotion);
        Assert.False(demotion.IsCompleted, "The demotion finished before reaching its commit gate.");

        var removal = RemoveAsync(organization.Id, kind == "member" ? c.Id : d.Id, b.Token);
        await PgLocks.WaitUntilBlockedAsync(factory.ConnectionString, removal);
        Assert.False(removal.IsCompleted, "The removal did not wait for the demotion's lock on the organization.");

        gate.Release();
        var (demoted, refused) = (await demotion, await removal);

        Assert.Equal(HttpStatusCode.OK, demoted.Status);
        Assert.Equal((HttpStatusCode.Forbidden, "organizations.access.forbidden"), (refused.Status, refused.ErrorCode));
        Assert.True(await IsMemberAsync(organization.Id, c.Id));
        Assert.True(await IsMemberAsync(organization.Id, d.Id));
        Assert.Equal(OrganizationRole.Member, (await MembershipTestData.FindMembershipAsync(factory, organization.Id, b.Id))!.Role);
    }

    // A removal against a downgrade of the removal's own caller (BR-M1-4): B downgrades A, held at its commit; A's removal of B
    // waits for the organization lock. A is a member when the lock is granted — still in the organization, but no longer an
    // administrator. BR-M1-4 answers before the caller's role is read again (api-contract § 1.6): B would be the last
    // administrator to go, and 409 «last_admin» answers, not 403. Exactly one change stands and an administrator remains.
    [Fact]
    public async Task Remove_AgainstADowngradeOfTheCaller_OneChangeStands_AnAdministratorRemains()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var a = await KnownAdminAsync(organization.Id, "Анна");
        var b = await KnownAdminAsync(organization.Id, "Борис");
        using var gate = factory.CommitGates.Open();

        var downgrade = OrganizationsApi.SendAsync(
            factory,
            HttpMethod.Put,
            $"{OrganizationsApi.Organization(organization.Id)}/members/{a.Id}/role",
            new { role = "member" },
            b.Token,
            gate);
        await Task.WhenAny(gate.Reached, downgrade);
        Assert.False(downgrade.IsCompleted, "The downgrade finished before reaching its commit gate.");

        var removal = RemoveAsync(organization.Id, b.Id, a.Token);
        await PgLocks.WaitUntilBlockedAsync(factory.ConnectionString, removal);
        Assert.False(removal.IsCompleted, "The removal did not wait for the downgrade's lock on the organization.");

        gate.Release();
        var (downgraded, removed) = (await downgrade, await removal);

        Assert.Equal(HttpStatusCode.OK, downgraded.Status);
        Assert.Equal((HttpStatusCode.Conflict, LastAdmin), (removed.Status, removed.ErrorCode));
        Assert.True(await IsMemberAsync(organization.Id, b.Id));
        Assert.Equal(OrganizationRole.Member, (await MembershipTestData.FindMembershipAsync(factory, organization.Id, a.Id))!.Role);
    }

    // US-3 · "add while the organization membership is removed" (BR-M1-10), the removal first: the removal is held at its commit
    // with the member row locked and the team memberships cascaded away (uncommitted); the add of the same user to a team waits on
    // that row at its insert, and once the removal commits the add is refused — 409 «только активного участника организации».
    // The user is in no team of the organization.
    [Fact]
    public async Task Remove_AgainstATeamAdd_RemovalFirst_TheAddIsRefused_TheUserIsInNoTeam()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var admin = await KnownAdminAsync(structure.OrganizationId, "Анна");
        var user = await MembershipTestData.NewNamedMemberAsync(factory, structure.OrganizationId, OrganizationRole.Member, "Вера", "Участница");
        using var gate = factory.CommitGates.Open();

        var removal = RemoveAsync(structure.OrganizationId, user.Id, admin.Token, gate);
        await Task.WhenAny(gate.Reached, removal);
        Assert.False(removal.IsCompleted, "The removal finished before reaching its commit gate.");

        var add = OrganizationsApi.SendAsync(
            factory,
            HttpMethod.Post,
            $"{OrganizationsApi.Teams(structure.OrganizationId, structure.ProjectId)}/{structure.Platform.Team.Id}/members",
            new { userId = user.Id },
            admin.Token);
        await PgLocks.WaitUntilBlockedAsync(factory.ConnectionString, add);
        Assert.False(add.IsCompleted, "The add did not wait for the removal's lock on the membership.");

        gate.Release();
        var (removed, added) = (await removal, await add);

        Assert.Equal(HttpStatusCode.NoContent, removed.Status);
        Assert.Equal((HttpStatusCode.Conflict, "organizations.team.member_not_in_organization"), (added.Status, added.ErrorCode));
        Assert.Empty(await MembershipTestData.TeamMembershipsOfAsync(factory, user.Id));
        Assert.False(await IsMemberAsync(structure.OrganizationId, user.Id));
    }

    // US-3 · "add while the organization membership is removed", the add first: the add is held at its commit (its team
    // membership inserted, the parent membership row key-share locked); the removal waits for the row, and then its cascade removes
    // the newly committed team membership. Either way the user is in no team of the organization afterwards.
    [Fact]
    public async Task Remove_AgainstATeamAdd_AddFirst_BothSucceed_TheCascadeEndsTheNewTeamMembership()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var admin = await KnownAdminAsync(structure.OrganizationId, "Анна");
        var user = await MembershipTestData.NewNamedMemberAsync(factory, structure.OrganizationId, OrganizationRole.Member, "Вера", "Участница");
        using var gate = factory.CommitGates.Open();

        var add = OrganizationsApi.SendAsync(
            factory,
            HttpMethod.Post,
            $"{OrganizationsApi.Teams(structure.OrganizationId, structure.ProjectId)}/{structure.Platform.Team.Id}/members",
            new { userId = user.Id },
            admin.Token,
            gate);
        await Task.WhenAny(gate.Reached, add);
        Assert.False(add.IsCompleted, "The add finished before reaching its commit gate.");

        var removal = RemoveAsync(structure.OrganizationId, user.Id, admin.Token);
        await PgLocks.WaitUntilBlockedAsync(factory.ConnectionString, removal);
        Assert.False(removal.IsCompleted, "The removal did not wait for the add's lock on the membership.");

        gate.Release();
        var (added, removed) = (await add, await removal);

        Assert.Equal(HttpStatusCode.Created, added.Status);
        Assert.Equal(HttpStatusCode.NoContent, removed.Status);
        Assert.Empty(await MembershipTestData.TeamMembershipsOfAsync(factory, user.Id));
        Assert.False(await IsMemberAsync(structure.OrganizationId, user.Id));
        Assert.Equal(JsonValueKind.Object, added.Body.ValueKind);
    }

    // US-2 · "A member whose role lacks the code is refused": a member and a viewer get 403; nothing is removed.
    [Theory]
    [InlineData(OrganizationRole.Member)]
    [InlineData(OrganizationRole.Viewer)]
    public async Task Remove_MemberAndViewer_Get403_NothingIsRemoved(OrganizationRole role)
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var caller = await MembershipTestData.NewMemberAsync(factory, organization.Id, role);
        var target = await MembershipTestData.NewMemberAsync(factory, organization.Id, OrganizationRole.Viewer);

        var other = await RemoveAsync(organization.Id, target.Id, caller.Token);
        var self = await RemoveAsync(organization.Id, caller.Id, caller.Token);

        foreach (var answer in new[] { other, self })
        {
            Assert.Equal((HttpStatusCode.Forbidden, "organizations.access.forbidden"), (answer.Status, answer.ErrorCode));
        }

        Assert.True(await IsMemberAsync(organization.Id, target.Id));
        Assert.True(await IsMemberAsync(organization.Id, caller.Id));
    }

    // US-2 · "A non-member gets «Организация не найдена»", and 401 without a token.
    [Fact]
    public async Task Remove_NonMember_Is404_NoToken_Is401()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var target = await MembershipTestData.NewMemberAsync(factory, organization.Id, OrganizationRole.Member);

        var nonMember = await RemoveAsync(organization.Id, target.Id, MembershipTestData.Outsider());
        var anonymous = await RemoveAsync(organization.Id, target.Id, token: null);

        Assert.Equal((HttpStatusCode.NotFound, "organizations.organization.not_found"), (nonMember.Status, nonMember.ErrorCode));
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.Status);
        Assert.True(await IsMemberAsync(organization.Id, target.Id));
    }
}
