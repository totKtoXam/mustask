using Mustask.Application.Abstractions.Organizations;
using Mustask.Application.Features.Organizations.GetProjects;
using Mustask.Application.Tests.TestDoubles.Organizations;
using Mustask.Domain.Organizations;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Tests.Features.Organizations.GetProjects;

/// <summary>
/// <c>GET /api/organizations/{organizationId}/projects</c> on fakes (api-contract
/// § 2.1, plan-backend § 5.3): what the list holds, the order it keeps, and the
/// one refusal the route has, the organization level.
/// </summary>
public sealed class GetProjectsHandlerTests
{
    private sealed class Fixture
    {
        public Fixture()
        {
            Organization = Store.AddOrganization();
            Handler = new GetProjectsHandler(new FakeOrganizationReadModel(Store), new FakeProjectReadModel(Store));
        }

        public InMemoryOrganizations Store { get; } = new();

        public Organization Organization { get; }

        public GetProjectsHandler Handler { get; }

        public Task<GetProjectsResult> HandleAsync(string? organizationId = null) =>
            Handler.Handle(new GetProjectsQuery(organizationId ?? Organization.Id.ToString()), CancellationToken.None);
    }

    /// <summary>Returns the rows it was built with, in that order, and remembers what it was asked for.</summary>
    private sealed class FixedOrderProjectReadModel(IReadOnlyList<ProjectRow> rows) : IProjectReadModel
    {
        public List<(Guid OrganizationId, ProjectStatus Status)> Asked { get; } = [];

        public Task<IReadOnlyList<ProjectRow>> ListAsync(Guid organizationId, ProjectStatus status, CancellationToken cancellationToken)
        {
            Asked.Add((organizationId, status));

            return Task.FromResult(rows);
        }

        public Task<ProjectRow?> FindReachableAsync(Guid organizationId, Guid projectId, CancellationToken cancellationToken) =>
            throw new NotSupportedException("The list reads no single project.");
    }

    // US-1 · "created with its process": a new organization has no project yet, and the list is empty, not a refusal.
    [Fact]
    public async Task Handle_NewOrganization_ReturnsAnEmptyList()
    {
        var f = new Fixture();

        var result = await f.HandleAsync();

        Assert.Empty(result.Items);
    }

    // US-3 · the list shows each project's name and key; US-8 · «Аналитика» and «Пример проекта» in alphabetical order.
    [Fact]
    public async Task Handle_ReturnsNameAndKeyOfEachProject_InDisplayOrder()
    {
        var f = new Fixture();
        var example = f.Store.AddProject(f.Organization, "Пример проекта", "PRM");
        var analytics = f.Store.AddProject(f.Organization, "Аналитика", "ANL");

        var result = await f.HandleAsync();

        Assert.Equal(
            [
                new ProjectListItemResponse(analytics.Project.Id, "Аналитика", "ANL"),
                new ProjectListItemResponse(example.Project.Id, "Пример проекта", "PRM"),
            ],
            result.Items);
    }

    // US-8 · "Lists in order": letters before letter case, so «альфа» comes before «Бета» although a lower-case letter sorts
    // after an upper-case one in code point order. The fake orders as the database does.
    [Fact]
    public async Task Handle_MixedCaseNames_AreOrderedByLetterNotByCase()
    {
        var f = new Fixture();
        f.Store.AddProject(f.Organization, "Бета", "BET");
        f.Store.AddProject(f.Organization, "альфа", "ALF");
        f.Store.AddProject(f.Organization, "Аналитика", "ANL");

        var result = await f.HandleAsync();

        Assert.Equal(["альфа", "Аналитика", "Бета"], result.Items.Select(item => item.Name));
    }

    // api-contract § 0.6: "The server orders every list, and the client renders it in the order received": the handler
    // does not sort again, so what the read model puts first stays first. It asks for the active projects of the
    // addressed organization, and for nothing else.
    [Fact]
    public async Task Handle_KeepsTheOrderOfTheReadModel_AndAsksForActiveProjectsOfTheOrganization()
    {
        var store = new InMemoryOrganizations();
        var organization = store.AddOrganization();
        var rows = new List<ProjectRow>
        {
            new(Guid.CreateVersion7(), organization.Id, "Ёлка", "YOL"),
            new(Guid.CreateVersion7(), organization.Id, "Ель", "EL"),
            new(Guid.CreateVersion7(), organization.Id, "Бета", "BET"),
            new(Guid.CreateVersion7(), organization.Id, "альфа", "ALF"),
        };
        var projects = new FixedOrderProjectReadModel(rows);
        var handler = new GetProjectsHandler(new FakeOrganizationReadModel(store), projects);

        var result = await handler.Handle(new GetProjectsQuery(organization.Id.ToString()), CancellationToken.None);

        Assert.Equal(rows.Select(row => row.Id), result.Items.Select(item => item.Id));
        Assert.Equal(rows.Select(row => row.Key), result.Items.Select(item => item.Key));
        Assert.Equal([(organization.Id, ProjectStatus.Active)], projects.Asked);
    }

    // US-3 · "A project is deactivated": it is no longer among the active projects (BR-M1-24); the others stay.
    // US-3 · another organization's projects are not listed (BR-M1-22).
    [Fact]
    public async Task Handle_LeavesOutDeactivatedProjectsAndThoseOfOtherOrganizations()
    {
        var f = new Fixture();
        var active = f.Store.AddProject(f.Organization, "Пример проекта", "PRM");
        var deactivated = f.Store.AddProject(f.Organization, "Архив", "ARC");
        deactivated.Project.Deactivate();
        f.Store.AddProject(f.Store.AddOrganization("Другая организация"), "Чужой проект", "OTH");

        var result = await f.HandleAsync();

        var item = Assert.Single(result.Items);
        Assert.Equal(active.Project.Id, item.Id);
        Assert.Equal("Пример проекта", item.Name);
    }

    // US-3 · a restored project is listed again, a project deactivated on its own does not take the others with it.
    [Fact]
    public async Task Handle_ProjectRestored_IsListedAgain()
    {
        var f = new Fixture();
        var restored = f.Store.AddProject(f.Organization, "Пример проекта", "PRM");
        f.Store.AddProject(f.Organization, "Аналитика", "ANL");
        restored.Project.Deactivate();
        Assert.Single((await f.HandleAsync()).Items);

        restored.Project.Restore();
        var result = await f.HandleAsync();

        Assert.Equal(["Аналитика", "Пример проекта"], result.Items.Select(item => item.Name));
    }

    // BR-M1-20: a malformed organization id answers like one that names nothing; behind the filter it is well formed,
    // but the handler is not the filter's only check (plan-backend § 7.3).
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-an-id")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public async Task Handle_MalformedOrganization_IsOrganizationNotFound(string? organizationId)
    {
        var f = new Fixture();
        f.Store.AddProject(f.Organization, "Пример проекта", "PRM");

        var refusal = await Assert.ThrowsAsync<NotFoundException>(() => f.Handler.Handle(new GetProjectsQuery(organizationId), CancellationToken.None));

        Assert.Equal(OrganizationErrorCodes.OrganizationNotFound, refusal.Code);
    }

    // US-2 · "A deactivated organization takes no change; unknown answers not found": the list does not tell an empty
    // organization from one that is not reachable, so a deactivated or unknown organization is not an empty list.
    [Theory]
    [InlineData("unknown")]
    [InlineData("deactivated")]
    public async Task Handle_OrganizationUnknownOrDeactivated_IsOrganizationNotFound_NotAnEmptyList(string how)
    {
        var f = new Fixture();
        f.Store.AddProject(f.Organization, "Пример проекта", "PRM");

        if (how == "deactivated")
        {
            f.Organization.Deactivate();
        }

        var organizationId = how == "unknown" ? Guid.CreateVersion7().ToString() : f.Organization.Id.ToString();

        var refusal = await Assert.ThrowsAsync<NotFoundException>(() => f.HandleAsync(organizationId));

        Assert.Equal(OrganizationErrorCodes.OrganizationNotFound, refusal.Code);
    }

    // US-2 · "restored exactly as it was": the projects of a restored organization are listed again, the one that was
    // deactivated on its own stays out.
    [Fact]
    public async Task Handle_OrganizationRestored_ListsItsActiveProjectsAgain()
    {
        var f = new Fixture();
        f.Store.AddProject(f.Organization, "Пример проекта", "PRM");
        f.Store.AddProject(f.Organization, "Архив", "ARC").Project.Deactivate();
        f.Organization.Deactivate();
        await Assert.ThrowsAsync<NotFoundException>(() => f.HandleAsync());

        f.Organization.Restore();
        var result = await f.HandleAsync();

        Assert.Equal(["Пример проекта"], result.Items.Select(item => item.Name));
    }
}
