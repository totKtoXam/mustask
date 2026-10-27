using Mustask.Application.Abstractions.Organizations;
using Mustask.Application.Features.Organizations.SetDefaultTeam;
using Mustask.Application.Tests.TestDoubles;
using Mustask.Application.Tests.TestDoubles.Organizations;
using Mustask.Domain.Organizations;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Tests.Features.Organizations.SetDefaultTeam;

/// <summary>
/// <c>SetDefaultTeam</c> on fakes (mod-017 plan-backend § 5.3, § 7; feat-028 plan-backend § 7.2; US-3, BR-OT-4, BR-OL-7): what it
/// stores, the order of its answers (api-contract § 0.5) and its lock order (ADR-0028 rule 8: the <b>link</b> <c>FOR SHARE</c>, then
/// the project row, never the other way round). Rewritten to "linked" (BR-OL-6; D-16).
/// </summary>
public sealed class SetDefaultTeamHandlerTests
{
    private sealed class Fixture
    {
        public Fixture()
        {
            Organization = Store.AddOrganization();
            Project = Store.AddProject(Organization);
            Platform = Store.AddLinkedTeam(Organization, Project);
            Handler = Build(new FakeProjectRepository(Store));
        }

        public InMemoryOrganizations Store { get; } = new();

        public Organization Organization { get; }

        public Project Project { get; }

        public Team Platform { get; }

        public SetDefaultTeamHandler Handler { get; }

        public FakeProjectTeamRepository Links => _links ??= new FakeProjectTeamRepository(Store);

        private FakeProjectTeamRepository? _links;

        public SetDefaultTeamHandler Build(IProjectRepository projects) =>
            new(new FakeOrganizationStructure(Store), projects, Links, new FakeUnitOfWork());

        public SetDefaultTeamCommand Command(string? teamId, string? projectId = null, string? organizationId = null) =>
            new(organizationId ?? Organization.Id.ToString(), projectId ?? Project.Id.ToString(), teamId);

        public Task HandleAsync(SetDefaultTeamCommand command) => Handler.Handle(command, CancellationToken.None);
    }

    // US-3 · "the default must be linked": the setting names «Платформа»; its link is locked before the project row.
    [Fact]
    public async Task Handle_NamesTheLinkedTeam_LocksTheLinkShare_ThenTheProject_ThenSavesOnce()
    {
        var f = new Fixture();

        await f.HandleAsync(f.Command(f.Platform.Id.ToString()));

        Assert.Equal(f.Platform.Id, f.Project.DefaultTeamId);
        Assert.Equal(
            [$"lock link share {f.Project.Id} {f.Platform.Id}", $"lock project {f.Project.Id}", "save"],
            f.Store.Calls);
    }

    // US-3 · "naming no team clears the setting": a missing id locks no link; the project row is locked and saved.
    [Fact]
    public async Task Handle_NoTeam_ClearsTheSetting_LocksNoLink()
    {
        var f = new Fixture();
        f.Project.NameDefaultTeam(f.Platform.Id);

        await f.HandleAsync(f.Command(teamId: null));

        Assert.Null(f.Project.DefaultTeamId);
        Assert.Equal([$"lock project {f.Project.Id}", "save"], f.Store.Calls);
    }

    // BR-OT-4 · naming the team the setting already names changes nothing and saves nothing.
    [Fact]
    public async Task Handle_TheSameTeamAgain_ChangesNothing_SavesNothing()
    {
        var f = new Fixture();
        f.Project.NameDefaultTeam(f.Platform.Id);

        await f.HandleAsync(f.Command(f.Platform.Id.ToString()));

        Assert.Equal(f.Platform.Id, f.Project.DefaultTeamId);
        Assert.Equal(0, f.Store.SaveCount);
    }

    // api-contract § 0.3 · a team that is not an active team of the organization answers team.not_found, whichever way it is not:
    // malformed, unknown, deleted, of another organization.
    [Theory]
    [InlineData("malformed")]
    [InlineData("unknown")]
    [InlineData("deleted")]
    [InlineData("of another organization")]
    public async Task Handle_TeamNotFound_IsTeamNotFound_SettingUnchanged(string how)
    {
        var f = new Fixture();
        var foreign = f.Store.AddTeam(f.Store.AddOrganization("Другая организация"), "Чужая");
        var teamId = how switch
        {
            "malformed" => "not-a-team",
            "unknown" => Guid.CreateVersion7().ToString(),
            "of another organization" => foreign.Id.ToString(),
            _ => f.Platform.Id.ToString(),
        };

        if (how == "deleted")
        {
            f.Platform.Delete();
        }

        var refusal = await Assert.ThrowsAsync<NotFoundException>(() => f.HandleAsync(f.Command(teamId)));

        Assert.Equal(OrganizationErrorCodes.TeamNotFound, refusal.Code);
        Assert.Null(f.Project.DefaultTeamId);
        Assert.Equal(0, f.Store.SaveCount);
    }

    // BR-OL-6, api-contract § 0.3 · an active team of the organization that is NOT LINKED to the project: 409 team_not_in_project
    // (renamed from ...TeamOfAnotherProjectOfTheOrganization_IsTeamNotInProject_SettingUnchanged, D-16).
    [Fact]
    public async Task Handle_TeamNotLinkedToTheProject_IsTeamNotInProject_SettingUnchanged()
    {
        var f = new Fixture();
        var unlinked = f.Store.AddTeam(f.Organization, "Не связана");

        var refusal = await Assert.ThrowsAsync<ConflictException>(() => f.HandleAsync(f.Command(unlinked.Id.ToString())));

        Assert.Equal(OrganizationErrorCodes.TeamNotInProject, refusal.Code);
        Assert.Null(f.Project.DefaultTeamId);
        Assert.Equal(0, f.Store.SaveCount);
    }

    // feat-028 review-security S-1 · "linked" is what the lock statement returned: a team whose link was committed after that statement
    // is held by nobody, so naming it is 409 team.not_in_project although a later read finds the link — an unlink or a deletion of it
    // would otherwise fail at its final DELETE on the setting's key. The setting stays, and the project row is not locked.
    [Fact]
    public async Task Handle_TeamLinkedAfterTheLock_IsTeamNotInProject_SettingUnchanged()
    {
        var f = new Fixture();
        f.Links.CommittedAfterTheLock.Add(f.Platform.Id);

        var refusal = await Assert.ThrowsAsync<ConflictException>(() => f.HandleAsync(f.Command(f.Platform.Id.ToString())));

        Assert.Equal(OrganizationErrorCodes.TeamNotInProject, refusal.Code);
        Assert.Null(f.Project.DefaultTeamId);
        Assert.Equal(0, f.Store.SaveCount);
        Assert.Equal([$"lock link share {f.Project.Id} {f.Platform.Id}"], f.Store.Calls);
    }

    // api-contract § 0.5 · the address answers before the team: malformed and unknown organizations and projects, a deactivated
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
            "project of another organization" => f.Command(f.Platform.Id.ToString(), projectId: other.Id.ToString()),
            _ => f.Command(f.Platform.Id.ToString()),
        };

        if (how == "deactivated project")
        {
            f.Project.Deactivate();
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
        var handler = f.Build(new DeactivatesAsTheLockIsGranted(new FakeProjectRepository(f.Store)));

        var refusal = await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(f.Command(f.Platform.Id.ToString()), CancellationToken.None));

        Assert.Equal(OrganizationErrorCodes.ProjectNotFound, refusal.Code);
        Assert.Equal(0, f.Store.SaveCount);
    }

    /// <summary>A project repository that lets a deactivation win the lock: the project is deactivated as the lock is granted.</summary>
    private sealed class DeactivatesAsTheLockIsGranted(IProjectRepository inner) : IProjectRepository
    {
        public Task<ProjectStatus?> FindNameHolderAsync(Guid organizationId, string normalizedName, Guid? exceptId, CancellationToken cancellationToken) =>
            inner.FindNameHolderAsync(organizationId, normalizedName, exceptId, cancellationToken);

        public Task<bool> IsKeyTakenAsync(Guid organizationId, string key, CancellationToken cancellationToken) =>
            inner.IsKeyTakenAsync(organizationId, key, cancellationToken);

        public Task<bool> IsReachableAsync(Guid organizationId, Guid projectId, CancellationToken cancellationToken) =>
            inner.IsReachableAsync(organizationId, projectId, cancellationToken);

        public Task<int> CountReachableAsync(Guid organizationId, IReadOnlyCollection<Guid> projectIds, CancellationToken cancellationToken) =>
            inner.CountReachableAsync(organizationId, projectIds, cancellationToken);

        public async Task<Project?> LockAsync(Guid organizationId, Guid projectId, CancellationToken cancellationToken)
        {
            var project = await inner.LockAsync(organizationId, projectId, cancellationToken);
            project?.Deactivate();

            return project;
        }

        public Task ClearDefaultTeamAsync(Guid organizationId, Guid projectId, Guid teamId, CancellationToken cancellationToken) =>
            inner.ClearDefaultTeamAsync(organizationId, projectId, teamId, cancellationToken);

        public Task ClearDefaultTeamAsync(Guid organizationId, Guid teamId, CancellationToken cancellationToken) =>
            inner.ClearDefaultTeamAsync(organizationId, teamId, cancellationToken);

        public void Add(Project project) => inner.Add(project);

        public Task SaveAsync(CancellationToken cancellationToken) => inner.SaveAsync(cancellationToken);
    }
}
