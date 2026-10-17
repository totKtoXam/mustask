using Mustask.Application.Features.Organizations.RenameTeam;
using Mustask.Application.Tests.TestDoubles;
using Mustask.Application.Tests.TestDoubles.Organizations;
using Mustask.Domain.Organizations;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Tests.Features.Organizations.RenameTeam;

/// <summary>
/// <c>RenameTeam</c> on fakes (plan-backend § 14.3, lane L-18): what a rename
/// changes and leaves, the order of its answers (§ 5.3, api-contract § 0.4) and
/// its lock (§ 8.2). Copies the pattern of <c>CreateAreaHandlerTests</c>.
/// </summary>
public sealed class RenameTeamHandlerTests
{
    private sealed class Fixture
    {
        public Fixture()
        {
            Organization = Store.AddOrganization();
            Project = Store.AddProject(Organization);
            Platform = Store.AddTeam(Project);
            Handler = new RenameTeamHandler(
                new FakeProjectRepository(Store),
                new FakeTeamRepository(Store),
                new FakeUnitOfWork());
        }

        public InMemoryOrganizations Store { get; } = new();

        public Organization Organization { get; }

        public NewProject Project { get; }

        public Team Platform { get; }

        public RenameTeamHandler Handler { get; }

        public RenameTeamCommand Command(string? name = "Платформа данных", string? teamId = null, string? projectId = null, string? organizationId = null) =>
            new(
                organizationId ?? Organization.Id.ToString(),
                projectId ?? Project.Project.Id.ToString(),
                teamId ?? Platform.Id.ToString(),
                name);

        public Task<RenameTeamResult> HandleAsync(RenameTeamCommand command) => Handler.Handle(command, CancellationToken.None);

        /// <summary>Every area of the store as it is now: place and name.</summary>
        public List<(Guid Id, Guid? ParentId, string Name)> AreaSnapshot() =>
            Store.Areas
                .Select(area => (area.Id, area.ParentId, area.Name))
                .OrderBy(area => area.Id)
                .ToList();
    }

    // US-4 · "A team is renamed": «Платформа» becomes «Платформа данных», trimmed, and the answer is the team as every team
    // response shows it, with its default mark (mod-017 api-contract § 1.3): two teams and none named, so none carries a mark.
    // Renamed (D-16, BR-OT-11): Handle_FreeName_RenamesTheTeam_AnswersTheTeamWithItsDefaultArea -> ..._AnswersTheTeam.
    [Fact]
    public async Task Handle_FreeName_RenamesTheTeam_AnswersTheTeam()
    {
        var f = new Fixture();

        var result = await f.HandleAsync(f.Command(name: "  Платформа данных "));

        Assert.Equal(new TeamResponse(f.Platform.Id, f.Project.Project.Id, "Платформа данных", DefaultMark: null), result.Response);
        Assert.Equal("Платформа данных", f.Platform.Name);
        Assert.Equal("ПЛАТФОРМА ДАННЫХ", f.Platform.NormalizedName);
    }

    // mod-017 US-4 · "renaming a team touches no area" (BR-OT-10): the areas keep their names and places.
    // Renamed (D-16, BR-OT-10): Handle_KeepsAreaNamesOwnershipAndDefault -> Handle_TouchesNoArea.
    [Fact]
    public async Task Handle_TouchesNoArea()
    {
        var f = new Fixture();
        var api = f.Store.AddArea(f.Project.Root, "API");
        f.Store.AddArea(api, "Шлюз");
        var before = f.AreaSnapshot();

        await f.HandleAsync(f.Command(name: "Платформа данных"));

        Assert.Equal(before, f.AreaSnapshot());
    }

    // mod-017 US-4 · "any team renamed" (BR-OT-11): the team the project came with is renamed like any other; the project's
    // root keeps its own name (it is renamed with its project, § 2.5).
    // Replaces Handle_FirstTeam_IsRenamed_DefaultStaysTheRoot_RootKeepsItsName (D-16, BR-OT-11).
    [Fact]
    public async Task Handle_AnyTeam_IsRenamed_RootKeepsItsName()
    {
        var f = new Fixture();

        var result = await f.HandleAsync(f.Command(name: "Основная команда", teamId: f.Project.Team.Id.ToString()));

        Assert.Equal("Основная команда", result.Response.Name);
        Assert.Equal("Пример проекта", f.Project.Root.Name);
    }

    // mod-017 BR-OT-4: the answer carries the team's default mark — «named» when the project's settings name it, «onlyTeam» when
    // it is the project's only active team, else none.
    [Fact]
    public async Task Handle_AnswersTheDefaultMark_NamedOnlyTeamOrNone()
    {
        var f = new Fixture();
        f.Project.Project.NameDefaultTeam(f.Platform.Id);

        var named = await f.HandleAsync(f.Command(name: "Платформа данных"));
        var other = await f.HandleAsync(f.Command(name: "Основная", teamId: f.Project.Team.Id.ToString()));

        f.Project.Project.NameDefaultTeam(null);
        f.Project.Team.Delete(2);
        var only = await f.HandleAsync(f.Command(name: "Платформа"));

        Assert.Equal("named", named.Response.DefaultMark);
        Assert.Null(other.Response.DefaultMark);
        Assert.Equal("onlyTeam", only.Response.DefaultMark);
    }

    // BR-M1-1 · "a name never conflicts with the object's own current name": «ПЛАТФОРМА» renames «Платформа».
    [Fact]
    public async Task Handle_OwnNameInAnotherCase_IsAllowed()
    {
        var f = new Fixture();

        var result = await f.HandleAsync(f.Command(name: " ПЛАТФОРМА "));

        Assert.Equal("ПЛАТФОРМА", result.Response.Name);
        Assert.Equal("ПЛАТФОРМА", f.Platform.Name);
        Assert.Equal(1, f.Store.SaveCount);
    }

    // US-4 · "A team's name is unique among the project's active teams": «платформа » is taken by «Платформа» of the same
    // project, whichever case and spaces, and the answer is the pre-check's, before anything is written.
    [Fact]
    public async Task Handle_NameOfAnotherActiveTeamAnyCase_IsTeamNameTaken_NothingChanges()
    {
        var f = new Fixture();
        var analytics = f.Store.AddTeam(f.Project, "Аналитика");

        var refusal = await Assert.ThrowsAsync<ConflictException>(() => f.HandleAsync(
            f.Command(name: " аналитика ", teamId: f.Platform.Id.ToString())));

        Assert.Equal(OrganizationErrorCodes.TeamNameTaken, refusal.Code);
        Assert.Equal("Платформа", f.Platform.Name);
        Assert.Equal("Аналитика", analytics.Name);
        Assert.Equal(0, f.Store.SaveCount);
    }

    // US-4 · "«Платформа» is still free as a team name in another project".
    [Fact]
    public async Task Handle_NameOfATeamInAnotherProject_IsFree()
    {
        var f = new Fixture();
        var secondProject = f.Store.AddProject(f.Organization, "Второй проект", "SEC");
        f.Store.AddTeam(secondProject, "Аналитика");

        var result = await f.HandleAsync(f.Command(name: "Аналитика"));

        Assert.Equal("Аналитика", result.Response.Name);
    }

    // US-7 · "A deleted team's name is free": a deleted team does not hold its name, so another team is renamed to it.
    [Fact]
    public async Task Handle_NameOfDeletedTeam_IsFree()
    {
        var f = new Fixture();
        var core = f.Store.AddTeam(f.Project, "Ядро");
        core.Delete(3);

        var result = await f.HandleAsync(f.Command(name: "ядро"));

        Assert.Equal("ядро", result.Response.Name);
    }

    // US-4 · "an empty name, or one longer than 100 characters, is refused with «Название должно содержать от 1 до 100
    // символов»". The address is fine, so the team is locked; nothing is saved.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("101")]
    public async Task Handle_InvalidName_IsValidationFailedWithNameLength_BeforeTheUniquenessCheck(string? name)
    {
        var f = new Fixture();
        var submitted = name == "101" ? new string('я', 101) : name;

        var refusal = await Assert.ThrowsAsync<ValidationException>(() => f.HandleAsync(f.Command(name: submitted)));

        Assert.Equal(OrganizationErrorCodes.ValidationFailed, refusal.Code);
        Assert.Equal(["name"], refusal.Errors.Keys);
        Assert.Equal(OrganizationErrorCodes.NameLength, Assert.Single(refusal.Errors["name"]).Code);
        Assert.Equal("Платформа", f.Platform.Name);
        Assert.Equal(0, f.Store.SaveCount);
    }

    // US-4 · "a name of exactly 100 characters" is the longest that passes, after trimming.
    [Fact]
    public async Task Handle_NameOfOneHundredCharactersAfterTrimming_IsAccepted()
    {
        var f = new Fixture();
        var hundred = new string('я', 100);

        var result = await f.HandleAsync(f.Command(name: $"  {hundred}  "));

        Assert.Equal(hundred, result.Response.Name);
    }

    // BR-M1-20: behind the filter the organization id is well formed; if it were not, the outermost level answers.
    [Fact]
    public async Task Handle_MalformedOrganization_IsOrganizationNotFound()
    {
        var f = new Fixture();

        var refusal = await Assert.ThrowsAsync<NotFoundException>(() => f.HandleAsync(f.Command(organizationId: "abc", name: "")));

        Assert.Equal(OrganizationErrorCodes.OrganizationNotFound, refusal.Code);
        Assert.Empty(f.Store.Calls);
    }

    // US-3 · "A project id names no project of the organization" and "a project is deactivated" (BR-M1-20, BR-M1-22): the
    // project level answers before the team and the body are looked at, and nothing is locked.
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

        var refusal = await Assert.ThrowsAsync<NotFoundException>(() => f.HandleAsync(f.Command(projectId: projectId, name: "", teamId: "x")));

        Assert.Equal(OrganizationErrorCodes.ProjectNotFound, refusal.Code);
        Assert.Empty(f.Store.Calls);
        Assert.Equal("Платформа", f.Platform.Name);
    }

    // US-4 · "A team identifier that names no team of the project" (BR-M1-20, BR-M1-22) and US-7 · "A deleted team answers
    // not found": malformed, unknown, of another project, or deleted. The team answers before the body's name, which is
    // bad here too, and nothing is saved.
    [Theory]
    [InlineData("malformed")]
    [InlineData("empty")]
    [InlineData("unknown")]
    [InlineData("of another project")]
    [InlineData("deleted")]
    public async Task Handle_TeamNotAnActiveTeamOfTheProject_IsTeamNotFound_BeforeTheBody(string how)
    {
        var f = new Fixture();
        var secondProject = f.Store.AddProject(f.Organization, "Второй проект", "SEC");
        var core = f.Store.AddTeam(f.Project, "Ядро");
        var teamId = how switch
        {
            "malformed" => "{" + core.Id + "}",
            "empty" => "",
            "unknown" => Guid.CreateVersion7().ToString(),
            "of another project" => secondProject.Team.Id.ToString(),
            _ => core.Id.ToString(),
        };

        if (how == "deleted")
        {
            core.Delete(3);
        }

        var refusal = await Assert.ThrowsAsync<NotFoundException>(() => f.HandleAsync(f.Command(teamId: teamId, name: "")));

        Assert.Equal(OrganizationErrorCodes.TeamNotFound, refusal.Code);
        Assert.Equal(0, f.Store.SaveCount);
        Assert.Equal("Ядро", core.Name);
        Assert.Equal("Второй проект", secondProject.Team.Name);
    }

    // plan-backend § 8.2 "Rename team": the team FOR NO KEY UPDATE and nothing else, then one save. No area is locked, and
    // the project is not (a write beneath only reads its container's status, ADR-0026 rule 8).
    [Fact]
    public async Task Handle_LocksTheTeamForNoKeyUpdate_ThenSavesOnce_LocksNoAreaAndNoProject()
    {
        var f = new Fixture();

        await f.HandleAsync(f.Command());

        Assert.Equal([$"lock team no-key-update {f.Platform.Id}", "save"], f.Store.Calls);
    }

    // US-4 · "Two teams of one name at the same moment", the rename side: the loser's save fails on the team index, and the
    // repository's answer reaches the client unchanged (plan-backend § 6.3).
    [Fact]
    public async Task Handle_LosesTheNameRace_AnswersTeamNameTaken()
    {
        var f = new Fixture();
        f.Store.FailNextSave(new ConflictException(OrganizationErrorCodes.TeamNameTaken));

        var refusal = await Assert.ThrowsAsync<ConflictException>(() => f.HandleAsync(f.Command(name: "Аналитика")));

        Assert.Equal(OrganizationErrorCodes.TeamNameTaken, refusal.Code);
    }
}
