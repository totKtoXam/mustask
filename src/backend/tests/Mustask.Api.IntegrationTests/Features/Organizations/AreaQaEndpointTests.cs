using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mustask.Api.IntegrationTests.Features.WorkItems;
using Mustask.Domain.Identity;
using Mustask.Domain.Organizations;
using Mustask.Infrastructure;

namespace Mustask.Api.IntegrationTests.Features.Organizations;

/// <summary>
/// mod-018 QA probes beyond the developer's tests: the answer order of US-6 (401, role before not-found, not-found after the address),
/// a refused creation leaving its name and key free (US-3), a re-link keeping its author and time (BR-AW-6), the team deletion ending
/// every area link, all-or-none on the area's end, and the edges of a body (a move with no body, a description counted in UTF-16 units).
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class AreaQaEndpointTests(MustaskApiFactory factory)
{
    private Task<Answer> SendAsync(HttpMethod method, string path, object? body = null, string? token = null) =>
        OrganizationsApi.SendAsync(factory, method, path, body, token ?? OrganizationsTokens.SignedIn());

    private static string Area(Guid organizationId, Guid areaId) => $"{OrganizationsApi.Areas(organizationId)}/{areaId}";

    private async Task<List<AreaProject>> ProjectLinksAsync(Guid areaId)
    {
        using var scope = factory.Services.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<MustaskDbContext>().Set<AreaProject>().AsNoTracking().Where(l => l.AreaId == areaId).ToListAsync();
    }

    // US-6 · "any request of this line sent with no token is answered 401 before anything else", on every route.
    [Fact]
    public async Task EveryAreaRoute_WithNoToken_Returns401()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var organization = structure.OrganizationId;
        var area = await OrganizationsTestData.AddAreaAsync(factory, organization, null, "Отчёты");
        var some = Guid.CreateVersion7().ToString();
        var routes = new (HttpMethod Method, string Path, object? Body)[]
        {
            (HttpMethod.Get, OrganizationsApi.Areas(organization), null),
            (HttpMethod.Post, OrganizationsApi.Areas(organization), new { name = "А" }),
            (HttpMethod.Get, Area(organization, area.Id), null),
            (HttpMethod.Put, $"{Area(organization, area.Id)}/name", new { name = "Б" }),
            (HttpMethod.Put, $"{Area(organization, area.Id)}/description", new { description = "Б" }),
            (HttpMethod.Put, $"{Area(organization, area.Id)}/parent", new { parentId = (string?)null }),
            (HttpMethod.Delete, Area(organization, area.Id), null),
            (HttpMethod.Get, $"{Area(organization, area.Id)}/deletion", null),
            (HttpMethod.Get, $"{Area(organization, area.Id)}/projects", null),
            (HttpMethod.Post, $"{Area(organization, area.Id)}/projects", new { projectIds = new[] { some } }),
            (HttpMethod.Delete, $"{Area(organization, area.Id)}/projects/{some}", null),
            (HttpMethod.Get, $"{Area(organization, area.Id)}/teams", null),
            (HttpMethod.Post, $"{Area(organization, area.Id)}/teams", new { teamIds = new[] { some } }),
            (HttpMethod.Delete, $"{Area(organization, area.Id)}/teams/{some}", null),
            (HttpMethod.Get, OrganizationsApi.ProjectAreas(organization, structure.ProjectId), null),
            (HttpMethod.Post, OrganizationsApi.ProjectAreas(organization, structure.ProjectId), new { areaIds = new[] { some } }),
            (HttpMethod.Delete, $"{OrganizationsApi.ProjectAreas(organization, structure.ProjectId)}/{area.Id}", null),
            (HttpMethod.Get, OrganizationsApi.TeamAreas(organization, structure.Platform.Id), null),
            (HttpMethod.Post, OrganizationsApi.TeamAreas(organization, structure.Platform.Id), new { areaIds = new[] { some } }),
            (HttpMethod.Delete, $"{OrganizationsApi.TeamAreas(organization, structure.Platform.Id)}/{area.Id}", null),
        };

        foreach (var (method, path, body) in routes)
        {
            var answer = await OrganizationsApi.SendAsync(factory, method, path, body, token: null);

            Assert.True(answer.Status == HttpStatusCode.Unauthorized, $"{method} {path}: {answer.Status}");
        }
    }

    // US-6 · "Vera, who renames, moves or deletes an area that does not exist, or creates one under it, is answered 403, not 404;
    // Ann is answered «Область не найдена.»" — and Ann's move of a real area under a missing one, or a link naming a missing project,
    // is answered after the area was found.
    [Fact]
    public async Task AMissingArea_IsA403ForAViewer_A404ForAnAdministrator_AndMissingEndsAnswerAfterTheAddress()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var organization = structure.OrganizationId;
        var finance = await OrganizationsTestData.AddAreaAsync(factory, organization, null, "Финансы");
        var viewer = await MembershipTestData.NewMemberAsync(factory, organization, OrganizationRole.Viewer);
        var missing = Area(organization, Guid.CreateVersion7());

        foreach (var (method, path, body) in new (HttpMethod, string, object?)[]
        {
            (HttpMethod.Put, $"{missing}/name", new { name = "Новое" }),
            (HttpMethod.Put, $"{missing}/parent", new { parentId = (string?)null }),
            (HttpMethod.Put, $"{missing}/parent", new { parentId = finance.Id.ToString() }),
            (HttpMethod.Delete, missing, null),
            (HttpMethod.Post, OrganizationsApi.Areas(organization), new { name = "Дочерняя", parentId = Guid.CreateVersion7().ToString() }),
        })
        {
            LinksTestData.AssertRefused(await SendAsync(method, path, body, viewer.Token), HttpStatusCode.Forbidden, "organizations.access.forbidden");
        }

        LinksTestData.AssertRefused(await SendAsync(HttpMethod.Put, $"{missing}/name", new { name = "Новое" }), HttpStatusCode.NotFound, "organizations.area.not_found");
        LinksTestData.AssertRefused(await SendAsync(HttpMethod.Delete, missing), HttpStatusCode.NotFound, "organizations.area.not_found");
        LinksTestData.AssertRefused(
            await SendAsync(HttpMethod.Put, $"{Area(organization, finance.Id)}/parent", new { parentId = Guid.CreateVersion7().ToString() }),
            HttpStatusCode.NotFound,
            "organizations.area.not_found");
        LinksTestData.AssertRefused(
            await SendAsync(HttpMethod.Post, $"{Area(organization, finance.Id)}/projects", new { projectIds = new[] { Guid.CreateVersion7().ToString() } }),
            HttpStatusCode.NotFound,
            "organizations.project.not_found");
        LinksTestData.AssertRefused(
            await SendAsync(HttpMethod.Post, $"{missing}/projects", new { projectIds = new[] { Guid.CreateVersion7().ToString() } }),
            HttpStatusCode.NotFound,
            "organizations.area.not_found");
    }

    // US-3 · "a creation naming a ... foreign area is refused, nothing is created, and the name and key stay free": a project and a team.
    [Fact]
    public async Task ARefusedCreationWithAreas_LeavesTheNameAndTheKeyFree()
    {
        var mine = await OrganizationsTestData.CreateStructureAsync(factory);
        var theirs = await OrganizationsTestData.CreateStructureAsync(factory);
        var foreign = await OrganizationsTestData.AddAreaAsync(factory, theirs.OrganizationId, null, "Чужая");
        var own = await OrganizationsTestData.AddAreaAsync(factory, mine.OrganizationId, null, "Своя");
        var projects = $"/api/organizations/{mine.OrganizationId}/projects";

        var refusedProject = await SendAsync(HttpMethod.Post, projects, new { name = "Дельта", key = "DEL", areaIds = new[] { foreign.Id.ToString() } });
        var refusedTeam = await SendAsync(HttpMethod.Post, OrganizationsApi.OrgTeams(mine.OrganizationId), new { name = "Нептун", areaIds = new[] { foreign.Id.ToString() } });
        var half = await SendAsync(HttpMethod.Post, projects, new { name = "Дельта", key = "DEL", areaIds = new[] { own.Id.ToString(), foreign.Id.ToString() } });

        LinksTestData.AssertRefused(refusedProject, HttpStatusCode.NotFound, "organizations.area.not_found");
        LinksTestData.AssertRefused(refusedTeam, HttpStatusCode.NotFound, "organizations.area.not_found");
        LinksTestData.AssertRefused(half, HttpStatusCode.NotFound, "organizations.area.not_found");
        var project = await SendAsync(HttpMethod.Post, projects, new { name = "Дельта", key = "DEL", areaIds = new[] { own.Id.ToString() } });
        var team = await SendAsync(HttpMethod.Post, OrganizationsApi.OrgTeams(mine.OrganizationId), new { name = "Нептун", areaIds = new[] { own.Id.ToString() } });
        Assert.Equal((HttpStatusCode.Created, HttpStatusCode.Created), (project.Status, team.Status));
        Assert.Empty(await ProjectLinksAsync(foreign.Id));
    }

    // US-6 · "a creation of a project or a team with areas": 403 for a member before any id is looked at.
    [Fact]
    public async Task CreatingAProjectOrATeamWithAreas_ForAMember_Returns403_NothingIsCreated()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var area = await OrganizationsTestData.AddAreaAsync(factory, structure.OrganizationId, null, "Своя");
        var member = await MembershipTestData.NewMemberAsync(factory, structure.OrganizationId, OrganizationRole.Member);
        var projects = $"/api/organizations/{structure.OrganizationId}/projects";

        var project = await SendAsync(HttpMethod.Post, projects, new { name = "Дельта", key = "DEL", areaIds = new[] { area.Id.ToString() } }, member.Token);
        var team = await SendAsync(HttpMethod.Post, OrganizationsApi.OrgTeams(structure.OrganizationId), new { name = "Нептун", areaIds = new[] { Guid.CreateVersion7().ToString() } }, member.Token);

        LinksTestData.AssertRefused(project, HttpStatusCode.Forbidden, "organizations.access.forbidden");
        LinksTestData.AssertRefused(team, HttpStatusCode.Forbidden, "organizations.access.forbidden");
        Assert.Empty(await ProjectLinksAsync(area.Id));
    }

    // US-3 · "Linking a linked pair changes nothing": the link still records its first author and time, from either end.
    [Fact]
    public async Task ARelink_KeepsTheFirstAuthorAndTime()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var organization = structure.OrganizationId;
        var area = await OrganizationsTestData.AddAreaAsync(factory, organization, null, "Отчёты");
        var ivan = await MembershipTestData.NewMemberAsync(factory, organization, OrganizationRole.Admin);

        await SendAsync(HttpMethod.Post, $"{Area(organization, area.Id)}/projects", new { projectIds = new[] { structure.ProjectId.ToString() } }, ivan.Token);
        var first = Assert.Single(await ProjectLinksAsync(area.Id));
        await Task.Delay(50);
        var again = await SendAsync(HttpMethod.Post, OrganizationsApi.ProjectAreas(organization, structure.ProjectId), new { areaIds = new[] { area.Id.ToString() } });
        var unlinkedPair = await SendAsync(HttpMethod.Delete, $"{Area(organization, area.Id)}/projects/{structure.ProjectId}");
        var unlinkedAgain = await SendAsync(HttpMethod.Delete, $"{Area(organization, area.Id)}/projects/{structure.ProjectId}");

        Assert.Equal(HttpStatusCode.NoContent, again.Status);
        Assert.Equal((ivan.Id, HttpStatusCode.NoContent, HttpStatusCode.NoContent), (first.LinkedBy, unlinkedPair.Status, unlinkedAgain.Status));
        Assert.Empty(await ProjectLinksAsync(area.Id));
    }

    // US-3 · "Several ends are linked in one confirmation, all or none": one project of the list does not exist.
    [Fact]
    public async Task LinkSeveralProjects_OneMissing_LinksNone()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var organization = structure.OrganizationId;
        var area = await OrganizationsTestData.AddAreaAsync(factory, organization, null, "Отчёты");

        var answer = await SendAsync(
            HttpMethod.Post, $"{Area(organization, area.Id)}/projects",
            new { projectIds = new[] { structure.ProjectId.ToString(), Guid.CreateVersion7().ToString() } });
        var malformed = await SendAsync(
            HttpMethod.Post, $"{Area(organization, area.Id)}/projects", new { projectIds = new[] { structure.ProjectId.ToString(), "not-a-guid" } });

        LinksTestData.AssertRefused(answer, HttpStatusCode.NotFound, "organizations.project.not_found");
        LinksTestData.AssertRefused(malformed, HttpStatusCode.NotFound, "organizations.project.not_found");
        Assert.Empty(await ProjectLinksAsync(area.Id));
    }

    // US-3 · "Deleting a team ends its area links": every area of the team, from a plain deletion (no race).
    [Fact]
    public async Task DeletingATeam_EndsEveryAreaLinkOfIt_AndNoAreaGoes()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var team = await OrganizationsTestData.AddUnlinkedTeamAsync(factory, organization.Id, "Марс");
        var reports = await OrganizationsTestData.AddAreaAsync(factory, organization.Id, null, "Отчёты");
        var finance = await OrganizationsTestData.AddAreaAsync(factory, organization.Id, null, "Финансы");
        await OrganizationsTestData.LinkAreaToTeamAsync(factory, organization.Id, reports.Id, team.Id);
        await OrganizationsTestData.LinkAreaToTeamAsync(factory, organization.Id, finance.Id, team.Id);

        var deleted = await TeamTestData.DeleteTeamAsync(factory, organization.Id, team.Id);

        Assert.Equal(HttpStatusCode.NoContent, deleted.Status);
        Assert.Empty((await SendAsync(HttpMethod.Get, $"{Area(organization.Id, reports.Id)}/teams")).Body.EnumerateArray());
        Assert.Empty((await SendAsync(HttpMethod.Get, $"{Area(organization.Id, finance.Id)}/teams")).Body.EnumerateArray());
        Assert.Equal(2, (await OrganizationsTestData.ReadTreeAsync(factory, organization.Id)).Count);
    }

    // BR-AW-4 · the description counts UTF-16 units (api-contract § 0): 1 000 emoji are 2 000 units and kept, 1 001 are refused; a move with
    // no body at all is the top level.
    [Fact]
    public async Task Edges_OfADescriptionInUnits_AndAMoveWithNoBody()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var organization = structure.OrganizationId;
        var parent = await OrganizationsTestData.AddAreaAsync(factory, organization, null, "Отчёты");
        var child = await OrganizationsTestData.AddAreaAsync(factory, organization, parent.Id, "Квартальные");

        var kept = await SendAsync(HttpMethod.Put, $"{Area(organization, parent.Id)}/description", new { description = string.Concat(Enumerable.Repeat("😀", 1000)) });
        var refused = await SendAsync(HttpMethod.Put, $"{Area(organization, parent.Id)}/description", new { description = string.Concat(Enumerable.Repeat("😀", 1001)) });
        var noBody = await SendAsync(HttpMethod.Put, $"{Area(organization, child.Id)}/parent");

        Assert.Equal(HttpStatusCode.OK, kept.Status);
        LinksTestData.AssertRefused(refused, HttpStatusCode.BadRequest, "organizations.validation_failed");
        Assert.True(noBody.Status is HttpStatusCode.OK or HttpStatusCode.BadRequest, $"A move with no body answered {noBody.Status}.");
    }
}
