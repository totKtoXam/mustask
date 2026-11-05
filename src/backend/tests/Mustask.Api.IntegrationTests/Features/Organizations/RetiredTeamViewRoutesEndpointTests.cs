using System.Net;
using Mustask.Api.IntegrationTests.Features.Boards;
using Mustask.Api.IntegrationTests.Features.WorkItems;
using static Mustask.Api.IntegrationTests.Features.Boards.TeamAcrossProjectsTestData;
using static Mustask.Api.IntegrationTests.Features.WorkItems.OwningTeamTestData;

namespace Mustask.Api.IntegrationTests.Features.Organizations;

/// <summary>
/// feat-040 api-contract § 3.4 (BR-TS-1; ADR-0028 § 7; US-1 "The project-nested team addresses are retired"): the team's backlog, its
/// reorder and its board are no longer served under a project — <c>…/projects/{projectId}/teams/{teamId}/backlogs/{level}</c>,
/// <c>…/backlogs/{level}/reorder</c> and <c>…/boards/{level}</c> answer as any path that does not exist does: 404, the platform's
/// <c>http.not_found</c>, no code of ours, whatever the method and whoever asks, whether or not the project and the team exist; and
/// nothing changes. There is no alias. (feat-028's eight routes are <c>RetiredTeamRoutesEndpointTests</c>'; the project keeps
/// <c>…/teams</c>, <c>…/unlink</c> and <c>…/unlink-count</c>.)
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class RetiredTeamViewRoutesEndpointTests(MustaskApiFactory factory)
{
    public static TheoryData<string, string> Routes => new()
    {
        { "GET", "/backlogs/stories" },
        { "POST", "/backlogs/stories/reorder" },
        { "GET", "/boards/stories" },
        { "PUT", "/backlogs/stories" },
        { "DELETE", "/boards/stories" },
        { "PATCH", "/backlogs/stories/reorder" },
        { "GET", "/backlogs/epics" },
        { "GET", "/boards/tasks" },
    };

    // The routes × a member of the organization × an existing project and team, and an unknown project, an unknown team, both.
    [Theory]
    [MemberData(nameof(Routes))]
    public async Task ProjectNestedTeamViewRoutes_AnswerAsPathsThatDoNotExist_ForAMember_ChangeNothing(string method, string suffix)
    {
        var cast = await CreateAsync(factory);
        var story = await StoryAsync(factory, cast.Alpha, "История", cast.Mars);
        var before = await RowsAsync(factory, cast, cast.Mars);
        var projects = new object[] { cast.Alpha.ProjectId, Guid.CreateVersion7() };
        var teams = new object[] { cast.Mars, Guid.CreateVersion7() };
        var body = method is "POST" or "PUT" or "PATCH" ? BoardsTestData.Top(story) : null;

        foreach (var project in projects)
        {
            foreach (var team in teams)
            {
                var path = $"/api/organizations/{cast.OrganizationId}/projects/{project}/teams/{team}{suffix}";

                var answer = await OrganizationsApi.SendAsync(factory, new HttpMethod(method), path, body, OrganizationsTokens.SignedIn());

                Assert.Equal((HttpStatusCode.NotFound, "http.not_found"), (answer.Status, answer.ErrorCode));
            }
        }

        Assert.Equal(before, await RowsAsync(factory, cast, cast.Mars));
        Assert.Equal(0, await TeamPlacesOfAsync(factory, story.Id));
    }

    // The same for a person who is not a member of the organization: a path that does not exist answers the same to everyone, so the
    // route table is no way to probe an organization or a team.
    [Theory]
    [MemberData(nameof(Routes))]
    public async Task ProjectNestedTeamViewRoutes_AnswerTheSame_ToANonMember(string method, string suffix)
    {
        var cast = await CreateAsync(factory);
        var path = $"/api/organizations/{cast.OrganizationId}/projects/{cast.Alpha.ProjectId}/teams/{cast.Mars}{suffix}";

        var answer = await OrganizationsApi.SendAsync(factory, new HttpMethod(method), path, null, OrganizationsTokens.Stranger());

        Assert.Equal((HttpStatusCode.NotFound, "http.not_found"), (answer.Status, answer.ErrorCode));
    }

    // No alias, no redirect: the answer is the platform's 404 with no location, and the team's own address serves the same team.
    [Fact]
    public async Task TheOldAddress_HasNoRedirect_AndTheTeamsOwnAddressServesTheTeam()
    {
        var cast = await CreateAsync(factory);
        var old = await OrganizationsApi.SendAsync(
            factory, HttpMethod.Get, $"/api/organizations/{cast.OrganizationId}/projects/{cast.Alpha.ProjectId}/teams/{cast.Mars}/backlogs/stories", token: OrganizationsTokens.SignedIn());
        var own = await GetBacklogAsync(factory, cast, cast.Mars);

        Assert.Equal(HttpStatusCode.NotFound, old.Status);
        Assert.Null(old.Location);
        Assert.DoesNotContain("MARS", old.Body.GetRawText(), StringComparison.Ordinal);
        Assert.Equal(HttpStatusCode.OK, own.Status);
    }
}
