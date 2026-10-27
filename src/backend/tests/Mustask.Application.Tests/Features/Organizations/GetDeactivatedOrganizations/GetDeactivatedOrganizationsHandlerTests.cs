using Mustask.Application.Abstractions.Organizations;
using Mustask.Application.Features.Organizations.GetDeactivatedOrganizations;
using Mustask.Application.Tests.TestDoubles;
using Mustask.Application.Tests.TestDoubles.Organizations;
using Mustask.Domain.Identity;
using Mustask.Domain.Organizations;

namespace Mustask.Application.Tests.Features.Organizations.GetDeactivatedOrganizations;

/// <summary>
/// <c>GET /api/organizations/deactivated</c> on fakes (plan-backend § 14.3,
/// lane L-2): which organizations the list holds, in what order, and that the
/// handler asks the read model for exactly the deactivated status and the roles
/// that may restore (mod-016 BR-M1-35). The route has no address and no body, so
/// there is no order of answers to pin.
/// </summary>
public sealed class GetDeactivatedOrganizationsHandlerTests
{
    private static readonly FakeCurrentUser User = new();

    private static GetDeactivatedOrganizationsHandler HandlerOver(InMemoryOrganizations store) =>
        new(new FakeOrganizationReadModel(store), User);

    private static Task<GetDeactivatedOrganizationsResult> HandleAsync(GetDeactivatedOrganizationsHandler handler) =>
        handler.Handle(new GetDeactivatedOrganizationsQuery(), CancellationToken.None);

    // US-2 · "A deactivated organization is listed separately, with no purge": «Пример организации» is among the
    // deactivated ones, the active ones are not.
    [Fact]
    public async Task Handle_ActiveAndDeactivated_ListsTheDeactivatedOnly()
    {
        var store = new InMemoryOrganizations();
        var active = store.AddOrganizationOf(User.SubjectId, "Активная");
        var deactivated = store.AddOrganizationOf(User.SubjectId, "Пример организации");
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
        var organization = store.AddOrganizationOf(User.SubjectId);
        var project = store.AddProject(organization);
        store.AddLinkedTeam(organization, project);
        organization.Deactivate();

        var result = await HandleAsync(HandlerOver(store));

        Assert.Equal(organization.Id, Assert.Single(result.Items).Id);
        Assert.True(project.IsActive);
        Assert.Single(store.Projects);
        Assert.Empty(store.Calls);
    }

    // BR-M1-20: an organization is in the list by its own status alone. A project deactivated on its own does not put an
    // active organization here.
    [Fact]
    public async Task Handle_ActiveOrganizationWithDeactivatedProject_IsNotListed()
    {
        var store = new InMemoryOrganizations();
        var organization = store.AddOrganizationOf(User.SubjectId);
        store.AddProject(organization).Deactivate();

        var result = await HandleAsync(HandlerOver(store));

        Assert.Empty(result.Items);
    }

    // US-2 · "The organization is restored exactly as it was": once active again it leaves this list.
    [Fact]
    public async Task Handle_RestoredOrganization_LeavesTheList()
    {
        var store = new InMemoryOrganizations();
        var organization = store.AddOrganizationOf(User.SubjectId);
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
        store.AddOrganizationOf(User.SubjectId);

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
            store.AddOrganizationOf(User.SubjectId, name).Deactivate();
        }

        var result = await HandleAsync(HandlerOver(store));

        Assert.Equal(["Альфа", "бета", "Гамма", "Яблоко"], result.Items.Select(item => item.Name));
    }

    // mod-016 US-2 · "Deactivated lists and restore are the administrators'" (BR-M1-35, D-13): the user is admin in C and
    // member in D, both deactivated — C is listed and D is not; a viewer's deactivated organization is not listed either.
    [Fact]
    public async Task Handle_ListsOnlyTheOrganizationsWhereTheRoleHoldsTheDeactivateCode()
    {
        var store = new InMemoryOrganizations();
        var c = store.AddOrganizationOf(User.SubjectId, "C", OrganizationRole.Admin);
        var d = store.AddOrganizationOf(User.SubjectId, "D", OrganizationRole.Member);
        var e = store.AddOrganizationOf(User.SubjectId, "E", OrganizationRole.Viewer);
        c.Deactivate();
        d.Deactivate();
        e.Deactivate();

        var result = await HandleAsync(HandlerOver(store));

        Assert.Equal([c.Id], result.Items.Select(item => item.Id));
    }

    // mod-016 US-2 · a deactivated organization the user is no member of is not listed, whoever created it.
    [Fact]
    public async Task Handle_DeactivatedOrganizationOfSomeoneElse_IsNotListed()
    {
        var store = new InMemoryOrganizations();
        store.AddOrganizationOf("someone-else", "Чужая").Deactivate();
        store.AddOrganization("Без участников").Deactivate();

        var result = await HandleAsync(HandlerOver(store));

        Assert.Empty(result.Items);
    }

    // The read model owns the order (SQL, plan-backend § 6.5): the handler hands the rows on as they come and says
    // which status it wants — and for which roles: those whose bundle holds `organizations.organization.deactivate`,
    // derived from the bundles, not hard-coded (BR-M1-35).
    [Fact]
    public async Task Handle_AsksForTheDeactivatedStatus_TheCallerAndTheRolesHoldingTheCode_AndKeepsTheReadModelsOrder()
    {
        var rows = new[]
        {
            new OrganizationRow(Guid.CreateVersion7(), "Ёлка"),
            new OrganizationRow(Guid.CreateVersion7(), "Альфа"),
            new OrganizationRow(Guid.CreateVersion7(), "Ель"),
        };
        var readModel = new RowsReadModel(rows);

        var result = await HandleAsync(new GetDeactivatedOrganizationsHandler(readModel, User));

        Assert.Equal([OrganizationStatus.Deactivated], readModel.RequestedStatuses);
        Assert.Equal([User.SubjectId], readModel.RequestedUsers);
        Assert.Equal(
            OrganizationRoleBundles.RolesHolding(PermissionCatalog.OrganizationsOrganizationDeactivate),
            Assert.Single(readModel.RequestedRoles)!);
        Assert.Equal(rows.Select(row => new OrganizationListItemResponse(row.Id, row.Name)), result.Items);
    }

    /// <summary>Answers a fixed list whatever is asked, and remembers what was asked.</summary>
    private sealed class RowsReadModel(IReadOnlyList<OrganizationRow> rows) : IOrganizationReadModel
    {
        public List<OrganizationStatus> RequestedStatuses { get; } = [];

        public List<string> RequestedUsers { get; } = [];

        public List<IReadOnlyCollection<OrganizationRole>?> RequestedRoles { get; } = [];

        public Task<IReadOnlyList<OrganizationRow>> ListAsync(
            OrganizationStatus status, string userId, IReadOnlyCollection<OrganizationRole>? onlyRoles, CancellationToken cancellationToken)
        {
            RequestedStatuses.Add(status);
            RequestedUsers.Add(userId);
            RequestedRoles.Add(onlyRoles);

            return Task.FromResult(rows);
        }

        public Task<OrganizationRow?> FindActiveAsync(Guid organizationId, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("The list reads nothing by id.");
    }
}
