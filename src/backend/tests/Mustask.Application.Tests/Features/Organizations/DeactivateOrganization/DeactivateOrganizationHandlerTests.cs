using Mustask.Application.Features.Organizations.DeactivateOrganization;
using Mustask.Application.Tests.TestDoubles;
using Mustask.Application.Tests.TestDoubles.Organizations;
using Mustask.Domain.Identity;
using Mustask.Domain.Organizations;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Tests.Features.Organizations.DeactivateOrganization;

/// <summary>
/// Deactivating an organization on fakes (US-2, api-contract § 1.6): the one
/// row it writes, the lists it moves between, what it leaves alone beneath, the
/// single answer it can refuse with, and its lock (plan-backend § 8.2).
/// </summary>
public sealed class DeactivateOrganizationHandlerTests
{
    /// <summary>The user whose lists are read back (the lists hold the organizations of the caller, mod-016 BR-M1-7).</summary>
    private const string Lister = "u-lister";

    private sealed class Fixture
    {
        public Fixture()
        {
            Organization = Store.AddOrganization();
            Store.AddMember(Organization, Lister, OrganizationRole.Admin);
            Project = Store.AddProject(Organization);
            Platform = Store.AddLinkedTeam(Organization, Project);
            Project.NameDefaultTeam(Platform.Id);
            Reports = Store.AddArea(Organization, "Отчёты");
            Handler = new DeactivateOrganizationHandler(new FakeOrganizationRepository(Store), new FakeUnitOfWork());
            ReadModel = new FakeOrganizationReadModel(Store);
        }

        public InMemoryOrganizations Store { get; } = new();

        public Organization Organization { get; }

        public Project Project { get; }

        public Team Platform { get; }

        public Area Reports { get; }

        public DeactivateOrganizationHandler Handler { get; }

        public FakeOrganizationReadModel ReadModel { get; }

        public Task HandleAsync(string? organizationId = null) =>
            Handler.Handle(new DeactivateOrganizationCommand(organizationId ?? Organization.Id.ToString()), CancellationToken.None);

        /// <summary>Everything beneath the organization as the store holds it: ids and the status each row carries.</summary>
        public string[] Beneath() =>
        [
            .. Store.Projects.Select(project => $"project {project.Id} {project.Status} default={project.DefaultTeamId}"),
            .. Store.Teams.Select(team => $"team {team.Id} {team.Status}"),
            .. Store.Areas.Select(area => $"area {area.Id} parent={area.ParentId}"),
        ];
    }

    // US-2 · "The organization is deactivated": it is no longer among the active organizations and is among the deactivated ones.
    [Fact]
    public async Task Handle_ActiveOrganization_LeavesTheActiveList_JoinsTheDeactivatedList()
    {
        var f = new Fixture();
        var other = f.Store.AddOrganizationOf(Lister, "Другая организация");

        await f.HandleAsync();

        Assert.Equal(OrganizationStatus.Deactivated, f.Organization.Status);
        Assert.DoesNotContain(await f.ReadModel.ListAsync(OrganizationStatus.Active, Lister, null, CancellationToken.None), row => row.Id == f.Organization.Id);
        Assert.Contains(await f.ReadModel.ListAsync(OrganizationStatus.Deactivated, Lister, null, CancellationToken.None), row => row.Id == f.Organization.Id && row.Name == f.Organization.Name);
        Assert.Contains(await f.ReadModel.ListAsync(OrganizationStatus.Active, Lister, null, CancellationToken.None), row => row.Id == other.Id);
        Assert.Null(await f.ReadModel.FindActiveAsync(f.Organization.Id, CancellationToken.None));
    }

    // US-2 · "And nothing under it is deleted" (BR-M1-20, ADR-0026 rule 2): a project deactivated on its own stays deactivated,
    // every team, area and the default-team setting is as it was, and nothing beneath gets a status of its own.
    [Fact]
    public async Task Handle_ActiveOrganization_DeletesNothing_ChangesNothingBeneath()
    {
        var f = new Fixture();
        var old = f.Store.AddProject(f.Organization, "Старый проект", "OLD");
        old.Deactivate();
        f.Store.AddArea(f.Organization, "Архив");
        var before = f.Beneath();

        await f.HandleAsync();

        Assert.Equal(before, f.Beneath());
        Assert.Equal(2, f.Store.Projects.Count);
        Assert.Equal(ProjectStatus.Active, f.Project.Status);
        Assert.Equal(ProjectStatus.Deactivated, old.Status);
        Assert.All(f.Store.Teams, team => Assert.Equal(TeamStatus.Active, team.Status));
        Assert.Equal(f.Platform.Id, f.Project.DefaultTeamId);
        Assert.Contains(f.Store.Organizations, organization => organization.Id == f.Organization.Id);
    }

    // US-2 · "A deactivated organization is listed separately, with no purge": its name, process and creator are kept, so its
    // name is still taken for new organizations (BR-M1-1, US-1).
    [Fact]
    public async Task Handle_ActiveOrganization_KeepsItsNameProcessAndCreator_NameStaysTaken()
    {
        var f = new Fixture();
        var (name, normalized, processId, createdBy) = (f.Organization.Name, f.Organization.NormalizedName, f.Organization.ProcessId, f.Organization.CreatedBy);

        await f.HandleAsync();

        Assert.Equal((name, normalized, processId, createdBy), (f.Organization.Name, f.Organization.NormalizedName, f.Organization.ProcessId, f.Organization.CreatedBy));
        Assert.Equal(name, await new FakeOrganizationRepository(f.Store).FindNameHolderAsync(normalized, exceptId: null, CancellationToken.None));
    }

    // US-2 · "The organization is deactivated": the request writes the addressed organization only; another one, and its
    // projects, stay active.
    [Fact]
    public async Task Handle_OneOrganization_LeavesTheOthersActive()
    {
        var f = new Fixture();
        var other = f.Store.AddOrganization("Другая организация");
        f.Store.AddProject(other, "Чужой проект", "OTH");

        await f.HandleAsync();

        Assert.Equal(OrganizationStatus.Active, other.Status);
        Assert.All(f.Store.Projects.Where(project => project.OrganizationId == other.Id), project => Assert.Equal(ProjectStatus.Active, project.Status));
    }

    // US-2 · "An unknown organization answers not found" (BR-M1-20): behind the filter the id is well formed; the handler
    // answers for the malformed ones all the same, and touches nothing.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    [InlineData("{5d1e8f3a-0000-4000-8000-00000000c0de}")]
    public async Task Handle_MalformedOrganization_IsOrganizationNotFound_TouchesNothing(string? organizationId)
    {
        var f = new Fixture();
        var before = f.Beneath();

        // Built here, not through HandleAsync, whose default would replace a null with the fixture's own id.
        var refusal = await Assert.ThrowsAsync<NotFoundException>(() =>
            f.Handler.Handle(new DeactivateOrganizationCommand(organizationId), CancellationToken.None));

        Assert.Equal(OrganizationErrorCodes.OrganizationNotFound, refusal.Code);
        Assert.Empty(f.Store.Calls);
        Assert.Equal(OrganizationStatus.Active, f.Organization.Status);
        Assert.Equal(before, f.Beneath());
    }

    // US-2 · "An unknown organization answers not found": a well-formed id that names nothing; no save is made.
    [Fact]
    public async Task Handle_UnknownOrganization_IsOrganizationNotFound_SavesNothing()
    {
        var f = new Fixture();

        var refusal = await Assert.ThrowsAsync<NotFoundException>(() => f.HandleAsync(Guid.CreateVersion7().ToString()));

        Assert.Equal(OrganizationErrorCodes.OrganizationNotFound, refusal.Code);
        Assert.Equal(0, f.Store.SaveCount);
        Assert.Equal(OrganizationStatus.Active, f.Organization.Status);
    }

    // US-2 · "A deactivated organization takes no change ... deactivates ... anything under it" and api-contract § 1.6
    // ("already deactivated included"): a second deactivation is a not-found, and writes nothing. This is also what a
    // deactivation that waited for the first one's commit meets after its lock.
    [Fact]
    public async Task Handle_AlreadyDeactivated_IsOrganizationNotFound_SavesNothing()
    {
        var f = new Fixture();
        await f.HandleAsync();
        f.Store.Calls.Clear();

        var refusal = await Assert.ThrowsAsync<NotFoundException>(() => f.HandleAsync());

        Assert.Equal(OrganizationErrorCodes.OrganizationNotFound, refusal.Code);
        Assert.Equal(0, f.Store.SaveCount);
        Assert.Equal(OrganizationStatus.Deactivated, f.Organization.Status);
    }

    // BR-M1-20 · the filter saw an active organization, but it was deactivated before the lock was granted: the row is read
    // after the lock, so the outcome is the committed one, and nothing is saved.
    [Fact]
    public async Task Handle_DeactivatedBeforeTheLock_IsOrganizationNotFound()
    {
        var f = new Fixture();
        f.Organization.Deactivate();

        var refusal = await Assert.ThrowsAsync<NotFoundException>(() => f.HandleAsync());

        Assert.Equal(OrganizationErrorCodes.OrganizationNotFound, refusal.Code);
        Assert.Equal([$"lock organization {f.Organization.Id}"], f.Store.Calls);
        Assert.Equal(0, f.Store.SaveCount);
    }

    // plan-backend § 8.2: the organization FOR NO KEY UPDATE, then one save, nothing else read or locked.
    [Fact]
    public async Task Handle_LocksTheOrganization_ThenSavesOnce()
    {
        var f = new Fixture();

        await f.HandleAsync();

        Assert.Equal([$"lock organization {f.Organization.Id}", "save"], f.Store.Calls);
    }

    // plan-backend § 5.3: a handler catches nothing else, so an infrastructure failure of the save stays a 500.
    [Fact]
    public async Task Handle_SaveFails_TheFailureIsNotSwallowed()
    {
        var f = new Fixture();
        f.Store.FailNextSave(new InvalidOperationException("the database is down"));

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => f.HandleAsync());

        Assert.Equal("the database is down", failure.Message);
    }
}
