using Mustask.Application.Abstractions.Organizations;
using Mustask.Application.Features.Organizations.DeactivateProject;
using Mustask.Application.Tests.TestDoubles;
using Mustask.Application.Tests.TestDoubles.Organizations;
using Mustask.Domain.Organizations;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Tests.Features.Organizations.DeactivateProject;

/// <summary>
/// US-3 "A project is deactivated" on fakes (plan-backend § 13, lane L-13):
/// what the handler writes, the order of its answers (§ 5.3, api-contract § 0.4)
/// and its lock (§ 8.2).
/// </summary>
public sealed class DeactivateProjectHandlerTests
{
    private sealed class Fixture
    {
        public Fixture()
        {
            Organization = Store.AddOrganization();
            Project = Store.AddProject(Organization);
            Platform = Store.AddTeam(Project);
            Reports = Store.AddArea(Project.Root, "Отчёты", owner: Platform.Team);
            Quarterly = Store.AddArea(Reports, "Квартальные");
            Handler = new DeactivateProjectHandler(new FakeProjectRepository(Store), new FakeUnitOfWork());
        }

        public InMemoryOrganizations Store { get; } = new();

        public Organization Organization { get; }

        public NewProject Project { get; }

        public NewTeam Platform { get; }

        public Area Reports { get; }

        public Area Quarterly { get; }

        public DeactivateProjectHandler Handler { get; private set; }

        public DeactivateProjectCommand Command(string? projectId = null, string? organizationId = null) =>
            new(organizationId ?? Organization.Id.ToString(), projectId ?? Project.Project.Id.ToString());

        public Task HandleAsync(DeactivateProjectCommand command) => Handler.Handle(command, CancellationToken.None);

        /// <summary>The handler over a project repository that has another request's deactivation commit just before its lock returns.</summary>
        public void LoseTheRaceForTheLock() =>
            Handler = new DeactivateProjectHandler(new DeactivatedBeforeItsLock(new FakeProjectRepository(Store)), new FakeUnitOfWork());

        /// <summary>Everything beneath the project, as a test compares it before and after: ids, names, owners, defaults and statuses.</summary>
        public IReadOnlyList<string> Beneath() =>
        [
            .. Store.Teams.OrderBy(team => team.Id).Select(team => $"team {team.Id} {team.Name} {team.Status} first={team.IsFirstTeam}"),
            .. Store.Areas.OrderBy(area => area.Id).Select(area =>
                $"area {area.Id} {area.ParentId} {area.Name} owner={area.OwnerTeamId} default={area.DefaultOfTeamId}"),
        ];
    }

    /// <summary>
    /// The race of two deactivations of one project: the other request took the lock first and committed, so what the lock
    /// returns to this one is already deactivated (ADR-0026 rule 8).
    /// </summary>
    private sealed class DeactivatedBeforeItsLock(IProjectRepository inner) : IProjectRepository
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

        public void Add(NewProject project) => inner.Add(project);

        public Task SaveAsync(CancellationToken cancellationToken) => inner.SaveAsync(cancellationToken);
    }

    // US-3 · "A project is deactivated": it is no longer reachable, and the organization is unchanged.
    [Fact]
    public async Task Handle_ActiveProject_DeactivatesIt()
    {
        var f = new Fixture();

        await f.HandleAsync(f.Command());

        Assert.Equal(ProjectStatus.Deactivated, f.Project.Project.Status);
        Assert.False(f.Store.IsReachable(f.Organization.Id, f.Project.Project.Id));
        Assert.True(f.Organization.IsActive);
    }

    // US-3 · "nothing under it is deleted" (ADR-0026 rule 2): the deactivation writes the project's one row; the teams, the
    // areas, their owners and defaults, and the project's siblings keep their rows and their statuses.
    [Fact]
    public async Task Handle_ActiveProject_DeletesNothing_ChangesNoRowBeneathOrBeside()
    {
        var f = new Fixture();
        var sibling = f.Store.AddProject(f.Organization, "Второй проект", "SEC");
        var beneath = f.Beneath();
        var projects = f.Store.Projects.Count;

        await f.HandleAsync(f.Command());

        Assert.Equal(beneath, f.Beneath());
        Assert.All(f.Store.Teams, team => Assert.True(team.IsActive));
        Assert.Equal(projects, f.Store.Projects.Count);
        Assert.Equal(ProjectStatus.Active, sibling.Project.Status);
        Assert.Equal("Пример проекта", f.Project.Project.Name);
        Assert.Equal("PRM", f.Project.Project.Key);
    }

    // plan-backend § 8.2: the project FOR NO KEY UPDATE, then one save; no other lock, because nothing beneath is read.
    [Fact]
    public async Task Handle_LocksTheProjectOnly_ThenSavesOnce()
    {
        var f = new Fixture();

        await f.HandleAsync(f.Command());

        Assert.Equal([$"lock project {f.Project.Project.Id}", "save"], f.Store.Calls);
    }

    // BR-M1-20: behind the filter the organization id is well formed; if it were not, the outermost level answers.
    [Fact]
    public async Task Handle_MalformedOrganization_IsOrganizationNotFound()
    {
        var f = new Fixture();

        var refusal = await Assert.ThrowsAsync<NotFoundException>(() => f.HandleAsync(f.Command(organizationId: "abc")));

        Assert.Equal(OrganizationErrorCodes.OrganizationNotFound, refusal.Code);
        Assert.True(f.Project.Project.IsActive);
        Assert.Empty(f.Store.Calls);
    }

    // US-3 · "A project identifier that names no project of the organization" (BR-M1-20, BR-M1-22) and "(already deactivated
    // included)" of api-contract § 2.6: every way a project is not reachable answers project.not_found, takes no lock and
    // changes nothing.
    [Theory]
    [InlineData("null")]
    [InlineData("malformed")]
    [InlineData("unknown")]
    [InlineData("of another organization")]
    [InlineData("deactivated already")]
    [InlineData("under a deactivated organization")]
    public async Task Handle_ProjectNotReachable_IsProjectNotFound_TakesNoLockAndChangesNothing(string how)
    {
        var f = new Fixture();
        var other = f.Store.AddProject(f.Store.AddOrganization("Другая организация"), "Чужой", "OTH");
        var command = f.Command(projectId: how switch
        {
            "malformed" => "{" + f.Project.Project.Id + "}",
            "unknown" => Guid.CreateVersion7().ToString(),
            "of another organization" => other.Project.Id.ToString(),
            _ => f.Project.Project.Id.ToString(),
        });

        if (how == "null")
        {
            command = command with { ProjectId = null };
        }

        if (how == "deactivated already")
        {
            f.Project.Project.Deactivate();
        }

        if (how == "under a deactivated organization")
        {
            f.Organization.Deactivate();
        }

        var beneath = f.Beneath();

        var refusal = await Assert.ThrowsAsync<NotFoundException>(() => f.HandleAsync(command));

        Assert.Equal(OrganizationErrorCodes.ProjectNotFound, refusal.Code);
        Assert.Empty(f.Store.Calls);
        Assert.Equal(beneath, f.Beneath());
        Assert.Equal(ProjectStatus.Active, other.Project.Status);
        Assert.Equal(how != "deactivated already", f.Project.Project.IsActive);
    }

    // api-contract § 2.6: "404 ... project (already deactivated included)". Two deactivations at the same moment: the one that
    // locks second finds what the first left, answers project.not_found and saves nothing (ADR-0026 rule 8).
    [Fact]
    public async Task Handle_DeactivatedByAnotherRequestBeforeTheLock_IsProjectNotFound_SavesNothing()
    {
        var f = new Fixture();
        f.LoseTheRaceForTheLock();

        var refusal = await Assert.ThrowsAsync<NotFoundException>(() => f.HandleAsync(f.Command()));

        Assert.Equal(OrganizationErrorCodes.ProjectNotFound, refusal.Code);
        Assert.Equal(0, f.Store.SaveCount);
        Assert.Equal(ProjectStatus.Deactivated, f.Project.Project.Status);
    }
}
