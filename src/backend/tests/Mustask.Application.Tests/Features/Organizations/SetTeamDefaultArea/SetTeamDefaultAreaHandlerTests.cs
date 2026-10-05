using Mustask.Application.Features.Organizations.SetTeamDefaultArea;
using Mustask.Application.Tests.TestDoubles;
using Mustask.Application.Tests.TestDoubles.Organizations;
using Mustask.Domain.Organizations;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Tests.Features.Organizations.SetTeamDefaultArea;

/// <summary>
/// <c>SetTeamDefaultArea</c> on fakes (plan-backend § 14.3, lane L-19): what it
/// changes, the order of its answers (§ 5.3, api-contract § 0.4) and its lock
/// order (§ 8.2). The races are the Api tests'.
/// </summary>
public sealed class SetTeamDefaultAreaHandlerTests
{
    private sealed class Fixture
    {
        public Fixture()
        {
            Organization = Store.AddOrganization();
            Project = Store.AddProject(Organization);

            // The team «Платформа» owns «Платформа» (its default) and «Платформа / API» explicitly,
            // and «Платформа / API / Шлюз» only by inheritance (US-4).
            Platform = Store.AddTeam(Project);
            Api = Store.AddArea(Platform.Area, "API", Platform.Team);
            Gateway = Store.AddArea(Api, "Шлюз");

            Handler = new SetTeamDefaultAreaHandler(
                new FakeProjectRepository(Store),
                new FakeTeamRepository(Store),
                new FakeAreaRepository(Store),
                new FakeUnitOfWork());
        }

        public InMemoryOrganizations Store { get; } = new();

        public Organization Organization { get; }

        public NewProject Project { get; }

        public NewTeam Platform { get; }

        public Area Api { get; }

        public Area Gateway { get; }

        public SetTeamDefaultAreaHandler Handler { get; }

        public SetTeamDefaultAreaCommand Command(string? areaId, string? teamId = null, string? projectId = null, string? organizationId = null) =>
            new(
                organizationId ?? Organization.Id.ToString(),
                projectId ?? Project.Project.Id.ToString(),
                teamId ?? Platform.Team.Id.ToString(),
                areaId);

        public Task<SetTeamDefaultAreaResult> HandleAsync(SetTeamDefaultAreaCommand command) => Handler.Handle(command, CancellationToken.None);
    }

    // US-4 · "A team's default area is chosen among the areas it owns explicitly": «Платформа / API» becomes the default;
    // the old default keeps its owner.
    [Fact]
    public async Task Handle_ExplicitlyOwnedArea_MovesTheMark_ReturnsTheTeamWithThePath()
    {
        var f = new Fixture();

        var result = await f.HandleAsync(f.Command(f.Api.Id.ToString()));

        Assert.Equal(
            new TeamResponse(
                f.Platform.Team.Id,
                f.Project.Project.Id,
                "Платформа",
                IsFirstTeam: false,
                new TeamDefaultAreaResponse(f.Api.Id, "Платформа / API")),
            result.Response);
        Assert.Equal(f.Platform.Team.Id, f.Api.DefaultOfTeamId);
        Assert.Null(f.Platform.Area.DefaultOfTeamId);
        Assert.Equal(f.Platform.Team.Id, f.Platform.Area.OwnerTeamId);
        Assert.Equal(f.Platform.Team.Id, f.Api.OwnerTeamId);
        Assert.Single(f.Store.Areas, area => area.DefaultOfTeamId == f.Platform.Team.Id);
    }

    // US-4 · the ids come in either letter case (api-contract § 0.2), and the default can go back to the team's first area.
    [Fact]
    public async Task Handle_UpperCaseIds_AndBackToTheFirstArea_Work()
    {
        var f = new Fixture();

        await f.HandleAsync(f.Command(f.Api.Id.ToString().ToUpperInvariant(), teamId: f.Platform.Team.Id.ToString().ToUpperInvariant()));
        var back = await f.HandleAsync(f.Command(f.Platform.Area.Id.ToString()));

        Assert.Equal(new TeamDefaultAreaResponse(f.Platform.Area.Id, "Платформа"), back.Response.DefaultArea);
        Assert.Equal(f.Platform.Team.Id, f.Platform.Area.DefaultOfTeamId);
        Assert.Null(f.Api.DefaultOfTeamId);
    }

    // api-contract § 3.5: "Setting the current default again answers 200 and changes nothing."
    [Fact]
    public async Task Handle_CurrentDefaultAgain_Returns200WithTheSameDefault_SavesNothing()
    {
        var f = new Fixture();

        var result = await f.HandleAsync(f.Command(f.Platform.Area.Id.ToString()));

        Assert.Equal(new TeamDefaultAreaResponse(f.Platform.Area.Id, "Платформа"), result.Response.DefaultArea);
        Assert.Equal(f.Platform.Team.Id, f.Platform.Area.DefaultOfTeamId);
        Assert.Equal(0, f.Store.SaveCount);
    }

    // US-4 · "setting, by a direct request, an area the team does not own explicitly is refused with «Область по умолчанию
    // должна принадлежать самой команде», and the default is unchanged": inherited, owned by another team, or the root.
    [Theory]
    [InlineData("inherited")]
    [InlineData("of another team")]
    [InlineData("the root")]
    public async Task Handle_AreaNotOwnedExplicitly_IsDefaultAreaNotOwned_NothingChanges(string how)
    {
        var f = new Fixture();
        var core = f.Store.AddTeam(f.Project, "Ядро");
        var areaId = how switch
        {
            "inherited" => f.Gateway.Id,
            "of another team" => core.Area.Id,
            _ => f.Project.Root.Id,
        };

        var refusal = await Assert.ThrowsAsync<ConflictException>(() => f.HandleAsync(f.Command(areaId.ToString())));

        Assert.Equal(OrganizationErrorCodes.TeamDefaultAreaNotOwned, refusal.Code);
        Assert.Equal(f.Platform.Team.Id, f.Platform.Area.DefaultOfTeamId);
        Assert.Null(f.Api.DefaultOfTeamId);
        Assert.Equal(core.Team.Id, core.Area.DefaultOfTeamId);
        Assert.Equal(f.Project.FirstTeam.Id, f.Project.Root.DefaultOfTeamId);
        Assert.Equal(0, f.Store.SaveCount);
    }

    // US-4 · "The first team's default is the root, for good ... a direct request to change it is refused": a rule about the
    // addressed team alone, so it answers before the body's area is looked at (api-contract § 0.4 step 2 before 4).
    [Theory]
    [InlineData("its current default")]
    [InlineData("another area it owns")]
    [InlineData("unknown")]
    [InlineData("malformed")]
    [InlineData("null")]
    public async Task Handle_FirstTeam_IsFirstTeamDefaultFixed_BeforeTheArea(string how)
    {
        var f = new Fixture();
        var reports = f.Store.AddArea(f.Project.Root, "Отчёты", f.Project.FirstTeam);
        var areaId = how switch
        {
            "its current default" => f.Project.Root.Id.ToString(),
            "another area it owns" => reports.Id.ToString(),
            "unknown" => Guid.CreateVersion7().ToString(),
            "malformed" => "not-an-area",
            _ => null,
        };

        var refusal = await Assert.ThrowsAsync<ConflictException>(() => f.HandleAsync(
            f.Command(areaId, teamId: f.Project.FirstTeam.Id.ToString())));

        Assert.Equal(OrganizationErrorCodes.TeamFirstTeamDefaultFixed, refusal.Code);
        Assert.Equal([$"lock team no-key-update {f.Project.FirstTeam.Id}"], f.Store.Calls);
        Assert.Equal(f.Project.FirstTeam.Id, f.Project.Root.DefaultOfTeamId);
        Assert.Null(reports.DefaultOfTeamId);
    }

    // BR-M1-22 · "an area of the team's own project": null, malformed, unknown, or an area of another project answers
    // area.not_found, before any area is locked, and nothing changes in either project.
    [Theory]
    [InlineData("null")]
    [InlineData("malformed")]
    [InlineData("unknown")]
    [InlineData("of another project")]
    public async Task Handle_AreaNotAnAreaOfTheProject_IsAreaNotFound_NothingLockedNothingChanged(string how)
    {
        var f = new Fixture();
        var other = f.Store.AddProject(f.Organization, "Второй проект", "SEC");
        var areaId = how switch
        {
            "null" => null,
            "malformed" => "00000000-0000-0000-0000-000000000000",
            "unknown" => Guid.CreateVersion7().ToString(),
            _ => other.Root.Id.ToString(),
        };

        var refusal = await Assert.ThrowsAsync<NotFoundException>(() => f.HandleAsync(f.Command(areaId)));

        Assert.Equal(OrganizationErrorCodes.AreaNotFound, refusal.Code);
        Assert.Equal([$"lock team no-key-update {f.Platform.Team.Id}"], f.Store.Calls);
        Assert.Equal(f.Platform.Team.Id, f.Platform.Area.DefaultOfTeamId);
        Assert.Equal(other.FirstTeam.Id, other.Root.DefaultOfTeamId);
    }

    // US-4 · "A team identifier that names no team of the project" and US-7 · "A deleted team answers not found" (BR-M1-20,
    // BR-M1-22): the team level answers before the body; the area is wrong too.
    [Theory]
    [InlineData("malformed")]
    [InlineData("unknown")]
    [InlineData("of another project")]
    [InlineData("deleted")]
    public async Task Handle_TeamNotAnActiveTeamOfTheProject_IsTeamNotFound_BeforeTheArea(string how)
    {
        var f = new Fixture();
        var other = f.Store.AddProject(f.Organization, "Второй проект", "SEC");
        var teamId = how switch
        {
            "malformed" => "{" + f.Platform.Team.Id + "}",
            "unknown" => Guid.CreateVersion7().ToString(),
            "of another project" => other.FirstTeam.Id.ToString(),
            _ => f.Platform.Team.Id.ToString(),
        };

        if (how == "deleted")
        {
            f.Platform.Team.Delete();
        }

        var refusal = await Assert.ThrowsAsync<NotFoundException>(() => f.HandleAsync(f.Command("not-an-area", teamId: teamId)));

        Assert.Equal(OrganizationErrorCodes.TeamNotFound, refusal.Code);
        Assert.DoesNotContain(f.Store.Calls, call => call.StartsWith("lock area", StringComparison.Ordinal));
        Assert.Equal(0, f.Store.SaveCount);
    }

    // BR-M1-20 · US-3 · "A project id names no project of the organization" and "a project is deactivated": the project level
    // answers before the team and the body are looked at, and nothing is locked.
    [Theory]
    [InlineData("malformed")]
    [InlineData("unknown")]
    [InlineData("of another organization")]
    [InlineData("deactivated")]
    [InlineData("under a deactivated organization")]
    public async Task Handle_ProjectNotReachable_IsProjectNotFound_BeforeTheTeamAndTheBody(string how)
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

        var refusal = await Assert.ThrowsAsync<NotFoundException>(() => f.HandleAsync(
            f.Command("not-an-area", teamId: "not-a-team", projectId: projectId)));

        Assert.Equal(OrganizationErrorCodes.ProjectNotFound, refusal.Code);
        Assert.Empty(f.Store.Calls);
    }

    // BR-M1-20: behind the filter the organization id is well formed; if it were not, the outermost level answers.
    [Fact]
    public async Task Handle_MalformedOrganization_IsOrganizationNotFound()
    {
        var f = new Fixture();

        var refusal = await Assert.ThrowsAsync<NotFoundException>(() => f.HandleAsync(f.Command("not-an-area", organizationId: "abc")));

        Assert.Equal(OrganizationErrorCodes.OrganizationNotFound, refusal.Code);
        Assert.Empty(f.Store.Calls);
    }

    // plan-backend § 8.2: the team FOR NO KEY UPDATE, then the new area and the current default FOR UPDATE in ascending id
    // order (one statement), then the two flushes: the old mark cleared, the new one set.
    [Fact]
    public async Task Handle_LocksTheTeamThenBothAreasInAscendingIdOrder_ThenSavesTwice()
    {
        var f = new Fixture();
        var ascending = new[] { f.Api.Id, f.Platform.Area.Id }.Order().ToArray();

        await f.HandleAsync(f.Command(f.Api.Id.ToString()));

        Assert.Equal(
            [
                $"lock team no-key-update {f.Platform.Team.Id}",
                $"lock area update {ascending[0]}",
                $"lock area update {ascending[1]}",
                "save",
                "save",
            ],
            f.Store.Calls);
    }

    // plan-backend § 8.2 "Set-default flushes twice": the unique index of the default is checked row by row, so the old mark
    // is cleared and flushed before the new one is set. A first flush that fails leaves the new area unmarked.
    [Fact]
    public async Task Handle_FirstFlushFails_TheNewAreaIsNotMarked()
    {
        var f = new Fixture();
        f.Store.FailNextSave(new InvalidOperationException("The database refused the first flush."));

        await Assert.ThrowsAsync<InvalidOperationException>(() => f.HandleAsync(f.Command(f.Api.Id.ToString())));

        Assert.Equal(1, f.Store.SaveCount);
        Assert.Null(f.Api.DefaultOfTeamId);
    }
}
