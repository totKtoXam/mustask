using Mustask.Application.Features.Organizations.MoveArea;
using Mustask.Application.Tests.TestDoubles;
using Mustask.Application.Tests.TestDoubles.Organizations;
using Mustask.Domain.Identity;
using Mustask.Domain.Organizations;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Tests.Features.Organizations.MoveArea;

/// <summary>mod-018 US-3 on fakes: the move of a typed node, the order of its answers (BR-AW-15) and its lock order (ADR-0031).</summary>
public sealed class MoveAreaHandlerTests
{
    private sealed class Fixture
    {
        public Fixture(OrganizationRole role = OrganizationRole.Admin)
        {
            Organization = Store.AddOrganization();
            Development = Store.AddDepartment(Organization, "Разработка");
            Product = Store.AddDepartment(Organization, "Продукт");
            Project = Store.AddProject(Organization, "Мастаск", "MST", Development).Project;
            Node = Store.RootOf(Project);
            Reports = Store.AddArea(Node, "Отчёты");
            Quarterly = Store.AddArea(Reports, "Квартальные");
            Archive = Store.AddArea(Node, "Архив");
            Areas = new FakeAreaRepository(Store);
            Handler = new MoveAreaHandler(
                new FakeOrganizationReadModel(Store), new FakeProjectRepository(Store), Areas, new FakeOrganizationAccess(role), new FakeUnitOfWork());
        }

        public InMemoryOrganizations Store { get; } = new();

        public Organization Organization { get; }

        public Project Project { get; }

        public Area Development { get; }

        public Area Product { get; }

        /// <summary>The project's node, under «Разработка».</summary>
        public Area Node { get; }

        public Area Reports { get; }

        public Area Quarterly { get; }

        public Area Archive { get; }

        public FakeAreaRepository Areas { get; }

        public MoveAreaHandler Handler { get; }

        public Task<MoveAreaResult> HandleAsync(Area area, Area? parent, string? organizationId = null) =>
            Handler.Handle(new MoveAreaCommand(organizationId ?? Organization.Id.ToString(), area.Id.ToString(), parent?.Id.ToString()), CancellationToken.None);
    }

    // US-3 · "A sub-area moves with its whole subtree": «Отчёты» under «Архив» takes «Квартальные» along; the path is from the top.
    [Fact]
    public async Task Handle_SubArea_MovesWithItsSubtree_AndAnswersTheNewPath()
    {
        var f = new Fixture();

        var moved = await f.HandleAsync(f.Reports, f.Archive);

        Assert.Equal((f.Archive.Id, "sub", "Разработка / Мастаск / Архив / Отчёты"), (moved.Response.ParentId, "sub", moved.Response.Path));
        Assert.Equal(f.Reports.Id, f.Quarterly.ParentId);
        Assert.Equal(1, f.Store.SaveCount);
    }

    // US-3 · "A project's node moves under a department and to the top, with its whole subtree".
    [Fact]
    public async Task Handle_ProjectNode_MovesBetweenDepartmentsAndTheTop()
    {
        var f = new Fixture();

        var toProduct = await f.HandleAsync(f.Node, f.Product);
        var toTop = await f.HandleAsync(f.Node, null);

        Assert.Equal(("project", f.Product.Id, "Продукт / Мастаск"), (toProduct.Response.Type, toProduct.Response.ParentId, toProduct.Response.Path));
        Assert.Null(toTop.Response.ParentId);
        Assert.Equal("Мастаск", toTop.Response.Path);
        Assert.Equal(f.Node.Id, f.Reports.ParentId);
    }

    // US-3 · "A move to the parent it has changes nothing": no save.
    [Fact]
    public async Task Handle_ToTheCurrentParent_IsASuccessThatChangesNothing()
    {
        var f = new Fixture();

        var moved = await f.HandleAsync(f.Quarterly, f.Reports);

        Assert.Equal("Разработка / Мастаск / Отчёты / Квартальные", moved.Response.Path);
        Assert.Equal(0, f.Store.SaveCount);
    }

    // OQ-3 A, OQ-7 A · a department never moves; a sub-area never goes to the top or under a department.
    [Fact]
    public async Task Handle_TypeRules_AreTypeNotAllowed()
    {
        var f = new Fixture();

        var department = await Assert.ThrowsAsync<ConflictException>(() => f.HandleAsync(f.Development, f.Product));
        var toTop = await Assert.ThrowsAsync<ConflictException>(() => f.HandleAsync(f.Reports, null));
        var underDepartment = await Assert.ThrowsAsync<ConflictException>(() => f.HandleAsync(f.Reports, f.Product));
        var nodeUnderArea = await Assert.ThrowsAsync<ConflictException>(() => f.HandleAsync(f.Node, f.Archive));

        Assert.All([department, toTop, underDepartment], refusal => Assert.Equal(OrganizationErrorCodes.AreaTypeNotAllowed, refusal.Code));
        Assert.Equal(OrganizationErrorCodes.AreaMoveIntoOwnSubtree, nodeUnderArea.Code);
        Assert.Equal(0, f.Store.SaveCount);
    }

    // OQ-7 A · a sub-area stays in its own project: another project's node or sub-area is outside_project.
    [Fact]
    public async Task Handle_SubAreaIntoAnotherProject_IsOutsideProject()
    {
        var f = new Fixture();
        var other = f.Store.AddProject(f.Organization, "Другой", "OTH").Project;
        var otherSub = f.Store.AddArea(f.Store.RootOf(other), "Чужая");

        var toNode = await Assert.ThrowsAsync<ConflictException>(() => f.HandleAsync(f.Reports, f.Store.RootOf(other)));
        var toSub = await Assert.ThrowsAsync<ConflictException>(() => f.HandleAsync(f.Reports, otherSub));

        Assert.Equal(OrganizationErrorCodes.AreaOutsideProject, toNode.Code);
        Assert.Equal(OrganizationErrorCodes.AreaOutsideProject, toSub.Code);
        Assert.Equal(f.Node.Id, f.Reports.ParentId);
    }

    // OQ-6 A · a deactivated project's sub-area or node does not move: «Проект не найден».
    [Fact]
    public async Task Handle_InADeactivatedProject_IsProjectNotFound()
    {
        var f = new Fixture();
        f.Project.Deactivate();

        var sub = await Assert.ThrowsAsync<NotFoundException>(() => f.HandleAsync(f.Reports, f.Archive));
        var node = await Assert.ThrowsAsync<NotFoundException>(() => f.HandleAsync(f.Node, f.Product));

        Assert.Equal(OrganizationErrorCodes.ProjectNotFound, sub.Code);
        Assert.Equal(OrganizationErrorCodes.ProjectNotFound, node.Code);
        Assert.Equal(0, f.Store.SaveCount);
    }

    // US-3 · "never under itself or a descendant" (BR-AW-3).
    [Fact]
    public async Task Handle_IntoItsOwnSubtree_IsConflict_AndNothingChanges()
    {
        var f = new Fixture();

        var refusal = await Assert.ThrowsAsync<ConflictException>(() => f.HandleAsync(f.Reports, f.Quarterly));
        var self = await Assert.ThrowsAsync<ConflictException>(() => f.HandleAsync(f.Reports, f.Reports));

        Assert.Equal(OrganizationErrorCodes.AreaMoveIntoOwnSubtree, refusal.Code);
        Assert.Equal(OrganizationErrorCodes.AreaMoveIntoOwnSubtree, self.Code);
        Assert.Equal(f.Node.Id, f.Reports.ParentId);
        Assert.Equal(0, f.Store.SaveCount);
    }

    // US-3 · "The tree is at most 10 levels deep after a move": the moved subtree's height counts.
    [Fact]
    public async Task Handle_PastLevelTen_IsAreaDepthExceeded()
    {
        var f = new Fixture();
        var parent = f.Archive;

        for (var level = 5; level <= 10; level++)
        {
            parent = f.Store.AddArea(parent, $"Уровень {level}");
        }

        var refusal = await Assert.ThrowsAsync<ConflictException>(() => f.HandleAsync(f.Reports, parent));

        Assert.Equal(OrganizationErrorCodes.AreaDepthExceeded, refusal.Code);
        Assert.Equal(f.Node.Id, f.Reports.ParentId);
    }

    // OQ-12 A · "A move keeps sibling names unique": the destination already holds the name — a department too, at the top.
    [Fact]
    public async Task Handle_DestinationHoldsTheName_IsAreaNameTaken()
    {
        var f = new Fixture();
        f.Store.AddArea(f.Archive, "Отчёты");
        f.Store.AddDepartment(f.Organization, "Мастаск");

        var sub = await Assert.ThrowsAsync<ConflictException>(() => f.HandleAsync(f.Reports, f.Archive));
        var top = await Assert.ThrowsAsync<ConflictException>(() => f.HandleAsync(f.Node, null));

        Assert.Equal(OrganizationErrorCodes.AreaNameTaken, sub.Code);
        Assert.Equal(OrganizationErrorCodes.AreaNameTaken, top.Code);
        Assert.Equal(0, f.Store.SaveCount);
    }

    // BR-AW-15 outside in: organization, then area, then the destination (all 404).
    [Fact]
    public async Task Handle_AddressAndDestination_Answer404OutsideIn()
    {
        var f = new Fixture();
        var foreign = f.Store.AddDepartment(f.Store.AddOrganization("Другая"), "Чужая");

        var org = await Assert.ThrowsAsync<NotFoundException>(() => f.HandleAsync(f.Reports, f.Archive, "abc"));
        var area = await Assert.ThrowsAsync<NotFoundException>(() => f.HandleAsync(foreign, f.Archive));
        var destination = await Assert.ThrowsAsync<NotFoundException>(() => f.HandleAsync(f.Reports, foreign));

        Assert.Equal(OrganizationErrorCodes.OrganizationNotFound, org.Code);
        Assert.Equal(OrganizationErrorCodes.AreaNotFound, area.Code);
        Assert.Equal(OrganizationErrorCodes.AreaNotFound, destination.Code);
        Assert.Equal(0, f.Store.SaveCount);
    }

    // US-6 · "Moving to or from the top level needs area.create": a member moves nested nodes, not top-level ones.
    [Fact]
    public async Task Handle_MemberAndTheTopLevel_IsForbidden_ButNestedMovesWork()
    {
        var f = new Fixture(OrganizationRole.Member);

        var toTop = await Assert.ThrowsAsync<ForbiddenException>(() => f.HandleAsync(f.Node, null));
        var fromTop = await Assert.ThrowsAsync<ForbiddenException>(() => f.HandleAsync(f.Development, f.Product));
        var nested = await f.HandleAsync(f.Reports, f.Archive);

        Assert.Equal(OrganizationErrorCodes.AccessForbidden, toTop.Code);
        Assert.Equal(OrganizationErrorCodes.AccessForbidden, fromTop.Code);
        Assert.Equal(f.Archive.Id, nested.Response.ParentId);
    }

    // ADR-0031: the tree lock, then the two area rows in id order (the moved one FOR UPDATE, the destination FOR SHARE).
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
