using Mustask.Application.Features.Organizations.GetAreaTree;
using Mustask.Application.Tests.TestDoubles.Organizations;
using Mustask.Domain.Organizations;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Tests.Features.Organizations.GetAreaTree;

/// <summary>
/// The area tree read on fakes (api-contract § 4.1): what the handler assembles
/// from the read model's rows through <see cref="AreaTree"/>, and the address
/// answers that come before it (plan-backend § 5.3).
/// </summary>
public sealed class GetAreaTreeHandlerTests
{
    private const string FirstTeamName = "Пример проекта";

    private sealed class Fixture
    {
        /// <summary>
        /// The tree every test but the root-alone one reads, siblings listed here out of display order on purpose:
        /// <code>
        /// Пример проекта      root, owned by the first team, its default
        ///   архив             inherits the first team
        ///   Отчёты            owned by «Платформа»
        ///     Годовые         owned by the first team
        ///     Квартальные     inherits «Платформа»
        ///       Март          inherits «Платформа»
        ///   Платформа         owned by «Платформа», its default
        /// </code>
        /// </summary>
        public Fixture()
        {
            Organization = Store.AddOrganization();
            Project = Store.AddProject(Organization);
            Platform = Store.AddTeam(Project);
            Reports = Store.AddArea(Project.Root, "Отчёты", Platform.Team);
            Quarterly = Store.AddArea(Reports, "Квартальные");
            March = Store.AddArea(Quarterly, "Март");
            Yearly = Store.AddArea(Reports, "Годовые", Project.FirstTeam);
            Archive = Store.AddArea(Project.Root, "архив");
            Handler = new GetAreaTreeHandler(new FakeAreaTreeReadModel(Store));
        }

        public InMemoryOrganizations Store { get; } = new();

        public Organization Organization { get; }

        public NewProject Project { get; }

        public NewTeam Platform { get; }

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
            [FirstTeamName, "архив", "Отчёты", "Годовые", "Квартальные", "Март", "Платформа"],
            items.Select(item => item.Name));
        Assert.Equal(
            [f.Project.Root.Id, f.Archive.Id, f.Reports.Id, f.Yearly.Id, f.Quarterly.Id, f.March.Id, f.Platform.Area.Id],
            items.Select(item => item.Id));
        Assert.Equal(
            [null, f.Project.Root.Id, f.Project.Root.Id, f.Reports.Id, f.Reports.Id, f.Quarterly.Id, f.Project.Root.Id],
            items.Select(item => item.ParentId));
    }

    // US-5 · "tree registry" and DC-9: the root's path is its own name, every other path is the names below the root joined by " / ".
    [Fact]
    public async Task Handle_Paths_RootIsItsOwnName_OthersAreTheNamesBelowTheRoot()
    {
        var f = new Fixture();

        var items = await f.ReadAsync();

        Assert.Equal(
            [FirstTeamName, "архив", "Отчёты", "Отчёты / Годовые", "Отчёты / Квартальные", "Отчёты / Квартальные / Март", "Платформа"],
            items.Select(item => item.Path));
    }

    // US-5 · "tree registry": the root is at depth 0, a child one deeper than its parent.
    [Fact]
    public async Task Handle_Depths_RootIsZero_EachChildOneDeeper()
    {
        var f = new Fixture();

        var items = await f.ReadAsync();

        Assert.Equal([0, 1, 1, 2, 2, 3, 1], items.Select(item => item.Depth));
    }

    // US-6 · "An area inherits its owner from the nearest owned ancestor": the explicit owner stays as stored, the resolved
    // owner is the nearest explicit one, and the inherited mark is set exactly where the explicit owner is null.
    [Fact]
    public async Task Handle_ResolvedOwners_NearestExplicitAncestor_InheritedMarksWhereNoExplicitOwner()
    {
        var f = new Fixture();
        var first = f.Project.FirstTeam.Id;
        var platform = f.Platform.Team.Id;

        var items = await f.ReadAsync();

        Assert.Equal([first, null, platform, first, null, null, platform], items.Select(item => item.OwnerTeamId));
        Assert.Equal(
            [first, first, platform, first, platform, platform, platform],
            items.Select(item => item.ResolvedOwner.Id));
        Assert.Equal(
            [FirstTeamName, FirstTeamName, "Платформа", FirstTeamName, "Платформа", "Платформа", "Платформа"],
            items.Select(item => item.ResolvedOwner.Name));
        Assert.Equal([false, true, false, false, true, true, false], items.Select(item => item.OwnerInherited));
        Assert.All(items, item => Assert.Equal(item.OwnerTeamId is null, item.OwnerInherited));
    }

    // US-6 · "An area inherits ...": "at the latest the root's", and the root's own owner is never null (BR-M1-11).
    [Fact]
    public async Task Handle_Root_IsOwnedExplicitlyByTheFirstTeam_NeverInherited()
    {
        var f = new Fixture();

        var root = (await f.ReadAsync())[0];

        Assert.Null(root.ParentId);
        Assert.Equal(f.Project.FirstTeam.Id, root.OwnerTeamId);
        Assert.Equal(new AreaTeamResponse(f.Project.FirstTeam.Id, FirstTeamName), root.ResolvedOwner);
        Assert.False(root.OwnerInherited);
    }

    // US-4 · "default among explicitly owned areas": a node names the team whose default it is, and only that node does.
    [Fact]
    public async Task Handle_DefaultOfTeam_NamesTheTeamOnItsDefaultAreaOnly()
    {
        var f = new Fixture();

        var items = await f.ReadAsync();

        Assert.Equal(
            [new AreaTeamResponse(f.Project.FirstTeam.Id, FirstTeamName), null, null, null, null, null, new AreaTeamResponse(f.Platform.Team.Id, "Платформа")],
            items.Select(item => item.DefaultOfTeam));
    }

    // US-5 · "root only": a new project's tree is its root alone, owned by the first team, the first team's default.
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
        var firstTeam = new AreaTeamResponse(project.FirstTeam.Id, FirstTeamName);
        Assert.Equal(
            new AreaNodeResponse(project.Root.Id, null, FirstTeamName, FirstTeamName, 0, project.FirstTeam.Id, firstTeam, false, firstTeam),
            root);
    }

    // US-4 · "team renamed": the tree names the team as it is called now, and the areas keep their names.
    [Fact]
    public async Task Handle_TeamRenamed_TreeNamesTheTeamNow_AreaNamesKept()
    {
        var f = new Fixture();
        f.Platform.Team.Rename(InMemoryOrganizations.Name("Ядро"));

        var items = await f.ReadAsync();

        var reports = items.Single(item => item.Id == f.Reports.Id);
        Assert.Equal(new AreaTeamResponse(f.Platform.Team.Id, "Ядро"), reports.ResolvedOwner);
        Assert.Equal(new AreaTeamResponse(f.Platform.Team.Id, "Ядро"), items.Single(item => item.Id == f.March.Id).ResolvedOwner);
        Assert.Equal(new AreaTeamResponse(f.Platform.Team.Id, "Ядро"), items.Single(item => item.Id == f.Platform.Area.Id).DefaultOfTeam);
        Assert.Equal("Платформа", items.Single(item => item.Id == f.Platform.Area.Id).Name);
    }

    // US-6 · "other modules get the same answer as the tree": every node's path, depth and resolved owner is what
    // AreaTree gives for the same nodes, the one definition the seam shares.
    [Fact]
    public async Task Handle_EveryNode_EqualsWhatAreaTreeResolves()
    {
        var f = new Fixture();
        var tree = new AreaTree(f.Store.Areas.Select(area => new AreaNode(area.Id, area.ParentId, area.Name, area.OwnerTeamId, area.DefaultOfTeamId)));

        var items = await f.ReadAsync();

        Assert.Equal(f.Store.Areas.Count, items.Count);
        foreach (var item in items)
        {
            var owner = tree.ResolveOwner(item.Id);
            Assert.Equal(tree.PathOf(item.Id), item.Path);
            Assert.Equal(tree.DepthOf(item.Id), item.Depth);
            Assert.Equal(owner.TeamId, item.ResolvedOwner.Id);
            Assert.Equal(owner.IsInherited, item.OwnerInherited);
        }
    }

    // A read takes no lock and writes nothing (plan-backend § 5.3, § 8.2).
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
