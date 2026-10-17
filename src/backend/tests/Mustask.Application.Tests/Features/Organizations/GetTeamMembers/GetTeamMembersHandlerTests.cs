using Mustask.Application.Abstractions.Organizations;
using Mustask.Application.Features.Organizations.GetTeamMembers;
using Mustask.Application.Tests.TestDoubles.Organizations;
using Mustask.Domain.Organizations;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Tests.Features.Organizations.GetTeamMembers;

/// <summary>
/// <c>GetTeamMembers</c> on fakes (mod-016 plan-backend § 3.6 row 7): the team's members as people, in the order of
/// api-contract § 0.5, with the inactive and the unknown kept and marked; the address chain from the outside in.
/// </summary>
public sealed class GetTeamMembersHandlerTests
{
    private sealed class Fixture
    {
        public Fixture()
        {
            Handler = new GetTeamMembersHandler(World.ProjectReads, World.TeamReads, World.Members);
        }

        public MembershipWorld World { get; } = new();

        public GetTeamMembersHandler Handler { get; }

        public GetTeamMembersQuery Query(string? organizationId = null, string? projectId = null, string? teamId = null) =>
            new(organizationId ?? World.OrganizationId, projectId ?? World.ProjectId, teamId ?? World.PlatformId);

        public async Task<IReadOnlyList<UserRefView>> ListAsync(GetTeamMembersQuery? query = null) =>
            (await Handler.Handle(query ?? Query(), CancellationToken.None)).Items;

        public void Join(string userId) => World.Store.AddTeamMember(World.Organization, World.Platform, userId);
    }

    // US-3 · "A team with no members says so": an empty list is an answer, not an error.
    [Fact]
    public async Task Handle_ATeamWithNoMembers_AnswersAnEmptyList()
    {
        var f = new Fixture();

        Assert.Empty(await f.ListAsync());
    }

    // US-3 · "An administrator adds an organization member to a team": the list names her with her e-mail.
    [Fact]
    public async Task Handle_ListsTheTeamsMembersByName_WithTheirEmail_OtherTeamsAreLeftOut()
    {
        var f = new Fixture();
        f.World.AddMember("u-vera", "Вера Новикова", email: "vera@example.org");
        f.World.AddMember("u-oleg", "Олег Иванов");
        f.World.AddMember("u-anna", "Анна Петрова");
        f.Join("u-vera");
        f.Join("u-oleg");
        f.World.Store.AddTeamMember(f.World.Organization, f.World.Project.Team, "u-anna");

        var members = await f.ListAsync();

        Assert.Equal(
            [new UserRefView("u-vera", "Вера Новикова", "vera@example.org", UserRefStates.Active), new UserRefView("u-oleg", "Олег Иванов", "", UserRefStates.Active)],
            members);
    }

    // US-3 · "An inactive or unknown team member stays and is marked": inactive stays by name's turn, unknown last by id.
    [Fact]
    public async Task Handle_InactiveStaysMarked_UnknownIsLastByIdWithNoName()
    {
        var f = new Fixture();
        f.World.AddMember("u-disabled", "Олег Иванов", isActive: false);
        f.World.AddUnknownMember("z-ghost");
        f.World.AddUnknownMember("a-ghost");
        f.World.AddMember("u-vera", "Вера Новикова");

        foreach (var id in new[] { "u-disabled", "z-ghost", "a-ghost", "u-vera" })
        {
            f.Join(id);
        }

        var members = await f.ListAsync();

        Assert.Equal(["u-vera", "u-disabled", "a-ghost", "z-ghost"], members.Select(member => member.Id));
        Assert.Equal(
            [UserRefStates.Active, UserRefStates.Inactive, UserRefStates.Unknown, UserRefStates.Unknown],
            members.Select(member => member.State));
        Assert.All(members.Skip(2), member => Assert.Null(member.DisplayName));
    }

    // US-1 · "Past 500 ids...": a team with 501 members is listed whole.
    [Fact]
    public async Task Handle_501TeamMembers_ListsAll_InBatches()
    {
        var f = new Fixture();

        for (var i = 0; i < 501; i++)
        {
            f.World.AddMember($"u-{i:D3}", $"Участник {i:D3}");
            f.Join($"u-{i:D3}");
        }

        f.World.Directory.LookupSizes.Clear();

        var members = await f.ListAsync();

        Assert.Equal(501, members.Count);
        Assert.All(f.World.Directory.LookupSizes, size => Assert.InRange(size, 1, 500));
        Assert.Equal(2, f.World.Directory.LookupSizes.Count);
    }

    // BR-M1-36 · a deleted team answers its not-found, the same as an unknown one.
    [Fact]
    public async Task Handle_ADeletedTeam_IsTeamNotFound()
    {
        var f = new Fixture();
        f.World.Platform.Delete(2);

        var refusal = await Assert.ThrowsAsync<NotFoundException>(() => f.ListAsync());

        Assert.Equal(OrganizationErrorCodes.TeamNotFound, refusal.Code);
    }

    // BR-M1-22: the chain from the outside in — a team of another project or organization names no team here.
    [Theory]
    [InlineData("malformed")]
    [InlineData("unknown")]
    [InlineData("first team of another project")]
    [InlineData("team of another organization")]
    public async Task Handle_TeamNotOfTheProject_IsTeamNotFound(string how)
    {
        var f = new Fixture();
        var sibling = f.World.Store.AddProject(f.World.Organization, "Второй проект", "SEC");
        var foreign = f.World.Store.AddTeam(
            f.World.Store.AddProject(f.World.Store.AddOrganization("Другая организация"), "Чужой", "OTH"), "Чужая команда");
        var teamId = how switch
        {
            "malformed" => "not-a-team",
            "unknown" => Guid.CreateVersion7().ToString(),
            "first team of another project" => sibling.Team.Id.ToString(),
            _ => foreign.Id.ToString(),
        };

        var refusal = await Assert.ThrowsAsync<NotFoundException>(() => f.ListAsync(f.Query(teamId: teamId)));

        Assert.Equal(OrganizationErrorCodes.TeamNotFound, refusal.Code);
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
            () => f.ListAsync(f.Query(projectId: how == "malformed" ? "abc" : null, teamId: "not-a-team")));

        Assert.Equal(OrganizationErrorCodes.ProjectNotFound, refusal.Code);
    }

    [Fact]
    public async Task Handle_MalformedOrganization_IsOrganizationNotFound_BeforeEverythingElse()
    {
        var f = new Fixture();

        var refusal = await Assert.ThrowsAsync<NotFoundException>(() => f.ListAsync(f.Query(organizationId: "abc", projectId: "x", teamId: "y")));

        Assert.Equal(OrganizationErrorCodes.OrganizationNotFound, refusal.Code);
    }
}
