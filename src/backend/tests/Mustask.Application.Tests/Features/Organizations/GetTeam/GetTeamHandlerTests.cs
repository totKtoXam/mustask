using Mustask.Application.Features.Organizations.GetTeam;
using Mustask.Application.Tests.TestDoubles.Organizations;
using Mustask.Domain.Organizations;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Tests.Features.Organizations.GetTeam;

/// <summary>
/// <c>GetTeam</c> on fakes (plan-backend § 14.3, L-16; mod-017 api-contract § 1.3): the team and its default mark, and the
/// order of the not-found answers, outermost level first (§ 5.3, api-contract § 0.3).
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
                new FakeTeamReadModel(Store));
        }

        public InMemoryOrganizations Store { get; } = new();

        public Organization Organization { get; }

        public NewProject Project { get; }

        public Team Platform { get; }

        public GetTeamHandler Handler { get; }

        public GetTeamQuery Query(string? teamId, string? projectId = null, string? organizationId = null) =>
            new(organizationId ?? Organization.Id.ToString(), projectId ?? Project.Project.Id.ToString(), teamId);

        public Task<GetTeamResult> HandleAsync(GetTeamQuery query) => Handler.Handle(query, CancellationToken.None);
    }

    // mod-017 US-4 · "No team is the first" (BR-OT-11): the team a project came with is read like any other — no mark when the
    // settings name none and there are two teams. Replaces Handle_FirstTeam_ReturnsItWithTheRootAsItsDefaultArea (D-16).
    [Fact]
    public async Task Handle_TheProjectsOwnTeam_ReturnsItLikeAnyOther()
    {
        var f = new Fixture();

        var result = await f.HandleAsync(f.Query(f.Project.Team.Id.ToString()));

        Assert.Equal(new TeamResponse(f.Project.Team.Id, f.Project.Project.Id, "Пример проекта", DefaultMark: null), result.Response);
    }

    // mod-017 BR-OT-4: the team the settings name carries «named»; the answer is the same for the single read as for the list,
    // except that «isMember» is always false here (api-contract § 1.3).
    [Fact]
    public async Task Handle_NamedTeam_ReturnsItMarkedNamed()
    {
        var f = new Fixture();
        f.Project.Project.NameDefaultTeam(f.Platform.Id);

        var result = await f.HandleAsync(f.Query(f.Platform.Id.ToString()));

        Assert.Equal(new TeamResponse(f.Platform.Id, f.Project.Project.Id, "Платформа", "named"), result.Response);
        Assert.False(result.Response.IsMember);
    }

    // mod-017 BR-OT-4: the only team of a project, with none named, carries «onlyTeam».
    [Fact]
    public async Task Handle_OnlyTeam_ReturnsItMarkedOnlyTeam()
    {
        var f = new Fixture();
        f.Platform.Delete(2);

        var result = await f.HandleAsync(f.Query(f.Project.Team.Id.ToString()));

        Assert.Equal("onlyTeam", result.Response.DefaultMark);
    }

    // US-4 · "A team is renamed": the new name is what the read shows.
    [Fact]
    public async Task Handle_RenamedTeam_ReturnsTheNewName()
    {
        var f = new Fixture();
        f.Platform.Rename(InMemoryOrganizations.Name("Платформа данных"));

        var result = await f.HandleAsync(f.Query(f.Platform.Id.ToString()));

        Assert.Equal("Платформа данных", result.Response.Name);
    }

    // api-contract § 0.2: the canonical form is accepted in either letter case.
    [Fact]
    public async Task Handle_IdsInUpperCase_FindsTheTeam()
    {
        var f = new Fixture();
        var query = new GetTeamQuery(
            f.Organization.Id.ToString().ToUpperInvariant(),
            f.Project.Project.Id.ToString().ToUpperInvariant(),
            f.Platform.Id.ToString().ToUpperInvariant());

        var result = await f.HandleAsync(query);

        Assert.Equal(f.Platform.Id, result.Response.Id);
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
        var teamId = how == "malformed" ? "not-a-team" : f.Platform.Id.ToString();

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
            "malformed" => f.Platform.Id.ToString("N"),
            "empty" => "",
            "unknown" => Guid.CreateVersion7().ToString(),
            "of another project" => secondProject.Team.Id.ToString(),
            "of another organization" => otherOrganization.Team.Id.ToString(),
            _ => f.Platform.Id.ToString(),
        };

        if (how == "deleted")
        {
            f.Platform.Delete(2);
        }

        var refusal = await Assert.ThrowsAsync<NotFoundException>(() => f.HandleAsync(f.Query(teamId)));

        Assert.Equal(OrganizationErrorCodes.TeamNotFound, refusal.Code);
    }

    // US-7 · "A deleted team answers not found" leaves the others alone: the project's own team is still read after a sibling
    // is deleted.
    [Fact]
    public async Task Handle_AfterATeamIsDeleted_TheOtherTeamsAreStillFound()
    {
        var f = new Fixture();
        f.Platform.Delete(2);

        var result = await f.HandleAsync(f.Query(f.Project.Team.Id.ToString()));

        Assert.Equal(f.Project.Team.Id, result.Response.Id);
    }
}
