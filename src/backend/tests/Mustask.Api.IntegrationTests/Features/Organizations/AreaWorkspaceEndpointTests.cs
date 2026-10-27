using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Mustask.Api.IntegrationTests.Features.WorkItems;
using Mustask.Domain.Identity;

namespace Mustask.Api.IntegrationTests.Features.Organizations;

/// <summary>
/// mod-018 US-1, US-2, US-4 (the item's «Область» is gone from the answers) and US-6 (containment, roles, the order of answers):
/// the organization's area tree through the real host and a real PostgreSQL. Every test builds its own organization
/// (<see cref="OrganizationsTestData"/>): the database is shared and never emptied.
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

    private static string Area(Guid organizationId, Guid areaId) => $"{OrganizationsApi.Areas(organizationId)}/{areaId}";

    private static IEnumerable<string> PropertyNames(JsonElement element) =>
        element.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal);

    // US-1 · "An administrator creates a top-level area and a child under any area": «Отчёты» at the top, «Квартальные» under it; the
    // answers are the 201 Area with its path, and the tree lists both in pre-order with their depth.
    [Fact]
    public async Task Create_TopLevelAndChild_Returns201_TheTreeShowsThePaths()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var organization = structure.OrganizationId;

        var reports = await CreateAsync(organization, new { name = " Отчёты ", description = " Всё про отчёты\n" });
        var reportsId = reports.Body.GetProperty("id").GetGuid();
        var quarterly = await CreateAsync(organization, new { name = "Квартальные", parentId = reportsId.ToString() });
        var quarterlyId = quarterly.Body.GetProperty("id").GetGuid();

        Assert.Equal(HttpStatusCode.Created, reports.Status);
        Assert.Equal("application/json", reports.MediaType);
        Assert.Equal($"{OrganizationsApi.Areas(organization)}/{reportsId}", reports.Location?.OriginalString);
        Assert.Equal(["description", "id", "name", "parentId", "path"], PropertyNames(reports.Body));
        Assert.Equal(("Отчёты", " Всё про отчёты\n", "Отчёты"), (
            reports.Body.GetProperty("name").GetString(),
            reports.Body.GetProperty("description").GetString(),
            reports.Body.GetProperty("path").GetString()));
        Assert.Equal(JsonValueKind.Null, reports.Body.GetProperty("parentId").ValueKind);
        Assert.Equal(HttpStatusCode.Created, quarterly.Status);
        Assert.Equal(("Отчёты / Квартальные", reportsId), (quarterly.Body.GetProperty("path").GetString(), quarterly.Body.GetProperty("parentId").GetGuid()));

        var tree = await SendAsync(HttpMethod.Get, OrganizationsApi.Areas(organization));
        Assert.Equal(HttpStatusCode.OK, tree.Status);
        Assert.Equal(["id", "parentId", "name", "description", "path", "depth"], tree.Body[0].EnumerateObject().Select(property => property.Name));
        Assert.Equal(
            [("Отчёты", 1), ("Отчёты / Квартальные", 2)],
            tree.Body.EnumerateArray().Select(node => (node.GetProperty("path").GetString()!, node.GetProperty("depth").GetInt32())));
    }

    // US-1 · "The creation inherits nothing it does not name": «Наследовать доступ» is stored and appears in no answer (OQ-2 B).
    [Fact]
    public async Task Create_AnswersNoInheritsAccess_AndTheTreeEither()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);

        var created = await CreateAsync(structure.OrganizationId, new { name = "Отчёты", inheritsAccess = false });
        var tree = await SendAsync(HttpMethod.Get, OrganizationsApi.Areas(structure.OrganizationId));

        Assert.False(created.Body.TryGetProperty("inheritsAccess", out _));
        Assert.DoesNotContain("inheritsAccess", tree.Body[0].EnumerateObject().Select(property => property.Name));
        await using var scope = factory.Services.CreateAsyncScope();
        var stored = await scope.ServiceProvider.GetRequiredService<Mustask.Infrastructure.MustaskDbContext>().Areas
            .AsNoTracking()
            .SingleAsync(area => area.Id == created.Body.GetProperty("id").GetGuid());
        Assert.True(stored.InheritsAccess);
    }

    // US-1 · "Sibling names are unique, the top level included": the same name in another case, with spaces, is taken; the same name
    // under another parent is free.
    [Fact]
    public async Task Create_SiblingName_IsTakenIgnoringCaseAndSpaces_FreeUnderAnotherParent()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var organization = structure.OrganizationId;
        var reports = await OrganizationsTestData.AddAreaAsync(factory, organization, null, "Отчёты");
        await OrganizationsTestData.AddAreaAsync(factory, organization, reports.Id, "Квартальные");

        var topClash = await CreateAsync(organization, new { name = "  ОТЧЁТЫ " });
        var childClash = await CreateAsync(organization, new { name = "квартальные", parentId = reports.Id.ToString() });
        var other = await OrganizationsTestData.AddAreaAsync(factory, organization, null, "Склад");
        var free = await CreateAsync(organization, new { name = "Квартальные", parentId = other.Id.ToString() });

        LinksTestData.AssertRefused(topClash, HttpStatusCode.Conflict, "organizations.area.name_taken");
        LinksTestData.AssertRefused(childClash, HttpStatusCode.Conflict, "organizations.area.name_taken");
        Assert.Equal(HttpStatusCode.Created, free.Status);
    }

    // US-1 · "A name of 1 to 100 characters, a description of at most 2 000": every reason at once. BR-AW-15: a new child's parent is its address, so a missing
    // parent is answered 404 first (QA fixed the stale expectation: the test used to expect the 400 before the parent).
    [Fact]
    public async Task Create_InvalidValues_Returns400WithEveryReason_AMissingParentIsAnsweredFirst()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);

        var answer = await CreateAsync(structure.OrganizationId, new
        {
            name = "   ",
            description = new string('я', 2001),
            parentId = (string?)null,
        });

        var parentFirst = await CreateAsync(structure.OrganizationId, new { name = "   ", parentId = Guid.CreateVersion7().ToString() });
        LinksTestData.AssertRefused(parentFirst, HttpStatusCode.NotFound, "organizations.area.not_found");

        LinksTestData.AssertRefused(answer, HttpStatusCode.BadRequest, "organizations.validation_failed");
        var errors = answer.Body.GetProperty("errors");
        Assert.Equal(["description", "name"], PropertyNames(errors));
        Assert.Equal("organizations.name_length", errors.GetProperty("name")[0].GetProperty("code").GetString());
        Assert.Equal("organizations.description_length", errors.GetProperty("description")[0].GetProperty("code").GetString());
    }

    // US-1 · "The tree is at most ten levels deep": the eleventh level is refused with its own code and nothing is written.
    [Fact]
    public async Task Create_TheEleventhLevel_Returns409DepthExceeded_WritesNothing()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var organization = structure.OrganizationId;
        Guid? parent = null;

        for (var level = 1; level <= 10; level++)
        {
            parent = (await OrganizationsTestData.AddAreaAsync(factory, organization, parent, $"Уровень {level}")).Id;
        }

        var answer = await CreateAsync(organization, new { name = "Одиннадцатый", parentId = parent!.Value.ToString() });

        LinksTestData.AssertRefused(answer, HttpStatusCode.Conflict, "organizations.area.depth_exceeded");
        Assert.Equal("Дерево областей не может быть глубже 10 уровней", answer.Detail);
        Assert.Equal(10, (await OrganizationsTestData.ReadTreeAsync(factory, organization)).Count);
    }

    // US-1 · "A parent that is no area of the organization": malformed, unknown, another organization's — the same 404.
    [Theory]
    [InlineData("malformed")]
    [InlineData("unknown")]
    [InlineData("of another organization")]
    public async Task Create_ParentNotOfTheOrganization_Returns404AreaNotFound(string how)
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var other = await OrganizationsTestData.CreateStructureAsync(factory);
        var foreign = await OrganizationsTestData.AddAreaAsync(factory, other.OrganizationId, null, "Чужая");
        var parent = how switch { "malformed" => "x", "unknown" => Guid.CreateVersion7().ToString(), _ => foreign.Id.ToString() };

        var answer = await CreateAsync(structure.OrganizationId, new { name = "Отчёты", parentId = parent });

        LinksTestData.AssertRefused(answer, HttpStatusCode.NotFound, "organizations.area.not_found");
        Assert.Equal("Область не найдена.", answer.Detail);
        Assert.Equal(0, (await OrganizationsTestData.ReadTreeAsync(factory, structure.OrganizationId)).Count);
    }

    // US-1 · "Every member reads the tree and an area's page": the page carries the parent with its path and the direct children.
    [Fact]
    public async Task GetArea_Page_CarriesTheParentAndTheChildren_ForAViewer()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var organization = structure.OrganizationId;
        var reports = await OrganizationsTestData.AddAreaAsync(factory, organization, null, "Отчёты");
        var quarterly = await OrganizationsTestData.AddAreaAsync(factory, organization, reports.Id, "Квартальные");
        var viewer = await MembershipTestData.NewMemberAsync(factory, organization, OrganizationRole.Viewer);

        var tree = await SendAsync(HttpMethod.Get, OrganizationsApi.Areas(organization), token: viewer.Token);
        var parentPage = await SendAsync(HttpMethod.Get, Area(organization, reports.Id), token: viewer.Token);
        var childPage = await SendAsync(HttpMethod.Get, Area(organization, quarterly.Id), token: viewer.Token);

        Assert.Equal(HttpStatusCode.OK, tree.Status);
        Assert.Equal(["children", "description", "id", "name", "parent", "parentId", "path"], PropertyNames(childPage.Body));
        Assert.Equal(
            (reports.Id, "Отчёты", "Отчёты / Квартальные"),
            (childPage.Body.GetProperty("parent").GetProperty("id").GetGuid(), childPage.Body.GetProperty("parent").GetProperty("path").GetString(), childPage.Body.GetProperty("path").GetString()));
        Assert.Equal(JsonValueKind.Null, parentPage.Body.GetProperty("parent").ValueKind);
        Assert.Equal(quarterly.Id, Assert.Single(parentPage.Body.GetProperty("children").EnumerateArray()).GetProperty("id").GetGuid());
    }

    // US-2 · "An area is renamed": the new name keeps its place, its children's paths follow, and a sibling's name is refused.
    [Fact]
    public async Task Rename_ChangesThePaths_RefusesATakenName()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var organization = structure.OrganizationId;
        var reports = await OrganizationsTestData.AddAreaAsync(factory, organization, null, "Отчёты");
        var quarterly = await OrganizationsTestData.AddAreaAsync(factory, organization, reports.Id, "Квартальные");
        await OrganizationsTestData.AddAreaAsync(factory, organization, null, "Склад");

        var renamed = await SendAsync(HttpMethod.Put, $"{Area(organization, reports.Id)}/name", new { name = " Аналитика " });
        var taken = await SendAsync(HttpMethod.Put, $"{Area(organization, reports.Id)}/name", new { name = "склад" });
        var invalid = await SendAsync(HttpMethod.Put, $"{Area(organization, reports.Id)}/name", new { name = "" });

        Assert.Equal(HttpStatusCode.OK, renamed.Status);
        Assert.Equal("Аналитика", renamed.Body.GetProperty("name").GetString());
        LinksTestData.AssertRefused(taken, HttpStatusCode.Conflict, "organizations.area.name_taken");
        LinksTestData.AssertRefused(invalid, HttpStatusCode.BadRequest, "organizations.validation_failed");
        Assert.Equal("Аналитика / Квартальные", (await OrganizationsTestData.ReadTreeAsync(factory, organization)).PathOf(quarterly.Id));
    }

    // US-2 · "A description is set and cleared apart from the name": kept as typed, an empty one clears it, the name stays.
    [Fact]
    public async Task SetDescription_KeepsItAsTyped_EmptyClears_NameStays()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var reports = await OrganizationsTestData.AddAreaAsync(factory, structure.OrganizationId, null, "Отчёты");
        var route = $"{Area(structure.OrganizationId, reports.Id)}/description";

        var set = await SendAsync(HttpMethod.Put, route, new { description = " Всё про отчёты\n" });
        var cleared = await SendAsync(HttpMethod.Put, route, new { description = "" });
        var tooLong = await SendAsync(HttpMethod.Put, route, new { description = new string('я', 2001) });

        Assert.Equal(" Всё про отчёты\n", set.Body.GetProperty("description").GetString());
        Assert.Equal(JsonValueKind.Null, cleared.Body.GetProperty("description").ValueKind);
        Assert.Equal("Отчёты", cleared.Body.GetProperty("name").GetString());
        LinksTestData.AssertRefused(tooLong, HttpStatusCode.BadRequest, "organizations.validation_failed");
    }

    // US-2 · "An area is moved with its subtree": under another area, to the top level; the same parent is a success that
    // changes nothing; a move under itself or its own descendant, a taken name and a too-deep result are 409s.
    [Fact]
    public async Task Move_UnderAnotherAreaAndToTheTop_RefusesTheCycleTheNameAndTheDepth()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var organization = structure.OrganizationId;
        var reports = await OrganizationsTestData.AddAreaAsync(factory, organization, null, "Отчёты");
        var quarterly = await OrganizationsTestData.AddAreaAsync(factory, organization, reports.Id, "Квартальные");
        var deep = await OrganizationsTestData.AddAreaAsync(factory, organization, quarterly.Id, "Глубже");
        var store = await OrganizationsTestData.AddAreaAsync(factory, organization, null, "Склад");

        var moved = await SendAsync(HttpMethod.Put, $"{Area(organization, quarterly.Id)}/parent", new { parentId = store.Id.ToString() });
        var same = await SendAsync(HttpMethod.Put, $"{Area(organization, quarterly.Id)}/parent", new { parentId = store.Id.ToString() });
        var cycle = await SendAsync(HttpMethod.Put, $"{Area(organization, store.Id)}/parent", new { parentId = deep.Id.ToString() });
        var self = await SendAsync(HttpMethod.Put, $"{Area(organization, store.Id)}/parent", new { parentId = store.Id.ToString() });
        var toTop = await SendAsync(HttpMethod.Put, $"{Area(organization, deep.Id)}/parent", new { parentId = (string?)null });

        Assert.Equal(HttpStatusCode.OK, moved.Status);
        Assert.Equal("Склад / Квартальные", moved.Body.GetProperty("path").GetString());
        Assert.Equal(HttpStatusCode.OK, same.Status);
        LinksTestData.AssertRefused(cycle, HttpStatusCode.Conflict, "organizations.area.move_into_own_subtree");
        LinksTestData.AssertRefused(self, HttpStatusCode.Conflict, "organizations.area.move_into_own_subtree");
        Assert.Equal(HttpStatusCode.OK, toTop.Status);
        Assert.Equal("Глубже", toTop.Body.GetProperty("path").GetString());

        var twin = await OrganizationsTestData.AddAreaAsync(factory, organization, reports.Id, "Склад");
        var nameTaken = await SendAsync(HttpMethod.Put, $"{Area(organization, store.Id)}/parent", new { parentId = reports.Id.ToString() });
        _ = twin;
        LinksTestData.AssertRefused(nameTaken, HttpStatusCode.Conflict, "organizations.area.name_taken");
    }

    // US-2 · "A move is judged by the depth of the whole subtree": a nine-level chain moved under a level-two area is too deep.
    [Fact]
    public async Task Move_ASubtreeThatWouldPassTenLevels_Returns409DepthExceeded()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var organization = structure.OrganizationId;
        Guid? parent = null;
        Guid top = default;

        for (var level = 1; level <= 9; level++)
        {
            parent = (await OrganizationsTestData.AddAreaAsync(factory, organization, parent, $"Цепь {level}")).Id;
            top = level == 1 ? parent.Value : top;
        }

        var anchor = await OrganizationsTestData.AddAreaAsync(factory, organization, null, "Якорь");
        var child = await OrganizationsTestData.AddAreaAsync(factory, organization, anchor.Id, "Второй");

        var answer = await SendAsync(HttpMethod.Put, $"{Area(organization, top)}/parent", new { parentId = child.Id.ToString() });

        LinksTestData.AssertRefused(answer, HttpStatusCode.Conflict, "organizations.area.depth_exceeded");
    }

    // US-2 · "An area with children is not deleted": 409 not_empty with its text; a leaf is deleted (204) and its links end with it.
    [Fact]
    public async Task Delete_WithChildren_Returns409NotEmpty_ALeafGoesWithItsLinks()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var organization = structure.OrganizationId;
        var reports = await OrganizationsTestData.AddAreaAsync(factory, organization, null, "Отчёты");
        var quarterly = await OrganizationsTestData.AddAreaAsync(factory, organization, reports.Id, "Квартальные");
        await OrganizationsTestData.LinkAreaToProjectAsync(factory, organization, quarterly.Id, structure.ProjectId);
        await OrganizationsTestData.LinkAreaToTeamAsync(factory, organization, quarterly.Id, structure.Platform.Id);

        var refused = await SendAsync(HttpMethod.Delete, Area(organization, reports.Id));
        var deletion = await SendAsync(HttpMethod.Get, $"{Area(organization, quarterly.Id)}/deletion");
        var deleted = await SendAsync(HttpMethod.Delete, Area(organization, quarterly.Id));
        var again = await SendAsync(HttpMethod.Delete, Area(organization, quarterly.Id));

        LinksTestData.AssertRefused(refused, HttpStatusCode.Conflict, "organizations.area.not_empty");
        Assert.Equal("Нельзя удалить область, в которой есть вложенные области", refused.Detail);
        Assert.Equal(["projects", "teams"], PropertyNames(deletion.Body));
        Assert.Equal(structure.ProjectId, Assert.Single(deletion.Body.GetProperty("projects").EnumerateArray()).GetProperty("id").GetGuid());
        Assert.Equal(structure.Platform.Id, Assert.Single(deletion.Body.GetProperty("teams").EnumerateArray()).GetProperty("id").GetGuid());
        Assert.Equal(HttpStatusCode.NoContent, deleted.Status);
        LinksTestData.AssertRefused(again, HttpStatusCode.NotFound, "organizations.area.not_found");
        var projectAreas = await SendAsync(HttpMethod.Get, OrganizationsApi.ProjectAreas(organization, structure.ProjectId));
        Assert.Empty(projectAreas.Body.EnumerateArray());
        Assert.Equal(1, (await OrganizationsTestData.ReadTreeAsync(factory, organization)).Count);
    }

    // US-2 · "An area owns nothing": renaming, moving and deleting areas change no item, no team and no project.
    [Fact]
    public async Task AreaEdits_ChangeNoItemNoTeamNoProject()
    {
        var world = await WorkItemsTestData.CreateWorldAsync(factory);
        var item = await WorkItemsTestData.CreateItemAsync(factory, world, "TASK", "Задача");
        var before = await WorkItemsTestData.ReadAsync(factory, world, item);
        var reports = await OrganizationsTestData.AddAreaAsync(factory, world.OrganizationId, null, "Отчёты");
        var store = await OrganizationsTestData.AddAreaAsync(factory, world.OrganizationId, null, "Склад");
        await OrganizationsTestData.LinkAreaToProjectAsync(factory, world.OrganizationId, reports.Id, world.ProjectId);

        await SendAsync(HttpMethod.Put, $"{Area(world.OrganizationId, reports.Id)}/name", new { name = "Аналитика" });
        await SendAsync(HttpMethod.Put, $"{Area(world.OrganizationId, reports.Id)}/parent", new { parentId = store.Id.ToString() });
        await SendAsync(HttpMethod.Delete, Area(world.OrganizationId, reports.Id));

        Assert.Equal(before.GetRawText(), (await WorkItemsTestData.ReadAsync(factory, world, item)).GetRawText());
    }

    // US-4 · "The item has no «Область»": no answer carries an area; `areaId` in a save is ignored and nothing is stored.
    [Fact]
    public async Task WorkItem_CarriesNoArea_AnAreaIdInASaveIsIgnored()
    {
        var world = await WorkItemsTestData.CreateWorldAsync(factory);
        var item = await WorkItemsTestData.CreateItemAsync(factory, world, "BUG", "Баг");
        var area = await OrganizationsTestData.AddAreaAsync(factory, world.OrganizationId, null, "Отчёты");

        var saved = await WorkItemsTestData.SaveAsync(factory, world, item, new { title = "Баг 2", areaId = area.Id.ToString() });
        var read = await WorkItemsTestData.ReadAsync(factory, world, item);
        var list = await WorkItemsTestData.ListAsync(factory, world);

        Assert.Equal(HttpStatusCode.OK, saved.Status);
        Assert.False(saved.Body.TryGetProperty("area", out _));
        Assert.False(read.TryGetProperty("area", out _));
        Assert.False(list.Body.GetProperty("data")[0].TryGetProperty("area", out _));
        Assert.Equal("Баг 2", read.GetProperty("title").GetString());
    }

    // US-4 · "The per-project routes are gone": a create, a rename and a move under a project's address are no longer routes.
    [Fact]
    public async Task ProjectNestedAreaRoutes_AreGone()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var area = await OrganizationsTestData.AddAreaAsync(factory, structure.OrganizationId, null, "Отчёты");
        var nested = OrganizationsApi.ProjectAreas(structure.OrganizationId, structure.ProjectId);

        var rename = await SendAsync(HttpMethod.Put, $"{nested}/{area.Id}/name", new { name = "Иная" });
        var move = await SendAsync(HttpMethod.Put, $"{nested}/{area.Id}/parent", new { parentId = (string?)null });

        Assert.All(new[] { rename, move }, answer => Assert.True(answer.Status is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed or HttpStatusCode.BadRequest));
        Assert.Equal("Отчёты", (await OrganizationsTestData.ReadTreeAsync(factory, structure.OrganizationId))[area.Id].Name);
    }

    // US-6 · "A non-member sees nothing": the organization is not found, whatever the route.
    [Fact]
    public async Task EveryAreaRoute_ANonMember_Returns404OrganizationNotFound()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var area = await OrganizationsTestData.AddAreaAsync(factory, structure.OrganizationId, null, "Отчёты");
        var stranger = MembershipTestData.Outsider();

        foreach (var (method, path, body) in Routes(structure.OrganizationId, area.Id))
        {
            var answer = await SendAsync(method, path, body, stranger);

            LinksTestData.AssertRefused(answer, HttpStatusCode.NotFound, "organizations.organization.not_found");
        }
    }

    // US-6 · "A member and a viewer read and write nothing": reads are open to every role; the writes answer 403 for both, and the
    // tree is unchanged. Until mod-019 only the administrator holds area.write, area.create and area.link.
    [Theory]
    [InlineData(OrganizationRole.Member)]
    [InlineData(OrganizationRole.Viewer)]
    public async Task Writes_ForAMemberAndAViewer_Return403_ReadsStayOpen(OrganizationRole role)
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var organization = structure.OrganizationId;
        var area = await OrganizationsTestData.AddAreaAsync(factory, organization, null, "Отчёты");
        var user = await MembershipTestData.NewMemberAsync(factory, organization, role);

        foreach (var (method, path, body) in Routes(organization, area.Id))
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

        Assert.Equal(1, (await OrganizationsTestData.ReadTreeAsync(factory, organization)).Count);
    }

    // US-6 · "The second codes come first": a top-level creation without area.create is 403 before the body is read.
    [Fact]
    public async Task Create_ForAMember_Returns403_BeforeTheBodyIsRead()
    {
        var structure = await OrganizationsTestData.CreateStructureAsync(factory);
        var member = await MembershipTestData.NewMemberAsync(factory, structure.OrganizationId, OrganizationRole.Member);

        var answer = await CreateAsync(structure.OrganizationId, new { name = "", parentId = "x" }, member.Token);

        LinksTestData.AssertRefused(answer, HttpStatusCode.Forbidden, "organizations.access.forbidden");
    }

    // US-6 · "An area of another organization is the same 404 as an unknown one", on every route that names an area, and nothing
    // of the other organization changes.
    [Fact]
    public async Task EveryAreaRoute_AnAreaOfAnotherOrganization_Returns404AreaNotFound_ChangesNothing()
    {
        var mine = await OrganizationsTestData.CreateStructureAsync(factory);
        var theirs = await OrganizationsTestData.CreateStructureAsync(factory);
        var foreign = await OrganizationsTestData.AddAreaAsync(factory, theirs.OrganizationId, null, "Чужая");
        var before = (await OrganizationsTestData.ReadTreeAsync(factory, theirs.OrganizationId)).PreOrder().ToList();

        foreach (var (method, path, body) in Routes(mine.OrganizationId, foreign.Id))
        {
            var answer = await SendAsync(method, path, body);

            LinksTestData.AssertRefused(answer, HttpStatusCode.NotFound, "organizations.area.not_found");
        }

        Assert.Equal(before, (await OrganizationsTestData.ReadTreeAsync(factory, theirs.OrganizationId)).PreOrder().ToList());
    }

    /// <summary>Every route that names one area of the organization, with a body that would be valid.</summary>
    private static IEnumerable<(HttpMethod Method, string Path, object? Body)> Routes(Guid organizationId, Guid areaId)
    {
        var area = Area(organizationId, areaId);
        yield return (HttpMethod.Get, area, null);
        yield return (HttpMethod.Put, $"{area}/name", new { name = "Новое имя" });
        yield return (HttpMethod.Put, $"{area}/description", new { description = "Описание" });
        yield return (HttpMethod.Put, $"{area}/parent", new { parentId = (string?)null });
        yield return (HttpMethod.Get, $"{area}/deletion", null);
        yield return (HttpMethod.Get, $"{area}/projects", null);
        yield return (HttpMethod.Get, $"{area}/teams", null);
        yield return (HttpMethod.Post, $"{area}/projects", new { projectIds = new[] { Guid.CreateVersion7().ToString() } });
        yield return (HttpMethod.Post, $"{area}/teams", new { teamIds = new[] { Guid.CreateVersion7().ToString() } });
        yield return (HttpMethod.Delete, $"{area}/projects/{Guid.CreateVersion7()}", null);
        yield return (HttpMethod.Delete, $"{area}/teams/{Guid.CreateVersion7()}", null);
        yield return (HttpMethod.Delete, area, null);
    }
}
