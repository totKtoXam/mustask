using Mustask.Application.Features.Organizations.GetTeam;
using Mustask.Application.Tests.TestDoubles.Organizations;
using Mustask.Domain.Organizations;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Tests.Features.Organizations.GetTeam;

/// <summary>
/// <c>GetTeam</c> on fakes (plan-backend § 14.3, L-16): the team and its default
/// area's path, and the order of the not-found answers, outermost level first
/// (§ 5.3, api-contract § 0.3).
/// </summary>
public sealed class GetTeamHandlerTests
{
    private sealed class Fixture
    {
        public Fixture()
        {
            Organization = Store.AddOrganization();
            Project = Store.AddProject(Organization);
            Platform = Store.AddTeam(Project);
            Handler = new GetTeamHandler(
                new FakeProjectReadModel(Store),
                new FakeTeamReadModel(Store),
                new FakeAreaTreeReadModel(Store));
        }

        public InMemoryOrganizations Store { get; } = new();

        public Organization Organization { get; }

        public NewProject Project { get; }

        public NewTeam Platform { get; }

        public GetTeamHandler Handler { get; }

        public GetTeamQuery Query(string? teamId, string? projectId = null, string? organizationId = null) =>
            new(organizationId ?? Organization.Id.ToString(), projectId ?? Project.Project.Id.ToString(), teamId);

        public Task<GetTeamResult> HandleAsync(GetTeamQuery query) => Handler.Handle(query, CancellationToken.None);
    }

    // US-4 · "The first team's default is the root, for good": «Пример проекта» is marked as the team that owns the root,
    // and its default area is the root, whose path is its own name (DC-9).
    [Fact]
    public async Task Handle_FirstTeam_ReturnsItWithTheRootAsItsDefaultArea()
    {
        var f = new Fixture();

        var result = await f.HandleAsync(f.Query(f.Project.FirstTeam.Id.ToString()));

        Assert.Equal(
            new TeamResponse(
                f.Project.FirstTeam.Id,
                f.Project.Project.Id,
                "Пример проекта",
                IsFirstTeam: true,
                new TeamDefaultAreaResponse(f.Project.Root.Id, "Пример проекта")),
            result.Response);
    }

    // US-4 · "A second team comes with its own area": «Платформа» is not the first team, and its default is its own child
    // of the root, so the path is the names below the root.
    [Fact]
    public async Task Handle_SecondTeam_ReturnsItWithItsOwnAreaAsItsDefaultArea()
    {
        var f = new Fixture();

        var result = await f.HandleAsync(f.Query(f.Platform.Team.Id.ToString()));

        Assert.Equal(
            new TeamResponse(
                f.Platform.Team.Id,
                f.Project.Project.Id,
                "Платформа",
                IsFirstTeam: false,
                new TeamDefaultAreaResponse(f.Platform.Area.Id, "Платформа")),
            result.Response);
    }

    // US-4 · "A team's default area is chosen among the areas it owns explicitly": «Платформа / API» as the default shows
    // its path below the root, and not «Платформа».
    [Fact]
    public async Task Handle_DefaultAreaBelowTheRoot_ReturnsThePathBelowTheRoot()
    {
        var f = new Fixture();
        var reports = f.Store.AddArea(f.Project.Root, "Отчёты");
        var api = f.Store.AddArea(reports, "API", f.Platform.Team);
        f.Platform.Area.ClearDefault();
        api.MarkDefaultOf(f.Platform.Team);

        var result = await f.HandleAsync(f.Query(f.Platform.Team.Id.ToString()));

        Assert.Equal(new TeamDefaultAreaResponse(api.Id, "Отчёты / API"), result.Response.DefaultArea);
    }

    // US-4 · "A team is renamed": the new name is what the read shows, and the default area is unchanged.
    [Fact]
    public async Task Handle_RenamedTeam_ReturnsTheNewNameAndTheSameDefaultArea()
    {
        var f = new Fixture();
        f.Platform.Team.Rename(InMemoryOrganizations.Name("Платформа данных"));

        var result = await f.HandleAsync(f.Query(f.Platform.Team.Id.ToString()));

        Assert.Equal("Платформа данных", result.Response.Name);
        Assert.Equal(new TeamDefaultAreaResponse(f.Platform.Area.Id, "Платформа"), result.Response.DefaultArea);
    }

    // api-contract § 0.2: the canonical form is accepted in either letter case.
    [Fact]
    public async Task Handle_IdsInUpperCase_FindsTheTeam()
    {
        var f = new Fixture();
        var query = new GetTeamQuery(
            f.Organization.Id.ToString().ToUpperInvariant(),
            f.Project.Project.Id.ToString().ToUpperInvariant(),
            f.Platform.Team.Id.ToString().ToUpperInvariant());

        var result = await f.HandleAsync(query);

        Assert.Equal(f.Platform.Team.Id, result.Response.Id);
    }

    // BR-M1-20: behind the filter the organization id is well formed; if it were not, the outermost level answers.
    [Fact]
    public async Task Handle_MalformedOrganization_IsOrganizationNotFound_BeforeTheOtherLevels()
    {
        var f = new Fixture();

        var refusal = await Assert.ThrowsAsync<NotFoundException>(() => f.HandleAsync(
            f.Query(teamId: "not-a-team", projectId: "not-a-project", organizationId: "abc")));

        Assert.Equal(OrganizationErrorCodes.OrganizationNotFound, refusal.Code);
    }

    // US-3 · "A project id names no project of the organization" and "a project is deactivated" (BR-M1-20, BR-M1-22,
    // ADR-0026 rule 5): the project level answers before the team is looked at, even when the team is wrong too.
    [Theory]
    [InlineData("malformed")]
    [InlineData("unknown")]
    [InlineData("of another organization")]
    [InlineData("deactivated")]
    [InlineData("under a deactivated organization")]
    public async Task Handle_ProjectNotReachable_IsProjectNotFound_BeforeTheTeam(string how)
    {
        var f = new Fixture();
        var other = f.Store.AddProject(f.Store.AddOrganization("Другая организация"), "Чужой", "OTH");
        var projectId = how switch
        {
            "malformed" => "not-an-id",
            "unknown" => Guid.CreateVersion7().ToString(),
            "of another organization" => other.Project.Id.ToString(),
            _ => f.Project.Project.Id.ToString(),
        };

        if (how == "deactivated")
        {
            f.Project.Project.Deactivate();
        }

        if (how == "under a deactivated organization")
        {
            f.Organization.Deactivate();
        }

        // The team is wrong too in the first case, and is a real team of the project in the others.
        var teamId = how == "malformed" ? "not-a-team" : f.Platform.Team.Id.ToString();

        var refusal = await Assert.ThrowsAsync<NotFoundException>(() => f.HandleAsync(f.Query(teamId, projectId)));

        Assert.Equal(OrganizationErrorCodes.ProjectNotFound, refusal.Code);
    }

    // US-4 · "A team identifier that names no team of the project" (BR-M1-20, BR-M1-22) and US-7 · "A deleted team answers
    // not found": malformed, absent, unknown, the team of another project or organization, and a deleted one.
    [Theory]
    [InlineData("null")]
    [InlineData("malformed")]
    [InlineData("empty")]
    [InlineData("unknown")]
    [InlineData("of another project")]
    [InlineData("of another organization")]
    [InlineData("deleted")]
    public async Task Handle_TeamNotAnActiveTeamOfTheProject_IsTeamNotFound(string how)
    {
        var f = new Fixture();
        var secondProject = f.Store.AddProject(f.Organization, "Второй проект", "SEC");
        var otherOrganization = f.Store.AddProject(f.Store.AddOrganization("Другая организация"), "Чужой", "OTH");
        var teamId = how switch
        {
            "null" => null,
            "malformed" => f.Platform.Team.Id.ToString("N"),
            "empty" => "",
            "unknown" => Guid.CreateVersion7().ToString(),
            "of another project" => secondProject.FirstTeam.Id.ToString(),
            "of another organization" => otherOrganization.FirstTeam.Id.ToString(),
            _ => f.Platform.Team.Id.ToString(),
        };

        if (how == "deleted")
        {
            f.Platform.Team.Delete();
        }

        var refusal = await Assert.ThrowsAsync<NotFoundException>(() => f.HandleAsync(f.Query(teamId)));

        Assert.Equal(OrganizationErrorCodes.TeamNotFound, refusal.Code);
    }

    // US-7 · "A deleted team answers not found" leaves the others alone: the first team is still read after a sibling is deleted.
    [Fact]
    public async Task Handle_AfterATeamIsDeleted_TheOtherTeamsAreStillFound()
    {
        var f = new Fixture();
        f.Platform.Team.Delete();

        var result = await f.HandleAsync(f.Query(f.Project.FirstTeam.Id.ToString()));

        Assert.True(result.Response.IsFirstTeam);
        Assert.Equal(f.Project.Root.Id, result.Response.DefaultArea.Id);
    }
}
