using Mustask.Application.Features.Organizations.GetTeamOwnedItemCount;
using Mustask.Application.Tests.TestDoubles.Organizations;
using Mustask.Domain.Organizations;
using Mustask.Shared.Exceptions;

namespace Mustask.Application.Tests.Features.Organizations.GetTeamOwnedItemCount;

/// <summary>
/// <c>GetTeamOwnedItemCount</c> on fakes (mod-017 api-contract § 2.9; US-7 "the confirmation counts"): the number of items a team
/// owns, in any state, read through the work items module's contract; the address from the outermost level in; no lock and no
/// write — the count is a read.
/// </summary>
public sealed class GetTeamOwnedItemCountHandlerTests
{
    private sealed class Fixture
    {
        public Fixture()
        {
            Organization = Store.AddOrganization();
            Project = Store.AddProject(Organization);
            Platform = Store.AddTeam(Project);
            Ownership = new FakeWorkItemTeamOwnership(Store);
            Handler = new GetTeamOwnedItemCountHandler(new FakeProjectReadModel(Store), new FakeTeamReadModel(Store), Ownership);
        }

        public InMemoryOrganizations Store { get; } = new();

        public Organization Organization { get; }

        public NewProject Project { get; }

        public Team Platform { get; }

        public FakeWorkItemTeamOwnership Ownership { get; }

        public GetTeamOwnedItemCountHandler Handler { get; }

        public GetTeamOwnedItemCountQuery Query(string? teamId, string? projectId = null, string? organizationId = null) =>
            new(organizationId ?? Organization.Id.ToString(), projectId ?? Project.Project.Id.ToString(), teamId);

        public Task<GetTeamOwnedItemCountResult> HandleAsync(GetTeamOwnedItemCountQuery query) => Handler.Handle(query, CancellationToken.None);
    }

    // US-7 · "the confirmation counts": the number is what the contract says the team owns now; a team that owns none counts zero.
    [Fact]
    public async Task Handle_CountsWhatTheTeamOwns_ZeroWhenItOwnsNothing_LocksNothing()
    {
        var f = new Fixture();
        f.Ownership.Owned[f.Platform.Id] = 12;

        var owning = await f.HandleAsync(f.Query(f.Platform.Id.ToString()));
        var none = await f.HandleAsync(f.Query(f.Project.Team.Id.ToString()));

        Assert.Equal(12, owning.Count);
        Assert.Equal(0, none.Count);
        Assert.Empty(f.Store.Calls);
    }

    // api-contract § 0.4 · the address is answered from the outermost level in.
    [Theory]
    [InlineData("malformed organization", "organizations.organization.not_found")]
    [InlineData("malformed project", "organizations.project.not_found")]
    [InlineData("deactivated project", "organizations.project.not_found")]
    [InlineData("malformed team", "organizations.team.not_found")]
    [InlineData("unknown team", "organizations.team.not_found")]
    [InlineData("deleted team", "organizations.team.not_found")]
    [InlineData("team of another project", "organizations.team.not_found")]
    public async Task Handle_Address_IsAnsweredFromTheOutermostLevelIn(string how, string code)
    {
        var f = new Fixture();
        var sibling = f.Store.AddProject(f.Organization, "Второй проект", "SEC");
        var team = f.Platform.Id.ToString();
        var query = how switch
        {
            "malformed organization" => f.Query(team, organizationId: "abc"),
            "malformed project" => f.Query(team, projectId: "abc"),
            "malformed team" => f.Query("abc"),
            "unknown team" => f.Query(Guid.CreateVersion7().ToString()),
            "team of another project" => f.Query(sibling.Team.Id.ToString()),
            _ => f.Query(team),
        };

        if (how == "deactivated project")
        {
            f.Project.Project.Deactivate();
        }

        if (how == "deleted team")
        {
            f.Platform.Delete(2);
        }

        var refusal = await Assert.ThrowsAsync<NotFoundException>(() => f.HandleAsync(query));

        Assert.Equal(code, refusal.Code);
    }
}
