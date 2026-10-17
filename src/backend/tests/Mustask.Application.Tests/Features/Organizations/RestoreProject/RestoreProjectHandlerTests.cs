using Mustask.Application.Features.Organizations.RestoreProject;
using Mustask.Application.Tests.TestDoubles;
using Mustask.Application.Tests.TestDoubles.Organizations;
using Mustask.Domain.Organizations;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Tests.Features.Organizations.RestoreProject;

/// <summary>
/// <c>RestoreProject</c> on fakes (api-contract § 2.7): the order of answers
/// (plan-backend § 5.3), the lock (§ 8.2) and what the restore writes, which is
/// the one row of the project (ADR-0026 rule 2).
/// </summary>
public sealed class RestoreProjectHandlerTests
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
            Handler = new RestoreProjectHandler(
                new FakeOrganizationReadModel(Store),
                new FakeProjectRepository(Store),
                new FakeUnitOfWork());
        }

        public InMemoryOrganizations Store { get; } = new();

        public Organization Organization { get; }

        public NewProject Project { get; }

        public Team Platform { get; }

        public Area Reports { get; }

        public Area Quarterly { get; }

        public RestoreProjectHandler Handler { get; }

        public RestoreProjectCommand Command(string? organizationId = null, string? projectId = null) =>
            new(organizationId ?? Organization.Id.ToString(), projectId ?? Project.Project.Id.ToString());

        public Task<RestoreProjectResult> HandleAsync(RestoreProjectCommand command) => Handler.Handle(command, CancellationToken.None);

        /// <summary>Everything beneath the project, as it is stored: the teams with their statuses and the whole area tree.</summary>
        public (string Teams, string Areas) Beneath() =>
            (
                string.Join(
                    ";",
                    Store.Teams.OrderBy(team => team.Id).Select(team => $"{team.Id}|{team.ProjectId}|{team.Name}|{team.Status}")),
                string.Join(
                    ";",
                    Store.Areas.OrderBy(area => area.Id).Select(area => $"{area.Id}|{area.ProjectId}|{area.ParentId}|{area.Name}")));
    }

    // US-3 · "A deactivated project is restored exactly as it was": back with its name, key, teams, default-team setting and area
    // tree as before. The restore writes one row, behind one lock, and saves once (ADR-0026 rule 2, plan-backend § 8.2).
    [Fact]
    public async Task Handle_DeactivatedProject_RestoresItExactly_WritingOnlyItsOwnRow()
    {
        var f = new Fixture();
        var before = f.Beneath();
        f.Project.Project.Deactivate();

        var result = await f.HandleAsync(f.Command());

        Assert.Equal(
            new ProjectResponse(f.Project.Project.Id, f.Organization.Id, "Пример проекта", "PRM"),
            result.Response);
        Assert.Equal(ProjectStatus.Active, f.Project.Project.Status);
        Assert.Equal(before, f.Beneath());
        Assert.Equal([$"lock project {f.Project.Project.Id}", "save"], f.Store.Calls);
    }

    // US-3 · "A deactivated project is restored exactly as it was": the team deleted before the deactivation stays deleted,
    // and the project's default-team setting is as it was (nothing beneath is read or written; mod-017 BR-OT-4).
    // Renamed (D-16, BR-OT-10): Handle_DeactivatedProject_KeepsTeamStatusesOwnersAndDefaults -> ..._KeepsTeamStatusesAndTheDefaultTeamSetting.
    [Fact]
    public async Task Handle_DeactivatedProject_KeepsTeamStatusesAndTheDefaultTeamSetting()
    {
        var f = new Fixture();
        var core = f.Store.AddTeam(f.Project, "Ядро");
        core.Delete(3);
        f.Project.Project.NameDefaultTeam(f.Platform.Id);
        f.Project.Project.Deactivate();

        await f.HandleAsync(f.Command());

        Assert.Equal(TeamStatus.Deleted, core.Status);
        Assert.Equal(f.Platform.Id, f.Project.Project.DefaultTeamId);
        Assert.Equal(TeamStatus.Active, f.Platform.Status);
        Assert.Equal(f.Project.Root.Id, f.Reports.ParentId);
    }

    // US-3 · "A deactivated project is restored exactly as it was": a rename made before the deactivation stands, and the key
    // is the one it was created with (BR-M1-1).
    [Fact]
    public async Task Handle_ProjectRenamedBeforeDeactivation_RestoresWithTheNewNameAndTheSameKey()
    {
        var f = new Fixture();
        f.Project.Project.Rename(InMemoryOrganizations.Name("Отчётность"), f.Project.Root);
        f.Project.Project.Deactivate();

        var result = await f.HandleAsync(f.Command());

        Assert.Equal("Отчётность", result.Response.Name);
        Assert.Equal("PRM", result.Response.Key);
        Assert.Equal("Отчётность", f.Project.Root.Name);
    }

    // api-contract § 2.7 · "Restoring an active project changes nothing and answers 200": the same shape, nothing written.
    [Fact]
    public async Task Handle_ActiveProject_IsANoOp_AnswersTheSameShape()
    {
        var f = new Fixture();
        var before = f.Beneath();

        var result = await f.HandleAsync(f.Command());

        Assert.Equal(
            new ProjectResponse(f.Project.Project.Id, f.Organization.Id, "Пример проекта", "PRM"),
            result.Response);
        Assert.Equal(ProjectStatus.Active, f.Project.Project.Status);
        Assert.Equal(before, f.Beneath());
        Assert.Equal(0, f.Store.SaveCount);
    }

    // The second of two restores in a row is the no-op of the first: the same answer, one write.
    [Fact]
    public async Task Handle_RestoredTwice_AnswersTheSameBothTimes_SavesOnce()
    {
        var f = new Fixture();
        f.Project.Project.Deactivate();

        var first = await f.HandleAsync(f.Command());
        var second = await f.HandleAsync(f.Command());

        Assert.Equal(first.Response, second.Response);
        Assert.Equal(1, f.Store.SaveCount);
    }

    // US-3 · restore "needs every container above it reachable" (ADR-0026 rule 5): under a deactivated organization the
    // answer is organization.not_found, the project stays deactivated, and no project is locked.
    [Fact]
    public async Task Handle_UnderDeactivatedOrganization_IsOrganizationNotFound_ProjectStaysDeactivated()
    {
        var f = new Fixture();
        f.Project.Project.Deactivate();
        f.Organization.Deactivate();

        var refusal = await Assert.ThrowsAsync<NotFoundException>(() => f.HandleAsync(f.Command()));

        Assert.Equal(OrganizationErrorCodes.OrganizationNotFound, refusal.Code);
        Assert.Equal(ProjectStatus.Deactivated, f.Project.Project.Status);
        Assert.Empty(f.Store.Calls);
    }

    // US-2 · "restored exactly as it was" and BR-M1-20: a project deactivated on its own stays so when its organization is
    // restored, and it can then be restored on its own.
    [Fact]
    public async Task Handle_AfterTheOrganizationIsRestored_RestoresTheProjectDeactivatedOnItsOwn()
    {
        var f = new Fixture();
        f.Project.Project.Deactivate();
        f.Organization.Deactivate();
        await Assert.ThrowsAsync<NotFoundException>(() => f.HandleAsync(f.Command()));

        f.Organization.Restore();
        Assert.Equal(ProjectStatus.Deactivated, f.Project.Project.Status);
        var result = await f.HandleAsync(f.Command());

        Assert.Equal(f.Project.Project.Id, result.Response.Id);
        Assert.Equal(ProjectStatus.Active, f.Project.Project.Status);
    }

    // BR-M1-20: behind the filter the organization id is well formed and active; if it were not, the outermost level answers,
    // before the project is looked at.
    [Theory]
    [InlineData("null")]
    [InlineData("malformed")]
    [InlineData("unknown")]
    public async Task Handle_OrganizationNotFound_IsOrganizationNotFound_BeforeTheProject(string how)
    {
        var f = new Fixture();
        f.Project.Project.Deactivate();
        var organizationId = how switch
        {
            "null" => null,
            "malformed" => "abc",
            _ => Guid.CreateVersion7().ToString(),
        };

        var refusal = await Assert.ThrowsAsync<NotFoundException>(() => f.HandleAsync(
            new RestoreProjectCommand(organizationId, "also-malformed")));

        Assert.Equal(OrganizationErrorCodes.OrganizationNotFound, refusal.Code);
        Assert.Equal(ProjectStatus.Deactivated, f.Project.Project.Status);
        Assert.Empty(f.Store.Calls);
    }

    // US-3 · "A project identifier that names no project of the organization" (BR-M1-20, BR-M1-22): null, malformed, unknown,
    // or the project of another organization answers project.not_found, and the other project is not touched.
    [Theory]
    [InlineData("null")]
    [InlineData("malformed")]
    [InlineData("empty id")]
    [InlineData("unknown")]
    [InlineData("of another organization")]
    public async Task Handle_ProjectNotOfTheOrganization_IsProjectNotFound_ChangesNothing(string how)
    {
        var f = new Fixture();
        var other = f.Store.AddProject(f.Store.AddOrganization("Другая организация"), "Чужой", "OTH");
        other.Project.Deactivate();
        var projectId = how switch
        {
            "null" => null,
            "malformed" => "PRM",
            "empty id" => Guid.Empty.ToString(),
            "unknown" => Guid.CreateVersion7().ToString(),
            _ => other.Project.Id.ToString(),
        };

        var refusal = await Assert.ThrowsAsync<NotFoundException>(() => f.HandleAsync(f.Command() with { ProjectId = projectId }));

        Assert.Equal(OrganizationErrorCodes.ProjectNotFound, refusal.Code);
        Assert.Equal(ProjectStatus.Deactivated, other.Project.Status);
        Assert.Equal(0, f.Store.SaveCount);
    }

    // api-contract § 0.2: the server accepts the canonical form of an id in either letter case.
    [Fact]
    public async Task Handle_IdsInUpperCase_AreAccepted()
    {
        var f = new Fixture();
        f.Project.Project.Deactivate();

        var result = await f.HandleAsync(f.Command(
            organizationId: f.Organization.Id.ToString().ToUpperInvariant(),
            projectId: f.Project.Project.Id.ToString().ToUpperInvariant()));

        Assert.Equal(f.Project.Project.Id, result.Response.Id);
        Assert.Equal(ProjectStatus.Active, f.Project.Project.Status);
    }
}
