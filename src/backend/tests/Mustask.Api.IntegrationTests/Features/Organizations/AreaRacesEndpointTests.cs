using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mustask.Api.IntegrationTests.Features.WorkItems;
using Mustask.Infrastructure;
using static Mustask.Api.IntegrationTests.Features.Organizations.TeamTestData;

namespace Mustask.Api.IntegrationTests.Features.Organizations;

/// <summary>
/// The concurrency pairs of mod-018's spec § NFR Concurrency (ADR-0028 rules 5 and 8, with the amendment): two requests meet on a lock,
/// the first held at its commit gate, the second sent and waiting, then both released. Each pair ends in one order and no 5xx; the
/// invariant (one name at a level, no cycle, no 11th level, no area under a deleted parent, no link to a deleted end) is read from the
/// database, not from the answers.
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

    private async Task<List<Mustask.Domain.Organizations.Area>> StoredAreasAsync(Guid organizationId)
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

    // US-1 · "Two writes of one name at one level keep one": two top-level creations of «Аналитика».
    [Fact]
    public async Task Create_SameTopLevelNameConcurrently_OneArea_TheOtherIsNameTaken()
    {
        var organization = (await OrganizationsTestData.CreateStructureAsync(factory)).OrganizationId;

        var (a, b) = await RaceAsync(factory, at => CreateAsync(organization, "Аналитика", null, at), at => CreateAsync(organization, "аналитика ", null, at));

        AssertOneOf(a, b, HttpStatusCode.Created, HttpStatusCode.Conflict, "organizations.area.name_taken");
        Assert.Single(await StoredAreasAsync(organization));
    }

    // US-1 · the same, a creation against a rename into the name: the rename holds only its row, the creation meets it on the sibling index.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Create_VersusRenameIntoTheSameName_OneArea_TheOtherIsNameTaken(bool renameFirst)
    {
        var organization = (await OrganizationsTestData.CreateStructureAsync(factory)).OrganizationId;
        var parent = await OrganizationsTestData.AddAreaAsync(factory, organization, null, "Логистика");
        var transport = await OrganizationsTestData.AddAreaAsync(factory, organization, parent.Id, "Транспортные системы");

        var (a, b) = renameFirst
            ? await RaceAsync(factory, at => RenameAsync(organization, transport.Id, "Аналитика", at), at => CreateAsync(organization, "Аналитика", parent.Id, at))
            : await RaceAsync(factory, at => CreateAsync(organization, "Аналитика", parent.Id, at), at => RenameAsync(organization, transport.Id, "Аналитика", at));

        AssertOneOf(
            a, b, renameFirst ? HttpStatusCode.OK : HttpStatusCode.Created, HttpStatusCode.Conflict, "organizations.area.name_taken");
        var stored = await StoredAreasAsync(organization);
        Assert.Single(stored, area => area.ParentId == parent.Id && area.NormalizedName == "АНАЛИТИКА");
    }

    // US-2 · "Two moves that together would form a cycle: one is refused", both orders.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Move_TwoThatFormACycle_OneIsRefused_TheTreeStaysAForest(bool financeFirst)
    {
        var organization = (await OrganizationsTestData.CreateStructureAsync(factory)).OrganizationId;
        var finance = await OrganizationsTestData.AddAreaAsync(factory, organization, null, "Финансы");
        var infra = await OrganizationsTestData.AddAreaAsync(factory, organization, null, "Инфраструктура");

        Task<Answer> FinanceUnderInfra(CommitGate? at) => MoveAsync(organization, finance.Id, infra.Id, at);
        Task<Answer> InfraUnderFinance(CommitGate? at) => MoveAsync(organization, infra.Id, finance.Id, at);

        var (a, b) = financeFirst
            ? await RaceAsync(factory, FinanceUnderInfra, InfraUnderFinance)
            : await RaceAsync(factory, InfraUnderFinance, FinanceUnderInfra);

        AssertOneOf(a, b, HttpStatusCode.OK, HttpStatusCode.Conflict, "organizations.area.move_into_own_subtree");
        var stored = await StoredAreasAsync(organization);
        Assert.Single(stored, area => area.ParentId is null);
        Assert.Equal(2, stored.Count);
    }

    // US-2 · "The tree is at most 10 levels deep after a move": a move that lands the children on level 10 against a creation of a
    // child of one of them, both orders; no area on level 11.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task MoveAndCreate_ThatTogetherPassTenLevels_OneIsRefused_NoAreaOnLevel11(bool moveFirst)
    {
        var organization = (await OrganizationsTestData.CreateOrganizationAsync(factory)).Id;
        Guid? parent = null;
        Mustask.Domain.Organizations.Area? level8 = null;

        for (var level = 1; level <= 8; level++)
        {
            level8 = await OrganizationsTestData.AddAreaAsync(factory, organization, parent, $"Ф{level}");
            parent = level8.Id;
        }

        var logistics = await OrganizationsTestData.AddAreaAsync(factory, organization, null, "Логистика");
        var storage = await OrganizationsTestData.AddAreaAsync(factory, organization, logistics.Id, "Складские системы");

        Task<Answer> Move(CommitGate? at) => MoveAsync(organization, logistics.Id, level8!.Id, at);
        Task<Answer> Create(CommitGate? at) => CreateAsync(organization, "Новая", storage.Id, at);

        // Alone, each is allowed: Логистика (level 9) with «Складские системы» (level 10); a child of «Складские системы» at the top is level 3.
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

        var tree = new Mustask.Domain.Organizations.AreaTree((await StoredAreasAsync(organization))
            .Select(area => new Mustask.Domain.Organizations.AreaNode(area.Id, area.ParentId, area.Name)).ToList());
        Assert.All(await StoredAreasAsync(organization), area => Assert.True(tree.LevelOf(area.Id) <= 10, $"{area.Name} stands on level {tree.LevelOf(area.Id)}."));
    }

    // US-2 · "A deletion and a write that needs the area at the same moment": delete a leaf against a child's creation under it, a move
    // under it, both orders: no area under a deleted parent.
    [Theory]
    [InlineData("create", true)]
    [InlineData("create", false)]
    [InlineData("move", true)]
    [InlineData("move", false)]
    public async Task Delete_VersusAWriteUnderTheLeaf_OneIsRefused_NoAreaUnderADeletedParent(string write, bool deleteFirst)
    {
        var organization = (await OrganizationsTestData.CreateStructureAsync(factory)).OrganizationId;
        var leaf = await OrganizationsTestData.AddAreaAsync(factory, organization, null, "Транспортные системы");
        var finance = await OrganizationsTestData.AddAreaAsync(factory, organization, null, "Финансы");

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

    // US-1 / US-2 · a rename into a name and a move that brings the same name to the same level at the same moment: the rename takes only
    // its own row and the move the tree lock, so they meet on the sibling index — one is applied, the other is name_taken, no 5xx.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Rename_VersusAMoveBringingTheSameNameToTheLevel_OneIsNameTaken(bool renameFirst)
    {
        var organization = (await OrganizationsTestData.CreateStructureAsync(factory)).OrganizationId;
        var top = await OrganizationsTestData.AddAreaAsync(factory, organization, null, "Альфа");
        var child = await OrganizationsTestData.AddAreaAsync(factory, organization, top.Id, "Бета");
        var neighbour = await OrganizationsTestData.AddAreaAsync(factory, organization, null, "Сосед");

        Task<Answer> Rename(CommitGate? at) => RenameAsync(organization, neighbour.Id, "Бета", at);
        Task<Answer> Move(CommitGate? at) => MoveAsync(organization, child.Id, null, at);

        var (a, b) = renameFirst ? await RaceAsync(factory, Rename, Move) : await RaceAsync(factory, Move, Rename);

        AssertOneOf(a, b, HttpStatusCode.OK, HttpStatusCode.Conflict, "organizations.area.name_taken");
        var stored = await StoredAreasAsync(organization);
        Assert.Single(stored, area => area.ParentId is null && area.NormalizedName == "БЕТА");
    }

    // US-2 · "a rename ... of the area that comes after its deletion is answered «Область не найдена.»", and a rename that commits first
    // is simply followed by the deletion.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Rename_VersusTheDeletionOfTheArea_BothOrders(bool deleteFirst)
    {
        var organization = (await OrganizationsTestData.CreateStructureAsync(factory)).OrganizationId;
        var leaf = await OrganizationsTestData.AddAreaAsync(factory, organization, null, "Транспортные системы");

        Task<Answer> Delete(CommitGate? at) => DeleteAsync(organization, leaf.Id, at);
        Task<Answer> Rename(CommitGate? at) => RenameAsync(organization, leaf.Id, "Другое", at);

        var (first, second) = deleteFirst ? await RaceAsync(factory, Delete, Rename) : await RaceAsync(factory, Rename, Delete);
        var (delete, rename) = deleteFirst ? (first, second) : (second, first);

        Assert.Equal(HttpStatusCode.NoContent, delete.Status);
        Assert.Equal(
            deleteFirst ? (HttpStatusCode.NotFound, "organizations.area.not_found") : (HttpStatusCode.OK, (string?)null),
            (rename.Status, rename.ErrorCode));
        Assert.Empty(await StoredAreasAsync(organization));
    }

    // US-3 · "a link against the deletion of either end" — the area: the link first (it holds the area FOR SHARE) and the deletion removes
    // it with the area; the deletion first and the link is «Область не найдена.». Never a link to a deleted area.
    [Theory]
    [InlineData("project", true)]
    [InlineData("project", false)]
    [InlineData("team", true)]
    [InlineData("team", false)]
    public async Task Link_VersusTheDeletionOfTheArea_BothOrders_NoLinkToADeletedAreaRemains(string kind, bool linkFirst)
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var organization = structure.OrganizationId;
        var area = await OrganizationsTestData.AddAreaAsync(factory, organization, null, "Транспортные системы");

        Task<Answer> Link(CommitGate? at) => kind == "project"
            ? SendAsync(HttpMethod.Post, $"{Area(organization, area.Id)}/projects", new { projectIds = new[] { structure.ProjectId.ToString() } }, at)
            : SendAsync(HttpMethod.Post, $"{OrganizationsApi.TeamAreas(organization, structure.Platform.Id)}", new { areaIds = new[] { area.Id.ToString() } }, at);
        Task<Answer> Delete(CommitGate? at) => DeleteAsync(organization, area.Id, at);

        var (first, second) = linkFirst ? await RaceAsync(factory, Link, Delete) : await RaceAsync(factory, Delete, Link);
        var (link, delete) = linkFirst ? (first, second) : (second, first);

        Assert.Equal(HttpStatusCode.NoContent, delete.Status);
        Assert.Equal(
            linkFirst ? (HttpStatusCode.NoContent, (string?)null) : (HttpStatusCode.NotFound, "organizations.area.not_found"),
            (link.Status, link.ErrorCode));
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MustaskDbContext>();
        Assert.Empty(await db.Set<Mustask.Domain.Organizations.AreaProject>().AsNoTracking().Where(l => l.AreaId == area.Id).ToListAsync());
        Assert.Empty(await db.Set<Mustask.Domain.Organizations.AreaTeam>().AsNoTracking().Where(l => l.AreaId == area.Id).ToListAsync());
    }

    // US-3 · a link of an area against the deletion of its team (the second end), both orders, from the area's end and from the team's.
    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public async Task Link_VersusTheDeletionOfTheTeam_BothOrders_NoAreaLinkToADeletedTeamRemains(bool fromTheArea, bool linkFirst)
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var team = await OrganizationsTestData.AddUnlinkedTeamAsync(factory, organization.Id, "Общая");
        var area = await OrganizationsTestData.AddAreaAsync(factory, organization.Id, null, "Транспортные системы");

        Task<Answer> Link(CommitGate? at) => fromTheArea
            ? SendAsync(HttpMethod.Post, $"{Area(organization.Id, area.Id)}/teams", new { teamIds = new[] { team.Id.ToString() } }, at)
            : SendAsync(HttpMethod.Post, OrganizationsApi.TeamAreas(organization.Id, team.Id), new { areaIds = new[] { area.Id.ToString() } }, at);
        Task<Answer> DeleteTeam(CommitGate? at) => DeleteTeamAsync(factory, organization.Id, team.Id, gate: at);

        var (first, second) = linkFirst ? await RaceAsync(factory, Link, DeleteTeam) : await RaceAsync(factory, DeleteTeam, Link);
        var (link, deletion) = linkFirst ? (first, second) : (second, first);

        Assert.Equal(HttpStatusCode.NoContent, deletion.Status);
        Assert.Equal(linkFirst ? HttpStatusCode.NoContent : HttpStatusCode.NotFound, link.Status);
        Assert.DoesNotContain(new[] { link, deletion }, answer => (int)answer.Status >= 500);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MustaskDbContext>();
        Assert.Empty(await db.Set<Mustask.Domain.Organizations.AreaTeam>().AsNoTracking().Where(l => l.TeamId == team.Id).ToListAsync());
    }

    // US-3 · "two requests that link the same unlinked pair at once leave exactly one link": the area's end and the project's end.
    [Fact]
    public async Task Link_SamePairFromBothEndsConcurrently_OneLink_No5xx()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var organization = structure.OrganizationId;
        var area = await OrganizationsTestData.AddAreaAsync(factory, organization, null, "Транспортные системы");

        var (a, b) = await RaceAsync(
            factory,
            at => SendAsync(HttpMethod.Post, $"{Area(organization, area.Id)}/projects", new { projectIds = new[] { structure.ProjectId.ToString() } }, at),
            at => SendAsync(HttpMethod.Post, OrganizationsApi.ProjectAreas(organization, structure.ProjectId), new { areaIds = new[] { area.Id.ToString() } }, at));

        Assert.Equal((HttpStatusCode.NoContent, HttpStatusCode.NoContent), (a.Status, b.Status));
        using var scope = factory.Services.CreateScope();
        var links = await scope.ServiceProvider.GetRequiredService<MustaskDbContext>()
            .Set<Mustask.Domain.Organizations.AreaProject>().AsNoTracking().Where(l => l.AreaId == area.Id).ToListAsync();
        Assert.Single(links);
    }
}
