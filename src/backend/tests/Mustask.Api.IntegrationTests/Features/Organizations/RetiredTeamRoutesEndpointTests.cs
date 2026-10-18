using System.Net;
using Mustask.Domain.Organizations;
using static Mustask.Api.IntegrationTests.Features.Organizations.TeamTestData;

namespace Mustask.Api.IntegrationTests.Features.Organizations;

/// <summary>
/// feat-028 api-contract § 2.6 (BR-OL-12; ADR-0028 rule 7): the eight project-nested team routes no longer exist. The platform answers each
/// as a path that does not exist — 404, the platform's own <c>http.not_found</c>, no code of ours — and none changes anything. There is no
/// alias: a team is the organization's and is addressed at <c>…/teams/{teamId}</c>; the project keeps only <c>…/teams</c> (the linked
/// teams), <c>…/teams/{teamId}/unlink</c>, <c>…/unlink-count</c> and the team views.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class RetiredTeamRoutesEndpointTests(MustaskApiFactory factory)
{
    public static TheoryData<string, string> Routes => new()
    {
        { "GET", "" },
        { "PUT", "/name" },
        { "DELETE", "" },
        { "GET", "/owned-items/count" },
        { "GET", "/members" },
        { "POST", "/members" },
        { "DELETE", "/members/{userId}" },
        { "GET", "/member-candidates" },
    };

    // feat-028 US-1 · "the project-nested routes are gone" (RT ProjectNestedTeamRoutes_AnswerAsPathsThatDoNotExist, the eight of § 2.6): each
    // answers 404 with the platform's code, whoever asks, with a body that would once have been valid; nothing changes.
    [Theory]
    [MemberData(nameof(Routes))]
    public async Task ProjectNestedTeamRoutes_AnswerAsPathsThatDoNotExist_ChangeNothing(string method, string suffix)
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var member = await MembershipTestData.NewMemberAsync(factory, structure.OrganizationId, Mustask.Domain.Identity.OrganizationRole.Member);
        var teamId = structure.Platform.Id;
        var path = $"{ProjectTeam(structure.OrganizationId, structure.ProjectId, teamId)}{suffix.Replace("{userId}", member.Id, StringComparison.Ordinal)}";
        object? body = method switch
        {
            "PUT" => new { name = "Переименованная" },
            "POST" => new { userId = member.Id },
            _ => null,
        };
        var membersBefore = await TeamMemberIdsAsync(factory, teamId);

        var answer = await OrganizationsApi.SendAsync(factory, new HttpMethod(method), path, body, OrganizationsTokens.SignedIn());

        Assert.Equal(HttpStatusCode.NotFound, answer.Status);
        Assert.Equal("http.not_found", answer.ErrorCode);
        var stored = await StoredTeamAsync(factory, teamId);
        Assert.Equal(("Платформа", TeamStatus.Active), (stored.Name, stored.Status));
        Assert.Equal(membersBefore, await TeamMemberIdsAsync(factory, teamId));
        Assert.Single(await LinksOfTeamAsync(factory, teamId));
    }

    // The same eight for a person who is not a member of the organization: a path that does not exist answers the same to everyone, so
    // the route table is not a way to probe an organization.
    [Theory]
    [MemberData(nameof(Routes))]
    public async Task ProjectNestedTeamRoutes_AnswerTheSame_ToANonMember(string method, string suffix)
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var path = $"{ProjectTeam(structure.OrganizationId, structure.ProjectId, structure.Platform.Id)}{suffix.Replace("{userId}", "someone", StringComparison.Ordinal)}";

        var answer = await OrganizationsApi.SendAsync(factory, new HttpMethod(method), path, null, OrganizationsTokens.Stranger());

        Assert.Equal((HttpStatusCode.NotFound, "http.not_found"), (answer.Status, answer.ErrorCode));
    }

    // feat-028 § 4 · "POST …/projects/{projectId}/teams remains with another meaning", and so do the reads of the project's end: the linked
    // list, the team views and the unlink pair still answer at the project's address.
    [Fact]
    public async Task ProjectsTeamsList_StillAnswers_AndTheTeamViewsAndTheUnlinkPairToo()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var team = ProjectTeam(structure.OrganizationId, structure.ProjectId, structure.Platform.Id);

        var list = await GetAsync(factory, OrganizationsApi.Teams(structure.OrganizationId, structure.ProjectId));
        var count = await GetAsync(factory, $"{team}/unlink-count");
        var backlog = await GetAsync(factory, $"{team}/backlogs/stories");
        var board = await GetAsync(factory, $"{team}/boards/stories");

        Assert.Equal(HttpStatusCode.OK, list.Status);
        Assert.Equal(2, list.Body.GetArrayLength());
        Assert.Equal(HttpStatusCode.OK, count.Status);
        Assert.Equal(HttpStatusCode.OK, backlog.Status);
        Assert.Equal(HttpStatusCode.OK, board.Status);
    }

    // feat-028 US-1 · and the moved routes answer at the organization's address, for the same team.
    [Fact]
    public async Task TheMovedRoutes_AnswerAtTheOrganizationsAddress()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var team = Team(structure.OrganizationId, structure.Platform.Id);

        var read = await GetAsync(factory, team);
        var members = await GetAsync(factory, $"{team}/members");
        var candidates = await GetAsync(factory, $"{team}/member-candidates?text=%D0%90");
        var rename = await OrganizationsApi.SendAsync(factory, HttpMethod.Put, $"{team}/name", new { name = "Платформа 2" }, OrganizationsTokens.SignedIn());

        Assert.Equal(
            (HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.OK, HttpStatusCode.OK),
            (read.Status, members.Status, candidates.Status, rename.Status));
    }
}
