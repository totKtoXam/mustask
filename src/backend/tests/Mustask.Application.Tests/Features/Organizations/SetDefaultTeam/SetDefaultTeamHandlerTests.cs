using Mustask.Application.Features.Organizations.SetDefaultTeam;
using Mustask.Application.Tests.TestDoubles;
using Mustask.Application.Tests.TestDoubles.Organizations;
using Mustask.Domain.Organizations;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Tests.Features.Organizations.SetDefaultTeam;

/// <summary>
/// <c>SetDefaultTeam</c> on fakes (mod-017 plan-backend § 5.3, § 7; US-3, BR-OT-4): what it stores, the order of its answers
/// (api-contract § 0.4) and its lock order (ADR-0028 rule 8: the team <c>FOR SHARE</c>, then the project row, never the other
/// way round).
/// </summary>
public sealed class SetDefaultTeamHandlerTests
{
    private sealed class Fixture
    {
        public Fixture()
        {
            Organization = Store.AddOrganization();
            Project = Store.AddProject(Organization);
            Platform = Store.AddTeam(Project);
            Handler = new SetDefaultTeamHandler(
                new FakeOrganizationStructure(Store),
                new FakeProjectRepository(Store),
                new FakeTeamRepository(Store),
                new FakeUnitOfWork());
        }

        public InMemoryOrganizations Store { get; } = new();

        public Organization Organization { get; }

        public NewProject Project { get; }

        public Team Platform { get; }

        public SetDefaultTeamHandler Handler { get; }

        public SetDefaultTeamCommand Command(string? teamId, string? projectId = null, string? organizationId = null) =>
            new(organizationId ?? Organization.Id.ToString(), projectId ?? Project.Project.Id.ToString(), teamId);

        public Task HandleAsync(SetDefaultTeamCommand command) => Handler.Handle(command, CancellationToken.None);
    }

    // US-3 · "The administrator chooses the default": the setting names «Платформа»; the team is locked before the project row.
    [Fact]
    public async Task Handle_NamesTheTeam_LocksTheTeamShare_ThenTheProject_ThenSavesOnce()
    {
        var f = new Fixture();

        await f.HandleAsync(f.Command(f.Platform.Id.ToString()));

        Assert.Equal(f.Platform.Id, f.Project.Project.DefaultTeamId);
        Assert.Equal([$"lock team share {f.Platform.Id}", $"lock project {f.Project.Project.Id}", "save"], f.Store.Calls);
    }

    // US-3 · "naming no team clears the setting": a missing id locks no team; the project row is locked and saved.
    [Fact]
    public async Task Handle_NoTeam_ClearsTheSetting_LocksNoTeam()
    {
        var f = new Fixture();
        f.Project.Project.NameDefaultTeam(f.Platform.Id);

        await f.HandleAsync(f.Command(teamId: null));

        Assert.Null(f.Project.Project.DefaultTeamId);
        Assert.Equal([$"lock project {f.Project.Project.Id}", "save"], f.Store.Calls);
    }

    // BR-OT-4 · naming the team the setting already names changes nothing and saves nothing.
    [Fact]
    public async Task Handle_TheSameTeamAgain_ChangesNothing_SavesNothing()
    {
        var f = new Fixture();
        f.Project.Project.NameDefaultTeam(f.Platform.Id);

        await f.HandleAsync(f.Command(f.Platform.Id.ToString()));

        Assert.Equal(f.Platform.Id, f.Project.Project.DefaultTeamId);
        Assert.Equal(0, f.Store.SaveCount);
    }

    // api-contract § 0.2 · a team that is not an active, reachable team of the organization answers team.not_found, whichever way it
    // is not: malformed, unknown, deleted, of another organization.
    [Theory]
    [InlineData("malformed")]
    [InlineData("unknown")]
    [InlineData("deleted")]
    [InlineData("of another organization")]
    public async Task Handle_TeamNotFound_IsTeamNotFound_SettingUnchanged(string how)
    {
        var f = new Fixture();
        var other = f.Store.AddProject(f.Store.AddOrganization("Другая организация"), "Чужой", "OTH");
        var teamId = how switch
        {
            "malformed" => "not-a-team",
            "unknown" => Guid.CreateVersion7().ToString(),
            "of another organization" => other.Team.Id.ToString(),
            _ => f.Platform.Id.ToString(),
        };

        if (how == "deleted")
        {
            f.Platform.Delete(2);
        }

        var refusal = await Assert.ThrowsAsync<NotFoundException>(() => f.HandleAsync(f.Command(teamId)));

        Assert.Equal(OrganizationErrorCodes.TeamNotFound, refusal.Code);
        Assert.Null(f.Project.Project.DefaultTeamId);
        Assert.Equal(0, f.Store.SaveCount);
    }

    // BR-OT-2, api-contract § 0.2 · an active team of another project of the organization: 409 team_not_in_project.
    [Fact]
    public async Task Handle_TeamOfAnotherProjectOfTheOrganization_IsTeamNotInProject_SettingUnchanged()
    {
        var f = new Fixture();
        var sibling = f.Store.AddProject(f.Organization, "Второй проект", "SEC");

        var refusal = await Assert.ThrowsAsync<ConflictException>(() => f.HandleAsync(f.Command(sibling.Team.Id.ToString())));

        Assert.Equal(OrganizationErrorCodes.TeamNotInProject, refusal.Code);
        Assert.Null(f.Project.Project.DefaultTeamId);
        Assert.Equal(0, f.Store.SaveCount);
    }

    // api-contract § 0.4 · the address answers before the team: malformed and unknown organizations and projects, a deactivated
    // project and a project of another organization, each with its level's not-found, and nothing is locked.
    [Theory]
    [InlineData("malformed organization", "organizations.organization.not_found")]
    [InlineData("malformed project", "organizations.project.not_found")]
    [InlineData("unknown project", "organizations.project.not_found")]
    [InlineData("project of another organization", "organizations.project.not_found")]
    [InlineData("deactivated project", "organizations.project.not_found")]
    public async Task Handle_Address_IsAnsweredBeforeTheTeam(string how, string code)
    {
        var f = new Fixture();
        var other = f.Store.AddProject(f.Store.AddOrganization("Другая организация"), "Чужой", "OTH");
        var command = how switch
        {
            "malformed organization" => f.Command(f.Platform.Id.ToString(), organizationId: "abc"),
            "malformed project" => f.Command(f.Platform.Id.ToString(), projectId: "abc"),
            "unknown project" => f.Command(f.Platform.Id.ToString(), projectId: Guid.CreateVersion7().ToString()),
            "project of another organization" => f.Command(f.Platform.Id.ToString(), projectId: other.Project.Id.ToString()),
            _ => f.Command(f.Platform.Id.ToString()),
        };

        if (how == "deactivated project")
        {
            f.Project.Project.Deactivate();
        }

        var refusal = await Assert.ThrowsAsync<NotFoundException>(() => f.HandleAsync(command));

        Assert.Equal(code, refusal.Code);
        Assert.Empty(f.Store.Calls);
    }

    // ADR-0026 rule 8 · a deactivation that committed between the reachability check and the project lock is seen under the lock:
    // the project is deactivated, so the answer is project.not_found and nothing is saved.
    [Fact]
    public async Task Handle_ProjectDeactivatedWhileWaitingForItsLock_IsProjectNotFound_SavesNothing()
    {
        var f = new Fixture();
        var handler = new SetDefaultTeamHandler(
            new FakeOrganizationStructure(f.Store),
            new DeactivatesAsTheLockIsGranted(new FakeProjectRepository(f.Store)),
            new FakeTeamRepository(f.Store),
            new FakeUnitOfWork());

        var refusal = await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(f.Command(f.Platform.Id.ToString()), CancellationToken.None));

        Assert.Equal(OrganizationErrorCodes.ProjectNotFound, refusal.Code);
        Assert.Equal(0, f.Store.SaveCount);
    }

    /// <summary>A project repository that lets a deactivation win the lock: the project is deactivated as the lock is granted.</summary>
    private sealed class DeactivatesAsTheLockIsGranted(Mustask.Application.Abstractions.Organizations.IProjectRepository inner)
        : Mustask.Application.Abstractions.Organizations.IProjectRepository
    {
        public Task<ProjectStatus?> FindNameHolderAsync(Guid organizationId, string normalizedName, Guid? exceptId, CancellationToken cancellationToken) =>
            inner.FindNameHolderAsync(organizationId, normalizedName, exceptId, cancellationToken);

        public Task<bool> IsKeyTakenAsync(Guid organizationId, string key, CancellationToken cancellationToken) =>
            inner.IsKeyTakenAsync(organizationId, key, cancellationToken);

        public Task<bool> IsReachableAsync(Guid organizationId, Guid projectId, CancellationToken cancellationToken) =>
            inner.IsReachableAsync(organizationId, projectId, cancellationToken);

        public async Task<Project?> LockAsync(Guid organizationId, Guid projectId, CancellationToken cancellationToken)
        {
            var project = await inner.LockAsync(organizationId, projectId, cancellationToken);
            project?.Deactivate();

            return project;
        }

        public Task ClearDefaultTeamAsync(Guid projectId, Guid teamId, CancellationToken cancellationToken) =>
            inner.ClearDefaultTeamAsync(projectId, teamId, cancellationToken);

        public void Add(NewProject project) => inner.Add(project);

        public Task SaveAsync(CancellationToken cancellationToken) => inner.SaveAsync(cancellationToken);
    }
}
