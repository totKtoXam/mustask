using System.Diagnostics;
using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Organizations;
using Xunit.Abstractions;

namespace Mustask.Api.IntegrationTests.Features.Organizations;

/// <summary>
/// mod-018 spec § NFR Volumes through the real host and a real PostgreSQL: in an organization of 1 000 areas over 10 levels, 200 projects
/// and 200 teams the whole tree answers within 2 s, and so do an area's page with 200 linked projects and 200 linked teams, its two link
/// lists and its deletion preview, a project's areas and a team's areas. The data are written in one transaction through the repositories.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class AreaVolumeEndpointTests(MustaskApiFactory factory, ITestOutputHelper output)
{
    private const int Chains = 100;
    private const int Depth = 10;
    private const int Ends = 200;
    private static readonly TimeSpan Budget = TimeSpan.FromSeconds(2);

    [Fact]
    public async Task Reads_1000Areas_200Projects_200Teams_Within2SecondsEach()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        Area? hub = null;
        var projectIds = new List<Guid>();
        var teamIds = new List<Guid>();

        await OrganizationsTestData.InUnitOfWorkAsync(factory, async (services, ct) =>
        {
            var areas = services.GetRequiredService<IAreaRepository>();
            var projects = services.GetRequiredService<IProjectRepository>();
            var teams = services.GetRequiredService<ITeamRepository>();
            var links = services.GetRequiredService<IAreaLinkRepository>();
            await areas.LockTreeAsync(organization.Id, ct);

            for (var chain = 0; chain < Chains; chain++)
            {
                Guid? parent = null;

                for (var level = 1; level <= Depth; level++)
                {
                    var area = Area.Create(organization.Id, parent, OrganizationsTestData.Name($"Ветка {chain:D3} уровень {level:D2}"), null, OrganizationsTestData.Creator, OrganizationsTestData.Now);
                    areas.Add(area);
                    parent = area.Id;
                    hub ??= area;
                }
            }

            for (var index = 0; index < Ends; index++)
            {
                var project = Project.Create(organization.Id, OrganizationsTestData.Name($"Проект {index:D3}"), $"V{index:D3}", OrganizationsTestData.Now, OrganizationsTestData.Creator);
                projects.Add(project);
                projectIds.Add(project.Id);
                var team = Team.Create(organization.Id, OrganizationsTestData.Name($"Команда {index:D3}"));
                teams.Add(team);
                teamIds.Add(team.Id);
            }

            await areas.SaveAsync(ct);
            await links.LinkProjectsAsync(organization.Id, projectIds.Select(id => (hub!.Id, id)).ToList(), OrganizationsTestData.Creator, OrganizationsTestData.Now, ct);
            await links.LinkTeamsAsync(organization.Id, teamIds.Select(id => (hub!.Id, id)).ToList(), OrganizationsTestData.Creator, OrganizationsTestData.Now, ct);
            await areas.SaveAsync(ct);

            return true;
        });

        var area = $"{OrganizationsApi.Areas(organization.Id)}/{hub!.Id}";
        var reads = new (string Name, string Path, int Expected)[]
        {
            ("tree", OrganizationsApi.Areas(organization.Id), Chains * Depth),
            ("page", area, -1),
            ("projects", $"{area}/projects", Ends),
            ("teams", $"{area}/teams", Ends),
            ("deletion", $"{area}/deletion", -1),
        };

        foreach (var (name, path, expected) in reads)
        {
            Assert.Equal(HttpStatusCode.OK, (await GetAsync(path)).Status); // warm-up, not asserted
            var watch = Stopwatch.StartNew();
            var answer = await GetAsync(path);
            watch.Stop();
            output.WriteLine($"{name}: {watch.Elapsed.TotalMilliseconds:F0} ms");

            Assert.Equal(HttpStatusCode.OK, answer.Status);
            Assert.True(watch.Elapsed < Budget, $"{name} took {watch.Elapsed}.");

            if (expected >= 0)
            {
                Assert.Equal(expected, answer.Body.GetArrayLength());
            }
        }

        var deletion = await GetAsync($"{area}/deletion");
        Assert.Equal((Ends, Ends), (deletion.Body.GetProperty("projects").GetArrayLength(), deletion.Body.GetProperty("teams").GetArrayLength()));

        // A project's and a team's areas, with the project and the team linked to a few areas of the tree.
        var firstProject = projectIds[0];
        var firstTeam = teamIds[0];
        Assert.Equal(1, (await GetAsync(OrganizationsApi.ProjectAreas(organization.Id, firstProject))).Body.GetArrayLength());
        var projectAreas = Stopwatch.StartNew();
        var projectAnswer = await GetAsync(OrganizationsApi.ProjectAreas(organization.Id, firstProject));
        projectAreas.Stop();
        var teamAreas = Stopwatch.StartNew();
        var teamAnswer = await GetAsync(OrganizationsApi.TeamAreas(organization.Id, firstTeam));
        teamAreas.Stop();
        output.WriteLine($"project areas: {projectAreas.Elapsed.TotalMilliseconds:F0} ms, team areas: {teamAreas.Elapsed.TotalMilliseconds:F0} ms");
        Assert.True(projectAreas.Elapsed < Budget && teamAreas.Elapsed < Budget);
        Assert.Equal((HttpStatusCode.OK, HttpStatusCode.OK), (projectAnswer.Status, teamAnswer.Status));
    }

    private Task<Answer> GetAsync(string path) =>
        OrganizationsApi.SendAsync(factory, HttpMethod.Get, path, token: OrganizationsTokens.SignedIn());
}
