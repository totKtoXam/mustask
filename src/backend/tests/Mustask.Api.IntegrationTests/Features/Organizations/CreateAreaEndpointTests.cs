using System.Net;
using System.Text.Json;
using Mustask.Domain.Organizations;

namespace Mustask.Api.IntegrationTests.Features.Organizations;

/// <summary>
/// <c>POST /api/organizations/{organizationId}/projects/{projectId}/areas</c>
/// (api-contract § 4.2) through the real host and a real PostgreSQL: the
/// reference use case's Api tests (plan-backend § 14.2), which the lanes copy.
/// Every test builds its own organization (<see cref="OrganizationsTestData"/>):
/// the database is shared and never emptied.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class CreateAreaEndpointTests(MustaskApiFactory factory)
{
    private Task<Answer> CreateAsync(
        Guid organizationId, string projectId, object body, string? token = null, CommitGate? gate = null) =>
        OrganizationsApi.SendAsync(
            factory,
            HttpMethod.Post,
            $"/api/organizations/{organizationId}/projects/{Uri.EscapeDataString(projectId)}/areas",
            body,
            token ?? OrganizationsTokens.SignedIn(),
            gate);

    private Task<Answer> CreateAsync(OrganizationStructure structure, object body, CommitGate? gate = null) =>
        CreateAsync(structure.OrganizationId, structure.ProjectId.ToString(), body, gate: gate);

    // US-5 · "A child area is created under any node": «Отчёты» under the root, «Квартальные» under «Отчёты»; the tree shows
    // «Пример проекта / Отчёты / Квартальные».
    [Fact]
    public async Task Create_UnderRootAndUnderChild_Returns201_TreeShowsThePath()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);

        var reports = await CreateAsync(structure, new { parentId = structure.RootId.ToString(), name = " Отчёты " });
        var reportsId = reports.Body.GetProperty("id").GetGuid();
        var quarterly = await CreateAsync(structure, new { parentId = reportsId.ToString(), name = "Квартальные" });
        var quarterlyId = quarterly.Body.GetProperty("id").GetGuid();

        Assert.Equal(HttpStatusCode.Created, reports.Status);
        Assert.Equal("application/json", reports.MediaType);
        Assert.Equal(
            $"/api/organizations/{structure.OrganizationId}/projects/{structure.ProjectId}/areas/{reportsId}",
            reports.Location?.OriginalString);
        Assert.Equal(["id", "name", "ownerTeamId", "parentId", "projectId"], PropertyNames(reports.Body));
        Assert.Equal(structure.ProjectId, reports.Body.GetProperty("projectId").GetGuid());
        Assert.Equal(structure.RootId, reports.Body.GetProperty("parentId").GetGuid());
        Assert.Equal("Отчёты", reports.Body.GetProperty("name").GetString());
        Assert.Equal(JsonValueKind.Null, reports.Body.GetProperty("ownerTeamId").ValueKind);

        Assert.Equal(HttpStatusCode.Created, quarterly.Status);
        Assert.Equal(reportsId, quarterly.Body.GetProperty("parentId").GetGuid());

        var tree = await OrganizationsTestData.ReadTreeAsync(factory, structure.ProjectId);
        Assert.Equal("Отчёты / Квартальные", tree.PathOf(quarterlyId));
        Assert.Equal("Пример проекта", tree.Root.Name);
        Assert.Equal(2, tree.DepthOf(quarterlyId));
    }

    // US-6 · "A child area is created with or without an owner": the chosen team, or none and the area inherits.
    [Fact]
    public async Task Create_WithOwnerOrInheriting_StoresTheExplicitOwnerOrNone()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);

        var owned = await CreateAsync(structure, new { parentId = structure.RootId.ToString(), name = "Отчёты", ownerTeamId = structure.Platform.Team.Id.ToString() });
        var inheriting = await CreateAsync(structure, new { parentId = structure.RootId.ToString(), name = "Архив", ownerTeamId = (string?)null });

        Assert.Equal(HttpStatusCode.Created, owned.Status);
        Assert.Equal(structure.Platform.Team.Id, owned.Body.GetProperty("ownerTeamId").GetGuid());
        Assert.Equal(HttpStatusCode.Created, inheriting.Status);
        Assert.Equal(JsonValueKind.Null, inheriting.Body.GetProperty("ownerTeamId").ValueKind);

        var tree = await OrganizationsTestData.ReadTreeAsync(factory, structure.ProjectId);
        Assert.Equal(new ResolvedOwner(structure.Platform.Team.Id, false), tree.ResolveOwner(owned.Body.GetProperty("id").GetGuid()));
        Assert.Equal(new ResolvedOwner(structure.FirstTeamId, true), tree.ResolveOwner(inheriting.Body.GetProperty("id").GetGuid()));
    }

    // US-1 · "A visitor who is not signed in sees nothing".
    [Fact]
    public async Task Create_NoToken_Returns401_CreatesNothing()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);

        var answer = await OrganizationsApi.SendAsync(
            factory,
            HttpMethod.Post,
            OrganizationsApi.Areas(structure.OrganizationId, structure.ProjectId),
            new { parentId = structure.RootId.ToString(), name = "Отчёты" });

        Assert.Equal(HttpStatusCode.Unauthorized, answer.Status);
        Assert.Equal(2, (await OrganizationsTestData.ReadTreeAsync(factory, structure.ProjectId)).Count);
    }

    // US-3 · "A project id names no project of the organization" (BR-M1-20, BR-M1-22): malformed, unknown, or of another
    // organization, the project level answers before the body is looked at.
    [Theory]
    [InlineData("malformed")]
    [InlineData("unknown")]
    [InlineData("of another organization")]
    public async Task Create_ProjectNotOfOrganization_Returns404ProjectNotFound(string how)
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var other = await OrganizationsTestData.CreateStructureAsync(factory);
        var projectId = how switch
        {
            "malformed" => "PRM",
            "unknown" => Guid.CreateVersion7().ToString(),
            _ => other.ProjectId.ToString(),
        };

        var answer = await CreateAsync(structure.OrganizationId, projectId, new { parentId = other.RootId.ToString(), name = "" });

        Assert.Equal(HttpStatusCode.NotFound, answer.Status);
        Assert.Equal("organizations.project.not_found", answer.ErrorCode);
        Assert.Equal("Проект не найден.", answer.Detail);
        Assert.Equal(2, (await OrganizationsTestData.ReadTreeAsync(factory, other.ProjectId)).Count);
    }

    // US-3 · "A project is deactivated": a write under it answers project.not_found and changes nothing (ADR-0026 rule 5).
    [Fact]
    public async Task Create_UnderDeactivatedProject_Returns404AndChangesNothing()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        await OrganizationsTestData.SetProjectStatusAsync(factory, structure.OrganizationId, structure.ProjectId, ProjectStatus.Deactivated);

        var answer = await CreateAsync(structure, new { parentId = structure.RootId.ToString(), name = "Отчёты" });

        Assert.Equal(HttpStatusCode.NotFound, answer.Status);
        Assert.Equal("organizations.project.not_found", answer.ErrorCode);
        Assert.Equal(2, (await OrganizationsTestData.ReadTreeAsync(factory, structure.ProjectId)).Count);
    }

    // US-5 · "Sibling names are unique ... an empty name, or one longer than 100 characters, is refused with «Название
    // должно содержать от 1 до 100 символов»". The parent is wrong too: the body's values answer first (api-contract § 0.4).
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("101")]
    public async Task Create_InvalidName_Returns400NameLength_BeforeTheReferences(string? name)
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);

        var answer = await CreateAsync(structure, new
        {
            parentId = Guid.CreateVersion7().ToString(),
            name = name == "101" ? new string('я', 101) : name,
            ownerTeamId = "not-a-team",
        });

        Assert.Equal(HttpStatusCode.BadRequest, answer.Status);
        Assert.Equal("organizations.validation_failed", answer.ErrorCode);
        var reason = Assert.Single(answer.Body.GetProperty("errors").GetProperty("name").EnumerateArray());
        Assert.Equal("organizations.name_length", reason.GetProperty("code").GetString());
        Assert.Equal("Название должно содержать от 1 до 100 символов", reason.GetProperty("detail").GetString());
        Assert.Equal(2, (await OrganizationsTestData.ReadTreeAsync(factory, structure.ProjectId)).Count);
    }

    // US-6 · "An area's owner is a team of the area's own project" (BR-M1-22) and US-7 · "A deleted team answers not found":
    // malformed, unknown, deleted, or of another project or organization. The owner answers before the parent (§ 4.2).
    [Theory]
    [InlineData("malformed")]
    [InlineData("unknown")]
    [InlineData("deleted")]
    [InlineData("of another project")]
    [InlineData("of another organization")]
    public async Task Create_OwnerNotAnActiveTeamOfTheProject_Returns404TeamNotFound_CreatesNothing(string how)
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var secondProject = await OrganizationsTestData.CreateProjectAsync(factory, structure.OrganizationId, "Второй проект", "SEC");
        var other = await OrganizationsTestData.CreateStructureAsync(factory);
        var deleted = await OrganizationsTestData.AddTeamAsync(factory, structure.ProjectId, "Ядро");
        await OrganizationsTestData.DeleteTeamAsync(factory, structure.ProjectId, deleted.Team.Id);
        var ownerTeamId = how switch
        {
            "malformed" => structure.Platform.Team.Id.ToString("N"),
            "unknown" => Guid.CreateVersion7().ToString(),
            "deleted" => deleted.Team.Id.ToString(),
            "of another project" => secondProject.FirstTeam.Id.ToString(),
            _ => other.Platform.Team.Id.ToString(),
        };

        var answer = await CreateAsync(structure, new { parentId = "not-an-area", name = "Отчёты", ownerTeamId });

        Assert.Equal(HttpStatusCode.NotFound, answer.Status);
        Assert.Equal("organizations.team.not_found", answer.ErrorCode);
        Assert.Equal("Команда не найдена.", answer.Detail);
        Assert.DoesNotContain(
            (await OrganizationsTestData.ReadTreeAsync(factory, structure.ProjectId)).PreOrder(),
            node => node.Name == "Отчёты");
    }

    // US-5 · "A node never leaves its project" (BR-M1-22): a parent that is null, malformed, unknown, or an area of another
    // project, answers area.not_found, and both trees are unchanged.
    [Theory]
    [InlineData("null")]
    [InlineData("malformed")]
    [InlineData("unknown")]
    [InlineData("of another project")]
    public async Task Create_ParentNotAnAreaOfTheProject_Returns404AreaNotFound_BothTreesUnchanged(string how)
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var secondProject = await OrganizationsTestData.CreateProjectAsync(factory, structure.OrganizationId, "Второй проект", "SEC");
        var parentId = how switch
        {
            "null" => null,
            "malformed" => "{" + structure.RootId + "}",
            "unknown" => Guid.CreateVersion7().ToString(),
            _ => secondProject.Root.Id.ToString(),
        };

        var answer = await CreateAsync(structure, new { parentId, name = "Отчёты" });

        Assert.Equal(HttpStatusCode.NotFound, answer.Status);
        Assert.Equal("organizations.area.not_found", answer.ErrorCode);
        Assert.Equal("Область не найдена.", answer.Detail);
        Assert.Equal(2, (await OrganizationsTestData.ReadTreeAsync(factory, structure.ProjectId)).Count);
        Assert.Equal(1, (await OrganizationsTestData.ReadTreeAsync(factory, secondProject.Project.Id)).Count);
    }

    // api-contract § 0.2: an id field that is not a JSON string fails binding with the platform's 400.
    [Fact]
    public async Task Create_ParentIdIsANumber_Returns400BadRequest()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);

        var answer = await CreateAsync(structure, new { parentId = 42, name = "Отчёты" });

        Assert.Equal(HttpStatusCode.BadRequest, answer.Status);
        Assert.Equal("http.bad_request", answer.ErrorCode);
    }

    // US-5 · "Sibling names are unique, compared ignoring case and spaces" (BR-M1-12); «Квартальные» is still free under
    // another parent.
    [Fact]
    public async Task Create_SiblingNameAnyCase_Returns409AreaNameTaken_SameNameUnderAnotherParentSucceeds()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var reports = await OrganizationsTestData.AddAreaAsync(factory, structure.ProjectId, structure.RootId, "Отчёты");
        await OrganizationsTestData.AddAreaAsync(factory, structure.ProjectId, reports.Id, "Квартальные");
        var archive = await OrganizationsTestData.AddAreaAsync(factory, structure.ProjectId, structure.RootId, "Архив");

        var taken = await CreateAsync(structure, new { parentId = reports.Id.ToString(), name = " квартальные " });
        var elsewhere = await CreateAsync(structure, new { parentId = archive.Id.ToString(), name = "Квартальные" });
        var rootsName = await CreateAsync(structure, new { parentId = reports.Id.ToString(), name = "Пример проекта" });

        Assert.Equal(HttpStatusCode.Conflict, taken.Status);
        Assert.Equal("organizations.area.name_taken", taken.ErrorCode);
        Assert.Equal("Область с таким названием уже есть на этом уровне", taken.Detail);
        Assert.Equal(HttpStatusCode.Created, elsewhere.Status);
        Assert.Equal(HttpStatusCode.Created, rootsName.Status);

        var tree = await OrganizationsTestData.ReadTreeAsync(factory, structure.ProjectId);
        Assert.Equal(["Квартальные", "Пример проекта"], tree.ChildrenOf(reports.Id).Select(node => node.Name));
    }

    // US-5 · "Two creations of one sibling name at the same moment keep one" (NFR Concurrency). Request A is held at its
    // commit with its row written; B passes the pre-check (A is not committed), then blocks on the sibling index until A
    // commits, and loses with the pre-check's code (plan-backend § 6.3, § 8.3).
    [Fact]
    public async Task Create_TwoConcurrentSameSiblingName_OneCreated_OtherRefused_OneExists()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var body = new { parentId = structure.RootId.ToString(), name = "Квартальные" };
        using var gate = factory.CommitGates.Open();

        var first = CreateAsync(structure, body, gate);
        await Task.WhenAny(gate.Reached, first);
        Assert.False(first.IsCompleted, "The first creation finished before reaching its commit gate.");

        var second = CreateAsync(structure, body);
        await PgLocks.WaitUntilBlockedAsync(factory.ConnectionString, second);
        Assert.False(second.IsCompleted, "The second creation did not wait for the first one's uncommitted row.");

        gate.Release();
        var (a, b) = (await first, await second);

        Assert.Equal(HttpStatusCode.Created, a.Status);
        Assert.Equal(HttpStatusCode.Conflict, b.Status);
        Assert.Equal("organizations.area.name_taken", b.ErrorCode);

        var tree = await OrganizationsTestData.ReadTreeAsync(factory, structure.ProjectId);
        Assert.Single(tree.ChildrenOf(structure.RootId), node => node.Name == "Квартальные");
    }

    // NFR Concurrency, BR-M1-18 "an area created with ... a team that is being deleted" (QA pass 1, A-15). «Отчёты» is
    // created owned by «Платформа» at the same moment «Платформа» is deleted, in both orders. The deletion first: the
    // creation waits on the team's lock, then finds the team deleted and answers team.not_found. The creation first: the
    // deletion waits on the team, then its re-own passes the new area to the first team. In neither order is an area left
    // owned by a deleted team (ADR-0026 rule 8).
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Create_WithAnOwnerTeamBeingDeleted_NoAreaOwnedByADeletedTeam_BothOrders(bool deletionFirst)
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var teamId = structure.Platform.Team.Id;
        var body = new { parentId = structure.RootId.ToString(), name = "Отчёты", ownerTeamId = teamId.ToString() };
        using var gate = factory.CommitGates.Open();

        Task<Answer> creation;
        Task<Answer> deletion;

        if (deletionFirst)
        {
            deletion = DeleteTeamAsync(structure, teamId, gate);
            await Task.WhenAny(gate.Reached, deletion);
            Assert.False(deletion.IsCompleted, "The deletion finished before reaching its commit gate.");

            creation = CreateAsync(structure, body);
            await PgLocks.WaitUntilBlockedAsync(factory.ConnectionString, creation);
            Assert.False(creation.IsCompleted, "The creation did not wait for the deletion's lock on the team.");
        }
        else
        {
            creation = CreateAsync(structure, body, gate);
            await Task.WhenAny(gate.Reached, creation);
            Assert.False(creation.IsCompleted, "The creation finished before reaching its commit gate.");

            deletion = DeleteTeamAsync(structure, teamId);
            await PgLocks.WaitUntilBlockedAsync(factory.ConnectionString, deletion);
            Assert.False(deletion.IsCompleted, "The deletion did not wait for the creation's lock on the team.");
        }

        gate.Release();
        var (created, deleted) = (await creation, await deletion);

        Assert.Equal(HttpStatusCode.NoContent, deleted.Status);

        var tree = await OrganizationsTestData.ReadTreeAsync(factory, structure.ProjectId);

        if (deletionFirst)
        {
            Assert.Equal(HttpStatusCode.NotFound, created.Status);
            Assert.Equal("organizations.team.not_found", created.ErrorCode);
            Assert.DoesNotContain(tree.ChildrenOf(structure.RootId), node => node.Name == "Отчёты");
        }
        else
        {
            Assert.Equal(HttpStatusCode.Created, created.Status);
            Assert.Equal(teamId, created.Body.GetProperty("ownerTeamId").GetGuid());
            Assert.Equal(structure.FirstTeamId, tree[created.Body.GetProperty("id").GetGuid()].OwnerTeamId);
        }

        Assert.DoesNotContain(tree.PreOrder(), node => node.OwnerTeamId == teamId || node.DefaultOfTeamId == teamId);
    }

    private Task<Answer> DeleteTeamAsync(OrganizationStructure structure, Guid teamId, CommitGate? gate = null) =>
        OrganizationsApi.SendAsync(
            factory,
            HttpMethod.Delete,
            $"{OrganizationsApi.Teams(structure.OrganizationId, structure.ProjectId)}/{teamId}",
            token: OrganizationsTokens.SignedIn(),
            gate: gate);

    private static IEnumerable<string> PropertyNames(JsonElement element) =>
        element.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal);
}
