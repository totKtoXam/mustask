using Mustask.Domain.Organizations;
using Mustask.Shared.Exceptions;
using static Mustask.Domain.Tests.Organizations.OrganizationsDomainTestData;

namespace Mustask.Domain.Tests.Organizations;

public sealed class AreaTests
{
    // US-5 · "A child area is created under any node"; US-6 · "created with or without an owner".
    [Fact]
    public void CreateChild_WithOrWithoutOwner_IsAChildOfItsParent()
    {
        var project = AProject();
        var platform = ASecondTeam(project);

        var inheriting = Area.CreateChild(project.Root, Name(" Отчёты "), ownerTeam: null);
        var owned = Area.CreateChild(inheriting, Name("Квартальные"), platform.Team);

        Assert.Equal(project.Root.Id, inheriting.ParentId);
        Assert.Equal(project.Project.Id, inheriting.ProjectId);
        Assert.Equal("Отчёты", inheriting.Name);
        Assert.Null(inheriting.OwnerTeamId);
        Assert.Null(inheriting.DefaultOfTeamId);
        Assert.False(inheriting.IsRoot);

        Assert.Equal(inheriting.Id, owned.ParentId);
        Assert.Equal(platform.Team.Id, owned.OwnerTeamId);
        Assert.Null(owned.DefaultOfTeamId);
    }

    // US-6 · "An area's owner is a team of the area's own project": the domain never builds the other case.
    [Fact]
    public void CreateChild_OwnerOfAnotherProjectOrDeleted_IsADefect()
    {
        var project = AProject();
        var other = AProject("Другой", "OTH");
        var deleted = ASecondTeam(project);
        deleted.Team.Delete();

        Assert.Throws<ArgumentException>(() => Area.CreateChild(project.Root, Name("Отчёты"), other.FirstTeam));
        Assert.Throws<ArgumentException>(() => Area.CreateChild(project.Root, Name("Отчёты"), deleted.Team));
    }

    // US-5 · "The root comes with the project": renamed only with its project, never moved, re-owned or deleted.
    [Fact]
    public void RootChanges_AreRefusedWithRootIsFixed()
    {
        var project = AProject();
        var child = Area.CreateChild(project.Root, Name("Отчёты"), ownerTeam: null);
        var tree = Tree(project.Root, child);

        Assert.Equal(OrganizationErrorCodes.AreaRootIsFixed, Assert.Throws<ConflictException>(() => project.Root.Rename(Name("Иное"))).Code);
        Assert.Equal(OrganizationErrorCodes.AreaRootIsFixed, Assert.Throws<ConflictException>(() => project.Root.MoveUnder(child, tree)).Code);
        Assert.Equal(OrganizationErrorCodes.AreaRootIsFixed, Assert.Throws<ConflictException>(() => project.Root.SetOwner(null, "Пример проекта")).Code);
        Assert.Equal(OrganizationErrorCodes.AreaRootIsFixed, Assert.Throws<ConflictException>(() => project.Root.EnsureDeletable(true, "Пример проекта")).Code);
        Assert.Equal(OrganizationErrorCodes.AreaRootIsFixed, Assert.Throws<ConflictException>(project.Root.EnsureNotRoot).Code);
        Assert.Equal("Пример проекта", project.Root.Name);
        Assert.Equal(project.FirstTeam.Id, project.Root.OwnerTeamId);
    }

    // US-5 · "A child area is renamed": children and owner unchanged.
    [Fact]
    public void Rename_Child_ChangesItsNameOnly()
    {
        var project = AProject();
        var child = Area.CreateChild(project.Root, Name("Квартальные"), project.FirstTeam);

        child.Rename(Name("Годовые"));

        Assert.Equal("Годовые", child.Name);
        Assert.Equal("ГОДОВЫЕ", child.NormalizedName);
        Assert.Equal(project.FirstTeam.Id, child.OwnerTeamId);
    }

    // US-5 · "A node moves with its whole subtree" and "never under itself or its descendant".
    [Fact]
    public void MoveUnder_SiblingIsAllowed_SelfAndDescendantRefused_CurrentParentIsANoOp()
    {
        var project = AProject();
        var reports = Area.CreateChild(project.Root, Name("Отчёты"), ownerTeam: null);
        var archive = Area.CreateChild(project.Root, Name("Архив"), ownerTeam: null);
        var quarterly = Area.CreateChild(reports, Name("Квартальные"), ownerTeam: null);
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
        var reports = Area.CreateChild(project.Root, Name("Отчёты"), ownerTeam: null);

        Assert.Throws<ArgumentException>(() => reports.MoveUnder(other.Root, Tree(project.Root, reports)));
    }

    // US-6 · "An area is re-owned" and "An explicit owner is cleared"; setting the same owner again changes nothing.
    [Fact]
    public void SetOwner_SetsClearsAndIsANoOpForTheSameOwner()
    {
        var project = AProject();
        var platform = ASecondTeam(project);
        var reports = Area.CreateChild(project.Root, Name("Отчёты"), project.FirstTeam);

        Assert.True(reports.SetOwner(platform.Team.Id, defaultTeamName: null));
        Assert.Equal(platform.Team.Id, reports.OwnerTeamId);

        Assert.False(reports.SetOwner(platform.Team.Id, defaultTeamName: null));

        Assert.True(reports.SetOwner(null, defaultTeamName: null));
        Assert.Null(reports.OwnerTeamId);
    }

    // US-6 · "A team's default area stays with its team": re-owning or clearing it names the team.
    [Fact]
    public void SetOwner_DefaultArea_IsRefusedNamingTheTeam()
    {
        var project = AProject();
        var platform = ASecondTeam(project);

        var reown = Assert.Throws<ConflictException>(() => platform.Area.SetOwner(project.FirstTeam.Id, "Платформа"));
        var clear = Assert.Throws<ConflictException>(() => platform.Area.SetOwner(null, "Платформа"));

        Assert.Equal(OrganizationErrorCodes.AreaIsTeamDefault, reown.Code);
        Assert.Equal("Платформа", reown.Params["team"].Value);
        Assert.Equal(OrganizationErrorCodes.AreaIsTeamDefault, clear.Code);
        Assert.Equal(platform.Team.Id, platform.Area.OwnerTeamId);

        Assert.False(platform.Area.SetOwner(platform.Team.Id, "Платформа"));
    }

    // US-5 · "Only an empty leaf is deleted"; US-6 · "A team's default area stays with its team".
    [Fact]
    public void EnsureDeletable_RefusesChildrenThenDefault_AllowsAnEmptyLeaf()
    {
        var project = AProject();
        var platform = ASecondTeam(project);
        var reports = Area.CreateChild(project.Root, Name("Отчёты"), ownerTeam: null);

        Assert.Equal(OrganizationErrorCodes.AreaNotEmpty, Assert.Throws<ConflictException>(() => reports.EnsureDeletable(true, null)).Code);
        Assert.Equal(OrganizationErrorCodes.AreaIsTeamDefault, Assert.Throws<ConflictException>(() => platform.Area.EnsureDeletable(false, "Платформа")).Code);

        reports.EnsureDeletable(false, null);
    }

    // US-4 · "The default is among the team's explicitly owned areas" (BR-M1-14).
    [Fact]
    public void MarkDefaultOf_RequiresExplicitOwnership()
    {
        var project = AProject();
        var platform = ASecondTeam(project);
        var inherited = Area.CreateChild(platform.Area, Name("API"), ownerTeam: null);
        var owned = Area.CreateChild(project.Root, Name("Отчёты"), platform.Team);

        var refusal = Assert.Throws<ConflictException>(() => inherited.MarkDefaultOf(platform.Team));
        Assert.Equal(OrganizationErrorCodes.TeamDefaultAreaNotOwned, refusal.Code);
        Assert.Null(inherited.DefaultOfTeamId);

        owned.MarkDefaultOf(platform.Team);
        Assert.Equal(platform.Team.Id, owned.DefaultOfTeamId);

        owned.ClearDefault();
        Assert.Null(owned.DefaultOfTeamId);
        Assert.Equal(platform.Team.Id, owned.OwnerTeamId);
    }
}
