using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mustask.Api.IntegrationTests.Features.WorkItems;
using Mustask.Domain.Organizations;
using Mustask.Infrastructure;
using static Mustask.Api.IntegrationTests.Features.Organizations.TeamTestData;

namespace Mustask.Api.IntegrationTests.Features.Organizations;

/// <summary>
/// mod-018 backend QA (2026-10-29) over the typed tree: the pairs and boundaries the developer's tests leave out — the project's creation
/// against the deletion of its department, a project's deactivation against the move of its node, the name space of one level
/// across a move and a rename (OQ-12 A), the depth boundary of a project's node moved under a department (BR-AW-14 meets BR-AW-3),
/// and a team's creation naming a project's node. Every test builds its own organization.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class AreaTypedTreeQaEndpointTests(MustaskApiFactory factory)
{
    private Task<Answer> SendAsync(HttpMethod method, string path, object? body = null, CommitGate? gate = null) =>
        OrganizationsApi.SendAsync(factory, method, path, body, OrganizationsTokens.SignedIn(), gate);

    private static string Area(Guid organizationId, Guid areaId) => $"{OrganizationsApi.Areas(organizationId)}/{areaId}";

    private Task<Answer> MoveAsync(Guid organizationId, Guid areaId, Guid? parentId, CommitGate? gate = null) =>
        SendAsync(HttpMethod.Put, $"{Area(organizationId, areaId)}/parent", new { parentId = parentId?.ToString() }, gate);

    private Task<Answer> CreateProjectAsync(Guid organizationId, string name, string key, Guid? parentAreaId, CommitGate? gate = null) =>
        SendAsync(
            HttpMethod.Post,
            $"/api/organizations/{organizationId}/projects",
            new { name, key, parentAreaId = parentAreaId?.ToString() },
            gate);

    private async Task<List<Area>> StoredAreasAsync(Guid organizationId)
    {
        using var scope = factory.Services.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<MustaskDbContext>().Areas
            .AsNoTracking()
            .Where(area => area.OrganizationId == organizationId)
            .ToListAsync();
    }

    // NFR Concurrency · a project's creation under a department against that department's deletion, both orders: either the department goes
    // (the project is then refused as an unknown parent) or the project's node is there and the deletion is not_empty. Never a node under a
    // deleted department, never a project without its node.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CreateProjectUnderADepartment_VersusItsDeletion_OneIsRefused_NoOrphan(bool deleteFirst)
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var department = await OrganizationsTestData.AddDepartmentAsync(factory, organization.Id, "Разработка");

        Task<Answer> Delete(CommitGate? at) => SendAsync(HttpMethod.Delete, Area(organization.Id, department.Id), null, at);
        Task<Answer> Create(CommitGate? at) => CreateProjectAsync(organization.Id, "Мастаск", "MST", department.Id, at);

        var (first, second) = deleteFirst ? await RaceAsync(factory, Delete, Create) : await RaceAsync(factory, Create, Delete);
        var (delete, create) = deleteFirst ? (first, second) : (second, first);

        Assert.DoesNotContain(new[] { delete, create }, answer => (int)answer.Status >= 500);

        if (deleteFirst)
        {
            Assert.Equal(HttpStatusCode.NoContent, delete.Status);
            LinksTestData.AssertRefused(create, HttpStatusCode.NotFound, "organizations.area.not_found");
        }
        else
        {
            Assert.Equal(HttpStatusCode.Created, create.Status);
            LinksTestData.AssertRefused(delete, HttpStatusCode.Conflict, "organizations.area.not_empty");
        }

        var stored = await StoredAreasAsync(organization.Id);
        var ids = stored.Select(area => area.Id).ToHashSet();
        Assert.All(stored.Where(area => area.ParentId is not null), area => Assert.Contains(area.ParentId!.Value, ids));
        Assert.Equal(deleteFirst ? 0 : 1, stored.Count(area => area.Type == AreaType.Project));
    }

    // NFR Concurrency · a project's deactivation against the move of its node, both orders: no 5xx; the node ends where the move put it only
    // if the move came first, and a move that comes after is «Проект не найден.».
    [Fact]
    public async Task DeactivateProject_VersusTheMoveOfItsNode_EitherOrder_NoServerError()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var organization = structure.OrganizationId;
        var node = await OrganizationsTestData.NodeOfAsync(factory, structure.ProjectId);
        var department = await OrganizationsTestData.AddDepartmentAsync(factory, organization, "Разработка");

        // A move reads the project's status without locking it (ADR-0026 rule 8), so neither request waits for the other and the pair
        // cannot be gated; it is sent together and the invariant is read from the database.
        var deactivateTask = SendAsync(HttpMethod.Post, $"{OrganizationsApi.Project(organization, structure.ProjectId)}/deactivate");
        var moveTask = MoveAsync(organization, node.Id, department.Id);
        var (deactivate, moved) = (await deactivateTask, await moveTask);

        Assert.DoesNotContain(new[] { deactivate, moved }, answer => (int)answer.Status >= 500);
        Assert.Equal(HttpStatusCode.NoContent, deactivate.Status);

        var stored = Assert.Single(await StoredAreasAsync(organization), area => area.Id == node.Id);

        if (moved.Status == HttpStatusCode.OK)
        {
            Assert.Equal(department.Id, stored.ParentId);
        }
        else
        {
            LinksTestData.AssertRefused(moved, HttpStatusCode.NotFound, "organizations.project.not_found");
            Assert.Null(stored.ParentId);
        }
    }

    // OQ-12 A · one name space per level, at the top level and across a move: a department cannot take a project node's name, a project
    // cannot be created or renamed into a department's name, and a node moved to the top meets the department of its name.
    [Fact]
    public async Task OneNameSpacePerLevel_AcrossCreateRenameAndMove()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var finance = await OrganizationsTestData.AddDepartmentAsync(factory, organization.Id, "Финансы");
        var logistics = await OrganizationsTestData.AddDepartmentAsync(factory, organization.Id, "Логистика");
        var alpha = await CreateProjectAsync(organization.Id, "Alpha", "ALP", logistics.Id);
        var named = await CreateProjectAsync(organization.Id, "Финансы", "FIN", logistics.Id);
        var alphaNode = await OrganizationsTestData.NodeOfAsync(factory, alpha.Body.GetProperty("id").GetGuid());
        var namedNode = await OrganizationsTestData.NodeOfAsync(factory, named.Body.GetProperty("id").GetGuid());

        var topClash = await CreateProjectAsync(organization.Id, " ЛОГИСТИКА", "LOG", null);
        var beta = await CreateProjectAsync(organization.Id, "Beta", "BET", null);
        var rename = await SendAsync(HttpMethod.Put, $"{OrganizationsApi.Project(organization.Id, beta.Body.GetProperty("id").GetGuid())}/name", new { name = "логистика" });
        var moveClash = await MoveAsync(organization.Id, namedNode.Id, null);
        var moveFree = await MoveAsync(organization.Id, alphaNode.Id, null);
        var departmentTakesAlphaAtTop = await SendAsync(HttpMethod.Post, OrganizationsApi.Areas(organization.Id), new { name = "ALPHA" });

        Assert.Equal((HttpStatusCode.Created, HttpStatusCode.Created), (alpha.Status, named.Status));
        LinksTestData.AssertRefused(topClash, HttpStatusCode.Conflict, "organizations.area.name_taken");
        LinksTestData.AssertRefused(rename, HttpStatusCode.Conflict, "organizations.area.name_taken");
        LinksTestData.AssertRefused(moveClash, HttpStatusCode.Conflict, "organizations.area.name_taken");
        Assert.Equal(HttpStatusCode.OK, moveFree.Status);
        LinksTestData.AssertRefused(departmentTakesAlphaAtTop, HttpStatusCode.Conflict, "organizations.area.name_taken");

        var stored = await StoredAreasAsync(organization.Id);
        Assert.Equal(logistics.Id, Assert.Single(stored, area => area.Id == namedNode.Id).ParentId);
        Assert.Equal("Alpha", Assert.Single(stored, area => area.Id == alphaNode.Id).Name);
    }

    // BR-AW-3 · the depth boundary of a project's node: nine levels counting the node move under a department (ten), ten levels do not.
    [Fact]
    public async Task MoveProjectNode_UnderADepartment_TheNinthLevelFits_TheTenthDoesNot()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var organization = structure.OrganizationId;
        var node = await OrganizationsTestData.NodeOfAsync(factory, structure.ProjectId);
        var department = await OrganizationsTestData.AddDepartmentAsync(factory, organization, "Разработка");
        var branch = node;

        for (var level = 2; level <= 9; level++)
        {
            branch = await OrganizationsTestData.AddSubAreaAsync(factory, organization, branch.Id, $"Уровень {level}");
        }

        var nine = await MoveAsync(organization, node.Id, department.Id);
        var back = await MoveAsync(organization, node.Id, null);
        await OrganizationsTestData.AddSubAreaAsync(factory, organization, branch.Id, "Уровень 10");
        var ten = await MoveAsync(organization, node.Id, department.Id);

        Assert.Equal(HttpStatusCode.OK, nine.Status);
        Assert.Equal(HttpStatusCode.OK, back.Status);
        LinksTestData.AssertRefused(ten, HttpStatusCode.Conflict, "organizations.area.depth_exceeded");
        Assert.Null(Assert.Single(await StoredAreasAsync(organization), area => area.Id == node.Id).ParentId);
    }

    // BR-AW-3 · a department does not move, and the answer for "to the top" (the parent it already has) is a success that changes nothing;
    // under another department it is type_not_allowed. Pins the behaviour api-contract § 3 words as "no, type_not_allowed".
    [Fact]
    public async Task MoveDepartment_ToTheTop_IsANoOp_UnderAnotherDepartment_IsTypeNotAllowed()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var first = await OrganizationsTestData.AddDepartmentAsync(factory, organization.Id, "Первый");
        var second = await OrganizationsTestData.AddDepartmentAsync(factory, organization.Id, "Второй");

        var toTop = await MoveAsync(organization.Id, first.Id, null);
        var under = await MoveAsync(organization.Id, first.Id, second.Id);

        Assert.Equal(HttpStatusCode.OK, toTop.Status);
        LinksTestData.AssertRefused(under, HttpStatusCode.Conflict, "organizations.area.type_not_allowed");
        Assert.Null(Assert.Single(await StoredAreasAsync(organization.Id), area => area.Id == first.Id).ParentId);
    }

    // OQ-8 A, US-3 · a team's creation naming a project's node, a sub-area or a missing area refuses and creates no team.
    [Fact]
    public async Task CreateTeam_NamingANonDepartment_Returns409_CreatesNoTeam()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var organization = structure.OrganizationId;
        var node = await OrganizationsTestData.NodeOfAsync(factory, structure.ProjectId);
        var sub = await OrganizationsTestData.AddSubAreaAsync(factory, organization, node.Id, "Отчёты");
        var department = await OrganizationsTestData.AddDepartmentAsync(factory, organization, "Разработка");
        var before = (await StoredTeamsAsync(factory, organization)).Count;

        var withNode = await SendAsync(HttpMethod.Post, OrganizationsApi.OrgTeams(organization), new { name = "Юпитер", areaIds = new[] { node.Id.ToString() } });
        var withSub = await SendAsync(HttpMethod.Post, OrganizationsApi.OrgTeams(organization), new { name = "Сатурн", areaIds = new[] { department.Id.ToString(), sub.Id.ToString() } });
        var withMissing = await SendAsync(HttpMethod.Post, OrganizationsApi.OrgTeams(organization), new { name = "Уран", areaIds = new[] { Guid.CreateVersion7().ToString() } });
        var fine = await SendAsync(HttpMethod.Post, OrganizationsApi.OrgTeams(organization), new { name = "Венера", areaIds = new[] { department.Id.ToString() } });

        LinksTestData.AssertRefused(withNode, HttpStatusCode.Conflict, "organizations.area.link_type_not_allowed");
        LinksTestData.AssertRefused(withSub, HttpStatusCode.Conflict, "organizations.area.link_type_not_allowed");
        LinksTestData.AssertRefused(withMissing, HttpStatusCode.NotFound, "organizations.area.not_found");
        Assert.Equal(HttpStatusCode.Created, fine.Status);
        Assert.Equal(before + 1, (await StoredTeamsAsync(factory, organization)).Count);
    }

    // BR-AW-11 · the item's «Область» is restricted to its project's subtree: a department, the sibling project's node and a sub-area of
    // it all answer «Область не найдена.» (US-4), the project's own node and sub-areas are accepted.
    [Fact]
    public async Task ItemArea_ADepartmentOrAnotherProjectsNode_IsRefused_OwnSubtreeIsAccepted()
    {
        var world = await WorkItemsTestData.CreateWorldAsync(factory);
        var other = await WorkItemsTestData.AddProjectAsync(factory, world, "OTH", "Другой");
        var otherNode = await OrganizationsTestData.NodeOfAsync(factory, other.ProjectId);
        var otherSub = await OrganizationsTestData.AddSubAreaAsync(factory, world.OrganizationId, otherNode.Id, "Чужая");
        var department = await OrganizationsTestData.AddDepartmentAsync(factory, world.OrganizationId, "Разработка");
        var ownNode = await OrganizationsTestData.NodeOfAsync(factory, world.ProjectId);
        var ownSub = await OrganizationsTestData.AddSubAreaAsync(factory, world.OrganizationId, ownNode.Id, "Своя");
        var bug = await WorkItemsTestData.CreateItemAsync(factory, world, "BUG", "Ошибка");

        var refused = new List<Answer>();

        foreach (var id in new[] { department.Id, otherNode.Id, otherSub.Id })
        {
            refused.Add(await WorkItemsTestData.SaveAsync(factory, world, bug, new { areaId = id.ToString() }));
        }

        var toSub = await WorkItemsTestData.SaveAsync(factory, world, bug, new { areaId = ownSub.Id.ToString() });
        var toNode = await WorkItemsTestData.SaveAsync(factory, world, bug, new { areaId = ownNode.Id.ToString() });

        Assert.All(refused, answer => LinksTestData.AssertRefused(answer, HttpStatusCode.NotFound, "organizations.area.not_found"));
        Assert.Equal(HttpStatusCode.OK, toSub.Status);
        Assert.Equal(HttpStatusCode.OK, toNode.Status);
        Assert.Equal(ownNode.Id, (await WorkItemsTestData.StoredAsync(factory, world.ProjectId))[0].AreaId);
    }

    // BR-AW-10 · a team link of a department survives a department's rename and the move of a project node under it; nothing else moves.
    [Fact]
    public async Task DepartmentLinks_SurviveARenameAndAProjectMovingUnderIt()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var organization = structure.OrganizationId;
        var node = await OrganizationsTestData.NodeOfAsync(factory, structure.ProjectId);
        var department = await OrganizationsTestData.AddDepartmentAsync(factory, organization, "Разработка");
        var team = await OrganizationsTestData.AddUnlinkedTeamAsync(factory, organization, "Марс");
        await OrganizationsTestData.LinkAreaToTeamAsync(factory, organization, department.Id, team.Id);

        var rename = await SendAsync(HttpMethod.Put, $"{Area(organization, department.Id)}/name", new { name = "Инженерия" });
        var move = await MoveAsync(organization, node.Id, department.Id);
        var teams = await SendAsync(HttpMethod.Get, $"{Area(organization, department.Id)}/teams");

        Assert.Equal(HttpStatusCode.OK, rename.Status);
        Assert.Equal(HttpStatusCode.OK, move.Status);
        Assert.Equal("Инженерия / Пример проекта", move.Body.GetProperty("path").GetString());
        Assert.Equal("Марс", Assert.Single(teams.Body.EnumerateArray()).GetProperty("name").GetString());
    }
}
