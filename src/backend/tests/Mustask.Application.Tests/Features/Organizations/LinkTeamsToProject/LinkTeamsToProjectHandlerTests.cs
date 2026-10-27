using Mustask.Application.Features.Organizations.LinkTeamsToProject;
using Mustask.Application.Tests.TestDoubles;
using Mustask.Application.Tests.TestDoubles.Organizations;
using Mustask.Domain.Identity;
using Mustask.Domain.Organizations;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Tests.Features.Organizations.LinkTeamsToProject;

/// <summary>
/// <c>LinkTeamsToProject</c> on fakes (feat-028 plan-backend § 7.2, D-7, D-14; US-3): the project's page links teams — each pair once,
/// all or nothing, the second code first of all, the teams locked <c>FOR SHARE</c> in one statement.
/// </summary>
public sealed class LinkTeamsToProjectHandlerTests
{
    private sealed class Fixture
    {
        public Fixture(OrganizationRole role = OrganizationRole.Admin)
        {
            Organization = Store.AddOrganization();
            Project = Store.AddProject(Organization);
            Mars = Store.AddTeam(Organization, "MARS");
            Venus = Store.AddTeam(Organization, "VENUS");
            User.SubjectId = "admin-anna";
            Clock.UtcNow = new DateTimeOffset(2026, 10, 17, 3, 0, 0, TimeSpan.Zero);
            Handler = new LinkTeamsToProjectHandler(
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

        public Project Project { get; }

        public Team Mars { get; }

        public Team Venus { get; }

        public LinkTeamsToProjectHandler Handler { get; }

        public LinkTeamsToProjectCommand Command(IReadOnlyList<string?>? teamIds, string? projectId = null, string? organizationId = null) =>
            new(organizationId ?? Organization.Id.ToString(), projectId ?? Project.Id.ToString(), teamIds);

        public Task HandleAsync(LinkTeamsToProjectCommand command) => Handler.Handle(command, CancellationToken.None);
    }

    // US-3 · "a link from either page" and "several ends in one confirmation": each team is linked once, recording the caller and
    // the time; a duplicate in the list collapses.
    [Fact]
    public async Task Link_Teams_RecordsTheCallerAndTheTime_DuplicatesCollapse()
    {
        var f = new Fixture();

        await f.HandleAsync(f.Command([f.Mars.Id.ToString(), f.Venus.Id.ToString(), f.Mars.Id.ToString()]));

        Assert.Equal(2, f.Store.Links.Count);
        Assert.All(f.Store.Links, link => Assert.Equal((f.Project.Id, "admin-anna", f.Clock.UtcNow), (link.ProjectId, link.LinkedBy, link.LinkedAt)));
    }

    // US-3 · "linking a linked pair": nothing changes, the link keeps who made it and when.
    [Fact]
    public async Task Link_AlreadyLinked_ChangesNothing_KeepsWhoAndWhen()
    {
        var f = new Fixture();
        f.Store.Link(f.Project, f.Mars);
        var first = f.Store.Links.Single();
        f.Clock.UtcNow = f.Clock.UtcNow.AddDays(1);
        f.User.SubjectId = "someone-else";

        await f.HandleAsync(f.Command([f.Mars.Id.ToString()]));

        Assert.Same(first, f.Store.Links.Single());
        Assert.Equal(InMemoryOrganizations.Creator, first.LinkedBy);
    }

    // D-14 · an absent, null or empty list is a no-op 204 — also the old { "name" } body.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Link_AbsentOrEmptyList_IsANoOp(bool empty)
    {
        var f = new Fixture();

        await f.HandleAsync(f.Command(empty ? [] : null));

        Assert.Empty(f.Store.Links);
        Assert.Empty(f.Store.Calls);
    }

    // US-3 · "only active ends are linked": all or nothing — one entry that is not an active team of the organization, and none
    // is linked (malformed, unknown, foreign and deleted alike).
    [Theory]
    [InlineData("malformed")]
    [InlineData("unknown")]
    [InlineData("foreign")]
    [InlineData("deleted")]
    public async Task Link_OneBadTeam_IsTeamNotFound_NothingLinked(string how)
    {
        var f = new Fixture();
        var foreign = f.Store.AddTeam(f.Store.AddOrganization("Другая"), "Чужая");
        f.Venus.Delete();
        var id = how switch
        {
            "malformed" => "not-an-id",
            "unknown" => Guid.CreateVersion7().ToString(),
            "foreign" => foreign.Id.ToString(),
            _ => f.Venus.Id.ToString(),
        };

        var refusal = await Assert.ThrowsAsync<NotFoundException>(() => f.HandleAsync(f.Command([f.Mars.Id.ToString(), id])));

        Assert.Equal(OrganizationErrorCodes.TeamNotFound, refusal.Code);
        Assert.Empty(f.Store.Links);
    }

    // The project of the address first: deactivated, foreign or malformed is project.not_found, before the teams.
    [Theory]
    [InlineData("deactivated")]
    [InlineData("malformed")]
    public async Task Link_ProjectNotActive_IsProjectNotFound_BeforeTheTeams(string how)
    {
        var f = new Fixture();

        if (how == "deactivated")
        {
            f.Project.Deactivate();
        }

        var refusal = await Assert.ThrowsAsync<NotFoundException>(
            () => f.HandleAsync(f.Command(["not-an-id"], projectId: how == "malformed" ? "abc" : null)));

        Assert.Equal(OrganizationErrorCodes.ProjectNotFound, refusal.Code);
        Assert.Empty(f.Store.Links);
    }

    // D-7 · the second code (team.link) is asked first of all, before the project or any id is looked at.
    [Fact]
    public async Task Link_WithoutTheSecondCode_Is403_BeforeAnyIdIsLooked_At()
    {
        var f = new Fixture(OrganizationRole.Member);

        var refusal = await Assert.ThrowsAsync<ForbiddenException>(() => f.HandleAsync(f.Command(["not-an-id"], projectId: "abc")));

        Assert.Equal(OrganizationErrorCodes.AccessForbidden, refusal.Code);
        Assert.Empty(f.Store.Calls);
    }

    // plan-backend § 7.2 · the teams are locked FOR SHARE in one statement ordered by id, then one INSERT for all the pairs.
    [Fact]
    public async Task Link_LocksTheTeamsInOneStatementByIdOrder_ThenLinksOnce()
    {
        var f = new Fixture();

        await f.HandleAsync(f.Command([f.Venus.Id.ToString(), f.Mars.Id.ToString()]));

        Assert.Equal(
            [$"lock teams share {string.Join(",", new[] { f.Mars.Id, f.Venus.Id }.Order())}", "link 2"],
            f.Store.Calls);
    }
}
