using Mustask.Application.Features.Organizations.GetDeactivatedProjects;
using Mustask.Application.Tests.TestDoubles.Organizations;
using Mustask.Domain.Organizations;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Tests.Features.Organizations.GetDeactivatedProjects;

/// <summary>
/// The organization's deactivated projects on fakes (plan-backend § 13, lane
/// L-9): which projects it lists, that it answers for the organization before
/// it reads anything, and that it changes nothing.
/// </summary>
public sealed class GetDeactivatedProjectsHandlerTests
{
    private sealed class Fixture
    {
        public Fixture()
        {
            Organization = Store.AddOrganization();
            Handler = new GetDeactivatedProjectsHandler(new FakeOrganizationReadModel(Store), new FakeProjectReadModel(Store));
        }

        public InMemoryOrganizations Store { get; } = new();

        public Organization Organization { get; }

        public GetDeactivatedProjectsHandler Handler { get; }

        /// <summary>A project of <see cref="Organization"/> (or of the given one), deactivated when asked.</summary>
        public NewProject AddProject(string name, string key, bool deactivated, Organization? organization = null)
        {
            var project = Store.AddProject(organization ?? Organization, name, key);

            if (deactivated)
            {
                project.Project.Deactivate();
            }

            return project;
        }

        public Task<GetDeactivatedProjectsResult> HandleAsync(string? organizationId = null) =>
            Handler.Handle(new GetDeactivatedProjectsQuery(organizationId ?? Organization.Id.ToString()), CancellationToken.None);
    }

    // US-3 · "A deactivated project is restored exactly as it was": the user opens the organization's deactivated projects
    // and finds there the deactivated one, not the active one (api-contract § 2.2).
    [Fact]
    public async Task Handle_ActiveAndDeactivatedProjects_ListsTheDeactivatedOnly()
    {
        var f = new Fixture();
        f.AddProject("Действующий", "ACT", deactivated: false);
        var archived = f.AddProject("Архивный", "ARC", deactivated: true);

        var result = await f.HandleAsync();

        Assert.Equal([new ProjectListItemResponse(archived.Project.Id, "Архивный", "ARC")], result.Items);
    }

    // US-3 · same scenario, nothing deactivated yet: the answer is an empty list, not a refusal.
    [Fact]
    public async Task Handle_NoDeactivatedProject_ReturnsEmpty()
    {
        var f = new Fixture();
        f.AddProject("Действующий", "ACT", deactivated: false);

        var result = await f.HandleAsync();

        Assert.Empty(result.Items);
    }

    // US-3 · "A deactivated project is restored": once restored it leaves the deactivated list (BR-M1-24).
    [Fact]
    public async Task Handle_RestoredProject_LeavesTheList()
    {
        var f = new Fixture();
        var project = f.AddProject("Архивный", "ARC", deactivated: true);
        Assert.Single((await f.HandleAsync()).Items);

        project.Project.Restore();

        Assert.Empty((await f.HandleAsync()).Items);
    }

    // BR-M1-22: a deactivated project of another organization is not in this organization's list, and this one's is not in theirs.
    [Fact]
    public async Task Handle_DeactivatedProjectOfAnotherOrganization_IsNotListed()
    {
        var f = new Fixture();
        var other = f.Store.AddOrganization("Другая организация");
        var mine = f.AddProject("Мой архивный", "MINE", deactivated: true);
        var theirs = f.AddProject("Чужой архивный", "THEIRS", deactivated: true, other);

        var result = await f.HandleAsync();
        var theirResult = await f.HandleAsync(other.Id.ToString());

        Assert.Equal([mine.Project.Id], result.Items.Select(item => item.Id));
        Assert.Equal([theirs.Project.Id], theirResult.Items.Select(item => item.Id));
    }

    // api-contract § 0.6: the server orders the list and the handler keeps that order, whatever the order of creation.
    [Fact]
    public async Task Handle_SeveralDeactivatedProjects_KeepsTheReadModelsOrder()
    {
        var f = new Fixture();
        f.AddProject("Gamma", "GAM", deactivated: true);
        f.AddProject("alpha", "ALP", deactivated: true);
        f.AddProject("Beta", "BET", deactivated: true);

        var result = await f.HandleAsync();

        Assert.Equal(["alpha", "Beta", "Gamma"], result.Items.Select(item => item.Name));
    }

    // BR-M1-20: behind the filter the organization id is well formed; if it were not, or the organization were not
    // there, the outermost level answers, never an empty list.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public async Task Handle_MalformedOrganization_IsOrganizationNotFound(string? organizationId)
    {
        var f = new Fixture();
        f.AddProject("Архивный", "ARC", deactivated: true);

        var refusal = await Assert.ThrowsAsync<NotFoundException>(() =>
            f.Handler.Handle(new GetDeactivatedProjectsQuery(organizationId), CancellationToken.None));

        Assert.Equal(OrganizationErrorCodes.OrganizationNotFound, refusal.Code);
    }

    // US-2 · "A deactivated organization takes no change; an unknown one answers not found" and ADR-0026 rule 5: an
    // organization that is unknown or deactivated is never revealed as such, and its deactivated projects are not listed.
    [Theory]
    [InlineData("unknown")]
    [InlineData("deactivated")]
    public async Task Handle_UnknownOrDeactivatedOrganization_IsOrganizationNotFound(string how)
    {
        var f = new Fixture();
        f.AddProject("Архивный", "ARC", deactivated: true);
        var organizationId = f.Organization.Id.ToString();

        if (how == "unknown")
        {
            organizationId = Guid.CreateVersion7().ToString();
        }
        else
        {
            f.Organization.Deactivate();
        }

        var refusal = await Assert.ThrowsAsync<NotFoundException>(() => f.HandleAsync(organizationId));

        Assert.Equal(OrganizationErrorCodes.OrganizationNotFound, refusal.Code);
    }

    // A read changes nothing: no lock, no save, and the statuses of the stored rows stay as they were.
    [Fact]
    public async Task Handle_ChangesNothing()
    {
        var f = new Fixture();
        var active = f.AddProject("Действующий", "ACT", deactivated: false);
        var archived = f.AddProject("Архивный", "ARC", deactivated: true);

        await f.HandleAsync();

        Assert.Empty(f.Store.Calls);
        Assert.Equal(ProjectStatus.Active, active.Project.Status);
        Assert.Equal(ProjectStatus.Deactivated, archived.Project.Status);
        Assert.Equal(OrganizationStatus.Active, f.Organization.Status);
    }
}
