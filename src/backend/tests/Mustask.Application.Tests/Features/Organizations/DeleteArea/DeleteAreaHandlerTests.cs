using Mustask.Application.Features.Organizations.DeleteArea;
using Mustask.Application.Tests.TestDoubles;
using Mustask.Application.Tests.TestDoubles.Organizations;
using Mustask.Domain.Organizations;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Tests.Features.Organizations.DeleteArea;

/// <summary>mod-018 US-2 on fakes: the deletion, the order of its answers (BR-AW-15) and its lock order (ADR-0031).</summary>
public sealed class DeleteAreaHandlerTests
{
    private sealed class Fixture
    {
        public Fixture()
        {
            Organization = Store.AddOrganization();
            Department = Store.AddDepartment(Organization, "Разработка");
            Project = Store.AddProject(Organization, department: Department).Project;
            Node = Store.RootOf(Project);
            Quarterly = Store.AddArea(Node, "Квартальные");
            Areas = new FakeAreaRepository(Store);
            Usage = new FakeAreaUsage();
            Handler = new DeleteAreaHandler(
                new FakeOrganizationReadModel(Store), new FakeProjectRepository(Store), Areas, Usage, new FakeUnitOfWork());
        }

        public InMemoryOrganizations Store { get; } = new();

        public Organization Organization { get; }

        public Project Project { get; }

        public Area Department { get; }

        public Area Node { get; }

        public Area Quarterly { get; }

        public FakeAreaRepository Areas { get; }

        public FakeAreaUsage Usage { get; }

        public DeleteAreaHandler Handler { get; }

        public Task HandleAsync(Area area, string? organizationId = null) =>
            Handler.Handle(new DeleteAreaCommand(organizationId ?? Organization.Id.ToString(), area.Id.ToString()), CancellationToken.None);
    }

    // US-2 · "A leaf sub-area is deleted and nothing else changes".
    [Fact]
    public async Task Handle_LeafSubArea_IsDeleted()
    {
        var f = new Fixture();

        await f.HandleAsync(f.Quarterly);

        Assert.DoesNotContain(f.Store.Areas, area => area.Id == f.Quarterly.Id);
        Assert.Contains(f.Store.Areas, area => area.Id == f.Node.Id);
    }

    // US-2 · "An empty department is deleted; its team links go with it" (BR-AW-9).
    [Fact]
    public async Task Handle_EmptyDepartment_IsDeleted_WithItsTeamLinks()
    {
        var f = new Fixture();
        var empty = f.Store.AddDepartment(f.Organization, "Пустой");
        var team = f.Store.AddTeam(f.Organization);
        f.Store.LinkArea(empty, team);

        await f.HandleAsync(empty);

        Assert.DoesNotContain(f.Store.Areas, area => area.Id == empty.Id);
        Assert.Empty(f.Store.AreaTeams);
        Assert.Contains(f.Store.Teams, candidate => candidate.Id == team.Id);
    }

    // US-2 · "A department holding a project node is not deleted": the changed «не пусто» text.
    [Fact]
    public async Task Handle_DepartmentWithAProjectNode_IsAreaNotEmpty_AndNothingChanges()
    {
        var f = new Fixture();
        var before = f.Store.Areas.Count;

        var refusal = await Assert.ThrowsAsync<ConflictException>(() => f.HandleAsync(f.Department));

        Assert.Equal(OrganizationErrorCodes.AreaNotEmpty, refusal.Code);
        Assert.Equal(before, f.Store.Areas.Count);
        Assert.Equal(0, f.Store.SaveCount);
    }

    // US-2 · a sub-area with a child, or with a work item in any state, is not empty.
    [Fact]
    public async Task Handle_SubAreaWithAChildOrAWorkItem_IsAreaNotEmpty()
    {
        var f = new Fixture();
        var child = f.Store.AddArea(f.Quarterly, "Q1");
        var withItem = f.Store.AddArea(f.Node, "С записями");
        f.Usage.InUse.Add((f.Project.Id, withItem.Id));

        var parentRefusal = await Assert.ThrowsAsync<ConflictException>(() => f.HandleAsync(f.Quarterly));
        var itemRefusal = await Assert.ThrowsAsync<ConflictException>(() => f.HandleAsync(withItem));

        Assert.Equal(OrganizationErrorCodes.AreaNotEmpty, parentRefusal.Code);
        Assert.Equal(OrganizationErrorCodes.AreaNotEmpty, itemRefusal.Code);
        Assert.Contains(f.Store.Areas, area => area.Id == child.Id);
        Assert.Equal(0, f.Store.SaveCount);
    }

    // OQ-5 A · a project's node goes only with its project: root_is_fixed, before the children are looked at.
    [Fact]
    public async Task Handle_ProjectNode_IsRootIsFixed_EvenWithChildren()
    {
        var f = new Fixture();

        var refusal = await Assert.ThrowsAsync<ConflictException>(() => f.HandleAsync(f.Node));

        Assert.Equal(OrganizationErrorCodes.AreaRootIsFixed, refusal.Code);
        Assert.Equal(0, f.Store.SaveCount);
    }

    // OQ-6 A · a sub-area of a deactivated project is read-only: «Проект не найден».
    [Fact]
    public async Task Handle_UnderADeactivatedProject_IsProjectNotFound()
    {
        var f = new Fixture();
        f.Project.Deactivate();

        var refusal = await Assert.ThrowsAsync<NotFoundException>(() => f.HandleAsync(f.Quarterly));

        Assert.Equal(OrganizationErrorCodes.ProjectNotFound, refusal.Code);
        Assert.Contains(f.Store.Areas, area => area.Id == f.Quarterly.Id);
    }

    // BR-AW-15 outside in: organization, then area (404), never the existence of another organization's area.
    [Fact]
    public async Task Handle_Address_Answers404OutsideIn()
    {
        var f = new Fixture();
        var foreign = f.Store.AddDepartment(f.Store.AddOrganization("Другая"), "Чужая");

        var org = await Assert.ThrowsAsync<NotFoundException>(() => f.HandleAsync(f.Quarterly, "abc"));
        var area = await Assert.ThrowsAsync<NotFoundException>(() => f.HandleAsync(foreign));

        Assert.Equal(OrganizationErrorCodes.OrganizationNotFound, org.Code);
        Assert.Equal(OrganizationErrorCodes.AreaNotFound, area.Code);
        Assert.Contains(f.Store.Areas, candidate => candidate.Id == foreign.Id);
    }

    // ADR-0031: the tree lock, then the area FOR UPDATE, then one save.
    [Fact]
    public async Task Handle_LocksTheTree_ThenTheArea_ThenSavesOnce()
    {
        var f = new Fixture();

        await f.HandleAsync(f.Quarterly);

        Assert.Equal([$"lock area tree {f.Organization.Id}", $"lock area update {f.Quarterly.Id}", "save"], f.Store.Calls);
    }

    // ADR-0028 A-9: absent from the lock statement's answer is 404, whatever the unlocked read saw (a concurrent delete won).
    [Fact]
    public async Task Handle_VanishedAtTheLock_IsAreaNotFound()
    {
        var f = new Fixture();
        f.Areas.VanishedAfterTheLock.Add(f.Quarterly.Id);

        var refusal = await Assert.ThrowsAsync<NotFoundException>(() => f.HandleAsync(f.Quarterly));

        Assert.Equal(OrganizationErrorCodes.AreaNotFound, refusal.Code);
        Assert.Equal(0, f.Store.SaveCount);
    }
}
