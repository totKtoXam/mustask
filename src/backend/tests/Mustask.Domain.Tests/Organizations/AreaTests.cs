using Mustask.Domain.Organizations;
using Mustask.Shared.Exceptions;
using static Mustask.Domain.Tests.Organizations.OrganizationsDomainTestData;

namespace Mustask.Domain.Tests.Organizations;

public sealed class AreaTests
{
    private static readonly Guid Org = Guid.CreateVersion7();

    private static Area Department(string name = "Разработка", Guid? organizationId = null) =>
        Area.CreateDepartment(organizationId ?? Org, Name(name), description: null, Creator, Now);

    private static Area ProjectNode(Guid? parentId = null, string name = "Проект", Guid? projectId = null) =>
        Area.CreateProjectNode(Org, parentId, projectId ?? Guid.CreateVersion7(), Name(name), Creator, Now);

    private static Area SubArea(Area parent, string name = "Платформа") =>
        Area.CreateSubArea(parent, Name(name), description: null, Creator, Now);

    private static string CodeOf(Action action) => Assert.Throws<ConflictException>(action).Code;

    // US-1 · a department stands at the top with no project; a project node carries its project; a sub-area takes its parent's project.
    [Fact]
    public void Factories_TypeTheNodeAndTakeTheProjectFromTheParent()
    {
        var department = Department(" Разработка ");
        var node = ProjectNode(department.Id);
        var sub = SubArea(node, "API");
        var deeper = SubArea(sub, "Публичное");

        Assert.Equal((AreaType.Department, null, null, "Разработка"), (department.Type, department.ProjectId, department.ParentId, department.Name));
        Assert.True(department.IsTopLevel);
        Assert.Equal((AreaType.Project, department.Id), (node.Type, node.ParentId));
        Assert.Equal((AreaType.Area, node.ProjectId, node.Id), (sub.Type, sub.ProjectId, sub.ParentId));
        Assert.Equal(node.ProjectId, deeper.ProjectId);
        Assert.Equal((Creator, Now, Org), (department.CreatedBy, department.CreatedAt, department.OrganizationId));
        Assert.True(department.InheritsAccess);
    }

    // OQ-7 A · a sub-area stands only below a project node or another sub-area.
    [Fact]
    public void CreateSubArea_UnderADepartment_IsTypeNotAllowed()
    {
        Assert.Equal(OrganizationErrorCodes.AreaTypeNotAllowed, CodeOf(() => SubArea(Department())));
    }

    // US-1 · "a description holds at most 2 000": kept as typed, none is null.
    [Fact]
    public void Describe_SetsAndClears_EveryTypeMayBeDescribed()
    {
        Assert.True(EntityDescription.TryCreate(" Всё про платформу\n", out var description));
        var node = ProjectNode();

        node.Describe(description);
        Assert.Equal(" Всё про платформу\n", node.Description);

        node.Describe(null);
        Assert.Null(node.Description);
    }

    [Fact]
    public void Create_WithoutOrganizationOrCreator_IsADefect()
    {
        Assert.Throws<ArgumentException>(() => Department(organizationId: Guid.Empty));
        Assert.Throws<ArgumentException>(() => Area.CreateDepartment(Org, Name("Разработка"), null, "", Now));
        Assert.Throws<ArgumentException>(() => Area.CreateProjectNode(Org, null, Guid.Empty, Name("Проект"), Creator, Now));
    }

    // OQ-10 A · a project node is renamed and deleted only with its project: root_is_fixed. Others rename in place.
    [Fact]
    public void Rename_ChangesNameOnly_ForDepartmentAndSubArea_ProjectNodeIsFixed()
    {
        var department = Department();
        var node = ProjectNode(department.Id);
        var sub = SubArea(node, "Квартальные");

        sub.Rename(Name("Годовые"));
        department.Rename(Name("Продукт"));

        Assert.Equal(("Годовые", "ГОДОВЫЕ", node.Id), (sub.Name, sub.NormalizedName, sub.ParentId));
        Assert.Equal("Продукт", department.Name);
        Assert.Equal(OrganizationErrorCodes.AreaRootIsFixed, CodeOf(() => node.Rename(Name("Другое"))));
        Assert.Equal("Проект", node.Name);
    }

    // OQ-5 A · deletion: a project node is fixed (checked first), a node with a child or an item is not empty, a leaf goes.
    [Fact]
    public void EnsureDeletable_ProjectNodeIsFixed_NonEmptyIsNotEmpty_LeafPasses()
    {
        var node = ProjectNode();
        var sub = SubArea(node);

        Assert.Equal(OrganizationErrorCodes.AreaRootIsFixed, CodeOf(() => node.EnsureDeletable(hasChildren: true, holdsWorkItems: false)));
        Assert.Equal(OrganizationErrorCodes.AreaNotEmpty, CodeOf(() => sub.EnsureDeletable(hasChildren: true, holdsWorkItems: false)));
        Assert.Equal(OrganizationErrorCodes.AreaNotEmpty, CodeOf(() => sub.EnsureDeletable(hasChildren: false, holdsWorkItems: true)));
        sub.EnsureDeletable(hasChildren: false, holdsWorkItems: false);
        Department().EnsureDeletable(hasChildren: false, holdsWorkItems: false);
    }

    // BR-AW-3 · a project node moves to the top or under a department, with its subtree; never under itself or a descendant.
    [Fact]
    public void MoveUnder_ProjectNode_GoesToTopOrADepartment_NotUnderItsOwnSubtreeOrAnArea()
    {
        var department = Department();
        var node = ProjectNode();
        var sub = SubArea(node);
        var other = ProjectNode(name: "Другой");
        var tree = Tree(department, node, sub, other);

        Assert.Equal(OrganizationErrorCodes.AreaTypeNotAllowed, CodeOf(() => node.MoveUnder(sub, tree)));
        Assert.Equal(OrganizationErrorCodes.AreaTypeNotAllowed, CodeOf(() => node.MoveUnder(node, tree)));
        Assert.Equal(OrganizationErrorCodes.AreaTypeNotAllowed, CodeOf(() => other.MoveUnder(sub, tree)));
        Assert.False(node.MoveUnder(null, tree));

        Assert.True(node.MoveUnder(department, tree));
        Assert.Equal(department.Id, node.ParentId);
        Assert.Equal(node.Id, sub.ParentId);
        Assert.True(node.MoveUnder(null, Tree(department, node, sub, other)));
        Assert.True(node.IsTopLevel);
    }

    // BR-AW-3 · a department never moves (the top has no depth, OQ-3 A): not under another department, not anywhere.
    [Fact]
    public void MoveUnder_Department_IsTypeNotAllowedUnderAnyNode_NoOpAtTheTop()
    {
        var department = Department();
        var other = Department("Продукт");
        var tree = Tree(department, other);

        Assert.Equal(OrganizationErrorCodes.AreaTypeNotAllowed, CodeOf(() => department.MoveUnder(other, tree)));
        Assert.False(department.MoveUnder(null, tree));
    }

    // BR-AW-3, OQ-7 A · a sub-area moves inside its own project only: another project is outside_project, the top or a department is not allowed.
    [Fact]
    public void MoveUnder_SubArea_StaysInItsProject()
    {
        var department = Department();
        var node = ProjectNode(department.Id);
        var otherNode = ProjectNode(name: "Другой");
        var a = SubArea(node, "А");
        var b = SubArea(node, "Б");
        var inB = SubArea(b, "В");
        var tree = Tree(department, node, otherNode, a, b, inB);

        Assert.Equal(OrganizationErrorCodes.AreaOutsideProject, CodeOf(() => a.MoveUnder(otherNode, tree)));
        Assert.Equal(OrganizationErrorCodes.AreaTypeNotAllowed, CodeOf(() => a.MoveUnder(department, tree)));
        Assert.Equal(OrganizationErrorCodes.AreaTypeNotAllowed, CodeOf(() => a.MoveUnder(null, tree)));
        Assert.Equal(OrganizationErrorCodes.AreaMoveIntoOwnSubtree, CodeOf(() => b.MoveUnder(inB, tree)));
        Assert.False(a.MoveUnder(node, tree));

        Assert.True(a.MoveUnder(inB, tree));
        Assert.Equal(inB.Id, a.ParentId);
        Assert.True(inB.MoveUnder(node, tree));
    }

    // BR-AW-3 · at most 10 levels in the whole tree: the moved subtree's height counts, not just the node.
    [Fact]
    public void MoveUnder_ASubtreeThatWouldPassLevelTen_IsRefusedWithDepthExceeded()
    {
        var node = ProjectNode();
        var chain = new List<Area> { node };

        for (var level = 2; level <= 9; level++)
        {
            chain.Add(SubArea(chain[^1], $"Уровень {level}"));
        }

        var movedTop = SubArea(node, "Переезжающая");
        var movedChild = SubArea(movedTop, "Дочерняя");
        var tree = Tree([.. chain, movedTop, movedChild]);

        Assert.Equal(OrganizationErrorCodes.AreaDepthExceeded, CodeOf(() => movedTop.MoveUnder(chain[^1], tree)));
        Assert.Equal(node.Id, movedTop.ParentId);
        Assert.True(movedChild.MoveUnder(chain[^1], tree));
    }
}
