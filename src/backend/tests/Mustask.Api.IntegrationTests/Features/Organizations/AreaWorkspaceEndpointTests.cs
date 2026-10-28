using System.Net;
using System.Text.Json;
using Mustask.Api.IntegrationTests.Features.WorkItems;
using Mustask.Domain.Identity;
using Mustask.Domain.Organizations;

namespace Mustask.Api.IntegrationTests.Features.Organizations;

/// <summary>
/// mod-018 (the typed single tree) through the real host and a real PostgreSQL: departments, project nodes and sub-areas — what each
/// route does with each type (OQ-3 A, OQ-5 A, OQ-7 A, OQ-10 A), the read-only nodes of a deactivated project (OQ-6 A), the roles and the
/// order of answers (US-6). Every test builds its own organization: the database is shared and never emptied.
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "RequiresDocker")]
[Collection(MustaskApiCollection.Name)]
public sealed class AreaWorkspaceEndpointTests(MustaskApiFactory factory)
{
    private Task<Answer> SendAsync(HttpMethod method, string path, object? body = null, string? token = null) =>
        OrganizationsApi.SendAsync(factory, method, path, body, token ?? OrganizationsTokens.SignedIn());

    private Task<Answer> CreateAsync(Guid organizationId, object body, string? token = null) =>
        SendAsync(HttpMethod.Post, OrganizationsApi.Areas(organizationId), body, token);

    private Task<Answer> MoveAsync(Guid organizationId, Guid areaId, Guid? parentId) =>
        SendAsync(HttpMethod.Put, $"{Area(organizationId, areaId)}/parent", new { parentId = parentId?.ToString() });

    private static string Area(Guid organizationId, Guid areaId) => $"{OrganizationsApi.Areas(organizationId)}/{areaId}";

    private static IEnumerable<string> PropertyNames(JsonElement element) => element.EnumerateObject().Select(property => property.Name);

    // US-1 · a department at the top, a sub-area below a project's node: the answers carry the type, the project and the path.
    [Fact]
    public async Task Create_DepartmentAndSubArea_Returns201_TheTreeShowsTypesAndPaths()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var organization = structure.OrganizationId;
        var node = await OrganizationsTestData.NodeOfAsync(factory, structure.ProjectId);

        var department = await CreateAsync(organization, new { name = " Разработка ", description = " Всё про разработку\n" });
        var departmentId = department.Body.GetProperty("id").GetGuid();
        var sub = await CreateAsync(organization, new { name = "Отчёты", parentId = node.Id.ToString() });

        Assert.Equal(HttpStatusCode.Created, department.Status);
        Assert.Equal($"{OrganizationsApi.Areas(organization)}/{departmentId}", department.Location?.OriginalString);
        Assert.Equal(["id", "parentId", "type", "projectId", "name", "description", "path"], PropertyNames(department.Body));
        Assert.Equal(("department", "Разработка", " Всё про разработку\n"), (
            department.Body.GetProperty("type").GetString(),
            department.Body.GetProperty("name").GetString(),
            department.Body.GetProperty("description").GetString()));
        Assert.Equal(JsonValueKind.Null, department.Body.GetProperty("projectId").ValueKind);
        Assert.Equal(HttpStatusCode.Created, sub.Status);
        Assert.Equal(("area", structure.ProjectId, node.Id, "Пример проекта / Отчёты"), (
            sub.Body.GetProperty("type").GetString(),
            sub.Body.GetProperty("projectId").GetGuid(),
            sub.Body.GetProperty("parentId").GetGuid(),
            sub.Body.GetProperty("path").GetString()));

        var tree = await SendAsync(HttpMethod.Get, OrganizationsApi.Areas(organization));
        Assert.Equal(HttpStatusCode.OK, tree.Status);
        Assert.Equal(
            ["id", "parentId", "type", "projectId", "projectActive", "name", "description", "path", "depth"],
            PropertyNames(tree.Body[0]));
        Assert.Equal(
            new[] { ("Пример проекта", "project", 1), ("Пример проекта / Отчёты", "area", 2), ("Разработка", "department", 1) }.Order().ToList(),
            tree.Body.EnumerateArray()
                .Select(n => (n.GetProperty("path").GetString()!, n.GetProperty("type").GetString()!, n.GetProperty("depth").GetInt32()))
                .Order()
                .ToList());
    }

    // OQ-7 A · a sub-area never stands below a department; a missing parent is answered before the values.
    [Fact]
    public async Task Create_UnderADepartment_IsTypeNotAllowed_AMissingParentIsAnsweredFirst()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var department = await OrganizationsTestData.AddDepartmentAsync(factory, structure.OrganizationId, "Разработка");

        var underDepartment = await CreateAsync(structure.OrganizationId, new { name = "Отчёты", parentId = department.Id.ToString() });
        var invalidAndMissing = await CreateAsync(structure.OrganizationId, new { name = "", parentId = Guid.CreateVersion7().ToString() });

        LinksTestData.AssertRefused(underDepartment, HttpStatusCode.Conflict, "organizations.area.type_not_allowed");
        LinksTestData.AssertRefused(invalidAndMissing, HttpStatusCode.NotFound, "organizations.area.not_found");
    }

    // OQ-12 A · one name space per level: a department and a project node share the top level; a name is free under another parent.
    [Fact]
    public async Task Create_SiblingName_IsTakenIgnoringCaseAndSpaces_AcrossTypes()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var organization = structure.OrganizationId;
        var node = await OrganizationsTestData.NodeOfAsync(factory, structure.ProjectId);
        var other = await OrganizationsTestData.CreateProjectAsync(factory, organization, "Другой", "OTH");
        var otherNode = await OrganizationsTestData.NodeOfAsync(factory, other.Project.Id);
        await OrganizationsTestData.AddSubAreaAsync(factory, organization, node.Id, "Отчёты");

        var sameLevel = await CreateAsync(organization, new { name = "отчёты ", parentId = node.Id.ToString() });
        var otherParent = await CreateAsync(organization, new { name = "Отчёты", parentId = otherNode.Id.ToString() });
        var clashWithNode = await CreateAsync(organization, new { name = "пример проекта" });

        LinksTestData.AssertRefused(sameLevel, HttpStatusCode.Conflict, "organizations.area.name_taken");
        Assert.Equal(HttpStatusCode.Created, otherParent.Status);
        LinksTestData.AssertRefused(clashWithNode, HttpStatusCode.Conflict, "organizations.area.name_taken");
    }

    // BR-AW-4 · the tree is at most 10 levels deep: a node on level 10 takes no child.
    [Fact]
    public async Task Create_TheEleventhLevel_Returns409DepthExceeded_WritesNothing()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var parent = await OrganizationsTestData.NodeOfAsync(factory, structure.ProjectId);

        for (var level = 2; level <= 10; level++)
        {
            parent = await OrganizationsTestData.AddSubAreaAsync(factory, structure.OrganizationId, parent.Id, $"Уровень {level}");
        }

        var answer = await CreateAsync(structure.OrganizationId, new { name = "Одиннадцатый", parentId = parent.Id.ToString() });

        LinksTestData.AssertRefused(answer, HttpStatusCode.Conflict, "organizations.area.depth_exceeded");
        Assert.Equal(10, (await OrganizationsTestData.ReadOrganizationTreeAsync(factory, structure.OrganizationId)).Count);
    }

    // US-3 · a node's page: the type, the parent with its path, the direct children with their types.
    [Fact]
    public async Task GetArea_Page_CarriesTheTypeTheParentAndTheChildren_ForAViewer()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var organization = structure.OrganizationId;
        var node = await OrganizationsTestData.NodeOfAsync(factory, structure.ProjectId);
        var sub = await OrganizationsTestData.AddSubAreaAsync(factory, organization, node.Id, "Квартальные");
        var viewer = await MembershipTestData.NewMemberAsync(factory, organization, OrganizationRole.Viewer);

        var page = await SendAsync(HttpMethod.Get, Area(organization, node.Id), token: viewer.Token);
        var leaf = await SendAsync(HttpMethod.Get, Area(organization, sub.Id), token: viewer.Token);

        Assert.Equal(HttpStatusCode.OK, page.Status);
        Assert.Equal(("project", structure.ProjectId), (page.Body.GetProperty("type").GetString(), page.Body.GetProperty("projectId").GetGuid()));
        Assert.True(page.Body.GetProperty("projectActive").GetBoolean());
        var child = Assert.Single(page.Body.GetProperty("children").EnumerateArray());
        Assert.Equal(("area", "Квартальные"), (child.GetProperty("type").GetString(), child.GetProperty("name").GetString()));
        Assert.Equal(
            ("Пример проекта", node.Id),
            (leaf.Body.GetProperty("parent").GetProperty("name").GetString(), leaf.Body.GetProperty("parent").GetProperty("id").GetGuid()));
    }

    // OQ-10 A · a sub-area and a department are renamed on their own; a project's node only with its project.
    [Fact]
    public async Task Rename_SubAreaAndDepartment_Work_ProjectNodeIsRootIsFixed_ATakenNameIsRefused()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var organization = structure.OrganizationId;
        var node = await OrganizationsTestData.NodeOfAsync(factory, structure.ProjectId);
        var sub = await OrganizationsTestData.AddSubAreaAsync(factory, organization, node.Id, "Квартальные");
        await OrganizationsTestData.AddSubAreaAsync(factory, organization, node.Id, "Годовые");
        var department = await OrganizationsTestData.AddDepartmentAsync(factory, organization, "Разработка");

        var renamedSub = await SendAsync(HttpMethod.Put, $"{Area(organization, sub.Id)}/name", new { name = "Месячные" });
        var renamedDepartment = await SendAsync(HttpMethod.Put, $"{Area(organization, department.Id)}/name", new { name = "Продукт" });
        var nodeRename = await SendAsync(HttpMethod.Put, $"{Area(organization, node.Id)}/name", new { name = "Иное" });
        var taken = await SendAsync(HttpMethod.Put, $"{Area(organization, sub.Id)}/name", new { name = "годовые" });

        Assert.Equal(
            ("Пример проекта / Месячные", "Продукт"),
            (renamedSub.Body.GetProperty("path").GetString(), renamedDepartment.Body.GetProperty("path").GetString()));
        LinksTestData.AssertRefused(nodeRename, HttpStatusCode.Conflict, "organizations.area.root_is_fixed");
        LinksTestData.AssertRefused(taken, HttpStatusCode.Conflict, "organizations.area.name_taken");
    }

    // OQ-10 A · a project's rename carries its name to its node.
    [Fact]
    public async Task RenameProject_RenamesItsNode()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);

        var renamed = await SendAsync(
            HttpMethod.Put, $"{OrganizationsApi.Project(structure.OrganizationId, structure.ProjectId)}/name", new { name = "Новое имя" });
        var node = await OrganizationsTestData.NodeOfAsync(factory, structure.ProjectId);

        Assert.Equal(HttpStatusCode.OK, renamed.Status);
        Assert.Equal("Новое имя", node.Name);
    }

    [Fact]
    public async Task SetDescription_KeepsItAsTyped_EmptyClears_NameStays_ProjectNodeIsDescribable()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var organization = structure.OrganizationId;
        var node = await OrganizationsTestData.NodeOfAsync(factory, structure.ProjectId);

        var set = await SendAsync(HttpMethod.Put, $"{Area(organization, node.Id)}/description", new { description = " О проекте\n" });
        var cleared = await SendAsync(HttpMethod.Put, $"{Area(organization, node.Id)}/description", new { description = "" });

        Assert.Equal(" О проекте\n", set.Body.GetProperty("description").GetString());
        Assert.Equal(JsonValueKind.Null, cleared.Body.GetProperty("description").ValueKind);
        Assert.Equal("Пример проекта", cleared.Body.GetProperty("name").GetString());
    }

    // US-3 · a sub-area moves with its subtree; a project's node moves between departments and the top; the rules of the types.
    [Fact]
    public async Task Move_SubAreaAndProjectNode_AndTheTypeRules()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var organization = structure.OrganizationId;
        var node = await OrganizationsTestData.NodeOfAsync(factory, structure.ProjectId);
        var reports = await OrganizationsTestData.AddSubAreaAsync(factory, organization, node.Id, "Отчёты");
        var quarterly = await OrganizationsTestData.AddSubAreaAsync(factory, organization, reports.Id, "Квартальные");
        var archive = await OrganizationsTestData.AddSubAreaAsync(factory, organization, node.Id, "Архив");
        var development = await OrganizationsTestData.AddDepartmentAsync(factory, organization, "Разработка");
        var product = await OrganizationsTestData.AddDepartmentAsync(factory, organization, "Продукт");
        var other = await OrganizationsTestData.CreateProjectAsync(factory, organization, "Другой", "OTH");
        var otherNode = await OrganizationsTestData.NodeOfAsync(factory, other.Project.Id);

        var subMove = await MoveAsync(organization, reports.Id, archive.Id);
        var nodeIntoDepartment = await MoveAsync(organization, node.Id, development.Id);
        var nodeToProduct = await MoveAsync(organization, node.Id, product.Id);
        var nodeToTop = await MoveAsync(organization, node.Id, null);
        var departmentMove = await MoveAsync(organization, development.Id, product.Id);
        var subToTop = await MoveAsync(organization, archive.Id, null);
        var subUnderDepartment = await MoveAsync(organization, archive.Id, development.Id);
        var subOutside = await MoveAsync(organization, archive.Id, otherNode.Id);
        var cycle = await MoveAsync(organization, reports.Id, quarterly.Id);
        var nodeUnderSub = await MoveAsync(organization, node.Id, archive.Id);

        Assert.Equal("Пример проекта / Архив / Отчёты", subMove.Body.GetProperty("path").GetString());
        Assert.Equal("Разработка / Пример проекта", nodeIntoDepartment.Body.GetProperty("path").GetString());
        Assert.Equal("Продукт / Пример проекта", nodeToProduct.Body.GetProperty("path").GetString());
        Assert.Equal(JsonValueKind.Null, nodeToTop.Body.GetProperty("parentId").ValueKind);
        LinksTestData.AssertRefused(departmentMove, HttpStatusCode.Conflict, "organizations.area.type_not_allowed");
        LinksTestData.AssertRefused(subToTop, HttpStatusCode.Conflict, "organizations.area.type_not_allowed");
        LinksTestData.AssertRefused(subUnderDepartment, HttpStatusCode.Conflict, "organizations.area.type_not_allowed");
        LinksTestData.AssertRefused(subOutside, HttpStatusCode.Conflict, "organizations.area.outside_project");
        LinksTestData.AssertRefused(cycle, HttpStatusCode.Conflict, "organizations.area.move_into_own_subtree");
        LinksTestData.AssertRefused(nodeUnderSub, HttpStatusCode.Conflict, "organizations.area.move_into_own_subtree");
    }

    // US-3 · "The tree is at most 10 levels deep after a move": a subtree that would pass level 10 is refused.
    [Fact]
    public async Task Move_ASubtreeThatWouldPassTenLevels_Returns409DepthExceeded()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var organization = structure.OrganizationId;
        var node = await OrganizationsTestData.NodeOfAsync(factory, structure.ProjectId);
        var branch = node;

        for (var level = 2; level <= 9; level++)
        {
            branch = await OrganizationsTestData.AddSubAreaAsync(factory, organization, branch.Id, $"Уровень {level}");
        }

        var moving = await OrganizationsTestData.AddSubAreaAsync(factory, organization, node.Id, "Переезжающая");
        await OrganizationsTestData.AddSubAreaAsync(factory, organization, moving.Id, "Дочерняя");

        var answer = await MoveAsync(organization, moving.Id, branch.Id);

        LinksTestData.AssertRefused(answer, HttpStatusCode.Conflict, "organizations.area.depth_exceeded");
    }

    // US-2 · the rules of deletion by type: a project's node is root_is_fixed, a non-empty node is not_empty, a leaf and an empty department go.
    [Fact]
    public async Task Delete_TheRulesOfEachType()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var organization = structure.OrganizationId;
        var node = await OrganizationsTestData.NodeOfAsync(factory, structure.ProjectId);
        var parent = await OrganizationsTestData.AddSubAreaAsync(factory, organization, node.Id, "Отчёты");
        var leaf = await OrganizationsTestData.AddSubAreaAsync(factory, organization, parent.Id, "Квартальные");
        var empty = await OrganizationsTestData.AddDepartmentAsync(factory, organization, "Пустой");
        var team = await OrganizationsTestData.AddUnlinkedTeamAsync(factory, organization, "Марс");
        await OrganizationsTestData.LinkAreaToTeamAsync(factory, organization, empty.Id, team.Id);
        var busy = await OrganizationsTestData.AddDepartmentAsync(factory, organization, "Занятой");
        await MoveAsync(organization, node.Id, busy.Id);

        var withChild = await SendAsync(HttpMethod.Delete, Area(organization, parent.Id));
        var nodeDelete = await SendAsync(HttpMethod.Delete, Area(organization, node.Id));
        var busyDelete = await SendAsync(HttpMethod.Delete, Area(organization, busy.Id));
        var leafDelete = await SendAsync(HttpMethod.Delete, Area(organization, leaf.Id));
        var preview = await SendAsync(HttpMethod.Get, $"{Area(organization, empty.Id)}/deletion");
        var emptyDelete = await SendAsync(HttpMethod.Delete, Area(organization, empty.Id));

        LinksTestData.AssertRefused(withChild, HttpStatusCode.Conflict, "organizations.area.not_empty");
        LinksTestData.AssertRefused(nodeDelete, HttpStatusCode.Conflict, "organizations.area.root_is_fixed");
        LinksTestData.AssertRefused(busyDelete, HttpStatusCode.Conflict, "organizations.area.not_empty");
        Assert.Equal(HttpStatusCode.NoContent, leafDelete.Status);
        Assert.Equal("Марс", Assert.Single(preview.Body.GetProperty("teams").EnumerateArray()).GetProperty("name").GetString());
        Assert.Equal(HttpStatusCode.NoContent, emptyDelete.Status);
        Assert.Equal(3, (await OrganizationsTestData.ReadOrganizationTreeAsync(factory, organization)).Count);
    }

    // OQ-6 A · a deactivated project's node stays in the tree, marked; its writes answer «Проект не найден».
    [Fact]
    public async Task DeactivatedProject_NodeIsMarked_AndReadOnly()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var organization = structure.OrganizationId;
        var node = await OrganizationsTestData.NodeOfAsync(factory, structure.ProjectId);
        var sub = await OrganizationsTestData.AddSubAreaAsync(factory, organization, node.Id, "Отчёты");
        var archive = await OrganizationsTestData.AddSubAreaAsync(factory, organization, node.Id, "Архив");
        await OrganizationsTestData.SetProjectStatusAsync(factory, organization, structure.ProjectId, ProjectStatus.Deactivated);

        var tree = await SendAsync(HttpMethod.Get, OrganizationsApi.Areas(organization));
        var create = await CreateAsync(organization, new { name = "Новая", parentId = node.Id.ToString() });
        var rename = await SendAsync(HttpMethod.Put, $"{Area(organization, sub.Id)}/name", new { name = "Иная" });
        var move = await MoveAsync(organization, sub.Id, archive.Id);
        var delete = await SendAsync(HttpMethod.Delete, Area(organization, sub.Id));

        Assert.Equal(3, tree.Body.GetArrayLength());
        Assert.All(tree.Body.EnumerateArray(), n => Assert.False(n.GetProperty("projectActive").GetBoolean()));

        foreach (var answer in new[] { create, rename, move, delete })
        {
            LinksTestData.AssertRefused(answer, HttpStatusCode.NotFound, "organizations.project.not_found");
        }
    }

    // OQ-5 A · a project is created under a department or at the top with its node; the parent must be a department.
    [Fact]
    public async Task CreateProject_WithAParentArea_PutsItsNodeThere()
    {
        var organization = await OrganizationsTestData.CreateOrganizationAsync(factory);
        var department = await OrganizationsTestData.AddDepartmentAsync(factory, organization.Id, "Разработка");
        var projects = $"/api/organizations/{organization.Id}/projects";

        var under = await SendAsync(HttpMethod.Post, projects, new { name = "Мастаск", key = "MST", parentAreaId = department.Id.ToString() });
        var top = await SendAsync(HttpMethod.Post, projects, new { name = "Верхний", key = "TOP" });
        var node = await OrganizationsTestData.NodeOfAsync(factory, under.Body.GetProperty("id").GetGuid());
        var notDepartment = await SendAsync(HttpMethod.Post, projects, new { name = "Третий", key = "THR", parentAreaId = node.Id.ToString() });
        var clash = await SendAsync(HttpMethod.Post, projects, new { name = "разработка", key = "CLS" });
        var missing = await SendAsync(HttpMethod.Post, projects, new { name = "Пятый", key = "FIV", parentAreaId = Guid.CreateVersion7().ToString() });

        Assert.Equal((HttpStatusCode.Created, HttpStatusCode.Created), (under.Status, top.Status));
        Assert.Equal(department.Id, node.ParentId);
        LinksTestData.AssertRefused(notDepartment, HttpStatusCode.Conflict, "organizations.area.type_not_allowed");
        LinksTestData.AssertRefused(clash, HttpStatusCode.Conflict, "organizations.area.name_taken");
        LinksTestData.AssertRefused(missing, HttpStatusCode.NotFound, "organizations.area.not_found");
    }

    // US-4 · the old project-area link routes are gone.
    [Fact]
    public async Task ProjectAreaLinkRoutes_AreGone()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var organization = structure.OrganizationId;
        var node = await OrganizationsTestData.NodeOfAsync(factory, structure.ProjectId);
        var some = Guid.CreateVersion7().ToString();

        var routes = new (HttpMethod, string, object?)[]
        {
            (HttpMethod.Get, $"{Area(organization, node.Id)}/projects", null),
            (HttpMethod.Post, $"{Area(organization, node.Id)}/projects", new { projectIds = new[] { some } }),
            (HttpMethod.Delete, $"{Area(organization, node.Id)}/projects/{some}", null),
            (HttpMethod.Post, OrganizationsApi.ProjectAreas(organization, structure.ProjectId), new { areaIds = new[] { some } }),
            (HttpMethod.Delete, $"{OrganizationsApi.ProjectAreas(organization, structure.ProjectId)}/{some}", null),
        };

        foreach (var (method, path, body) in routes)
        {
            var answer = await SendAsync(method, path, body);

            Assert.True(answer.Status is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed, $"{method} {path}: {answer.Status}");
        }
    }

    // US-3 · a project's subtree: the node first, then its sub-areas; a deactivated project is 404 on its own route.
    [Fact]
    public async Task ProjectAreas_ListsTheSubtree_ADeactivatedProjectIsNotFound()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var organization = structure.OrganizationId;
        var node = await OrganizationsTestData.NodeOfAsync(factory, structure.ProjectId);
        await OrganizationsTestData.AddSubAreaAsync(factory, organization, node.Id, "Отчёты");

        var list = await SendAsync(HttpMethod.Get, OrganizationsApi.ProjectAreas(organization, structure.ProjectId));
        await OrganizationsTestData.SetProjectStatusAsync(factory, organization, structure.ProjectId, ProjectStatus.Deactivated);
        var deactivated = await SendAsync(HttpMethod.Get, OrganizationsApi.ProjectAreas(organization, structure.ProjectId));

        Assert.Equal(
            [("project", 1), ("area", 2)],
            list.Body.EnumerateArray().Select(n => (n.GetProperty("type").GetString()!, n.GetProperty("depth").GetInt32())));
        LinksTestData.AssertRefused(deactivated, HttpStatusCode.NotFound, "organizations.project.not_found");
    }

    // US-6 · no token: 401 on every route before anything else.
    [Fact]
    public async Task EveryAreaRoute_WithNoToken_Returns401()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);

        foreach (var (method, path, body) in Routes(structure.OrganizationId, Guid.CreateVersion7(), structure.ProjectId, structure.Platform.Id))
        {
            var answer = await OrganizationsApi.SendAsync(factory, method, path, body, token: null);

            Assert.True(answer.Status == HttpStatusCode.Unauthorized, $"{method} {path}: {answer.Status}");
        }
    }

    // US-6 · a non-member sees nothing: the organization is not found, whatever the route.
    [Fact]
    public async Task EveryAreaRoute_ANonMember_Returns404OrganizationNotFound()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var area = await OrganizationsTestData.AddDepartmentAsync(factory, structure.OrganizationId, "Отчёты");

        foreach (var (method, path, body) in Routes(structure.OrganizationId, area.Id, structure.ProjectId, structure.Platform.Id))
        {
            var answer = await SendAsync(method, path, body, MembershipTestData.Outsider());

            LinksTestData.AssertRefused(answer, HttpStatusCode.NotFound, "organizations.organization.not_found");
        }
    }

    // US-6 · a member and a viewer read and write nothing: reads stay open, writes answer 403 and nothing changes.
    [Theory]
    [InlineData(OrganizationRole.Member)]
    [InlineData(OrganizationRole.Viewer)]
    public async Task Writes_ForAMemberAndAViewer_Return403_ReadsStayOpen(OrganizationRole role)
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var organization = structure.OrganizationId;
        var area = await OrganizationsTestData.AddDepartmentAsync(factory, organization, "Отчёты");
        var user = await MembershipTestData.NewMemberAsync(factory, organization, role);

        foreach (var (method, path, body) in Routes(organization, area.Id, structure.ProjectId, structure.Platform.Id))
        {
            var answer = await SendAsync(method, path, body, user.Token);

            if (method == HttpMethod.Get && !path.EndsWith("/deletion", StringComparison.Ordinal))
            {
                Assert.Equal(HttpStatusCode.OK, answer.Status);
            }
            else
            {
                LinksTestData.AssertRefused(answer, HttpStatusCode.Forbidden, "organizations.access.forbidden");
            }
        }

        Assert.Equal(2, (await OrganizationsTestData.ReadOrganizationTreeAsync(factory, organization)).Count);
    }

    // US-6 · an area of another organization is the same 404 as an unknown one on every route that names an area.
    [Fact]
    public async Task EveryAreaRoute_AnAreaOfAnotherOrganization_Returns404AreaNotFound()
    {
        var mine = await OrganizationsTestData.CreateStructureAsync(factory);
        var theirs = await OrganizationsTestData.CreateStructureAsync(factory);
        var foreign = await OrganizationsTestData.AddDepartmentAsync(factory, theirs.OrganizationId, "Чужая");
        var before = (await OrganizationsTestData.ReadOrganizationTreeAsync(factory, theirs.OrganizationId)).PreOrder().ToList();

        foreach (var (method, path, body) in AreaRoutes(mine.OrganizationId, foreign.Id))
        {
            LinksTestData.AssertRefused(await SendAsync(method, path, body), HttpStatusCode.NotFound, "organizations.area.not_found");
        }

        Assert.Equal(before, (await OrganizationsTestData.ReadOrganizationTreeAsync(factory, theirs.OrganizationId)).PreOrder().ToList());
    }

    // US-6 · a missing area is 403 for a viewer and 404 for an administrator.
    [Fact]
    public async Task AMissingArea_IsA403ForAViewer_A404ForAnAdministrator()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var organization = structure.OrganizationId;
        var viewer = await MembershipTestData.NewMemberAsync(factory, organization, OrganizationRole.Viewer);
        var missing = Area(organization, Guid.CreateVersion7());

        foreach (var (method, path, body) in new (HttpMethod, string, object?)[]
        {
            (HttpMethod.Put, $"{missing}/name", new { name = "Новое" }),
            (HttpMethod.Put, $"{missing}/parent", new { parentId = (string?)null }),
            (HttpMethod.Delete, missing, null),
            (HttpMethod.Post, OrganizationsApi.Areas(organization), new { name = "Дочерняя", parentId = Guid.CreateVersion7().ToString() }),
        })
        {
            LinksTestData.AssertRefused(await SendAsync(method, path, body, viewer.Token), HttpStatusCode.Forbidden, "organizations.access.forbidden");
        }

        LinksTestData.AssertRefused(await SendAsync(HttpMethod.Delete, missing), HttpStatusCode.NotFound, "organizations.area.not_found");
    }

    // BR-AW-4 · the description counts UTF-16 units: 1 000 emoji are kept, 1 001 are refused.
    [Fact]
    public async Task Description_IsCountedInUtf16Units()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var department = await OrganizationsTestData.AddDepartmentAsync(factory, structure.OrganizationId, "Отчёты");
        var path = $"{Area(structure.OrganizationId, department.Id)}/description";

        var kept = await SendAsync(HttpMethod.Put, path, new { description = string.Concat(Enumerable.Repeat("😀", 1000)) });
        var refused = await SendAsync(HttpMethod.Put, path, new { description = string.Concat(Enumerable.Repeat("😀", 1001)) });

        Assert.Equal(HttpStatusCode.OK, kept.Status);
        LinksTestData.AssertRefused(refused, HttpStatusCode.BadRequest, "organizations.validation_failed");
    }

    private static IEnumerable<(HttpMethod Method, string Path, object? Body)> AreaRoutes(Guid organizationId, Guid areaId)
    {
        var area = Area(organizationId, areaId);
        yield return (HttpMethod.Get, area, null);
        yield return (HttpMethod.Put, $"{area}/name", new { name = "Новое имя" });
        yield return (HttpMethod.Put, $"{area}/description", new { description = "Описание" });
        yield return (HttpMethod.Put, $"{area}/parent", new { parentId = (string?)null });
        yield return (HttpMethod.Get, $"{area}/deletion", null);
        yield return (HttpMethod.Get, $"{area}/teams", null);
        yield return (HttpMethod.Post, $"{area}/teams", new { teamIds = new[] { Guid.CreateVersion7().ToString() } });
        yield return (HttpMethod.Delete, $"{area}/teams/{Guid.CreateVersion7()}", null);
        yield return (HttpMethod.Delete, area, null);
    }

    /// <summary>Every route of the typed tree, with a body that would be valid.</summary>
    private static IEnumerable<(HttpMethod Method, string Path, object? Body)> Routes(Guid organizationId, Guid areaId, Guid projectId, Guid teamId)
    {
        yield return (HttpMethod.Get, OrganizationsApi.Areas(organizationId), null);
        yield return (HttpMethod.Post, OrganizationsApi.Areas(organizationId), new { name = "Новая" });

        foreach (var route in AreaRoutes(organizationId, areaId))
        {
            yield return route;
        }

        yield return (HttpMethod.Get, OrganizationsApi.ProjectAreas(organizationId, projectId), null);
        yield return (HttpMethod.Get, OrganizationsApi.TeamAreas(organizationId, teamId), null);
        yield return (HttpMethod.Post, OrganizationsApi.TeamAreas(organizationId, teamId), new { areaIds = new[] { areaId.ToString() } });
        yield return (HttpMethod.Delete, $"{OrganizationsApi.TeamAreas(organizationId, teamId)}/{areaId}", null);
    }
}
