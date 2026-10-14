using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Mustask.Application.Abstractions.Organizations;
using Mustask.Application.Features.Organizations;
using Mustask.Domain.Organizations;

namespace Mustask.Api.IntegrationTests.Features.Organizations;

/// <summary>
/// <c>GET /api/organizations/{organizationId}/projects/{projectId}/areas</c>
/// (api-contract § 4.1) through the real host and a real PostgreSQL: the
/// project's area tree in pre-order with paths, depths and resolved owners, and
/// the in-process seam's answer for the same areas. Every test builds its own
/// organization (<see cref="OrganizationsTestData"/>): the database is shared
/// and never emptied.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class GetAreaTreeEndpointTests(MustaskApiFactory factory)
{
    private const string FirstTeamName = "Пример проекта";

    /// <summary>A team as a node of the tree names it.</summary>
    private sealed record TeamRef(Guid Id, string Name);

    /// <summary>One node of the answer, read back into plain values.</summary>
    private sealed record NodeView(
        Guid Id,
        Guid? ParentId,
        string Name,
        string Path,
        int Depth,
        Guid? OwnerTeamId,
        TeamRef ResolvedOwner,
        bool OwnerInherited,
        TeamRef? DefaultOfTeam);

    /// <summary>
    /// The tree most tests read: a project «Пример проекта» whose first team has the same name, a second team «Платформа»
    /// with its area, and
    /// <code>
    /// Пример проекта      root, owned by the first team, its default
    ///   архив             inherits the first team
    ///   Отчёты            owned by «Платформа»
    ///     Годовые         owned by the first team
    ///     Квартальные     inherits «Платформа»
    ///       Март          inherits «Платформа»
    ///   Платформа         owned by «Платформа», its default
    /// </code>
    /// </summary>
    private sealed record SampleTree(OrganizationStructure Structure, Area Reports, Area Quarterly, Area March, Area Yearly, Area Archive);

    private async Task<SampleTree> CreateSampleTreeAsync()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var reports = await OrganizationsTestData.AddAreaAsync(factory, structure.ProjectId, structure.RootId, "Отчёты", structure.Platform.Team.Id);
        var quarterly = await OrganizationsTestData.AddAreaAsync(factory, structure.ProjectId, reports.Id, "Квартальные");
        var march = await OrganizationsTestData.AddAreaAsync(factory, structure.ProjectId, quarterly.Id, "Март");
        var yearly = await OrganizationsTestData.AddAreaAsync(factory, structure.ProjectId, reports.Id, "Годовые", structure.FirstTeamId);
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
                node.GetProperty("depth").GetInt32(),
                NullableGuid(node.GetProperty("ownerTeamId")),
                TeamOf(node.GetProperty("resolvedOwner"))!,
                node.GetProperty("ownerInherited").GetBoolean(),
                TeamOf(node.GetProperty("defaultOfTeam"))))
            .ToList();
    }

    private static Guid? NullableGuid(JsonElement element) =>
        element.ValueKind == JsonValueKind.Null ? null : element.GetGuid();

    private static TeamRef? TeamOf(JsonElement element) =>
        element.ValueKind == JsonValueKind.Null
            ? null
            : new TeamRef(element.GetProperty("id").GetGuid(), element.GetProperty("name").GetString()!);

    private static IEnumerable<string> PropertyNames(JsonElement element) =>
        element.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal);

    // US-5 · "tree registry": the whole tree in pre-order (each node before its children, siblings in display order, the root
    // first), each node with its path (DC-9), its depth, its explicit owner, its resolved owner and the team whose default it is.
    [Fact]
    public async Task Get_ReturnsPreOrderNodes_WithPathsDepthsAndResolvedOwners()
    {
        var tree = await CreateSampleTreeAsync();
        var structure = tree.Structure;
        var first = new TeamRef(structure.FirstTeamId, FirstTeamName);
        var platform = new TeamRef(structure.Platform.Team.Id, "Платформа");

        var answer = await GetAsync(structure);

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Equal("application/json", answer.MediaType);
        Assert.Equal(
            [
                new NodeView(structure.RootId, null, FirstTeamName, FirstTeamName, 0, structure.FirstTeamId, first, false, first),
                new NodeView(tree.Archive.Id, structure.RootId, "архив", "архив", 1, null, first, true, null),
                new NodeView(tree.Reports.Id, structure.RootId, "Отчёты", "Отчёты", 1, platform.Id, platform, false, null),
                new NodeView(tree.Yearly.Id, tree.Reports.Id, "Годовые", "Отчёты / Годовые", 2, structure.FirstTeamId, first, false, null),
                new NodeView(tree.Quarterly.Id, tree.Reports.Id, "Квартальные", "Отчёты / Квартальные", 2, null, platform, true, null),
                new NodeView(tree.March.Id, tree.Quarterly.Id, "Март", "Отчёты / Квартальные / Март", 3, null, platform, true, null),
                new NodeView(structure.Platform.Area.Id, structure.RootId, "Платформа", "Платформа", 1, platform.Id, platform, false, platform),
            ],
            Nodes(answer));
    }

    // api-contract § 4.1: the exact shape of a node and of the team objects it holds.
    [Fact]
    public async Task Get_NodeShape_IsExactlyTheContracts()
    {
        var tree = await CreateSampleTreeAsync();

        var answer = await GetAsync(tree.Structure);

        var root = answer.Body.EnumerateArray().First();
        var march = answer.Body.EnumerateArray().Single(node => node.GetProperty("id").GetGuid() == tree.March.Id);
        Assert.Equal(
            ["defaultOfTeam", "depth", "id", "name", "ownerInherited", "ownerTeamId", "parentId", "path", "resolvedOwner"],
            PropertyNames(root));
        Assert.Equal(["id", "name"], PropertyNames(root.GetProperty("resolvedOwner")));
        Assert.Equal(["id", "name"], PropertyNames(root.GetProperty("defaultOfTeam")));
        Assert.Equal(JsonValueKind.Null, root.GetProperty("parentId").ValueKind);
        Assert.Equal(JsonValueKind.Null, march.GetProperty("ownerTeamId").ValueKind);
        Assert.Equal(JsonValueKind.Null, march.GetProperty("defaultOfTeam").ValueKind);
    }

    // US-5 · "root comes with the project" and "root only": a new project's tree is its root alone, named after the project,
    // owned by the first team and that team's default.
    [Fact]
    public async Task Get_NewProject_IsTheRootAlone()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var project = await OrganizationsTestData.CreateProjectAsync(factory, organization.Id);
        var first = new TeamRef(project.FirstTeam.Id, FirstTeamName);

        var answer = await GetAsync(organization.Id, project.Project.Id.ToString());

        Assert.Equal(HttpStatusCode.OK, answer.Status);
        Assert.Equal(
            [new NodeView(project.Root.Id, null, FirstTeamName, FirstTeamName, 0, project.FirstTeam.Id, first, false, first)],
            Nodes(answer));
    }

    // US-6 · "An area inherits its owner from the nearest owned ancestor": the inherited ones are marked, the resolved owner is
    // the nearest explicit one, and the root, owned by the first team, is the fallback of everything beneath it.
    [Fact]
    public async Task Get_InheritedOwnersMarked_RootFallsBackToFirstTeam()
    {
        var tree = await CreateSampleTreeAsync();
        var structure = tree.Structure;

        var nodes = Nodes(await GetAsync(structure)).ToDictionary(node => node.Id);

        var root = nodes[structure.RootId];
        Assert.Equal(structure.FirstTeamId, root.OwnerTeamId);
        Assert.False(root.OwnerInherited);

        var archive = nodes[tree.Archive.Id];
        Assert.Null(archive.OwnerTeamId);
        Assert.True(archive.OwnerInherited);
        Assert.Equal(new TeamRef(structure.FirstTeamId, FirstTeamName), archive.ResolvedOwner);

        var march = nodes[tree.March.Id];
        Assert.Null(march.OwnerTeamId);
        Assert.True(march.OwnerInherited);
        Assert.Equal(new TeamRef(structure.Platform.Team.Id, "Платформа"), march.ResolvedOwner);

        Assert.Equal(structure.FirstTeamId, nodes[tree.Yearly.Id].ResolvedOwner.Id);
        Assert.False(nodes[tree.Yearly.Id].OwnerInherited);
        Assert.All(nodes.Values, node => Assert.Equal(node.OwnerTeamId is null, node.OwnerInherited));
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
        Assert.Equal(["альфа", "Бета", "Ёлка", "Ель", "Платформа"], siblings);
    }

    // US-7 · "Deleted, areas pass to the first team": the tree shows the first team as the explicit owner of what the deleted
    // team owned, the inherited ones follow, and no area is a deleted team's default any more.
    [Fact]
    public async Task Get_AfterTeamDeleted_ItsAreasBelongToTheFirstTeam_NoDefaultOfIt()
    {
        var tree = await CreateSampleTreeAsync();
        var structure = tree.Structure;
        await OrganizationsTestData.DeleteTeamAsync(factory, structure.ProjectId, structure.Platform.Team.Id);
        var first = new TeamRef(structure.FirstTeamId, FirstTeamName);

        var nodes = Nodes(await GetAsync(structure)).ToDictionary(node => node.Id);

        Assert.Equal(7, nodes.Count);
        Assert.Equal(structure.FirstTeamId, nodes[tree.Reports.Id].OwnerTeamId);
        Assert.Equal(structure.FirstTeamId, nodes[structure.Platform.Area.Id].OwnerTeamId);
        Assert.Equal(first, nodes[tree.March.Id].ResolvedOwner);
        Assert.True(nodes[tree.March.Id].OwnerInherited);
        Assert.All(nodes.Values, node => Assert.Equal(first, node.ResolvedOwner));
        Assert.Null(nodes[structure.Platform.Area.Id].DefaultOfTeam);
        Assert.Equal(first, nodes[structure.RootId].DefaultOfTeam);
        Assert.Single(nodes.Values, node => node.DefaultOfTeam is not null);
    }

    // US-6 · "other modules get the same answer as the tree": for every node of the tree, the seam's owner of that area is the
    // tree's resolved owner, with the same inherited mark (plan-backend § 11).
    [Fact]
    public async Task Seam_FindAreaOwner_EqualsTheTreesResolvedOwner_ForEveryNode()
    {
        var tree = await CreateSampleTreeAsync();
        var structure = tree.Structure;
        var nodes = Nodes(await GetAsync(structure));
        using var scope = factory.Services.CreateScope();
        var seam = scope.ServiceProvider.GetRequiredService<IOrganizationStructure>();

        Assert.Equal(7, nodes.Count);
        foreach (var node in nodes)
        {
            var owner = await seam.FindAreaOwnerAsync(node.Id, CancellationToken.None);

            Assert.Equal(new AreaOwnerInfo(node.Id, structure.ProjectId, node.ResolvedOwner.Id, node.OwnerInherited, IsReachable: true), owner);
        }
    }

    // US-6 · "other modules get the same answer as the tree": the root the seam gives for a project is the tree's first node,
    // and it is owned by the project's first team (BR-M1-14: where an item with no team context goes).
    [Fact]
    public async Task Seam_ProjectRootArea_IsTheTreesRoot()
    {
        var tree = await CreateSampleTreeAsync();
        var structure = tree.Structure;
        var nodes = Nodes(await GetAsync(structure));
        using var scope = factory.Services.CreateScope();
        var seam = scope.ServiceProvider.GetRequiredService<IOrganizationStructure>();

        var project = await seam.FindProjectAsync(structure.ProjectId, CancellationToken.None);

        Assert.NotNull(project);
        Assert.Equal(nodes[0].Id, project.RootAreaId);
        Assert.Null(nodes[0].ParentId);
        Assert.Equal(nodes[0].OwnerTeamId, project.FirstTeamId);
        Assert.Single(nodes, node => node.ParentId is null);
    }

    // ADR-0026 rule 4 against rule 5: beneath a deactivated project the tree answers not found, while the seam still names
    // the area's owner and says it is unreachable.
    [Fact]
    public async Task Seam_FindAreaOwner_UnderDeactivatedProject_IsUnreachable_WhereTheTreeAnswers404()
    {
        var tree = await CreateSampleTreeAsync();
        var structure = tree.Structure;
        var before = Nodes(await GetAsync(structure)).Single(node => node.Id == tree.March.Id);
        await OrganizationsTestData.SetProjectStatusAsync(factory, structure.OrganizationId, structure.ProjectId, ProjectStatus.Deactivated);
        using var scope = factory.Services.CreateScope();
        var seam = scope.ServiceProvider.GetRequiredService<IOrganizationStructure>();

        var answer = await GetAsync(structure);
        var owner = await seam.FindAreaOwnerAsync(tree.March.Id, CancellationToken.None);

        Assert.Equal(HttpStatusCode.NotFound, answer.Status);
        Assert.Equal(new AreaOwnerInfo(tree.March.Id, structure.ProjectId, before.ResolvedOwner.Id, before.OwnerInherited, IsReachable: false), owner);
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
        Assert.Equal(2, Nodes(answer).Count);
    }

    // NFR Volumes (QA pass 1, A-18): an area tree of 500 nodes and 10 levels returns over HTTP within 2 s, every node with its
    // resolved owner. The read model's query count is pinned by ReadModelsTests; this is the end-to-end bound the NFR names.
    // Fifty branches of ten levels, each branch's top owned explicitly by «Платформа» or by the first team in turn, the rest
    // inheriting. One warm-up request first, so the bound measures the read, not the first call's JIT.
    [Fact]
    public async Task Get_FiveHundredNodesTenLevels_ReturnsWithinTwoSeconds_EveryNodeResolved()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);

        await OrganizationsTestData.InUnitOfWorkAsync(factory, async (services, ct) =>
        {
            var areas = services.GetRequiredService<IAreaRepository>();
            var teams = services.GetRequiredService<ITeamRepository>();
            var root = await areas.LockRootAsync(structure.ProjectId, forUpdate: false, ct);
            var platform = await teams.LockForShareAsync(structure.ProjectId, structure.Platform.Team.Id, ct);
            var first = await teams.LockForShareAsync(structure.ProjectId, structure.FirstTeamId, ct);

            for (var branch = 0; branch < 50; branch++)
            {
                var parent = root;

                for (var level = 1; level <= 10; level++)
                {
                    var owner = level == 1 ? (branch % 2 == 0 ? platform : first) : null;
                    var child = Area.CreateChild(parent, OrganizationsTestData.Name($"Узел {branch:D2}-{level:D2}"), owner);
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
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(2), $"The 502-node tree took {stopwatch.Elapsed}.");

        var nodes = Nodes(answer);
        Assert.Equal(502, nodes.Count);
        Assert.Equal(10, nodes.Max(node => node.Depth));
        Assert.All(nodes, node => Assert.Contains(node.ResolvedOwner.Id, new[] { structure.FirstTeamId, structure.Platform.Team.Id }));
        Assert.Equal(250, nodes.Count(node => node.Depth > 0 && node.ResolvedOwner.Id == structure.Platform.Team.Id && node.Name.StartsWith("Узел", StringComparison.Ordinal)));
    }
}
