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
/// (mod-017 api-contract § 2.7) through the real host and a real PostgreSQL (plan-backend
/// § 13, lane L-17). Every test builds its own organization
/// (<see cref="OrganizationsTestData"/>): the database is shared and never
/// emptied. A fresh structure holds the project «Пример проекта» with its team and root, and a second team «Платформа»; a
/// team has no area (BR-OT-10), so the tree is the root alone.
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

    // mod-017 US-4 · "A team added later is an ordinary one" (BR-OT-11, BR-OT-10): «Аналитика» joins «Пример проекта» and
    // «Платформа»; it comes with no area, carries no mark (the project already has teams and the settings name none), and the
    // others are untouched. Replaces Add_CreatesTeam_WithRootChildOwnedAndDefault (retired, BR-OT-11).
    [Fact]
    public async Task Add_CreatesAnOrdinaryTeam_NoAreaCreated()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);

        var answer = await AddAsync(structure, new { name = " Аналитика " });
        var teamId = answer.Body.GetProperty("id").GetGuid();

        Assert.Equal(HttpStatusCode.Created, answer.Status);
        Assert.Equal("application/json", answer.MediaType);
        Assert.Equal(
            $"/api/organizations/{structure.OrganizationId}/projects/{structure.ProjectId}/teams/{teamId}",
            answer.Location?.OriginalString);
        Assert.Equal(["defaultMark", "id", "isMember", "name", "projectId"], PropertyNames(answer.Body));
        Assert.Equal(structure.ProjectId, answer.Body.GetProperty("projectId").GetGuid());
        Assert.Equal("Аналитика", answer.Body.GetProperty("name").GetString());
        Assert.False(answer.Body.GetProperty("isMember").GetBoolean());
        Assert.Equal(JsonValueKind.Null, answer.Body.GetProperty("defaultMark").ValueKind);

        // No area is created, and the others are untouched; the list is by name (BR-OT-11).
        Assert.Equal(1, (await OrganizationsTestData.ReadTreeAsync(factory, structure.ProjectId)).Count);
        var listed = await ListActiveTeamsAsync(structure);
        Assert.Equal(["Аналитика", "Платформа", "Пример проекта"], listed.Select(team => team.Name));
        Assert.All(listed, team => Assert.Equal(DefaultTeamMark.None, team.Mark));
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
    }

    // US-4 · "An empty name, or one longer than 100 characters, is refused with «Название должно содержать от 1 до 100
    // символов»": no team is created.
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
    }

    // US-4 · "A team's name is unique among the project's active teams", compared ignoring case and spaces: «платформа »
    // and the project's own team's name.
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
    }

    // US-4 · "«Платформа» is still free as a team name in another project".
    [Fact]
    public async Task Add_SameNameInAnotherProject_Succeeds()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var second = await OrganizationsTestData.CreateProjectAsync(factory, structure.OrganizationId, "Второй проект", "SEC");

        var answer = await AddAsync(structure.OrganizationId, second.Project.Id.ToString(), new { name = "Платформа" });

        Assert.Equal(HttpStatusCode.Created, answer.Status);
        Assert.Equal(second.Project.Id, answer.Body.GetProperty("projectId").GetGuid());
        Assert.Equal(2, (await TeamsOfAsync(structure.ProjectId)).Count);
    }

    // mod-017 US-4 · "A team's name is no area's name" (BR-OT-10): the root has a child «Аналитика» and the team of that name is
    // still created; no area is created and none is renamed. Replaces Add_RootHasChildOfThatName_Returns409AreaNameTaken_CreatesNothing
    // (retired, BR-OT-10).
    [Fact]
    public async Task Add_NameOfAnExistingAreaUnderTheRoot_Succeeds_NoAreaCreated()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        await OrganizationsTestData.AddAreaAsync(factory, structure.ProjectId, structure.RootId, "Аналитика");

        var answer = await AddAsync(structure, new { name = " аналитика " });

        Assert.Equal(HttpStatusCode.Created, answer.Status);
        Assert.Equal("аналитика", answer.Body.GetProperty("name").GetString());
        Assert.Equal(3, (await TeamsOfAsync(structure.ProjectId)).Count);
        Assert.Equal(2, (await OrganizationsTestData.ReadTreeAsync(factory, structure.ProjectId)).Count);
    }

    // mod-017 US-7 · "The deleted team's name is free" (BR-OT-10: there is no area to hold it): «Платформа» was deleted and the
    // team is added again at once. Replaces Add_NameOfDeletedTeam_WhileRootChildRemains_Returns409AreaNameTaken and
    // Add_NameOfDeletedTeam_AfterAreaRenamed_Succeeds (retired, BR-OT-10).
    [Fact]
    public async Task Add_NameOfDeletedTeam_IsFree()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        await OrganizationsTestData.DeleteTeamAsync(factory, structure.ProjectId, structure.Platform.Id);

        var answer = await AddAsync(structure, new { name = "Платформа" });
        var teamId = answer.Body.GetProperty("id").GetGuid();

        Assert.Equal(HttpStatusCode.Created, answer.Status);
        Assert.NotEqual(structure.Platform.Id, teamId);

        var teams = await TeamsOfAsync(structure.ProjectId);
        Assert.Equal(TeamStatus.Deleted, teams.Single(team => team.Id == structure.Platform.Id).Status);
        Assert.Equal(TeamStatus.Active, teams.Single(team => team.Id == teamId).Status);
    }

    // mod-017 US-3 · "adding a second team ends the inference" (BR-OT-4): a project with one team lists it «onlyTeam»; once a
    // second team is added neither carries a mark, and the new team is not marked either.
    [Fact]
    public async Task Add_SecondTeam_EndsTheOnlyTeamMark()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var project = await OrganizationsTestData.CreateProjectAsync(factory, organization.Id);
        var onlyTeam = new OrganizationStructure(organization, project, project.Team);
        Assert.Equal("onlyTeam", Assert.Single(await ListActiveTeamsAsync(onlyTeam)).Mark.ToWireName());

        var answer = await AddAsync(onlyTeam, new { name = "Вторая" });

        Assert.Equal(HttpStatusCode.Created, answer.Status);
        Assert.Equal(JsonValueKind.Null, answer.Body.GetProperty("defaultMark").ValueKind);
        var listed = await ListActiveTeamsAsync(onlyTeam);
        Assert.Equal(2, listed.Count);
        Assert.All(listed, team => Assert.Equal(DefaultTeamMark.None, team.Mark));
    }

    // mod-017 US-4 · "Adding or renaming a team touches no area" (BR-OT-10): the area tree is what it was.
    [Fact]
    public async Task Add_TouchesNoArea()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var reports = await OrganizationsTestData.AddAreaAsync(factory, structure.ProjectId, structure.RootId, "Отчёты");
        var before = await OrganizationsTestData.ReadTreeAsync(factory, structure.ProjectId);

        Assert.Equal(HttpStatusCode.Created, (await AddAsync(structure, new { name = "Аналитика" })).Status);

        var after = await OrganizationsTestData.ReadTreeAsync(factory, structure.ProjectId);
        Assert.Equal(before.PreOrder().Select(node => (node.Id, node.ParentId, node.Name)), after.PreOrder().Select(node => (node.Id, node.ParentId, node.Name)));
        Assert.Equal("Отчёты", after[reports.Id].Name);
    }

    // US-4 · "Two teams of one name at the same moment" (NFR Concurrency). Request A is held at its commit with its team row
    // written; B passes the pre-check (A is not committed), then blocks on the team index until A commits, and loses with the
    // pre-check's code (plan-backend § 6.3, § 7). Exactly one team of the name exists. Renamed (D-16, BR-OT-10):
    // Add_TwoConcurrentSameName_OneTeamAndOneArea -> Add_TwoConcurrentSameName_OneTeam.
    [Fact]
    public async Task Add_TwoConcurrentSameName_OneTeam()
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
        Assert.Equal(1, (await OrganizationsTestData.ReadTreeAsync(factory, structure.ProjectId)).Count);
    }

    /// <summary>The project's teams of every status, as stored now.</summary>
    private async Task<IReadOnlyList<Team>> TeamsOfAsync(Guid projectId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MustaskDbContext>();

        return await db.Teams.AsNoTracking().Where(team => team.ProjectId == projectId).ToListAsync();
    }

    /// <summary>The project's active teams as the read model lists them: by name.</summary>
    private async Task<IReadOnlyList<TeamRow>> ListActiveTeamsAsync(OrganizationStructure structure)
    {
        using var scope = factory.Services.CreateScope();

        return await scope.ServiceProvider
            .GetRequiredService<ITeamReadModel>()
            .ListActiveAsync(structure.OrganizationId, structure.ProjectId, "nobody", CancellationToken.None);
    }

    private static IEnumerable<string> PropertyNames(JsonElement element) =>
        element.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal);
}
