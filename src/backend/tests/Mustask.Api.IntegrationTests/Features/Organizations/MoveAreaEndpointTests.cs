using System.Net;
using System.Text.Json;
using Mustask.Domain.Organizations;

namespace Mustask.Api.IntegrationTests.Features.Organizations;

/// <summary>
/// <c>PUT /api/organizations/{organizationId}/projects/{projectId}/areas/{areaId}/parent</c>
/// (api-contract § 4.4) through the real host and a real PostgreSQL. Every test
/// builds its own organization (<see cref="OrganizationsTestData"/>): the
/// database is shared and never emptied.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class MoveAreaEndpointTests(MustaskApiFactory factory)
{
    private Task<Answer> MoveAsync(
        Guid organizationId, string projectId, string areaId, object body, string? token = null, CommitGate? gate = null) =>
        OrganizationsApi.SendAsync(
            factory,
            HttpMethod.Put,
            $"{OrganizationsApi.Organization(organizationId)}/projects/{Uri.EscapeDataString(projectId)}/areas/{Uri.EscapeDataString(areaId)}/parent",
            body,
            token ?? OrganizationsTokens.SignedIn(),
            gate);

    private Task<Answer> MoveAsync(OrganizationStructure structure, Guid areaId, object body, CommitGate? gate = null) =>
        MoveAsync(structure.OrganizationId, structure.ProjectId.ToString(), areaId.ToString(), body, gate: gate);

    private Task<Answer> MoveAsync(OrganizationStructure structure, Guid areaId, Guid parentId, CommitGate? gate = null) =>
        MoveAsync(structure, areaId, new { parentId = parentId.ToString() }, gate);

    // US-5 · "A node moves with its whole subtree": «Отчёты» with «Квартальные» and «Годовые» is moved under «Архив»; the
    // tree shows «Архив / Отчёты / Квартальные» and «Архив / Отчёты / Годовые», and no «Отчёты» remains under the root.
    [Fact]
    public async Task Move_CarriesTheWholeSubtree_Returns200_TreeShowsThePaths()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var reports = await OrganizationsTestData.AddAreaAsync(factory, structure.ProjectId, structure.RootId, "Отчёты");
        var quarterly = await OrganizationsTestData.AddAreaAsync(factory, structure.ProjectId, reports.Id, "Квартальные");
        var yearly = await OrganizationsTestData.AddAreaAsync(factory, structure.ProjectId, reports.Id, "Годовые");
        var archive = await OrganizationsTestData.AddAreaAsync(factory, structure.ProjectId, structure.RootId, "Архив");

        var answer = await MoveAsync(structure, reports.Id, archive.Id);

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Equal("application/json", answer.MediaType);
        Assert.Equal(["id", "name", "parentId", "projectId"], PropertyNames(answer.Body));
        Assert.Equal(reports.Id, answer.Body.GetProperty("id").GetGuid());
        Assert.Equal(structure.ProjectId, answer.Body.GetProperty("projectId").GetGuid());
        Assert.Equal(archive.Id, answer.Body.GetProperty("parentId").GetGuid());
        Assert.Equal("Отчёты", answer.Body.GetProperty("name").GetString());

        var tree = await OrganizationsTestData.ReadTreeAsync(factory, structure.ProjectId);
        Assert.Equal("Архив / Отчёты / Квартальные", tree.PathOf(quarterly.Id));
        Assert.Equal("Архив / Отчёты / Годовые", tree.PathOf(yearly.Id));
        Assert.DoesNotContain(tree.ChildrenOf(structure.RootId), node => node.Id == reports.Id);
        Assert.Equal(5, tree.Count);
    }

    // US-5 · "A move needs a free name at its destination": «Архив» already has «Отчёты» (compared ignoring case,
    // BR-M1-12), and the root has another one; the root's is refused and the tree is unchanged.
    [Fact]
    public async Task Move_NameTakenAtDestination_Returns409AreaNameTaken_TreeUnchanged()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var reports = await OrganizationsTestData.AddAreaAsync(factory, structure.ProjectId, structure.RootId, "Отчёты");
        var archive = await OrganizationsTestData.AddAreaAsync(factory, structure.ProjectId, structure.RootId, "Архив");
        var held = await OrganizationsTestData.AddAreaAsync(factory, structure.ProjectId, archive.Id, "ОТЧЁТЫ");

        var answer = await MoveAsync(structure, reports.Id, archive.Id);

        Assert.Equal(HttpStatusCode.Conflict, answer.Status);
        Assert.Equal("organizations.area.name_taken", answer.ErrorCode);
        Assert.Equal("Область с таким названием уже есть на этом уровне", answer.Detail);

        var tree = await OrganizationsTestData.ReadTreeAsync(factory, structure.ProjectId);
        Assert.Equal(structure.RootId, tree[reports.Id].ParentId);
        Assert.Equal([held.Id], tree.ChildrenOf(archive.Id).Select(node => node.Id));
        Assert.Equal(4, tree.Count);
    }

    // US-5 · "A node is never moved under itself or its descendant": «Отчёты» under «Квартальные», under «Итоги» or under
    // itself is refused with «Нельзя переместить область внутрь неё самой или её вложенной области», and the tree is unchanged.
    [Theory]
    [InlineData("itself")]
    [InlineData("a child")]
    [InlineData("a grandchild")]
    public async Task Move_UnderItselfOrADescendant_Returns409MoveIntoOwnSubtree_TreeUnchanged(string how)
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var reports = await OrganizationsTestData.AddAreaAsync(factory, structure.ProjectId, structure.RootId, "Отчёты");
        var quarterly = await OrganizationsTestData.AddAreaAsync(factory, structure.ProjectId, reports.Id, "Квартальные");
        var totals = await OrganizationsTestData.AddAreaAsync(factory, structure.ProjectId, quarterly.Id, "Итоги");
        var parent = how switch
        {
            "itself" => reports,
            "a child" => quarterly,
            _ => totals,
        };

        var answer = await MoveAsync(structure, reports.Id, parent.Id);

        Assert.Equal(HttpStatusCode.Conflict, answer.Status);
        Assert.Equal("organizations.area.move_into_own_subtree", answer.ErrorCode);
        Assert.Equal("Нельзя переместить область внутрь неё самой или её вложенной области", answer.Detail);

        var tree = await OrganizationsTestData.ReadTreeAsync(factory, structure.ProjectId);
        Assert.Equal("Отчёты / Квартальные / Итоги", tree.PathOf(totals.Id));
        Assert.Equal(structure.RootId, tree[reports.Id].ParentId);
    }

    // US-5 · "A node never leaves its project" (BR-M1-22): a direct request that moves «Отчёты» of «Пример проекта» under an
    // area of «Второй проект» (of this organization or another), or under no area, is refused and both trees are unchanged.
    [Theory]
    [InlineData("null")]
    [InlineData("malformed")]
    [InlineData("unknown")]
    [InlineData("of another project")]
    [InlineData("of another organization")]
    public async Task Move_ParentNotAnAreaOfTheProject_Returns404AreaNotFound_BothTreesUnchanged(string how)
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var reports = await OrganizationsTestData.AddAreaAsync(factory, structure.ProjectId, structure.RootId, "Отчёты");
        var secondProject = await OrganizationsTestData.CreateProjectAsync(factory, structure.OrganizationId, "Второй проект", "SEC");
        var other = await OrganizationsTestData.CreateStructureAsync(factory);
        var parentId = how switch
        {
            "null" => null,
            "malformed" => "{" + structure.RootId + "}",
            "unknown" => Guid.CreateVersion7().ToString(),
            "of another project" => secondProject.Root.Id.ToString(),
            _ => other.RootId.ToString(),
        };

        var answer = await MoveAsync(structure, reports.Id, new { parentId });

        Assert.Equal(HttpStatusCode.NotFound, answer.Status);
        Assert.Equal("organizations.area.not_found", answer.ErrorCode);
        Assert.Equal("Область не найдена.", answer.Detail);
        var tree = await OrganizationsTestData.ReadTreeAsync(factory, structure.ProjectId);
        Assert.Equal(structure.RootId, tree[reports.Id].ParentId);
        Assert.Equal(2, tree.Count);
        Assert.Equal(1, (await OrganizationsTestData.ReadTreeAsync(factory, secondProject.Project.Id)).Count);
        Assert.Equal(1, (await OrganizationsTestData.ReadTreeAsync(factory, other.ProjectId)).Count);
    }

    // api-contract § 4.4: the body's field is absent, as for a missing reference.
    [Fact]
    public async Task Move_ParentIdAbsent_Returns404AreaNotFound()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var reports = await OrganizationsTestData.AddAreaAsync(factory, structure.ProjectId, structure.RootId, "Отчёты");

        var answer = await MoveAsync(structure, reports.Id, new { });

        Assert.Equal(HttpStatusCode.NotFound, answer.Status);
        Assert.Equal("organizations.area.not_found", answer.ErrorCode);
        Assert.Equal(structure.RootId, (await OrganizationsTestData.ReadTreeAsync(factory, structure.ProjectId))[reports.Id].ParentId);
    }

    // api-contract § 0.2: an id field that is not a JSON string fails binding with the platform's 400.
    [Fact]
    public async Task Move_ParentIdIsANumber_Returns400BadRequest()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var reports = await OrganizationsTestData.AddAreaAsync(factory, structure.ProjectId, structure.RootId, "Отчёты");

        var answer = await MoveAsync(structure, reports.Id, new { parentId = 42 });

        Assert.Equal(HttpStatusCode.BadRequest, answer.Status);
        Assert.Equal("http.bad_request", answer.ErrorCode);
    }

    // US-5 · "The root comes with the project and follows it": a direct request to move it is refused and changes nothing
    // (BR-M1-11). The rule about the addressed object answers before the body's reference is looked at (§ 0.4).
    [Theory]
    [InlineData("an area of the project")]
    [InlineData("an unknown area")]
    public async Task Move_Root_Returns409RootIsFixed_TreeUnchanged(string parent)
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var archive = await OrganizationsTestData.AddAreaAsync(factory, structure.ProjectId, structure.RootId, "Архив");
        var parentId = parent == "an unknown area" ? Guid.CreateVersion7() : archive.Id;

        var answer = await MoveAsync(structure, structure.RootId, parentId);

        Assert.Equal(HttpStatusCode.Conflict, answer.Status);
        Assert.Equal("organizations.area.root_is_fixed", answer.ErrorCode);
        Assert.Equal("Корневая область меняется только вместе с проектом", answer.Detail);
        var tree = await OrganizationsTestData.ReadTreeAsync(factory, structure.ProjectId);
        Assert.Null(tree.Root.ParentId);
        Assert.Equal(structure.RootId, tree[archive.Id].ParentId);
    }

    // api-contract § 4.4: "Moving under the current parent answers 200 and changes nothing."
    [Fact]
    public async Task Move_UnderTheCurrentParent_Returns200_ChangesNothing()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var reports = await OrganizationsTestData.AddAreaAsync(factory, structure.ProjectId, structure.RootId, "Отчёты");
        var quarterly = await OrganizationsTestData.AddAreaAsync(factory, structure.ProjectId, reports.Id, "Квартальные");

        var answer = await MoveAsync(structure, quarterly.Id, reports.Id);

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Equal(reports.Id, answer.Body.GetProperty("parentId").GetGuid());
        var tree = await OrganizationsTestData.ReadTreeAsync(factory, structure.ProjectId);
        Assert.Equal("Отчёты / Квартальные", tree.PathOf(quarterly.Id));
        Assert.Equal(3, tree.Count);
    }


    // US-1 · "A visitor who is not signed in sees nothing".
    [Fact]
    public async Task Move_NoToken_Returns401_ChangesNothing()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var reports = await OrganizationsTestData.AddAreaAsync(factory, structure.ProjectId, structure.RootId, "Отчёты");
        var archive = await OrganizationsTestData.AddAreaAsync(factory, structure.ProjectId, structure.RootId, "Архив");

        var answer = await OrganizationsApi.SendAsync(
            factory,
            HttpMethod.Put,
            $"{OrganizationsApi.Areas(structure.OrganizationId, structure.ProjectId)}/{reports.Id}/parent",
            new { parentId = archive.Id.ToString() });

        Assert.Equal(HttpStatusCode.Unauthorized, answer.Status);
        Assert.Equal(structure.RootId, (await OrganizationsTestData.ReadTreeAsync(factory, structure.ProjectId))[reports.Id].ParentId);
    }

    // US-3 · "A project id names no project of the organization" (BR-M1-20, BR-M1-22): malformed, unknown, or of another
    // organization, the project level answers before the area and the body are looked at.
    [Theory]
    [InlineData("malformed")]
    [InlineData("unknown")]
    [InlineData("of another organization")]
    public async Task Move_ProjectNotOfOrganization_Returns404ProjectNotFound(string how)
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var other = await OrganizationsTestData.CreateStructureAsync(factory);
        var reports = await OrganizationsTestData.AddAreaAsync(factory, other.ProjectId, other.RootId, "Отчёты");
        var projectId = how switch
        {
            "malformed" => "PRM",
            "unknown" => Guid.CreateVersion7().ToString(),
            _ => other.ProjectId.ToString(),
        };

        var answer = await MoveAsync(structure.OrganizationId, projectId, reports.Id.ToString(), new { parentId = "not-an-area" });

        Assert.Equal(HttpStatusCode.NotFound, answer.Status);
        Assert.Equal("organizations.project.not_found", answer.ErrorCode);
        Assert.Equal("Проект не найден.", answer.Detail);
        Assert.Equal(other.RootId, (await OrganizationsTestData.ReadTreeAsync(factory, other.ProjectId))[reports.Id].ParentId);
    }

    // US-5 · "A node never leaves its project" (BR-M1-22): an area id that is malformed, unknown, or of another project or
    // organization names no area of this project; the area level answers before the body is looked at.
    [Theory]
    [InlineData("malformed")]
    [InlineData("unknown")]
    [InlineData("of another project")]
    [InlineData("the root of another project")]
    [InlineData("of another organization")]
    public async Task Move_AreaNotOfProject_Returns404AreaNotFound_ChangesNothing(string how)
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var secondProject = await OrganizationsTestData.CreateProjectAsync(factory, structure.OrganizationId, "Второй проект", "SEC");
        var foreign = await OrganizationsTestData.AddAreaAsync(factory, secondProject.Project.Id, secondProject.Root.Id, "Чужая");
        var other = await OrganizationsTestData.CreateStructureAsync(factory);
        var otherArea = await OrganizationsTestData.AddAreaAsync(factory, other.ProjectId, other.RootId, "Платформа");
        var areaId = how switch
        {
            "malformed" => "not-an-area",
            "unknown" => Guid.CreateVersion7().ToString(),
            "of another project" => foreign.Id.ToString(),
            "the root of another project" => secondProject.Root.Id.ToString(),
            _ => otherArea.Id.ToString(),
        };

        var answer = await MoveAsync(structure.OrganizationId, structure.ProjectId.ToString(), areaId, new { parentId = structure.RootId.ToString() });

        Assert.Equal(HttpStatusCode.NotFound, answer.Status);
        Assert.Equal("organizations.area.not_found", answer.ErrorCode);
        Assert.Equal("Область не найдена.", answer.Detail);
        Assert.Equal(secondProject.Root.Id, (await OrganizationsTestData.ReadTreeAsync(factory, secondProject.Project.Id))[foreign.Id].ParentId);
        Assert.Equal(1, (await OrganizationsTestData.ReadTreeAsync(factory, structure.ProjectId)).Count);
        Assert.Equal(other.RootId, (await OrganizationsTestData.ReadTreeAsync(factory, other.ProjectId))[otherArea.Id].ParentId);
    }

    // US-3 · "A project is deactivated": a write under it answers project.not_found and changes nothing (ADR-0026 rule 5).
    [Fact]
    public async Task Move_UnderDeactivatedProject_Returns404AndChangesNothing()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var reports = await OrganizationsTestData.AddAreaAsync(factory, structure.ProjectId, structure.RootId, "Отчёты");
        var archive = await OrganizationsTestData.AddAreaAsync(factory, structure.ProjectId, structure.RootId, "Архив");
        await OrganizationsTestData.SetProjectStatusAsync(factory, structure.OrganizationId, structure.ProjectId, ProjectStatus.Deactivated);

        var answer = await MoveAsync(structure, reports.Id, archive.Id);

        Assert.Equal(HttpStatusCode.NotFound, answer.Status);
        Assert.Equal("organizations.project.not_found", answer.ErrorCode);
        Assert.Equal(structure.RootId, (await OrganizationsTestData.ReadTreeAsync(factory, structure.ProjectId))[reports.Id].ParentId);
    }

    // US-5 · "A node is never moved under itself or its descendant", the concurrent half (NFR Concurrency): «Отчёты» under
    // «Архив» and «Архив» under «Отчёты» at the same moment, in both orders. Request A is held at its commit with its row
    // written and the project row locked; B blocks on the project row (plan-backend § 8.2) until A commits, then reads the
    // tree A left and loses with the cycle's code. Reading the tree back builds an AreaTree, which refuses a stored cycle.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Move_TwoConcurrentFormingACycle_OneMoved_OtherRefused_BothOrders(bool reportsMovesFirst)
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var reports = await OrganizationsTestData.AddAreaAsync(factory, structure.ProjectId, structure.RootId, "Отчёты");
        var archive = await OrganizationsTestData.AddAreaAsync(factory, structure.ProjectId, structure.RootId, "Архив");
        var (firstArea, firstParent, secondArea, secondParent) = reportsMovesFirst
            ? (reports, archive, archive, reports)
            : (archive, reports, reports, archive);
        using var gate = factory.CommitGates.Open();

        var first = MoveAsync(structure, firstArea.Id, firstParent.Id, gate);
        await Task.WhenAny(gate.Reached, first);
        Assert.False(first.IsCompleted, "The first move finished before reaching its commit gate.");

        var second = MoveAsync(structure, secondArea.Id, secondParent.Id);
        await PgLocks.WaitUntilBlockedAsync(factory.ConnectionString, second);
        Assert.False(second.IsCompleted, "The second move did not wait for the first one's project lock.");

        gate.Release();
        var (a, b) = (await first, await second);

        Assert.Equal(HttpStatusCode.OK, a.Status);
        Assert.Equal(HttpStatusCode.Conflict, b.Status);
        Assert.Equal("organizations.area.move_into_own_subtree", b.ErrorCode);

        var tree = await OrganizationsTestData.ReadTreeAsync(factory, structure.ProjectId);
        Assert.Equal(firstParent.Id, tree[firstArea.Id].ParentId);
        Assert.Equal(structure.RootId, tree[secondArea.Id].ParentId);
    }

    // NFR Concurrency, BR-M1-12 "sibling names ... at a move's destination too" (QA pass 1): the root's «Отчёты» is moved
    // under «Архив» at the same moment a child «Отчёты» is created under «Архив», in both orders. Each passes its pre-check
    // alone (the other is not committed); the sibling index decides, and the loser answers area.name_taken whichever of
    // the two it is. «Архив» ends with exactly one «Отчёты».
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Move_ConcurrentWithACreationOfItsNameAtTheDestination_OneRefused_BothOrders(bool moveFirst)
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var reports = await OrganizationsTestData.AddAreaAsync(factory, structure.ProjectId, structure.RootId, "Отчёты");
        var archive = await OrganizationsTestData.AddAreaAsync(factory, structure.ProjectId, structure.RootId, "Архив");
        var creationBody = new { parentId = archive.Id.ToString(), name = "Отчёты" };
        using var gate = factory.CommitGates.Open();

        Task<Answer> move;
        Task<Answer> creation;

        if (moveFirst)
        {
            move = MoveAsync(structure, reports.Id, archive.Id, gate);
            await Task.WhenAny(gate.Reached, move);
            Assert.False(move.IsCompleted, "The move finished before reaching its commit gate.");

            creation = CreateAreaAsync(structure, creationBody);
            await PgLocks.WaitUntilBlockedAsync(factory.ConnectionString, creation);
            Assert.False(creation.IsCompleted, "The creation did not wait for the move's uncommitted row.");
        }
        else
        {
            creation = CreateAreaAsync(structure, creationBody, gate);
            await Task.WhenAny(gate.Reached, creation);
            Assert.False(creation.IsCompleted, "The creation finished before reaching its commit gate.");

            move = MoveAsync(structure, reports.Id, archive.Id);
            await PgLocks.WaitUntilBlockedAsync(factory.ConnectionString, move);
            Assert.False(move.IsCompleted, "The move did not wait for the creation's uncommitted row.");
        }

        gate.Release();
        var (moved, created) = (await move, await creation);

        var (winner, loser) = moveFirst ? (moved, created) : (created, moved);
        Assert.True(winner.Status is HttpStatusCode.OK or HttpStatusCode.Created, $"The winner answered {winner.Status}.");
        Assert.Equal(HttpStatusCode.Conflict, loser.Status);
        Assert.Equal("organizations.area.name_taken", loser.ErrorCode);

        var tree = await OrganizationsTestData.ReadTreeAsync(factory, structure.ProjectId);
        var underArchive = Assert.Single(tree.ChildrenOf(archive.Id), node => node.Name == "Отчёты");
        Assert.Equal(moveFirst ? reports.Id : created.Body.GetProperty("id").GetGuid(), underArchive.Id);
        Assert.Equal(moveFirst ? archive.Id : structure.RootId, tree[reports.Id].ParentId);
    }

    private Task<Answer> CreateAreaAsync(OrganizationStructure structure, object body, CommitGate? gate = null) =>
        OrganizationsApi.SendAsync(
            factory,
            HttpMethod.Post,
            OrganizationsApi.Areas(structure.OrganizationId, structure.ProjectId),
            body,
            OrganizationsTokens.SignedIn(),
            gate);

    private static IEnumerable<string> PropertyNames(JsonElement element) =>
        element.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal);
}
