using Mustask.Application.Abstractions.Organizations;
using Mustask.Application.Features.Organizations.GetDeactivatedOrganizations;
using Mustask.Application.Tests.TestDoubles.Organizations;
using Mustask.Domain.Organizations;

namespace Mustask.Application.Tests.Features.Organizations.GetDeactivatedOrganizations;

/// <summary>
/// <c>GET /api/organizations/deactivated</c> on fakes (plan-backend § 14.3,
/// lane L-2): which organizations the list holds, in what order, and that the
/// handler asks the read model for exactly the deactivated status. The route
/// has no address and no body, so there is no order of answers to pin.
/// </summary>
public sealed class GetDeactivatedOrganizationsHandlerTests
{
    private static GetDeactivatedOrganizationsHandler HandlerOver(InMemoryOrganizations store) =>
        new(new FakeOrganizationReadModel(store));

    private static Task<GetDeactivatedOrganizationsResult> HandleAsync(GetDeactivatedOrganizationsHandler handler) =>
        handler.Handle(new GetDeactivatedOrganizationsQuery(), CancellationToken.None);

    // US-2 · "A deactivated organization is listed separately, with no purge": «Пример организации» is among the
    // deactivated ones, the active ones are not.
    [Fact]
    public async Task Handle_ActiveAndDeactivated_ListsTheDeactivatedOnly()
    {
        var store = new InMemoryOrganizations();
        var active = store.AddOrganization("Активная");
        var deactivated = store.AddOrganization("Пример организации");
        deactivated.Deactivate();

        var result = await HandleAsync(HandlerOver(store));

        var item = Assert.Single(result.Items);
        Assert.Equal(new OrganizationListItemResponse(deactivated.Id, "Пример организации"), item);
        Assert.DoesNotContain(result.Items, listed => listed.Id == active.Id);
    }

    // US-2 · "The organization is deactivated" (BR-M1-24): it leaves the active list and joins this one; nothing beneath it
    // is read or changed (ADR-0026 rule 2), so its project does not matter to the list.
    [Fact]
    public async Task Handle_DeactivatedWithProjectsBeneath_IsListedAndTheProjectsAreUntouched()
    {
        var store = new InMemoryOrganizations();
        var organization = store.AddOrganization();
        var project = store.AddProject(organization);
        store.AddTeam(project);
        organization.Deactivate();

        var result = await HandleAsync(HandlerOver(store));

        Assert.Equal(organization.Id, Assert.Single(result.Items).Id);
        Assert.True(project.Project.IsActive);
        Assert.Single(store.Projects);
        Assert.Empty(store.Calls);
    }

    // BR-M1-20: an organization is in the list by its own status alone. A project deactivated on its own does not put an
    // active organization here.
    [Fact]
    public async Task Handle_ActiveOrganizationWithDeactivatedProject_IsNotListed()
    {
        var store = new InMemoryOrganizations();
        var organization = store.AddOrganization();
        store.AddProject(organization).Project.Deactivate();

        var result = await HandleAsync(HandlerOver(store));

        Assert.Empty(result.Items);
    }

    // US-2 · "The organization is restored exactly as it was": once active again it leaves this list.
    [Fact]
    public async Task Handle_RestoredOrganization_LeavesTheList()
    {
        var store = new InMemoryOrganizations();
        var organization = store.AddOrganization();
        organization.Deactivate();
        var handler = HandlerOver(store);

        var whileDeactivated = await HandleAsync(handler);
        organization.Restore();
        var afterRestore = await HandleAsync(handler);

        Assert.Equal(organization.Id, Assert.Single(whileDeactivated.Items).Id);
        Assert.Empty(afterRestore.Items);
    }

    // US-2 · "listed separately": nothing deactivated, nothing listed; an empty list, not an error.
    [Fact]
    public async Task Handle_NoDeactivatedOrganization_ReturnsEmpty()
    {
        var store = new InMemoryOrganizations();
        store.AddOrganization();

        var result = await HandleAsync(HandlerOver(store));

        Assert.Empty(result.Items);
    }

    // api-contract § 0.6 · "display order": by name in Russian collation, ignoring letter case, then by id. The fake read
    // model orders as the database does; the names are inserted in another order and mix the cases.
    [Fact]
    public async Task Handle_SeveralDeactivated_AreInDisplayOrder_IgnoringCase()
    {
        var store = new InMemoryOrganizations();
        foreach (var name in new[] { "Яблоко", "бета", "Гамма", "Альфа" })
        {
            store.AddOrganization(name).Deactivate();
        }

        var result = await HandleAsync(HandlerOver(store));

        Assert.Equal(["Альфа", "бета", "Гамма", "Яблоко"], result.Items.Select(item => item.Name));
    }

    // The read model owns the order (SQL, plan-backend § 6.5): the handler hands the rows on as they come and says
    // which status it wants.
    [Fact]
    public async Task Handle_AsksForTheDeactivatedStatus_AndKeepsTheReadModelsOrder()
    {
        var rows = new[]
        {
            new OrganizationRow(Guid.CreateVersion7(), "Ёлка"),
            new OrganizationRow(Guid.CreateVersion7(), "Альфа"),
            new OrganizationRow(Guid.CreateVersion7(), "Ель"),
        };
        var readModel = new RowsReadModel(rows);

        var result = await HandleAsync(new GetDeactivatedOrganizationsHandler(readModel));

        Assert.Equal([OrganizationStatus.Deactivated], readModel.RequestedStatuses);
        Assert.Equal(rows.Select(row => new OrganizationListItemResponse(row.Id, row.Name)), result.Items);
    }

    /// <summary>Answers a fixed list whatever is asked, and remembers what was asked.</summary>
    private sealed class RowsReadModel(IReadOnlyList<OrganizationRow> rows) : IOrganizationReadModel
    {
        public List<OrganizationStatus> RequestedStatuses { get; } = [];

        public Task<IReadOnlyList<OrganizationRow>> ListAsync(OrganizationStatus status, CancellationToken cancellationToken)
        {
            RequestedStatuses.Add(status);

            return Task.FromResult(rows);
        }

        public Task<OrganizationRow?> FindActiveAsync(Guid organizationId, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("The list reads nothing by id.");

        public Task<OrganizationStatus?> FindStatusAsync(Guid organizationId, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("The list reads nothing by id.");
    }
}
