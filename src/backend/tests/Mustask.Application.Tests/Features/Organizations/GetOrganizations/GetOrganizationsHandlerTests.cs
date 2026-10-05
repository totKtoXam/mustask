using Mustask.Application.Features.Organizations.GetOrganizations;
using Mustask.Application.Tests.TestDoubles.Organizations;
using Mustask.Domain.Organizations;
using Mustask.Domain.Process;

namespace Mustask.Application.Tests.Features.Organizations.GetOrganizations;

/// <summary>
/// <c>GET /api/organizations</c> on fakes (plan-backend § 13, lane L-1): the
/// active organizations, in the order the read model gives them, whoever the
/// user is. The query is a projection, so there is no order of answers to pin
/// beyond "active only", "no reordering" and "no write".
/// </summary>
public sealed class GetOrganizationsHandlerTests
{
    private sealed class Fixture
    {
        public Fixture()
        {
            Handler = new GetOrganizationsHandler(new FakeOrganizationReadModel(Store));
        }

        public InMemoryOrganizations Store { get; } = new();

        public GetOrganizationsHandler Handler { get; }

        public Task<GetOrganizationsResult> HandleAsync() => Handler.Handle(new GetOrganizationsQuery(), CancellationToken.None);
    }

    // US-1 · "No organization yet": the list is empty, not an error; the two frames are stage 6.
    [Fact]
    public async Task Handle_NoActiveOrganization_ReturnsEmpty()
    {
        var f = new Fixture();

        var result = await f.HandleAsync();

        Assert.Empty(result.Items);
    }

    // US-1 · "No organization yet" and US-8 · "deactivated ... left out": only deactivated ones exist, the list is empty.
    [Fact]
    public async Task Handle_OnlyDeactivatedOrganizations_ReturnsEmpty()
    {
        var f = new Fixture();
        f.Store.AddOrganization("Закрытая").Deactivate();

        var result = await f.HandleAsync();

        Assert.Empty(result.Items);
    }

    // US-1 · the list shows each active organization by id and name (api-contract § 1.1: id, name).
    [Fact]
    public async Task Handle_ActiveOrganization_IsListedWithIdAndName()
    {
        var f = new Fixture();
        var organization = f.Store.AddOrganization("Акме");

        var result = await f.HandleAsync();

        Assert.Equal([new OrganizationListItemResponse(organization.Id, "Акме")], result.Items);
    }

    // US-8 · "Lists in order, deactivated ... left out": a deactivated organization is not in the active list, and
    // the restored one comes back (ADR-0026 rule 4: the status is the read's own condition).
    [Fact]
    public async Task Handle_DeactivatedOrganization_IsLeftOut_UntilRestored()
    {
        var f = new Fixture();
        var kept = f.Store.AddOrganization("Остаётся");
        var closed = f.Store.AddOrganization("Закрытая");
        closed.Deactivate();

        var whileDeactivated = await f.HandleAsync();
        closed.Restore();
        var afterRestore = await f.HandleAsync();

        Assert.Equal([kept.Id], whileDeactivated.Items.Select(item => item.Id));
        Assert.Equal([closed.Id, kept.Id], afterRestore.Items.Select(item => item.Id));
    }

    // US-8 · "Lists in order": «альфа», «Бета», «Ёлка», «Ель», as a person reads them (Russian collation, letters before
    // letter case, «Ё» beside «Е», design § 4.6). The organizations are stored in another order, and the handler keeps
    // the read model's.
    [Fact]
    public async Task Handle_ListsInDisplayOrder_YoBesideYeAndLetterCaseIgnored()
    {
        var f = new Fixture();
        f.Store.AddOrganization("Ель");
        f.Store.AddOrganization("Бета");
        f.Store.AddOrganization("Ёлка");
        f.Store.AddOrganization("альфа");

        var result = await f.HandleAsync();

        Assert.Equal(["альфа", "Бета", "Ёлка", "Ель"], result.Items.Select(item => item.Name));
    }

    // US-1 · "Every signed-in user sees every organization" (BR-M1-19, api-contract § 0.1): the list is not narrowed by who
    // created an organization, and the handler has no current user to narrow by.
    [Fact]
    public async Task Handle_OrganizationsOfOtherCreators_AreAllListed()
    {
        var f = new Fixture();
        var own = f.Store.AddOrganization("Альфа");
        var foreign = Organization.Create(
            Guid.CreateVersion7(), InMemoryOrganizations.Name("Бета"), ProcessId.New(), InMemoryOrganizations.Now, "7a3c9d2e-0000-4000-8000-0000000000aa");
        f.Store.Organizations.Add(foreign);

        var result = await f.HandleAsync();

        Assert.NotEqual(own.CreatedBy, foreign.CreatedBy);
        Assert.Equal([own.Id, foreign.Id], result.Items.Select(item => item.Id));
    }

    // plan-backend § 6.4: a read. It takes no lock and saves nothing.
    [Fact]
    public async Task Handle_IsARead_TakesNoLockAndSavesNothing()
    {
        var f = new Fixture();
        f.Store.AddOrganization("Акме");

        await f.HandleAsync();

        Assert.Empty(f.Store.Calls);
    }
}
