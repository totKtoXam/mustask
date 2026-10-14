using Mustask.Application.Features.Organizations.RemoveTeamMember;
using Mustask.Application.Tests.TestDoubles.Organizations;
using Mustask.Domain.Identity;
using Mustask.Domain.Organizations;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Tests.Features.Organizations.RemoveTeamMember;

/// <summary>
/// <c>RemoveTeamMember</c> on fakes (mod-016 plan-backend § 3.6 row 10): the delete, the member who is gone, and the address chain
/// from the outside in (api-contract § 2.4).
/// </summary>
public sealed class RemoveTeamMemberHandlerTests
{
    private sealed class Fixture
    {
        public Fixture()
        {
            World.AddMember("u-anna", "Анна Смирнова");
            World.Store.AddTeamMember(World.Organization, World.Platform.Team, "u-anna");
            Handler = new RemoveTeamMemberHandler(World.ProjectReads, World.TeamReads, World.TeamMemberships);
        }

        public MembershipWorld World { get; } = new();

        public RemoveTeamMemberHandler Handler { get; }

        public RemoveTeamMemberCommand Command(string? userId = "u-anna", string? teamId = null, string? projectId = null, string? organizationId = null) =>
            new(organizationId ?? World.OrganizationId, projectId ?? World.ProjectId, teamId ?? World.PlatformId, userId);

        public Task HandleAsync(RemoveTeamMemberCommand command) => Handler.Handle(command, CancellationToken.None);
    }

    // US-3 · "An administrator removes a team member": she is no longer in the team, and still a member of the organization
    // with her role, so she still reads every team.
    [Fact]
    public async Task Handle_RemovesTheTeamMembership_TheOrganizationMembershipAndRoleStay()
    {
        var f = new Fixture();

        await f.HandleAsync(f.Command());

        Assert.Empty(f.World.Store.TeamMemberships);
        Assert.Equal(OrganizationRole.Member, f.World.RoleOf("u-anna"));
    }

    // Only that team's membership goes: the same user in another team stays there.
    [Fact]
    public async Task Handle_OtherTeamsOfTheUser_AreUntouched()
    {
        var f = new Fixture();
        f.World.Store.AddTeamMember(f.World.Organization, f.World.Project.FirstTeam, "u-anna");

        await f.HandleAsync(f.Command());

        var remaining = Assert.Single(f.World.Store.TeamMemberships);
        Assert.Equal(f.World.Project.FirstTeam.Id, remaining.TeamId);
    }

    // US-3 · "Removing a team member who is already gone": refused with the team-member not-found code.
    [Fact]
    public async Task Handle_TheUserIsNotInTheTeam_IsTeamMemberNotFound()
    {
        var f = new Fixture();
        f.World.AddMember("u-oleg", "Олег Иванов");

        var refusal = await Assert.ThrowsAsync<NotFoundException>(() => f.HandleAsync(f.Command(userId: "u-oleg")));

        Assert.Equal(OrganizationErrorCodes.TeamMemberNotFound, refusal.Code);
        Assert.Single(f.World.Store.TeamMemberships);
    }

    [Fact]
    public async Task Handle_RemovedTwice_TheSecondIsTeamMemberNotFound()
    {
        var f = new Fixture();
        await f.HandleAsync(f.Command());

        var refusal = await Assert.ThrowsAsync<NotFoundException>(() => f.HandleAsync(f.Command()));

        Assert.Equal(OrganizationErrorCodes.TeamMemberNotFound, refusal.Code);
    }

    // api-contract § 0.3: a blank or over-long user id names no membership, like an unknown one.
    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("256")]
    [InlineData(null)]
    public async Task Handle_UserIdBlankOrTooLong_IsTeamMemberNotFound(string? userId)
    {
        var f = new Fixture();
        var submitted = userId == "256" ? new string('u', 256) : userId;

        var refusal = await Assert.ThrowsAsync<NotFoundException>(() => f.HandleAsync(f.Command(userId: submitted)));

        Assert.Equal(OrganizationErrorCodes.TeamMemberNotFound, refusal.Code);
    }

    // BR-M1-36 · a deleted team answers the team's not-found: its memberships ended with it.
    [Fact]
    public async Task Handle_ADeletedTeam_IsTeamNotFound()
    {
        var f = new Fixture();
        f.World.Platform.Team.Delete();

        var refusal = await Assert.ThrowsAsync<NotFoundException>(() => f.HandleAsync(f.Command()));

        Assert.Equal(OrganizationErrorCodes.TeamNotFound, refusal.Code);
    }

    // BR-M1-22: the address chain; the team's level answers before the member's.
    [Theory]
    [InlineData("malformed")]
    [InlineData("unknown")]
    [InlineData("first team of another project")]
    public async Task Handle_TeamNotOfTheProject_IsTeamNotFound_BeforeTheMember(string how)
    {
        var f = new Fixture();
        var sibling = f.World.Store.AddProject(f.World.Organization, "Второй проект", "SEC");
        var teamId = how switch
        {
            "malformed" => "not-a-team",
            "unknown" => Guid.CreateVersion7().ToString(),
            _ => sibling.FirstTeam.Id.ToString(),
        };

        var refusal = await Assert.ThrowsAsync<NotFoundException>(() => f.HandleAsync(f.Command(userId: "nobody", teamId: teamId)));

        Assert.Equal(OrganizationErrorCodes.TeamNotFound, refusal.Code);
        Assert.Single(f.World.Store.TeamMemberships);
    }

    [Fact]
    public async Task Handle_DeactivatedProject_IsProjectNotFound_BeforeTheTeam()
    {
        var f = new Fixture();
        f.World.Project.Project.Deactivate();

        var refusal = await Assert.ThrowsAsync<NotFoundException>(() => f.HandleAsync(f.Command(teamId: "not-a-team")));

        Assert.Equal(OrganizationErrorCodes.ProjectNotFound, refusal.Code);
    }

    [Fact]
    public async Task Handle_MalformedOrganization_IsOrganizationNotFound_BeforeEverythingElse()
    {
        var f = new Fixture();

        var refusal = await Assert.ThrowsAsync<NotFoundException>(() => f.HandleAsync(f.Command(organizationId: "abc", projectId: "x", teamId: "y")));

        Assert.Equal(OrganizationErrorCodes.OrganizationNotFound, refusal.Code);
    }
}
