using System.Net;
using System.Text.Json;
using Mustask.Domain.Organizations;

namespace Mustask.Api.IntegrationTests.Features.Organizations;

/// <summary>
/// <c>DELETE /api/organizations/{organizationId}/projects/{projectId}/areas/{areaId}</c>
/// (api-contract § 4.6) through the real host and a real PostgreSQL. Every test
/// builds its own organization (<see cref="OrganizationsTestData"/>): the
/// database is shared and never emptied. The races of § 8.1 run on
/// <see cref="CommitGate"/> and <see cref="PgLocks"/> (plan-backend § 8.3), in
/// both orders. The move race drives the real <c>PUT …/areas/{areaId}/parent</c>,
/// so it needs the <c>MoveArea</c> lane's handler (L-23) to be in the build.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class DeleteAreaEndpointTests(MustaskApiFactory factory)
{
    private Task<Answer> DeleteAsync(
        Guid organizationId, string projectId, string areaId, string? token = null, CommitGate? gate = null) =>
        OrganizationsApi.SendAsync(
            factory,
            HttpMethod.Delete,
            $"/api/organizations/{organizationId}/projects/{Uri.EscapeDataString(projectId)}/areas/{Uri.EscapeDataString(areaId)}",
            token: token ?? OrganizationsTokens.SignedIn(),
            gate: gate);

    private Task<Answer> DeleteAsync(OrganizationStructure structure, Guid areaId, CommitGate? gate = null) =>
        DeleteAsync(structure.OrganizationId, structure.ProjectId.ToString(), areaId.ToString(), gate: gate);

    private Task<Answer> CreateUnderAsync(OrganizationStructure structure, Guid parentId, string name, CommitGate? gate = null) =>
        OrganizationsApi.SendAsync(
            factory,
            HttpMethod.Post,
            OrganizationsApi.Areas(structure.OrganizationId, structure.ProjectId),
            new { parentId = parentId.ToString(), name },
            OrganizationsTokens.SignedIn(),
            gate);

    private Task<Answer> MoveUnderAsync(OrganizationStructure structure, Guid areaId, Guid parentId, CommitGate? gate = null) =>
        OrganizationsApi.SendAsync(
            factory,
            HttpMethod.Put,
            $"{OrganizationsApi.Areas(structure.OrganizationId, structure.ProjectId)}/{areaId}/parent",
            new { parentId = parentId.ToString() },
            OrganizationsTokens.SignedIn(),
            gate);

    // US-5 · "Only an empty leaf is deleted": «Квартальные» has no children and is no team's default; it disappears, its
    // sibling and the other areas stay, and no action restores it.
    [Fact]
    public async Task Delete_Leaf_Returns204_Gone_NeighboursStay_NoRestore()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var reports = await OrganizationsTestData.AddAreaAsync(factory, structure.ProjectId, structure.RootId, "Отчёты");
        var quarterly = await OrganizationsTestData.AddAreaAsync(factory, structure.ProjectId, reports.Id, "Квартальные");
        await OrganizationsTestData.AddAreaAsync(factory, structure.ProjectId, reports.Id, "Годовые");

        var answer = await DeleteAsync(structure, quarterly.Id);

        Assert.Equal(HttpStatusCode.NoContent, answer.Status);
        Assert.Equal(JsonValueKind.Undefined, answer.Body.ValueKind);

        var tree = await OrganizationsTestData.ReadTreeAsync(factory, structure.ProjectId);
        Assert.False(tree.Contains(quarterly.Id));
        Assert.Equal(["Годовые"], tree.ChildrenOf(reports.Id).Select(node => node.Name));
        Assert.True(tree.Contains(structure.Platform.Area.Id));
        Assert.Equal(4, tree.Count);

        var again = await DeleteAsync(structure, quarterly.Id);
        Assert.Equal(HttpStatusCode.NotFound, again.Status);
        Assert.Equal("organizations.area.not_found", again.ErrorCode);
    }

    // US-5 · "Only an empty leaf is deleted": «Отчёты» is refused with the story's text while «Квартальные» is under it,
    // and is deleted once «Квартальные» is gone.
    [Fact]
    public async Task Delete_NonLeaf_Returns409NotEmpty_ChildrenStay_DeletableOnceEmpty()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var reports = await OrganizationsTestData.AddAreaAsync(factory, structure.ProjectId, structure.RootId, "Отчёты");
        var quarterly = await OrganizationsTestData.AddAreaAsync(factory, structure.ProjectId, reports.Id, "Квартальные");

        var refused = await DeleteAsync(structure, reports.Id);

        Assert.Equal(HttpStatusCode.Conflict, refused.Status);
        Assert.Equal("organizations.area.not_empty", refused.ErrorCode);
        Assert.Equal(
            "Нельзя удалить область, в которой есть рабочие элементы (включая удалённые) или вложенные области",
            refused.Detail);
        Assert.Equal(4, (await OrganizationsTestData.ReadTreeAsync(factory, structure.ProjectId)).Count);

        Assert.Equal(HttpStatusCode.NoContent, (await DeleteAsync(structure, quarterly.Id)).Status);
        Assert.Equal(HttpStatusCode.NoContent, (await DeleteAsync(structure, reports.Id)).Status);

        var tree = await OrganizationsTestData.ReadTreeAsync(factory, structure.ProjectId);
        Assert.False(tree.Contains(reports.Id));
        Assert.Equal(2, tree.Count);
    }

    // US-5 · "The root comes with the project" and BR-M1-11: a delete requested directly is refused and changes nothing,
    // although the root has a child and is the first team's default area: the rule about the addressed area answers first
    // (api-contract § 0.4 step 2), and the root stays owned by the first team.
    [Fact]
    public async Task Delete_Root_Returns409RootIsFixed_RootStaysOwnedByTheFirstTeam()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);

        var answer = await DeleteAsync(structure, structure.RootId);

        Assert.Equal(HttpStatusCode.Conflict, answer.Status);
        Assert.Equal("organizations.area.root_is_fixed", answer.ErrorCode);
        Assert.Equal("Корневая область меняется только вместе с проектом", answer.Detail);

        var tree = await OrganizationsTestData.ReadTreeAsync(factory, structure.ProjectId);
        Assert.Equal(2, tree.Count);
        Assert.Equal(new ResolvedOwner(structure.FirstTeamId, false), tree.ResolveOwner(structure.RootId));
    }

    // US-6 · "A team's default area stays with its team": the leaf «Платформа» is the default area of the team «Платформа»;
    // the refusal names the team, in the text and in its parameter, and the area stays.
    [Fact]
    public async Task Delete_DefaultArea_Returns409IsTeamDefault_NamingTheTeam()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);

        var answer = await DeleteAsync(structure, structure.Platform.Area.Id);

        Assert.Equal(HttpStatusCode.Conflict, answer.Status);
        Assert.Equal("organizations.area.is_team_default", answer.ErrorCode);
        Assert.Equal("Область используется как область по умолчанию команды «Платформа»", answer.Detail);
        var team = answer.Body.GetProperty("params").GetProperty("team");
        Assert.Equal("Платформа", team.GetProperty("value").GetString());
        Assert.Equal("string", team.GetProperty("type").GetString());
        Assert.True((await OrganizationsTestData.ReadTreeAsync(factory, structure.ProjectId)).Contains(structure.Platform.Area.Id));
    }

    // US-6 · "once the team's default is changed to «Отчёты», the same actions are accepted": the former default is deleted,
    // and the new default stays the team's.
    [Fact]
    public async Task Delete_FormerDefaultArea_AfterTheDefaultMoved_Returns204()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var reports = await OrganizationsTestData.AddAreaAsync(
            factory, structure.ProjectId, structure.RootId, "Отчёты", structure.Platform.Team.Id);
        Assert.True(await OrganizationsTestData.SetTeamDefaultAreaAsync(factory, structure.ProjectId, structure.Platform.Team.Id, reports.Id));

        var answer = await DeleteAsync(structure, structure.Platform.Area.Id);

        Assert.Equal(HttpStatusCode.NoContent, answer.Status);

        var tree = await OrganizationsTestData.ReadTreeAsync(factory, structure.ProjectId);
        Assert.False(tree.Contains(structure.Platform.Area.Id));
        Assert.Equal(structure.Platform.Team.Id, tree[reports.Id].DefaultOfTeamId);
    }

    // US-1 · "A visitor who is not signed in sees nothing": the challenge comes before every other answer, and nothing is
    // deleted.
    [Fact]
    public async Task Delete_NoToken_Returns401_DeletesNothing()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var leaf = await OrganizationsTestData.AddAreaAsync(factory, structure.ProjectId, structure.RootId, "Отчёты");

        var answer = await OrganizationsApi.SendAsync(
            factory,
            HttpMethod.Delete,
            $"{OrganizationsApi.Areas(structure.OrganizationId, structure.ProjectId)}/{leaf.Id}");

        Assert.Equal(HttpStatusCode.Unauthorized, answer.Status);
        Assert.True((await OrganizationsTestData.ReadTreeAsync(factory, structure.ProjectId)).Contains(leaf.Id));
    }

    // US-3 · "A project id names no project of the organization" (BR-M1-20, BR-M1-22): malformed, unknown, or of another
    // organization, the project level answers before the area is looked at, and the area of the other project stays.
    [Theory]
    [InlineData("malformed")]
    [InlineData("unknown")]
    [InlineData("of another organization")]
    public async Task Delete_ProjectNotOfOrganization_Returns404ProjectNotFound(string how)
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var other = await OrganizationsTestData.CreateStructureAsync(factory);
        var leaf = await OrganizationsTestData.AddAreaAsync(factory, other.ProjectId, other.RootId, "Отчёты");
        var projectId = how switch
        {
            "malformed" => "PRM",
            "unknown" => Guid.CreateVersion7().ToString(),
            _ => other.ProjectId.ToString(),
        };

        var answer = await DeleteAsync(structure.OrganizationId, projectId, leaf.Id.ToString());

        Assert.Equal(HttpStatusCode.NotFound, answer.Status);
        Assert.Equal("organizations.project.not_found", answer.ErrorCode);
        Assert.Equal("Проект не найден.", answer.Detail);
        Assert.True((await OrganizationsTestData.ReadTreeAsync(factory, other.ProjectId)).Contains(leaf.Id));
    }

    // US-3 · "A project is deactivated": a write under it answers project.not_found and changes nothing (ADR-0026 rule 5).
    [Fact]
    public async Task Delete_UnderDeactivatedProject_Returns404AndChangesNothing()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var leaf = await OrganizationsTestData.AddAreaAsync(factory, structure.ProjectId, structure.RootId, "Отчёты");
        await OrganizationsTestData.SetProjectStatusAsync(factory, structure.OrganizationId, structure.ProjectId, ProjectStatus.Deactivated);

        var answer = await DeleteAsync(structure, leaf.Id);

        Assert.Equal(HttpStatusCode.NotFound, answer.Status);
        Assert.Equal("organizations.project.not_found", answer.ErrorCode);
        Assert.Equal(3, (await OrganizationsTestData.ReadTreeAsync(factory, structure.ProjectId)).Count);
    }

    // US-5 · "A node never leaves its project" (BR-M1-22) read for the addressed area: malformed, unknown, an area of
    // another project of the organization, or of another organization, answers area.not_found, and both trees are unchanged.
    [Theory]
    [InlineData("malformed")]
    [InlineData("unknown")]
    [InlineData("of another project")]
    [InlineData("of another organization")]
    public async Task Delete_AreaNotOfProject_Returns404AreaNotFound_BothTreesUnchanged(string how)
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var leaf = await OrganizationsTestData.AddAreaAsync(factory, structure.ProjectId, structure.RootId, "Отчёты");
        var secondProject = await OrganizationsTestData.CreateProjectAsync(factory, structure.OrganizationId, "Второй проект", "SEC");
        var foreignInProject = await OrganizationsTestData.AddAreaAsync(factory, secondProject.Project.Id, secondProject.Root.Id, "Чужая");
        var other = await OrganizationsTestData.CreateStructureAsync(factory);
        var foreignInOrganization = await OrganizationsTestData.AddAreaAsync(factory, other.ProjectId, other.RootId, "Чужая");
        var areaId = how switch
        {
            "malformed" => "{" + leaf.Id + "}",
            "unknown" => Guid.CreateVersion7().ToString(),
            "of another project" => foreignInProject.Id.ToString(),
            _ => foreignInOrganization.Id.ToString(),
        };

        var answer = await DeleteAsync(structure.OrganizationId, structure.ProjectId.ToString(), areaId);

        Assert.Equal(HttpStatusCode.NotFound, answer.Status);
        Assert.Equal("organizations.area.not_found", answer.ErrorCode);
        Assert.Equal("Область не найдена.", answer.Detail);
        Assert.Equal(3, (await OrganizationsTestData.ReadTreeAsync(factory, structure.ProjectId)).Count);
        Assert.Equal(2, (await OrganizationsTestData.ReadTreeAsync(factory, secondProject.Project.Id)).Count);
        Assert.Equal(3, (await OrganizationsTestData.ReadTreeAsync(factory, other.ProjectId)).Count);
    }

    // US-5 · "A leaf deleted while a child is put under it" (BR-M1-13, NFR Concurrency), both orders. The request that goes
    // first is held at its commit with its lock on the leaf: the deletion holds it FOR UPDATE, the creation FOR SHARE as the
    // parent. The other request waits on that lock, then re-reads the leaf as the first left it. Deletion first: the
    // creation finds no parent (area.not_found) and nothing is created. Creation first: the deletion sees the child
    // (area.not_empty) and the leaf stays. Either way no area is left under a deleted parent.
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Delete_ConcurrentWithCreateUnderIt_OneRefused_NoOrphan_BothOrders(bool deletionFirst)
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var leaf = await OrganizationsTestData.AddAreaAsync(factory, structure.ProjectId, structure.RootId, "Квартальные");
        Func<CommitGate?, Task<Answer>> delete = gate => DeleteAsync(structure, leaf.Id, gate);
        Func<CommitGate?, Task<Answer>> create = gate => CreateUnderAsync(structure, leaf.Id, "Годовые", gate);

        var (deleted, created) = deletionFirst
            ? await RaceAsync(delete, create)
            : DeletionAnswerFirst(await RaceAsync(create, delete));

        var tree = await OrganizationsTestData.ReadTreeAsync(factory, structure.ProjectId);
        Assert.All(tree.PreOrder(), node => Assert.True(node.ParentId is null || tree.Contains(node.ParentId.Value)));

        if (deletionFirst)
        {
            Assert.Equal(HttpStatusCode.NoContent, deleted.Status);
            Assert.Equal(HttpStatusCode.NotFound, created.Status);
            Assert.Equal("organizations.area.not_found", created.ErrorCode);
            Assert.False(tree.Contains(leaf.Id));
            Assert.DoesNotContain(tree.PreOrder(), node => node.Name == "Годовые");
        }
        else
        {
            Assert.Equal(HttpStatusCode.Created, created.Status);
            Assert.Equal(HttpStatusCode.Conflict, deleted.Status);
            Assert.Equal("organizations.area.not_empty", deleted.ErrorCode);
            Assert.Equal(["Годовые"], tree.ChildrenOf(leaf.Id).Select(node => node.Name));
        }
    }

    // US-5 · "A leaf deleted while a child is put under it" by a move: «Квартальные» is deleted at the same moment as
    // «Годовые» is moved under it, both orders. The move locks its project first, then the moved area FOR UPDATE and the
    // new parent FOR SHARE (§ 8.2). Deletion first: the move finds no parent (area.not_found) and «Годовые» stays under
    // «Отчёты». Move first: the deletion sees «Годовые» under the leaf (area.not_empty).
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Delete_ConcurrentWithMoveUnderIt_OneRefused_BothOrders(bool deletionFirst)
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var reports = await OrganizationsTestData.AddAreaAsync(factory, structure.ProjectId, structure.RootId, "Отчёты");
        var quarterly = await OrganizationsTestData.AddAreaAsync(factory, structure.ProjectId, reports.Id, "Квартальные");
        var annual = await OrganizationsTestData.AddAreaAsync(factory, structure.ProjectId, reports.Id, "Годовые");
        Func<CommitGate?, Task<Answer>> delete = gate => DeleteAsync(structure, quarterly.Id, gate);
        Func<CommitGate?, Task<Answer>> move = gate => MoveUnderAsync(structure, annual.Id, quarterly.Id, gate);

        var (deleted, moved) = deletionFirst
            ? await RaceAsync(delete, move)
            : DeletionAnswerFirst(await RaceAsync(move, delete));

        var tree = await OrganizationsTestData.ReadTreeAsync(factory, structure.ProjectId);
        Assert.All(tree.PreOrder(), node => Assert.True(node.ParentId is null || tree.Contains(node.ParentId.Value)));

        if (deletionFirst)
        {
            Assert.Equal(HttpStatusCode.NoContent, deleted.Status);
            Assert.Equal(HttpStatusCode.NotFound, moved.Status);
            Assert.Equal("organizations.area.not_found", moved.ErrorCode);
            Assert.False(tree.Contains(quarterly.Id));
            Assert.Equal(reports.Id, tree[annual.Id].ParentId);
        }
        else
        {
            Assert.Equal(HttpStatusCode.OK, moved.Status);
            Assert.Equal(HttpStatusCode.Conflict, deleted.Status);
            Assert.Equal("organizations.area.not_empty", deleted.ErrorCode);
            Assert.Equal(quarterly.Id, tree[annual.Id].ParentId);
            Assert.True(tree.Contains(quarterly.Id));
        }
    }

    /// <summary>
    /// Request A is held at its commit with its locks taken, B is started once A is held and waits on a lock of A's (or
    /// ends at once when it does not conflict, which the assertion refuses), then A is released (plan-backend § 8.3).
    /// </summary>
    private async Task<(Answer First, Answer Second)> RaceAsync(
        Func<CommitGate?, Task<Answer>> first, Func<CommitGate?, Task<Answer>> second)
    {
        using var gate = factory.CommitGates.Open();

        var holder = first(gate);
        await Task.WhenAny(gate.Reached, holder);
        Assert.False(holder.IsCompleted, "The first request finished before reaching its commit gate.");

        var waiter = second(null);
        await PgLocks.WaitUntilBlockedAsync(factory.ConnectionString, waiter);
        Assert.False(waiter.IsCompleted, "The second request did not wait on the first one's lock on the leaf.");

        gate.Release();

        return (await holder, await waiter);
    }

    /// <summary>The deletion's answer first, whichever request went first.</summary>
    private static (Answer Deletion, Answer Other) DeletionAnswerFirst((Answer First, Answer Second) answers) =>
        (answers.Second, answers.First);
}
