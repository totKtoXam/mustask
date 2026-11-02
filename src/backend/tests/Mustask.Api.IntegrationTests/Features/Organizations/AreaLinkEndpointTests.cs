using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mustask.Api.IntegrationTests.Features.WorkItems;
using Mustask.Domain.Identity;
using Mustask.Domain.Organizations;
using Mustask.Infrastructure;

namespace Mustask.Api.IntegrationTests.Features.Organizations;

/// <summary>
/// mod-018 US-4 (the typed tree): the area–team links join departments only (OQ-8 A) — from the department's end and from the
/// team's end, the changed answers (<c>link_type_not_allowed</c>), idempotence, containment, and a refused creation leaving nothing.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class AreaLinkEndpointTests(MustaskApiFactory factory)
{
    private Task<Answer> SendAsync(HttpMethod method, string path, object? body = null, string? token = null) =>
        OrganizationsApi.SendAsync(factory, method, path, body, token ?? OrganizationsTokens.SignedIn());

    private static string Area(Guid organizationId, Guid areaId) => $"{OrganizationsApi.Areas(organizationId)}/{areaId}";

    private async Task<List<AreaTeam>> LinksAsync(Guid areaId)
    {
        using var scope = factory.Services.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<MustaskDbContext>().Set<AreaTeam>().AsNoTracking().Where(l => l.AreaId == areaId).ToListAsync();
    }

    // US-4 · a department and a team are linked from either end; the page of each shows the other; a second link changes nothing.
    [Fact]
    public async Task Link_FromBothEnds_ShowsFromBoth_IsIdempotent_UnlinkRemovesOnlyTheLink()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var organization = structure.OrganizationId;
        var department = await OrganizationsTestData.AddDepartmentAsync(factory, organization, "Разработка");
        var team = await OrganizationsTestData.AddUnlinkedTeamAsync(factory, organization, "Марс");
        var ivan = await MembershipTestData.NewMemberAsync(factory, organization, OrganizationRole.Admin);

        var fromTheArea = await SendAsync(HttpMethod.Post, $"{Area(organization, department.Id)}/teams", new { teamIds = new[] { team.Id.ToString() } }, ivan.Token);
        var first = Assert.Single(await LinksAsync(department.Id));
        var fromTheTeam = await SendAsync(HttpMethod.Post, OrganizationsApi.TeamAreas(organization, team.Id), new { areaIds = new[] { department.Id.ToString() } });
        var areaTeams = await SendAsync(HttpMethod.Get, $"{Area(organization, department.Id)}/teams");
        var teamAreas = await SendAsync(HttpMethod.Get, OrganizationsApi.TeamAreas(organization, team.Id));
        var unlinked = await SendAsync(HttpMethod.Delete, $"{OrganizationsApi.TeamAreas(organization, team.Id)}/{department.Id}");
        var unlinkedAgain = await SendAsync(HttpMethod.Delete, $"{Area(organization, department.Id)}/teams/{team.Id}");

        Assert.Equal((HttpStatusCode.NoContent, HttpStatusCode.NoContent), (fromTheArea.Status, fromTheTeam.Status));
        Assert.Equal(ivan.Id, first.LinkedBy);
        Assert.Equal("Марс", Assert.Single(areaTeams.Body.EnumerateArray()).GetProperty("name").GetString());
        Assert.Equal("Разработка", Assert.Single(teamAreas.Body.EnumerateArray()).GetProperty("path").GetString());
        Assert.Equal((HttpStatusCode.NoContent, HttpStatusCode.NoContent), (unlinked.Status, unlinkedAgain.Status));
        Assert.Empty(await LinksAsync(department.Id));
        Assert.Contains(await TeamTestData.StoredTeamsAsync(factory, organization), stored => stored.Id == team.Id);
    }

    // OQ-8 A, OQ-11 A · a project's node and a sub-area take no team link; from the team's end one such node refuses the whole request.
    [Fact]
    public async Task Link_ToAProjectNodeOrASubArea_Returns409LinkTypeNotAllowed_LinksNothing()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var organization = structure.OrganizationId;
        var node = await OrganizationsTestData.NodeOfAsync(factory, structure.ProjectId);
        var sub = await OrganizationsTestData.AddSubAreaAsync(factory, organization, node.Id, "Отчёты");
        var department = await OrganizationsTestData.AddDepartmentAsync(factory, organization, "Разработка");

        var toNode = await SendAsync(HttpMethod.Post, $"{Area(organization, node.Id)}/teams", new { teamIds = new[] { structure.Platform.Id.ToString() } });
        var toSub = await SendAsync(HttpMethod.Post, $"{Area(organization, sub.Id)}/teams", new { teamIds = new[] { structure.Platform.Id.ToString() } });
        var mixed = await SendAsync(
            HttpMethod.Post,
            OrganizationsApi.TeamAreas(organization, structure.Platform.Id),
            new { areaIds = new[] { department.Id.ToString(), node.Id.ToString() } });
        var creation = await SendAsync(
            HttpMethod.Post,
            OrganizationsApi.Areas(organization),
            new { name = "Под узлом", parentId = node.Id.ToString(), teamIds = new[] { structure.Platform.Id.ToString() } });

        foreach (var answer in new[] { toNode, toSub, mixed, creation })
        {
            LinksTestData.AssertRefused(answer, HttpStatusCode.Conflict, "organizations.area.link_type_not_allowed");
        }

        Assert.Empty(await LinksAsync(department.Id));
        Assert.Empty(await LinksAsync(node.Id));
        Assert.Equal(3, (await OrganizationsTestData.ReadOrganizationTreeAsync(factory, organization)).Count);
    }

    // US-4 · a department is created with teams in the same step, all or nothing.
    [Fact]
    public async Task Create_DepartmentWithTeams_LinksInTheSameStep_AllOrNothing()
    {
        var mine = await OrganizationsTestData.CreateStructureAsync(factory);
        var theirs = await OrganizationsTestData.CreateStructureAsync(factory);
        var ownTeam = await OrganizationsTestData.AddUnlinkedTeamAsync(factory, mine.OrganizationId, "Марс");

        var refused = await SendAsync(
            HttpMethod.Post,
            OrganizationsApi.Areas(mine.OrganizationId),
            new { name = "Разработка", teamIds = new[] { ownTeam.Id.ToString(), theirs.Platform.Id.ToString() } });
        var created = await SendAsync(HttpMethod.Post, OrganizationsApi.Areas(mine.OrganizationId), new { name = "Разработка", teamIds = new[] { ownTeam.Id.ToString() } });

        LinksTestData.AssertRefused(refused, HttpStatusCode.NotFound, "organizations.team.not_found");
        Assert.Equal(HttpStatusCode.Created, created.Status);
        Assert.Equal(ownTeam.Id, Assert.Single(await LinksAsync(created.Body.GetProperty("id").GetGuid())).TeamId);
    }

    // US-4 · a team or a department of another organization is the same 404 as an unknown one and links nothing.
    [Fact]
    public async Task Link_AnEndOfAnotherOrganization_Returns404_LinksNothing()
    {
        var mine = await OrganizationsTestData.CreateStructureAsync(factory);
        var theirs = await OrganizationsTestData.CreateStructureAsync(factory);
        var department = await OrganizationsTestData.AddDepartmentAsync(factory, mine.OrganizationId, "Разработка");
        var foreignDepartment = await OrganizationsTestData.AddDepartmentAsync(factory, theirs.OrganizationId, "Чужая");

        var team = await SendAsync(HttpMethod.Post, $"{Area(mine.OrganizationId, department.Id)}/teams", new { teamIds = new[] { theirs.Platform.Id.ToString() } });
        var area = await SendAsync(HttpMethod.Post, OrganizationsApi.TeamAreas(mine.OrganizationId, mine.Platform.Id), new { areaIds = new[] { foreignDepartment.Id.ToString() } });

        LinksTestData.AssertRefused(team, HttpStatusCode.NotFound, "organizations.team.not_found");
        LinksTestData.AssertRefused(area, HttpStatusCode.NotFound, "organizations.area.not_found");
        Assert.Empty(await LinksAsync(department.Id));
        Assert.Empty(await LinksAsync(foreignDepartment.Id));
    }

    // US-4 · deleting a team ends its department links, and no department goes.
    [Fact]
    public async Task DeletingATeam_EndsEveryAreaLinkOfIt_AndNoAreaGoes()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var team = await OrganizationsTestData.AddUnlinkedTeamAsync(factory, organization.Id, "Марс");
        var reports = await OrganizationsTestData.AddDepartmentAsync(factory, organization.Id, "Отчёты");
        var finance = await OrganizationsTestData.AddDepartmentAsync(factory, organization.Id, "Финансы");
        await OrganizationsTestData.LinkAreaToTeamAsync(factory, organization.Id, reports.Id, team.Id);
        await OrganizationsTestData.LinkAreaToTeamAsync(factory, organization.Id, finance.Id, team.Id);

        var deleted = await TeamTestData.DeleteTeamAsync(factory, organization.Id, team.Id);

        Assert.Equal(HttpStatusCode.NoContent, deleted.Status);
        Assert.Empty(await LinksAsync(reports.Id));
        Assert.Empty(await LinksAsync(finance.Id));
        Assert.Equal(2, (await OrganizationsTestData.ReadOrganizationTreeAsync(factory, organization.Id)).Count);
    }

    // US-4 · a deleted team is left out of the department's list; an unreachable team cannot be unlinked from the department's end.
    [Fact]
    public async Task Unlink_AnUnknownTeam_Returns404TeamNotFound()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var department = await OrganizationsTestData.AddDepartmentAsync(factory, organization.Id, "Разработка");

        var answer = await SendAsync(HttpMethod.Delete, $"{Area(organization.Id, department.Id)}/teams/{Guid.CreateVersion7()}");

        LinksTestData.AssertRefused(answer, HttpStatusCode.NotFound, "organizations.team.not_found");
    }

    // US-6 · the link writes need area.link and team.link: a member is 403 before any id is looked at.
    [Theory]
    [InlineData(OrganizationRole.Member)]
    [InlineData(OrganizationRole.Viewer)]
    public async Task LinkWrites_ForAMemberAndAViewer_Return403(OrganizationRole role)
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var organization = structure.OrganizationId;
        var department = await OrganizationsTestData.AddDepartmentAsync(factory, organization, "Разработка");
        var user = await MembershipTestData.NewMemberAsync(factory, organization, role);

        var link = await SendAsync(HttpMethod.Post, $"{Area(organization, department.Id)}/teams", new { teamIds = new[] { structure.Platform.Id.ToString() } }, user.Token);
        var create = await SendAsync(
            HttpMethod.Post, OrganizationsApi.Areas(organization), new { name = "Новая", teamIds = new[] { Guid.CreateVersion7().ToString() } }, user.Token);

        LinksTestData.AssertRefused(link, HttpStatusCode.Forbidden, "organizations.access.forbidden");
        LinksTestData.AssertRefused(create, HttpStatusCode.Forbidden, "organizations.access.forbidden");
        Assert.Empty(await LinksAsync(department.Id));
    }
}
