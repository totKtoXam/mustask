using Mustask.Domain.Organizations;
using Mustask.Shared.Exceptions;
using static Mustask.Domain.Tests.Organizations.OrganizationsDomainTestData;

namespace Mustask.Domain.Tests.Organizations;

public sealed class AreaTests
{
    // US-5 · "A child area is created under any node". mod-017 BR-OT-10: it is created with no owner (the owner choice is gone).
    // Retired with it (rule BR-OT-10): CreateChild_WithOrWithoutOwner_IsAChildOfItsParent (renamed), CreateChild_OwnerOfAnotherProjectOrDeleted_IsADefect.
    [Fact]
    public void CreateChild_IsAChildOfItsParent_OwningNothing()
    {
        var project = AProject();

        var reports = Area.CreateChild(project.Root, Name(" Отчёты "));
        var quarterly = Area.CreateChild(reports, Name("Квартальные"));

        Assert.Equal(project.Root.Id, reports.ParentId);
        Assert.Equal(project.Project.Id, reports.ProjectId);
        Assert.Equal("Отчёты", reports.Name);
        Assert.False(reports.IsRoot);
        Assert.Equal(reports.Id, quarterly.ParentId);
        Assert.True(project.Root.IsRoot);
    }

    // US-5 · "The root comes with the project": renamed only with its project, never moved or deleted.
    [Fact]
    public void RootChanges_AreRefusedWithRootIsFixed()
    {
        var project = AProject();
        var child = Area.CreateChild(project.Root, Name("Отчёты"));
        var tree = Tree(project.Root, child);

        Assert.Equal(OrganizationErrorCodes.AreaRootIsFixed, Assert.Throws<ConflictException>(() => project.Root.Rename(Name("Иное"))).Code);
        Assert.Equal(OrganizationErrorCodes.AreaRootIsFixed, Assert.Throws<ConflictException>(() => project.Root.MoveUnder(child, tree)).Code);
        Assert.Equal(OrganizationErrorCodes.AreaRootIsFixed, Assert.Throws<ConflictException>(() => project.Root.EnsureDeletable(true, true)).Code);
        Assert.Equal(OrganizationErrorCodes.AreaRootIsFixed, Assert.Throws<ConflictException>(project.Root.EnsureNotRoot).Code);
        Assert.Equal("Пример проекта", project.Root.Name);
    }

    // US-5 · "A child area is renamed": children unchanged.
    [Fact]
    public void Rename_Child_ChangesItsNameOnly()
    {
        var project = AProject();
        var child = Area.CreateChild(project.Root, Name("Квартальные"));

        child.Rename(Name("Годовые"));

        Assert.Equal("Годовые", child.Name);
        Assert.Equal("ГОДОВЫЕ", child.NormalizedName);
        Assert.Equal(project.Root.Id, child.ParentId);
    }

    // US-5 · "A node moves with its whole subtree" and "never under itself or its descendant".
    [Fact]
    public void MoveUnder_SiblingIsAllowed_SelfAndDescendantRefused_CurrentParentIsANoOp()
    {
        var project = AProject();
        var reports = Area.CreateChild(project.Root, Name("Отчёты"));
        var archive = Area.CreateChild(project.Root, Name("Архив"));
        var quarterly = Area.CreateChild(reports, Name("Квартальные"));
        var tree = Tree(project.Root, reports, archive, quarterly);

        Assert.Equal(OrganizationErrorCodes.AreaMoveIntoOwnSubtree, Assert.Throws<ConflictException>(() => reports.MoveUnder(quarterly, tree)).Code);
        Assert.Equal(OrganizationErrorCodes.AreaMoveIntoOwnSubtree, Assert.Throws<ConflictException>(() => reports.MoveUnder(reports, tree)).Code);
        Assert.False(reports.MoveUnder(project.Root, tree));
        Assert.Equal(project.Root.Id, reports.ParentId);

        Assert.True(reports.MoveUnder(archive, tree));
        Assert.Equal(archive.Id, reports.ParentId);
        Assert.Equal(reports.Id, quarterly.ParentId);
    }

    // US-5 · "A node never leaves its project" (BR-M1-22).
    [Fact]
    public void MoveUnder_AreaOfAnotherProject_IsADefect()
    {
        var project = AProject();
        var other = AProject("Другой", "OTH");
        var reports = Area.CreateChild(project.Root, Name("Отчёты"));

        Assert.Throws<ArgumentException>(() => reports.MoveUnder(other.Root, Tree(project.Root, reports)));
    }

    // US-5 · "Only an empty leaf is deleted"; mod-017 BR-OT-10: a team's default area is no reason any more (a team has none),
    // so the refusal is children, then work items — one code, AreaNotEmpty.
    // Renamed (D-16, BR-OT-10): EnsureDeletable_RefusesChildrenThenDefault_AllowsAnEmptyLeaf -> EnsureDeletable_RefusesChildren_AllowsAnEmptyLeaf.
    [Fact]
    public void EnsureDeletable_RefusesChildren_AllowsAnEmptyLeaf()
    {
        var project = AProject();
        var reports = Area.CreateChild(project.Root, Name("Отчёты"));

        Assert.Equal(OrganizationErrorCodes.AreaNotEmpty, Assert.Throws<ConflictException>(() => reports.EnsureDeletable(true, false)).Code);

        reports.EnsureDeletable(false, false);
    }

    // mod-003 US-6 · "An area holding a work item is not deleted" and "Every item counts, whatever its state" (BR-M3-24):
    // work items answer as children do, with the same code.
    // Renamed (D-16, BR-OT-10): EnsureDeletable_HoldsWorkItems_RefusedAsNotEmpty_BeforeTheDefault -> EnsureDeletable_HoldsWorkItems_RefusedAsNotEmpty.
    [Fact]
    public void EnsureDeletable_HoldsWorkItems_RefusedAsNotEmpty()
    {
        var project = AProject();
        var reports = Area.CreateChild(project.Root, Name("Отчёты"));

        Assert.Equal(OrganizationErrorCodes.AreaNotEmpty, Assert.Throws<ConflictException>(() => reports.EnsureDeletable(false, true)).Code);
    }
}
