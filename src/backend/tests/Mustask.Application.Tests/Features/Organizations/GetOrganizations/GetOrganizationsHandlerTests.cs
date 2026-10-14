using Mustask.Application.Features.Organizations.GetOrganizations;
using Mustask.Application.Tests.TestDoubles;
using Mustask.Application.Tests.TestDoubles.Organizations;
using Mustask.Domain.Identity;
using Mustask.Domain.Organizations;
using Mustask.Domain.Process;

namespace Mustask.Application.Tests.Features.Organizations.GetOrganizations;

/// <summary>
/// <c>GET /api/organizations</c> on fakes (plan-backend § 13, lane L-1): the
/// active organizations the caller is a member of (mod-016 BR-M1-7), in the order
/// the read model gives them. The query is a projection, so there is no order of
/// answers to pin beyond "active only", "mine only", "no reordering" and "no write".
/// </summary>
public sealed class GetOrganizationsHandlerTests
{
    private sealed class Fixture
    {
        public Fixture()
        {
            Handler = new GetOrganizationsHandler(new FakeOrganizationReadModel(Store), User);
        }

        public InMemoryOrganizations Store { get; } = new();

        public FakeCurrentUser User { get; } = new();

        public GetOrganizationsHandler Handler { get; }

        /// <summary>An organization the signed-in user belongs to.</summary>
        public Organization Mine(string name, OrganizationRole role = OrganizationRole.Admin) => Store.AddOrganizationOf(User.SubjectId, name, role);

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
        f.Mine("Закрытая").Deactivate();

        var result = await f.HandleAsync();

        Assert.Empty(result.Items);
    }

    // US-1 · the list shows each active organization by id and name (api-contract § 1.1: id, name).
    [Fact]
    public async Task Handle_ActiveOrganization_IsListedWithIdAndName()
    {
        var f = new Fixture();
        var organization = f.Mine("Акме");

        var result = await f.HandleAsync();

        Assert.Equal([new OrganizationListItemResponse(organization.Id, "Акме")], result.Items);
    }

    // US-8 · "Lists in order, deactivated ... left out": a deactivated organization is not in the active list, and
    // the restored one comes back (ADR-0026 rule 4: the status is the read's own condition).
    [Fact]
    public async Task Handle_DeactivatedOrganization_IsLeftOut_UntilRestored()
    {
        var f = new Fixture();
        var kept = f.Mine("Остаётся");
        var closed = f.Mine("Закрытая");
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
        f.Mine("Ель");
        f.Mine("Бета");
        f.Mine("Ёлка");
        f.Mine("альфа");

        var result = await f.HandleAsync();

        Assert.Equal(["альфа", "Бета", "Ёлка", "Ель"], result.Items.Select(item => item.Name));
    }

    // mod-016 US-2 · "The organization list holds only the user's organizations" (BR-M1-7): an organization the user holds no
    // membership in is not listed, whoever created it — an organization of another creator, one of the same creator without
    // a membership (a creator who has since been removed), and one where only somebody else is a member.
    [Fact]
    public async Task Handle_OrganizationsTheUserIsNotAMemberOf_AreNotListed()
    {
        var f = new Fixture();
        var mine = f.Mine("Альфа");
        var foreign = Organization.Create(
            Guid.CreateVersion7(), InMemoryOrganizations.Name("Бета"), ProcessId.New(), InMemoryOrganizations.Now, "7a3c9d2e-0000-4000-8000-0000000000aa");
        f.Store.Organizations.Add(foreign);
        var noOneInIt = f.Store.AddOrganization("Гамма");
        var someoneElses = f.Store.AddOrganizationOf("someone-else", "Дельта");

        var result = await f.HandleAsync();

        Assert.Equal([mine.Id], result.Items.Select(item => item.Id));
        Assert.DoesNotContain(result.Items, item => item.Id == foreign.Id || item.Id == noOneInIt.Id || item.Id == someoneElses.Id);
    }

    // mod-016 US-2 · the list holds the organizations "in any role": an admin, a member and a viewer all see theirs.
    [Theory]
    [InlineData(OrganizationRole.Admin)]
    [InlineData(OrganizationRole.Member)]
    [InlineData(OrganizationRole.Viewer)]
    public async Task Handle_TheUsersOrganization_IsListedInAnyRole(OrganizationRole role)
    {
        var f = new Fixture();
        var organization = f.Mine("Акме", role);

        var result = await f.HandleAsync();

        Assert.Equal([organization.Id], result.Items.Select(item => item.Id));
    }

    // mod-016 US-2 · "A user who belongs to no organization sees an explicit empty state": other people's organizations exist,
    // the user is in none of them, and the answer is an empty list — not an error.
    [Fact]
    public async Task Handle_UserWithNoMembership_GetsAnEmptyList_WhoeverElseHasOrganizations()
    {
        var f = new Fixture();
        f.Store.AddOrganizationOf("someone-else", "Чужая");

        var result = await f.HandleAsync();

        Assert.Empty(result.Items);
    }

    // mod-016 api-contract § 0.1 · the list is the caller's: the handler asks the read model for the token's subject and any role.
    [Fact]
    public async Task Handle_AsksTheReadModelForTheCurrentUser_InAnyRole()
    {
        var f = new Fixture();
        var other = f.Store.AddOrganization("Чужая");
        f.Store.AddMember(other, "someone-else");
        f.User.SubjectId = "the-caller";
        var theirs = f.Store.AddOrganizationOf("the-caller", "Своя", OrganizationRole.Viewer);

        var result = await f.HandleAsync();

        Assert.Equal([theirs.Id], result.Items.Select(item => item.Id));
    }

    // plan-backend § 6.4: a read. It takes no lock and saves nothing.
    [Fact]
    public async Task Handle_IsARead_TakesNoLockAndSavesNothing()
    {
        var f = new Fixture();
        f.Mine("Акме");

        await f.HandleAsync();

        Assert.Empty(f.Store.Calls);
    }
}
