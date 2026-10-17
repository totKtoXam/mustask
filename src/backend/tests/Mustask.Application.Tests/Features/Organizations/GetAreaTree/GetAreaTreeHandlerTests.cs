using Mustask.Application.Features.Organizations.GetAreaTree;
using Mustask.Application.Tests.TestDoubles.Organizations;
using Mustask.Domain.Organizations;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Tests.Features.Organizations.GetAreaTree;

/// <summary>
/// The area tree read on fakes (mod-017 api-contract § 1.6): what the handler assembles from the read model's rows through
/// <see cref="AreaTree"/> — place, name, path and depth, no team — and the address answers that come before it
/// (plan-backend § 5.3).
/// </summary>
public sealed class GetAreaTreeHandlerTests
{
    private const string RootName = "Пример проекта";

    private sealed class Fixture
    {
        /// <summary>
        /// The tree every test but the root-alone one reads, siblings listed here out of display order on purpose:
        /// <code>
        /// Пример проекта      root
        ///   архив
        ///   Отчёты
        ///     Годовые
        ///     Квартальные
        ///       Март
        /// </code>
        /// </summary>
        public Fixture()
        {
            Organization = Store.AddOrganization();
            Project = Store.AddProject(Organization);
            Platform = Store.AddLinkedTeam(Organization, Project);
            Reports = Store.AddArea(Project.Root, "Отчёты");
            Quarterly = Store.AddArea(Reports, "Квартальные");
            March = Store.AddArea(Quarterly, "Март");
            Yearly = Store.AddArea(Reports, "Годовые");
            Archive = Store.AddArea(Project.Root, "архив");
            Handler = new GetAreaTreeHandler(new FakeAreaTreeReadModel(Store));
        }

        public InMemoryOrganizations Store { get; } = new();

        public Organization Organization { get; }

        public NewProject Project { get; }

        public Team Platform { get; }

        public Area Reports { get; }

        public Area Quarterly { get; }

        public Area March { get; }

        public Area Yearly { get; }

        public Area Archive { get; }

        public GetAreaTreeHandler Handler { get; }

        public GetAreaTreeQuery Query(string? organizationId = null, string? projectId = null) =>
            new(organizationId ?? Organization.Id.ToString(), projectId ?? Project.Project.Id.ToString());

        public async Task<IReadOnlyList<AreaNodeResponse>> ReadAsync() =>
            (await Handler.Handle(Query(), CancellationToken.None)).Items;
    }

    // US-5 · "tree registry": each node before its children, siblings in display order (Russian, ignoring letter case), the root first.
    [Fact]
    public async Task Handle_ReturnsPreOrder_SiblingsInDisplayOrderIgnoringCase_RootFirst()
    {
        var f = new Fixture();

        var items = await f.ReadAsync();

        Assert.Equal(
            [RootName, "архив", "Отчёты", "Годовые", "Квартальные", "Март"],
            items.Select(item => item.Name));
        Assert.Equal(
            [f.Project.Root.Id, f.Archive.Id, f.Reports.Id, f.Yearly.Id, f.Quarterly.Id, f.March.Id],
            items.Select(item => item.Id));
        Assert.Equal(
            [null, f.Project.Root.Id, f.Project.Root.Id, f.Reports.Id, f.Reports.Id, f.Quarterly.Id],
            items.Select(item => item.ParentId));
    }

    // US-5 · "tree registry" and DC-9: the root's path is its own name, every other path is the names below the root joined by " / ".
    [Fact]
    public async Task Handle_Paths_RootIsItsOwnName_OthersAreTheNamesBelowTheRoot()
    {
        var f = new Fixture();

        var items = await f.ReadAsync();

        Assert.Equal(
            [RootName, "архив", "Отчёты", "Отчёты / Годовые", "Отчёты / Квартальные", "Отчёты / Квартальные / Март"],
            items.Select(item => item.Path));
    }

    // US-5 · "tree registry": the root is at depth 0, a child one deeper than its parent.
    [Fact]
    public async Task Handle_Depths_RootIsZero_EachChildOneDeeper()
    {
        var f = new Fixture();

        var items = await f.ReadAsync();

        Assert.Equal([0, 1, 1, 2, 2, 3], items.Select(item => item.Depth));
    }

    // mod-017 api-contract § 1.6 (BR-OT-10): a node is its place, its name, its path and its depth, and nothing about a team —
    // not an owner, not a resolved owner, not a default. Replaces the owner assertions of
    // Handle_ResolvedOwners_NearestExplicitAncestor_InheritedMarksWhereNoExplicitOwner,
    // Handle_Root_IsOwnedExplicitlyByTheFirstTeam_NeverInherited and Handle_DefaultOfTeam_NamesTheTeamOnItsDefaultAreaOnly
    // (retired, BR-OT-10).
    [Fact]
    public void AreaNodeResponse_IsExactlyThePlaceTheNameThePathAndTheDepth()
    {
        var shape = typeof(AreaNodeResponse).GetProperties().Select(property => property.Name).Order(StringComparer.Ordinal);

        Assert.Equal(["Depth", "Id", "Name", "ParentId", "Path"], shape);
    }

    // US-5 · "root only": a new project's tree is its root alone.
    [Fact]
    public async Task Handle_NewProject_IsTheRootAlone()
    {
        var store = new InMemoryOrganizations();
        var organization = store.AddOrganization();
        var project = store.AddProject(organization);
        var handler = new GetAreaTreeHandler(new FakeAreaTreeReadModel(store));

        var result = await handler.Handle(
            new GetAreaTreeQuery(organization.Id.ToString(), project.Project.Id.ToString()),
            CancellationToken.None);

        var root = Assert.Single(result.Items);
        Assert.Equal(new AreaNodeResponse(project.Root.Id, null, RootName, RootName, 0), root);
    }

    // mod-017 US-4 · "renaming a team changes no area": the tree is the same before and after.
    [Fact]
    public async Task Handle_TeamRenamed_TreeIsUnchanged()
    {
        var f = new Fixture();
        var before = await f.ReadAsync();
        f.Platform.Rename(InMemoryOrganizations.Name("Ядро"));

        var after = await f.ReadAsync();

        Assert.Equal(before, after);
    }

    // DC-9: every node's path and depth is what AreaTree gives for the same nodes, the one definition the work item reads share.
    // Rewritten without the owner (D-16, BR-OT-10): Handle_EveryNode_EqualsWhatAreaTreeResolves keeps its name.
    [Fact]
    public async Task Handle_EveryNode_EqualsWhatAreaTreeResolves()
    {
        var f = new Fixture();
        var tree = new AreaTree(f.Store.Areas.Select(area => new AreaNode(area.Id, area.ParentId, area.Name)));

        var items = await f.ReadAsync();

        Assert.Equal(f.Store.Areas.Count, items.Count);
        foreach (var item in items)
        {
            Assert.Equal(tree.PathOf(item.Id), item.Path);
            Assert.Equal(tree.DepthOf(item.Id), item.Depth);
        }
    }

    // A read takes no lock and writes nothing (plan-backend § 5.3, § 7).
    [Fact]
    public async Task Handle_LocksNothingAndSavesNothing()
    {
        var f = new Fixture();

        await f.ReadAsync();

        Assert.Empty(f.Store.Calls);
    }

    // BR-M1-20: behind the filter the organization id is well formed; if it were not, the outermost level answers,
    // even when the project id is malformed too.
    [Theory]
    [InlineData("abc")]
    [InlineData("")]
    [InlineData(null)]
    public async Task Handle_MalformedOrganization_IsOrganizationNotFound_BeforeTheProject(string? organizationId)
    {
        var f = new Fixture();

        var refusal = await Assert.ThrowsAsync<NotFoundException>(() => f.Handler.Handle(
            new GetAreaTreeQuery(organizationId, "not-an-id"), CancellationToken.None));

        Assert.Equal(OrganizationErrorCodes.OrganizationNotFound, refusal.Code);
    }

    // US-3 · "A project id names no project of the organization" and "a project is deactivated" (BR-M1-20, BR-M1-22,
    // ADR-0026 rule 5): malformed, unknown, of another organization, deactivated, or under a deactivated organization.
    [Theory]
    [InlineData("malformed")]
    [InlineData("empty")]
    [InlineData("unknown")]
    [InlineData("of another organization")]
    [InlineData("deactivated")]
    [InlineData("under a deactivated organization")]
    public async Task Handle_ProjectNotReachable_IsProjectNotFound(string how)
    {
        var f = new Fixture();
        var other = f.Store.AddProject(f.Store.AddOrganization("Другая организация"), "Чужой", "OTH");
        var projectId = how switch
        {
            "malformed" => "PRM",
            "empty" => "",
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

        var refusal = await Assert.ThrowsAsync<NotFoundException>(() => f.Handler.Handle(
            f.Query(projectId: projectId), CancellationToken.None));

        Assert.Equal(OrganizationErrorCodes.ProjectNotFound, refusal.Code);
    }
}
