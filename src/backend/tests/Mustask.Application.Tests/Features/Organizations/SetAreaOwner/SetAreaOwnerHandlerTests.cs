using Mustask.Application.Abstractions.Organizations;
using Mustask.Application.Features.Organizations.SetAreaOwner;
using Mustask.Application.Tests.TestDoubles;
using Mustask.Application.Tests.TestDoubles.Organizations;
using Mustask.Domain.Organizations;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Tests.Features.Organizations.SetAreaOwner;

/// <summary>
/// <c>PUT …/areas/{areaId}/owner</c> on fakes (plan-backend § 14.3, L-24): what
/// setting and clearing an owner stores, the refusals of the root and of a
/// team's default area, the order of answers (§ 5.3, api-contract § 0.4) and the
/// lock order (§ 8.2).
/// </summary>
public sealed class SetAreaOwnerHandlerTests
{
    private sealed class Fixture
    {
        public Fixture(Func<InMemoryOrganizations, ITeamRepository>? teams = null)
        {
            Organization = Store.AddOrganization();
            Project = Store.AddProject(Organization);
            Platform = Store.AddTeam(Project);
            Handler = new SetAreaOwnerHandler(
                new FakeProjectRepository(Store),
                teams?.Invoke(Store) ?? new FakeTeamRepository(Store),
                new FakeAreaRepository(Store),
                new FakeUnitOfWork());
        }

        public InMemoryOrganizations Store { get; } = new();

        public Organization Organization { get; }

        public NewProject Project { get; }

        /// <summary>The second team «Платформа», with its area under the root: owned by it and its default.</summary>
        public NewTeam Platform { get; }

        public Team FirstTeam => Project.FirstTeam;

        public SetAreaOwnerHandler Handler { get; }

        public AreaTree Tree() =>
            new(Store.Areas.Select(area => new AreaNode(area.Id, area.ParentId, area.Name, area.OwnerTeamId, area.DefaultOfTeamId)));

        /// <summary>A command for <paramref name="area"/>; a <c>null</c> owner clears it, as the contract has it.</summary>
        public SetAreaOwnerCommand Command(Area area, Team? owner) => Command(area.Id.ToString(), owner?.Id.ToString());

        public SetAreaOwnerCommand Command(string? areaId, string? ownerTeamId, string? projectId = null, string? organizationId = null) =>
            new(organizationId ?? Organization.Id.ToString(), projectId ?? Project.Project.Id.ToString(), areaId, ownerTeamId);

        public Task<SetAreaOwnerResult> HandleAsync(SetAreaOwnerCommand command) => Handler.Handle(command, CancellationToken.None);
    }

    // US-6 · "An area is re-owned": «Отчёты», owned explicitly by the first team and no team's default, with the child
    // «Квартальные» that has no explicit owner, is re-owned to «Платформа»; the child follows, marked inherited.
    [Fact]
    public async Task Handle_Reown_StoresTheOwner_ChildrenInheritIt()
    {
        var f = new Fixture();
        var reports = f.Store.AddArea(f.Project.Root, "Отчёты", f.FirstTeam);
        var quarterly = f.Store.AddArea(reports, "Квартальные");

        var result = await f.HandleAsync(f.Command(reports, f.Platform.Team));

        Assert.Equal(new AreaResponse(reports.Id, f.Project.Project.Id, f.Project.Root.Id, "Отчёты", f.Platform.Team.Id), result.Response);
        var tree = f.Tree();
        Assert.Equal(new ResolvedOwner(f.Platform.Team.Id, false), tree.ResolveOwner(reports.Id));
        Assert.Equal(new ResolvedOwner(f.Platform.Team.Id, true), tree.ResolveOwner(quarterly.Id));
        Assert.Null(f.Store.Areas.Single(area => area.Id == reports.Id).DefaultOfTeamId);
    }

    // US-6 · "An area with no explicit owner inherits": an inheriting area is given an explicit owner of its own.
    [Fact]
    public async Task Handle_SetOnAnInheritingArea_StoresTheExplicitOwner()
    {
        var f = new Fixture();
        var reports = f.Store.AddArea(f.Project.Root, "Отчёты");

        var result = await f.HandleAsync(f.Command(reports, f.Platform.Team));

        Assert.Equal(f.Platform.Team.Id, result.Response.OwnerTeamId);
        Assert.Equal(new ResolvedOwner(f.Platform.Team.Id, false), f.Tree().ResolveOwner(reports.Id));
    }

    // US-6 · "An explicit owner is cleared": «Отчёты», owned by «Платформа» and no team's default, loses its owner, and it
    // and its children resolve to the first team, the root's owner, marked inherited.
    [Fact]
    public async Task Handle_Clear_ResolvesToTheRootOwner_Inherited()
    {
        var f = new Fixture();
        var reports = f.Store.AddArea(f.Project.Root, "Отчёты", f.Platform.Team);
        var quarterly = f.Store.AddArea(reports, "Квартальные");

        var result = await f.HandleAsync(f.Command(reports, owner: null));

        Assert.Null(result.Response.OwnerTeamId);
        Assert.Null(f.Store.Areas.Single(area => area.Id == reports.Id).OwnerTeamId);
        var tree = f.Tree();
        Assert.Equal(new ResolvedOwner(f.FirstTeam.Id, true), tree.ResolveOwner(reports.Id));
        Assert.Equal(new ResolvedOwner(f.FirstTeam.Id, true), tree.ResolveOwner(quarterly.Id));
    }

    // api-contract § 4.5: "Setting the current owner again answers 200 and changes nothing" — and so does clearing an area
    // that has no explicit owner. Nothing is written.
    [Theory]
    [InlineData("the same owner")]
    [InlineData("clearing an inheriting area")]
    public async Task Handle_NothingToChange_Returns200AndSavesNothing(string how)
    {
        var f = new Fixture();
        var reports = f.Store.AddArea(f.Project.Root, "Отчёты", how == "the same owner" ? f.Platform.Team : null);

        var result = await f.HandleAsync(f.Command(reports, how == "the same owner" ? f.Platform.Team : null));

        Assert.Equal(reports.OwnerTeamId, result.Response.OwnerTeamId);
        Assert.Equal(0, f.Store.SaveCount);
    }

    // US-6 · "A team's default area stays with its team": the owner it already has is not a change, so the default area
    // answers 200 to its own team and stays the team's default.
    [Fact]
    public async Task Handle_DefaultAreaSetToItsOwnTeam_Returns200_DefaultKept()
    {
        var f = new Fixture();

        var result = await f.HandleAsync(f.Command(f.Platform.Area, f.Platform.Team));

        Assert.Equal(f.Platform.Team.Id, result.Response.OwnerTeamId);
        Assert.Equal(f.Platform.Team.Id, f.Platform.Area.DefaultOfTeamId);
        Assert.Equal(0, f.Store.SaveCount);
    }

    // US-6 · "The root's owner is fixed": re-owning and clearing, requested directly, are refused and the root stays owned
    // by the first team. The rule about the addressed area answers before the body's team is looked at (§ 0.4 step 2 before 4).
    [Theory]
    [InlineData("re-own")]
    [InlineData("clear")]
    [InlineData("re-own to a team that does not exist")]
    public async Task Handle_Root_IsRootIsFixed_BeforeTheTeamIsLooked(string how)
    {
        var f = new Fixture();
        var ownerTeamId = how switch
        {
            "re-own" => f.Platform.Team.Id.ToString(),
            "clear" => null,
            _ => "not-a-team",
        };

        var refusal = await Assert.ThrowsAsync<ConflictException>(() => f.HandleAsync(f.Command(f.Project.Root.Id.ToString(), ownerTeamId)));

        Assert.Equal(OrganizationErrorCodes.AreaRootIsFixed, refusal.Code);
        Assert.Equal(f.FirstTeam.Id, f.Project.Root.OwnerTeamId);
        Assert.Empty(f.Store.Calls);
    }

    // US-6 · "A team's default area stays with its team": «Платформа», the default area of the team «Платформа», is
    // refused a re-own and a clear, with the team's name, and keeps its owner and its default mark.
    [Theory]
    [InlineData("re-own")]
    [InlineData("clear")]
    public async Task Handle_DefaultArea_ReownOrClear_IsAreaIsTeamDefault_NamingTheTeam(string how)
    {
        var f = new Fixture();
        var owner = how == "re-own" ? f.FirstTeam : null;

        var refusal = await Assert.ThrowsAsync<ConflictException>(() => f.HandleAsync(f.Command(f.Platform.Area, owner)));

        Assert.Equal(OrganizationErrorCodes.AreaIsTeamDefault, refusal.Code);
        Assert.Equal("Платформа", refusal.Params["team"].Value);
        Assert.Equal(f.Platform.Team.Id, f.Platform.Area.OwnerTeamId);
        Assert.Equal(f.Platform.Team.Id, f.Platform.Area.DefaultOfTeamId);
        Assert.Equal(0, f.Store.SaveCount);
    }

    // US-6 · "…once the team's default is changed to «Отчёты», the same actions are allowed": the former default is
    // re-owned and cleared at will, and the new default is refused instead.
    [Fact]
    public async Task Handle_AfterTheDefaultMoved_FormerDefaultIsFree_NewDefaultIsRefused()
    {
        var f = new Fixture();
        var reports = f.Store.AddArea(f.Project.Root, "Отчёты", f.Platform.Team);
        f.Platform.Area.ClearDefault();
        reports.MarkDefaultOf(f.Platform.Team);

        var reowned = await f.HandleAsync(f.Command(f.Platform.Area, f.FirstTeam));
        var cleared = await f.HandleAsync(f.Command(f.Platform.Area, owner: null));
        var refusal = await Assert.ThrowsAsync<ConflictException>(() => f.HandleAsync(f.Command(reports, f.FirstTeam)));

        Assert.Equal(f.FirstTeam.Id, reowned.Response.OwnerTeamId);
        Assert.Null(cleared.Response.OwnerTeamId);
        Assert.Equal(OrganizationErrorCodes.AreaIsTeamDefault, refusal.Code);
        Assert.Equal(f.Platform.Team.Id, reports.OwnerTeamId);
    }

    // US-6 · "An area's owner is a team of the area's own project" (BR-M1-22) and US-7 · "A deleted team answers not
    // found": a team that is malformed, empty, unknown, of another project or deleted answers team.not_found, and nothing changes.
    [Theory]
    [InlineData("malformed")]
    [InlineData("empty")]
    [InlineData("unknown")]
    [InlineData("of another project")]
    [InlineData("deleted")]
    public async Task Handle_OwnerNotAnActiveTeamOfTheProject_IsTeamNotFound_AreaUntouched(string how)
    {
        var f = new Fixture();
        var other = f.Store.AddProject(f.Organization, "Второй проект", "SEC");
        var reports = f.Store.AddArea(f.Project.Root, "Отчёты", f.FirstTeam);
        var ownerTeamId = how switch
        {
            "malformed" => "{" + f.Platform.Team.Id + "}",
            "empty" => "",
            "unknown" => Guid.CreateVersion7().ToString(),
            "of another project" => other.FirstTeam.Id.ToString(),
            _ => f.Platform.Team.Id.ToString(),
        };

        if (how == "deleted")
        {
            f.Platform.Team.Delete();
        }

        var refusal = await Assert.ThrowsAsync<NotFoundException>(() => f.HandleAsync(f.Command(reports.Id.ToString(), ownerTeamId)));

        Assert.Equal(OrganizationErrorCodes.TeamNotFound, refusal.Code);
        Assert.Equal(f.FirstTeam.Id, reports.OwnerTeamId);
        Assert.DoesNotContain(f.Store.Calls, call => call.StartsWith("lock area", StringComparison.Ordinal));
        Assert.Equal(0, f.Store.SaveCount);
    }

    // US-5 / BR-M1-22: an area that is malformed, unknown, or of another project answers area.not_found, and it answers
    // first: the body's team is wrong too, and nothing is locked.
    [Theory]
    [InlineData("malformed")]
    [InlineData("empty")]
    [InlineData("unknown")]
    [InlineData("of another project")]
    [InlineData("the root of another project")]
    public async Task Handle_AreaNotOfTheProject_IsAreaNotFound_BeforeTheTeam(string how)
    {
        var f = new Fixture();
        var other = f.Store.AddProject(f.Organization, "Второй проект", "SEC");
        var foreign = f.Store.AddArea(other.Root, "Чужая", other.FirstTeam);
        var areaId = how switch
        {
            "malformed" => "00000000-0000-0000-0000-000000000000",
            "empty" => "",
            "unknown" => Guid.CreateVersion7().ToString(),
            "of another project" => foreign.Id.ToString(),
            _ => other.Root.Id.ToString(),
        };

        var refusal = await Assert.ThrowsAsync<NotFoundException>(() => f.HandleAsync(f.Command(areaId, ownerTeamId: "not-a-team")));

        Assert.Equal(OrganizationErrorCodes.AreaNotFound, refusal.Code);
        Assert.Equal(other.FirstTeam.Id, foreign.OwnerTeamId);
        Assert.Empty(f.Store.Calls);
    }

    // US-3 · "A project id names no project of the organization" and "a project is deactivated" (BR-M1-20, BR-M1-22): the
    // project level answers before the area and the body are looked at, and nothing is locked.
    [Theory]
    [InlineData("malformed")]
    [InlineData("unknown")]
    [InlineData("of another organization")]
    [InlineData("deactivated")]
    [InlineData("under a deactivated organization")]
    public async Task Handle_ProjectNotReachable_IsProjectNotFound_BeforeTheAreaAndTheBody(string how)
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

        var refusal = await Assert.ThrowsAsync<NotFoundException>(() => f.HandleAsync(
            f.Command(areaId: "x", ownerTeamId: "y", projectId: projectId)));

        Assert.Equal(OrganizationErrorCodes.ProjectNotFound, refusal.Code);
        Assert.Empty(f.Store.Calls);
    }

    // BR-M1-20: behind the filter the organization id is well formed; if it were not, the outermost level answers.
    [Fact]
    public async Task Handle_MalformedOrganization_IsOrganizationNotFound()
    {
        var f = new Fixture();

        var refusal = await Assert.ThrowsAsync<NotFoundException>(() => f.HandleAsync(
            f.Command(areaId: "x", ownerTeamId: "y", organizationId: "abc")));

        Assert.Equal(OrganizationErrorCodes.OrganizationNotFound, refusal.Code);
        Assert.Empty(f.Store.Calls);
    }

    // plan-backend § 8.2: the new owner FOR SHARE, then the area FOR UPDATE, then one save; clearing takes no team lock.
    [Fact]
    public async Task Handle_LocksTheOwnerThenTheArea_ThenSavesOnce()
    {
        var f = new Fixture();
        var reports = f.Store.AddArea(f.Project.Root, "Отчёты", f.FirstTeam);

        await f.HandleAsync(f.Command(reports, f.Platform.Team));
        Assert.Equal([$"lock team share {f.Platform.Team.Id}", $"lock area update {reports.Id}", "save"], f.Store.Calls);

        f.Store.Calls.Clear();
        await f.HandleAsync(f.Command(reports, owner: null));
        Assert.Equal([$"lock area update {reports.Id}", "save"], f.Store.Calls);
    }

    // ADR-0026 rule 8, plan-backend § 8.2: the area is read again under its lock. A delete that commits while this
    // request waits for the team's lock leaves nothing to re-own: area.not_found, not a write to a gone row.
    [Fact]
    public async Task Handle_AreaDeletedWhileWaitingForTheTeam_IsAreaNotFound_SavesNothing()
    {
        Area? reports = null;
        var f = new Fixture(store => new DeletingAreaOnTeamLock(new FakeTeamRepository(store), store, () => reports!));
        reports = f.Store.AddArea(f.Project.Root, "Отчёты", f.FirstTeam);

        var refusal = await Assert.ThrowsAsync<NotFoundException>(() => f.HandleAsync(f.Command(reports, f.Platform.Team)));

        Assert.Equal(OrganizationErrorCodes.AreaNotFound, refusal.Code);
        Assert.Equal(0, f.Store.SaveCount);
    }

    /// <summary>
    /// A team repository whose share lock lets another transaction delete the
    /// area first: the lock waits for it, and what the handler reads after
    /// the lock is the committed outcome.
    /// </summary>
    private sealed class DeletingAreaOnTeamLock(ITeamRepository inner, InMemoryOrganizations store, Func<Area> area) : ITeamRepository
    {
        public async Task<Team?> LockForShareAsync(Guid projectId, Guid teamId, CancellationToken cancellationToken)
        {
            var team = await inner.LockForShareAsync(projectId, teamId, cancellationToken);
            store.Areas.Remove(area());

            return team;
        }

        public Task<Team?> LockForUpdateAsync(Guid projectId, Guid teamId, CancellationToken cancellationToken) =>
            inner.LockForUpdateAsync(projectId, teamId, cancellationToken);

        public Task<Team?> LockForNoKeyUpdateAsync(Guid projectId, Guid teamId, CancellationToken cancellationToken) =>
            inner.LockForNoKeyUpdateAsync(projectId, teamId, cancellationToken);

        public Task<Team> GetFirstTeamAsync(Guid projectId, CancellationToken cancellationToken) =>
            inner.GetFirstTeamAsync(projectId, cancellationToken);

        public Task<bool> IsActiveNameTakenAsync(Guid projectId, string normalizedName, Guid? exceptId, CancellationToken cancellationToken) =>
            inner.IsActiveNameTakenAsync(projectId, normalizedName, exceptId, cancellationToken);

        public Task<string?> FindNameAsync(Guid teamId, CancellationToken cancellationToken) =>
            inner.FindNameAsync(teamId, cancellationToken);

        public Task ReassignExplicitAreasAsync(Guid fromTeamId, Guid toTeamId, CancellationToken cancellationToken) =>
            inner.ReassignExplicitAreasAsync(fromTeamId, toTeamId, cancellationToken);

        public void Add(NewTeam team) => inner.Add(team);

        public Task SaveAsync(CancellationToken cancellationToken) => inner.SaveAsync(cancellationToken);
    }
}
