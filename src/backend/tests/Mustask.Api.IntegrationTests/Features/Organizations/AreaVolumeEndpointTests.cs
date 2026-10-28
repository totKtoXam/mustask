using System.Diagnostics;
using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Organizations;
using Xunit.Abstractions;

namespace Mustask.Api.IntegrationTests.Features.Organizations;

/// <summary>
/// mod-018 spec § NFR Volumes through the real host and a real PostgreSQL: in an organization of about 1 000 nodes over 10 levels,
/// 200 projects and 200 teams the whole tree answers within 2 s, and so do a department's page with 200 linked teams, its team list
/// and its deletion preview, a project's subtree and a team's departments. The data are written in one transaction.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class AreaVolumeEndpointTests(MustaskApiFactory factory, ITestOutputHelper output)
{
    private const int DeepProjects = 100;
    private const int ShallowProjects = 100;
    private const int Depth = 10;
    private const int Teams = 200;
    private static readonly TimeSpan Budget = TimeSpan.FromSeconds(2);

    [Fact]
    public async Task Reads_1000Nodes_200Projects_200Teams_Within2SecondsEach()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        Area? hub = null;
        Guid firstProject = Guid.Empty;
        var teamIds = new List<Guid>();

        await OrganizationsTestData.InUnitOfWorkAsync(factory, async (services, ct) =>
        {
            var areas = services.GetRequiredService<IAreaRepository>();
            var projects = services.GetRequiredService<IProjectRepository>();
            var teams = services.GetRequiredService<ITeamRepository>();
            var links = services.GetRequiredService<IAreaLinkRepository>();
            await areas.LockTreeAsync(organization.Id, ct);
            hub = Area.CreateDepartment(organization.Id, OrganizationsTestData.Name("Центр"), null, OrganizationsTestData.Creator, OrganizationsTestData.Now);
            areas.Add(hub);

            for (var index = 0; index < DeepProjects + ShallowProjects; index++)
            {
                var created = Project.Create(
                    organization.Id, OrganizationsTestData.Name($"Проект {index:D3}"), $"V{index:D3}", OrganizationsTestData.Now, OrganizationsTestData.Creator);
                projects.Add(created);
                firstProject = firstProject == Guid.Empty ? created.Project.Id : firstProject;
                var parent = created.Root;

                for (var level = 2; level <= Depth && index < DeepProjects; level++)
                {
                    var sub = Area.CreateSubArea(parent, OrganizationsTestData.Name($"Ветка {level:D2}"), null, OrganizationsTestData.Creator, OrganizationsTestData.Now);
                    areas.Add(sub);
                    parent = sub;
                }
            }

            for (var index = 0; index < Teams; index++)
            {
                var team = Team.Create(organization.Id, OrganizationsTestData.Name($"Команда {index:D3}"));
                teams.Add(team);
                teamIds.Add(team.Id);
            }

            await areas.SaveAsync(ct);
            await links.LinkTeamsAsync(organization.Id, teamIds.Select(id => (hub.Id, id)).ToList(), OrganizationsTestData.Creator, OrganizationsTestData.Now, ct);
            await areas.SaveAsync(ct);

            return true;
        });

        var area = $"{OrganizationsApi.Areas(organization.Id)}/{hub!.Id}";
        var reads = new (string Name, string Path, int Expected)[]
        {
            ("tree", OrganizationsApi.Areas(organization.Id), 1 + (DeepProjects * Depth) + ShallowProjects),
            ("page", area, -1),
            ("teams", $"{area}/teams", Teams),
            ("deletion", $"{area}/deletion", -1),
            ("project subtree", OrganizationsApi.ProjectAreas(organization.Id, firstProject), Depth),
            ("team departments", OrganizationsApi.TeamAreas(organization.Id, teamIds[0]), 1),
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
        Assert.Equal(Teams, deletion.Body.GetProperty("teams").GetArrayLength());
    }

    private Task<Answer> GetAsync(string path) =>
        OrganizationsApi.SendAsync(factory, HttpMethod.Get, path, token: OrganizationsTokens.SignedIn());
}
