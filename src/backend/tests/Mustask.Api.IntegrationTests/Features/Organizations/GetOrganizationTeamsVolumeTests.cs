using System.Diagnostics;
using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Organizations;
using Xunit.Abstractions;

namespace Mustask.Api.IntegrationTests.Features.Organizations;

/// <summary>
/// feat-028 US-1 and the NFR Volumes through the real host and a real PostgreSQL: an organization with 200 teams linked to 50 projects
/// lists them within 2 s (a fixed number of statements, ADR-0028 rule 13), each with its active projects, by name; the project's linked list
/// of the same organization (50 teams) answers inside the same bound. The data are written in one transaction through the repositories.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class GetOrganizationTeamsVolumeTests(MustaskApiFactory factory, ITestOutputHelper output)
{
    private const int Teams = 200;
    private const int Projects = 50;
    private static readonly TimeSpan Budget = TimeSpan.FromSeconds(2);

    [Fact]
    public async Task Get_200Teams_Within2Seconds_EachWithItsProjects()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var projectIds = new List<Guid>();
        var teamIds = new List<Guid>();

        await OrganizationsTestData.InUnitOfWorkAsync(factory, async (services, ct) =>
        {
            var projects = services.GetRequiredService<IProjectRepository>();
            var teams = services.GetRequiredService<ITeamRepository>();
            var links = services.GetRequiredService<IProjectTeamRepository>();

            for (var p = 0; p < Projects; p++)
            {
                var project = Project.Create(organization.Id, OrganizationsTestData.Name($"Проект {p:D3}"), $"P{p:D3}", OrganizationsTestData.Now, OrganizationsTestData.Creator);
                projects.Add(project);
                projectIds.Add(project.Id);
            }

            for (var t = 0; t < Teams; t++)
            {
                var team = Team.Create(organization.Id, OrganizationsTestData.Name($"Команда {t:D3}"));
                teams.Add(team);
                teamIds.Add(team.Id);

                // Every team is in two projects, and team t is in the project t % 50 and the one after it.
                foreach (var p in new[] { t % Projects, (t + 1) % Projects })
                {
                    links.Add(ProjectTeam.Link(organization.Id, projectIds[p], team.Id, OrganizationsTestData.Creator, OrganizationsTestData.Now));
                }
            }

            await projects.SaveAsync(ct);

            return true;
        });

        // One warm-up, not asserted: a process's first call of an operation also pays JIT and connection warm-up.
        Assert.Equal(HttpStatusCode.OK, (await ListAsync(organization.Id)).Status);

        var watch = Stopwatch.StartNew();
        var answer = await ListAsync(organization.Id);
        watch.Stop();
        output.WriteLine($"listing {Teams} teams over {Projects} projects: {watch.Elapsed.TotalMilliseconds:F0} ms");

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.True(watch.Elapsed < Budget, $"The list took {watch.Elapsed}.");
        Assert.Equal(Teams, answer.Body.GetArrayLength());
        Assert.Equal(
            Enumerable.Range(0, Teams).Select(t => $"Команда {t:D3}"),
            answer.Body.EnumerateArray().Select(team => team.GetProperty("name").GetString()));
        Assert.All(answer.Body.EnumerateArray(), team => Assert.Equal(2, team.GetProperty("projects").GetArrayLength()));

        var projectList = await OrganizationsApi.SendAsync(
            factory, HttpMethod.Get, OrganizationsApi.Teams(organization.Id, projectIds[0]), token: OrganizationsTokens.SignedIn());
        Assert.Equal(HttpStatusCode.OK, projectList.Status);
        Assert.Equal(8, projectList.Body.GetArrayLength());
    }

    private Task<Answer> ListAsync(Guid organizationId) =>
        OrganizationsApi.SendAsync(factory, HttpMethod.Get, OrganizationsApi.OrgTeams(organizationId), token: OrganizationsTokens.SignedIn());
}
