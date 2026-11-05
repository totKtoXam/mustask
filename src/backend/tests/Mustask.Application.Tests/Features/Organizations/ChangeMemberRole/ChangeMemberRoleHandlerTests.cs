using Mustask.Application.Abstractions.Organizations;
using Mustask.Application.Features.Organizations.ChangeMemberRole;
using Mustask.Application.Tests.TestDoubles;
using Mustask.Application.Tests.TestDoubles.Organizations;
using Mustask.Domain.Identity;
using Mustask.Domain.Organizations;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Tests.Features.Organizations.ChangeMemberRole;

/// <summary>
/// <c>ChangeMemberRole</c> on fakes (mod-016 plan-backend § 3.6 row 5): the order of answers (api-contract § 0.1, § 1.5), the
/// last-administrator rule over the projection (BR-M1-4, BR-M1-32) and the lock order (§ 3.8). The 404/403 before the handler are
/// the scoped check's (Api tests).
/// </summary>
public sealed class ChangeMemberRoleHandlerTests
{
    private sealed class Fixture
    {
        public Fixture()
        {
            World.AddMember("user-oleg", "Олег Иванов");
            Handler = new ChangeMemberRoleHandler(
                World.Organizations, World.Memberships, World.Members, World.Clock, World.User, new FakeUnitOfWork());
        }

        public MembershipWorld World { get; } = new();

        public ChangeMemberRoleHandler Handler { get; }

        public ChangeMemberRoleCommand Command(string? userId = "user-oleg", string? role = "viewer", string? organizationId = null) =>
            new(organizationId ?? World.OrganizationId, userId, role);

        public Task<ChangeMemberRoleResult> HandleAsync(ChangeMemberRoleCommand command) => Handler.Handle(command, CancellationToken.None);
    }

    // US-1 · "An administrator changes a member's role from the row menu": the row shows the new role and «Роль изменена» with the
    // administrator's name and the time.
    [Fact]
    public async Task Handle_DifferentRole_ChangesItAndRecordsWhoAndWhen()
    {
        var f = new Fixture();

        var result = await f.HandleAsync(f.Command(role: "viewer"));

        Assert.Equal(OrganizationRole.Viewer, f.World.RoleOf("user-oleg"));
        Assert.Equal("viewer", result.Member.Role);
        Assert.Equal("Анна Смирнова", result.Member.RoleChangedBy!.DisplayName);
        Assert.Equal(f.World.Clock.UtcNow, result.Member.RoleChangedAt);

        // «Добавлен» keeps the original adding, untouched by the change.
        Assert.Equal(InMemoryOrganizations.Now, result.Member.AddedAt);
    }

    // api-contract § 1.5: setting the role the member already has answers 200 and changes nothing — not even the two «changed»
    // fields, and nothing is written.
    [Fact]
    public async Task Handle_TheRoleTheMemberAlreadyHas_ChangesNothing_NotEvenTheChangedFields_NoSave()
    {
        var f = new Fixture();

        var result = await f.HandleAsync(f.Command(role: "member"));

        Assert.Equal("member", result.Member.Role);
        Assert.Null(result.Member.RoleChangedBy);
        Assert.Null(result.Member.RoleChangedAt);
        Assert.Equal(0, f.World.Store.SaveCount);
    }

    // plan-backend § 3.8, P-4: the organization row first, then the member's row; one save.
    [Fact]
    public async Task Handle_LocksTheOrganizationFirst_ThenTheMember_ThenSavesOnce()
    {
        var f = new Fixture();

        await f.HandleAsync(f.Command());

        Assert.Equal([$"lock organization {f.World.Organization.Id}", "lock member user-oleg", "save"], f.World.Store.Calls);
    }

    // US-1 · "A role outside the three is refused": the role is unchanged.
    [Theory]
    [InlineData("owner")]
    [InlineData("Viewer")]
    [InlineData("")]
    [InlineData(null)]
    public async Task Handle_RoleOutsideTheThree_IsValidationFailedWithRoleInvalid_RoleUnchanged(string? role)
    {
        var f = new Fixture();

        var refusal = await Assert.ThrowsAsync<ValidationException>(() => f.HandleAsync(f.Command(role: role)));

        Assert.Equal(OrganizationErrorCodes.ValidationFailed, refusal.Code);
        Assert.Equal(OrganizationErrorCodes.MembershipRoleInvalid, Assert.Single(refusal.Errors["role"]).Code);
        Assert.Equal(OrganizationRole.Member, f.World.RoleOf("user-oleg"));
        Assert.Empty(f.World.Store.Calls);
    }

    // US-1 · "Removing a membership that is already gone": a user who has no membership here — also a blank or over-long id.
    [Theory]
    [InlineData("nobody")]
    [InlineData(" ")]
    [InlineData("256")]
    public async Task Handle_NoSuchMembership_IsMembershipNotFound(string userId)
    {
        var f = new Fixture();
        var submitted = userId == "256" ? new string('u', 256) : userId;

        var refusal = await Assert.ThrowsAsync<NotFoundException>(() => f.HandleAsync(f.Command(userId: submitted)));

        Assert.Equal(OrganizationErrorCodes.MembershipNotFound, refusal.Code);
        Assert.Empty(f.World.Store.Calls);
    }

    // api-contract § 0.1: the member in the path (4) answers before the value in the body (5).
    [Fact]
    public async Task Handle_NoSuchMembershipAndABadRole_AnswersTheMembershipFirst()
    {
        var f = new Fixture();

        var refusal = await Assert.ThrowsAsync<NotFoundException>(() => f.HandleAsync(f.Command(userId: "nobody", role: "boss")));

        Assert.Equal(OrganizationErrorCodes.MembershipNotFound, refusal.Code);
    }

    // US-1 · "Removing a membership that is already gone": another administrator removed it between the pre-check and the lock.
    [Fact]
    public async Task Handle_MembershipRemovedByAnotherAdministratorMeanwhile_IsMembershipNotFound()
    {
        var f = new Fixture();
        f.World.Memberships.BeforeNextLockMember = () => f.World.Store.DeleteMembership(f.World.Organization.Id, "user-oleg");

        var refusal = await Assert.ThrowsAsync<NotFoundException>(() => f.HandleAsync(f.Command()));

        Assert.Equal(OrganizationErrorCodes.MembershipNotFound, refusal.Code);
        Assert.Equal(0, f.World.Store.SaveCount);
    }

    // US-1 · "An unknown administrator does not count as the one who remains" (BR-M1-4, BR-M1-32): admins A (active) and B whose id
    // the projection does not know — A stepping down is refused.
    [Fact]
    public async Task Handle_SoleKnownAdministratorStepsDown_WhileAnUnknownAdministratorRemains_IsLastAdmin()
    {
        var f = new Fixture();
        f.World.AddUnknownMember("admin-unknown", OrganizationRole.Admin);

        var refusal = await Assert.ThrowsAsync<ConflictException>(
            () => f.HandleAsync(f.Command(userId: MembershipWorld.AdminId, role: "member")));

        Assert.Equal(OrganizationErrorCodes.MembershipLastAdmin, refusal.Code);
        Assert.Equal(OrganizationRole.Admin, f.World.RoleOf(MembershipWorld.AdminId));
        Assert.Equal(0, f.World.Store.SaveCount);
    }

    // BR-M1-32: an administrator whose account is disabled does not count either.
    [Fact]
    public async Task Handle_TheOnlyOtherAdministratorIsInactive_IsLastAdmin()
    {
        var f = new Fixture();
        f.World.AddMember("admin-disabled", "Отключённый админ", OrganizationRole.Admin, isActive: false);

        var refusal = await Assert.ThrowsAsync<ConflictException>(
            () => f.HandleAsync(f.Command(userId: MembershipWorld.AdminId, role: "viewer")));

        Assert.Equal(OrganizationErrorCodes.MembershipLastAdmin, refusal.Code);
    }

    // The sole administrator of an organization cannot step down at all.
    [Fact]
    public async Task Handle_TheSoleAdministratorStepsDown_IsLastAdmin()
    {
        var f = new Fixture();

        var refusal = await Assert.ThrowsAsync<ConflictException>(
            () => f.HandleAsync(f.Command(userId: MembershipWorld.AdminId, role: "member")));

        Assert.Equal(OrganizationErrorCodes.MembershipLastAdmin, refusal.Code);
    }

    // US-1 doc-006 scenario 10 · "stepping down while another admin remains": allowed, and an administrator may change their own role.
    [Fact]
    public async Task Handle_AnAdministratorStepsDownWhileAnotherKnownActiveOneRemains_IsAllowed()
    {
        var f = new Fixture();
        f.World.AddMember("admin-boris", "Борис Админов", OrganizationRole.Admin);

        var result = await f.HandleAsync(f.Command(userId: MembershipWorld.AdminId, role: "member"));

        Assert.Equal("member", result.Member.Role);
        Assert.Equal(OrganizationRole.Member, f.World.RoleOf(MembershipWorld.AdminId));
        Assert.Equal(MembershipWorld.AdminId, f.World.Store.FindMembership(f.World.Organization.Id, MembershipWorld.AdminId)!.RoleChangedBy);
    }

    // BR-M1-4 concerns an administrator stepping down only: promoting a member never asks for the count.
    [Fact]
    public async Task Handle_PromotingAMember_NeverCountsTheAdministrators()
    {
        var f = new Fixture();

        await f.HandleAsync(f.Command(role: "admin"));

        // The one lookup is the answer's own: the people named in «Добавлен» and «Роль изменена».
        Assert.Equal(OrganizationRole.Admin, f.World.RoleOf("user-oleg"));
        Assert.Single(f.World.Directory.LookupSizes);
    }

    // BR-M1-4: an administrator stepping down asks for the count of the others — a second lookup, over the other administrators.
    [Fact]
    public async Task Handle_AnAdministratorSteppingDown_CountsTheOthersThroughTheProjection()
    {
        var f = new Fixture();
        f.World.AddMember("admin-boris", "Борис Админов", OrganizationRole.Admin);

        await f.HandleAsync(f.Command(userId: MembershipWorld.AdminId, role: "member"));

        Assert.Equal(2, f.World.Directory.LookupSizes.Count);
        Assert.Equal(1, f.World.Directory.LookupSizes[0]);
    }

    // NFR Concurrency / US-1 doc-006 scenario 11 · two administrators downgrade each other: the first change is saved, and the
    // second finds one administrator fewer and is refused with last_admin; exactly one change is saved and one admin is left.
    [Fact]
    public async Task Handle_TwoAdministratorsDowngradeEachOther_ExactlyOneChangeIsSaved_TheOtherIsLastAdmin()
    {
        var f = new Fixture();
        f.World.AddMember("admin-boris", "Борис Админов", OrganizationRole.Admin);

        // Anna downgrades Boris first; then Boris's request, admitted before that, reaches the lock.
        await f.HandleAsync(f.Command(userId: "admin-boris", role: "member"));
        f.World.User.SubjectId = "admin-boris";
        var refusal = await Assert.ThrowsAsync<ConflictException>(
            () => f.HandleAsync(f.Command(userId: MembershipWorld.AdminId, role: "member")));

        Assert.Equal(OrganizationErrorCodes.MembershipLastAdmin, refusal.Code);
        Assert.Equal(OrganizationRole.Admin, f.World.RoleOf(MembershipWorld.AdminId));
        Assert.Equal(OrganizationRole.Member, f.World.RoleOf("admin-boris"));
    }

    // BE-SEC-1 / plan-backend § 3.6 row 5 · the caller was demoted by an administrator who won the lock: the scoped check admitted
    // the request at its start, but the role read again under the lock no longer holds organizations.membership.write — 403, and
    // the demoted caller cannot undo the demotion by promoting themselves.
    [Fact]
    public async Task Handle_ACallerDemotedBeforeTheLock_PromotingThemselves_IsForbidden_RoleUnchanged()
    {
        var f = new Fixture();
        f.World.AddMember("admin-boris", "Борис Админов", OrganizationRole.Admin);
        await f.HandleAsync(f.Command(userId: "admin-boris", role: "member"));
        f.World.Store.Calls.Clear();
        f.World.User.SubjectId = "admin-boris";

        var refusal = await Assert.ThrowsAsync<ForbiddenException>(() => f.HandleAsync(f.Command(userId: "admin-boris", role: "admin")));

        Assert.Equal(OrganizationErrorCodes.AccessForbidden, refusal.Code);
        Assert.Equal(OrganizationRole.Member, f.World.RoleOf("admin-boris"));
        Assert.Equal(0, f.World.Store.SaveCount);
    }

    // BE-SEC-1 · the same caller promoting a third user, or demoting a third administrator while another remains (BR-M1-4 passes),
    // is refused by the role read again; nothing is written.
    [Theory]
    [InlineData("user-oleg", "admin")]
    [InlineData("admin-dina", "member")]
    public async Task Handle_ACallerDemotedBeforeTheLock_ChangingSomeoneElse_IsForbidden_NothingChanges(string target, string role)
    {
        var f = new Fixture();
        f.World.AddMember("admin-boris", "Борис Админов", OrganizationRole.Admin);
        f.World.AddMember("admin-dina", "Дина Админова", OrganizationRole.Admin);
        await f.HandleAsync(f.Command(userId: "admin-boris", role: "member"));
        f.World.Store.Calls.Clear();
        f.World.User.SubjectId = "admin-boris";
        var before = f.World.RoleOf(target);

        var refusal = await Assert.ThrowsAsync<ForbiddenException>(() => f.HandleAsync(f.Command(userId: target, role: role)));

        Assert.Equal(OrganizationErrorCodes.AccessForbidden, refusal.Code);
        Assert.Equal(before, f.World.RoleOf(target));
        Assert.Equal(0, f.World.Store.SaveCount);
    }

    // BE-SEC-1 · a role that holds no write code (a viewer) is refused even for the request that "changes nothing": the 200 of the
    // same-role answer belongs to a caller who still holds the code.
    [Fact]
    public async Task Handle_ACallerWhoNoLongerHoldsTheCode_SettingTheRoleTheTargetHas_IsForbidden()
    {
        var f = new Fixture();
        f.World.User.SubjectId = "user-oleg";

        var refusal = await Assert.ThrowsAsync<ForbiddenException>(() => f.HandleAsync(f.Command(userId: "user-oleg", role: "member")));

        Assert.Equal(OrganizationErrorCodes.AccessForbidden, refusal.Code);
    }

    // BE-SEC-1 / api-contract § 1.6 · the order: BR-M1-4 answers before the role read again, so the demoted loser of two
    // administrators who demote each other hears last_admin, not 403 — and the same caller acting on a member hears 403.
    [Fact]
    public async Task Handle_TheDemotedLoserOfAMutualDowngrade_HearsLastAdmin_ButForAnyoneElseForbidden()
    {
        var f = new Fixture();
        f.World.AddMember("admin-boris", "Борис Админов", OrganizationRole.Admin);
        await f.HandleAsync(f.Command(userId: "admin-boris", role: "member"));
        f.World.User.SubjectId = "admin-boris";

        var mutual = await Assert.ThrowsAsync<ConflictException>(
            () => f.HandleAsync(f.Command(userId: MembershipWorld.AdminId, role: "member")));
        var other = await Assert.ThrowsAsync<ForbiddenException>(() => f.HandleAsync(f.Command(userId: "user-oleg", role: "admin")));

        Assert.Equal(OrganizationErrorCodes.MembershipLastAdmin, mutual.Code);
        Assert.Equal(OrganizationErrorCodes.AccessForbidden, other.Code);
    }

    // api-contract § 1.6: "if the refused caller's own membership is already gone, as a non-member: 404 organization".
    [Fact]
    public async Task Handle_TheCallersOwnMembershipIsGone_IsOrganizationNotFound_ChangesNothing()
    {
        var f = new Fixture();
        f.World.User.SubjectId = "admin-removed";

        var refusal = await Assert.ThrowsAsync<NotFoundException>(() => f.HandleAsync(f.Command()));

        Assert.Equal(OrganizationErrorCodes.OrganizationNotFound, refusal.Code);
        Assert.Equal(OrganizationRole.Member, f.World.RoleOf("user-oleg"));
        Assert.Equal(0, f.World.Store.SaveCount);
    }

    // ADR-0026 rule 5: an organization deactivated since the check takes no change.
    [Fact]
    public async Task Handle_TheOrganizationWasDeactivatedMeanwhile_IsOrganizationNotFound()
    {
        var f = new Fixture();
        f.World.Organization.Deactivate();

        var refusal = await Assert.ThrowsAsync<NotFoundException>(() => f.HandleAsync(f.Command()));

        Assert.Equal(OrganizationErrorCodes.OrganizationNotFound, refusal.Code);
    }

    // BR-M1-20: a malformed organization id answers first, whatever the rest holds.
    [Fact]
    public async Task Handle_MalformedOrganization_IsOrganizationNotFound_Before_EverythingElse()
    {
        var f = new Fixture();

        var refusal = await Assert.ThrowsAsync<NotFoundException>(
            () => f.HandleAsync(new ChangeMemberRoleCommand("abc", "nobody", "boss")));

        Assert.Equal(OrganizationErrorCodes.OrganizationNotFound, refusal.Code);
    }

    // The unknown or inactive member keeps their membership and can be re-roled: the projection never refuses a role change.
    [Fact]
    public async Task Handle_AnUnknownMember_CanBeReRoled_AndIsShownUnknown()
    {
        var f = new Fixture();
        f.World.AddUnknownMember("ghost");

        var result = await f.HandleAsync(f.Command(userId: "ghost", role: "viewer"));

        Assert.Equal(UserRefStates.Unknown, result.Member.User.State);
        Assert.Equal("viewer", result.Member.Role);
    }
}
