using Mustask.Application.Features.Organizations.RestoreOrganization;
using Mustask.Application.Tests.TestDoubles;
using Mustask.Application.Tests.TestDoubles.Organizations;
using Mustask.Domain.Organizations;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Tests.Features.Organizations.RestoreOrganization;

/// <summary>
/// <c>RestoreOrganization</c> on fakes (plan-backend § 14.3, lane L-7): that it
/// writes the one row and nothing beneath it (ADR-0026 rule 2), that it restores
/// exactly as it was (US-2), and what it answers for an address that names no
/// organization (BR-M1-20).
/// </summary>
public sealed class RestoreOrganizationHandlerTests
{
    private sealed class Fixture
    {
        public Fixture()
        {
            Organization = Store.AddOrganization(" Пример организации ");
            Project = Store.AddProject(Organization);
            Platform = Store.AddLinkedTeam(Organization, Project);
            Project.Project.NameDefaultTeam(Platform.Id);
            Reports = Store.AddArea(Project.Root, "Отчёты");
            Quarterly = Store.AddArea(Reports, "Квартальные");
            Handler = new RestoreOrganizationHandler(new FakeOrganizationRepository(Store), new FakeUnitOfWork());
        }

        public InMemoryOrganizations Store { get; } = new();

        public Organization Organization { get; }

        public NewProject Project { get; }

        public Team Platform { get; }

        public Area Reports { get; }

        public Area Quarterly { get; }

        public RestoreOrganizationHandler Handler { get; }

        public RestoreOrganizationCommand Command() => new(Organization.Id.ToString());

        public Task<RestoreOrganizationResult> HandleAsync(RestoreOrganizationCommand command) =>
            Handler.Handle(command, CancellationToken.None);

        public string Locked() => $"lock organization {Organization.Id}";
    }

    /// <summary>
    /// Everything beneath an organization as stored: statuses, names and keys.
    /// A restore must leave it exactly as it found it.
    /// </summary>
    private static IReadOnlyList<string> Beneath(InMemoryOrganizations store) =>
    [
        .. store.Projects.Select(project =>
            $"project {project.Id} of {project.OrganizationId}: {project.Name} / {project.NormalizedName} / {project.Key} / {project.Status} / {project.CreatedBy}"),
        .. store.Teams.Select(team =>
            $"team {team.Id} of {team.OrganizationId}: {team.Name} / {team.NormalizedName} / {team.Status}"),
        .. store.Areas.Select(area =>
            $"area {area.Id} of {area.ProjectId} under {area.ParentId}: {area.Name}"),
    ];

    // US-2 · "The organization is restored exactly as it was": it is back, under its stored name, with the answer the
    // contract gives (api-contract § 1.7), and the request locked the organization's row and saved once.
    [Fact]
    public async Task Handle_DeactivatedOrganization_BecomesActive_AnswersItsIdAndStoredName()
    {
        var f = new Fixture();
        f.Organization.Deactivate();

        var result = await f.HandleAsync(f.Command());

        Assert.Equal(new OrganizationResponse(f.Organization.Id, "Пример организации"), result.Response);
        Assert.Equal(OrganizationStatus.Active, f.Organization.Status);
        Assert.Equal([f.Locked(), "save"], f.Store.Calls);
    }

    // US-2 · "...with its name, process, projects, teams, default-team setting and area trees as before": the handler writes the
    // organization's row alone, so nothing beneath it, and not the name or the process either, differs from before.
    [Fact]
    public async Task Handle_Restores_NameProcessProjectsTeamsDefaultsAndTreesExactlyAsTheyWere()
    {
        var f = new Fixture();
        var (name, normalizedName, processId, createdBy) =
            (f.Organization.Name, f.Organization.NormalizedName, f.Organization.ProcessId, f.Organization.CreatedBy);
        f.Organization.Deactivate();
        var beneathBefore = Beneath(f.Store);

        await f.HandleAsync(f.Command());

        Assert.Equal((name, normalizedName, processId, createdBy), (f.Organization.Name, f.Organization.NormalizedName, f.Organization.ProcessId, f.Organization.CreatedBy));
        Assert.Equal(beneathBefore, Beneath(f.Store));

        // The default-team setting: the project names «Платформа», and the restore leaves it so.
        Assert.Equal(f.Platform.Id, f.Project.Project.DefaultTeamId);
        Assert.Equal(TeamStatus.Active, f.Platform.Status);

        // The tree answers as before, through reachability alone.
        Assert.True(f.Store.IsReachable(f.Organization.Id, f.Project.Project.Id));
        var tree = new AreaTree(f.Store.Areas.Select(area => new AreaNode(area.Id, area.ParentId, area.Name)));
        Assert.Equal("Отчёты / Квартальные", tree.PathOf(f.Quarterly.Id));
    }

    // US-2 · "...«Старый проект» is still deactivated and is listed among the organization's deactivated projects":
    // a restore does not reactivate what was deactivated on its own (BR-M1-20); the other project is reachable again.
    [Fact]
    public async Task Handle_ProjectDeactivatedOnItsOwn_StaysDeactivated_OtherProjectIsReachableAgain()
    {
        var f = new Fixture();
        var old = f.Store.AddProject(f.Organization, "Старый проект", "OLD");
        old.Project.Deactivate();
        f.Organization.Deactivate();

        await f.HandleAsync(f.Command());

        Assert.Equal(ProjectStatus.Deactivated, old.Project.Status);
        Assert.False(f.Store.IsReachable(f.Organization.Id, old.Project.Id));
        Assert.Equal(ProjectStatus.Active, f.Project.Project.Status);
        Assert.True(f.Store.IsReachable(f.Organization.Id, f.Project.Project.Id));
        Assert.Equal(TeamStatus.Active, f.Platform.Status);
    }

    // US-2 · "...restores it": only the addressed organization comes back; another deactivated one stays deactivated.
    [Fact]
    public async Task Handle_RestoresOnlyTheAddressedOrganization()
    {
        var f = new Fixture();
        var other = f.Store.AddOrganization("Другая организация");
        other.Deactivate();
        f.Organization.Deactivate();

        await f.HandleAsync(f.Command());

        Assert.Equal(OrganizationStatus.Active, f.Organization.Status);
        Assert.Equal(OrganizationStatus.Deactivated, other.Status);
    }

    // api-contract § 1.7 · "Restoring an active organization changes nothing and answers 200": the same answer, the same
    // state, and a second restore of the same organization answers the same again.
    [Fact]
    public async Task Handle_ActiveOrganization_IsANoOp_AnswersTheSame_AlsoTheSecondTime()
    {
        var f = new Fixture();
        var beneathBefore = Beneath(f.Store);

        var first = await f.HandleAsync(f.Command());
        var second = await f.HandleAsync(f.Command());

        Assert.Equal(new OrganizationResponse(f.Organization.Id, "Пример организации"), first.Response);
        Assert.Equal(first.Response, second.Response);
        Assert.Equal(OrganizationStatus.Active, f.Organization.Status);
        Assert.Equal(beneathBefore, Beneath(f.Store));
        Assert.Equal("Пример организации", f.Organization.Name);
    }

    // api-contract § 0.2: an id is accepted in either letter case.
    [Fact]
    public async Task Handle_UpperCaseId_Restores()
    {
        var f = new Fixture();
        f.Organization.Deactivate();

        var result = await f.HandleAsync(new RestoreOrganizationCommand(f.Organization.Id.ToString().ToUpperInvariant()));

        Assert.Equal(f.Organization.Id, result.Response.Id);
        Assert.Equal(OrganizationStatus.Active, f.Organization.Status);
    }

    // US-2 · "An unknown organization answers not found" (BR-M1-20): a value that is not a well-formed identifier is
    // answered before the store is touched, and nothing changes.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abc")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    [InlineData("3f2504e04f8911d39a0c0305e82c3301")]
    [InlineData("{3f2504e0-4f89-11d3-9a0c-0305e82c3301}")]
    public async Task Handle_MalformedId_IsOrganizationNotFound_BeforeAnyLock(string? organizationId)
    {
        var f = new Fixture();
        f.Organization.Deactivate();

        var refusal = await Assert.ThrowsAsync<NotFoundException>(() => f.HandleAsync(new RestoreOrganizationCommand(organizationId)));

        Assert.Equal(OrganizationErrorCodes.OrganizationNotFound, refusal.Code);
        Assert.Empty(f.Store.Calls);
        Assert.Equal(OrganizationStatus.Deactivated, f.Organization.Status);
    }

    // US-2 · "An unknown organization answers not found": a well-formed id that names no organization is answered after
    // the lock finds nothing, and nothing is saved or restored.
    [Fact]
    public async Task Handle_UnknownOrganization_IsOrganizationNotFound_SavesNothing()
    {
        var f = new Fixture();
        f.Organization.Deactivate();
        var unknown = Guid.CreateVersion7();

        var refusal = await Assert.ThrowsAsync<NotFoundException>(() => f.HandleAsync(new RestoreOrganizationCommand(unknown.ToString())));

        Assert.Equal(OrganizationErrorCodes.OrganizationNotFound, refusal.Code);
        Assert.Equal([$"lock organization {unknown}"], f.Store.Calls);
        Assert.Equal(OrganizationStatus.Deactivated, f.Organization.Status);
    }

    // plan-backend § 5.3 · "A handler catches nothing else, so an infrastructure failure stays a 500": a failed save
    // leaves the handler as it is, with no business code.
    [Fact]
    public async Task Handle_SaveFails_PropagatesTheFailure()
    {
        var f = new Fixture();
        f.Organization.Deactivate();
        f.Store.FailNextSave(new InvalidOperationException("The database is down."));

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => f.HandleAsync(f.Command()));

        Assert.Equal("The database is down.", failure.Message);
    }
}
