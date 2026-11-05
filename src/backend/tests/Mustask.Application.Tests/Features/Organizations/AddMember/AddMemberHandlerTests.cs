using Mustask.Application.Abstractions.Organizations;
using Mustask.Application.Features.Organizations.AddMember;
using Mustask.Application.Tests.TestDoubles.Organizations;
using Mustask.Domain.Identity;
using Mustask.Domain.Organizations;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Tests.Features.Organizations.AddMember;

/// <summary>
/// <c>AddMember</c> on fakes (mod-016 plan-backend § 3.6 row 4, the reference use case): what it stores, the order of its
/// answers (api-contract § 0.1, § 1.4) and the absence of locks (§ 3.8). The 404/403/401 before it are the scoped check's,
/// tested at the Api; the race on the primary key is the Api's too — here the loser's save is made to fail the way the
/// repository reports it.
/// </summary>
public sealed class AddMemberHandlerTests
{
    private sealed class Fixture
    {
        public Fixture()
        {
            World.AddOutsider("user-oleg", "Олег Иванов", email: "oleg@example.org");
            Handler = new AddMemberHandler(World.Memberships, World.Members, World.Directory, World.Clock, World.User);
        }

        public MembershipWorld World { get; } = new();

        public AddMemberHandler Handler { get; }

        public AddMemberCommand Command(string? userId = "user-oleg", string? role = "member", string? organizationId = null) =>
            new(organizationId ?? World.OrganizationId, userId, role);

        public Task<AddMemberResult> HandleAsync(AddMemberCommand command) => Handler.Handle(command, CancellationToken.None);
    }

    // US-1 · "An administrator adds a member from «Участники»": the user is listed with the chosen role, «Добавлен» shows the
    // administrator and the time, «Роль изменена» is empty.
    [Fact]
    public async Task Handle_ActiveProjectedUser_AddsTheMembershipWithWhoAndWhen_NoRoleChange()
    {
        var f = new Fixture();

        var result = await f.HandleAsync(f.Command());

        var stored = f.World.Store.FindMembership(f.World.Organization.Id, "user-oleg")!;
        Assert.Equal(OrganizationRole.Member, stored.Role);
        Assert.Equal(MembershipWorld.AdminId, stored.AddedBy);
        Assert.Equal(f.World.Clock.UtcNow, stored.AddedAt);
        Assert.Null(stored.RoleChangedBy);

        Assert.Equal(new UserRefView("user-oleg", "Олег Иванов", "oleg@example.org", UserRefStates.Active), result.Member.User);
        Assert.Equal("member", result.Member.Role);
        Assert.Equal("Анна Смирнова", result.Member.AddedBy.DisplayName);
        Assert.Equal(f.World.Clock.UtcNow, result.Member.AddedAt);
        Assert.Null(result.Member.RoleChangedBy);
        Assert.Null(result.Member.RoleChangedAt);
    }

    // US-1 · the role chosen is the role stored, for each of the three.
    [Theory]
    [InlineData("admin", OrganizationRole.Admin)]
    [InlineData("member", OrganizationRole.Member)]
    [InlineData("viewer", OrganizationRole.Viewer)]
    public async Task Handle_EachOfTheThreeRoles_IsStoredAsChosen(string role, OrganizationRole expected)
    {
        var f = new Fixture();

        await f.HandleAsync(f.Command(role: role));

        Assert.Equal(expected, f.World.RoleOf("user-oleg"));
    }

    // plan-backend § 3.8: adding cannot threaten BR-M1-4 and the primary key closes BR-M1-5, so no lock is taken: one save.
    [Fact]
    public async Task Handle_TakesNoLock_OneSave()
    {
        var f = new Fixture();

        await f.HandleAsync(f.Command());

        Assert.Equal(["save"], f.World.Store.Calls);
    }

    // US-1 · "A role outside the three is refused": any other spelling — letter case included — is the role reason, and
    // nothing is stored.
    [Theory]
    [InlineData("owner")]
    [InlineData("Admin")]
    [InlineData("MEMBER")]
    [InlineData(" viewer")]
    [InlineData("")]
    [InlineData(null)]
    public async Task Handle_RoleOutsideTheThree_IsValidationFailedWithRoleInvalid_StoresNothing(string? role)
    {
        var f = new Fixture();

        var refusal = await Assert.ThrowsAsync<ValidationException>(() => f.HandleAsync(f.Command(role: role)));

        Assert.Equal(OrganizationErrorCodes.ValidationFailed, refusal.Code);
        Assert.Equal(["role"], refusal.Errors.Keys);
        Assert.Equal(OrganizationErrorCodes.MembershipRoleInvalid, Assert.Single(refusal.Errors["role"]).Code);
        Assert.False(await f.World.Memberships.ExistsAsync(f.World.Organization.Id, "user-oleg", CancellationToken.None));
        Assert.Equal(0, f.World.Store.SaveCount);
    }

    // api-contract § 1.4: a user id that is missing, blank or over 255 characters is the user reason.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("256")]
    public async Task Handle_UserIdMissingBlankOrTooLong_IsValidationFailedWithUserInvalid(string? userId)
    {
        var f = new Fixture();
        var submitted = userId == "256" ? new string('u', 256) : userId;

        var refusal = await Assert.ThrowsAsync<ValidationException>(() => f.HandleAsync(f.Command(userId: submitted)));

        Assert.Equal(["userId"], refusal.Errors.Keys);
        Assert.Equal(OrganizationErrorCodes.MembershipUserInvalid, Assert.Single(refusal.Errors["userId"]).Code);
        Assert.Equal(0, f.World.Store.SaveCount);
    }

    // api-contract § 0.5 / §1.4: every field reason at once — a bad user id and a bad role come back together.
    [Fact]
    public async Task Handle_UserIdAndRoleBothBad_IsOneValidationFailedWithBothReasons()
    {
        var f = new Fixture();

        var refusal = await Assert.ThrowsAsync<ValidationException>(() => f.HandleAsync(f.Command(userId: " ", role: "boss")));

        Assert.Equal(["role", "userId"], refusal.Errors.Keys.Order(StringComparer.Ordinal));
    }

    // api-contract § 1.4: the values answer before the user is looked up: a bad role with an unknown user is the 400.
    [Fact]
    public async Task Handle_BadRoleAndUnknownUser_AnswersTheValuesFirst()
    {
        var f = new Fixture();

        await Assert.ThrowsAsync<ValidationException>(() => f.HandleAsync(f.Command(userId: "nobody-known", role: "boss")));

        Assert.Empty(f.World.Directory.LookupSizes);
    }

    // US-1 · "An inactive or unknown user is neither offered nor added" (BR-M1-32): the projection does not hold the id.
    [Fact]
    public async Task Handle_UserTheProjectionDoesNotKnow_IsUserUnavailable_NothingCreated()
    {
        var f = new Fixture();

        var refusal = await Assert.ThrowsAsync<ConflictException>(() => f.HandleAsync(f.Command(userId: "never-projected")));

        Assert.Equal(OrganizationErrorCodes.MembershipUserUnavailable, refusal.Code);
        Assert.DoesNotContain(f.World.Store.Memberships, membership => membership.UserId == "never-projected");
        Assert.Equal(0, f.World.Store.SaveCount);
    }

    // US-1 · the account is disabled in Keycloak: the same refusal.
    [Fact]
    public async Task Handle_InactiveUser_IsUserUnavailable_NothingCreated()
    {
        var f = new Fixture();
        f.World.AddOutsider("user-disabled", "Отключённый", isActive: false);

        var refusal = await Assert.ThrowsAsync<ConflictException>(() => f.HandleAsync(f.Command(userId: "user-disabled")));

        Assert.Equal(OrganizationErrorCodes.MembershipUserUnavailable, refusal.Code);
        Assert.Single(f.World.Store.Memberships);
    }

    // US-1 doc-006 scenario 6 · "Пользователь уже состоит в организации": an active member added again.
    [Fact]
    public async Task Handle_AlreadyAMember_IsAlreadyMember_TheRoleIsUnchanged()
    {
        var f = new Fixture();
        f.World.AddMember("user-oleg", "Олег Иванов", OrganizationRole.Viewer);

        var refusal = await Assert.ThrowsAsync<ConflictException>(() => f.HandleAsync(f.Command(role: "admin")));

        Assert.Equal(OrganizationErrorCodes.MembershipAlreadyMember, refusal.Code);
        Assert.Equal(OrganizationRole.Viewer, f.World.RoleOf("user-oleg"));
        Assert.Equal(0, f.World.Store.SaveCount);
    }

    // api-contract § 1.4: "user_unavailable is answered before already_member" — a member whose account has been disabled.
    [Fact]
    public async Task Handle_AnInactiveExistingMember_AnswersUserUnavailable_BeforeAlreadyMember()
    {
        var f = new Fixture();
        f.World.AddMember("user-oleg", "Олег Иванов", isActive: false);

        var refusal = await Assert.ThrowsAsync<ConflictException>(() => f.HandleAsync(f.Command()));

        Assert.Equal(OrganizationErrorCodes.MembershipUserUnavailable, refusal.Code);
    }

    // US-1 / NFR Concurrency (BR-M1-5): two administrators add one user at the same moment; the loser's save fails on the
    // primary key and the repository answers with the pre-check's code, which reaches the client unchanged.
    [Fact]
    public async Task Handle_LosesTheAddRace_AnswersAlreadyMember_StoresNothing()
    {
        var f = new Fixture();
        f.World.Store.FailNextSave(new ConflictException(OrganizationErrorCodes.MembershipAlreadyMember));

        var refusal = await Assert.ThrowsAsync<ConflictException>(() => f.HandleAsync(f.Command()));

        Assert.Equal(OrganizationErrorCodes.MembershipAlreadyMember, refusal.Code);
        Assert.DoesNotContain(f.World.Store.Memberships, membership => membership.UserId == "user-oleg");
    }

    // BR-M1-20 / api-contract § 0.1: behind the check the organization id is well formed; if it were not, the address answers
    // first, whatever the body holds.
    [Theory]
    [InlineData("not-an-id")]
    [InlineData("")]
    [InlineData(null)]
    public async Task Handle_MalformedOrganization_IsOrganizationNotFound_BeforeTheBody(string? organizationId)
    {
        var f = new Fixture();

        var refusal = await Assert.ThrowsAsync<NotFoundException>(
            () => f.HandleAsync(new AddMemberCommand(organizationId, " ", "boss")));

        Assert.Equal(OrganizationErrorCodes.OrganizationNotFound, refusal.Code);
        Assert.Empty(f.World.Store.Calls);
    }

    // api-contract § 0.3: a user id is an opaque string of 1 to 255 characters; the longest is accepted when the projection knows it.
    [Fact]
    public async Task Handle_TheLongestUserId_IsAccepted()
    {
        var f = new Fixture();
        var longest = new string('u', OrganizationLimits.UserIdMaxLength);
        f.World.AddOutsider(longest, "Длинный");

        var result = await f.HandleAsync(f.Command(userId: longest));

        Assert.Equal(longest, result.Member.User.Id);
    }

    // BR-M1-17 / api-contract § 0.4: «Добавлен» names the adder even when the projection does not know them yet: the adder is
    // shown as unknown, the membership is still made (access is read from the membership, never from the projection).
    [Fact]
    public async Task Handle_TheAdderIsUnknownToTheProjection_TheAdderIsShownUnknown()
    {
        var f = new Fixture();
        f.World.User.SubjectId = "creator-not-projected";
        f.World.AddUnknownMember("creator-not-projected", OrganizationRole.Admin);

        var result = await f.HandleAsync(f.Command());

        Assert.Equal(new UserRefView("creator-not-projected", null, null, UserRefStates.Unknown), result.Member.AddedBy);
        Assert.Equal("creator-not-projected", f.World.Store.FindMembership(f.World.Organization.Id, "user-oleg")!.AddedBy);
    }

    // The user id is taken as given: no trimming, so a padded id is simply an id the projection does not hold.
    [Fact]
    public async Task Handle_APaddedUserId_IsNotTrimmed_AndIsUnavailable()
    {
        var f = new Fixture();

        var refusal = await Assert.ThrowsAsync<ConflictException>(() => f.HandleAsync(f.Command(userId: " user-oleg ")));

        Assert.Equal(OrganizationErrorCodes.MembershipUserUnavailable, refusal.Code);
    }
}
