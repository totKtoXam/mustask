using Mustask.Application.Abstractions.Organizations;
using Mustask.Application.Features.Organizations.RenameProject;
using Mustask.Application.Tests.TestDoubles;
using Mustask.Application.Tests.TestDoubles.Organizations;
using Mustask.Domain.Organizations;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Tests.Features.Organizations.RenameProject;

/// <summary>
/// <c>RenameProject</c> on fakes (plan-backend § 14.3, lane L-12): what a rename
/// changes and what it keeps (US-3), the order of its answers (§ 5.3,
/// api-contract § 0.4), its lock order (§ 8.2) and the loser of a name race.
/// </summary>
public sealed class RenameProjectHandlerTests
{
    private sealed class Fixture
    {
        public Fixture()
        {
            Organization = Store.AddOrganization();
            Project = Store.AddProject(Organization);
            Platform = Store.AddTeam(Project);
            Reports = Store.AddArea(Project.Root, "Отчёты");
            Quarterly = Store.AddArea(Reports, "Квартальные");
            Handler = new RenameProjectHandler(
                new FakeProjectRepository(Store),
                new FakeAreaRepository(Store),
                new FakeUnitOfWork());
        }

        public InMemoryOrganizations Store { get; } = new();

        public Organization Organization { get; }

        public NewProject Project { get; }

        public Team Platform { get; }

        public Area Reports { get; }

        public Area Quarterly { get; }

        public RenameProjectHandler Handler { get; }

        public RenameProjectCommand Command(string? name, string? projectId = null, string? organizationId = null) =>
            new(
                organizationId ?? Organization.Id.ToString(),
                projectId ?? Project.Project.Id.ToString(),
                name);

        public Task<RenameProjectResult> HandleAsync(RenameProjectCommand command) => Handler.Handle(command, CancellationToken.None);

        /// <summary>Everything a rename must leave alone: every team, and every area but the root.</summary>
        public (string[] Teams, string[] Areas) Rest() =>
        (
            Store.Teams.Select(team => $"{team.Id} {team.Name} {team.Status}").Order(StringComparer.Ordinal).ToArray(),
            Store.Areas
                .Where(area => !area.IsRoot)
                .Select(area => $"{area.Id} {area.ParentId} {area.Name}")
                .Order(StringComparer.Ordinal)
                .ToArray());
    }

    /// <summary>
    /// A project repository that lets a deactivation win the lock: the project is deactivated as the lock is granted, as a
    /// concurrent deactivation that committed between the reachability check and the lock would leave it.
    /// </summary>
    private sealed class DeactivatesAsTheLockIsGranted(IProjectRepository inner) : IProjectRepository
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

    // US-3 · "Renaming a project renames its root area and keeps its key": «Пример проекта» (PRM) becomes «Платформа данных».
    // The root takes the name; the key, every team (the project's own team keeps «Пример проекта») and the rest
    // of the tree are unchanged.
    [Fact]
    public async Task Handle_FreeName_RenamesTheProjectAndItsRoot_KeepsTheKeyTheTeamsAndTheTree()
    {
        var f = new Fixture();
        var (teamsBefore, areasBefore) = f.Rest();

        var result = await f.HandleAsync(f.Command(" Платформа данных "));

        Assert.Equal(new ProjectResponse(f.Project.Project.Id, f.Organization.Id, "Платформа данных", "PRM"), result.Response);
        Assert.Equal("Платформа данных", f.Project.Project.Name);
        Assert.Equal("ПЛАТФОРМА ДАННЫХ", f.Project.Project.NormalizedName);
        Assert.Equal("PRM", f.Project.Project.Key);
        Assert.Equal(ProjectStatus.Active, f.Project.Project.Status);

        Assert.Equal("Платформа данных", f.Project.Root.Name);
        Assert.Equal("ПЛАТФОРМА ДАННЫХ", f.Project.Root.NormalizedName);
        Assert.True(f.Project.Root.IsRoot);

        Assert.Equal("Пример проекта", f.Project.Team.Name);
        var (teamsAfter, areasAfter) = f.Rest();
        Assert.Equal(teamsBefore, teamsAfter);
        Assert.Equal(areasBefore, areasAfter);

        var tree = new AreaTree(f.Store.Areas.Select(area => new AreaNode(area.Id, area.ParentId, area.Name)));
        Assert.Equal("Платформа данных", tree.Root.Name);
        Assert.Equal("Отчёты / Квартальные", tree.PathOf(f.Quarterly.Id));
    }

    // US-3 · "The name is unique in the organization" and api-contract § 0.6: a name never conflicts with the object's own
    // current name, so «ПРИМЕР ПРОЕКТА» renames «Пример проекта» to another case of itself.
    [Fact]
    public async Task Handle_OwnNameInAnotherCase_RenamesWithoutConflict_RootFollows()
    {
        var f = new Fixture();

        var result = await f.HandleAsync(f.Command("  ПРИМЕР ПРОЕКТА"));

        Assert.Equal("ПРИМЕР ПРОЕКТА", result.Response.Name);
        Assert.Equal("ПРИМЕР ПРОЕКТА", f.Project.Project.Name);
        Assert.Equal("ПРИМЕР ПРОЕКТА", f.Project.Root.Name);
        Assert.Equal(1, f.Store.SaveCount);
    }

    // US-3 · "The name is unique in the organization": «пример проекта » is refused, for any case and surrounding spaces,
    // and nothing changes. The name is still free in another organization.
    [Fact]
    public async Task Handle_NameOfAnActiveProjectAnyCase_IsProjectNameTaken_ChangesNothing_FreeInAnotherOrganization()
    {
        var f = new Fixture();
        var alpha = f.Store.AddProject(f.Organization, "Альфа", "ALF");
        var beta = f.Store.AddProject(f.Store.AddOrganization("Бета"), "Пример проекта", "PRM");

        var refusal = await Assert.ThrowsAsync<ConflictException>(() => f.HandleAsync(f.Command(" альфа ")));

        Assert.Equal(OrganizationErrorCodes.ProjectNameTaken, refusal.Code);
        Assert.Equal("Пример проекта", f.Project.Project.Name);
        Assert.Equal("Пример проекта", f.Project.Root.Name);
        Assert.Equal("Альфа", alpha.Project.Name);
        Assert.Equal(0, f.Store.SaveCount);

        // The same name is free in «Бета»: renaming its project to «Альфа» succeeds there.
        var elsewhere = await f.HandleAsync(f.Command("Альфа", beta.Project.Id.ToString(), beta.Project.OrganizationId.ToString()));

        Assert.Equal("Альфа", elsewhere.Response.Name);
        Assert.Equal("Альфа", beta.Root.Name);
    }

    // US-3 · "A deactivated project's name stays reserved": the holder is deactivated, so the answer has its own code.
    [Fact]
    public async Task Handle_NameOfADeactivatedProject_IsProjectNameTakenByDeactivated_ChangesNothing()
    {
        var f = new Fixture();
        var archive = f.Store.AddProject(f.Organization, "Архив", "ARC");
        archive.Project.Deactivate();

        var refusal = await Assert.ThrowsAsync<ConflictException>(() => f.HandleAsync(f.Command("архив")));

        Assert.Equal(OrganizationErrorCodes.ProjectNameTakenByDeactivated, refusal.Code);
        Assert.Equal("Пример проекта", f.Project.Project.Name);
        Assert.Equal("Пример проекта", f.Project.Root.Name);
        Assert.Equal(0, f.Store.SaveCount);
    }

    // BR-M1-20: behind the filter the organization id is well formed; if it were not, the outermost level answers.
    [Fact]
    public async Task Handle_MalformedOrganization_IsOrganizationNotFound()
    {
        var f = new Fixture();

        var refusal = await Assert.ThrowsAsync<NotFoundException>(() => f.HandleAsync(f.Command(name: "", organizationId: "abc")));

        Assert.Equal(OrganizationErrorCodes.OrganizationNotFound, refusal.Code);
        Assert.Empty(f.Store.Calls);
    }

    // US-3 · "A project identifier that names no project of the organization" and "A project is deactivated" (BR-M1-20,
    // BR-M1-22): the project level answers before the body is looked at, and nothing is locked.
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

        var refusal = await Assert.ThrowsAsync<NotFoundException>(() => f.HandleAsync(f.Command(name: "", projectId: projectId)));

        Assert.Equal(OrganizationErrorCodes.ProjectNotFound, refusal.Code);
        Assert.Empty(f.Store.Calls);
        Assert.Equal("Чужой", other.Project.Name);
    }

    // US-3 · "The name is unique ... an empty name, or one longer than 100 characters, is refused with «Название должно
    // содержать от 1 до 100 символов»": one reason on `name`, answered before anything is locked or compared.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("101")]
    public async Task Handle_InvalidName_IsValidationFailedWithNameLength_BeforeAnyLock(string? name)
    {
        var f = new Fixture();
        var submitted = name == "101" ? new string('я', 101) : name;

        var refusal = await Assert.ThrowsAsync<ValidationException>(() => f.HandleAsync(f.Command(submitted)));

        Assert.Equal(OrganizationErrorCodes.ValidationFailed, refusal.Code);
        Assert.Equal(["name"], refusal.Errors.Keys);
        Assert.Equal(OrganizationErrorCodes.NameLength, Assert.Single(refusal.Errors["name"]).Code);
        Assert.Empty(f.Store.Calls);
        Assert.Equal("Пример проекта", f.Project.Project.Name);
    }

    // A project that was deactivated between the reachability check and the lock (the deactivation holds the same lock, so
    // it committed first) is not renamed: the status is read again after the lock (ADR-0026 rule 8).
    [Fact]
    public async Task Handle_DeactivatedWhileWaitingForTheLock_IsProjectNotFound_ChangesNothing()
    {
        var f = new Fixture();
        var handler = new RenameProjectHandler(
            new DeactivatesAsTheLockIsGranted(new FakeProjectRepository(f.Store)),
            new FakeAreaRepository(f.Store),
            new FakeUnitOfWork());

        var refusal = await Assert.ThrowsAsync<NotFoundException>(() => handler.Handle(f.Command("Платформа данных"), CancellationToken.None));

        Assert.Equal(OrganizationErrorCodes.ProjectNotFound, refusal.Code);
        Assert.Equal([$"lock project {f.Project.Project.Id}"], f.Store.Calls);
        Assert.Equal("Пример проекта", f.Project.Project.Name);
        Assert.Equal("Пример проекта", f.Project.Root.Name);
    }

    // plan-backend § 8.2: the project FOR NO KEY UPDATE, then the root FOR UPDATE, then one save. A refused name locks the
    // project only: the root is locked once the name is known to be free.
    [Fact]
    public async Task Handle_LocksTheProjectThenTheRoot_ThenSavesOnce()
    {
        var f = new Fixture();
        var projectId = f.Project.Project.Id;
        f.Store.AddProject(f.Organization, "Альфа", "ALF");

        await f.HandleAsync(f.Command("Платформа данных"));
        Assert.Equal([$"lock project {projectId}", $"lock root update {projectId}", "save"], f.Store.Calls);

        f.Store.Calls.Clear();
        await Assert.ThrowsAsync<ConflictException>(() => f.HandleAsync(f.Command("Альфа")));
        Assert.Equal([$"lock project {projectId}"], f.Store.Calls);
    }

    // US-3 · "The name is unique" and NFR Concurrency: two renames to one free name keep one. The loser's save fails on the
    // name index, and the repository's answer reaches the client unchanged.
    [Fact]
    public async Task Handle_LosesTheNameRace_AnswersProjectNameTaken()
    {
        var f = new Fixture();
        f.Store.FailNextSave(new ConflictException(OrganizationErrorCodes.ProjectNameTaken));

        var refusal = await Assert.ThrowsAsync<ConflictException>(() => f.HandleAsync(f.Command("Платформа данных")));

        Assert.Equal(OrganizationErrorCodes.ProjectNameTaken, refusal.Code);
        Assert.Equal(1, f.Store.SaveCount);
    }
}
