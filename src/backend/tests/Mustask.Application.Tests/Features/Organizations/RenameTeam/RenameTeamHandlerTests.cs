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
                new FakeAreaRepository(Store),
                new FakeUnitOfWork());
        }

        public InMemoryOrganizations Store { get; } = new();

        public Organization Organization { get; }

        public NewProject Project { get; }

        public NewTeam Platform { get; }

        public RenameTeamHandler Handler { get; }

        public RenameTeamCommand Command(string? name = "Платформа данных", string? teamId = null, string? projectId = null, string? organizationId = null) =>
            new(
                organizationId ?? Organization.Id.ToString(),
                projectId ?? Project.Project.Id.ToString(),
                teamId ?? Platform.Team.Id.ToString(),
                name);

        public Task<RenameTeamResult> HandleAsync(RenameTeamCommand command) => Handler.Handle(command, CancellationToken.None);

        /// <summary>Every area of the store as it is now: place, name, explicit owner and default mark.</summary>
        public List<(Guid Id, Guid? ParentId, string Name, Guid? OwnerTeamId, Guid? DefaultOfTeamId)> AreaSnapshot() =>
            Store.Areas
                .Select(area => (area.Id, area.ParentId, area.Name, area.OwnerTeamId, area.DefaultOfTeamId))
                .OrderBy(area => area.Id)
                .ToList();
    }

    // US-4 · "A team is renamed": «Платформа» becomes «Платформа данных», trimmed, and the answer is the team as every team
    // response shows it, with its default area and that area's path (api-contract § 3).
    [Fact]
    public async Task Handle_FreeName_RenamesTheTeam_AnswersTheTeamWithItsDefaultArea()
    {
        var f = new Fixture();

        var result = await f.HandleAsync(f.Command(name: "  Платформа данных "));

        Assert.Equal(
            new TeamResponse(
                f.Platform.Team.Id,
                f.Project.Project.Id,
                "Платформа данных",
                IsFirstTeam: false,
                new TeamDefaultAreaResponse(f.Platform.Area.Id, "Платформа")),
            result.Response);
        Assert.Equal("Платформа данных", f.Platform.Team.Name);
        Assert.Equal("ПЛАТФОРМА ДАННЫХ", f.Platform.Team.NormalizedName);
    }

    // US-4 · "A team is renamed": its areas keep their names, and its ownership and default area are unchanged (BR-M1-9).
    // «Платформа» owns «Платформа» (its default) and «Платформа / API» explicitly, and «Платформа / API / Шлюз» by inheritance.
    [Fact]
    public async Task Handle_KeepsAreaNamesOwnershipAndDefault()
    {
        var f = new Fixture();
        var api = f.Store.AddArea(f.Platform.Area, "API", f.Platform.Team);
        f.Store.AddArea(api, "Шлюз");
        var before = f.AreaSnapshot();

        var result = await f.HandleAsync(f.Command(name: "Платформа данных"));

        Assert.Equal(before, f.AreaSnapshot());
        Assert.Equal(f.Platform.Area.Id, result.Response.DefaultArea.Id);
        Assert.Equal("Платформа", result.Response.DefaultArea.Path);
        Assert.Equal(f.Platform.Team.Id, f.Platform.Area.DefaultOfTeamId);
        Assert.Equal(f.Platform.Team.Id, f.Platform.Area.OwnerTeamId);
    }

    // US-4 · "The first team's default is the root, for good" is not a reason to refuse a rename: the first team is renamed
    // like any other, its default area stays the root, and the root keeps its own name (it is renamed with its project, § 2.5).
    [Fact]
    public async Task Handle_FirstTeam_IsRenamed_DefaultStaysTheRoot_RootKeepsItsName()
    {
        var f = new Fixture();

        var result = await f.HandleAsync(f.Command(name: "Основная команда", teamId: f.Project.FirstTeam.Id.ToString()));

        Assert.True(result.Response.IsFirstTeam);
        Assert.Equal("Основная команда", result.Response.Name);
        Assert.Equal(f.Project.Root.Id, result.Response.DefaultArea.Id);
        Assert.Equal("Пример проекта", result.Response.DefaultArea.Path);
        Assert.Equal("Пример проекта", f.Project.Root.Name);
    }

    // BR-M1-1 · "a name never conflicts with the object's own current name": «ПЛАТФОРМА» renames «Платформа».
    [Fact]
    public async Task Handle_OwnNameInAnotherCase_IsAllowed()
    {
        var f = new Fixture();

        var result = await f.HandleAsync(f.Command(name: " ПЛАТФОРМА "));

        Assert.Equal("ПЛАТФОРМА", result.Response.Name);
        Assert.Equal("ПЛАТФОРМА", f.Platform.Team.Name);
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
            f.Command(name: " аналитика ", teamId: f.Platform.Team.Id.ToString())));

        Assert.Equal(OrganizationErrorCodes.TeamNameTaken, refusal.Code);
        Assert.Equal("Платформа", f.Platform.Team.Name);
        Assert.Equal("Аналитика", analytics.Team.Name);
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
        core.Team.Delete();

        var result = await f.HandleAsync(f.Command(name: "ядро"));

        Assert.Equal("ядро", result.Response.Name);
        Assert.Equal("Ядро", core.Area.Name);
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
        Assert.Equal("Платформа", f.Platform.Team.Name);
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
        Assert.Equal("Платформа", f.Platform.Team.Name);
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
            "malformed" => "{" + core.Team.Id + "}",
            "empty" => "",
            "unknown" => Guid.CreateVersion7().ToString(),
            "of another project" => secondProject.FirstTeam.Id.ToString(),
            _ => core.Team.Id.ToString(),
        };

        if (how == "deleted")
        {
            core.Team.Delete();
        }

        var refusal = await Assert.ThrowsAsync<NotFoundException>(() => f.HandleAsync(f.Command(teamId: teamId, name: "")));

        Assert.Equal(OrganizationErrorCodes.TeamNotFound, refusal.Code);
        Assert.Equal(0, f.Store.SaveCount);
        Assert.Equal("Ядро", core.Team.Name);
        Assert.Equal("Второй проект", secondProject.FirstTeam.Name);
    }

    // plan-backend § 8.2 "Rename team": the team FOR NO KEY UPDATE and nothing else, then one save. No area is locked, and
    // the project is not (a write beneath only reads its container's status, ADR-0026 rule 8).
    [Fact]
    public async Task Handle_LocksTheTeamForNoKeyUpdate_ThenSavesOnce_LocksNoAreaAndNoProject()
    {
        var f = new Fixture();

        await f.HandleAsync(f.Command());

        Assert.Equal([$"lock team no-key-update {f.Platform.Team.Id}", "save"], f.Store.Calls);
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
