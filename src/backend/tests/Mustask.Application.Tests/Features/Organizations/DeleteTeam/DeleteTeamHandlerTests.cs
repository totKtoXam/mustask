using Mustask.Application.Features.Organizations.DeleteTeam;
using Mustask.Application.Tests.TestDoubles;
using Mustask.Application.Tests.TestDoubles.Organizations;
using Mustask.Domain.Organizations;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Tests.Features.Organizations.DeleteTeam;

/// <summary>
/// <c>DeleteTeam</c> on fakes (plan-backend § 14.3, L-20): the cascade of
/// BR-M1-18, the order of answers (§ 5.3, api-contract § 0.4) and the lock order
/// (§ 8.2).
/// </summary>
public sealed class DeleteTeamHandlerTests
{
    private sealed class Fixture
    {
        public Fixture()
        {
            Organization = Store.AddOrganization();
            Project = Store.AddProject(Organization);
            Platform = Store.AddTeam(Project);
            Handler = new DeleteTeamHandler(
                new FakeProjectRepository(Store),
                new FakeTeamRepository(Store),
                new FakeUnitOfWork());
        }

        public InMemoryOrganizations Store { get; } = new();

        public Organization Organization { get; }

        /// <summary>«Пример проекта» (PRM): the first team «Пример проекта» and the root, owned by it.</summary>
        public NewProject Project { get; }

        /// <summary>The second team «Платформа» with its area «Платформа», a child of the root, owned by it and its default.</summary>
        public NewTeam Platform { get; }

        public DeleteTeamHandler Handler { get; }

        public DeleteTeamCommand Command(string? teamId = null, string? projectId = null, string? organizationId = null) =>
            new(
                organizationId ?? Organization.Id.ToString(),
                projectId ?? Project.Project.Id.ToString(),
                teamId ?? Platform.Team.Id.ToString());

        public Task HandleAsync(DeleteTeamCommand command) => Handler.Handle(command, CancellationToken.None);

        public AreaTree Tree() =>
            new(Store.Areas.Select(area => new AreaNode(area.Id, area.ParentId, area.Name, area.OwnerTeamId, area.DefaultOfTeamId)));
    }

    // US-7 · "A team is deleted after a confirmation, and its areas pass to the first team": «Платформа» owns the areas
    // «Платформа» and «Платформа / API» explicitly and «Платформа / API / Шлюз» by inheritance. The areas of another team stay.
    [Fact]
    public async Task Handle_PassesExplicitAreasToTheFirstTeam_InheritedFollow_NamesAndPlacesKept()
    {
        var f = new Fixture();
        var api = f.Store.AddArea(f.Platform.Area, "API", f.Platform.Team);
        var gateway = f.Store.AddArea(api, "Шлюз");
        var data = f.Store.AddTeam(f.Project, "Данные");
        var reports = f.Store.AddArea(f.Project.Root, "Отчёты", data.Team);
        var before = f.Tree();
        var placesBefore = f.Store.Areas.ToDictionary(area => area.Id, area => (area.ParentId, area.Name, Path: before.PathOf(area.Id)));
        var firstTeamId = f.Project.FirstTeam.Id;

        await f.HandleAsync(f.Command());

        Assert.Equal(TeamStatus.Deleted, f.Platform.Team.Status);
        Assert.Equal(firstTeamId, f.Platform.Area.OwnerTeamId);
        Assert.Equal(firstTeamId, api.OwnerTeamId);
        Assert.Null(gateway.OwnerTeamId);

        var after = f.Tree();
        Assert.Equal(new ResolvedOwner(firstTeamId, false), after.ResolveOwner(api.Id));
        Assert.Equal(new ResolvedOwner(firstTeamId, true), after.ResolveOwner(gateway.Id));
        Assert.Equal("Платформа / API / Шлюз", after.PathOf(gateway.Id));
        Assert.Equal(
            placesBefore,
            f.Store.Areas.ToDictionary(area => area.Id, area => (area.ParentId, area.Name, Path: after.PathOf(area.Id))));

        // The areas of another team, and the other team's default, are not touched.
        Assert.Equal(data.Team.Id, reports.OwnerTeamId);
        Assert.Equal(data.Team.Id, data.Area.DefaultOfTeamId);
        Assert.Equal(data.Team.Id, data.Area.OwnerTeamId);
    }

    // US-7 · "A team is deleted ...": the area that was the deleted team's default loses the mark, and the first team's default
    // stays the root for good (BR-M1-14, BR-M1-18). Nothing is removed.
    [Fact]
    public async Task Handle_DefaultAreaLosesTheMark_FirstTeamsDefaultStaysTheRoot_NothingIsRemoved()
    {
        var f = new Fixture();
        var areasBefore = f.Store.Areas.Count;
        var firstTeamId = f.Project.FirstTeam.Id;

        await f.HandleAsync(f.Command());

        Assert.Null(f.Platform.Area.DefaultOfTeamId);
        Assert.Equal(firstTeamId, f.Platform.Area.OwnerTeamId);
        Assert.Equal(firstTeamId, f.Project.Root.OwnerTeamId);
        Assert.Equal(firstTeamId, f.Project.Root.DefaultOfTeamId);
        Assert.Single(f.Store.Areas, area => area.DefaultOfTeamId == firstTeamId);
        Assert.DoesNotContain(f.Store.Areas, area => area.OwnerTeamId == f.Platform.Team.Id || area.DefaultOfTeamId == f.Platform.Team.Id);
        Assert.Equal(areasBefore, f.Store.Areas.Count);
        Assert.Contains(f.Platform.Team, f.Store.Teams);
    }

    // US-7 · "A deleted team answers not found": the second deletion of the same team.
    [Fact]
    public async Task Handle_DeletedTwice_SecondIsTeamNotFound_ChangesNothing()
    {
        var f = new Fixture();
        await f.HandleAsync(f.Command());
        f.Store.Calls.Clear();

        var refusal = await Assert.ThrowsAsync<NotFoundException>(() => f.HandleAsync(f.Command()));

        Assert.Equal(OrganizationErrorCodes.TeamNotFound, refusal.Code);
        Assert.Equal(TeamStatus.Deleted, f.Platform.Team.Status);
        Assert.DoesNotContain(f.Store.Calls, call => call.StartsWith("reassign", StringComparison.Ordinal));
        Assert.Equal(0, f.Store.SaveCount);
    }

    // US-7 · "The first team cannot be deleted": refused with its own code, and nothing changes (the refusal comes before any write).
    [Fact]
    public async Task Handle_FirstTeam_IsFirstTeamCannotBeDeleted_ChangesNothing()
    {
        var f = new Fixture();
        var first = f.Project.FirstTeam;

        var refusal = await Assert.ThrowsAsync<ConflictException>(() => f.HandleAsync(f.Command(teamId: first.Id.ToString())));

        Assert.Equal(OrganizationErrorCodes.TeamFirstTeamCannotBeDeleted, refusal.Code);
        Assert.True(first.IsActive);
        Assert.Equal(first.Id, f.Project.Root.OwnerTeamId);
        Assert.Equal([$"lock team update {first.Id}"], f.Store.Calls);
    }

    // BR-M1-20: behind the filter the organization id is well formed; if it were not, the outermost level answers.
    [Fact]
    public async Task Handle_MalformedOrganization_IsOrganizationNotFound()
    {
        var f = new Fixture();

        var refusal = await Assert.ThrowsAsync<NotFoundException>(() => f.HandleAsync(f.Command(organizationId: "abc", projectId: "x", teamId: "y")));

        Assert.Equal(OrganizationErrorCodes.OrganizationNotFound, refusal.Code);
        Assert.Empty(f.Store.Calls);
    }

    // US-3 · "A project id names no project of the organization" and "a project is deactivated" (BR-M1-20, BR-M1-22): the
    // project level answers before the team is looked at, so the team's id is wrong too.
    [Theory]
    [InlineData("malformed")]
    [InlineData("unknown")]
    [InlineData("of another organization")]
    [InlineData("deactivated")]
    [InlineData("under a deactivated organization")]
    public async Task Handle_ProjectNotReachable_IsProjectNotFound_BeforeTheTeam(string how)
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

        var refusal = await Assert.ThrowsAsync<NotFoundException>(() => f.HandleAsync(f.Command(projectId: projectId, teamId: "not-a-team")));

        Assert.Equal(OrganizationErrorCodes.ProjectNotFound, refusal.Code);
        Assert.Empty(f.Store.Calls);
        Assert.True(f.Platform.Team.IsActive);
    }

    // US-4 · "A team id names no team of the project" (BR-M1-22) and US-7 · "A deleted team answers not found": malformed,
    // absent, unknown, a team of another project or organization (its first team included: the address answers before the
    // first-team rule, api-contract § 0.4 step 1 before 2), or one already deleted. Nothing is written.
    [Theory]
    [InlineData("malformed")]
    [InlineData("absent")]
    [InlineData("unknown")]
    [InlineData("first team of another project")]
    [InlineData("team of another organization")]
    [InlineData("deleted")]
    public async Task Handle_TeamNotAnActiveTeamOfTheProject_IsTeamNotFound_ChangesNothing(string how)
    {
        var f = new Fixture();
        var sibling = f.Store.AddProject(f.Organization, "Второй проект", "SEC");
        var foreign = f.Store.AddTeam(f.Store.AddProject(f.Store.AddOrganization("Другая организация"), "Чужой", "OTH"), "Чужая команда");
        var command = how switch
        {
            "malformed" => f.Command(teamId: f.Platform.Team.Id.ToString("N")),
            "absent" => f.Command() with { TeamId = null },
            "unknown" => f.Command(teamId: Guid.CreateVersion7().ToString()),
            "first team of another project" => f.Command(teamId: sibling.FirstTeam.Id.ToString()),
            "team of another organization" => f.Command(teamId: foreign.Team.Id.ToString()),
            _ => f.Command(),
        };

        if (how == "deleted")
        {
            f.Platform.Team.Delete();
        }

        var refusal = await Assert.ThrowsAsync<NotFoundException>(() => f.HandleAsync(command));

        Assert.Equal(OrganizationErrorCodes.TeamNotFound, refusal.Code);
        Assert.DoesNotContain(f.Store.Calls, call => call.StartsWith("reassign", StringComparison.Ordinal));
        Assert.Equal(0, f.Store.SaveCount);
        Assert.Equal(f.Platform.Team.Id, f.Platform.Area.OwnerTeamId);
        Assert.True(foreign.Team.IsActive);
        Assert.True(sibling.FirstTeam.IsActive);
    }

    // plan-backend § 8.2: the team FOR UPDATE, then the set-based re-own to the first team, then one save. No project lock, no
    // area lock of the handler's own (the statement takes the area rows it changes).
    [Fact]
    public async Task Handle_LocksTheTeamForUpdate_ThenReassignsSetBased_ThenSavesOnce()
    {
        var f = new Fixture();

        await f.HandleAsync(f.Command());

        Assert.Equal(
            [$"lock team update {f.Platform.Team.Id}", $"reassign {f.Platform.Team.Id} {f.Project.FirstTeam.Id}", "save"],
            f.Store.Calls);
    }
}
