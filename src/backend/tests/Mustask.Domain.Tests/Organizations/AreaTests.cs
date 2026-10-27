using Mustask.Domain.Organizations;
using Mustask.Shared.Exceptions;
using static Mustask.Domain.Tests.Organizations.OrganizationsDomainTestData;

namespace Mustask.Domain.Tests.Organizations;

public sealed class AreaTests
{
    private static Area AnArea(Guid organizationId, Guid? parentId, string name = "Платформа") =>
        Area.Create(organizationId, parentId, Name(name), description: null, Creator, Now);

    // US-1 · "The administrator builds the example tree" (BR-AW-3): a top-level area has no parent, a child has one.
    [Fact]
    public void Create_TopLevelHasNoParent_ChildHasItsParent_NameIsTrimmed()
    {
        var organizationId = Guid.CreateVersion7();

        var top = AnArea(organizationId, null, " Платформа ");
        var child = AnArea(organizationId, top.Id, "API");

        Assert.Null(top.ParentId);
        Assert.True(top.IsTopLevel);
        Assert.Equal("Платформа", top.Name);
        Assert.Equal(organizationId, top.OrganizationId);
        Assert.Equal(top.Id, child.ParentId);
        Assert.False(child.IsTopLevel);
        Assert.Equal(Creator, top.CreatedBy);
        Assert.Equal(Now, top.CreatedAt);
    }

    // US-1 · "Only the name is required on the create form" / "a description holds at most 2 000": kept as typed, none is null.
    // BR-AW-5, OQ-2 B: «Наследовать доступ» is stored on.
    [Fact]
    public void Create_DescriptionIsKeptAsTyped_NoneIsNull_InheritsAccessIsOn()
    {
        Assert.True(EntityDescription.TryCreate(" Всё про платформу\n", out var description));
        var organizationId = Guid.CreateVersion7();

        var described = Area.Create(organizationId, null, Name("Платформа"), description, Creator, Now);
        var plain = AnArea(organizationId, null, "Отчёты");

        Assert.Equal(" Всё про платформу\n", described.Description);
        Assert.Null(plain.Description);
        Assert.True(described.InheritsAccess);
        Assert.True(plain.InheritsAccess);
    }

    // US-1 · an area belongs to an organization: an empty organization or creator is a defect, never a user answer.
    [Fact]
    public void Create_WithoutOrganizationOrCreator_IsADefect()
    {
        Assert.Throws<ArgumentException>(() => AnArea(Guid.Empty, null));
        Assert.Throws<ArgumentException>(() => Area.Create(Guid.CreateVersion7(), null, Name("Платформа"), null, "", Now));
    }

    // US-2 · "A rename and a description show everywhere at once": only the name (and its normalized form) changes.
    [Fact]
    public void Rename_ChangesNameOnly_KeepsPlaceAndDescription()
    {
        var organizationId = Guid.CreateVersion7();
        var top = AnArea(organizationId, null);
        var child = AnArea(organizationId, top.Id, "Квартальные");

        child.Rename(Name("Годовые"));

        Assert.Equal("Годовые", child.Name);
        Assert.Equal("ГОДОВЫЕ", child.NormalizedName);
        Assert.Equal(top.Id, child.ParentId);
    }

    [Fact]
    public void Describe_SetsAndClears_KeepsTheName()
    {
        var area = AnArea(Guid.CreateVersion7(), null);
        Assert.True(EntityDescription.TryCreate("Описание", out var description));

        area.Describe(description);
        Assert.Equal("Описание", area.Description);

        area.Describe(null);
        Assert.Null(area.Description);
        Assert.Equal("Платформа", area.Name);
    }

    // US-2 · "An area moves with its whole subtree", "never under itself or a descendant, even by a direct request" (BR-AW-3; SC-08),
    // and "a move to the parent it has changes nothing" (api-contract § 2).
    [Fact]
    public void MoveUnder_SiblingAndTopLevelAreAllowed_SelfAndDescendantRefused_CurrentParentIsANoOp()
    {
        var organizationId = Guid.CreateVersion7();
        var reports = AnArea(organizationId, null, "Отчёты");
        var archive = AnArea(organizationId, null, "Архив");
        var quarterly = AnArea(organizationId, reports.Id, "Квартальные");
        var tree = Tree(reports, archive, quarterly);

        Assert.Equal(OrganizationErrorCodes.AreaMoveIntoOwnSubtree, Assert.Throws<ConflictException>(() => reports.MoveUnder(quarterly.Id, tree)).Code);
        Assert.Equal(OrganizationErrorCodes.AreaMoveIntoOwnSubtree, Assert.Throws<ConflictException>(() => reports.MoveUnder(reports.Id, tree)).Code);
        Assert.False(reports.MoveUnder(null, tree));
        Assert.Null(reports.ParentId);

        Assert.True(reports.MoveUnder(archive.Id, tree));
        Assert.Equal(archive.Id, reports.ParentId);
        Assert.Equal(reports.Id, quarterly.ParentId);

        Assert.True(quarterly.MoveUnder(null, tree));
        Assert.True(quarterly.IsTopLevel);
    }

    // US-2 · "The tree is at most 10 levels deep after a move" (BR-AW-3): the moved subtree's height counts, not just the node.
    [Fact]
    public void MoveUnder_ASubtreeThatWouldPassLevelTen_IsRefusedWithDepthExceeded()
    {
        var organizationId = Guid.CreateVersion7();
        var chain = new List<Area> { AnArea(organizationId, null, "Уровень 1") };

        for (var level = 2; level <= 9; level++)
        {
            chain.Add(AnArea(organizationId, chain[^1].Id, $"Уровень {level}"));
        }

        var movedTop = AnArea(organizationId, null, "Переезжающая");
        var movedChild = AnArea(organizationId, movedTop.Id, "Дочерняя");
        var tree = Tree([.. chain, movedTop, movedChild]);

        Assert.Equal(OrganizationErrorCodes.AreaDepthExceeded, Assert.Throws<ConflictException>(() => movedTop.MoveUnder(chain[^1].Id, tree)).Code);
        Assert.Null(movedTop.ParentId);

        Assert.True(movedChild.MoveUnder(chain[^1].Id, tree));
    }
}
