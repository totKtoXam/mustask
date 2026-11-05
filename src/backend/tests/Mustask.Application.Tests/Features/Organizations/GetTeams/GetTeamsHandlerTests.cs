using Mustask.Application.Features.Organizations.GetTeams;
using Mustask.Application.Tests.TestDoubles;
using Mustask.Application.Tests.TestDoubles.Organizations;
using Mustask.Domain.Organizations;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Tests.Features.Organizations.GetTeams;

/// <summary>
/// <c>GetTeams</c> on fakes (feat-028 api-contract § 1.3, § 2.4; US-2 "a project without a team works", US-3 "only linked teams are
/// offered", "the mark follows the links"): the project's <b>linked</b> active teams, by name, each with its default mark (BR-OL-7) and
/// whether the caller is in it. Rewritten from the project's own teams (mod-017 BR-OT-4, BR-OT-11; D-16).
/// </summary>
public sealed class GetTeamsHandlerTests
{
    private sealed class Fixture
    {
        public Fixture()
        {
            Organization = Store.AddOrganization();
            Project = Store.AddProject(Organization);
            User.SubjectId = "u-caller";
            Handler = new GetTeamsHandler(new FakeProjectReadModel(Store), new FakeTeamReadModel(Store), User);
        }

        public InMemoryOrganizations Store { get; } = new();

        public FakeCurrentUser User { get; } = new();

        public Organization Organization { get; }

        public NewProject Project { get; }

        public GetTeamsHandler Handler { get; }

        public GetTeamsQuery Query(string? projectId = null, string? organizationId = null) =>
            new(organizationId ?? Organization.Id.ToString(), projectId ?? Project.Project.Id.ToString());

        public async Task<IReadOnlyList<TeamResponse>> ListAsync(GetTeamsQuery? query = null) =>
            (await Handler.Handle(query ?? Query(), CancellationToken.None)).Items;
    }

    // US-2 · "a project without a team works" (the state of every project right after the upgrade): an empty list, not a not-found.
    [Fact]
    public async Task Handle_ProjectWithNoTeam_ReturnsEmpty()
    {
        var f = new Fixture();

        Assert.Empty(await f.ListAsync());
    }

    // US-3 · "only linked teams are offered": linked active teams by name, no team first; a team of the organization that is not
    // linked, and a team of another organization, are left out. Two teams and none named: no mark.
    [Fact]
    public async Task Get_OnlyLinkedActiveTeams_ByName_NoMarkWhenSeveralAndNoneNamed()
    {
        var f = new Fixture();
        var beta = f.Store.AddLinkedTeam(f.Organization, f.Project, "Бета");
        var alpha = f.Store.AddLinkedTeam(f.Organization, f.Project, "альфа");
        f.Store.AddTeam(f.Organization, "Не связана");
        f.Store.AddTeam(f.Store.AddOrganization("Другая"), "Чужая");

        var teams = await f.ListAsync();

        Assert.Equal([new TeamResponse(alpha.Id, "альфа", null), new TeamResponse(beta.Id, "Бета", null)], teams);
    }

    // US-3 · "the mark follows the links": the only linked team is marked onlyTeam; naming one carries named and no other mark.
    [Fact]
    public async Task Get_DefaultMark_FollowsTheLinks_NamedOnlyTeamNone()
    {
        var f = new Fixture();
        var alpha = f.Store.AddLinkedTeam(f.Organization, f.Project, "Альфа");

        Assert.Equal([new TeamResponse(alpha.Id, "Альфа", "onlyTeam")], await f.ListAsync());

        var beta = f.Store.AddLinkedTeam(f.Organization, f.Project, "Бета");
        Assert.All(await f.ListAsync(), team => Assert.Null(team.DefaultMark));

        f.Project.Project.NameDefaultTeam(beta.Id);
        var named = await f.ListAsync();
        Assert.Equal("named", named.Single(team => team.Id == beta.Id).DefaultMark);
        Assert.Null(named.Single(team => team.Id == alpha.Id).DefaultMark);
    }

    [Fact]
    public async Task Handle_DeletedTeam_IsLeftOut()
    {
        var f = new Fixture();
        var gone = f.Store.AddLinkedTeam(f.Organization, f.Project, "Ушла");
        var stays = f.Store.AddLinkedTeam(f.Organization, f.Project, "Осталась");
        gone.Delete();

        Assert.Equal([stays.Id], (await f.ListAsync()).Select(team => team.Id));
    }

    // mod-016 BR-M1-27 · each team says whether the caller is in it, at read time.
    [Fact]
    public async Task Handle_EachTeamSaysWhetherTheCallerIsInIt()
    {
        var f = new Fixture();
        var alpha = f.Store.AddLinkedTeam(f.Organization, f.Project, "Альфа");
        var beta = f.Store.AddLinkedTeam(f.Organization, f.Project, "Бета");
        f.Store.AddMember(f.Organization, "u-caller");
        f.Store.AddTeamMember(f.Organization, beta, "u-caller");

        var teams = await f.ListAsync();

        Assert.False(teams.Single(team => team.Id == alpha.Id).IsMember);
        Assert.True(teams.Single(team => team.Id == beta.Id).IsMember);
    }

    // US-8 · the address answers by its level: a malformed organization, a malformed or unknown project, a deactivated project.
    [Theory]
    [InlineData("malformed organization", "organizations.organization.not_found")]
    [InlineData("malformed project", "organizations.project.not_found")]
    [InlineData("unknown project", "organizations.project.not_found")]
    [InlineData("deactivated project", "organizations.project.not_found")]
    public async Task Handle_AddressNotReachable_AnswersItsLevelsNotFound(string how, string code)
    {
        var f = new Fixture();
        var query = how switch
        {
            "malformed organization" => f.Query(organizationId: "abc"),
            "malformed project" => f.Query(projectId: "abc"),
            "unknown project" => f.Query(projectId: Guid.CreateVersion7().ToString()),
            _ => f.Query(),
        };

        if (how == "deactivated project")
        {
            f.Project.Project.Deactivate();
        }

        var refusal = await Assert.ThrowsAsync<NotFoundException>(() => f.ListAsync(query));

        Assert.Equal(code, refusal.Code);
    }
}
