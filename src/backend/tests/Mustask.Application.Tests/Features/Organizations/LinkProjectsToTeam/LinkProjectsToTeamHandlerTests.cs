using Mustask.Application.Features.Organizations.LinkProjectsToTeam;
using Mustask.Application.Tests.TestDoubles;
using Mustask.Application.Tests.TestDoubles.Organizations;
using Mustask.Domain.Identity;
using Mustask.Domain.Organizations;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Tests.Features.Organizations.LinkProjectsToTeam;

/// <summary>
/// <c>LinkProjectsToTeam</c> on fakes (feat-028 plan-backend § 7.2, D-7, D-14; US-3): the team's page links projects — each pair once,
/// all or nothing, the second code (project.link) first of all, the team locked <c>FOR SHARE</c>.
/// </summary>
public sealed class LinkProjectsToTeamHandlerTests
{
    private sealed class Fixture
    {
        public Fixture(OrganizationRole role = OrganizationRole.Admin)
        {
            Organization = Store.AddOrganization();
            Alpha = Store.AddProject(Organization, "Альфа", "ALP");
            Beta = Store.AddProject(Organization, "Бета", "BET");
            Mars = Store.AddTeam(Organization, "MARS");
            User.SubjectId = "admin-anna";
            Clock.UtcNow = new DateTimeOffset(2026, 10, 17, 3, 0, 0, TimeSpan.Zero);
            Handler = new LinkProjectsToTeamHandler(
                new FakeProjectRepository(Store),
                new FakeTeamRepository(Store),
                new FakeProjectTeamRepository(Store),
                new FakeOrganizationAccess(role),
                User,
                Clock,
                new FakeUnitOfWork());
        }

        public InMemoryOrganizations Store { get; } = new();

        public FakeCurrentUser User { get; } = new();

        public FakeClock Clock { get; } = new();

        public Organization Organization { get; }

        public NewProject Alpha { get; }

        public NewProject Beta { get; }

        public Team Mars { get; }

        public LinkProjectsToTeamHandler Handler { get; }

        public LinkProjectsToTeamCommand Command(IReadOnlyList<string?>? projectIds, string? teamId = null) =>
            new(Organization.Id.ToString(), teamId ?? Mars.Id.ToString(), projectIds);

        public Task HandleAsync(LinkProjectsToTeamCommand command) => Handler.Handle(command, CancellationToken.None);
    }

    // US-3 · "several ends in one confirmation": the team is linked to each project once, recording the caller and the time.
    [Fact]
    public async Task Link_Projects_RecordsTheCallerAndTheTime_CreatesNoTeamProjectOrItem()
    {
        var f = new Fixture();

        await f.HandleAsync(f.Command([f.Alpha.Project.Id.ToString(), f.Beta.Project.Id.ToString(), f.Alpha.Project.Id.ToString()]));

        Assert.Equal(2, f.Store.Links.Count);
        Assert.All(f.Store.Links, link => Assert.Equal((f.Mars.Id, "admin-anna", f.Clock.UtcNow), (link.TeamId, link.LinkedBy, link.LinkedAt)));
        Assert.Single(f.Store.Teams);
        Assert.Equal(2, f.Store.Projects.Count);
    }

    [Fact]
    public async Task Link_AlreadyLinked_ChangesNothing()
    {
        var f = new Fixture();
        f.Store.Link(f.Alpha, f.Mars);
        var first = f.Store.Links.Single();

        await f.HandleAsync(f.Command([f.Alpha.Project.Id.ToString()]));

        Assert.Same(first, f.Store.Links.Single());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Link_AbsentOrEmptyList_IsANoOp(bool empty)
    {
        var f = new Fixture();

        await f.HandleAsync(f.Command(empty ? [] : null));

        Assert.Empty(f.Store.Links);
    }

    // US-3 · "only active ends are linked": all or nothing — a deactivated, foreign, unknown or malformed project means none linked.
    [Theory]
    [InlineData("malformed")]
    [InlineData("unknown")]
    [InlineData("foreign")]
    [InlineData("deactivated")]
    public async Task Link_OneBadProject_IsProjectNotFound_NothingLinked(string how)
    {
        var f = new Fixture();
        var foreign = f.Store.AddProject(f.Store.AddOrganization("Другая"), "Чужой", "OTH");
        f.Beta.Project.Deactivate();
        var id = how switch
        {
            "malformed" => "not-an-id",
            "unknown" => Guid.CreateVersion7().ToString(),
            "foreign" => foreign.Project.Id.ToString(),
            _ => f.Beta.Project.Id.ToString(),
        };

        var refusal = await Assert.ThrowsAsync<NotFoundException>(() => f.HandleAsync(f.Command([f.Alpha.Project.Id.ToString(), id])));

        Assert.Equal(OrganizationErrorCodes.ProjectNotFound, refusal.Code);
        Assert.Empty(f.Store.Links);
    }

    // The team of the address first (404), before the projects named.
    [Theory]
    [InlineData("deleted")]
    [InlineData("malformed")]
    [InlineData("foreign")]
    public async Task Link_TeamNotActiveInTheOrganization_IsTeamNotFound_BeforeTheProjects(string how)
    {
        var f = new Fixture();
        var foreign = f.Store.AddTeam(f.Store.AddOrganization("Другая"), "Чужая");

        if (how == "deleted")
        {
            f.Mars.Delete();
        }

        var teamId = how switch
        {
            "malformed" => "abc",
            "foreign" => foreign.Id.ToString(),
            _ => null,
        };

        var refusal = await Assert.ThrowsAsync<NotFoundException>(() => f.HandleAsync(f.Command(["not-an-id"], teamId)));

        Assert.Equal(OrganizationErrorCodes.TeamNotFound, refusal.Code);
        Assert.Empty(f.Store.Links);
    }

    // D-7 · the second code (project.link) is asked first of all, before the team or any id is looked at.
    [Fact]
    public async Task Link_WithoutTheSecondCode_Is403_BeforeAnyIdIsLooked_At()
    {
        var f = new Fixture(OrganizationRole.Member);

        var refusal = await Assert.ThrowsAsync<ForbiddenException>(() => f.HandleAsync(f.Command(["not-an-id"], teamId: "abc")));

        Assert.Equal(OrganizationErrorCodes.AccessForbidden, refusal.Code);
        Assert.Empty(f.Store.Calls);
    }

    // plan-backend § 7.2 · the team FOR SHARE, then one INSERT for all the pairs.
    [Fact]
    public async Task Link_LocksTheTeamShare_ThenLinksOnce()
    {
        var f = new Fixture();

        await f.HandleAsync(f.Command([f.Alpha.Project.Id.ToString(), f.Beta.Project.Id.ToString()]));

        Assert.Equal([$"lock team share {f.Mars.Id}", "link 2"], f.Store.Calls);
    }
}
