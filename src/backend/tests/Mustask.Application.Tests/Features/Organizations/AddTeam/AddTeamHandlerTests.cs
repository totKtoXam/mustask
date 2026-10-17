using Mustask.Application.Features.Organizations.AddTeam;
using Mustask.Application.Tests.TestDoubles;
using Mustask.Application.Tests.TestDoubles.Organizations;
using Mustask.Domain.Organizations;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Tests.Features.Organizations.AddTeam;

/// <summary>
/// AddTeam on fakes (plan-backend § 13, lane L-17; mod-017 BR-OT-10, BR-OT-11): what it creates (an ordinary team, no area),
/// the order of its answers (§ 5.3, api-contract § 0.4) and that it locks nothing (§ 7).
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
                new FakeUnitOfWork());
        }

        public InMemoryOrganizations Store { get; } = new();

        public Organization Organization { get; }

        public NewProject Project { get; }

        public Team Platform { get; }

        public AddTeamHandler Handler { get; }

        public AddTeamCommand Command(string? name = "Аналитика", string? projectId = null, string? organizationId = null) =>
            new(
                organizationId ?? Organization.Id.ToString(),
                projectId ?? Project.Project.Id.ToString(),
                name);

        public Task<AddTeamResult> HandleAsync(AddTeamCommand command) => Handler.Handle(command, CancellationToken.None);
    }

    // mod-017 US-4 · "A team added later is an ordinary one": a name, a status and no mark; it comes with no area (BR-OT-10,
    // BR-OT-11). Replaces Handle_AddsTheTeamWithRootChildOwnedByItAndItsDefault (retired, BR-OT-11).
    [Fact]
    public async Task Handle_AddsAnOrdinaryTeam_WithNoArea()
    {
        var f = new Fixture();
        var areasBefore = f.Store.Areas.Count;

        var result = await f.HandleAsync(f.Command(name: " Аналитика "));

        var team = f.Store.Teams.Single(candidate => candidate.Id == result.Response.Id);
        Assert.Equal("Аналитика", team.Name);
        Assert.True(team.IsActive);
        Assert.Equal(f.Project.Project.Id, team.ProjectId);
        Assert.Equal(areasBefore, f.Store.Areas.Count);
        Assert.Equal(new TeamResponse(team.Id, f.Project.Project.Id, "Аналитика", DefaultMark: null), result.Response);
    }

    // mod-017 US-4: the project has «Пример проекта», «Платформа» and now «Аналитика»; the existing teams are untouched.
    // Renamed (D-16, BR-OT-10): Handle_LeavesTheOtherTeamsAndTheirAreasUnchanged -> Handle_LeavesTheOtherTeamsUnchanged.
    [Fact]
    public async Task Handle_LeavesTheOtherTeamsUnchanged()
    {
        var f = new Fixture();

        await f.HandleAsync(f.Command());

        Assert.Equal(
            ["Пример проекта", "Платформа", "Аналитика"],
            f.Store.Teams.Where(team => team.ProjectId == f.Project.Project.Id).Select(team => team.Name));
        Assert.All(f.Store.Teams, team => Assert.True(team.IsActive));
        Assert.Null(f.Project.Project.DefaultTeamId);
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

        var refusal = await Assert.ThrowsAsync<NotFoundException>(() => f.HandleAsync(f.Command(projectId: projectId, name: "")));

        Assert.Equal(OrganizationErrorCodes.ProjectNotFound, refusal.Code);
        Assert.Empty(f.Store.Calls);
        Assert.Equal(teamsBefore, f.Store.Teams.Count);
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

    // US-4 · "A team's name is unique among the project's active teams", compared ignoring case and spaces.
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
    }

    // mod-017 US-4 · "a team's name is no area's name" (BR-OT-10): an area of the root that holds the name is no obstacle, and
    // no area is created. Replaces Handle_RootHasChildOfThatName_IsAreaNameTaken_CreatesNothing and
    // Handle_AreaOfThatNameBelowAChild_DoesNotBlock (retired, BR-OT-10).
    [Fact]
    public async Task Handle_NameOfAnExistingArea_Succeeds_NoAreaCreated()
    {
        var f = new Fixture();
        f.Store.AddArea(f.Project.Root, "Аналитика");
        var areasBefore = f.Store.Areas.Count;

        var result = await f.HandleAsync(f.Command(name: " аналитика "));

        Assert.Equal("аналитика", result.Response.Name);
        Assert.Equal(areasBefore, f.Store.Areas.Count);
    }

    // US-7 · "The deleted team's name is free": with no area in the way, the name of a deleted team is taken again at once.
    // Replaces Handle_NameOfDeletedTeam_WhileItsAreaRemains_IsAreaNameTaken_AfterAreaRenamed_Succeeds (retired, BR-OT-10).
    [Fact]
    public async Task Handle_NameOfDeletedTeam_IsFree()
    {
        var f = new Fixture();
        f.Platform.Delete(3);

        var created = await f.HandleAsync(f.Command(name: "Платформа"));

        var team = f.Store.Teams.Single(candidate => candidate.Id == created.Response.Id);
        Assert.True(team.IsActive);
        Assert.NotEqual(f.Platform.Id, team.Id);
        Assert.Single(f.Store.Teams, candidate => candidate.Name == "Платформа" && candidate.IsActive);
    }

    // plan-backend § 7: no lock at all (the address is read, the name is checked, the insert is closed by the index), then one
    // save. Replaces Handle_LocksTheRootShare_ThenSavesOnce (retired, BR-OT-10: there is no root lock).
    [Fact]
    public async Task Handle_LocksNothing_SavesOnce()
    {
        var f = new Fixture();

        await f.HandleAsync(f.Command());

        Assert.Equal(["save"], f.Store.Calls);
    }

    // US-4 · "Two teams of one name at the same moment": the loser's save fails on the team index, and the repository's
    // answer reaches the client unchanged; nothing it staged is stored.
    [Fact]
    public async Task Handle_LosesTheTeamNameRace_AnswersTeamNameTaken_StoresNothing()
    {
        var f = new Fixture();
        f.Store.FailNextSave(new ConflictException(OrganizationErrorCodes.TeamNameTaken));
        var teamsBefore = f.Store.Teams.Count;

        var refusal = await Assert.ThrowsAsync<ConflictException>(() => f.HandleAsync(f.Command()));

        Assert.Equal(OrganizationErrorCodes.TeamNameTaken, refusal.Code);
        Assert.Equal(teamsBefore, f.Store.Teams.Count);
    }
}
