using Mustask.Application.Features.Organizations.MoveArea;
using Mustask.Application.Tests.TestDoubles;
using Mustask.Application.Tests.TestDoubles.Organizations;
using Mustask.Domain.Identity;
using Mustask.Domain.Organizations;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Tests.Features.Organizations.MoveArea;

/// <summary>mod-018 US-2 on fakes: the move, the order of its answers (BR-AW-15) and its lock order (ADR-0028 rule 8).</summary>
public sealed class MoveAreaHandlerTests
{
    private sealed class Fixture
    {
        public Fixture(OrganizationRole role = OrganizationRole.Admin)
        {
            Organization = Store.AddOrganization();
            Reports = Store.AddArea(Organization, "Отчёты");
            Quarterly = Store.AddArea(Organization, "Квартальные", Reports);
            Archive = Store.AddArea(Organization, "Архив");
            Areas = new FakeAreaRepository(Store);
            Handler = new MoveAreaHandler(new FakeOrganizationReadModel(Store), Areas, new FakeOrganizationAccess(role), new FakeUnitOfWork());
        }

        public InMemoryOrganizations Store { get; } = new();

        public Organization Organization { get; }

        public Area Reports { get; }

        public Area Quarterly { get; }

        public Area Archive { get; }

        public FakeAreaRepository Areas { get; }

        public MoveAreaHandler Handler { get; }

        public Task<MoveAreaResult> HandleAsync(Area area, Area? parent, string? organizationId = null) =>
            Handler.Handle(new MoveAreaCommand(organizationId ?? Organization.Id.ToString(), area.Id.ToString(), parent?.Id.ToString()), CancellationToken.None);
    }

    // US-2 · "An area moves with its whole subtree": «Отчёты» under «Архив» takes «Квартальные» along.
    [Fact]
    public async Task Handle_MovesTheSubtree_AndAnswersTheNewPath()
    {
        var f = new Fixture();

        var moved = await f.HandleAsync(f.Reports, f.Archive);

        Assert.Equal(f.Archive.Id, moved.Response.ParentId);
        Assert.Equal("Архив / Отчёты", moved.Response.Path);
        Assert.Equal(f.Reports.Id, f.Quarterly.ParentId);
        Assert.Equal(1, f.Store.SaveCount);
    }

    // US-2 · "An area moves to the top level".
    [Fact]
    public async Task Handle_ToTheTopLevel_ClearsTheParent()
    {
        var f = new Fixture();

        var moved = await f.HandleAsync(f.Quarterly, null);

        Assert.Null(moved.Response.ParentId);
        Assert.Equal("Квартальные", moved.Response.Path);
    }

    // US-2 · "A move to the parent it has changes nothing": no save.
    [Fact]
    public async Task Handle_ToTheCurrentParent_IsASuccessThatChangesNothing()
    {
        var f = new Fixture();

        var moved = await f.HandleAsync(f.Quarterly, f.Reports);

        Assert.Equal("Отчёты / Квартальные", moved.Response.Path);
        Assert.Equal(0, f.Store.SaveCount);
    }

    // US-2 · "never under itself or a descendant" (BR-AW-3).
    [Fact]
    public async Task Handle_IntoItsOwnSubtree_IsConflict_AndNothingChanges()
    {
        var f = new Fixture();

        var refusal = await Assert.ThrowsAsync<ConflictException>(() => f.HandleAsync(f.Reports, f.Quarterly));
        var self = await Assert.ThrowsAsync<ConflictException>(() => f.HandleAsync(f.Reports, f.Reports));

        Assert.Equal(OrganizationErrorCodes.AreaMoveIntoOwnSubtree, refusal.Code);
        Assert.Equal(OrganizationErrorCodes.AreaMoveIntoOwnSubtree, self.Code);
        Assert.Null(f.Reports.ParentId);
        Assert.Equal(0, f.Store.SaveCount);
    }

    // US-2 · "The tree is at most 10 levels deep after a move": the moved subtree's height counts.
    [Fact]
    public async Task Handle_PastLevelTen_IsAreaDepthExceeded()
    {
        var f = new Fixture();
        Area? parent = null;

        for (var level = 1; level <= 9; level++)
        {
            parent = f.Store.AddArea(f.Organization, $"Уровень {level}", parent);
        }

        var refusal = await Assert.ThrowsAsync<ConflictException>(() => f.HandleAsync(f.Reports, parent));

        Assert.Equal(OrganizationErrorCodes.AreaDepthExceeded, refusal.Code);
        Assert.Null(f.Reports.ParentId);
    }

    // US-2 · "A move keeps sibling names unique": the destination already holds the name.
    [Fact]
    public async Task Handle_DestinationHoldsTheName_IsAreaNameTaken()
    {
        var f = new Fixture();
        f.Store.AddArea(f.Organization, "Отчёты", f.Archive);

        var refusal = await Assert.ThrowsAsync<ConflictException>(() => f.HandleAsync(f.Reports, f.Archive));

        Assert.Equal(OrganizationErrorCodes.AreaNameTaken, refusal.Code);
        Assert.Equal(0, f.Store.SaveCount);
    }

    // BR-AW-15 outside in: organization, then area, then the destination (all 404).
    [Fact]
    public async Task Handle_AddressAndDestination_Answer404OutsideIn()
    {
        var f = new Fixture();
        var foreign = f.Store.AddArea(f.Store.AddOrganization("Другая"), "Чужая");

        var org = await Assert.ThrowsAsync<NotFoundException>(() => f.HandleAsync(f.Reports, f.Archive, "abc"));
        var area = await Assert.ThrowsAsync<NotFoundException>(() => f.HandleAsync(foreign, f.Archive));
        var destination = await Assert.ThrowsAsync<NotFoundException>(() => f.HandleAsync(f.Reports, foreign));

        Assert.Equal(OrganizationErrorCodes.OrganizationNotFound, org.Code);
        Assert.Equal(OrganizationErrorCodes.AreaNotFound, area.Code);
        Assert.Equal(OrganizationErrorCodes.AreaNotFound, destination.Code);
        Assert.Equal(0, f.Store.SaveCount);
    }

    // US-6 · "Moving to or from the top level needs area.create": a member moves nested areas, not top-level ones.
    [Fact]
    public async Task Handle_MemberAndTheTopLevel_IsForbidden_ButNestedMovesWork()
    {
        var f = new Fixture(OrganizationRole.Member);
        var second = f.Store.AddArea(f.Organization, "Второй", f.Archive);

        var toTop = await Assert.ThrowsAsync<ForbiddenException>(() => f.HandleAsync(f.Quarterly, null));
        var fromTop = await Assert.ThrowsAsync<ForbiddenException>(() => f.HandleAsync(f.Reports, f.Archive));
        var nested = await f.HandleAsync(second, f.Reports);

        Assert.Equal(OrganizationErrorCodes.AccessForbidden, toTop.Code);
        Assert.Equal(OrganizationErrorCodes.AccessForbidden, fromTop.Code);
        Assert.Equal(f.Reports.Id, nested.Response.ParentId);
    }

    // ADR-0028 rule 8: the tree lock, then the two area rows in id order (the moved one FOR UPDATE, the destination FOR SHARE).
    [Fact]
    public async Task Handle_LocksTheTree_ThenTheAreasInIdOrder()
    {
        var f = new Fixture();

        await f.HandleAsync(f.Reports, f.Archive);

        var update = $"lock area update {f.Reports.Id}";
        var share = $"lock area share {f.Archive.Id}";
        var rows = f.Reports.Id.CompareTo(f.Archive.Id) < 0 ? new[] { update, share } : [share, update];

        Assert.Equal([$"lock area tree {f.Organization.Id}", .. rows, "save"], f.Store.Calls);
    }

    // ADR-0028 A-9: an area or destination absent from its lock statement's answer is 404, whatever the unlocked read saw.
    [Theory]
    [InlineData("area")]
    [InlineData("destination")]
    public async Task Handle_VanishedAtTheLock_IsAreaNotFound(string which)
    {
        var f = new Fixture();
        f.Areas.VanishedAfterTheLock.Add(which == "area" ? f.Reports.Id : f.Archive.Id);

        var refusal = await Assert.ThrowsAsync<NotFoundException>(() => f.HandleAsync(f.Reports, f.Archive));

        Assert.Equal(OrganizationErrorCodes.AreaNotFound, refusal.Code);
        Assert.Equal(0, f.Store.SaveCount);
    }
}
