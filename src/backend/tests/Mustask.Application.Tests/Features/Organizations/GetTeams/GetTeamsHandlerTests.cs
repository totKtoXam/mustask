using Mustask.Application.Features.Organizations.GetTeams;
using Mustask.Application.Tests.TestDoubles;
using Mustask.Application.Tests.TestDoubles.Organizations;
using Mustask.Domain.Organizations;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Tests.Features.Organizations.GetTeams;

/// <summary>
/// <c>GetTeams</c> on fakes (plan-backend § 14.3, lane L-15): the first team
/// first, the default areas' paths, the deleted and the unreachable left out,
/// and the order of answers of a read (§ 5.3).
/// </summary>
public sealed class GetTeamsHandlerTests
{
    private sealed class Fixture
    {
        public Fixture()
        {
            Organization = Store.AddOrganization();
            Project = Store.AddProject(Organization);
            Platform = Store.AddTeam(Project);
            Handler = new GetTeamsHandler(new FakeTeamReadModel(Store), new FakeAreaTreeReadModel(Store), User);
        }

        /// <summary>The acting user: a member of the organization, in no team until a test says so.</summary>
        public FakeCurrentUser User { get; } = new() { SubjectId = "4c1f0e6a-0000-4000-8000-00000000a11a" };

        public InMemoryOrganizations Store { get; } = new();

        public Organization Organization { get; }

        public NewProject Project { get; }

        public NewTeam Platform { get; }

        public GetTeamsHandler Handler { get; }

        public GetTeamsQuery Query(string? organizationId = null, string? projectId = null) =>
            new(organizationId ?? Organization.Id.ToString(), projectId ?? Project.Project.Id.ToString());

        public Task<GetTeamsResult> HandleAsync(GetTeamsQuery query) => Handler.Handle(query, CancellationToken.None);

        /// <summary>A team's default area moves to an area it owns explicitly, as US-4 "chosen among the areas it owns explicitly" does.</summary>
        public void MoveDefault(NewTeam team, Area next)
        {
            var current = Store.Areas.Single(area => area.DefaultOfTeamId == team.Team.Id);

            Assert.True(TeamDefaultArea.RequiresChange(team.Team, current, next));
            current.ClearDefault();
            next.MarkDefaultOf(team.Team);
        }

        /// <summary>A team as the list shows it, with its own area under the root as its default.</summary>
        public static TeamResponse Listed(Guid projectId, NewTeam team) =>
            new(team.Team.Id, projectId, team.Team.Name, false, new TeamDefaultAreaResponse(team.Area.Id, team.Area.Name));
    }

    // US-4 · "The project's teams": every active team is listed with its default area; US-8 · "The switcher lists ... the
    // teams": «Пример проекта» first, then the rest in alphabetical order, although «Платформа» sorts before it by name.
    [Fact]
    public async Task Handle_ActiveTeams_FirstTeamFirstThenDisplayOrder_WithDefaultAreaPaths()
    {
        var f = new Fixture();
        var projectId = f.Project.Project.Id;
        var beta = f.Store.AddTeam(f.Project, "Бета");
        var alpha = f.Store.AddTeam(f.Project, "альфа");
        var elsewhere = f.Store.AddProject(f.Organization, "Аналитика", "ANL");
        f.Store.AddTeam(elsewhere, "Ядро");

        // «Платформа» chooses «Платформа / API» as its default area, which it owns explicitly.
        var api = f.Store.AddArea(f.Platform.Area, "API", owner: f.Platform.Team);
        f.MoveDefault(f.Platform, api);

        var result = await f.HandleAsync(f.Query());

        Assert.Equal(
            [
                new TeamResponse(f.Project.FirstTeam.Id, projectId, "Пример проекта", true, new TeamDefaultAreaResponse(f.Project.Root.Id, "Пример проекта")),
                Fixture.Listed(projectId, alpha),
                Fixture.Listed(projectId, beta),
                new TeamResponse(f.Platform.Team.Id, projectId, "Платформа", false, new TeamDefaultAreaResponse(api.Id, "Платформа / API")),
            ],
            result.Items);
    }

    // US-8 · "The default team is the project's first team": the first team owns the root, and its default area is the root,
    // whose path is its own name (DC-9). A project that has just been created lists that team alone (US-3).
    [Fact]
    public async Task Handle_NewProject_ListsTheFirstTeamAlone_DefaultAreaIsTheRoot()
    {
        var f = new Fixture();
        var created = f.Store.AddProject(f.Organization, "Аналитика", "ANL");

        var result = await f.HandleAsync(f.Query(projectId: created.Project.Id.ToString()));

        var only = Assert.Single(result.Items);
        Assert.Equal(
            new TeamResponse(created.FirstTeam.Id, created.Project.Id, "Аналитика", true, new TeamDefaultAreaResponse(created.Root.Id, "Аналитика")),
            only);
    }

    // US-8 · "The switcher lists ... «Пример проекта» first, then «Платформа», and not the deleted team" (BR-M1-18).
    [Fact]
    public async Task Handle_DeletedTeam_IsLeftOut()
    {
        var f = new Fixture();
        var core = f.Store.AddTeam(f.Project, "Ядро");
        core.Team.Delete();

        var result = await f.HandleAsync(f.Query());

        Assert.Equal(["Пример проекта", "Платформа"], result.Items.Select(team => team.Name));
        Assert.DoesNotContain(result.Items, team => team.Id == core.Team.Id);
    }

    // mod-016 US-7 · "Choosing a project selects the user's own team" (BR-M1-27, D-5): each team says whether the CALLER is
    // in it, in the list's order; another user's membership does not count.
    [Fact]
    public async Task Handle_EachTeamSaysWhetherTheCallerIsInIt_InTheListsOrder()
    {
        var f = new Fixture();
        var analytics = f.Store.AddTeam(f.Project, "Аналитика");
        f.Store.AddMember(f.Organization, f.User.SubjectId);
        f.Store.AddMember(f.Organization, "someone-else");
        f.Store.AddTeamMember(f.Organization, f.Platform.Team, f.User.SubjectId);
        f.Store.AddTeamMember(f.Organization, f.Project.FirstTeam, "someone-else");

        var result = await f.HandleAsync(f.Query());

        Assert.Equal(
            [("Пример проекта", false), ("Аналитика", false), ("Платформа", true)],
            result.Items.Select(team => (team.Name, team.IsMember)));
        Assert.NotEqual(Guid.Empty, analytics.Team.Id);
    }

    // mod-016 US-7 · "Several teams — the first of them in the project's order": the flag is set on every team the caller is in.
    [Fact]
    public async Task Handle_CallerInSeveralTeams_EveryOneOfThemIsMarked()
    {
        var f = new Fixture();
        var analytics = f.Store.AddTeam(f.Project, "Аналитика");
        f.Store.AddMember(f.Organization, f.User.SubjectId);
        f.Store.AddTeamMember(f.Organization, f.Platform.Team, f.User.SubjectId);
        f.Store.AddTeamMember(f.Organization, analytics.Team, f.User.SubjectId);

        var result = await f.HandleAsync(f.Query());

        Assert.Equal(["Аналитика", "Платформа"], result.Items.Where(team => team.IsMember).Select(team => team.Name));
    }

    // mod-016 US-7 · "In no team of the project — the project's first team": no team is marked, so the panel falls back.
    [Fact]
    public async Task Handle_CallerInNoTeam_NoTeamIsMarked()
    {
        var f = new Fixture();
        f.Store.AddMember(f.Organization, f.User.SubjectId);

        var result = await f.HandleAsync(f.Query());

        Assert.All(result.Items, team => Assert.False(team.IsMember));
    }

    // mod-016 BR-M1-27 · "a change to them counts from the next time that list is read": the flag follows the memberships
    // at read time, nothing is cached.
    [Fact]
    public async Task Handle_TheFlagFollowsTheMembershipsAtReadTime()
    {
        var f = new Fixture();
        f.Store.AddMember(f.Organization, f.User.SubjectId);

        var before = await f.HandleAsync(f.Query());
        f.Store.AddTeamMember(f.Organization, f.Platform.Team, f.User.SubjectId);
        var added = await f.HandleAsync(f.Query());
        f.Store.TeamMemberships.Clear();
        var removed = await f.HandleAsync(f.Query());

        Assert.DoesNotContain(before.Items, team => team.IsMember);
        Assert.Equal(["Платформа"], added.Items.Where(team => team.IsMember).Select(team => team.Name));
        Assert.DoesNotContain(removed.Items, team => team.IsMember);
    }

    // BR-M1-20: behind the filter the organization id is well formed; if it were not, the outermost level answers.
    [Fact]
    public async Task Handle_MalformedOrganization_IsOrganizationNotFound_BeforeTheProject()
    {
        var f = new Fixture();

        var refusal = await Assert.ThrowsAsync<NotFoundException>(() => f.HandleAsync(f.Query(organizationId: "abc", projectId: "xyz")));

        Assert.Equal(OrganizationErrorCodes.OrganizationNotFound, refusal.Code);
    }

    // US-3 · "A project id names no project of the organization" and "a project is deactivated" (BR-M1-20, BR-M1-22,
    // ADR-0026 rule 5): nothing about a project that is malformed, unknown, of another organization, deactivated or
    // beneath a deactivated organization is revealed.
    [Theory]
    [InlineData("null")]
    [InlineData("malformed")]
    [InlineData("unknown")]
    [InlineData("of another organization")]
    [InlineData("deactivated")]
    [InlineData("under a deactivated organization")]
    public async Task Handle_ProjectNotReachable_IsProjectNotFound(string how)
    {
        var f = new Fixture();
        var other = f.Store.AddProject(f.Store.AddOrganization("Другая организация"), "Чужой", "OTH");
        var query = how switch
        {
            "null" => new GetTeamsQuery(f.Organization.Id.ToString(), null),
            "malformed" => f.Query(projectId: "not-an-id"),
            "unknown" => f.Query(projectId: Guid.CreateVersion7().ToString()),
            "of another organization" => f.Query(projectId: other.Project.Id.ToString()),
            _ => f.Query(),
        };

        if (how == "deactivated")
        {
            f.Project.Project.Deactivate();
        }

        if (how == "under a deactivated organization")
        {
            f.Organization.Deactivate();
        }

        var refusal = await Assert.ThrowsAsync<NotFoundException>(() => f.HandleAsync(query));

        Assert.Equal(OrganizationErrorCodes.ProjectNotFound, refusal.Code);
    }
}
