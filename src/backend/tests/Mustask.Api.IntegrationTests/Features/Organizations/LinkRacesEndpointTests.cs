using System.Net;
using static Mustask.Api.IntegrationTests.Features.Organizations.TeamTestData;

namespace Mustask.Api.IntegrationTests.Features.Organizations;

/// <summary>
/// The races of a link (feat-028 plan-backend § 7.3; ADR-0011 echelon 3 and 1, ADR-0028 rule 8): two requests that link one unlinked pair
/// at once leave one link and no 5xx (the primary key and <c>ON CONFLICT DO NOTHING</c>); a link that meets the deletion of its team ends in
/// one order — the link commits first and the deletion removes it with the rest, or the deletion commits first and the link is refused
/// with <c>team.not_found</c> (C-10). The first request is held at its commit gate; the second must block on a lock of the first.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class LinkRacesEndpointTests(MustaskApiFactory factory)
{
    private Task<Answer> FromTheProjectAsync(Guid organizationId, Guid projectId, Guid teamId, CommitGate? gate) =>
        LinkTeamsAsync(factory, organizationId, projectId, new { teamIds = new[] { teamId.ToString() } }, gate: gate);

    private Task<Answer> FromTheTeamAsync(Guid organizationId, Guid teamId, Guid projectId, CommitGate? gate) =>
        LinkProjectsAsync(factory, organizationId, teamId, new { projectIds = new[] { projectId.ToString() } }, gate: gate);

    // feat-028 US-3 · "Link_SamePairConcurrently_OneLinkNo5xx": the same request twice. The first inserts the pair and is held at its
    // commit; the second waits on the pair's primary key, then does nothing. One link, both 204, the first one's author.
    [Fact]
    public async Task Link_SamePairConcurrently_FromTheProject_OneLink_No5xx()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var project = await OrganizationsTestData.CreateBareProjectAsync(factory, organization.Id);
        var team = await OrganizationsTestData.AddUnlinkedTeamAsync(factory, organization.Id, "Общая");
        var first = await MembershipTestData.NewMemberAsync(factory, organization.Id, Mustask.Domain.Identity.OrganizationRole.Admin);
        var second = await MembershipTestData.NewMemberAsync(factory, organization.Id, Mustask.Domain.Identity.OrganizationRole.Admin);

        var (a, b) = await RaceAsync(
            factory,
            at => LinkTeamsAsync(factory, organization.Id, project.Project.Id, new { teamIds = new[] { team.Id.ToString() } }, first.Token, at),
            at => LinkTeamsAsync(factory, organization.Id, project.Project.Id, new { teamIds = new[] { team.Id.ToString() } }, second.Token, at));

        Assert.Equal((HttpStatusCode.NoContent, HttpStatusCode.NoContent), (a.Status, b.Status));
        var link = Assert.Single(await LinksOfProjectAsync(factory, project.Project.Id));
        Assert.Equal((team.Id, first.Id), (link.TeamId, link.LinkedBy));
    }

    // feat-028 US-3 · the same pair from the two ends at once: the project's page and the team's page link it together.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Link_SamePairConcurrently_FromBothEnds_OneLink_No5xx(bool projectFirst)
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var project = await OrganizationsTestData.CreateBareProjectAsync(factory, organization.Id);
        var team = await OrganizationsTestData.AddUnlinkedTeamAsync(factory, organization.Id, "Общая");

        var (a, b) = projectFirst
            ? await RaceAsync(factory, at => FromTheProjectAsync(organization.Id, project.Project.Id, team.Id, at), at => FromTheTeamAsync(organization.Id, team.Id, project.Project.Id, at))
            : await RaceAsync(factory, at => FromTheTeamAsync(organization.Id, team.Id, project.Project.Id, at), at => FromTheProjectAsync(organization.Id, project.Project.Id, team.Id, at));

        Assert.Equal((HttpStatusCode.NoContent, HttpStatusCode.NoContent), (a.Status, b.Status));
        Assert.Single(await LinksOfProjectAsync(factory, project.Project.Id));
        Assert.Single(await LinksOfTeamAsync(factory, team.Id));
    }

    // feat-028 C-10 · "Link_ToMars_AndItsDeletion_NoLinkToTheDeletedTeamRemains": the link first — it holds the team row FOR SHARE, the
    // deletion waits for it, then removes the link it wrote with the rest: both done, no link to a deleted team. The deletion first —
    // the link waits on the team row, reads the team deleted and is refused team.not_found: no link.
    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public async Task Link_VersusTheDeletionOfTheTeam_BothOrders_NoLinkToADeletedTeamRemains(bool fromTheProject, bool linkFirst)
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var project = await OrganizationsTestData.CreateBareProjectAsync(factory, organization.Id);
        var team = await OrganizationsTestData.AddUnlinkedTeamAsync(factory, organization.Id, "Общая");

        Task<Answer> Link(CommitGate? at) => fromTheProject
            ? FromTheProjectAsync(organization.Id, project.Project.Id, team.Id, at)
            : FromTheTeamAsync(organization.Id, team.Id, project.Project.Id, at);
        Task<Answer> Deletion(CommitGate? at) => DeleteTeamAsync(factory, organization.Id, team.Id, gate: at);

        var (link, deletion) = linkFirst
            ? await RaceAsync(factory, Link, Deletion)
            : Flip(await RaceAsync(factory, Deletion, Link));

        Assert.Equal(HttpStatusCode.NoContent, deletion.Status);
        Assert.Equal(
            linkFirst ? (HttpStatusCode.NoContent, (string?)null) : (HttpStatusCode.NotFound, "organizations.team.not_found"),
            (link.Status, link.ErrorCode));
        Assert.Empty(await LinksOfTeamAsync(factory, team.Id));
        Assert.Empty(await LinksOfProjectAsync(factory, project.Project.Id));
        Assert.Equal(Mustask.Domain.Organizations.TeamStatus.Deleted, (await StoredTeamAsync(factory, team.Id)).Status);
    }

    /// <summary>The race helper answers (held, waiting); a test that sent the deletion first wants (link, deletion).</summary>
    private static (Answer Link, Answer Deletion) Flip((Answer Deletion, Answer Link) answers) => (answers.Link, answers.Deletion);
}
