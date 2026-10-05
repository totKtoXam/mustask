using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Organizations;
using Mustask.Infrastructure;

namespace Mustask.Api.IntegrationTests.Features.Organizations;

/// <summary>
/// <c>POST /api/organizations/{organizationId}/projects/{projectId}/teams</c>
/// (api-contract § 3.3) through the real host and a real PostgreSQL (plan-backend
/// § 13, lane L-17). Every test builds its own organization
/// (<see cref="OrganizationsTestData"/>): the database is shared and never
/// emptied. A fresh structure holds the project «Пример проекта» with its first
/// team and root, and a second team «Платформа» with its area.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class AddTeamEndpointTests(MustaskApiFactory factory)
{
    private Task<Answer> AddAsync(
        Guid organizationId, string projectId, object body, string? token = null, CommitGate? gate = null) =>
        OrganizationsApi.SendAsync(
            factory,
            HttpMethod.Post,
            $"/api/organizations/{organizationId}/projects/{Uri.EscapeDataString(projectId)}/teams",
            body,
            token ?? OrganizationsTokens.SignedIn(),
            gate);

    private Task<Answer> AddAsync(OrganizationStructure structure, object body, CommitGate? gate = null) =>
        AddAsync(structure.OrganizationId, structure.ProjectId.ToString(), body, gate: gate);

    // US-4 · "A second team comes with its own area": «Аналитика» joins «Пример проекта» and «Платформа»; the root gains a
    // child «Аналитика», owned by the new team and set as its default (BR-M1-9, BR-M1-14).
    [Fact]
    public async Task Add_CreatesTeam_WithRootChildOwnedAndDefault()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);

        var answer = await AddAsync(structure, new { name = " Аналитика " });
        var teamId = answer.Body.GetProperty("id").GetGuid();
        var areaId = answer.Body.GetProperty("defaultArea").GetProperty("id").GetGuid();

        Assert.Equal(HttpStatusCode.Created, answer.Status);
        Assert.Equal("application/json", answer.MediaType);
        Assert.Equal(
            $"/api/organizations/{structure.OrganizationId}/projects/{structure.ProjectId}/teams/{teamId}",
            answer.Location?.OriginalString);
        Assert.Equal(["defaultArea", "id", "isFirstTeam", "name", "projectId"], PropertyNames(answer.Body));
        Assert.Equal(["id", "path"], PropertyNames(answer.Body.GetProperty("defaultArea")));
        Assert.Equal(structure.ProjectId, answer.Body.GetProperty("projectId").GetGuid());
        Assert.Equal("Аналитика", answer.Body.GetProperty("name").GetString());
        Assert.False(answer.Body.GetProperty("isFirstTeam").GetBoolean());
        Assert.Equal("Аналитика", answer.Body.GetProperty("defaultArea").GetProperty("path").GetString());

        var tree = await OrganizationsTestData.ReadTreeAsync(factory, structure.ProjectId);
        var area = tree[areaId];
        Assert.Equal(structure.RootId, area.ParentId);
        Assert.Equal("Аналитика", area.Name);
        Assert.Equal(teamId, area.OwnerTeamId);
        Assert.Equal(teamId, area.DefaultOfTeamId);
        Assert.Equal(new ResolvedOwner(teamId, false), tree.ResolveOwner(areaId));
        Assert.Equal("Аналитика", tree.PathOf(areaId));

        // The first team comes first in the project's teams, then display order; the others are untouched.
        var listed = await ListActiveTeamsAsync(structure);
        Assert.Equal(["Пример проекта", "Аналитика", "Платформа"], listed.Select(team => team.Name));
        Assert.Equal(structure.FirstTeamId, listed[0].Id);
        Assert.True(listed[0].IsFirstTeam);
        Assert.Equal(structure.Platform.Area.Id, listed.Single(team => team.Id == structure.Platform.Team.Id).DefaultAreaId);
        Assert.Equal(areaId, listed.Single(team => team.Id == teamId).DefaultAreaId);
    }

    // US-1 · "A visitor who is not signed in sees nothing".
    [Fact]
    public async Task Add_NoToken_Returns401_CreatesNothing()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);

        var answer = await OrganizationsApi.SendAsync(
            factory,
            HttpMethod.Post,
            OrganizationsApi.Teams(structure.OrganizationId, structure.ProjectId),
            new { name = "Аналитика" });

        Assert.Equal(HttpStatusCode.Unauthorized, answer.Status);
        Assert.Equal(2, (await TeamsOfAsync(structure.ProjectId)).Count);
        Assert.Equal(2, (await OrganizationsTestData.ReadTreeAsync(factory, structure.ProjectId)).Count);
    }

    // US-3 · "A project id names no project of the organization" (BR-M1-20, BR-M1-22): malformed, unknown, or of another
    // organization, the project level answers before the body is looked at.
    [Theory]
    [InlineData("malformed")]
    [InlineData("unknown")]
    [InlineData("of another organization")]
    public async Task Add_ProjectNotOfOrganization_Returns404ProjectNotFound(string how)
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var other = await OrganizationsTestData.CreateStructureAsync(factory);
        var projectId = how switch
        {
            "malformed" => "PRM",
            "unknown" => Guid.CreateVersion7().ToString(),
            _ => other.ProjectId.ToString(),
        };

        var answer = await AddAsync(structure.OrganizationId, projectId, new { name = "" });

        Assert.Equal(HttpStatusCode.NotFound, answer.Status);
        Assert.Equal("organizations.project.not_found", answer.ErrorCode);
        Assert.Equal("Проект не найден.", answer.Detail);
        Assert.Equal(2, (await TeamsOfAsync(other.ProjectId)).Count);
        Assert.Equal(2, (await OrganizationsTestData.ReadTreeAsync(factory, other.ProjectId)).Count);
    }

    // US-3 · "A project is deactivated": a write under it answers project.not_found and changes nothing (ADR-0026 rule 5).
    [Fact]
    public async Task Add_UnderDeactivatedProject_Returns404AndChangesNothing()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        await OrganizationsTestData.SetProjectStatusAsync(factory, structure.OrganizationId, structure.ProjectId, ProjectStatus.Deactivated);

        var answer = await AddAsync(structure, new { name = "Аналитика" });

        Assert.Equal(HttpStatusCode.NotFound, answer.Status);
        Assert.Equal("organizations.project.not_found", answer.ErrorCode);
        Assert.Equal(2, (await TeamsOfAsync(structure.ProjectId)).Count);
        Assert.Equal(2, (await OrganizationsTestData.ReadTreeAsync(factory, structure.ProjectId)).Count);
    }

    // US-4 · "An empty name, or one longer than 100 characters, is refused with «Название должно содержать от 1 до 100
    // символов»": no team and no area is created.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("101")]
    public async Task Add_InvalidName_Returns400NameLength_CreatesNothing(string? name)
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);

        var answer = await AddAsync(structure, new { name = name == "101" ? new string('я', 101) : name });

        Assert.Equal(HttpStatusCode.BadRequest, answer.Status);
        Assert.Equal("organizations.validation_failed", answer.ErrorCode);
        var reason = Assert.Single(answer.Body.GetProperty("errors").GetProperty("name").EnumerateArray());
        Assert.Equal("organizations.name_length", reason.GetProperty("code").GetString());
        Assert.Equal("Название должно содержать от 1 до 100 символов", reason.GetProperty("detail").GetString());
        Assert.Equal(2, (await TeamsOfAsync(structure.ProjectId)).Count);
        Assert.Equal(2, (await OrganizationsTestData.ReadTreeAsync(factory, structure.ProjectId)).Count);
    }

    // US-4 · "A team's name is unique among the project's active teams", compared ignoring case and spaces: «платформа »
    // and the first team's own name. The new name's area is taken too, and the team's refusal answers first (§ 3.3).
    [Theory]
    [InlineData("платформа ")]
    [InlineData(" ПЛАТФОРМА")]
    [InlineData("Платформа")]
    [InlineData("пример проекта")]
    public async Task Add_NameTakenAnyCase_Returns409TeamNameTaken_CreatesNothing(string name)
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);

        var answer = await AddAsync(structure, new { name });

        Assert.Equal(HttpStatusCode.Conflict, answer.Status);
        Assert.Equal("organizations.team.name_taken", answer.ErrorCode);
        Assert.Equal("Команда с таким названием уже есть в проекте", answer.Detail);
        Assert.Equal(2, (await TeamsOfAsync(structure.ProjectId)).Count);
        Assert.Equal(2, (await OrganizationsTestData.ReadTreeAsync(factory, structure.ProjectId)).Count);
    }

    // US-4 · "«Платформа» is still free as a team name in another project", and its area lands under that project's root.
    [Fact]
    public async Task Add_SameNameInAnotherProject_Succeeds()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var second = await OrganizationsTestData.CreateProjectAsync(factory, structure.OrganizationId, "Второй проект", "SEC");

        var answer = await AddAsync(structure.OrganizationId, second.Project.Id.ToString(), new { name = "Платформа" });

        Assert.Equal(HttpStatusCode.Created, answer.Status);
        Assert.Equal(second.Project.Id, answer.Body.GetProperty("projectId").GetGuid());
        var areaId = answer.Body.GetProperty("defaultArea").GetProperty("id").GetGuid();
        var secondTree = await OrganizationsTestData.ReadTreeAsync(factory, second.Project.Id);
        Assert.Equal(second.Root.Id, secondTree[areaId].ParentId);
        Assert.Equal(2, (await TeamsOfAsync(structure.ProjectId)).Count);
        Assert.Equal(2, (await OrganizationsTestData.ReadTreeAsync(factory, structure.ProjectId)).Count);
    }

    // US-4 · "A new team's area name must be free under the root": the root has a child «Аналитика» and there is no team of
    // that name; the answer is the area's, and no team and no area is created.
    [Fact]
    public async Task Add_RootHasChildOfThatName_Returns409AreaNameTaken_CreatesNothing()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        await OrganizationsTestData.AddAreaAsync(factory, structure.ProjectId, structure.RootId, "Аналитика");

        var answer = await AddAsync(structure, new { name = " аналитика " });

        Assert.Equal(HttpStatusCode.Conflict, answer.Status);
        Assert.Equal("organizations.area.name_taken", answer.ErrorCode);
        Assert.Equal("Область с таким названием уже есть на этом уровне", answer.Detail);
        Assert.Equal(2, (await TeamsOfAsync(structure.ProjectId)).Count);
        Assert.Equal(3, (await OrganizationsTestData.ReadTreeAsync(factory, structure.ProjectId)).Count);
    }

    // US-7 · "The deleted team's name is free, but its area's name is not": «Платформа» was deleted and the root still has the
    // child «Платформа»; adding the team again is refused with the area's code (not the team's), and nothing is created.
    [Fact]
    public async Task Add_NameOfDeletedTeam_WhileRootChildRemains_Returns409AreaNameTaken()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        await OrganizationsTestData.DeleteTeamAsync(factory, structure.ProjectId, structure.Platform.Team.Id);

        var answer = await AddAsync(structure, new { name = "Платформа" });

        Assert.Equal(HttpStatusCode.Conflict, answer.Status);
        Assert.Equal("organizations.area.name_taken", answer.ErrorCode);
        Assert.Equal(2, (await TeamsOfAsync(structure.ProjectId)).Count);
        Assert.Equal(2, (await OrganizationsTestData.ReadTreeAsync(factory, structure.ProjectId)).Count);
    }

    // US-7 · "The deleted team's name is free, but its area's name is not": that area renamed «Платформа (архив)», the team is
    // created, with a new child area «Платформа» of the root as its default.
    [Fact]
    public async Task Add_NameOfDeletedTeam_AfterAreaRenamed_Succeeds()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        await OrganizationsTestData.DeleteTeamAsync(factory, structure.ProjectId, structure.Platform.Team.Id);
        await RenameAreaAsync(structure.ProjectId, structure.Platform.Area.Id, "Платформа (архив)");

        var answer = await AddAsync(structure, new { name = "Платформа" });
        var teamId = answer.Body.GetProperty("id").GetGuid();
        var areaId = answer.Body.GetProperty("defaultArea").GetProperty("id").GetGuid();

        Assert.Equal(HttpStatusCode.Created, answer.Status);
        Assert.NotEqual(structure.Platform.Team.Id, teamId);
        Assert.NotEqual(structure.Platform.Area.Id, areaId);
        Assert.Equal("Платформа", answer.Body.GetProperty("defaultArea").GetProperty("path").GetString());

        var tree = await OrganizationsTestData.ReadTreeAsync(factory, structure.ProjectId);
        Assert.Equal(structure.RootId, tree[areaId].ParentId);
        Assert.Equal(teamId, tree[areaId].OwnerTeamId);
        Assert.Equal(teamId, tree[areaId].DefaultOfTeamId);
        Assert.Equal("Платформа (архив)", tree[structure.Platform.Area.Id].Name);

        var teams = await TeamsOfAsync(structure.ProjectId);
        Assert.Equal(TeamStatus.Deleted, teams.Single(team => team.Id == structure.Platform.Team.Id).Status);
        Assert.Equal(TeamStatus.Active, teams.Single(team => team.Id == teamId).Status);
    }

    // US-4 · "Two teams of one name at the same moment" (NFR Concurrency). Request A is held at its commit with its team and
    // area rows written; B passes the pre-checks (A is not committed), then blocks on the team index until A commits, and
    // loses with the pre-check's code (plan-backend § 6.3, § 8.1, § 8.3). Exactly one team and one area exist.
    [Fact]
    public async Task Add_TwoConcurrentSameName_OneTeamAndOneArea()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var body = new { name = "Аналитика" };
        using var gate = factory.CommitGates.Open();

        var first = AddAsync(structure, body, gate);
        await Task.WhenAny(gate.Reached, first);
        Assert.False(first.IsCompleted, "The first addition finished before reaching its commit gate.");

        var second = AddAsync(structure, body);
        await PgLocks.WaitUntilBlockedAsync(factory.ConnectionString, second);
        Assert.False(second.IsCompleted, "The second addition did not wait for the first one's uncommitted row.");

        gate.Release();
        var (a, b) = (await first, await second);

        Assert.Equal(HttpStatusCode.Created, a.Status);
        Assert.Equal(HttpStatusCode.Conflict, b.Status);
        Assert.Equal("organizations.team.name_taken", b.ErrorCode);

        var winnerId = a.Body.GetProperty("id").GetGuid();
        var named = (await TeamsOfAsync(structure.ProjectId)).Where(team => team.NormalizedName == "АНАЛИТИКА").ToList();
        Assert.Equal(winnerId, Assert.Single(named).Id);

        var tree = await OrganizationsTestData.ReadTreeAsync(factory, structure.ProjectId);
        var area = Assert.Single(tree.ChildrenOf(structure.RootId), node => node.Name == "Аналитика");
        Assert.Equal(winnerId, area.OwnerTeamId);
        Assert.Equal(winnerId, area.DefaultOfTeamId);
        Assert.Equal(3, tree.Count);
    }

    // NFR Concurrency, BR-M1-9 "a team and its root child area" (QA pass 1): the team «Аналитика» is added at the same
    // moment a child area «Аналитика» is created under the root, in both orders. Each passes its pre-checks alone (the
    // other is not committed); the sibling index decides. The team first: the creation loses with area.name_taken. The
    // area first: the addition loses with area.name_taken and leaves no team without its area behind.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Add_ConcurrentWithARootChildAreaOfTheSameName_OneRefused_NoTeamWithoutItsArea(bool teamFirst)
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var teamBody = new { name = "Аналитика" };
        var areaBody = new { parentId = structure.RootId.ToString(), name = "Аналитика" };
        using var gate = factory.CommitGates.Open();

        Task<Answer> addition;
        Task<Answer> creation;

        if (teamFirst)
        {
            addition = AddAsync(structure, teamBody, gate);
            await Task.WhenAny(gate.Reached, addition);
            Assert.False(addition.IsCompleted, "The addition finished before reaching its commit gate.");

            creation = CreateAreaAsync(structure, areaBody);
            await PgLocks.WaitUntilBlockedAsync(factory.ConnectionString, creation);
            Assert.False(creation.IsCompleted, "The creation did not wait for the addition's uncommitted area.");
        }
        else
        {
            creation = CreateAreaAsync(structure, areaBody, gate);
            await Task.WhenAny(gate.Reached, creation);
            Assert.False(creation.IsCompleted, "The creation finished before reaching its commit gate.");

            addition = AddAsync(structure, teamBody);
            await PgLocks.WaitUntilBlockedAsync(factory.ConnectionString, addition);
            Assert.False(addition.IsCompleted, "The addition did not wait for the creation's uncommitted area.");
        }

        gate.Release();
        var (added, created) = (await addition, await creation);

        var (winner, loser) = teamFirst ? (added, created) : (created, added);
        Assert.Equal(HttpStatusCode.Created, winner.Status);
        Assert.Equal(HttpStatusCode.Conflict, loser.Status);
        Assert.Equal("organizations.area.name_taken", loser.ErrorCode);

        var tree = await OrganizationsTestData.ReadTreeAsync(factory, structure.ProjectId);
        var area = Assert.Single(tree.ChildrenOf(structure.RootId), node => node.Name == "Аналитика");
        var named = (await TeamsOfAsync(structure.ProjectId)).Where(team => team.NormalizedName == "АНАЛИТИКА").ToList();

        if (teamFirst)
        {
            var teamId = added.Body.GetProperty("id").GetGuid();
            Assert.Equal(teamId, Assert.Single(named).Id);
            Assert.Equal((teamId, teamId), (area.OwnerTeamId, area.DefaultOfTeamId));
        }
        else
        {
            Assert.Empty(named);
            Assert.Equal(created.Body.GetProperty("id").GetGuid(), area.Id);
            Assert.Null(area.OwnerTeamId);
        }
    }

    private Task<Answer> CreateAreaAsync(OrganizationStructure structure, object body, CommitGate? gate = null) =>
        OrganizationsApi.SendAsync(
            factory,
            HttpMethod.Post,
            OrganizationsApi.Areas(structure.OrganizationId, structure.ProjectId),
            body,
            OrganizationsTokens.SignedIn(),
            gate);

    /// <summary>The project's teams of every status, as stored now.</summary>
    private async Task<IReadOnlyList<Team>> TeamsOfAsync(Guid projectId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MustaskDbContext>();

        return await db.Teams.AsNoTracking().Where(team => team.ProjectId == projectId).ToListAsync();
    }

    /// <summary>The project's active teams as the read model lists them: the first team first, then display order.</summary>
    private async Task<IReadOnlyList<TeamRow>> ListActiveTeamsAsync(OrganizationStructure structure)
    {
        using var scope = factory.Services.CreateScope();

        return await scope.ServiceProvider
            .GetRequiredService<ITeamReadModel>()
            .ListActiveAsync(structure.OrganizationId, structure.ProjectId, CancellationToken.None);
    }

    /// <summary>Renames a child area as US-5 does, in a unit of work of its own.</summary>
    private Task RenameAreaAsync(Guid projectId, Guid areaId, string name) =>
        OrganizationsTestData.InUnitOfWorkAsync(factory, async (services, ct) =>
        {
            var areas = services.GetRequiredService<IAreaRepository>();
            var area = await areas.LockForUpdateAsync(projectId, areaId, ct)
                ?? throw new ArgumentException("The area is not an area of the project.", nameof(areaId));
            area.Rename(OrganizationsTestData.Name(name));
            await areas.SaveAsync(ct);

            return true;
        });

    private static IEnumerable<string> PropertyNames(JsonElement element) =>
        element.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal);
}
