using Mustask.Application.Features.Organizations.AddTeam;
using Mustask.Application.Tests.TestDoubles;
using Mustask.Application.Tests.TestDoubles.Organizations;
using Mustask.Domain.Organizations;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Tests.Features.Organizations.AddTeam;

/// <summary>
/// AddTeam on fakes (plan-backend § 13, lane L-17): what it creates, the order of
/// its answers (§ 5.3, api-contract § 0.4) and its lock (§ 8.2).
/// </summary>
public sealed class AddTeamHandlerTests
{
    private sealed class Fixture
    {
        public Fixture()
        {
            Organization = Store.AddOrganization();
            Project = Store.AddProject(Organization);
            Platform = Store.AddTeam(Project);
            Handler = new AddTeamHandler(
                new FakeProjectRepository(Store),
                new FakeTeamRepository(Store),
                new FakeAreaRepository(Store),
                new FakeUnitOfWork());
        }

        public InMemoryOrganizations Store { get; } = new();

        public Organization Organization { get; }

        public NewProject Project { get; }

        public NewTeam Platform { get; }

        public AddTeamHandler Handler { get; }

        public AddTeamCommand Command(string? name = "Аналитика", string? projectId = null, string? organizationId = null) =>
            new(
                organizationId ?? Organization.Id.ToString(),
                projectId ?? Project.Project.Id.ToString(),
                name);

        public Task<AddTeamResult> HandleAsync(AddTeamCommand command) => Handler.Handle(command, CancellationToken.None);
    }

    // US-4 · "A second team comes with its own area": the root gains a child «Аналитика», owned by the team and its default.
    [Fact]
    public async Task Handle_AddsTheTeamWithRootChildOwnedByItAndItsDefault()
    {
        var f = new Fixture();

        var result = await f.HandleAsync(f.Command(name: " Аналитика "));

        var team = f.Store.Teams.Single(candidate => candidate.Id == result.Response.Id);
        var area = f.Store.Areas.Single(candidate => candidate.Id == result.Response.DefaultArea.Id);
        Assert.Equal("Аналитика", team.Name);
        Assert.False(team.IsFirstTeam);
        Assert.True(team.IsActive);
        Assert.Equal(f.Project.Project.Id, team.ProjectId);
        Assert.Equal("Аналитика", area.Name);
        Assert.Equal(f.Project.Root.Id, area.ParentId);
        Assert.Equal(team.Id, area.OwnerTeamId);
        Assert.Equal(team.Id, area.DefaultOfTeamId);
        Assert.Equal(
            new TeamResponse(team.Id, f.Project.Project.Id, "Аналитика", false, new TeamDefaultAreaResponse(area.Id, "Аналитика")),
            result.Response);
    }

    // US-4 · "A second team comes with its own area": the project has «Пример проекта», «Платформа» and now «Аналитика»;
    // the first team and the existing team are untouched.
    [Fact]
    public async Task Handle_LeavesTheOtherTeamsAndTheirAreasUnchanged()
    {
        var f = new Fixture();

        var result = await f.HandleAsync(f.Command());

        Assert.Equal(
            ["Пример проекта", "Платформа", "Аналитика"],
            f.Store.Teams.Where(team => team.ProjectId == f.Project.Project.Id).Select(team => team.Name));
        Assert.Equal(f.Project.FirstTeam.Id, f.Store.Teams.Single(team => team.IsFirstTeam).Id);
        Assert.Equal(f.Platform.Team.Id, f.Platform.Area.DefaultOfTeamId);
        Assert.Equal(f.Project.FirstTeam.Id, f.Project.Root.DefaultOfTeamId);
        Assert.Equal(1, f.Store.Areas.Count(area => area.DefaultOfTeamId == result.Response.Id));
    }

    // BR-M1-20: behind the filter the organization id is well formed; if it were not, the outermost level answers.
    [Fact]
    public async Task Handle_MalformedOrganization_IsOrganizationNotFound()
    {
        var f = new Fixture();

        var refusal = await Assert.ThrowsAsync<NotFoundException>(() => f.HandleAsync(f.Command(organizationId: "abc", name: "")));

        Assert.Equal(OrganizationErrorCodes.OrganizationNotFound, refusal.Code);
    }

    // US-3 · "A project id names no project of the organization" and "a project is deactivated" (BR-M1-20, BR-M1-22):
    // the project level answers before the body is looked at.
    [Theory]
    [InlineData("malformed")]
    [InlineData("unknown")]
    [InlineData("of another organization")]
    [InlineData("deactivated")]
    [InlineData("under a deactivated organization")]
    public async Task Handle_ProjectNotReachable_IsProjectNotFound_BeforeTheBody(string how)
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

        var teamsBefore = f.Store.Teams.Count;
        var areasBefore = f.Store.Areas.Count;

        var refusal = await Assert.ThrowsAsync<NotFoundException>(() => f.HandleAsync(f.Command(projectId: projectId, name: "")));

        Assert.Equal(OrganizationErrorCodes.ProjectNotFound, refusal.Code);
        Assert.Empty(f.Store.Calls);
        Assert.Equal(teamsBefore, f.Store.Teams.Count);
        Assert.Equal(areasBefore, f.Store.Areas.Count);
    }

    // US-4 · "An empty name, or one longer than 100 characters, is refused with «Название должно содержать от 1 до 100
    // символов»": nothing is locked or written.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("101")]
    public async Task Handle_InvalidName_IsValidationFailedWithNameLength_BeforeAnyLock(string? name)
    {
        var f = new Fixture();
        var submitted = name == "101" ? new string('я', 101) : name;

        var refusal = await Assert.ThrowsAsync<ValidationException>(() => f.HandleAsync(f.Command(name: submitted)));

        Assert.Equal(OrganizationErrorCodes.ValidationFailed, refusal.Code);
        Assert.Equal(["name"], refusal.Errors.Keys);
        Assert.Equal(OrganizationErrorCodes.NameLength, Assert.Single(refusal.Errors["name"]).Code);
        Assert.Empty(f.Store.Calls);
        Assert.Equal(2, f.Store.Teams.Count(team => team.ProjectId == f.Project.Project.Id));
    }

    // US-4 · "A team's name is unique among the project's active teams", compared ignoring case and spaces. The new name's
    // area name is taken too (the existing team's own area), and the team's refusal answers first (api-contract § 3.3).
    [Theory]
    [InlineData("платформа ")]
    [InlineData(" ПЛАТФОРМА")]
    [InlineData("Платформа")]
    [InlineData("пример проекта")]
    public async Task Handle_NameOfAnActiveTeamAnyCase_IsTeamNameTaken_CreatesNothing(string name)
    {
        var f = new Fixture();

        var refusal = await Assert.ThrowsAsync<ConflictException>(() => f.HandleAsync(f.Command(name: name)));

        Assert.Equal(OrganizationErrorCodes.TeamNameTaken, refusal.Code);
        Assert.Equal(2, f.Store.Teams.Count(team => team.ProjectId == f.Project.Project.Id));
        Assert.Single(f.Store.Areas, area => area.ProjectId == f.Project.Project.Id && !area.IsRoot);
        Assert.Equal(0, f.Store.SaveCount);
    }

    // US-4 · "«Платформа» is still free as a team name in another project".
    [Fact]
    public async Task Handle_SameNameInAnotherProject_Succeeds()
    {
        var f = new Fixture();
        var other = f.Store.AddProject(f.Organization, "Второй проект", "SEC");

        var result = await f.HandleAsync(f.Command(name: "Платформа", projectId: other.Project.Id.ToString()));

        Assert.Equal(other.Project.Id, result.Response.ProjectId);
        Assert.Equal(2, f.Store.Teams.Count(team => team.Name == "Платформа"));
        Assert.Equal(other.Root.Id, f.Store.Areas.Single(area => area.Id == result.Response.DefaultArea.Id).ParentId);
    }

    // US-4 · "A new team's area name must be free under the root": «Аналитика» is a child of the root with no team of that
    // name; no team and no area is created.
    [Fact]
    public async Task Handle_RootHasChildOfThatName_IsAreaNameTaken_CreatesNothing()
    {
        var f = new Fixture();
        f.Store.AddArea(f.Project.Root, "Аналитика");
        var teamsBefore = f.Store.Teams.Count;
        var areasBefore = f.Store.Areas.Count;

        var refusal = await Assert.ThrowsAsync<ConflictException>(() => f.HandleAsync(f.Command(name: " аналитика ")));

        Assert.Equal(OrganizationErrorCodes.AreaNameTaken, refusal.Code);
        Assert.Equal(teamsBefore, f.Store.Teams.Count);
        Assert.Equal(areasBefore, f.Store.Areas.Count);
        Assert.Equal(0, f.Store.SaveCount);
    }

    // US-4 · "A new team's area name must be free under the root": a child of another parent with that name does not count.
    [Fact]
    public async Task Handle_AreaOfThatNameBelowAChild_DoesNotBlock()
    {
        var f = new Fixture();
        f.Store.AddArea(f.Platform.Area, "Аналитика");

        var result = await f.HandleAsync(f.Command(name: "Аналитика"));

        Assert.Equal("Аналитика", result.Response.DefaultArea.Path);
    }

    // US-7 · "The deleted team's name is free, but its area's name is not": the team's name is free, the root still has the
    // child «Платформа», so the answer is the area's; renamed away, the team is created with a new area.
    [Fact]
    public async Task Handle_NameOfDeletedTeam_WhileItsAreaRemains_IsAreaNameTaken_AfterAreaRenamed_Succeeds()
    {
        var f = new Fixture();
        f.Platform.Team.Delete();

        var refusal = await Assert.ThrowsAsync<ConflictException>(() => f.HandleAsync(f.Command(name: "Платформа")));

        Assert.Equal(OrganizationErrorCodes.AreaNameTaken, refusal.Code);
        Assert.Single(f.Store.Teams, team => team.Name == "Платформа");

        f.Platform.Area.Rename(InMemoryOrganizations.Name("Платформа (архив)"));
        var created = await f.HandleAsync(f.Command(name: "Платформа"));

        var team = f.Store.Teams.Single(candidate => candidate.Id == created.Response.Id);
        var area = f.Store.Areas.Single(candidate => candidate.Id == created.Response.DefaultArea.Id);
        Assert.True(team.IsActive);
        Assert.NotEqual(f.Platform.Team.Id, team.Id);
        Assert.NotEqual(f.Platform.Area.Id, area.Id);
        Assert.Equal(f.Project.Root.Id, area.ParentId);
        Assert.Equal(team.Id, area.DefaultOfTeamId);
        Assert.Equal("Платформа", created.Response.DefaultArea.Path);
    }

    // plan-backend § 8.2: the root FOR SHARE, nothing else, then one save.
    [Fact]
    public async Task Handle_LocksTheRootShare_ThenSavesOnce()
    {
        var f = new Fixture();

        await f.HandleAsync(f.Command());

        Assert.Equal([$"lock root share {f.Project.Project.Id}", "save"], f.Store.Calls);
    }

    // US-4 · "Two teams of one name at the same moment": the loser's save fails on the team index, and the repository's
    // answer reaches the client unchanged; nothing it staged is stored.
    [Fact]
    public async Task Handle_LosesTheTeamNameRace_AnswersTeamNameTaken_StoresNothing()
    {
        var f = new Fixture();
        f.Store.FailNextSave(new ConflictException(OrganizationErrorCodes.TeamNameTaken));
        var teamsBefore = f.Store.Teams.Count;
        var areasBefore = f.Store.Areas.Count;

        var refusal = await Assert.ThrowsAsync<ConflictException>(() => f.HandleAsync(f.Command()));

        Assert.Equal(OrganizationErrorCodes.TeamNameTaken, refusal.Code);
        Assert.Equal(teamsBefore, f.Store.Teams.Count);
        Assert.Equal(areasBefore, f.Store.Areas.Count);
    }
}
