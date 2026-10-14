using Mustask.Application.Abstractions.Organizations;
using Mustask.Application.Features.Organizations.AddTeamMember;
using Mustask.Application.Tests.TestDoubles;
using Mustask.Application.Tests.TestDoubles.Organizations;
using Mustask.Domain.Identity;
using Mustask.Domain.Organizations;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Tests.Features.Organizations.AddTeamMember;

/// <summary>
/// <c>AddTeamMember</c> on fakes (mod-016 plan-backend § 3.6 row 9): what it stores, the order of answers (api-contract § 0.1, § 2.3)
/// and the lock (§ 3.8: the team <c>FOR SHARE</c>, re-checked active). The races are the Api tests' (a real lock is the referee);
/// here the loser's save is made to fail the way the repository reports it.
/// </summary>
public sealed class AddTeamMemberHandlerTests
{
    private sealed class Fixture
    {
        public Fixture()
        {
            World.AddMember("u-anna", "Анна Смирнова", email: "anna@example.org");
            Handler = new AddTeamMemberHandler(
                World.Projects, World.Teams, World.TeamMemberships, World.Members, World.Directory, new FakeUnitOfWork());
        }

        public MembershipWorld World { get; } = new();

        public AddTeamMemberHandler Handler { get; }

        public AddTeamMemberCommand Command(string? userId = "u-anna", string? teamId = null, string? projectId = null, string? organizationId = null) =>
            new(organizationId ?? World.OrganizationId, projectId ?? World.ProjectId, teamId ?? World.PlatformId, userId);

        public Task<AddTeamMemberResult> HandleAsync(AddTeamMemberCommand command) => Handler.Handle(command, CancellationToken.None);
    }

    // US-3 · "An administrator adds an organization member to a team": she is listed with her e-mail, her role is unchanged.
    [Fact]
    public async Task Handle_AnActiveMember_JoinsTheTeam_RoleUnchanged_AnswersTheirUserRef()
    {
        var f = new Fixture();

        var result = await f.HandleAsync(f.Command());

        var stored = Assert.Single(f.World.Store.TeamMemberships);
        Assert.Equal((f.World.Platform.Team.Id, f.World.Organization.Id, "u-anna"), (stored.TeamId, stored.OrganizationId, stored.UserId));
        Assert.Equal(OrganizationRole.Member, f.World.RoleOf("u-anna"));
        Assert.Equal(new UserRefView("u-anna", "Анна Смирнова", "anna@example.org", UserRefStates.Active), result.Member);
    }

    // plan-backend § 3.8 / ADR-0026 rule 8: the team FOR SHARE, then one save. The organization row is never taken.
    [Fact]
    public async Task Handle_LocksTheTeamForShare_ThenSavesOnce_NeverTheOrganization()
    {
        var f = new Fixture();

        await f.HandleAsync(f.Command());

        Assert.Equal([$"lock team share {f.World.Platform.Team.Id}", "save"], f.World.Store.Calls);
    }

    // US-3 · "Only active organization members are offered and accepted": a user of another organization, a projected user in none, a
    // member whose account is disabled, an unknown id — each is refused with the same code, and nothing is stored.
    [Theory]
    [InlineData("other organization")]
    [InlineData("projected non-member")]
    [InlineData("disabled member")]
    [InlineData("unknown id")]
    public async Task Handle_NotAnActiveMemberOfThisOrganization_IsMemberNotInOrganization_NothingStored(string how)
    {
        var f = new Fixture();
        var other = f.World.Store.AddOrganization("Другая организация");
        f.World.Directory.Add("u-other", "Чужой");
        f.World.Store.AddMember(other, "u-other");
        f.World.AddOutsider("u-nobody", "Посторонний");
        f.World.AddMember("u-disabled", "Отключённый", isActive: false);
        var userId = how switch
        {
            "other organization" => "u-other",
            "projected non-member" => "u-nobody",
            "disabled member" => "u-disabled",
            _ => "never-projected",
        };

        var refusal = await Assert.ThrowsAsync<ConflictException>(() => f.HandleAsync(f.Command(userId: userId)));

        Assert.Equal(OrganizationErrorCodes.TeamMemberNotInOrganization, refusal.Code);
        Assert.Empty(f.World.Store.TeamMemberships);
        Assert.Equal(0, f.World.Store.SaveCount);
    }

    // US-3 · "A user is in a team at most once".
    [Fact]
    public async Task Handle_AlreadyInTheTeam_IsMemberExists_ListedOnce()
    {
        var f = new Fixture();
        f.World.Store.AddTeamMember(f.World.Organization, f.World.Platform.Team, "u-anna");

        var refusal = await Assert.ThrowsAsync<ConflictException>(() => f.HandleAsync(f.Command()));

        Assert.Equal(OrganizationErrorCodes.TeamMemberExists, refusal.Code);
        Assert.Single(f.World.Store.TeamMemberships);
    }

    // US-3 · "when two administrators add a user ... at the same moment, one add succeeds, the other is refused": the loser's save
    // fails on the primary key and the repository's answer reaches the client unchanged.
    [Fact]
    public async Task Handle_LosesTheAddRace_AnswersMemberExists_StoresNothing()
    {
        var f = new Fixture();
        f.World.Store.FailNextSave(new ConflictException(OrganizationErrorCodes.TeamMemberExists));

        var refusal = await Assert.ThrowsAsync<ConflictException>(() => f.HandleAsync(f.Command()));

        Assert.Equal(OrganizationErrorCodes.TeamMemberExists, refusal.Code);
        Assert.Empty(f.World.Store.TeamMemberships);
    }

    // US-3 · "A team member added while the organization membership is removed": the composite key refuses the insert and the
    // repository answers member_not_in_organization.
    [Fact]
    public async Task Handle_TheOrganizationMembershipEndedWhileTheAddRan_IsMemberNotInOrganization()
    {
        var f = new Fixture();
        f.World.Store.FailNextSave(new ConflictException(OrganizationErrorCodes.TeamMemberNotInOrganization));

        var refusal = await Assert.ThrowsAsync<ConflictException>(() => f.HandleAsync(f.Command()));

        Assert.Equal(OrganizationErrorCodes.TeamMemberNotInOrganization, refusal.Code);
        Assert.Empty(f.World.Store.TeamMemberships);
    }

    // The fake store models the database's composite key: a team membership whose organization membership is not there is refused
    // (the Infrastructure test proves the real key; this pins the fake the other tests lean on).
    [Fact]
    public async Task TheFakeStore_RefusesATeamMembershipWithoutItsOrganizationMembership()
    {
        var f = new Fixture();
        f.World.TeamMemberships.Add(TeamMembership.Add(f.World.Platform.Team.Id, f.World.Organization.Id, "u-removed"));

        var refusal = await Assert.ThrowsAsync<ConflictException>(() => f.World.TeamMemberships.SaveAsync(CancellationToken.None));

        Assert.Equal(OrganizationErrorCodes.TeamMemberNotInOrganization, refusal.Code);
    }

    // US-3 · "A member added to a team that is being deleted": the deletion committed first, the add finds the team gone.
    [Fact]
    public async Task Handle_TheTeamWasDeleted_IsTeamNotFound_NoMembershipOfADeletedTeamRemains()
    {
        var f = new Fixture();
        f.World.Platform.Team.Delete();

        var refusal = await Assert.ThrowsAsync<NotFoundException>(() => f.HandleAsync(f.Command()));

        Assert.Equal(OrganizationErrorCodes.TeamNotFound, refusal.Code);
        Assert.Empty(f.World.Store.TeamMemberships);
        Assert.Equal([$"lock team share {f.World.Platform.Team.Id}"], f.World.Store.Calls);
    }

    // api-contract § 2.3: the user id is a field of the body — missing, blank or over-long is the user reason; it answers after
    // the address (the team is locked first) and before the user is looked up.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("256")]
    public async Task Handle_UserIdMissingBlankOrTooLong_IsValidationFailedWithUserInvalid(string? userId)
    {
        var f = new Fixture();
        var submitted = userId == "256" ? new string('u', 256) : userId;

        var refusal = await Assert.ThrowsAsync<ValidationException>(() => f.HandleAsync(f.Command(userId: submitted)));

        Assert.Equal(OrganizationErrorCodes.ValidationFailed, refusal.Code);
        Assert.Equal(OrganizationErrorCodes.MembershipUserInvalid, Assert.Single(refusal.Errors["userId"]).Code);
        Assert.Empty(f.World.Directory.LookupSizes);
    }

    // api-contract § 0.1 order: the address answers before the body — a deleted team with a bad body is the team's not-found.
    [Fact]
    public async Task Handle_DeletedTeamAndABadBody_AnswersTheAddressFirst()
    {
        var f = new Fixture();
        f.World.Platform.Team.Delete();

        var refusal = await Assert.ThrowsAsync<NotFoundException>(() => f.HandleAsync(f.Command(userId: " ")));

        Assert.Equal(OrganizationErrorCodes.TeamNotFound, refusal.Code);
    }

    // BR-M1-22: the chain from the outside in.
    [Theory]
    [InlineData("malformed team")]
    [InlineData("unknown team")]
    [InlineData("team of another project")]
    public async Task Handle_TeamNotOfTheProject_IsTeamNotFound_NothingStored(string how)
    {
        var f = new Fixture();
        var sibling = f.World.Store.AddProject(f.World.Organization, "Второй проект", "SEC");
        var teamId = how switch
        {
            "malformed team" => "not-a-team",
            "unknown team" => Guid.CreateVersion7().ToString(),
            _ => sibling.FirstTeam.Id.ToString(),
        };

        var refusal = await Assert.ThrowsAsync<NotFoundException>(() => f.HandleAsync(f.Command(teamId: teamId)));

        Assert.Equal(OrganizationErrorCodes.TeamNotFound, refusal.Code);
        Assert.Empty(f.World.Store.TeamMemberships);
    }

    [Theory]
    [InlineData("malformed")]
    [InlineData("deactivated")]
    public async Task Handle_ProjectNotReachable_IsProjectNotFound_BeforeTheTeam(string how)
    {
        var f = new Fixture();

        if (how == "deactivated")
        {
            f.World.Project.Project.Deactivate();
        }

        var refusal = await Assert.ThrowsAsync<NotFoundException>(
            () => f.HandleAsync(f.Command(projectId: how == "malformed" ? "abc" : null, teamId: "not-a-team")));

        Assert.Equal(OrganizationErrorCodes.ProjectNotFound, refusal.Code);
        Assert.Empty(f.World.Store.Calls);
    }

    [Fact]
    public async Task Handle_MalformedOrganization_IsOrganizationNotFound_BeforeEverythingElse()
    {
        var f = new Fixture();

        var refusal = await Assert.ThrowsAsync<NotFoundException>(() => f.HandleAsync(f.Command(organizationId: "abc", projectId: "x", teamId: "y")));

        Assert.Equal(OrganizationErrorCodes.OrganizationNotFound, refusal.Code);
        Assert.Empty(f.World.Store.Calls);
    }
}
