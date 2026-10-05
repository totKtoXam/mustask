using Mustask.Application.Features.Organizations.MoveArea;
using Mustask.Application.Tests.TestDoubles;
using Mustask.Application.Tests.TestDoubles.Organizations;
using Mustask.Domain.Organizations;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Tests.Features.Organizations.MoveArea;

/// <summary>
/// <c>MoveArea</c> on fakes: what a move changes, the order of its answers
/// (plan-backend § 5.3, api-contract § 0.4) and its lock order (§ 8.2), which the
/// race test of the Api project then proves against a real database.
/// </summary>
public sealed class MoveAreaHandlerTests
{
    private sealed class Fixture
    {
        public Fixture()
        {
            Organization = Store.AddOrganization();
            Project = Store.AddProject(Organization);
            Platform = Store.AddTeam(Project);
            Handler = new MoveAreaHandler(
                new FakeProjectRepository(Store),
                new FakeAreaRepository(Store),
                new FakeUnitOfWork());
        }

        public InMemoryOrganizations Store { get; } = new();

        public Organization Organization { get; }

        public NewProject Project { get; }

        public NewTeam Platform { get; }

        public MoveAreaHandler Handler { get; }

        public Area Root => Project.Root;

        public MoveAreaCommand Command(Area area, Area parent) => Command(area.Id.ToString(), parent.Id.ToString());

        public MoveAreaCommand Command(string? areaId, string? parentId, string? projectId = null, string? organizationId = null) =>
            new(organizationId ?? Organization.Id.ToString(), projectId ?? Project.Project.Id.ToString(), areaId, parentId);

        public Task<MoveAreaResult> HandleAsync(MoveAreaCommand command) => Handler.Handle(command, CancellationToken.None);

        public AreaTree Tree() =>
            new(Store.Areas
                .Where(area => area.ProjectId == Project.Project.Id)
                .Select(area => new AreaNode(area.Id, area.ParentId, area.Name, area.OwnerTeamId, area.DefaultOfTeamId)));
    }

    // US-5 · "A node moves with its whole subtree": «Отчёты» with «Квартальные» and «Годовые» is moved under «Архив».
    [Fact]
    public async Task Handle_UnderAnotherNode_MovesTheWholeSubtree()
    {
        var f = new Fixture();
        var reports = f.Store.AddArea(f.Root, "Отчёты");
        var quarterly = f.Store.AddArea(reports, "Квартальные");
        var yearly = f.Store.AddArea(reports, "Годовые");
        var archive = f.Store.AddArea(f.Root, "Архив");

        var moved = await f.HandleAsync(f.Command(reports, archive));

        Assert.Equal(new AreaResponse(reports.Id, f.Project.Project.Id, archive.Id, "Отчёты", null), moved.Response);
        var tree = f.Tree();
        Assert.Equal("Архив / Отчёты / Квартальные", tree.PathOf(quarterly.Id));
        Assert.Equal("Архив / Отчёты / Годовые", tree.PathOf(yearly.Id));
        Assert.DoesNotContain(tree.ChildrenOf(f.Root.Id), node => node.Id == reports.Id);
        Assert.Equal(reports.Id, quarterly.ParentId);
        Assert.Equal(reports.Id, yearly.ParentId);
        Assert.Equal(1, f.Store.SaveCount);
    }

    // BR-M1-20: behind the filter the organization id is well formed; if it were not, the outermost level answers.
    [Fact]
    public async Task Handle_MalformedOrganization_IsOrganizationNotFound()
    {
        var f = new Fixture();

        var refusal = await Assert.ThrowsAsync<NotFoundException>(() => f.HandleAsync(f.Command("x", "y", organizationId: "abc")));

        Assert.Equal(OrganizationErrorCodes.OrganizationNotFound, refusal.Code);
        Assert.Empty(f.Store.Calls);
    }

    // US-3 · "A project id names no project of the organization" and "a project is deactivated" (BR-M1-20, BR-M1-22):
    // the project level answers before the area and the body are looked at.
    [Theory]
    [InlineData("malformed")]
    [InlineData("unknown")]
    [InlineData("of another organization")]
    [InlineData("deactivated")]
    [InlineData("under a deactivated organization")]
    public async Task Handle_ProjectNotReachable_IsProjectNotFound_BeforeTheAreaAndTheBody(string how)
    {
        var f = new Fixture();
        var reports = f.Store.AddArea(f.Root, "Отчёты");
        var archive = f.Store.AddArea(f.Root, "Архив");
        var other = f.Store.AddProject(f.Store.AddOrganization("Другая организация"), "Чужой", "OTH");
        var projectId = how switch
        {
            "malformed" => "not-an-id",
            "unknown" => Guid.CreateVersion7().ToString(),
            "of another organization" => other.Project.Id.ToString(),
            _ => f.Project.Project.Id.ToString(),
        };

        if (how == "deactivated")
        {
            f.Project.Project.Deactivate();
        }

        if (how == "under a deactivated organization")
        {
            f.Organization.Deactivate();
        }

        var refusal = await Assert.ThrowsAsync<NotFoundException>(
            () => f.HandleAsync(f.Command(reports.Id.ToString(), archive.Id.ToString(), projectId)));

        Assert.Equal(OrganizationErrorCodes.ProjectNotFound, refusal.Code);
        Assert.Empty(f.Store.Calls);
        Assert.Equal(f.Root.Id, reports.ParentId);
    }

    // US-5 · "A node never leaves its project" (BR-M1-22): an area id that is malformed, unknown or of another project
    // names no area of this one. The address answers before the root rule: another project's root is not "the root" here.
    [Theory]
    [InlineData("malformed")]
    [InlineData("unknown")]
    [InlineData("of another project")]
    [InlineData("the root of another project")]
    public async Task Handle_AreaNotOfTheProject_IsAreaNotFound_BeforeAnyLock(string how)
    {
        var f = new Fixture();
        var archive = f.Store.AddArea(f.Root, "Архив");
        var other = f.Store.AddProject(f.Organization, "Второй проект", "SEC");
        var foreign = f.Store.AddArea(other.Root, "Чужая");
        var areaId = how switch
        {
            "malformed" => "{" + archive.Id + "}",
            "unknown" => Guid.CreateVersion7().ToString(),
            "of another project" => foreign.Id.ToString(),
            _ => other.Root.Id.ToString(),
        };

        var refusal = await Assert.ThrowsAsync<NotFoundException>(() => f.HandleAsync(f.Command(areaId, archive.Id.ToString())));

        Assert.Equal(OrganizationErrorCodes.AreaNotFound, refusal.Code);
        Assert.Empty(f.Store.Calls);
        Assert.Equal(other.Root.Id, foreign.ParentId);
    }

    // US-5 · "The root comes with the project and follows it": a direct request to move it is refused (BR-M1-11), and the
    // rule about the addressed object answers before the body's reference is looked at (api-contract § 0.4 step 2 before 4).
    [Fact]
    public async Task Handle_Root_IsRootIsFixed_BeforeTheParentAndAnyLock()
    {
        var f = new Fixture();
        var archive = f.Store.AddArea(f.Root, "Архив");

        var refusal = await Assert.ThrowsAsync<ConflictException>(
            () => f.HandleAsync(f.Command(f.Root.Id.ToString(), Guid.CreateVersion7().ToString())));
        var underAnArea = await Assert.ThrowsAsync<ConflictException>(() => f.HandleAsync(f.Command(f.Root, archive)));

        Assert.Equal(OrganizationErrorCodes.AreaRootIsFixed, refusal.Code);
        Assert.Equal(OrganizationErrorCodes.AreaRootIsFixed, underAnArea.Code);
        Assert.Empty(f.Store.Calls);
        Assert.Null(f.Root.ParentId);
    }

    // US-5 · "A node never leaves its project" (BR-M1-22): a parent that is null, malformed, unknown or an area of another
    // project answers area.not_found, and both trees are unchanged.
    [Theory]
    [InlineData("null")]
    [InlineData("malformed")]
    [InlineData("zero")]
    [InlineData("unknown")]
    [InlineData("of another project")]
    public async Task Handle_ParentNotAnAreaOfTheProject_IsAreaNotFound_NothingSaved(string how)
    {
        var f = new Fixture();
        var reports = f.Store.AddArea(f.Root, "Отчёты");
        var other = f.Store.AddProject(f.Organization, "Второй проект", "SEC");
        var parentId = how switch
        {
            "null" => null,
            "malformed" => "{" + f.Root.Id + "}",
            "zero" => "00000000-0000-0000-0000-000000000000",
            "unknown" => Guid.CreateVersion7().ToString(),
            _ => other.Root.Id.ToString(),
        };

        var refusal = await Assert.ThrowsAsync<NotFoundException>(() => f.HandleAsync(f.Command(reports.Id.ToString(), parentId)));

        Assert.Equal(OrganizationErrorCodes.AreaNotFound, refusal.Code);
        Assert.Equal(0, f.Store.SaveCount);
        Assert.Equal(f.Root.Id, reports.ParentId);
        Assert.Equal(1, f.Store.Areas.Count(area => area.ProjectId == other.Project.Id));
    }

    // US-5 · "A node is never moved under itself or its descendant": under «Квартальные» or «Итоги», or under itself.
    [Theory]
    [InlineData("itself")]
    [InlineData("a child")]
    [InlineData("a grandchild")]
    public async Task Handle_ParentIsTheNodeOrADescendant_IsMoveIntoOwnSubtree_TreeUnchanged(string how)
    {
        var f = new Fixture();
        var reports = f.Store.AddArea(f.Root, "Отчёты");
        var quarterly = f.Store.AddArea(reports, "Квартальные");
        var totals = f.Store.AddArea(quarterly, "Итоги");
        var parent = how switch
        {
            "itself" => reports,
            "a child" => quarterly,
            _ => totals,
        };

        var refusal = await Assert.ThrowsAsync<ConflictException>(() => f.HandleAsync(f.Command(reports, parent)));

        Assert.Equal(OrganizationErrorCodes.AreaMoveIntoOwnSubtree, refusal.Code);
        Assert.Equal(0, f.Store.SaveCount);
        Assert.Equal(f.Root.Id, reports.ParentId);
        Assert.Equal(reports.Id, quarterly.ParentId);
        Assert.Equal(quarterly.Id, totals.ParentId);
    }

    // The area as its own parent is one row: it is locked once (plan-backend § 8.2) and then refused.
    [Fact]
    public async Task Handle_ParentIsTheNodeItself_LocksTheRowOnce()
    {
        var f = new Fixture();
        var reports = f.Store.AddArea(f.Root, "Отчёты");

        await Assert.ThrowsAsync<ConflictException>(() => f.HandleAsync(f.Command(reports, reports)));

        Assert.Equal([$"lock project {f.Project.Project.Id}", $"lock area update {reports.Id}"], f.Store.Calls);
    }

    // US-5 · "A move needs a free name at its destination": «Архив» already has «Отчёты» (any case, BR-M1-12), the root's
    // «Отчёты» is refused and nothing is saved.
    [Fact]
    public async Task Handle_NameTakenAtDestination_IsAreaNameTaken_NothingSaved()
    {
        var f = new Fixture();
        var reports = f.Store.AddArea(f.Root, "Отчёты");
        var archive = f.Store.AddArea(f.Root, "Архив");
        f.Store.AddArea(archive, "ОТЧЁТЫ");

        var refusal = await Assert.ThrowsAsync<ConflictException>(() => f.HandleAsync(f.Command(reports, archive)));

        Assert.Equal(OrganizationErrorCodes.AreaNameTaken, refusal.Code);
        Assert.Equal(0, f.Store.SaveCount);
    }

    // The domain rule answers before the uniqueness pre-check (plan-backend § 5.3 step 5): under a descendant that already
    // has a child of the node's name, the refusal is the cycle's.
    [Fact]
    public async Task Handle_DescendantHoldsTheNodesName_IsMoveIntoOwnSubtree_NotNameTaken()
    {
        var f = new Fixture();
        var reports = f.Store.AddArea(f.Root, "Отчёты");
        var quarterly = f.Store.AddArea(reports, "Квартальные");
        f.Store.AddArea(quarterly, "Отчёты");

        var refusal = await Assert.ThrowsAsync<ConflictException>(() => f.HandleAsync(f.Command(reports, quarterly)));

        Assert.Equal(OrganizationErrorCodes.AreaMoveIntoOwnSubtree, refusal.Code);
    }

    // api-contract § 4.4: "Moving under the current parent answers 200 and changes nothing." The node's own name does not
    // clash with itself.
    [Fact]
    public async Task Handle_UnderTheCurrentParent_IsANoOp_NothingSaved()
    {
        var f = new Fixture();
        var reports = f.Store.AddArea(f.Root, "Отчёты");
        var quarterly = f.Store.AddArea(reports, "Квартальные");

        var moved = await f.HandleAsync(f.Command(quarterly, reports));

        Assert.Equal(new AreaResponse(quarterly.Id, f.Project.Project.Id, reports.Id, "Квартальные", null), moved.Response);
        Assert.Equal(reports.Id, quarterly.ParentId);
        Assert.Equal(0, f.Store.SaveCount);
    }

    // US-6 · "A move re-resolves inherited owners and keeps explicit ones": «Отчёты» has no explicit owner, its child
    // «Квартальные» is owned explicitly by the first team; «Отчёты» moves under «Платформа», the team's area.
    [Fact]
    public async Task Handle_ReResolvesInheritedOwners_KeepsExplicitOnes()
    {
        var f = new Fixture();
        var reports = f.Store.AddArea(f.Root, "Отчёты");
        var quarterly = f.Store.AddArea(reports, "Квартальные", f.Project.FirstTeam);
        Assert.Equal(new ResolvedOwner(f.Project.FirstTeam.Id, true), f.Tree().ResolveOwner(reports.Id));

        await f.HandleAsync(f.Command(reports, f.Platform.Area));

        var tree = f.Tree();
        Assert.Equal(new ResolvedOwner(f.Platform.Team.Id, true), tree.ResolveOwner(reports.Id));
        Assert.Equal(new ResolvedOwner(f.Project.FirstTeam.Id, false), tree.ResolveOwner(quarterly.Id));
        Assert.Null(reports.OwnerTeamId);
        Assert.Equal(f.Project.FirstTeam.Id, quarterly.OwnerTeamId);
    }

    // plan-backend § 8.2: the project row, then the area FOR UPDATE and the parent FOR SHARE in ascending id order, then
    // one save. Both orders are pinned by taking the two siblings and sorting them by id.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Handle_LocksTheProject_ThenTheAreaAndTheParentInAscendingIdOrder_ThenSavesOnce(bool areaHasTheLowerId)
    {
        var f = new Fixture();
        var one = f.Store.AddArea(f.Root, "Первая");
        var two = f.Store.AddArea(f.Root, "Вторая");
        var (low, high) = one.Id.CompareTo(two.Id) < 0 ? (one, two) : (two, one);
        var (area, parent) = areaHasTheLowerId ? (low, high) : (high, low);

        await f.HandleAsync(f.Command(area, parent));

        var projectLock = $"lock project {f.Project.Project.Id}";
        var expected = areaHasTheLowerId
            ? new[] { projectLock, $"lock area update {area.Id}", $"lock area share {parent.Id}", "save" }
            : new[] { projectLock, $"lock area share {parent.Id}", $"lock area update {area.Id}", "save" };
        Assert.Equal(expected, f.Store.Calls);
    }

    // US-5 · "A node moves ...": a sibling of the same name committed under the new parent after the pre-check fails the
    // save on the sibling index, and the repository's answer reaches the client unchanged.
    [Fact]
    public async Task Handle_LosesTheSiblingNameRace_AnswersAreaNameTaken()
    {
        var f = new Fixture();
        var reports = f.Store.AddArea(f.Root, "Отчёты");
        var archive = f.Store.AddArea(f.Root, "Архив");
        f.Store.FailNextSave(new ConflictException(OrganizationErrorCodes.AreaNameTaken));

        var refusal = await Assert.ThrowsAsync<ConflictException>(() => f.HandleAsync(f.Command(reports, archive)));

        Assert.Equal(OrganizationErrorCodes.AreaNameTaken, refusal.Code);
        Assert.Equal(1, f.Store.SaveCount);
    }
}
