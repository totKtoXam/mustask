using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mustask.Api.IntegrationTests.Features.WorkItems;
using Mustask.Domain.Organizations;
using Mustask.Infrastructure;
using static Mustask.Api.IntegrationTests.Features.Organizations.TeamTestData;

namespace Mustask.Api.IntegrationTests.Features.Organizations;

/// <summary>
/// The concurrency pairs of mod-018's spec § NFR Concurrency on the typed tree (ADR-0031, the lock order of ADR-0028 rule 8 reworked): two
/// requests meet on a lock, the first held at its commit gate, the second sent and waiting, then both released. Each pair ends in one order
/// and no 5xx; the invariant (one name at a level, no cycle, no 11th level, no node under a deleted parent, no link to a deleted end) is
/// read from the database, not from the answers.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class AreaRacesEndpointTests(MustaskApiFactory factory)
{
    private Task<Answer> SendAsync(HttpMethod method, string path, object? body, CommitGate? gate) =>
        OrganizationsApi.SendAsync(factory, method, path, body, OrganizationsTokens.SignedIn(), gate);

    private static string Area(Guid organizationId, Guid areaId) => $"{OrganizationsApi.Areas(organizationId)}/{areaId}";

    private Task<Answer> CreateAsync(Guid organizationId, string name, Guid? parentId, CommitGate? gate) =>
        SendAsync(HttpMethod.Post, OrganizationsApi.Areas(organizationId), new { name, parentId = parentId?.ToString() }, gate);

    private Task<Answer> RenameAsync(Guid organizationId, Guid areaId, string name, CommitGate? gate) =>
        SendAsync(HttpMethod.Put, $"{Area(organizationId, areaId)}/name", new { name }, gate);

    private Task<Answer> MoveAsync(Guid organizationId, Guid areaId, Guid? parentId, CommitGate? gate) =>
        SendAsync(HttpMethod.Put, $"{Area(organizationId, areaId)}/parent", new { parentId = parentId?.ToString() }, gate);

    private Task<Answer> DeleteAsync(Guid organizationId, Guid areaId, CommitGate? gate) =>
        SendAsync(HttpMethod.Delete, Area(organizationId, areaId), null, gate);

    private async Task<List<Area>> StoredAreasAsync(Guid organizationId)
    {
        using var scope = factory.Services.CreateScope();

        return await scope.ServiceProvider.GetRequiredService<MustaskDbContext>().Areas
            .AsNoTracking()
            .Where(area => area.OrganizationId == organizationId)
            .ToListAsync();
    }

    private static void AssertOneOf(Answer a, Answer b, HttpStatusCode winner, HttpStatusCode loser, string loserCode)
    {
        var answers = new[] { a, b };
        Assert.DoesNotContain(answers, answer => (int)answer.Status >= 500);
        Assert.Single(answers, answer => answer.Status == winner);
        var refused = Assert.Single(answers, answer => answer.Status == loser);
        Assert.Equal(loserCode, refused.ErrorCode);
    }

    // US-1 · two writes of one name at one level keep one: two top-level creations of «Аналитика».
    [Fact]
    public async Task Create_SameTopLevelNameConcurrently_OneDepartment_TheOtherIsNameTaken()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);

        var (a, b) = await RaceAsync(factory, at => CreateAsync(organization.Id, "Аналитика", null, at), at => CreateAsync(organization.Id, "аналитика ", null, at));

        AssertOneOf(a, b, HttpStatusCode.Created, HttpStatusCode.Conflict, "organizations.area.name_taken");
        Assert.Single(await StoredAreasAsync(organization.Id));
    }

    // OQ-12 A · a department and a project's creation of one name at the top level meet on the tree lock: one is name_taken.
    [Fact]
    public async Task CreateDepartment_VersusCreateProject_OfOneName_OneIsNameTaken()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var projects = $"/api/organizations/{organization.Id}/projects";

        var (a, b) = await RaceAsync(
            factory,
            at => CreateAsync(organization.Id, "Мастаск", null, at),
            at => SendAsync(HttpMethod.Post, projects, new { name = "мастаск", key = "MST" }, at));

        Assert.DoesNotContain(new[] { a, b }, answer => (int)answer.Status >= 500);
        Assert.Equal(HttpStatusCode.Created, a.Status);
        LinksTestData.AssertRefused(b, HttpStatusCode.Conflict, "organizations.area.name_taken");
        Assert.Single(await StoredAreasAsync(organization.Id));
    }

    // US-1 · a creation against a rename into the name: the rename holds only its row, the creation meets it on the sibling index.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Create_VersusRenameIntoTheSameName_OneArea_TheOtherIsNameTaken(bool renameFirst)
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var organization = structure.OrganizationId;
        var node = await OrganizationsTestData.NodeOfAsync(factory, structure.ProjectId);
        var transport = await OrganizationsTestData.AddSubAreaAsync(factory, organization, node.Id, "Транспортные системы");

        var (a, b) = renameFirst
            ? await RaceAsync(factory, at => RenameAsync(organization, transport.Id, "Аналитика", at), at => CreateAsync(organization, "Аналитика", node.Id, at))
            : await RaceAsync(factory, at => CreateAsync(organization, "Аналитика", node.Id, at), at => RenameAsync(organization, transport.Id, "Аналитика", at));

        AssertOneOf(a, b, renameFirst ? HttpStatusCode.OK : HttpStatusCode.Created, HttpStatusCode.Conflict, "organizations.area.name_taken");
        Assert.Single(await StoredAreasAsync(organization), area => area.ParentId == node.Id && area.NormalizedName == "АНАЛИТИКА");
    }

    // US-3 · two moves that together would form a cycle: one is refused, both orders.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Move_TwoThatFormACycle_OneIsRefused_TheTreeStaysAForest(bool financeFirst)
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var organization = structure.OrganizationId;
        var node = await OrganizationsTestData.NodeOfAsync(factory, structure.ProjectId);
        var finance = await OrganizationsTestData.AddSubAreaAsync(factory, organization, node.Id, "Финансы");
        var infra = await OrganizationsTestData.AddSubAreaAsync(factory, organization, node.Id, "Инфраструктура");

        Task<Answer> FinanceUnderInfra(CommitGate? at) => MoveAsync(organization, finance.Id, infra.Id, at);
        Task<Answer> InfraUnderFinance(CommitGate? at) => MoveAsync(organization, infra.Id, finance.Id, at);

        var (a, b) = financeFirst
            ? await RaceAsync(factory, FinanceUnderInfra, InfraUnderFinance)
            : await RaceAsync(factory, InfraUnderFinance, FinanceUnderInfra);

        AssertOneOf(a, b, HttpStatusCode.OK, HttpStatusCode.Conflict, "organizations.area.move_into_own_subtree");
        var stored = await StoredAreasAsync(organization);
        Assert.Single(stored, area => area.ParentId == node.Id && area.Type == AreaType.Area);
        Assert.Equal(4, stored.Count);
    }

    // US-3 · the tree is at most 10 levels deep after a move: a move that lands a subtree on level 10 against a creation below it, both orders.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task MoveAndCreate_ThatTogetherPassTenLevels_OneIsRefused_NoAreaOnLevel11(bool moveFirst)
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var organization = structure.OrganizationId;
        var node = await OrganizationsTestData.NodeOfAsync(factory, structure.ProjectId);
        var level8 = node;

        for (var level = 2; level <= 8; level++)
        {
            level8 = await OrganizationsTestData.AddSubAreaAsync(factory, organization, level8.Id, $"Ф{level}");
        }

        var logistics = await OrganizationsTestData.AddSubAreaAsync(factory, organization, node.Id, "Логистика");
        var storage = await OrganizationsTestData.AddSubAreaAsync(factory, organization, logistics.Id, "Складские системы");

        // Alone, each is allowed: «Логистика» on level 9 with «Складские системы» on level 10; a child of «Складские системы» at its place is level 4.
        Task<Answer> Move(CommitGate? at) => MoveAsync(organization, logistics.Id, level8.Id, at);
        Task<Answer> Create(CommitGate? at) => CreateAsync(organization, "Новая", storage.Id, at);

        var (a, b) = moveFirst ? await RaceAsync(factory, Move, Create) : await RaceAsync(factory, Create, Move);

        Assert.DoesNotContain(new[] { a, b }, answer => (int)answer.Status >= 500);
        var moved = moveFirst ? a : b;
        var created = moveFirst ? b : a;

        if (moveFirst)
        {
            Assert.Equal(HttpStatusCode.OK, moved.Status);
            LinksTestData.AssertRefused(created, HttpStatusCode.Conflict, "organizations.area.depth_exceeded");
        }
        else
        {
            Assert.Equal(HttpStatusCode.Created, created.Status);
            LinksTestData.AssertRefused(moved, HttpStatusCode.Conflict, "organizations.area.depth_exceeded");
        }

        var stored = await StoredAreasAsync(organization);
        var tree = new AreaTree(stored.Select(area => new AreaNode(area.Id, area.ParentId, area.Name, area.Type, area.ProjectId)).ToList());
        Assert.All(stored, area => Assert.True(tree.LevelOf(area.Id) <= 10, $"{area.Name} stands on level {tree.LevelOf(area.Id)}."));
    }

    // US-2 · a deletion and a write that needs the area at the same moment: delete a leaf against a child's creation under it or a move
    // under it, both orders: no node under a deleted parent.
    [Theory]
    [InlineData("create", true)]
    [InlineData("create", false)]
    [InlineData("move", true)]
    [InlineData("move", false)]
    public async Task Delete_VersusAWriteUnderTheLeaf_OneIsRefused_NoAreaUnderADeletedParent(string write, bool deleteFirst)
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var organization = structure.OrganizationId;
        var node = await OrganizationsTestData.NodeOfAsync(factory, structure.ProjectId);
        var leaf = await OrganizationsTestData.AddSubAreaAsync(factory, organization, node.Id, "Транспортные системы");
        var finance = await OrganizationsTestData.AddSubAreaAsync(factory, organization, node.Id, "Финансы");

        Task<Answer> Delete(CommitGate? at) => DeleteAsync(organization, leaf.Id, at);
        Task<Answer> Write(CommitGate? at) => write == "create"
            ? CreateAsync(organization, "Дочерняя", leaf.Id, at)
            : MoveAsync(organization, finance.Id, leaf.Id, at);

        var (first, second) = deleteFirst ? await RaceAsync(factory, Delete, Write) : await RaceAsync(factory, Write, Delete);
        var (delete, wrote) = deleteFirst ? (first, second) : (second, first);

        Assert.DoesNotContain(new[] { delete, wrote }, answer => (int)answer.Status >= 500);

        if (deleteFirst)
        {
            Assert.Equal(HttpStatusCode.NoContent, delete.Status);
            LinksTestData.AssertRefused(wrote, HttpStatusCode.NotFound, "organizations.area.not_found");
        }
        else
        {
            Assert.True(wrote.Status is HttpStatusCode.Created or HttpStatusCode.OK, $"The write answered {wrote.Status}.");
            LinksTestData.AssertRefused(delete, HttpStatusCode.Conflict, "organizations.area.not_empty");
        }

        var stored = await StoredAreasAsync(organization);
        var ids = stored.Select(area => area.Id).ToHashSet();
        Assert.All(stored.Where(area => area.ParentId is not null), area => Assert.Contains(area.ParentId!.Value, ids));
    }

    // OQ-3 A, OQ-5 A · a project node's move under a department against the department's deletion, both orders: no node under a deleted department.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task MoveProjectNode_VersusTheDeletionOfTheDepartment_OneIsRefused(bool deleteFirst)
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var organization = structure.OrganizationId;
        var node = await OrganizationsTestData.NodeOfAsync(factory, structure.ProjectId);
        var department = await OrganizationsTestData.AddDepartmentAsync(factory, organization, "Разработка");

        Task<Answer> Delete(CommitGate? at) => DeleteAsync(organization, department.Id, at);
        Task<Answer> Move(CommitGate? at) => MoveAsync(organization, node.Id, department.Id, at);

        var (first, second) = deleteFirst ? await RaceAsync(factory, Delete, Move) : await RaceAsync(factory, Move, Delete);
        var (delete, moved) = deleteFirst ? (first, second) : (second, first);

        Assert.DoesNotContain(new[] { delete, moved }, answer => (int)answer.Status >= 500);

        if (deleteFirst)
        {
            Assert.Equal(HttpStatusCode.NoContent, delete.Status);
            LinksTestData.AssertRefused(moved, HttpStatusCode.NotFound, "organizations.area.not_found");
        }
        else
        {
            Assert.Equal(HttpStatusCode.OK, moved.Status);
            LinksTestData.AssertRefused(delete, HttpStatusCode.Conflict, "organizations.area.not_empty");
        }

        var stored = await StoredAreasAsync(organization);
        var ids = stored.Select(area => area.Id).ToHashSet();
        Assert.All(stored.Where(area => area.ParentId is not null), area => Assert.Contains(area.ParentId!.Value, ids));
    }

    // OQ-10 A · a project's rename against a move of its node (both take the tree lock first): no 5xx, the name and the place both stand.
    [Fact]
    public async Task RenameProject_VersusTheMoveOfItsNode_BothApply()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var organization = structure.OrganizationId;
        var node = await OrganizationsTestData.NodeOfAsync(factory, structure.ProjectId);
        var department = await OrganizationsTestData.AddDepartmentAsync(factory, organization, "Разработка");

        var (a, b) = await RaceAsync(
            factory,
            at => MoveAsync(organization, node.Id, department.Id, at),
            at => SendAsync(HttpMethod.Put, $"{OrganizationsApi.Project(organization, structure.ProjectId)}/name", new { name = "Новое имя" }, at));

        Assert.Equal((HttpStatusCode.OK, HttpStatusCode.OK), (a.Status, b.Status));
        var stored = Assert.Single(await StoredAreasAsync(organization), area => area.Id == node.Id);
        Assert.Equal((department.Id, "Новое имя"), (stored.ParentId, stored.Name));
    }

    // US-2 · a rename that comes after a deletion is answered «Область не найдена.», and one that commits first is followed by the deletion.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Rename_VersusTheDeletionOfTheArea_BothOrders(bool deleteFirst)
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var organization = structure.OrganizationId;
        var node = await OrganizationsTestData.NodeOfAsync(factory, structure.ProjectId);
        var leaf = await OrganizationsTestData.AddSubAreaAsync(factory, organization, node.Id, "Транспортные системы");

        Task<Answer> Delete(CommitGate? at) => DeleteAsync(organization, leaf.Id, at);
        Task<Answer> Rename(CommitGate? at) => RenameAsync(organization, leaf.Id, "Другое", at);

        var (first, second) = deleteFirst ? await RaceAsync(factory, Delete, Rename) : await RaceAsync(factory, Rename, Delete);
        var (delete, rename) = deleteFirst ? (first, second) : (second, first);

        Assert.Equal(HttpStatusCode.NoContent, delete.Status);
        Assert.Equal(
            deleteFirst ? (HttpStatusCode.NotFound, "organizations.area.not_found") : (HttpStatusCode.OK, (string?)null),
            (rename.Status, rename.ErrorCode));
        Assert.DoesNotContain(await StoredAreasAsync(organization), area => area.Id == leaf.Id);
    }


    private Task<Answer> MoveTeamAsync(Guid organizationId, Guid teamId, Guid departmentId, CommitGate? gate) =>
        SendAsync(HttpMethod.Put, $"{OrganizationsApi.OrgTeams(organizationId)}/{teamId}/department", new { departmentId = departmentId.ToString() }, gate);

    private async Task<Guid?> DepartmentOfTeamAsync(Guid teamId) => (await StoredTeamAsync(factory, teamId)).DepartmentId;

    // mod-021 US-3 · a team's move into a department against that department's deletion, both orders: the team never stands in a deleted
    // department and nothing answers 5xx. Moved first, the department holds a team and refuses; deleted first, the move finds it gone.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task MoveTeam_VersusTheDeletionOfTheDestination_BothOrders_NeverIntoADeletedDepartment(bool moveFirst)
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var source = await OrganizationsTestData.AddDepartmentAsync(factory, organization.Id, "Разработка");
        var destination = await OrganizationsTestData.AddDepartmentAsync(factory, organization.Id, "Продажи");
        var team = await OrganizationsTestData.AddTeamInAsync(factory, organization.Id, source.Id, "SATURN");

        Task<Answer> Move(CommitGate? at) => MoveTeamAsync(organization.Id, team.Id, destination.Id, at);
        Task<Answer> Delete(CommitGate? at) => DeleteAsync(organization.Id, destination.Id, at);

        var (first, second) = moveFirst ? await RaceAsync(factory, Move, Delete) : await RaceAsync(factory, Delete, Move);
        var (move, delete) = moveFirst ? (first, second) : (second, first);

        Assert.DoesNotContain(new[] { move, delete }, answer => (int)answer.Status >= 500);

        if (moveFirst)
        {
            Assert.Equal(HttpStatusCode.OK, move.Status);
            Assert.Equal((HttpStatusCode.Conflict, "organizations.area.has_teams"), (delete.Status, delete.ErrorCode));
            Assert.Equal(destination.Id, await DepartmentOfTeamAsync(team.Id));
        }
        else
        {
            Assert.Equal(HttpStatusCode.NoContent, delete.Status);
            Assert.Equal((HttpStatusCode.NotFound, "organizations.area.not_found"), (move.Status, move.ErrorCode));
            Assert.Equal(source.Id, await DepartmentOfTeamAsync(team.Id));
        }
    }

    // mod-021 US-1 · a team's creation in a department against that department's deletion, both orders.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CreateTeam_VersusTheDeletionOfItsDepartment_BothOrders_NoTeamInADeletedDepartment(bool createFirst)
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var department = await OrganizationsTestData.AddDepartmentAsync(factory, organization.Id, "Разработка");

        Task<Answer> Create(CommitGate? at) =>
            SendAsync(HttpMethod.Post, OrganizationsApi.OrgTeams(organization.Id), new { name = "NEPTUNE", departmentId = department.Id.ToString() }, at);
        Task<Answer> Delete(CommitGate? at) => DeleteAsync(organization.Id, department.Id, at);

        var (first, second) = createFirst ? await RaceAsync(factory, Create, Delete) : await RaceAsync(factory, Delete, Create);
        var (create, delete) = createFirst ? (first, second) : (second, first);

        Assert.DoesNotContain(new[] { create, delete }, answer => (int)answer.Status >= 500);

        if (createFirst)
        {
            Assert.Equal(HttpStatusCode.Created, create.Status);
            Assert.Equal((HttpStatusCode.Conflict, "organizations.area.has_teams"), (delete.Status, delete.ErrorCode));
            Assert.All(await StoredTeamsAsync(factory, organization.Id), team => Assert.Equal(department.Id, team.DepartmentId));
        }
        else
        {
            Assert.Equal(HttpStatusCode.NoContent, delete.Status);
            Assert.Equal((HttpStatusCode.NotFound, "organizations.area.not_found"), (create.Status, create.ErrorCode));
            Assert.Empty(await StoredTeamsAsync(factory, organization.Id));
        }
    }

    // mod-021 US-3 · a move against a link of the team to a project, both orders: a team linked first does not move, a team moved first links.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task MoveTeam_VersusALinkOfTheTeam_BothOrders_NeverLinkedAndMoved(bool moveFirst)
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var source = await OrganizationsTestData.AddDepartmentAsync(factory, structure.OrganizationId, "Разработка");
        var destination = await OrganizationsTestData.AddDepartmentAsync(factory, structure.OrganizationId, "Продажи");
        var team = await OrganizationsTestData.AddTeamInAsync(factory, structure.OrganizationId, source.Id, "SATURN");

        Task<Answer> Move(CommitGate? at) => MoveTeamAsync(structure.OrganizationId, team.Id, destination.Id, at);
        Task<Answer> Link(CommitGate? at) =>
            LinkTeamsAsync(factory, structure.OrganizationId, structure.ProjectId, new { teamIds = new[] { team.Id.ToString() } }, gate: at);

        var (first, second) = moveFirst ? await RaceAsync(factory, Move, Link) : await RaceAsync(factory, Link, Move);
        var (move, link) = moveFirst ? (first, second) : (second, first);

        Assert.DoesNotContain(new[] { move, link }, answer => (int)answer.Status >= 500);

        if (moveFirst)
        {
            Assert.Equal(HttpStatusCode.OK, move.Status);
            Assert.Equal(destination.Id, await DepartmentOfTeamAsync(team.Id));
            Assert.NotEmpty(await LinksOfTeamAsync(factory, team.Id));
        }
        else
        {
            Assert.Equal((HttpStatusCode.Conflict, "organizations.team.has_links"), (move.Status, move.ErrorCode));
            Assert.Equal(source.Id, await DepartmentOfTeamAsync(team.Id));
            Assert.NotEmpty(await LinksOfTeamAsync(factory, team.Id));
        }
    }

    // mod-021 US-3 · two moves of one team into two departments at once: both succeed, the team stands in exactly the last one.
    [Fact]
    public async Task MoveTeam_TwoMovesAtOnce_TheTeamStandsInOneOfThem()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var source = await OrganizationsTestData.AddDepartmentAsync(factory, organization.Id, "Разработка");
        var first = await OrganizationsTestData.AddDepartmentAsync(factory, organization.Id, "Продажи");
        var second = await OrganizationsTestData.AddDepartmentAsync(factory, organization.Id, "Финансы");
        var team = await OrganizationsTestData.AddTeamInAsync(factory, organization.Id, source.Id, "SATURN");

        var (a, b) = await RaceAsync(
            factory,
            at => MoveTeamAsync(organization.Id, team.Id, first.Id, at),
            at => MoveTeamAsync(organization.Id, team.Id, second.Id, at));

        Assert.Equal((HttpStatusCode.OK, HttpStatusCode.OK), (a.Status, b.Status));
        Assert.Equal(second.Id, await DepartmentOfTeamAsync(team.Id));
    }
}
