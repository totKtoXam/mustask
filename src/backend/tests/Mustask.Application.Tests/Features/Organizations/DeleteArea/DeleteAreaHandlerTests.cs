using Mustask.Application.Features.Organizations.DeleteArea;
using Mustask.Application.Tests.TestDoubles;
using Mustask.Application.Tests.TestDoubles.Organizations;
using Mustask.Domain.Organizations;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Tests.Features.Organizations.DeleteArea;

/// <summary>
/// <c>DeleteArea</c> on fakes: what it removes, the order of its answers
/// (plan-backend § 5.3, api-contract § 0.4) and its one lock (§ 8.2). The
/// lanes' races are the Api tests'; here a refusal shows that nothing is saved.
/// </summary>
public sealed class DeleteAreaHandlerTests
{
    private sealed class Fixture
    {
        public Fixture()
        {
            Organization = Store.AddOrganization();
            Project = Store.AddProject(Organization);
            Platform = Store.AddTeam(Project);
            Reports = Store.AddArea(Project.Root, "Отчёты");
            Quarterly = Store.AddArea(Reports, "Квартальные");
            Handler = new DeleteAreaHandler(
                new FakeProjectRepository(Store),
                new FakeTeamRepository(Store),
                new FakeAreaRepository(Store),
                new FakeUnitOfWork());
        }

        public InMemoryOrganizations Store { get; } = new();

        public Organization Organization { get; }

        public NewProject Project { get; }

        /// <summary>The team «Платформа», with its own area under the root: owned by it and its default.</summary>
        public NewTeam Platform { get; }

        /// <summary>«Отчёты» under the root, with the leaf child «Квартальные».</summary>
        public Area Reports { get; }

        public Area Quarterly { get; }

        public DeleteAreaHandler Handler { get; }

        public DeleteAreaCommand Command(string? areaId, string? projectId = null, string? organizationId = null) =>
            new(organizationId ?? Organization.Id.ToString(), projectId ?? Project.Project.Id.ToString(), areaId);

        public DeleteAreaCommand Command(Area area) => Command(area.Id.ToString());

        public Task HandleAsync(DeleteAreaCommand command) => Handler.Handle(command, CancellationToken.None);
    }

    // US-5 · "Only an empty leaf is deleted": «Квартальные» has no children and is no team's default; it disappears and
    // its neighbours stay. One lock, on the area, then one save.
    [Fact]
    public async Task Handle_EmptyLeaf_RemovesOnlyIt_LocksTheAreaOnce_SavesOnce()
    {
        var f = new Fixture();

        await f.HandleAsync(f.Command(f.Quarterly));

        Assert.Equal([$"lock area update {f.Quarterly.Id}", "save"], f.Store.Calls);
        Assert.DoesNotContain(f.Store.Areas, area => area.Id == f.Quarterly.Id);
        Assert.Contains(f.Store.Areas, area => area.Id == f.Reports.Id);
        Assert.Contains(f.Store.Areas, area => area.Id == f.Platform.Area.Id);
        Assert.Contains(f.Store.Areas, area => area.Id == f.Project.Root.Id);
    }

    // US-5 · "Only an empty leaf is deleted", "no action restores it": the second request finds nothing.
    [Fact]
    public async Task Handle_DeletedAgain_IsAreaNotFound()
    {
        var f = new Fixture();
        await f.HandleAsync(f.Command(f.Quarterly));

        var refusal = await Assert.ThrowsAsync<NotFoundException>(() => f.HandleAsync(f.Command(f.Quarterly)));

        Assert.Equal(OrganizationErrorCodes.AreaNotFound, refusal.Code);
        Assert.Equal(1, f.Store.SaveCount);
    }

    // US-5 · "Only an empty leaf is deleted": «Отчёты» is refused while «Квартальные» is under it, and is a leaf, and so
    // deletable, once «Квартальные» is gone.
    [Fact]
    public async Task Handle_NonLeaf_IsAreaNotEmpty_NothingRemoved_ThenDeletableOnceItsLeafIsGone()
    {
        var f = new Fixture();
        var before = f.Store.Areas.Count;

        var refusal = await Assert.ThrowsAsync<ConflictException>(() => f.HandleAsync(f.Command(f.Reports)));

        Assert.Equal(OrganizationErrorCodes.AreaNotEmpty, refusal.Code);
        Assert.Equal(before, f.Store.Areas.Count);
        Assert.Equal([$"lock area update {f.Reports.Id}"], f.Store.Calls);

        await f.HandleAsync(f.Command(f.Quarterly));
        await f.HandleAsync(f.Command(f.Reports));

        Assert.Equal(before - 2, f.Store.Areas.Count);
        Assert.Equal(2, f.Store.SaveCount);
    }

    // US-5 · "root comes with the project" and BR-M1-11: the root has children and is the first team's default area, and
    // still the refusal is root_is_fixed (api-contract § 0.4 step 2 before the 409s of step 5).
    [Fact]
    public async Task Handle_Root_IsRootIsFixed_BeforeTheChildrenAndTheDefault()
    {
        var f = new Fixture();
        var before = f.Store.Areas.Count;

        var refusal = await Assert.ThrowsAsync<ConflictException>(() => f.HandleAsync(f.Command(f.Project.Root)));

        Assert.Equal(OrganizationErrorCodes.AreaRootIsFixed, refusal.Code);
        Assert.Equal(before, f.Store.Areas.Count);
        Assert.Equal(0, f.Store.SaveCount);
    }

    // US-6 · "A team's default area stays with its team": a leaf that is «Платформа»'s default is refused, naming the team.
    [Fact]
    public async Task Handle_DefaultLeaf_IsAreaIsTeamDefault_NamingTheTeam_NothingRemoved()
    {
        var f = new Fixture();

        var refusal = await Assert.ThrowsAsync<ConflictException>(() => f.HandleAsync(f.Command(f.Platform.Area)));

        Assert.Equal(OrganizationErrorCodes.AreaIsTeamDefault, refusal.Code);
        Assert.Equal("Платформа", refusal.Params["team"].Value);
        Assert.Contains(f.Store.Areas, area => area.Id == f.Platform.Area.Id);
        Assert.Equal(0, f.Store.SaveCount);
    }

    // The order inside step 5: a default area with a child is refused for the child first, as the domain orders them.
    [Fact]
    public async Task Handle_DefaultAreaWithAChild_IsAreaNotEmpty_BeforeTheDefault()
    {
        var f = new Fixture();
        f.Store.AddArea(f.Platform.Area, "API", f.Platform.Team);

        var refusal = await Assert.ThrowsAsync<ConflictException>(() => f.HandleAsync(f.Command(f.Platform.Area)));

        Assert.Equal(OrganizationErrorCodes.AreaNotEmpty, refusal.Code);
    }

    // US-6 · "once the team's default is changed to «Отчёты», the same actions are accepted": the former default is deleted.
    [Fact]
    public async Task Handle_FormerDefault_AfterTheDefaultMoved_IsDeleted()
    {
        var f = new Fixture();
        var archive = f.Store.AddArea(f.Project.Root, "Архив", f.Platform.Team);
        var former = f.Platform.Area;
        former.ClearDefault();
        archive.MarkDefaultOf(f.Platform.Team);

        await f.HandleAsync(f.Command(former));

        Assert.DoesNotContain(f.Store.Areas, area => area.Id == former.Id);
        Assert.Equal(f.Platform.Team.Id, f.Store.Areas.Single(area => area.Id == archive.Id).DefaultOfTeamId);
    }

    // BR-M1-20: behind the filter the organization id is well formed; if it were not, the outermost level answers.
    [Fact]
    public async Task Handle_MalformedOrganization_IsOrganizationNotFound()
    {
        var f = new Fixture();

        var refusal = await Assert.ThrowsAsync<NotFoundException>(() => f.HandleAsync(f.Command(f.Quarterly.Id.ToString(), organizationId: "abc")));

        Assert.Equal(OrganizationErrorCodes.OrganizationNotFound, refusal.Code);
        Assert.Empty(f.Store.Calls);
    }

    // US-3 · "A project id names no project of the organization" and "a project is deactivated" (BR-M1-20, BR-M1-22): the
    // project level answers before the area is looked at, and nothing is locked.
    [Theory]
    [InlineData("malformed")]
    [InlineData("unknown")]
    [InlineData("of another organization")]
    [InlineData("deactivated")]
    [InlineData("under a deactivated organization")]
    public async Task Handle_ProjectNotReachable_IsProjectNotFound_BeforeTheArea(string how)
    {
        var f = new Fixture();
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

        var before = f.Store.Areas.Count;

        var refusal = await Assert.ThrowsAsync<NotFoundException>(() => f.HandleAsync(f.Command("x", projectId)));

        Assert.Equal(OrganizationErrorCodes.ProjectNotFound, refusal.Code);
        Assert.Empty(f.Store.Calls);
        Assert.Equal(before, f.Store.Areas.Count);
    }

    // US-5 · "A node never leaves its project" (BR-M1-22) read for the addressed area: an id that is null, empty, malformed,
    // all zeros, unknown, or an area of another project of the organization answers area.not_found, and nothing is removed.
    [Theory]
    [InlineData("null")]
    [InlineData("empty")]
    [InlineData("malformed")]
    [InlineData("all zeros")]
    [InlineData("unknown")]
    [InlineData("of another project")]
    public async Task Handle_AreaNotAnAreaOfTheProject_IsAreaNotFound_NothingRemoved(string how)
    {
        var f = new Fixture();
        var other = f.Store.AddProject(f.Organization, "Второй проект", "SEC");
        var foreign = f.Store.AddArea(other.Root, "Чужая");
        string? areaId = how switch
        {
            "null" => null,
            "empty" => "",
            "malformed" => "{" + f.Quarterly.Id + "}",
            "all zeros" => Guid.Empty.ToString(),
            "unknown" => Guid.CreateVersion7().ToString(),
            _ => foreign.Id.ToString(),
        };
        var before = f.Store.Areas.Count;

        var refusal = await Assert.ThrowsAsync<NotFoundException>(() => f.HandleAsync(f.Command(areaId)));

        Assert.Equal(OrganizationErrorCodes.AreaNotFound, refusal.Code);
        Assert.Equal(0, f.Store.SaveCount);
        Assert.Equal(before, f.Store.Areas.Count);
        Assert.Contains(f.Store.Areas, area => area.Id == foreign.Id);
    }
}
