using Mustask.Application.Features.Organizations.DeleteTeam;
using Mustask.Application.Tests.TestDoubles;
using Mustask.Application.Tests.TestDoubles.Organizations;
using Mustask.Domain.Organizations;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Tests.Features.Organizations.DeleteTeam;

/// <summary>
/// <c>DeleteTeam</c> on fakes (feat-028 plan-backend § 7.2, § 9.1; US-5): the operation of BR-OL-9 — the owned items of every linked
/// project go to «Без команды», the places go, the settings are cleared, the links are removed, the memberships end — with no
/// last-team guard (BR-OL-10), the order of answers (api-contract § 0.5) and the lock order (ADR-0028 rule 8). Rewritten from the
/// project-nested form (mod-017 BR-OT-11, BR-OT-12; D-16); the last-team tests are retired with BR-OL-10.
/// </summary>
public sealed class DeleteTeamHandlerTests
{
    private sealed class Fixture
    {
        public Fixture()
        {
            Organization = Store.AddOrganization();
            Alpha = Store.AddProject(Organization, "Альфа", "ALP");
            Beta = Store.AddProject(Organization, "Бета", "BET");
            Mars = Store.AddTeam(Organization, "MARS");
            Store.Link(Alpha, Mars);
            Store.Link(Beta, Mars);
            Ownership = new FakeWorkItemTeamOwnership(Store);
            Clock = new FakeClock();
            Handler = new DeleteTeamHandler(
                new FakeProjectRepository(Store),
                new FakeTeamRepository(Store),
                new FakeProjectTeamRepository(Store),
                new FakeTeamMembershipRepository(Store),
                Ownership,
                new FakeTeamBacklogPlaces(Store),
                Clock,
                new FakeUnitOfWork());
        }

        public InMemoryOrganizations Store { get; } = new();

        public Organization Organization { get; }

        public NewProject Alpha { get; }

        public NewProject Beta { get; }

        /// <summary>«MARS», linked to both projects.</summary>
        public Team Mars { get; }

        public FakeWorkItemTeamOwnership Ownership { get; }

        public FakeClock Clock { get; }

        public DeleteTeamHandler Handler { get; }

        public DeleteTeamCommand Command(string? teamId = null, string? organizationId = null) =>
            new(organizationId ?? Organization.Id.ToString(), teamId ?? Mars.Id.ToString());

        public Task HandleAsync(DeleteTeamCommand command) => Handler.Handle(command, CancellationToken.None);
    }

    // US-5 · "confirming deletes in one operation": the team is deleted, the items it owns in EVERY linked project pass to nobody
    // with the operation's time, the links go, the area tree is untouched.
    [Fact]
    public async Task Handle_DeletesTheTeam_ReassignsWhatItOwnsInEveryProject_RemovesTheLinks()
    {
        var f = new Fixture();
        f.Ownership.Owned[(f.Alpha.Project.Id, f.Mars.Id)] = 5;
        f.Ownership.Owned[(f.Beta.Project.Id, f.Mars.Id)] = 3;
        var areasBefore = f.Store.Areas.Select(area => (area.Id, area.ParentId, area.Name)).ToList();
        f.Clock.UtcNow = new DateTimeOffset(2026, 10, 17, 3, 0, 0, TimeSpan.Zero);

        await f.HandleAsync(f.Command());

        Assert.Equal(TeamStatus.Deleted, f.Mars.Status);
        Assert.Empty(f.Ownership.Owned);
        Assert.Equal(8, f.Ownership.Unowned);
        Assert.Equal(f.Clock.UtcNow, f.Ownership.ReassignedAt);
        Assert.Null(f.Ownership.LastDestination);
        Assert.Empty(f.Store.Links);
        Assert.Equal(areasBefore, f.Store.Areas.Select(area => (area.Id, area.ParentId, area.Name)).ToList());
        Assert.Contains(f.Mars, f.Store.Teams);
    }

    // US-5 · "a project's last team can be deleted" (BR-OL-10): no guard — the only team of a project goes, and the project stays
    // (retires TeamTests.Delete_LastTeam_IsRefused and Handle_LastTeam_Returns409_ChangesNothing).
    [Fact]
    public async Task Handle_TheProjectsLastTeam_IsDeleted_NoGuardCode()
    {
        var f = new Fixture();
        f.Store.Links.RemoveAll(link => link.ProjectId == f.Beta.Project.Id);

        await f.HandleAsync(f.Command());

        Assert.Equal(TeamStatus.Deleted, f.Mars.Status);
        Assert.True(f.Alpha.Project.IsActive);
    }

    // US-3 · "losing the named team": a setting that named the deleted team is cleared in every project; one that names another
    // team stays.
    [Fact]
    public async Task Handle_ClearsTheDefaultSettingsThatNamedTheTeam_KeepsOneThatNamesAnother()
    {
        var f = new Fixture();
        var other = f.Store.AddLinkedTeam(f.Organization, f.Beta, "VENUS");
        f.Alpha.Project.NameDefaultTeam(f.Mars.Id);
        f.Beta.Project.NameDefaultTeam(other.Id);

        await f.HandleAsync(f.Command());

        Assert.Null(f.Alpha.Project.DefaultTeamId);
        Assert.Equal(other.Id, f.Beta.Project.DefaultTeamId);
    }

    // US-5 · "a team deleted again answers not found": the second deletion of the same team changes nothing.
    [Fact]
    public async Task Handle_DeletedTwice_SecondIsTeamNotFound_ChangesNothing()
    {
        var f = new Fixture();
        await f.HandleAsync(f.Command());
        f.Store.Calls.Clear();

        var refusal = await Assert.ThrowsAsync<NotFoundException>(() => f.HandleAsync(f.Command()));

        Assert.Equal(OrganizationErrorCodes.TeamNotFound, refusal.Code);
        Assert.Equal(["lock team no-key-update " + f.Mars.Id], f.Store.Calls);
    }

    // US-8 · a malformed organization, and an unknown, malformed or foreign team, answer by their level.
    [Fact]
    public async Task Handle_MalformedOrganization_IsOrganizationNotFound()
    {
        var f = new Fixture();

        var refusal = await Assert.ThrowsAsync<NotFoundException>(() => f.HandleAsync(f.Command(organizationId: "not-an-id")));

        Assert.Equal(OrganizationErrorCodes.OrganizationNotFound, refusal.Code);
    }

    [Theory]
    [InlineData("malformed")]
    [InlineData("unknown")]
    [InlineData("foreign")]
    public async Task Handle_TeamNotAnActiveTeamOfTheOrganization_IsTeamNotFound_ChangesNothing(string how)
    {
        var f = new Fixture();
        var other = f.Store.AddOrganization("Другая");
        var foreign = f.Store.AddTeam(other, "Чужая");
        var id = how switch
        {
            "malformed" => "not-an-id",
            "unknown" => Guid.CreateVersion7().ToString(),
            _ => foreign.Id.ToString(),
        };

        var refusal = await Assert.ThrowsAsync<NotFoundException>(() => f.HandleAsync(f.Command(teamId: id)));

        Assert.Equal(OrganizationErrorCodes.TeamNotFound, refusal.Code);
        Assert.True(foreign.IsActive);
        Assert.Equal(2, f.Store.Links.Count);
    }

    // plan-backend § 7.2 · the order of ADR-0028 rule 8: the team row, then ITS links, then the settings (the project rows — before any
    // item, mod-005 plan-backend § 14: a project backlog's reorder holds the project row and then its members), then the owned items,
    // the places, the reassignment, the links, the memberships; one flush.
    [Fact]
    public async Task Handle_LocksTheTeam_ThenItsLinks_ThenClearsTheSettings_ThenTheOwnedItems_ThenReassigns_ThenEndsTheMemberships_ThenSavesOnce()
    {
        var f = new Fixture();

        await f.HandleAsync(f.Command());

        var team = f.Mars.Id;
        Assert.Equal(
            [
                $"lock team no-key-update {team}",
                $"lock team links {team}",
                $"clear default team {team}",
                $"lock owned {team}",
                $"drop team places {team}",
                $"reassign owned {team} to none",
                $"remove team links {team}",
                $"end team memberships {team}",
                "save",
            ],
            f.Store.Calls);
    }

    // mod-016 BR-M1-36 · the team's memberships end; a person who is also in another team stays there.
    [Fact]
    public async Task Handle_EndsTheTeamsMemberships_AMemberOfAnotherTeamStaysThere()
    {
        var f = new Fixture();
        var other = f.Store.AddLinkedTeam(f.Organization, f.Alpha, "VENUS");
        f.Store.AddMember(f.Organization, "u-1");
        f.Store.AddTeamMember(f.Organization, f.Mars, "u-1");
        f.Store.AddTeamMember(f.Organization, other, "u-1");

        await f.HandleAsync(f.Command());

        var remaining = Assert.Single(f.Store.TeamMemberships);
        Assert.Equal(other.Id, remaining.TeamId);
    }
}
