using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Mustask.Application.Abstractions.Organizations;
using Mustask.Domain.Organizations;

namespace Mustask.Api.IntegrationTests.Features.Organizations;

/// <summary>
/// <c>GET /api/organizations/{organizationId}/projects/{projectId}/areas</c>
/// (mod-017 api-contract § 1.6) through the real host and a real PostgreSQL: the
/// project's area tree in pre-order with paths and depths — a node is its place, its name, its path and its depth, and
/// nothing about a team (BR-OT-10). Every test builds its own
/// organization (<see cref="OrganizationsTestData"/>): the database is shared
/// and never emptied.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class GetAreaTreeEndpointTests(MustaskApiFactory factory)
{
    private const string RootName = "Пример проекта";

    /// <summary>One node of the answer, read back into plain values.</summary>
    private sealed record NodeView(Guid Id, Guid? ParentId, string Name, string Path, int Depth);

    /// <summary>
    /// The tree most tests read: a project «Пример проекта», a second team «Платформа» (which owns no area), and
    /// <code>
    /// Пример проекта      root
    ///   архив
    ///   Отчёты
    ///     Годовые
    ///     Квартальные
    ///       Март
    /// </code>
    /// </summary>
    private sealed record SampleTree(OrganizationStructure Structure, Area Reports, Area Quarterly, Area March, Area Yearly, Area Archive);

    private async Task<SampleTree> CreateSampleTreeAsync()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var reports = await OrganizationsTestData.AddAreaAsync(factory, structure.ProjectId, structure.RootId, "Отчёты");
        var quarterly = await OrganizationsTestData.AddAreaAsync(factory, structure.ProjectId, reports.Id, "Квартальные");
        var march = await OrganizationsTestData.AddAreaAsync(factory, structure.ProjectId, quarterly.Id, "Март");
        var yearly = await OrganizationsTestData.AddAreaAsync(factory, structure.ProjectId, reports.Id, "Годовые");
        var archive = await OrganizationsTestData.AddAreaAsync(factory, structure.ProjectId, structure.RootId, "архив");

        return new SampleTree(structure, reports, quarterly, march, yearly, archive);
    }

    private Task<Answer> GetAsync(Guid organizationId, string projectId, string? token = null) =>
        OrganizationsApi.SendAsync(
            factory,
            HttpMethod.Get,
            $"/api/organizations/{organizationId}/projects/{Uri.EscapeDataString(projectId)}/areas",
            token: token ?? OrganizationsTokens.SignedIn());

    private Task<Answer> GetAsync(OrganizationStructure structure) =>
        GetAsync(structure.OrganizationId, structure.ProjectId.ToString());

    private static List<NodeView> Nodes(Answer answer)
    {
        Assert.Equal(JsonValueKind.Array, answer.Body.ValueKind);

        return answer.Body.EnumerateArray()
            .Select(node => new NodeView(
                node.GetProperty("id").GetGuid(),
                NullableGuid(node.GetProperty("parentId")),
                node.GetProperty("name").GetString()!,
                node.GetProperty("path").GetString()!,
                node.GetProperty("depth").GetInt32()))
            .ToList();
    }

    private static Guid? NullableGuid(JsonElement element) =>
        element.ValueKind == JsonValueKind.Null ? null : element.GetGuid();

    private static IEnumerable<string> PropertyNames(JsonElement element) =>
        element.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal);

    // US-5 · "tree registry": the whole tree in pre-order (each node before its children, siblings in display order, the root
    // first), each node with its path (DC-9) and its depth. Rewritten without the owners (D-16, BR-OT-10): the test keeps its
    // purpose, and its name is Get_ReturnsPreOrderNodes_WithPathsAndDepths.
    [Fact]
    public async Task Get_ReturnsPreOrderNodes_WithPathsAndDepths()
    {
        var tree = await CreateSampleTreeAsync();
        var structure = tree.Structure;

        var answer = await GetAsync(structure);

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Equal("application/json", answer.MediaType);
        Assert.Equal(
            [
                new NodeView(structure.RootId, null, RootName, RootName, 0),
                new NodeView(tree.Archive.Id, structure.RootId, "архив", "архив", 1),
                new NodeView(tree.Reports.Id, structure.RootId, "Отчёты", "Отчёты", 1),
                new NodeView(tree.Yearly.Id, tree.Reports.Id, "Годовые", "Отчёты / Годовые", 2),
                new NodeView(tree.Quarterly.Id, tree.Reports.Id, "Квартальные", "Отчёты / Квартальные", 2),
                new NodeView(tree.March.Id, tree.Quarterly.Id, "Март", "Отчёты / Квартальные / Март", 3),
            ],
            Nodes(answer));
    }

    // mod-017 api-contract § 1.6: the exact shape of a node — `ownerTeamId`, `resolvedOwner`, `ownerInherited` and
    // `defaultOfTeam` are removed (BR-OT-10). Rewritten: Get_NodeShape_IsExactlyTheContracts keeps its name.
    [Fact]
    public async Task Get_NodeShape_IsExactlyTheContracts()
    {
        var tree = await CreateSampleTreeAsync();

        var answer = await GetAsync(tree.Structure);

        var root = answer.Body.EnumerateArray().First();
        Assert.Equal(["depth", "id", "name", "parentId", "path"], PropertyNames(root));
        Assert.Equal(JsonValueKind.Null, root.GetProperty("parentId").ValueKind);
    }

    // US-5 · "root comes with the project" and "root only": a new project's tree is its root alone, named after the project.
    [Fact]
    public async Task Get_NewProject_IsTheRootAlone()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var project = await OrganizationsTestData.CreateProjectAsync(factory, organization.Id);

        var answer = await GetAsync(organization.Id, project.Project.Id.ToString());

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Equal([new NodeView(project.Root.Id, null, RootName, RootName, 0)], Nodes(answer));
    }

    // US-8 · "lists in order" applied to the tree (design § 4.6): siblings in Russian collation, ignoring letter case, «Ё»
    // with «Е».
    [Fact]
    public async Task Get_SiblingsInDisplayOrder_RussianCollation_IgnoringCaseAndYo()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        foreach (var name in new[] { "Ель", "Ёлка", "Бета", "альфа" })
        {
            await OrganizationsTestData.AddAreaAsync(factory, structure.ProjectId, structure.RootId, name);
        }

        var nodes = Nodes(await GetAsync(structure));

        var siblings = nodes.Where(node => node.ParentId == structure.RootId).Select(node => node.Name).ToList();
        Assert.Equal(["альфа", "Бета", "Ёлка", "Ель"], siblings);
    }

    // mod-017 US-7 · "A team deleted changes no area" (BR-OT-10, BR-OT-12): the tree is the same after the deletion of a team —
    // there is no area to hand over and none loses a mark. Replaces Get_AfterTeamDeleted_ItsAreasBelongToTheFirstTeam_NoDefaultOfIt.
    [Fact]
    public async Task Get_AfterTeamDeleted_TheTreeIsUnchanged()
    {
        var tree = await CreateSampleTreeAsync();
        var structure = tree.Structure;
        var before = Nodes(await GetAsync(structure));

        await OrganizationsTestData.DeleteTeamAsync(factory, structure.ProjectId, structure.Platform.Id);

        Assert.Equal(before, Nodes(await GetAsync(structure)));
    }

    // US-6 · "other modules get the same answer as the tree": the root the seam gives for a project is the tree's first node. The
    // in-process owner read of mod-002 (`FindAreaOwnerAsync`) is gone with the owner (mod-017 BR-OT-10, api-contract § 7).
    [Fact]
    public async Task Seam_ProjectRootArea_IsTheTreesRoot()
    {
        var tree = await CreateSampleTreeAsync();
        var structure = tree.Structure;
        var nodes = Nodes(await GetAsync(structure));
        using var scope = factory.Services.CreateScope();
        var seam = scope.ServiceProvider.GetRequiredService<Mustask.Application.Features.Organizations.IOrganizationStructure>();

        var project = await seam.FindProjectAsync(structure.OrganizationId, structure.ProjectId, CancellationToken.None);

        Assert.NotNull(project);
        Assert.Equal(nodes[0].Id, project.RootAreaId);
        Assert.Null(nodes[0].ParentId);
        Assert.Single(nodes, node => node.ParentId is null);
    }

    // US-1 · "A visitor who is not signed in sees nothing": the challenge comes before any other answer.
    [Fact]
    public async Task Get_NoToken_Returns401()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);

        var answer = await OrganizationsApi.SendAsync(
            factory, HttpMethod.Get, OrganizationsApi.Areas(structure.OrganizationId, structure.ProjectId));

        Assert.Equal(HttpStatusCode.Unauthorized, answer.Status);
        Assert.Equal("http.unauthorized", answer.ErrorCode);
    }

    // US-3 · "A project id names no project of the organization" (BR-M1-20, BR-M1-22): malformed, unknown, or the project of
    // another organization.
    [Theory]
    [InlineData("malformed")]
    [InlineData("unknown")]
    [InlineData("of another organization")]
    public async Task Get_ProjectNotOfOrganization_Returns404ProjectNotFound(string how)
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var other = await OrganizationsTestData.CreateStructureAsync(factory);
        var projectId = how switch
        {
            "malformed" => "PRM",
            "unknown" => Guid.CreateVersion7().ToString(),
            _ => other.ProjectId.ToString(),
        };

        var answer = await GetAsync(structure.OrganizationId, projectId);

        Assert.Equal(HttpStatusCode.NotFound, answer.Status);
        Assert.Equal("organizations.project.not_found", answer.ErrorCode);
        Assert.Equal("Проект не найден.", answer.Detail);
    }

    // US-3 · "A project is deactivated": reads beneath it answer project.not_found (ADR-0026 rule 5), and the restored project
    // gives its tree back as it was.
    [Fact]
    public async Task Get_UnderDeactivatedProject_Returns404ProjectNotFound_RestoredGivesTheTreeBack()
    {
        var tree = await CreateSampleTreeAsync();
        var structure = tree.Structure;
        var before = Nodes(await GetAsync(structure));
        await OrganizationsTestData.SetProjectStatusAsync(factory, structure.OrganizationId, structure.ProjectId, ProjectStatus.Deactivated);

        var deactivated = await GetAsync(structure);
        await OrganizationsTestData.SetProjectStatusAsync(factory, structure.OrganizationId, structure.ProjectId, ProjectStatus.Active);
        var restored = await GetAsync(structure);

        Assert.Equal(HttpStatusCode.NotFound, deactivated.Status);
        Assert.Equal("organizations.project.not_found", deactivated.ErrorCode);
        Assert.Equal(HttpStatusCode.OK, restored.Status);
        Assert.Equal(before, Nodes(restored));
    }

    // US-2 · "A deactivated organization takes no change; unknown answers not found" (ADR-0026 rule 5): the organization
    // level answers first, whatever the project's own status.
    [Fact]
    public async Task Get_UnderDeactivatedOrganization_Returns404OrganizationNotFound()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        await OrganizationsTestData.SetOrganizationStatusAsync(factory, structure.OrganizationId, OrganizationStatus.Deactivated);

        var answer = await GetAsync(structure);

        Assert.Equal(HttpStatusCode.NotFound, answer.Status);
        Assert.Equal("organizations.organization.not_found", answer.ErrorCode);
        Assert.Equal("Организация не найдена.", answer.Detail);
    }

    // mod-016 US-2 · the tree needs no token-carried right, only the membership: the caller is the creator, an administrator,
    // and organizations.area.read comes from the membership (a member and a viewer read it too, a stranger gets 404 — the
    // inventory and ScopedAccessTests call every role).
    [Fact]
    public async Task Get_ATokenWithNoRight_ReadsTheTreeOnTheMembership()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);

        var answer = await GetAsync(structure.OrganizationId, structure.ProjectId.ToString(), OrganizationsTokens.SignedIn());

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Single(Nodes(answer));
    }

    // NFR Volumes (QA pass 1, A-18): an area tree of 500 nodes and 10 levels returns over HTTP within 2 s, every node with its
    // path and depth. The read model's query count is pinned by ReadModelsTests; this is the end-to-end bound the NFR names.
    // Fifty branches of ten levels. One warm-up request first, so the bound measures the read, not the first call's JIT.
    // Rewritten without the owners (D-16, BR-OT-10): Get_FiveHundredNodesTenLevels_ReturnsWithinTwoSeconds_EveryNodeResolved
    // -> Get_FiveHundredNodesTenLevels_ReturnsWithinTwoSeconds.
    [Fact]
    public async Task Get_FiveHundredNodesTenLevels_ReturnsWithinTwoSeconds()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);

        await OrganizationsTestData.InUnitOfWorkAsync(factory, async (services, ct) =>
        {
            var areas = services.GetRequiredService<IAreaRepository>();
            var root = await areas.LockRootAsync(structure.ProjectId, forUpdate: false, ct);

            for (var branch = 0; branch < 50; branch++)
            {
                var parent = root;

                for (var level = 1; level <= 10; level++)
                {
                    var child = Area.CreateChild(parent, OrganizationsTestData.Name($"Узел {branch:D2}-{level:D2}"));
                    areas.Add(child);
                    parent = child;
                }
            }

            await areas.SaveAsync(ct);

            return true;
        });

        Assert.Equal(HttpStatusCode.OK, (await GetAsync(structure)).Status);

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var answer = await GetAsync(structure);
        stopwatch.Stop();

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(2), $"The 501-node tree took {stopwatch.Elapsed}.");

        var nodes = Nodes(answer);
        Assert.Equal(501, nodes.Count);
        Assert.Equal(10, nodes.Max(node => node.Depth));
    }
}
