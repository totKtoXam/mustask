using Mustask.Application.Features.Organizations.DeleteTeam;
using Mustask.Application.Tests.TestDoubles;
using Mustask.Application.Tests.TestDoubles.Organizations;
using Mustask.Domain.Organizations;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Tests.Features.Organizations.DeleteTeam;

/// <summary>
/// <c>DeleteTeam</c> on fakes (mod-017 plan-backend § 7.2, § 9.1): the operation of BR-OT-12 — the owned items go to «Без
/// команды», the places go, the setting is cleared, the memberships end — the last-team guard of BR-OT-11, the order of answers
/// (api-contract § 0.4) and the lock order (ADR-0028 rule 8).
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
            Ownership = new FakeWorkItemTeamOwnership(Store);
            Clock = new FakeClock();
            Handler = new DeleteTeamHandler(
                new FakeProjectRepository(Store),
                new FakeTeamRepository(Store),
                new FakeTeamMembershipRepository(Store),
                Ownership,
                new FakeTeamBacklogPlaces(Store),
                Clock,
                new FakeUnitOfWork());
        }

        public InMemoryOrganizations Store { get; } = new();

        public Organization Organization { get; }

        /// <summary>«Пример проекта» (PRM): the team «Пример проекта» it came with and the root.</summary>
        public NewProject Project { get; }

        /// <summary>The second team «Платформа».</summary>
        public Team Platform { get; }

        public FakeWorkItemTeamOwnership Ownership { get; }

        public FakeClock Clock { get; }

        public DeleteTeamHandler Handler { get; }

        public DeleteTeamCommand Command(string? teamId = null, string? projectId = null, string? organizationId = null) =>
            new(
                organizationId ?? Organization.Id.ToString(),
                projectId ?? Project.Project.Id.ToString(),
                teamId ?? Platform.Id.ToString());

        public Task HandleAsync(DeleteTeamCommand command) => Handler.Handle(command, CancellationToken.None);
    }

    // US-7 · "confirming sends the items to none": the team is deleted, the items it owns are released with the operation's time,
    // and the areas — which a team does not own any more — are not touched.
    [Fact]
    public async Task Handle_DeletesTheTeam_ReleasesWhatItOwns_TouchesNoArea()
    {
        var f = new Fixture();
        f.Ownership.Owned[f.Platform.Id] = 5;
        var areasBefore = f.Store.Areas.Select(area => (area.Id, area.ParentId, area.Name)).ToList();
        f.Clock.UtcNow = new DateTimeOffset(2026, 10, 17, 3, 0, 0, TimeSpan.Zero);

        await f.HandleAsync(f.Command());

        Assert.Equal(TeamStatus.Deleted, f.Platform.Status);
        Assert.Equal(0, f.Ownership.Owned.GetValueOrDefault(f.Platform.Id));
        Assert.Equal(f.Clock.UtcNow, f.Ownership.ReleasedAt);
        Assert.Equal(areasBefore, f.Store.Areas.Select(area => (area.Id, area.ParentId, area.Name)).ToList());
        Assert.Contains(f.Platform, f.Store.Teams);
    }

    // US-7 · "any team renamed or deleted": the team the project was created with is deleted like any other while another
    // remains; the last one cannot be.
    [Fact]
    public async Task Handle_TheTeamTheProjectCameWith_IsDeletedWhenAnotherRemains()
    {
        var f = new Fixture();

        await f.HandleAsync(f.Command(teamId: f.Project.Team.Id.ToString()));

        Assert.Equal(TeamStatus.Deleted, f.Project.Team.Status);
        Assert.True(f.Platform.IsActive);
    }

    // US-7 · "the last team cannot be deleted": 409, and nothing changes — nothing is locked beyond the project's teams, written
    // or saved.
    [Fact]
    public async Task Handle_LastTeam_Returns409_ChangesNothing()
    {
        var f = new Fixture();
        await f.HandleAsync(f.Command());
        f.Store.Calls.Clear();

        var refusal = await Assert.ThrowsAsync<ConflictException>(() => f.HandleAsync(f.Command(teamId: f.Project.Team.Id.ToString())));

        Assert.Equal(OrganizationErrorCodes.TeamLastTeamCannotBeDeleted, refusal.Code);
        Assert.True(f.Project.Team.IsActive);
        Assert.Equal([$"lock project teams {f.Project.Project.Id}"], f.Store.Calls);
    }

    // US-3 · "losing the named team" (BR-OT-4): a setting that named the deleted team is cleared in the same operation; a
    // setting that names another team stays.
    [Fact]
    public async Task Handle_ClearsTheDefaultSettingThatNamedTheTeam_KeepsOneThatNamesAnother()
    {
        var named = new Fixture();
        named.Project.Project.NameDefaultTeam(named.Platform.Id);
        var other = new Fixture();
        other.Project.Project.NameDefaultTeam(other.Project.Team.Id);

        await named.HandleAsync(named.Command());
        await other.HandleAsync(other.Command());

        Assert.Null(named.Project.Project.DefaultTeamId);
        Assert.Equal(other.Project.Team.Id, other.Project.Project.DefaultTeamId);
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
        Assert.Equal(TeamStatus.Deleted, f.Platform.Status);
        Assert.DoesNotContain(f.Store.Calls, call => call.StartsWith("release owned", StringComparison.Ordinal));
        Assert.Equal(0, f.Store.SaveCount);
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
        Assert.True(f.Platform.IsActive);
    }

    // US-4 · "A team id names no team of the project" (BR-M1-22) and US-7 · "A deleted team answers not found": malformed,
    // absent, unknown, a team of another project or organization, or one already deleted. Nothing is written. (The address
    // answers before the last-team guard, api-contract § 0.4.)
    [Theory]
    [InlineData("malformed")]
    [InlineData("absent")]
    [InlineData("unknown")]
    [InlineData("team of another project")]
    [InlineData("team of another organization")]
    [InlineData("deleted")]
    public async Task Handle_TeamNotAnActiveTeamOfTheProject_IsTeamNotFound_ChangesNothing(string how)
    {
        var f = new Fixture();
        var sibling = f.Store.AddProject(f.Organization, "Второй проект", "SEC");
        var foreign = f.Store.AddTeam(f.Store.AddProject(f.Store.AddOrganization("Другая организация"), "Чужой", "OTH"), "Чужая команда");
        var command = how switch
        {
            "malformed" => f.Command(teamId: f.Platform.Id.ToString("N")),
            "absent" => f.Command() with { TeamId = null },
            "unknown" => f.Command(teamId: Guid.CreateVersion7().ToString()),
            "team of another project" => f.Command(teamId: sibling.Team.Id.ToString()),
            "team of another organization" => f.Command(teamId: foreign.Id.ToString()),
            _ => f.Command(),
        };

        if (how == "deleted")
        {
            f.Platform.Delete(2);
        }

        var refusal = await Assert.ThrowsAsync<NotFoundException>(() => f.HandleAsync(command));

        Assert.Equal(OrganizationErrorCodes.TeamNotFound, refusal.Code);
        Assert.DoesNotContain(f.Store.Calls, call => call.StartsWith("release owned", StringComparison.Ordinal));
        Assert.Equal(0, f.Store.SaveCount);
        Assert.True(foreign.IsActive);
        Assert.True(sibling.Team.IsActive);
    }

    // plan-backend § 7.2: all the project's active teams FOR NO KEY UPDATE in id order, then the owned items FOR UPDATE, the
    // team's places dropped, the items released, the setting cleared, the team's memberships ended under the same lock, then
    // one save. No project lock before a team's.
    [Fact]
    public async Task Handle_LocksAllTheProjectsTeams_ThenReleasesTheOwnedItems_ThenEndsTheMemberships_ThenSavesOnce()
    {
        var f = new Fixture();

        await f.HandleAsync(f.Command());

        Assert.Equal(
            [
                $"lock project teams {f.Project.Project.Id}",
                $"lock owned {f.Platform.Id}",
                $"drop team places {f.Platform.Id}",
                $"release owned {f.Platform.Id}",
                $"clear default team {f.Platform.Id}",
                $"end team memberships {f.Platform.Id}",
                "save",
            ],
            f.Store.Calls);
    }

    // mod-016 US-3 · "A deleted team's memberships end" (BR-M1-18, BR-M1-36): neither member is in «Платформа» any longer, the
    // one who is also in «Аналитика» still is, and the organization memberships are untouched.
    [Fact]
    public async Task Handle_EndsTheTeamsMemberships_AMemberOfAnotherTeamStaysThere()
    {
        var f = new Fixture();
        var analytics = f.Store.AddTeam(f.Project, "Аналитика");
        f.Store.AddMember(f.Organization, "anna");
        f.Store.AddMember(f.Organization, "oleg");
        f.Store.AddTeamMember(f.Organization, f.Platform, "anna");
        f.Store.AddTeamMember(f.Organization, f.Platform, "oleg");
        f.Store.AddTeamMember(f.Organization, analytics, "oleg");

        await f.HandleAsync(f.Command());

        var remaining = Assert.Single(f.Store.TeamMemberships);
        Assert.Equal((analytics.Id, "oleg"), (remaining.TeamId, remaining.UserId));
        Assert.Equal(2, f.Store.Memberships.Count);
    }

    // mod-016 · a refused deletion ends nothing: the last team keeps its members.
    [Fact]
    public async Task Handle_LastTeamRefused_EndsNoMembership()
    {
        var f = new Fixture();
        await f.HandleAsync(f.Command());
        f.Store.AddMember(f.Organization, "anna");
        f.Store.AddTeamMember(f.Organization, f.Project.Team, "anna");

        await Assert.ThrowsAsync<ConflictException>(() => f.HandleAsync(f.Command(teamId: f.Project.Team.Id.ToString())));

        Assert.Single(f.Store.TeamMemberships);
    }
}
