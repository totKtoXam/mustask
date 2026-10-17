using Mustask.Application.Features.Organizations.RemoveMember;
using Mustask.Application.Tests.TestDoubles;
using Mustask.Application.Tests.TestDoubles.Organizations;
using Mustask.Domain.Identity;
using Mustask.Domain.Organizations;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Tests.Features.Organizations.RemoveMember;

/// <summary>
/// <c>RemoveMember</c> on fakes (mod-016 plan-backend § 3.6 row 6): the order of answers (api-contract § 0.1, § 1.6), the
/// last-administrator rule over the projection (BR-M1-4, BR-M1-32), the end of the user's team memberships (BR-M1-36) and the lock
/// order (§ 3.8).
/// </summary>
public sealed class RemoveMemberHandlerTests
{
    private sealed class Fixture
    {
        public Fixture()
        {
            World.AddMember("user-oleg", "Олег Иванов");
            Handler = new RemoveMemberHandler(World.Organizations, World.Memberships, World.Members, World.User, new FakeUnitOfWork());
        }

        public MembershipWorld World { get; } = new();

        public RemoveMemberHandler Handler { get; }

        public RemoveMemberCommand Command(string? userId = "user-oleg", string? organizationId = null) =>
            new(organizationId ?? World.OrganizationId, userId);

        public Task HandleAsync(RemoveMemberCommand command) => Handler.Handle(command, CancellationToken.None);

        public bool IsMember(string userId) => World.Store.FindMembership(World.Organization.Id, userId) is not null;
    }

    // US-1 · "An administrator removes a member": no longer listed.
    [Fact]
    public async Task Handle_RemovesTheMembership()
    {
        var f = new Fixture();

        await f.HandleAsync(f.Command());

        Assert.False(f.IsMember("user-oleg"));
        Assert.True(f.IsMember(MembershipWorld.AdminId));
    }

    // US-1 / BR-M1-36 · "...nor in «Платформа»'s members": the user leaves every team of the organization with the membership,
    // and the teams keep their other members.
    [Fact]
    public async Task Handle_EndsTheUsersTeamMemberships_OthersKeepTheirs()
    {
        var f = new Fixture();
        f.World.AddMember("user-vera", "Вера Новикова");
        f.World.Store.AddTeamMember(f.World.Organization, f.World.Platform, "user-oleg");
        f.World.Store.AddTeamMember(f.World.Organization, f.World.Project.Team, "user-oleg");
        f.World.Store.AddTeamMember(f.World.Organization, f.World.Platform, "user-vera");

        await f.HandleAsync(f.Command());

        var remaining = Assert.Single(f.World.Store.TeamMemberships);
        Assert.Equal("user-vera", remaining.UserId);
    }

    // plan-backend § 3.8: the organization row first, then the member's row, then the delete; no handler-level save.
    [Fact]
    public async Task Handle_LocksTheOrganizationFirst_ThenTheMember_ThenDeletes()
    {
        var f = new Fixture();

        await f.HandleAsync(f.Command());

        Assert.Equal([$"lock organization {f.World.Organization.Id}", "lock member user-oleg", "delete member user-oleg"], f.World.Store.Calls);
    }

    // BR-M1-4 concerns an administrator only: removing a member never asks the projection for the count.
    [Fact]
    public async Task Handle_RemovingAPlainMember_NeverCountsTheAdministrators()
    {
        var f = new Fixture();

        await f.HandleAsync(f.Command());

        Assert.Empty(f.World.Directory.LookupSizes);
    }

    // US-1 · "An administrator removes themselves while another admin remains": allowed; the caller's next request is a non-member's.
    [Fact]
    public async Task Handle_AnAdministratorRemovesThemselves_WhileAnotherKnownActiveOneRemains_IsAllowed()
    {
        var f = new Fixture();
        f.World.AddMember("admin-boris", "Борис Админов", OrganizationRole.Admin);

        await f.HandleAsync(f.Command(userId: MembershipWorld.AdminId));

        Assert.False(f.IsMember(MembershipWorld.AdminId));
        Assert.True(f.IsMember("admin-boris"));
    }

    // US-1 · "An unknown administrator does not count as the one who remains": A removes themselves with B unknown — refused.
    [Fact]
    public async Task Handle_TheKnownAdministratorRemovesThemselves_WhileOnlyAnUnknownOneRemains_IsLastAdmin()
    {
        var f = new Fixture();
        f.World.AddUnknownMember("admin-unknown", OrganizationRole.Admin);

        var refusal = await Assert.ThrowsAsync<ConflictException>(() => f.HandleAsync(f.Command(userId: MembershipWorld.AdminId)));

        Assert.Equal(OrganizationErrorCodes.MembershipLastAdmin, refusal.Code);
        Assert.True(f.IsMember(MembershipWorld.AdminId));
    }

    [Fact]
    public async Task Handle_TheSoleAdministratorRemovesThemselves_IsLastAdmin()
    {
        var f = new Fixture();

        var refusal = await Assert.ThrowsAsync<ConflictException>(() => f.HandleAsync(f.Command(userId: MembershipWorld.AdminId)));

        Assert.Equal(OrganizationErrorCodes.MembershipLastAdmin, refusal.Code);
    }

    // api-contract § 1.6: "An inactive administrator is removed only while a known active one remains."
    [Fact]
    public async Task Handle_AnInactiveAdministrator_IsRemovedWhileAKnownActiveOneRemains()
    {
        var f = new Fixture();
        f.World.AddMember("admin-disabled", "Отключённый админ", OrganizationRole.Admin, isActive: false);

        await f.HandleAsync(f.Command(userId: "admin-disabled"));

        Assert.False(f.IsMember("admin-disabled"));
    }

    // BR-M1-32: an administrator whose account is disabled counts for nothing as "the one who remains".
    [Fact]
    public async Task Handle_TheKnownAdministratorRemovesThemselves_WhileOnlyAnInactiveOneRemains_IsLastAdmin()
    {
        var f = new Fixture();
        f.World.AddMember("admin-disabled", "Отключённый админ", OrganizationRole.Admin, isActive: false);

        var refusal = await Assert.ThrowsAsync<ConflictException>(() => f.HandleAsync(f.Command(userId: MembershipWorld.AdminId)));

        Assert.Equal(OrganizationErrorCodes.MembershipLastAdmin, refusal.Code);
    }

    // NFR Concurrency / US-1 doc-006 scenario 11 · two administrators remove each other: the first is saved; the second's own
    // membership is gone by the time it holds the lock, so it is answered as a non-member; exactly one administrator is left.
    [Fact]
    public async Task Handle_TwoAdministratorsRemoveEachOther_OneRemovalIsSaved_TheOtherIsAnsweredAsANonMember()
    {
        var f = new Fixture();
        f.World.AddMember("admin-boris", "Борис Админов", OrganizationRole.Admin);

        await f.HandleAsync(f.Command(userId: "admin-boris"));
        f.World.User.SubjectId = "admin-boris";
        var refusal = await Assert.ThrowsAsync<NotFoundException>(() => f.HandleAsync(f.Command(userId: MembershipWorld.AdminId)));

        Assert.Equal(OrganizationErrorCodes.OrganizationNotFound, refusal.Code);
        Assert.True(f.IsMember(MembershipWorld.AdminId));
        Assert.False(f.IsMember("admin-boris"));
    }

    // BE-SEC-1 / plan-backend § 3.6 row 6 · the caller was demoted by an administrator who won the lock: the role read again under
    // the lock no longer holds organizations.membership.write — 403 whomever they remove (a member, or a third administrator while
    // another remains, where BR-M1-4 passes), and nothing is deleted.
    [Theory]
    [InlineData("user-oleg")]
    [InlineData("admin-dina")]
    [InlineData("admin-boris")]
    public async Task Handle_ACallerDemotedBeforeTheLock_IsForbidden_RemovesNothing(string target)
    {
        var f = new Fixture();
        f.World.AddMember("admin-boris", "Борис Админов", OrganizationRole.Admin);
        f.World.AddMember("admin-dina", "Дина Админова", OrganizationRole.Admin);
        f.World.Store.FindMembership(f.World.Organization.Id, "admin-boris")!
            .ChangeRole(OrganizationRole.Member, MembershipWorld.AdminId, f.World.Clock.UtcNow);
        f.World.Store.Calls.Clear();
        f.World.User.SubjectId = "admin-boris";

        var refusal = await Assert.ThrowsAsync<ForbiddenException>(() => f.HandleAsync(f.Command(userId: target)));

        Assert.Equal(OrganizationErrorCodes.AccessForbidden, refusal.Code);
        Assert.True(f.IsMember(target));
        Assert.DoesNotContain($"delete member {target}", f.World.Store.Calls);
    }

    // BE-SEC-1 / api-contract § 1.6 · BR-M1-4 answers before the role read again: the demoted loser of two administrators who
    // demote and remove each other hears last_admin when the removal would take the last administrator.
    [Fact]
    public async Task Handle_TheDemotedLoserRemovingTheLastAdministrator_HearsLastAdmin_NotForbidden()
    {
        var f = new Fixture();
        f.World.AddMember("admin-boris", "Борис Админов", OrganizationRole.Admin);
        f.World.Store.FindMembership(f.World.Organization.Id, "admin-boris")!
            .ChangeRole(OrganizationRole.Member, MembershipWorld.AdminId, f.World.Clock.UtcNow);
        f.World.User.SubjectId = "admin-boris";

        var refusal = await Assert.ThrowsAsync<ConflictException>(() => f.HandleAsync(f.Command(userId: MembershipWorld.AdminId)));

        Assert.Equal(OrganizationErrorCodes.MembershipLastAdmin, refusal.Code);
        Assert.True(f.IsMember(MembershipWorld.AdminId));
    }

    // US-1 · "Removing a membership that is already gone": refused with «Участник не найден.», including a blank or over-long id.
    [Theory]
    [InlineData("nobody")]
    [InlineData("")]
    [InlineData("256")]
    public async Task Handle_NoSuchMembership_IsMembershipNotFound_NothingLocked(string userId)
    {
        var f = new Fixture();
        var submitted = userId == "256" ? new string('u', 256) : userId;

        var refusal = await Assert.ThrowsAsync<NotFoundException>(() => f.HandleAsync(f.Command(userId: submitted)));

        Assert.Equal(OrganizationErrorCodes.MembershipNotFound, refusal.Code);
        Assert.Empty(f.World.Store.Calls);
    }

    [Fact]
    public async Task Handle_RemovedByAnotherAdministratorMeanwhile_IsMembershipNotFound()
    {
        var f = new Fixture();
        f.World.Memberships.BeforeNextLockMember = () => f.World.Store.DeleteMembership(f.World.Organization.Id, "user-oleg");

        var refusal = await Assert.ThrowsAsync<NotFoundException>(() => f.HandleAsync(f.Command()));

        Assert.Equal(OrganizationErrorCodes.MembershipNotFound, refusal.Code);
    }

    [Fact]
    public async Task Handle_TheCallersOwnMembershipIsGone_IsOrganizationNotFound_RemovesNothing()
    {
        var f = new Fixture();
        f.World.User.SubjectId = "admin-removed";

        var refusal = await Assert.ThrowsAsync<NotFoundException>(() => f.HandleAsync(f.Command()));

        Assert.Equal(OrganizationErrorCodes.OrganizationNotFound, refusal.Code);
        Assert.True(f.IsMember("user-oleg"));
    }

    [Fact]
    public async Task Handle_MalformedOrganization_IsOrganizationNotFound_Before_TheMember()
    {
        var f = new Fixture();

        var refusal = await Assert.ThrowsAsync<NotFoundException>(() => f.HandleAsync(new RemoveMemberCommand("abc", "nobody")));

        Assert.Equal(OrganizationErrorCodes.OrganizationNotFound, refusal.Code);
    }

    [Fact]
    public async Task Handle_TheOrganizationWasDeactivatedMeanwhile_IsOrganizationNotFound_RemovesNothing()
    {
        var f = new Fixture();
        f.World.Organization.Deactivate();

        var refusal = await Assert.ThrowsAsync<NotFoundException>(() => f.HandleAsync(f.Command()));

        Assert.Equal(OrganizationErrorCodes.OrganizationNotFound, refusal.Code);
        Assert.True(f.IsMember("user-oleg"));
    }

    // US-1 · BR-M1-17 / BR-M1-26 · an unknown or inactive MEMBER (not an administrator) is removed like any other: no count needed.
    [Fact]
    public async Task Handle_AnUnknownMember_IsRemoved()
    {
        var f = new Fixture();
        f.World.AddUnknownMember("ghost");

        await f.HandleAsync(f.Command(userId: "ghost"));

        Assert.False(f.IsMember("ghost"));
    }
}
