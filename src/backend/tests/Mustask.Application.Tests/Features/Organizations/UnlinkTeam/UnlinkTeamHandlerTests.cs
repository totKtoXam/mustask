using Mustask.Application.Features.Organizations.UnlinkTeam;
using Mustask.Application.Tests.TestDoubles;
using Mustask.Application.Tests.TestDoubles.Organizations;
using Mustask.Domain.Identity;
using Mustask.Domain.Organizations;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Tests.Features.Organizations.UnlinkTeam;

/// <summary>
/// <c>UnlinkTeam</c> on fakes (feat-028 plan-backend § 7.2, D-3, D-5, D-7, D-8; US-4, BR-OL-8): the one operation, the order of the
/// answers (api-contract § 0.5) and the lock order (ADR-0028 rule 8). The races are the Api tests' (a real lock is the referee).
/// </summary>
public sealed class UnlinkTeamHandlerTests
{
    private sealed class Fixture
    {
        public Fixture(OrganizationRole role = OrganizationRole.Admin)
        {
            Organization = Store.AddOrganization();
            Alpha = Store.AddProject(Organization, "Альфа", "ALP");
            Beta = Store.AddProject(Organization, "Бета", "BET");
            Mars = Store.AddLinkedTeam(Organization, Alpha, "MARS");
            Jupiter = Store.AddLinkedTeam(Organization, Alpha, "JUPITER");
            Store.Link(Beta, Mars);
            Clock.UtcNow = new DateTimeOffset(2026, 10, 17, 3, 0, 0, TimeSpan.Zero);
            Handler = new UnlinkTeamHandler(
                new FakeOrganizationStructure(Store),
                new FakeProjectRepository(Store),
                new FakeProjectTeamRepository(Store),
                Ownership,
                new FakeTeamBacklogPlaces(Store),
                new FakeOrganizationAccess(role),
                Clock,
                new FakeUnitOfWork());
        }

        public InMemoryOrganizations Store { get; } = new();

        public FakeWorkItemTeamOwnership Ownership => _ownership ??= new FakeWorkItemTeamOwnership(Store);

        private FakeWorkItemTeamOwnership? _ownership;

        public FakeClock Clock { get; } = new();

        public Organization Organization { get; }

        public NewProject Alpha { get; }

        public NewProject Beta { get; }

        /// <summary>«MARS», linked to both projects.</summary>
        public Team Mars { get; }

        /// <summary>«JUPITER», linked to Alpha only.</summary>
        public Team Jupiter { get; }

        public UnlinkTeamHandler Handler { get; }

        public UnlinkTeamCommand Command(Guid? destination, string? teamId = null, string? projectId = null, string? organizationId = null) =>
            new(
                organizationId ?? Organization.Id.ToString(),
                projectId ?? Alpha.Project.Id.ToString(),
                teamId ?? Mars.Id.ToString(),
                new UnlinkDestination(destination?.ToString()));

        public Task HandleAsync(UnlinkTeamCommand command) => Handler.Handle(command, CancellationToken.None);
    }

    // US-4 · "confirming with another team": the items of THIS project pass to the destination, with the operation's time; the
    // link goes; another project's items and link stay.
    [Fact]
    public async Task Unlink_ToATeam_ReassignsOnce_RemovesTheLink_TouchesNoOtherProject()
    {
        var f = new Fixture();
        f.Ownership.Owned[(f.Alpha.Project.Id, f.Mars.Id)] = 8;
        f.Ownership.Owned[(f.Beta.Project.Id, f.Mars.Id)] = 5;

        await f.HandleAsync(f.Command(f.Jupiter.Id));

        Assert.False(f.Store.IsLinked(f.Alpha.Project.Id, f.Mars.Id));
        Assert.True(f.Store.IsLinked(f.Beta.Project.Id, f.Mars.Id));
        Assert.Equal(8, f.Ownership.Owned[(f.Alpha.Project.Id, f.Jupiter.Id)]);
        Assert.Equal(5, f.Ownership.Owned[(f.Beta.Project.Id, f.Mars.Id)]);
        Assert.Equal(f.Clock.UtcNow, f.Ownership.ReassignedAt);
        Assert.Equal(f.Jupiter.Id, f.Ownership.LastDestination);
    }

    // US-4 · "confirming with «Без команды»": the items are left with no owning team.
    [Fact]
    public async Task Unlink_ToNone_LeavesTheItemsWithoutATeam()
    {
        var f = new Fixture();
        f.Ownership.Owned[(f.Alpha.Project.Id, f.Mars.Id)] = 3;

        await f.HandleAsync(f.Command(destination: null));

        Assert.Equal(3, f.Ownership.Unowned);
        Assert.Null(f.Ownership.LastDestination);
        Assert.False(f.Store.IsLinked(f.Alpha.Project.Id, f.Mars.Id));
    }

    // US-4 · "the only team": the project is left teamless and works.
    [Fact]
    public async Task Unlink_TheOnlyTeam_ToNone_LeavesTheProjectTeamless()
    {
        var f = new Fixture();
        f.Store.Links.RemoveAll(link => link.ProjectId == f.Alpha.Project.Id && link.TeamId == f.Jupiter.Id);

        await f.HandleAsync(f.Command(destination: null));

        Assert.DoesNotContain(f.Store.Links, link => link.ProjectId == f.Alpha.Project.Id);
        Assert.True(f.Alpha.Project.IsActive);
    }

    // US-4 · the setting that named the team is cleared; one that names another team stays.
    [Fact]
    public async Task Unlink_ClearsTheNamedDefault_KeepsAnotherTeamsSetting()
    {
        var named = new Fixture();
        named.Alpha.Project.NameDefaultTeam(named.Mars.Id);
        var other = new Fixture();
        other.Alpha.Project.NameDefaultTeam(other.Jupiter.Id);

        await named.HandleAsync(named.Command(named.Jupiter.Id));
        await other.HandleAsync(other.Command(other.Jupiter.Id));

        Assert.Null(named.Alpha.Project.DefaultTeamId);
        Assert.Equal(other.Jupiter.Id, other.Alpha.Project.DefaultTeamId);
    }

    // US-3 · "linking a linked pair, unlinking an unlinked one": the pair is not linked, so 204 and nothing moves, whatever the
    // destination names.
    [Fact]
    public async Task Unlink_NotLinked_Succeeds_NamingAnyDestination_ChangesNothing()
    {
        var f = new Fixture();
        f.Ownership.Owned[(f.Alpha.Project.Id, f.Mars.Id)] = 4;
        var unlinked = f.Store.AddTeam(f.Organization, "Не связана");
        f.Store.Calls.Clear();

        await f.HandleAsync(f.Command(f.Jupiter.Id, teamId: unlinked.Id.ToString()));
        await f.HandleAsync(f.Command(unlinked.Id, teamId: unlinked.Id.ToString()));

        Assert.Equal(4, f.Ownership.Owned[(f.Alpha.Project.Id, f.Mars.Id)]);
        Assert.DoesNotContain(f.Store.Calls, call => call.StartsWith("reassign", StringComparison.Ordinal));
        Assert.True(f.Store.IsLinked(f.Alpha.Project.Id, f.Jupiter.Id));
    }

    // US-4 · "a bad destination": itself and an unlinked team are team.not_in_project; a deleted, an unknown and a foreign one are
    // team.not_found; an absent destination is 400. Nothing changes.
    [Theory]
    [InlineData("itself", "organizations.team.not_in_project")]
    [InlineData("not linked", "organizations.team.not_in_project")]
    [InlineData("deleted", "organizations.team.not_found")]
    [InlineData("unknown", "organizations.team.not_found")]
    [InlineData("foreign", "organizations.team.not_found")]
    [InlineData("malformed", "organizations.team.not_found")]
    public async Task Unlink_DestinationRefusals_ChangeNothing(string how, string code)
    {
        var f = new Fixture();
        f.Ownership.Owned[(f.Alpha.Project.Id, f.Mars.Id)] = 6;
        var unlinked = f.Store.AddTeam(f.Organization, "Не связана");
        var gone = f.Store.AddLinkedTeam(f.Organization, f.Alpha, "Ушла");
        gone.Delete();
        var foreign = f.Store.AddTeam(f.Store.AddOrganization("Другая"), "Чужая");
        var command = how switch
        {
            "itself" => f.Command(f.Mars.Id),
            "not linked" => f.Command(unlinked.Id),
            "deleted" => f.Command(gone.Id),
            "unknown" => f.Command(Guid.CreateVersion7()),
            "foreign" => f.Command(foreign.Id),
            _ => new UnlinkTeamCommand(f.Organization.Id.ToString(), f.Alpha.Project.Id.ToString(), f.Mars.Id.ToString(), new UnlinkDestination("not-an-id")),
        };

        var refusal = await Assert.ThrowsAnyAsync<MustaskException>(() => f.HandleAsync(command));

        Assert.Equal(code, refusal.Code);
        Assert.True(f.Store.IsLinked(f.Alpha.Project.Id, f.Mars.Id));
        Assert.Equal(6, f.Ownership.Owned[(f.Alpha.Project.Id, f.Mars.Id)]);
    }

    // D-8 · the destination wrapper's teamId key must be present: absent is 400 destination_required, never «Без команды».
    [Fact]
    public async Task Unlink_NoDestination_IsValidationFailed_DestinationRequired_ChangesNothing()
    {
        var f = new Fixture();
        f.Ownership.Owned[(f.Alpha.Project.Id, f.Mars.Id)] = 6;

        var refusal = await Assert.ThrowsAsync<ValidationException>(() => f.HandleAsync(
            new UnlinkTeamCommand(f.Organization.Id.ToString(), f.Alpha.Project.Id.ToString(), f.Mars.Id.ToString(), Destination: null)));

        Assert.Equal(OrganizationErrorCodes.TeamDestinationRequired, Assert.Single(refusal.Errors["destination"]).Code);
        Assert.Equal(6, f.Ownership.Owned[(f.Alpha.Project.Id, f.Mars.Id)]);
        Assert.True(f.Store.IsLinked(f.Alpha.Project.Id, f.Mars.Id));
    }

    // api-contract § 0.5 · the address from the outside in: organization, project (deactivated too), then the team of the address.
    [Theory]
    [InlineData("malformed organization", "organizations.organization.not_found")]
    [InlineData("deactivated project", "organizations.project.not_found")]
    [InlineData("foreign project", "organizations.project.not_found")]
    [InlineData("deleted team", "organizations.team.not_found")]
    [InlineData("unknown team", "organizations.team.not_found")]
    public async Task Unlink_Address_IsAnsweredFromTheOutsideIn(string how, string code)
    {
        var f = new Fixture();
        var foreign = f.Store.AddProject(f.Store.AddOrganization("Другая"), "Чужой", "OTH");
        var command = how switch
        {
            "malformed organization" => f.Command(null, organizationId: "abc"),
            "foreign project" => f.Command(null, projectId: foreign.Project.Id.ToString()),
            "unknown team" => f.Command(null, teamId: Guid.CreateVersion7().ToString()),
            _ => f.Command(null),
        };

        if (how == "deactivated project")
        {
            f.Alpha.Project.Deactivate();
        }

        if (how == "deleted team")
        {
            f.Mars.Delete();
        }

        var refusal = await Assert.ThrowsAsync<NotFoundException>(() => f.HandleAsync(command));

        Assert.Equal(code, refusal.Code);
        Assert.Empty(f.Store.Calls);
    }

    // D-7 · the second code, organizations.team.link, is asked first of all: a role without it learns nothing.
    [Fact]
    public async Task Unlink_WithoutTheSecondCode_Is403_BeforeAnyIdIsLooked_At()
    {
        var f = new Fixture(OrganizationRole.Member);

        var refusal = await Assert.ThrowsAsync<ForbiddenException>(() => f.HandleAsync(f.Command(null, projectId: "abc", teamId: "abc")));

        Assert.Equal(OrganizationErrorCodes.AccessForbidden, refusal.Code);
        Assert.Empty(f.Store.Calls);
    }

    // plan-backend § 7.2 · the links of the pair in ONE statement ordered by team id, no team row, then the owned items, the places of
    // this project, the reassignment, the setting, the link — in this order.
    [Fact]
    public async Task Unlink_LocksThePairOfLinks_ThenTheOwnedItems_ThenDropsPlaces_ThenReassigns_ThenRemovesTheLink()
    {
        var f = new Fixture();

        await f.HandleAsync(f.Command(f.Jupiter.Id));

        var pair = string.Join(",", new[] { f.Mars.Id, f.Jupiter.Id }.Order());
        Assert.Equal(
            [
                $"lock links {f.Alpha.Project.Id} {pair}",
                $"lock owned {f.Mars.Id}",
                $"drop team places in {f.Alpha.Project.Id} {f.Mars.Id}",
                $"reassign owned {f.Mars.Id} to {f.Jupiter.Id}",
                $"clear default team {f.Mars.Id} in {f.Alpha.Project.Id}",
                $"remove link {f.Alpha.Project.Id} {f.Mars.Id}",
            ],
            f.Store.Calls);
    }
}
